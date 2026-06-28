// <copyright file="InterconnectionViewLayoutStrategy.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Engine;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Rendering.Internal;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Internal;

namespace DemaConsulting.SysML2Tools.Layout.Internal;

/// <summary>
/// Layout strategy for Interconnection View diagrams. Shows the internal structure of a single part
/// definition: its nested part usages as boxes placed by the force-directed engine, ports on the box
/// boundaries assigned by <see cref="PortAssigner"/>, and connection usages routed as orthogonal
/// connector lines between the ports.
/// </summary>
internal sealed class InterconnectionViewLayoutStrategy : ILayoutStrategy
{
    /// <summary>Minimum width of a nested part box.</summary>
    private const double MinPartWidth = 110.0;

    /// <summary>Approximate width-per-character factor relative to font size.</summary>
    private const double CharWidthFactor = 0.62;

    /// <summary>Nominal spacing between adjacent part centres in the force layout.</summary>
    private const double PartSpacing = 150.0;

    /// <summary>Clearance kept between routed connectors and part boxes.</summary>
    private const double ConnectorClearance = 10.0;

    /// <summary>A nested part usage with its computed box size.</summary>
    private sealed record PartItem(string Name, string Keyword, string? Typing, double Width, double Height);

    /// <summary>A resolved binary connection between two nested part indices.</summary>
    private sealed record ConnPair(int A, int B);

    /// <inheritdoc/>
    public LayoutTree BuildLayout(ViewContext context, RenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        var theme = options.Theme;

        // Choose the part definition whose internals to show: the one with the most connections.
        var root = FindRoot(context.Workspace);
        if (root is null)
        {
            return new LayoutTree(200.0, 100.0, []);
        }

        var parts = CollectParts(root, theme);
        if (parts.Count == 0)
        {
            return new LayoutTree(200.0, 100.0, []);
        }

        var partIndex = BuildPartIndex(parts);
        var pairs = ResolveConnections(root, partIndex);

        // Place the part boxes with the force-directed engine using connections as springs.
        var force = ForceDirectedEngine.Place(
            [.. parts.Select(p => new ForceNode(p.Width, p.Height))],
            [.. pairs.Select(c => new ForceEdge(c.A, c.B))],
            spacing: PartSpacing,
            padding: theme.LabelPadding * 4.0);

        // Offset the placed parts to sit below the container title area.
        var titleArea = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true);
        var offsetX = theme.LabelPadding * 2.0;
        var offsetY = titleArea + (theme.LabelPadding * 2.0);

        var partRects = new Rect[parts.Count];
        for (var i = 0; i < parts.Count; i++)
        {
            var r = force.Rects[i];
            partRects[i] = new Rect(r.X + offsetX, r.Y + offsetY, r.Width, r.Height);
        }

        // Gravity-compress overlaps to a tight minimum gap, then quantise to the grid so part
        // anchors fall on predictable lines before ports and connectors are routed.
        partRects = CompressAndQuantize(partRects, theme);

        var nodes = new List<LayoutNode>();

        // Container box for the root part definition, sized to fit the compressed/quantised parts.
        var contentRight = parts.Count == 0 ? force.Width : partRects.Max(r => r.X + r.Width);
        var contentBottom = parts.Count == 0 ? force.Height : partRects.Max(r => r.Y + r.Height);
        var containerWidth = Math.Max(force.Width + (offsetX * 2.0), contentRight + offsetX);
        var containerHeight = Math.Max(offsetY + force.Height + (theme.LabelPadding * 2.0), contentBottom + (theme.LabelPadding * 2.0));
        nodes.Add(new LayoutBox(
            X: 0,
            Y: 0,
            Width: containerWidth,
            Height: containerHeight,
            Label: root.Name ?? "Interconnection",
            Depth: 0,
            Shape: BoxShape.Rectangle,
            Compartments: [],
            Children: [],
            Keyword: string.IsNullOrEmpty(root.DefinitionKeyword) ? "part def" : root.DefinitionKeyword));

        // Part usage boxes (rounded — they are usages).
        for (var i = 0; i < parts.Count; i++)
        {
            nodes.Add(MakePartBox(parts[i], partRects[i]));
        }

