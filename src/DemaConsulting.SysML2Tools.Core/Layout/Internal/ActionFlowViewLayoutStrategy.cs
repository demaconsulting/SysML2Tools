// <copyright file="ActionFlowViewLayoutStrategy.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Engine;
using DemaConsulting.SysML2Tools.Layout.Engine.Layered;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Rendering.Internal;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Internal;

namespace DemaConsulting.SysML2Tools.Layout.Internal;

/// <summary>
/// Layout strategy for Action Flow View diagrams. Renders action usages as rounded boxes placed
/// top-to-bottom by the layered layout pipeline, with a start node entering the initial actions, a
/// done node leaving the final actions, and successions drawn as dashed flow arrows.
/// </summary>
/// <remarks>
/// Actions are placed by <see cref="LayeredLayoutPipeline"/> with <see cref="LayoutDirection.Down"/> so
/// the flow reads top-to-bottom: a succession leaves its source on the SOUTH face and enters its target
/// on the NORTH face, and each succession follows the orthogonal polyline the pipeline routed for it.
/// The (possibly cyclic) succession graph is made acyclic by the pipeline's cycle-breaking stage; a
/// filled-circle start marker enters the actions with no predecessor and a bullseye done marker leaves
/// the actions with no successor, with a reserved marker band at the top and bottom of the canvas.
/// </remarks>
internal sealed class ActionFlowViewLayoutStrategy : ILayoutStrategy
{
    /// <summary>Minimum width of an action box.</summary>
    private const double MinActionWidth = 110.0;

    /// <summary>Approximate width-per-character factor relative to font size.</summary>
    private const double CharWidthFactor = 0.62;

    /// <summary>Diameter of the start and done markers.</summary>
    private const double MarkerSize = 20.0;

    /// <summary>
    /// Gap between a start/done marker and the adjacent action layer. Set to the layered pipeline's
    /// between-layer spacing (<see cref="LayeredLayoutMetrics.CorridorMinWidth"/>) so the control
    /// markers keep the same vertical rhythm as the action layers.
    /// </summary>
    private const double MarkerLayerGap = LayeredLayoutMetrics.CorridorMinWidth;

    /// <summary>An action with its computed box size.</summary>
    private sealed record ActionItem(string Name, double Width, double Height);

    /// <inheritdoc/>
    public LayoutTree BuildLayout(ViewContext context, RenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        var theme = options.Theme;

        var root = FindRoot(context.Workspace);
        if (root is null)
        {
            return new LayoutTree(200.0, 100.0, []);
        }

        var (actions, index) = CollectActions(root, theme);
        if (actions.Count == 0)
        {
            return new LayoutTree(200.0, 100.0, []);
        }

        var edges = ResolveSuccessions(root, index);

        // Place action boxes with the layered pipeline flowing top-to-bottom (DOWN). Each action
        // becomes a node and each succession a directed edge; the pipeline's cycle-breaking stage
        // makes the (possibly cyclic) flow graph acyclic, so it tolerates back edges. Self-loops are
        // already excluded by ResolveSuccessions (it keeps only from != to).
        var layerNodes = actions.Select(a => new LayerNode(a.Width, a.Height)).ToList();
        var layerEdges = edges.Select(e => new LayerEdge(e.From, e.To)).ToList();

        var graph = new LayeredGraph(layerNodes, layerEdges, LayoutDirection.Down)
        {
            // Reserve a clean straight approach for the open-chevron target marker that every
            // succession carries, so the pipeline pushes reversed (back) edges far enough out that the
            // renderer's rounded corner never intrudes into the decoration. The approach equals the
            // marker's along-line length plus one corner radius (consumed by the rounded corner) plus
            // a safety margin — identical injection to the State Transition strategy.
            BackEdgeEntryApproach = NotationMetrics.AlongLineLength(EndMarkerStyle.OpenChevron)
                + theme.LineCornerRadius + theme.CleanLegMargin,
        };
        var pipeline = LayeredLayoutPipeline.Builder()
            .Direction(LayoutDirection.Down)
            .Hierarchy(HierarchyHandling.Flat)
            .AddDefaultStages()
            .Build();
        pipeline.Run(graph);

        // Compute the top-left of the content bounding box over the real action nodes and the screen
        // offset that normalizes it into the canvas, reserving a marker band at the top (start) and
        // bottom (done).
        var margin = theme.LabelPadding * 4.0;
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        for (var i = 0; i < actions.Count; i++)
        {
            minX = Math.Min(minX, graph.AugX[i]);
            minY = Math.Min(minY, graph.AugY[i]);
        }

        var offsetX = margin - minX;
        var offsetY = (margin + MarkerSize + MarkerLayerGap) - minY;

        var rects = new Rect[actions.Count];
        for (var i = 0; i < actions.Count; i++)
        {
            rects[i] = new Rect(graph.AugX[i] + offsetX, graph.AugY[i] + offsetY, actions[i].Width, actions[i].Height);
        }

        var nodes = new List<LayoutNode>();
        for (var i = 0; i < actions.Count; i++)
        {
            nodes.Add(MakeActionBox(actions[i], rects[i]));
        }

        var crossings = AddSuccessionEdges(edges, graph, rects, offsetX, offsetY, nodes);
        AddStartAndDone(actions, rects, edges, margin, nodes);

        // Size the canvas to the actual rendered content: action boxes plus routed succession
        // polylines (back edges can bulge beyond the box columns) and the start/done markers.
        var (maxX, maxY) = ContentExtent(nodes);
        var warnings = LayoutWarnings.ForCrossings(context.ViewName, crossings);
        return new LayoutTree(maxX + margin, maxY + margin, nodes) { Warnings = warnings };
    }

