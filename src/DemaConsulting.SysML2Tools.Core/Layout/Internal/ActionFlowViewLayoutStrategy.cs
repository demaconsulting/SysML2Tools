// <copyright file="ActionFlowViewLayoutStrategy.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Engine;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Rendering.Internal;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Internal;

namespace DemaConsulting.SysML2Tools.Layout.Internal;

/// <summary>
/// Layout strategy for Action Flow View diagrams. Renders action usages as rounded boxes arranged
/// top-to-bottom in layers by the layered (Sugiyama-style) engine, with a start node entering the
/// initial actions, a done node leaving the final actions, and successions drawn as flow arrows.
/// </summary>
internal sealed class ActionFlowViewLayoutStrategy : ILayoutStrategy
{
    /// <summary>Minimum width of an action box.</summary>
    private const double MinActionWidth = 110.0;

    /// <summary>Approximate width-per-character factor relative to font size.</summary>
    private const double CharWidthFactor = 0.62;

    /// <summary>Diameter of the start and done markers.</summary>
    private const double MarkerSize = 20.0;

    /// <summary>Vertical space reserved above and below the layers for the start/done markers.</summary>
    private const double MarkerBand = 50.0;

    /// <summary>Clearance kept between routed successions and action boxes.</summary>
    private const double FlowClearance = 10.0;

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

        // Lay the actions out top-to-bottom in layers.
        var layered = LayeredLayoutEngine.Place(
            [.. actions.Select(a => new LayeredNode(a.Width, a.Height))],
            [.. edges.Select(e => new LayeredEdge(e.From, e.To))],
            layerGap: theme.FontSizeTitle * 3.0,
            nodeGap: theme.FontSizeTitle * 2.0,
            padding: theme.LabelPadding * 4.0);

        // Shift everything down to leave room for the start marker band.
        var rects = new Rect[actions.Count];
        for (var i = 0; i < actions.Count; i++)
        {
            var r = layered.Rects[i];
            rects[i] = new Rect(r.X, r.Y + MarkerBand, r.Width, r.Height);
        }

        var nodes = new List<LayoutNode>();
        for (var i = 0; i < actions.Count; i++)
        {
            nodes.Add(MakeActionBox(actions[i], rects[i]));
        }

        // Highway routing: bundle parallel successions onto shared corridors with cost bands so the
        // detailed router prefers them, reducing wire overlap. The layered placement is preserved
        // (no compression pass) to keep the deliberate top-to-bottom action ordering.
        var highwayBoxes = rects.Select((r, i) => new HighwayBox(r.X, r.Y, r.Width, r.Height, i.ToString())).ToList();
        var highwayEdges = edges.Select(e => new HighwayEdge(e.From, e.To, "succession")).ToList();
        var highway = HighwayAssigner.Assign(highwayBoxes, highwayEdges, theme.LabelPadding * 2.0, FlowClearance, FlowClearance * 2.0);
        var costBands = highway.Corridors
            .Where(c => c.IsHighway)
            .Select(c => new CostBand(c.IsHorizontal, c.Position - (c.ReservedWidth / 2.0), c.Position + (c.ReservedWidth / 2.0), 0.6))
            .ToList();

        var crossings = AddSuccessionEdges(edges, rects, nodes, layered.Layers, costBands);
        AddStartAndDone(actions, rects, edges, layered, nodes);

        var width = layered.Width;
        var height = layered.Height + (2.0 * MarkerBand);
        var warnings = LayoutWarnings.ForCrossings(context.ViewName, crossings);
        return new LayoutTree(width, height, nodes) { Warnings = warnings };
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
    /// Adds the succession flow edges (top-to-bottom) between action boxes, returning the number
    /// that had to cross a box.
    /// </summary>
    private static int AddSuccessionEdges(
        IReadOnlyList<(int From, int To)> edges,
        Rect[] rects,
        List<LayoutNode> nodes,
        IReadOnlyList<int> layers,
        IReadOnlyList<CostBand> costBands)
    {
        var crossings = 0;
        foreach (var (from, to) in edges)
        {
            var source = new Point2D(rects[from].X + (rects[from].Width / 2.0), rects[from].Y + rects[from].Height);
            var target = new Point2D(rects[to].X + (rects[to].Width / 2.0), rects[to].Y);

            var obstacles = new List<Rect>();
            for (var i = 0; i < rects.Length; i++)
            {
                if (i != from && i != to)
                {
                    obstacles.Add(rects[i]);
                }
            }

            // A back edge (target in an earlier layer) detours with extra clearance because the
            // orthogonal router cannot draw an arc.
            var isBackEdge = from < layers.Count && to < layers.Count && layers[from] > layers[to];
            var clearance = isBackEdge ? FlowClearance * 2.5 : FlowClearance;
            var route = ChannelRouter.RouteWithStatus(
                source, target, obstacles, clearance,
                sourceSide: PortSide.Bottom, targetSide: PortSide.Top, costBands: costBands);
            if (route.Crossed)
            {
                crossings++;
            }

            nodes.Add(new LayoutLine(
                Waypoints: route.Waypoints,
                SourceArrowhead: ArrowheadStyle.None,
                TargetArrowhead: ArrowheadStyle.Open,
                LineStyle: LineStyle.Dashed,
                MidpointLabel: null));
        }

        return crossings;
    }

    /// <summary>
    /// Adds the start marker (filled circle) entering the actions with no predecessor and the done
    /// marker (bullseye) leaving the actions with no successor.
    /// </summary>
    private static void AddStartAndDone(
        IReadOnlyList<ActionItem> actions,
        Rect[] rects,
        IReadOnlyList<(int From, int To)> edges,
        LayeredResult layered,
        List<LayoutNode> nodes)
    {
        var hasIncoming = new bool[actions.Count];
        var hasOutgoing = new bool[actions.Count];
        foreach (var (from, to) in edges)
        {
            hasOutgoing[from] = true;
            hasIncoming[to] = true;
        }

        var fallbackX = layered.Width / 2.0;

        // Centre the start marker over the action(s) it enters so the entry arrow stays vertical.
        var starts = Enumerable.Range(0, actions.Count).Where(i => !hasIncoming[i]).ToList();
        var startX = starts.Count > 0
            ? starts.Average(i => rects[i].X + (rects[i].Width / 2.0))
            : fallbackX;
        var startY = MarkerBand / 2.0;
        nodes.Add(new LayoutBadge(startX, startY, MarkerSize, BadgeShape.FilledCircle, null));
        foreach (var i in starts)
        {
            nodes.Add(FlowLine(new Point2D(startX, startY + (MarkerSize / 2.0)),
                new Point2D(rects[i].X + (rects[i].Width / 2.0), rects[i].Y)));
        }

        // Centre the done marker under the action(s) that reach it.
        var ends = Enumerable.Range(0, actions.Count).Where(i => !hasOutgoing[i]).ToList();
        var doneX = ends.Count > 0
            ? ends.Average(i => rects[i].X + (rects[i].Width / 2.0))
            : fallbackX;
        var doneY = MarkerBand + layered.Height + (MarkerBand / 2.0);
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
            SourceArrowhead: ArrowheadStyle.None,
            TargetArrowhead: ArrowheadStyle.Filled,
            LineStyle: LineStyle.Solid,
            MidpointLabel: null);

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
