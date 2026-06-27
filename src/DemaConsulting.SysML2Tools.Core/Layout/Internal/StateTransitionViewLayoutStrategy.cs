// <copyright file="StateTransitionViewLayoutStrategy.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Engine;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Rendering.Internal;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Internal;

namespace DemaConsulting.SysML2Tools.Layout.Internal;

/// <summary>
/// Layout strategy for State Transition View diagrams. Renders state usages as rounded boxes placed
/// by the force-directed engine, an initial pseudo-state marker entering the first declared state,
/// and transitions as orthogonal arrows annotated with their guard conditions.
/// </summary>
/// <remarks>
/// Transitions are routed with <see cref="ChannelRouter"/> (orthogonal) rather than Bezier curves;
/// self-transitions are drawn as a small loop above the state. The initial state is taken to be the
/// first state declared in the owning definition.
/// </remarks>
internal sealed class StateTransitionViewLayoutStrategy : ILayoutStrategy
{
    /// <summary>Minimum width of a state box.</summary>
    private const double MinStateWidth = 100.0;

    /// <summary>Approximate width-per-character factor relative to font size.</summary>
    private const double CharWidthFactor = 0.62;

    /// <summary>Nominal spacing between adjacent state centres in the force layout.</summary>
    private const double StateSpacing = 160.0;

    /// <summary>Clearance kept between routed transitions and state boxes.</summary>
    private const double TransitionClearance = 12.0;

    /// <summary>Diameter of the initial pseudo-state marker.</summary>
    private const double InitialMarkerSize = 18.0;

    /// <summary>A state with its computed box size.</summary>
    private sealed record StateItem(string Name, double Width, double Height);

    /// <summary>A resolved transition between two state indices with an optional guard.</summary>
    private sealed record TransitionItem(int Source, int Target, string? Guard);

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

        var (states, index) = CollectStates(root, theme);
        if (states.Count == 0)
        {
            return new LayoutTree(200.0, 100.0, []);
        }

        var transitions = ResolveTransitions(root, index);

        // Place state boxes with the force-directed engine using transitions as springs.
        var margin = theme.LabelPadding * 4.0;
        var force = ForceDirectedEngine.Place(
            [.. states.Select(s => new ForceNode(s.Width, s.Height))],
            [.. transitions.Where(t => t.Source != t.Target).Select(t => new ForceEdge(t.Source, t.Target))],
            spacing: StateSpacing,
            padding: margin + InitialMarkerSize);

        var stateRects = new Rect[states.Count];
        for (var i = 0; i < states.Count; i++)
        {
            var r = force.Rects[i];
            stateRects[i] = new Rect(r.X, r.Y, r.Width, r.Height);
        }

        var nodes = new List<LayoutNode>();

        // State boxes (rounded rectangles).
        for (var i = 0; i < states.Count; i++)
        {
            nodes.Add(MakeStateBox(states[i], stateRects[i]));
        }

        // Initial pseudo-state entering the first declared state.
        AddInitialMarker(stateRects[0], nodes);

        // Transition edges with guard labels.
        AddTransitions(transitions, stateRects, nodes);

