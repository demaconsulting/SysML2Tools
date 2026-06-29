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
/// definition: its nested part usages as boxes placed by the layered engine, ports on the box
/// boundaries, and connection usages routed as orthogonal connector lines between the ports, all
/// enclosed by a container box for the host definition.
/// </summary>
internal sealed class InterconnectionViewLayoutStrategy : ILayoutStrategy
{
    /// <summary>Minimum width of a nested part box.</summary>
    private const double MinPartWidth = 110.0;

    /// <summary>Approximate width-per-character factor relative to font size.</summary>
    private const double CharWidthFactor = 0.62;

    /// <summary>Minimum vertical gap between nodes in the same layer column.</summary>
    private const double NodeSpacing = 20.0;

    /// <summary>Minimum width of each inter-layer corridor, regardless of edge count.</summary>
    private const double MinCorridorWidth = 60.0;

    /// <summary>Additional corridor width added per edge that crosses the corridor.</summary>
    private const double EdgeSpacing = 12.0;

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

        // Place the part boxes with the layered engine: BFS-derived layer columns separated by
        // corridors whose width scales with the number of edges that pass through them.
        var layerNodes = parts.Select(p => new LayerNode(p.Width, p.Height)).ToList();
        var layerEdges = pairs.Select(p => new LayerEdge(p.A, p.B)).ToList();
        var placed = LayeredPlacer.Place(
            layerNodes,
            layerEdges,
            nodeSpacing: NodeSpacing,
            minCorridorWidth: MinCorridorWidth,
            edgeSpacing: EdgeSpacing,
            clearance: ConnectorClearance);