    /// <summary>Finds the definition with the most successions to use as the diagram root.</summary>
    private static SysmlDefinitionNode? FindRoot(SysmlWorkspace workspace)
    {
        SysmlDefinitionNode? best = null;
        var bestScore = -1;

        foreach (var (qualifiedName, node) in workspace.Declarations)
        {
            if (node is not SysmlDefinitionNode def)
            {
                continue;
            }

            if (StdlibFilter.IsStdlibElement(qualifiedName, workspace.StdlibNames))
            {
                continue;
            }

            var successions = def.Children.OfType<SysmlTransitionNode>().Count();
            var actions = def.Children.OfType<SysmlFeatureNode>().Count(f => f.FeatureKeyword == "action");
            var score = (successions * 100) + actions;
            if (score > bestScore && (successions > 0 || actions > 0))
            {
                best = def;
                bestScore = score;
            }
        }

        return best;
    }

    /// <summary>Collects the action usages of the root definition and builds a name → index lookup.</summary>
    private static (IReadOnlyList<ActionItem> Actions, Dictionary<string, int> Index) CollectActions(
        SysmlDefinitionNode root,
        Theme theme)
    {
        var actions = new List<ActionItem>();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);

        void Add(string name)
        {
            if (index.ContainsKey(name))
            {
                return;
            }

            index[name] = actions.Count;
            var (width, height) = ComputeActionSize(name, theme);
            actions.Add(new ActionItem(name, width, height));
        }

        foreach (var feature in root.Children.OfType<SysmlFeatureNode>())
        {
            if (feature.FeatureKeyword == "action" && feature.Name is not null)
            {
                Add(feature.Name);
            }
        }

        foreach (var succession in root.Children.OfType<SysmlTransitionNode>())
        {
            if (LastSegment(succession.Source) is { } s)
            {
                Add(s);
            }

            if (LastSegment(succession.Target) is { } t)
            {
                Add(t);
            }
        }