        return new LayoutTree(force.Width, force.Height, nodes);
    }

    /// <summary>Finds the definition with the most transitions to use as the diagram root.</summary>
    private static SysmlDefinitionNode? FindRoot(SysmlWorkspace workspace)
    {
        SysmlDefinitionNode? best = null;
        var bestTransitions = -1;

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

            var transitions = def.Children.OfType<SysmlTransitionNode>().Count();
            if (transitions > bestTransitions)
            {
                best = def;
                bestTransitions = transitions;
            }
        }

        return best;
    }

    /// <summary>
    /// Collects the states of the root definition — both declared state usages and any state names
    /// referenced only by transitions — and builds a name → index lookup.
    /// </summary>
    private static (IReadOnlyList<StateItem> States, Dictionary<string, int> Index) CollectStates(
        SysmlDefinitionNode root,
        Theme theme)
    {
        var states = new List<StateItem>();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);

        void Add(string name)
        {
            if (index.ContainsKey(name))
            {
                return;
            }

            index[name] = states.Count;
            var (width, height) = ComputeStateSize(name, theme);
            states.Add(new StateItem(name, width, height));
        }

        // Declared state usages first (preserves declaration order for the initial-state choice).
        foreach (var feature in root.Children.OfType<SysmlFeatureNode>())
        {
            if (feature.FeatureKeyword == "state" && feature.Name is not null)
            {
                Add(feature.Name);
            }
        }

        // Any additional states referenced only by transition endpoints.
        foreach (var transition in root.Children.OfType<SysmlTransitionNode>())
        {
            if (LastSegment(transition.Source) is { } s)
            {
                Add(s);
            }

            if (LastSegment(transition.Target) is { } t)
            {
                Add(t);
            }
        }

        return (states, index);
    }

    /// <summary>Resolves transition endpoints to state indices via their last name segment.</summary>
    private static IReadOnlyList<TransitionItem> ResolveTransitions(SysmlDefinitionNode root, Dictionary<string, int> index)
    {
        var result = new List<TransitionItem>();
        foreach (var transition in root.Children.OfType<SysmlTransitionNode>())
        {
            var source = LastSegment(transition.Source);
            var target = LastSegment(transition.Target);
            if (source is null || target is null ||
                !index.TryGetValue(source, out var si) || !index.TryGetValue(target, out var ti))
            {
                continue;
            }

            result.Add(new TransitionItem(si, ti, transition.Guard));
        }

        return result;
    }

    /// <summary>Computes the intrinsic size of a state box.</summary>
    private static (double Width, double Height) ComputeStateSize(string name, Theme theme)
    {
        var labelWidth = (name.Length * theme.FontSizeTitle * CharWidthFactor) + (4.0 * theme.LabelPadding);
        var width = Math.Max(MinStateWidth, labelWidth);
        var height = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true) + theme.LabelPadding;
        return (width, height);
    }

    /// <summary>Creates a rounded-rectangle state box at the given position.</summary>
    private static LayoutBox MakeStateBox(StateItem state, Rect rect) =>
        new(
            X: rect.X,
            Y: rect.Y,
            Width: rect.Width,
            Height: rect.Height,
            Label: state.Name,
            Depth: 1,
            Shape: BoxShape.RoundedRectangle,
            Compartments: [],
            Children: [],
            Keyword: "state");

    /// <summary>Adds the initial pseudo-state marker and its arrow into the first state.</summary>
    private static void AddInitialMarker(Rect first, List<LayoutNode> nodes)
    {
        // Place the marker above the first state, centred horizontally.
        var markerX = first.X + (first.Width / 2.0);
        var markerY = first.Y - InitialMarkerSize - 10.0;

        nodes.Add(new LayoutBadge(markerX, markerY, InitialMarkerSize, BadgeShape.FilledCircle, null));

        // Straight arrow from the marker down to the top of the first state.
        nodes.Add(new LayoutLine(
            Waypoints: [new Point2D(markerX, markerY + (InitialMarkerSize / 2.0)), new Point2D(markerX, first.Y)],
            SourceArrowhead: ArrowheadStyle.None,
            TargetArrowhead: ArrowheadStyle.Filled,
            LineStyle: LineStyle.Solid,
            MidpointLabel: null));
    }

    /// <summary>Adds transition edges (with guard labels) between state boxes.</summary>
    private static void AddTransitions(
        IReadOnlyList<TransitionItem> transitions,
        Rect[] stateRects,
        List<LayoutNode> nodes)
    {
        foreach (var transition in transitions)
        {
            var label = transition.Guard is { Length: > 0 } g ? $"[{g}]" : null;

            if (transition.Source == transition.Target)
            {
                nodes.Add(BuildSelfLoop(stateRects[transition.Source], label));
                continue;
            }

            var from = stateRects[transition.Source];
            var to = stateRects[transition.Target];
            var (source, sourceSide) = AnchorToward(from, Centre(to));
            var (target, targetSide) = AnchorToward(to, Centre(from));

            var obstacles = new List<Rect>();
            for (var i = 0; i < stateRects.Length; i++)
            {
                if (i != transition.Source && i != transition.Target)
                {
                    obstacles.Add(stateRects[i]);
                }
            }

            var waypoints = ChannelRouter.Route(source, target, obstacles, TransitionClearance, sourceSide, targetSide);
            nodes.Add(new LayoutLine(
                Waypoints: waypoints,
                SourceArrowhead: ArrowheadStyle.None,
                TargetArrowhead: ArrowheadStyle.Filled,
                LineStyle: LineStyle.Solid,
                MidpointLabel: label));
        }
    }

    /// <summary>Builds a small self-transition loop above the state box.</summary>
    private static LayoutLine BuildSelfLoop(Rect box, string? label)
    {
        const double Loop = 22.0;
        var x1 = box.X + (box.Width * 0.35);
        var x2 = box.X + (box.Width * 0.65);
        var top = box.Y;

        var waypoints = new List<Point2D>
        {
            new(x1, top),
            new(x1, top - Loop),
            new(x2, top - Loop),
            new(x2, top),
        };

        return new LayoutLine(
            Waypoints: waypoints,
            SourceArrowhead: ArrowheadStyle.None,
            TargetArrowhead: ArrowheadStyle.Filled,
            LineStyle: LineStyle.Solid,
            MidpointLabel: label);
    }

    /// <summary>
    /// Returns the midpoint of the box side whose outward normal best points at the target, along
    /// with that side.
    /// </summary>
    private static (Point2D Point, PortSide Side) AnchorToward(Rect box, Point2D target)
    {
        var cx = box.X + (box.Width / 2.0);
        var cy = box.Y + (box.Height / 2.0);
        var dx = target.X - cx;
        var dy = target.Y - cy;

        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            return dx >= 0
                ? (new Point2D(box.X + box.Width, cy), PortSide.Right)
                : (new Point2D(box.X, cy), PortSide.Left);
        }

        return dy >= 0
            ? (new Point2D(cx, box.Y + box.Height), PortSide.Bottom)
            : (new Point2D(cx, box.Y), PortSide.Top);
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
