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
/// Layout strategy for Interconnection View diagrams.
/// </summary>
/// <remarks>
/// <para>
/// Shows the internal structure of a single part definition: its nested part usages as
/// boxes placed by <see cref="InterconnectionLayoutEngine"/>, ports on the box boundaries,
/// and connection usages routed as orthogonal connector polylines between the ports, all
/// enclosed by a container box for the host definition.
/// </para>
/// <para>
/// Box heights are scaled to ensure each port has at least <see cref="MinPortSlot"/> px of
/// vertical clearance, so connectors remain visually distinct regardless of connection count.
/// All placement and routing is delegated to <see cref="InterconnectionLayoutEngine"/>, which
/// implements the full ELK-compatible Sugiyama pipeline.
/// </para>
/// </remarks>
internal sealed class InterconnectionViewLayoutStrategy : ILayoutStrategy
{
    /// <summary>Minimum width of a nested part box.</summary>
    private const double MinPartWidth = 110.0;

    /// <summary>Approximate width-per-character factor relative to the title font size.</summary>
    private const double CharWidthFactor = 0.62;

    /// <summary>Minimum vertical slot per port on a box face, for height-scaling.</summary>
    private const double MinPortSlot = 11.0;

    /// <summary>Clearance used when computing the minimum box height from port count.</summary>
    private const double ConnectorClearance = 10.0;

    /// <summary>A nested part usage with its computed intrinsic box size.</summary>
    private sealed record PartItem(string Name, string Keyword, string? Typing, double Width, double Height);

    /// <summary>A resolved binary connection between two nested-part indices.</summary>
    private sealed record ConnPair(int A, int B);

    /// <inheritdoc/>
    public LayoutTree BuildLayout(ViewContext context, RenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        var theme = options.Theme;

        // Choose the part definition whose internals to show.
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

        // Scale each box height to guarantee at least MinPortSlot px per port on its face.
        var degree = new int[parts.Count];
        foreach (var p in pairs)
        {
            degree[p.A]++;
            degree[p.B]++;
        }

        var layerNodes = parts
            .Select((p, i) =>
            {
                var minH = (degree[i] * MinPortSlot) + (2.0 * ConnectorClearance);
                return new LayerNode(p.Width, Math.Max(p.Height, minH));
            })
            .ToList();

        var layerEdges = pairs.Select(p => new LayerEdge(p.A, p.B)).ToList();

        // Delegate all placement and routing to the engine.
        var placed = InterconnectionLayoutEngine.Place(layerNodes, layerEdges);

        // Shift placed content down/right to sit inside the container box.
        var titleArea = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true);
        var offsetX = theme.LabelPadding * 2.0;
        var offsetY = titleArea + (theme.LabelPadding * 2.0);

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

        // One rounded box per nested part usage.
        for (var i = 0; i < parts.Count; i++)
        {
            var r = placed.Rects[i];
            nodes.Add(MakePartBox(parts[i], new Rect(r.X + offsetX, r.Y + offsetY, r.Width, r.Height)));
        }

        // One port pair and one connector line per connection.
        for (var i = 0; i < pairs.Count; i++)
        {
            var wp = placed.ConnectorWaypoints[i];
            if (wp.Count < 2)
            {
                continue;
            }

            // Shift all waypoints by the container offset.
            var shifted = wp.Select(p => new Point2D(p.X + offsetX, p.Y + offsetY)).ToList();

            // Source port: first waypoint on the source box's right face.
            nodes.Add(new LayoutPort(shifted[0].X, shifted[0].Y, PortSide.Right, null));

            // Target port: last waypoint on the target box's left face.
            nodes.Add(new LayoutPort(shifted[^1].X, shifted[^1].Y, PortSide.Left, null));

            nodes.Add(new LayoutLine(
                Waypoints: shifted,
                SourceArrowhead: ArrowheadStyle.None,
                TargetArrowhead: ArrowheadStyle.None,
                LineStyle: LineStyle.Solid,
                MidpointLabel: null));
        }

        return new LayoutTree(containerWidth, containerHeight, nodes);
    }

    /// <summary>
    /// Finds the part definition whose interior to render: the non-stdlib <c>part def</c>
    /// with the most connections, falling back to the one with the most part usages.
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
    /// Resolves each binary connection's endpoints to nested-part indices by matching the
    /// first segment of the dotted endpoint reference against the part names.
    /// </summary>
    private static IReadOnlyList<ConnPair> ResolveConnections(
        SysmlDefinitionNode root,
        Dictionary<string, int> partIndex)
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

    /// <summary>Computes the intrinsic size of a nested part box.</summary>
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
}