        return (actions, index);
    }

    /// <summary>Resolves succession endpoints to action indices via their last name segment.</summary>
    private static IReadOnlyList<(int From, int To)> ResolveSuccessions(SysmlDefinitionNode root, Dictionary<string, int> index)
    {
        var result = new List<(int, int)>();
        foreach (var succession in root.Children.OfType<SysmlTransitionNode>())
        {
            var source = LastSegment(succession.Source);
            var target = LastSegment(succession.Target);
            if (source is not null && target is not null &&
                index.TryGetValue(source, out var from) && index.TryGetValue(target, out var to) && from != to)
            {
                result.Add((from, to));
            }
        }

        return result;
    }

    /// <summary>Computes the intrinsic size of an action box.</summary>
    private static (double Width, double Height) ComputeActionSize(string name, Theme theme)
    {
        var labelWidth = (name.Length * theme.FontSizeTitle * CharWidthFactor) + (4.0 * theme.LabelPadding);
        var width = Math.Max(MinActionWidth, labelWidth);
        var height = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true) + theme.LabelPadding;
        return (width, height);
    }

    /// <summary>Creates a rounded-rectangle action box at the given position.</summary>
    private static LayoutBox MakeActionBox(ActionItem action, Rect rect) =>
        new(
            X: rect.X,
            Y: rect.Y,
            Width: rect.Width,
            Height: rect.Height,
            Label: action.Name,
            Depth: 1,
            Shape: BoxShape.RoundedRectangle,
            Compartments: [],
            Children: [],
            Keyword: "action");

    /// <summary>
    /// Adds the succession flow edges (top-to-bottom) between action boxes, mapping each succession to
    /// the orthogonal polyline the layered pipeline routed for it, and returns the number of
    /// successions whose polyline crosses a non-endpoint action box.
    /// </summary>
    /// <remarks>
    /// The pipeline's cycle-breaking stage de-duplicates identical directed pairs and reverses back
    /// edges, so <see cref="LayeredGraph.Waypoints"/> is not 1:1 with the input successions. A lookup
    /// keyed by the routed <c>(source, target)</c> pair recovers each succession's polyline; a
    /// succession whose routed edge was reversed reuses that polyline in reverse so the open chevron
    /// end marker lands on the true target.
    /// </remarks>
    private static int AddSuccessionEdges(
        IReadOnlyList<(int From, int To)> edges,
        LayeredGraph graph,
        Rect[] rects,
        double offsetX,
        double offsetY,
        List<LayoutNode> nodes)
    {
        // Build the routed (source, target) -> polyline lookup over the acyclic edge set.
        var routed = new Dictionary<(int Source, int Target), IReadOnlyList<Point2D>>();
        for (var k = 0; k < graph.Acyclic.Count; k++)
        {
            var edge = graph.Acyclic[k];
            routed[(edge.Source, edge.Target)] = graph.Waypoints[k];
        }

        var crossings = 0;
        foreach (var (from, to) in edges)
        {
            var routedPoints = ResolveSuccessionPolyline(from, to, routed)
                ?? [Centre(rects[from]), Centre(rects[to])];

            var waypoints = routedPoints
                .Select(p => new Point2D(p.X + offsetX, p.Y + offsetY))
                .ToList();

            if (CrossesNonEndpointBox(waypoints, rects, from, to))
            {
                crossings++;
            }

            nodes.Add(new LayoutLine(
                Waypoints: waypoints,
                SourceEnd: EndMarkerStyle.None,
                TargetEnd: EndMarkerStyle.OpenChevron,
                LineStyle: LineStyle.Dashed,
                MidpointLabel: null));
        }

        return crossings;
    }

    /// <summary>
    /// Returns the routed polyline for a succession, reversing it when only the opposite direction was
    /// routed (a reversed back edge), or null when neither direction was routed.
    /// </summary>
    private static IReadOnlyList<Point2D>? ResolveSuccessionPolyline(
        int from,
        int to,
        IReadOnlyDictionary<(int Source, int Target), IReadOnlyList<Point2D>> routed)
    {
        if (routed.TryGetValue((from, to), out var forward))
        {
            return forward;
        }

        if (routed.TryGetValue((to, from), out var backward))
        {
            // The pipeline reversed this back edge; reverse the polyline so it runs source -> target
            // with the open chevron end marker at the true target.
            return [.. backward.Reverse()];
        }

        return null;
    }

    /// <summary>
    /// Adds the start marker (filled circle) entering the actions with no predecessor and the done
    /// marker (bullseye) leaving the actions with no successor, each joined by a solid flow line.
    /// </summary>
    private static void AddStartAndDone(
        IReadOnlyList<ActionItem> actions,
        Rect[] rects,
        IReadOnlyList<(int From, int To)> edges,
        double margin,
        List<LayoutNode> nodes)
    {
        var hasIncoming = new bool[actions.Count];
        var hasOutgoing = new bool[actions.Count];
        foreach (var (from, to) in edges)
        {
            hasOutgoing[from] = true;
            hasIncoming[to] = true;
        }

        var fallbackX = rects.Average(r => r.X + (r.Width / 2.0));

        // Centre the start marker over the action(s) it enters so the entry arrow stays vertical.
        var starts = Enumerable.Range(0, actions.Count).Where(i => !hasIncoming[i]).ToList();
        var startX = starts.Count > 0
            ? starts.Average(i => rects[i].X + (rects[i].Width / 2.0))
            : fallbackX;
        var startY = margin + (MarkerSize / 2.0);
        nodes.Add(new LayoutBadge(startX, startY, MarkerSize, BadgeShape.FilledCircle, null));
        foreach (var i in starts)
        {
            nodes.Add(FlowLine(new Point2D(startX, startY + (MarkerSize / 2.0)),
                new Point2D(rects[i].X + (rects[i].Width / 2.0), rects[i].Y)));
        }

        // Centre the done marker under the action(s) that reach it.
        var actionsBottom = rects.Max(r => r.Y + r.Height);
        var ends = Enumerable.Range(0, actions.Count).Where(i => !hasOutgoing[i]).ToList();
        var doneX = ends.Count > 0
            ? ends.Average(i => rects[i].X + (rects[i].Width / 2.0))
            : fallbackX;
        var doneY = actionsBottom + MarkerLayerGap + (MarkerSize / 2.0);
        nodes.Add(new LayoutBadge(doneX, doneY, MarkerSize, BadgeShape.Bullseye, null));
        foreach (var i in ends)
        {
            nodes.Add(FlowLine(new Point2D(rects[i].X + (rects[i].Width / 2.0), rects[i].Y + rects[i].Height),
                new Point2D(doneX, doneY - (MarkerSize / 2.0))));
        }
    }

    /// <summary>Builds a straight downward flow line with a filled arrowhead at the target.</summary>
    private static LayoutLine FlowLine(Point2D source, Point2D target) =>
        new(
            Waypoints: Math.Abs(source.X - target.X) < 1e-9
                ? [source, target]
                : [source, new Point2D(source.X, (source.Y + target.Y) / 2.0), new Point2D(target.X, (source.Y + target.Y) / 2.0), target],
            SourceEnd: EndMarkerStyle.None,
            TargetEnd: EndMarkerStyle.FilledArrow,
            LineStyle: LineStyle.Solid,
            MidpointLabel: null);

    /// <summary>Returns the maximum X and Y coordinate reached by any built layout node.</summary>
    private static (double MaxX, double MaxY) ContentExtent(IReadOnlyList<LayoutNode> nodes)
    {
        var maxX = 0.0;
        var maxY = 0.0;

        foreach (var node in nodes)
        {
            switch (node)
            {
                case LayoutBox box:
                    maxX = Math.Max(maxX, box.X + box.Width);
                    maxY = Math.Max(maxY, box.Y + box.Height);
                    break;
                case LayoutLine line:
                    foreach (var point in line.Waypoints)
                    {
                        maxX = Math.Max(maxX, point.X);
                        maxY = Math.Max(maxY, point.Y);
                    }

                    break;
                case LayoutBadge badge:
                    maxX = Math.Max(maxX, badge.CentreX + (badge.Size / 2.0));
                    maxY = Math.Max(maxY, badge.CentreY + (badge.Size / 2.0));
                    break;
                default:
                    break;
            }
        }

        return (maxX, maxY);
    }

    /// <summary>
    /// Returns whether any segment of the polyline passes through the interior of an action box other
    /// than the succession's own source or target.
    /// </summary>
    private static bool CrossesNonEndpointBox(
        IReadOnlyList<Point2D> waypoints,
        Rect[] rects,
        int source,
        int target)
    {
        for (var j = 0; j < rects.Length; j++)
        {
            if (j == source || j == target)
            {
                continue;
            }

            for (var s = 0; s + 1 < waypoints.Count; s++)
            {
                if (SegmentIntersectsRect(waypoints[s], waypoints[s + 1], rects[j]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Returns whether the segment from <paramref name="a"/> to <paramref name="b"/> intersects the
    /// interior of <paramref name="rect"/>, using parametric slab clipping.
    /// </summary>
    private static bool SegmentIntersectsRect(Point2D a, Point2D b, Rect rect)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lower = 0.0;
        var upper = 1.0;

        Span<double> p = [-dx, dx, -dy, dy];
        Span<double> q =
        [
            a.X - rect.X,
            rect.X + rect.Width - a.X,
            a.Y - rect.Y,
            rect.Y + rect.Height - a.Y,
        ];

        for (var i = 0; i < 4; i++)
        {
            if (Math.Abs(p[i]) < 1e-9)
            {
                if (q[i] < 0.0)
                {
                    return false;
                }

                continue;
            }

            var r = q[i] / p[i];
            if (p[i] < 0.0)
            {
                lower = Math.Max(lower, r);
            }
            else
            {
                upper = Math.Min(upper, r);
            }
        }

        return lower < upper;
    }

    /// <summary>Returns the centre point of a rectangle.</summary>
    private static Point2D Centre(Rect rect) =>
        new(rect.X + (rect.Width / 2.0), rect.Y + (rect.Height / 2.0));

    /// <summary>Returns the last <c>::</c>-separated segment of a qualified reference, or null.</summary>
    private static string? LastSegment(string? reference)
    {
        if (string.IsNullOrEmpty(reference))
        {
            return null;
        }

        var sep = reference.LastIndexOf("::", StringComparison.Ordinal);
        return sep >= 0 ? reference[(sep + 2)..] : reference;
    }
}
