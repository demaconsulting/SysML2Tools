// <copyright file="ActionFlowViewLayoutStrategy.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Rendering.Internal;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;

namespace DemaConsulting.SysML2Tools.Layout.Internal;

/// <summary>
/// Layout strategy for Action Flow View diagrams. Renders action usages as rounded boxes placed
/// top-to-bottom by the layered layout algorithm, with a start node entering the initial actions, a
/// done node leaving the final actions, and successions drawn as dashed flow arrows.
/// </summary>
/// <remarks>
/// Actions are placed by the bundled layered algorithm flowing top-to-bottom (down) so the flow reads
/// top-to-bottom: a succession leaves its source on the SOUTH face and enters its target on the NORTH
/// face, and each succession follows the orthogonal polyline the algorithm routed for it. The
/// (possibly cyclic) succession graph is made acyclic by the algorithm's cycle-breaking stage; a
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
    /// Gap between a start/done marker and the adjacent action layer. Mirrors the layered algorithm's
    /// between-layer corridor width (70 px) so the control markers keep the same vertical rhythm as
    /// the action layers.
    /// </summary>
    private const double MarkerLayerGap = 70.0;

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

        // Place action boxes with the layered algorithm flowing top-to-bottom (down). Each action
        // becomes a node and each succession a directed edge; the algorithm's cycle-breaking stage
        // makes the (possibly cyclic) flow graph acyclic, so it tolerates back edges. Self-loops are
        // already excluded by ResolveSuccessions (it keeps only from != to).
        var placed = LayeredPlacement.Place(
            actions.Select(a => (a.Width, a.Height)).ToList(),
            edges,
            LayoutFlowDirection.Down);

        // Compute the top-left of the content bounding box over the real action nodes and the screen
        // offset that normalizes it into the canvas, reserving a marker band at the top (start) and
        // bottom (done).
        var margin = theme.LabelPadding * 4.0;
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        for (var i = 0; i < actions.Count; i++)
        {
            minX = Math.Min(minX, placed.Rects[i].X);
            minY = Math.Min(minY, placed.Rects[i].Y);
        }

        var offsetX = margin - minX;
        var offsetY = (margin + MarkerSize + MarkerLayerGap) - minY;

        var rects = new Rect[actions.Count];
        for (var i = 0; i < actions.Count; i++)
        {
            rects[i] = new Rect(placed.Rects[i].X + offsetX, placed.Rects[i].Y + offsetY, actions[i].Width, actions[i].Height);
        }

        var nodes = new List<LayoutNode>();
        for (var i = 0; i < actions.Count; i++)
        {
            nodes.Add(MakeActionBox(actions[i], rects[i]));
        }

        var crossings = AddSuccessionEdges(edges, placed.EdgePolylines, rects, offsetX, offsetY, nodes);
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
    /// Adds the succession flow edges (top-to-bottom) between action boxes, using the orthogonal
    /// polyline the layered algorithm routed for each succession, and returns the number of
    /// successions whose polyline crosses a non-endpoint action box.
    /// </summary>
    /// <remarks>
    /// The layered algorithm returns exactly one routed polyline per input succession, in input order
    /// and already oriented source-to-target, so succession <c>e</c> uses <c>edgePolylines[e]</c>
    /// directly. The open chevron end marker therefore always lands on the true target.
    /// </remarks>
    private static int AddSuccessionEdges(
        IReadOnlyList<(int From, int To)> edges,
        IReadOnlyList<IReadOnlyList<Point2D>> edgePolylines,
        Rect[] rects,
        double offsetX,
        double offsetY,
        List<LayoutNode> nodes)
    {
        var crossings = 0;
        for (var e = 0; e < edges.Count; e++)
        {
            var (from, to) = edges[e];
            var poly = edgePolylines[e];

            // The algorithm routes every edge, so poly normally has >= 2 points; fall back to a
            // straight segment between the (already-offset) box centres only if it does not.
            var waypoints = poly.Count >= 2
                ? poly.Select(p => new Point2D(p.X + offsetX, p.Y + offsetY)).ToList()
                : [Centre(rects[from]), Centre(rects[to])];

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