        // Offset the placed parts to sit below the container title area.
        var titleArea = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true);
        var offsetX = theme.LabelPadding * 2.0;
        var offsetY = titleArea + (theme.LabelPadding * 2.0);

        var partRects = new Rect[parts.Count];
        for (var i = 0; i < parts.Count; i++)
        {
            var r = placed.Rects[i];
            partRects[i] = new Rect(r.X + offsetX, r.Y + offsetY, r.Width, r.Height);
        }

        // Size the container box to enclose all placed parts with uniform padding.
        var containerWidth = placed.TotalWidth + (offsetX * 2.0);
        var containerHeight = placed.TotalHeight + offsetY + (theme.LabelPadding * 2.0);

        var nodes = new List<LayoutNode>();

        // Container box for the root part definition.
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

        // Ports and connectors via slot-based routing for cross-layer pairs and channel routing
        // for same-layer pairs.
        var crossings = AddPortsAndConnectors(parts, partRects, pairs, placed.NodeLayers, nodes);

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
    /// Assigns ports and routes connectors for all connections, appending the resulting port and
    /// line nodes to <paramref name="nodes"/> and returning the number of connectors that had to
    /// cross a box.
    /// </summary>
    /// <remarks>
    /// Two routing paths are used depending on whether the two endpoints of a connection occupy
    /// different layers or the same layer:
    /// <list type="bullet">
    ///   <item><description>
    ///     <strong>Cross-layer</strong>: the lower-layer node connects on its right face and the
    ///     higher-layer node on its left face; a vertical slot in the corridor between them routes
    ///     the connector as a Z-path. Slots are spaced by <see cref="EdgeSpacing"/> so connectors
    ///     in the same corridor never share a vertical segment.
    ///   </description></item>
    ///   <item><description>
    ///     <strong>Same-layer</strong>: <see cref="PortAssigner"/> assigns port sides and
    ///     <see cref="ChannelRouter"/> routes orthogonally around obstacles, as before.
    ///   </description></item>
    /// </list>
    /// </remarks>
    private static int AddPortsAndConnectors(
        IReadOnlyList<PartItem> parts,
        Rect[] partRects,
        IReadOnlyList<ConnPair> pairs,
        IReadOnlyList<int> nodeLayers,
        List<LayoutNode> nodes)
    {
        // Separate pairs into cross-layer (slot routing) and same-layer (channel routing) buckets.
        var crossLayerPairs = new List<(int PairIdx, int SrcNode, int TgtNode)>();
        var sameLayerIndices = new List<int>();

        for (var c = 0; c < pairs.Count; c++)
        {
            var (a, b) = (pairs[c].A, pairs[c].B);
            if (nodeLayers[a] == nodeLayers[b])
            {
                sameLayerIndices.Add(c);
            }
            else
            {
                // Normalise so SrcNode is the lower-layer endpoint (uses right face)
                var (src, tgt) = nodeLayers[a] < nodeLayers[b] ? (a, b) : (b, a);
                crossLayerPairs.Add((c, src, tgt));
            }
        }

        // Path A: slot-based Z-path routing for cross-layer pairs (never crosses an obstacle)
        var crossings = RouteCrossLayerPairs(partRects, pairs, crossLayerPairs, nodeLayers, nodes);

        // Path B: PortAssigner + ChannelRouter for same-layer pairs (may cross when over-dense)
        crossings += RouteSameLayerPairs(parts, partRects, pairs, sameLayerIndices, nodes);

        return crossings;
    }

    /// <summary>
    /// Routes cross-layer connections using slot-based Z-paths: the connector leaves the source's
    /// right face, travels horizontally to a reserved slot in the inter-layer corridor, runs
    /// vertically to the target's Y, then enters the target's left face. Slot X coordinates are
    /// spaced by <see cref="EdgeSpacing"/> so no two connectors in the same corridor share a
    /// vertical segment. Returns 0 (slot routing never crosses an obstacle by construction).
    /// </summary>
    private static int RouteCrossLayerPairs(
        Rect[] partRects,
        IReadOnlyList<ConnPair> pairs,
        IReadOnlyList<(int PairIdx, int SrcNode, int TgtNode)> crossLayerPairs,
        IReadOnlyList<int> nodeLayers,
        List<LayoutNode> nodes)
    {
        if (crossLayerPairs.Count == 0)
        {
            return 0;
        }

        // Distribute source ports evenly along the right face of each source box, ordered by
        // the target centre Y so connections cross as little as possible.
        var srcPortY = new double[pairs.Count];
        foreach (var srcGroup in crossLayerPairs.GroupBy(p => p.SrcNode))
        {
            var box = partRects[srcGroup.Key];
            var items = srcGroup
                .OrderBy(p => partRects[p.TgtNode].Y + (partRects[p.TgtNode].Height / 2.0))
                .ThenBy(p => p.PairIdx)
                .ToList();
            DistributePortsAlongFace(items.Select(p => p.PairIdx).ToList(), box, srcPortY);
        }

        // Distribute target ports evenly along the left face of each target box, ordered by
        // the source centre Y.
        var tgtPortY = new double[pairs.Count];
        foreach (var tgtGroup in crossLayerPairs.GroupBy(p => p.TgtNode))
        {
            var box = partRects[tgtGroup.Key];
            var items = tgtGroup
                .OrderBy(p => partRects[p.SrcNode].Y + (partRects[p.SrcNode].Height / 2.0))
                .ThenBy(p => p.PairIdx)
                .ToList();
            DistributePortsAlongFace(items.Select(p => p.PairIdx).ToList(), box, tgtPortY);
        }

        // Compute the rightmost X of all boxes in each layer — slot X starts just beyond this edge.
        var maxRightPerLayer = new Dictionary<int, double>();
        for (var i = 0; i < partRects.Length; i++)
        {
            var layer = nodeLayers[i];
            var right = partRects[i].X + partRects[i].Width;
            if (!maxRightPerLayer.TryGetValue(layer, out var cur) || right > cur)
            {
                maxRightPerLayer[layer] = right;
            }
        }

        // Assign slot X per pair: group by source layer, sort by mean port Y so slots are ordered
        // top-to-bottom, then step right by EdgeSpacing for each additional slot in the corridor.
        var slotX = new double[pairs.Count];
        foreach (var corridorGroup in crossLayerPairs.GroupBy(p => nodeLayers[p.SrcNode]))
        {
            var maxRight = maxRightPerLayer.GetValueOrDefault(corridorGroup.Key);
            var sorted = corridorGroup
                .OrderBy(p => (srcPortY[p.PairIdx] + tgtPortY[p.PairIdx]) / 2.0)
                .ThenBy(p => p.PairIdx)
                .ToList();

            for (var s = 0; s < sorted.Count; s++)
            {
                slotX[sorted[s].PairIdx] = maxRight + ConnectorClearance + (s * EdgeSpacing);
            }
        }

        // Emit a right-face port and a left-face port for each cross-layer pair, then route a
        // Z-path: source → (slotX, srcY) → (slotX, tgtY) → target.
        foreach (var (pairIdx, srcNode, tgtNode) in crossLayerPairs)
        {
            var srcBox = partRects[srcNode];
            var tgtBox = partRects[tgtNode];
            var srcX = srcBox.X + srcBox.Width;
            var srcY = srcPortY[pairIdx];
            var tgtX = tgtBox.X;
            var tgtY = tgtPortY[pairIdx];
            var sx = slotX[pairIdx];

            nodes.Add(new LayoutPort(srcX, srcY, PortSide.Right, null));
            nodes.Add(new LayoutPort(tgtX, tgtY, PortSide.Left, null));

            // Z-path: step right off the source → vertical slot → step right into the target
            nodes.Add(new LayoutLine(
                Waypoints: [new(srcX, srcY), new(sx, srcY), new(sx, tgtY), new(tgtX, tgtY)],
                SourceArrowhead: ArrowheadStyle.None,
                TargetArrowhead: ArrowheadStyle.None,
                LineStyle: LineStyle.Solid,
                MidpointLabel: null));
        }

        // Slot routing is obstacle-free by construction; no crossings to report
        return 0;
    }

    /// <summary>
    /// Fills <paramref name="portY"/> with evenly distributed Y coordinates for the ports in
    /// <paramref name="pairIndices"/> along the vertical extent of <paramref name="box"/>, keeping
    /// <see cref="ConnectorClearance"/> inset from the top and bottom edges. When there is only
    /// one port it is centred. The indices are assumed to be pre-sorted in top-to-bottom order.
    /// </summary>
    private static void DistributePortsAlongFace(IReadOnlyList<int> pairIndices, Rect box, double[] portY)
    {
        var count = pairIndices.Count;
        var margin = ConnectorClearance;

        for (var k = 0; k < count; k++)
        {
            var y = count == 1
                ? box.Y + (box.Height / 2.0)
                : box.Y + margin + (k * (box.Height - (2.0 * margin)) / (count - 1));

            portY[pairIndices[k]] = Math.Clamp(y, box.Y + margin, box.Y + box.Height - margin);
        }
    }

    /// <summary>
    /// Routes same-layer connections using <see cref="PortAssigner"/> for port placement and
    /// <see cref="ChannelRouter"/> for obstacle-avoiding orthogonal routing. Returns the number
    /// of connectors that had to cross an obstacle.
    /// </summary>
    private static int RouteSameLayerPairs(
        IReadOnlyList<PartItem> parts,
        Rect[] partRects,
        IReadOnlyList<ConnPair> pairs,
        IReadOnlyList<int> sameLayerIndices,
        List<LayoutNode> nodes)
    {
        if (sameLayerIndices.Count == 0)
        {
            return 0;
        }

        // Collect a port request per incident same-layer connection for each part.
        var requestsPerPart = new List<PortRequest>[parts.Count];
        var connSlotPerPart = new List<int>[parts.Count];
        for (var i = 0; i < parts.Count; i++)
        {
            requestsPerPart[i] = [];
            connSlotPerPart[i] = [];
        }

        foreach (var c in sameLayerIndices)
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

        // Alignment pass: snap the two ports of a connection to a shared axis coordinate so the
        // connector is a straight line when safe to do so.
        foreach (var c in sameLayerIndices)
        {
            AlignConnectorPorts(pairs[c], c, partRects, sideCount, portByPartConn);
        }

        // Emit the (possibly aligned) port nodes.
        foreach (var placement in portByPartConn.Values)
        {
            nodes.Add(new LayoutPort(placement.CentreX, placement.CentreY, placement.Side, null));
        }

        // Route a connector line for each same-layer connection between its two ports.
        var crossings = 0;
        foreach (var c in sameLayerIndices)
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
}