        // Ports and connectors.
        var crossings = AddPortsAndConnectors(parts, partRects, pairs, nodes);

        var warnings = LayoutWarnings.ForCrossings(context.ViewName, crossings);
        return new LayoutTree(containerWidth, containerHeight, nodes) { Warnings = warnings };
    }

    /// <summary>
    /// Finds the part definition whose interior to render: the non-stdlib <c>part def</c> with the
    /// most connection usages, falling back to the one with the most part usages.
    /// </summary>
    private static SysmlDefinitionNode? FindRoot(SysmlWorkspace workspace)
    {
        SysmlDefinitionNode? best = null;
        var bestConnections = -1;
        var bestParts = -1;

        foreach (var (qualifiedName, node) in workspace.Declarations)
        {
            if (node is not SysmlDefinitionNode def || def.DefinitionKeyword != "part def")
            {
                continue;
            }

            if (StdlibFilter.IsStdlibElement(qualifiedName, workspace.StdlibNames))
            {
                continue;
            }

            var connections = def.Children.OfType<SysmlConnectionNode>().Count();
            var partCount = def.Children.OfType<SysmlFeatureNode>().Count(f => f.FeatureKeyword == "part");

            if (connections > bestConnections || (connections == bestConnections && partCount > bestParts))
            {
                best = def;
                bestConnections = connections;
                bestParts = partCount;
            }
        }

        return best;
    }

    /// <summary>Collects the nested part usages of the root definition, sized for rendering.</summary>
    private static IReadOnlyList<PartItem> CollectParts(SysmlDefinitionNode root, Theme theme)
    {
        var result = new List<PartItem>();
        foreach (var feature in root.Children.OfType<SysmlFeatureNode>())
        {
            if (feature.FeatureKeyword != "part")
            {
                continue;
            }

            var name = feature.Name ?? feature.FeatureTyping ?? "part";
            var (width, height) = ComputePartSize(name, feature.FeatureTyping, theme);
            result.Add(new PartItem(name, "part", feature.FeatureTyping, width, height));
        }

        return result;
    }

    /// <summary>Builds a name → index lookup for the nested parts.</summary>
    private static Dictionary<string, int> BuildPartIndex(IReadOnlyList<PartItem> parts)
    {
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < parts.Count; i++)
        {
            index.TryAdd(parts[i].Name, i);
        }

        return index;
    }

    /// <summary>
    /// Resolves each binary connection's endpoints to nested-part indices by matching the first
    /// segment of the dotted endpoint reference against the part names.
    /// </summary>
    private static IReadOnlyList<ConnPair> ResolveConnections(SysmlDefinitionNode root, Dictionary<string, int> partIndex)
    {
        var pairs = new List<ConnPair>();
        foreach (var conn in root.Children.OfType<SysmlConnectionNode>())
        {
            var a = ResolveEndpoint(conn.EndpointA, partIndex);
            var b = ResolveEndpoint(conn.EndpointB, partIndex);
            if (a >= 0 && b >= 0 && a != b)
            {
                pairs.Add(new ConnPair(a, b));
            }
        }

        return pairs;
    }

    /// <summary>Resolves a dotted endpoint reference to a part index via its first segment.</summary>
    private static int ResolveEndpoint(string? reference, Dictionary<string, int> partIndex)
    {
        if (string.IsNullOrEmpty(reference))
        {
            return -1;
        }

        var dot = reference.IndexOf('.', StringComparison.Ordinal);
        var head = dot >= 0 ? reference[..dot] : reference;
        return partIndex.TryGetValue(head, out var i) ? i : -1;
    }

    /// <summary>Computes the intrinsic size of a nested part box (keyword + name : type lines).</summary>
    private static (double Width, double Height) ComputePartSize(string name, string? typing, Theme theme)
    {
        var label = typing is { Length: > 0 } ? $"{name} : {typing}" : name;
        var labelWidth = (label.Length * theme.FontSizeTitle * CharWidthFactor) + (2.0 * theme.LabelPadding);
        var width = Math.Max(MinPartWidth, labelWidth);
        var height = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true) + theme.LabelPadding;
        return (width, height);
    }

    /// <summary>Creates a rounded-rectangle part usage box at the given position.</summary>
    private static LayoutBox MakePartBox(PartItem part, Rect rect)
    {
        var label = part.Typing is { Length: > 0 } ? $"{part.Name} : {part.Typing}" : part.Name;
        return new LayoutBox(
            X: rect.X,
            Y: rect.Y,
            Width: rect.Width,
            Height: rect.Height,
            Label: label,
            Depth: 1,
            Shape: BoxShape.RoundedRectangle,
            Compartments: [],
            Children: [],
            Keyword: part.Keyword);
    }

    /// <summary>
    /// Assigns ports to each part box for its incident connections and routes a connector line for
    /// each connection between the two ports, appending the port and line nodes to the output and
    /// returning the number of connectors that had to cross a box.
    /// </summary>
    private static int AddPortsAndConnectors(
        IReadOnlyList<PartItem> parts,
        Rect[] partRects,
        IReadOnlyList<ConnPair> pairs,
        List<LayoutNode> nodes)
    {
        // For each part, collect a port request per incident connection (toward the other part).
        var requestsPerPart = new List<PortRequest>[parts.Count];
        var connSlotPerPart = new List<int>[parts.Count];
        for (var i = 0; i < parts.Count; i++)
        {
            requestsPerPart[i] = [];
            connSlotPerPart[i] = [];
        }

        for (var c = 0; c < pairs.Count; c++)
        {
            var (a, b) = (pairs[c].A, pairs[c].B);
            requestsPerPart[a].Add(new PortRequest(partRects[a], Centre(partRects[b])));
            connSlotPerPart[a].Add(c);
            requestsPerPart[b].Add(new PortRequest(partRects[b], Centre(partRects[a])));
            connSlotPerPart[b].Add(c);
        }

        // Assign port placements per part and index them by connection; also count how many ports
        // share each box side (used to decide where it is safe to align a connector).
        var portByPartConn = new Dictionary<(int Part, int Conn), PortPlacement>();
        var sideCount = new Dictionary<(int Part, PortSide Side), int>();
        for (var i = 0; i < parts.Count; i++)
        {
            var placements = PortAssigner.Assign(requestsPerPart[i]);
            for (var k = 0; k < placements.Count; k++)
            {
                portByPartConn[(i, connSlotPerPart[i][k])] = placements[k];
                var sideKey = (i, placements[k].Side);
                sideCount[sideKey] = sideCount.GetValueOrDefault(sideKey) + 1;
            }
        }

        // Alignment pass: where a connection's two ports each sit alone on facing edges and the boxes
        // overlap along the connector axis, snap both ports to a common coordinate so the connector
        // is a single straight line instead of having a small jog. Boxes are not moved, so this can
        // never introduce an overlap.
        for (var c = 0; c < pairs.Count; c++)
        {
            AlignConnectorPorts(pairs[c], c, partRects, sideCount, portByPartConn);
        }

        // Emit the (possibly aligned) port nodes.
        foreach (var placement in portByPartConn.Values)
        {
            nodes.Add(new LayoutPort(placement.CentreX, placement.CentreY, placement.Side, null));
        }

        // Route a connector line for each connection between its two ports.
        var crossings = 0;
        for (var c = 0; c < pairs.Count; c++)
        {
            var (a, b) = (pairs[c].A, pairs[c].B);
            if (!portByPartConn.TryGetValue((a, c), out var portA) ||
                !portByPartConn.TryGetValue((b, c), out var portB))
            {
                continue;
            }

            var obstacles = new List<Rect>();
            for (var i = 0; i < parts.Count; i++)
            {
                if (i != a && i != b)
                {
                    obstacles.Add(partRects[i]);
                }
            }

            var route = ChannelRouter.RouteWithStatus(
                new Point2D(portA.CentreX, portA.CentreY),
                new Point2D(portB.CentreX, portB.CentreY),
                obstacles,
                ConnectorClearance,
                sourceSide: portA.Side,
                targetSide: portB.Side);
            if (route.Crossed)
            {
                crossings++;
            }

            nodes.Add(new LayoutLine(
                Waypoints: route.Waypoints,
                SourceArrowhead: ArrowheadStyle.None,
                TargetArrowhead: ArrowheadStyle.None,
                LineStyle: LineStyle.Solid,
                MidpointLabel: null));
        }

        return crossings;
    }

    /// <summary>
    /// Snaps both ports of a connection to a shared axis coordinate so the connector renders as a
    /// straight line, but only when each port is alone on its (facing) edge and the two boxes overlap
    /// along the connector axis. In every other case the placement is left untouched.
    /// </summary>
    private static void AlignConnectorPorts(
        ConnPair pair,
        int conn,
        Rect[] partRects,
        Dictionary<(int Part, PortSide Side), int> sideCount,
        Dictionary<(int Part, int Conn), PortPlacement> portByPartConn)
    {
        if (!portByPartConn.TryGetValue((pair.A, conn), out var portA) ||
            !portByPartConn.TryGetValue((pair.B, conn), out var portB))
        {
            return;
        }

        // Only safe when each port is the sole occupant of its edge.
        if (sideCount.GetValueOrDefault((pair.A, portA.Side)) != 1 ||
            sideCount.GetValueOrDefault((pair.B, portB.Side)) != 1)
        {
            return;
        }

        var boxA = partRects[pair.A];
        var boxB = partRects[pair.B];

        var verticalFacing =
            (portA.Side == PortSide.Top && portB.Side == PortSide.Bottom) ||
            (portA.Side == PortSide.Bottom && portB.Side == PortSide.Top);
        var horizontalFacing =
            (portA.Side == PortSide.Left && portB.Side == PortSide.Right) ||
            (portA.Side == PortSide.Right && portB.Side == PortSide.Left);

        if (verticalFacing)
        {
            var lo = Math.Max(boxA.X, boxB.X);
            var hi = Math.Min(boxA.X + boxA.Width, boxB.X + boxB.Width);
            if (lo <= hi)
            {
                var x = (lo + hi) / 2.0;
                portByPartConn[(pair.A, conn)] = portA with { CentreX = x };
                portByPartConn[(pair.B, conn)] = portB with { CentreX = x };
            }
        }
        else if (horizontalFacing)
        {
            var lo = Math.Max(boxA.Y, boxB.Y);
            var hi = Math.Min(boxA.Y + boxA.Height, boxB.Y + boxB.Height);
            if (lo <= hi)
            {
                var y = (lo + hi) / 2.0;
                portByPartConn[(pair.A, conn)] = portA with { CentreY = y };
                portByPartConn[(pair.B, conn)] = portB with { CentreY = y };
            }
        }
    }

    /// <summary>Returns the centre point of a rectangle.</summary>
    private static Point2D Centre(Rect rect) =>
        new(rect.X + (rect.Width / 2.0), rect.Y + (rect.Height / 2.0));

    /// <summary>
    /// Gravity-compresses the placed part boxes to a tight minimum gap and quantises them to a grid
    /// so the connectors that follow have predictable, well-separated anchor lines. Falls back to the
    /// uncompressed positions if compression cannot find a non-overlapping arrangement.
    /// </summary>
    private static Rect[] CompressAndQuantize(Rect[] partRects, Theme theme)
    {
        if (partRects.Length == 0)
        {
            return partRects;
        }

        var minGap = ConnectorClearance * 2.0;
        var grid = theme.LabelPadding * 2.0;

        var compressed = GravityCompressor.Compress(
            [.. partRects.Select(r => new CompressBox(r.X, r.Y, r.Width, r.Height, r.Width, r.Height))],
            minGap,
            grid);
        if (!compressed.Feasible)
        {
            return partRects;
        }

        var quantised = GridQuantizer.Quantize(
            [.. partRects.Select((r, i) => new QuantizeBox(compressed.Positions[i].X, compressed.Positions[i].Y, r.Width, r.Height))],
            grid,
            theme.LabelPadding);

        return [.. quantised.Select(q => new Rect(q.X, q.Y, q.Width, q.Height))];
    }
}
