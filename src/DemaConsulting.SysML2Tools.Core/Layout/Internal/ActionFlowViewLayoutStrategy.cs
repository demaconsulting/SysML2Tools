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

        AddSuccessionEdges(edges, rects, nodes);
        AddStartAndDone(actions, rects, edges, layered, nodes);

        var width = layered.Width;
        var height = layered.Height + (2.0 * MarkerBand);
        return new LayoutTree(width, height, nodes);
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

    /// <summary>Adds the succession flow edges (top-to-bottom) between action boxes.</summary>
    private static void AddSuccessionEdges(
        IReadOnlyList<(int From, int To)> edges,
        Rect[] rects,
        List<LayoutNode> nodes)
    {
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

            var waypoints = ChannelRouter.Route(
                source, target, obstacles, FlowClearance,
                sourceSide: PortSide.Bottom, targetSide: PortSide.Top);
            nodes.Add(new LayoutLine(
                Waypoints: waypoints,
                SourceArrowhead: ArrowheadStyle.None,
                TargetArrowhead: ArrowheadStyle.Filled,
                LineStyle: LineStyle.Solid,
                MidpointLabel: null));
        }
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

        var centreX = layered.Width / 2.0;

        // Start marker above the first layer.
        var startY = MarkerBand / 2.0;
        nodes.Add(new LayoutBadge(centreX, startY, MarkerSize, BadgeShape.FilledCircle, null));
        for (var i = 0; i < actions.Count; i++)
        {
            if (!hasIncoming[i])
            {
                nodes.Add(FlowLine(new Point2D(centreX, startY + (MarkerSize / 2.0)),
                    new Point2D(rects[i].X + (rects[i].Width / 2.0), rects[i].Y)));
            }
        }

        // Done marker below the last layer.
        var doneY = MarkerBand + layered.Height + (MarkerBand / 2.0);
        nodes.Add(new LayoutBadge(centreX, doneY, MarkerSize, BadgeShape.Bullseye, null));
        for (var i = 0; i < actions.Count; i++)
        {
            if (!hasOutgoing[i])
            {
                nodes.Add(FlowLine(new Point2D(rects[i].X + (rects[i].Width / 2.0), rects[i].Y + rects[i].Height),
                    new Point2D(centreX, doneY - (MarkerSize / 2.0))));
            }
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
