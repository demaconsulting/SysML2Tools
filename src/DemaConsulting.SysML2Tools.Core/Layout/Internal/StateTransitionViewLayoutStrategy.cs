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
    private const double StateSpacing = 240.0;

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
        var flowEdges = transitions.Where(t => t.Source != t.Target).Select(t => new ForceEdge(t.Source, t.Target)).ToList();

        // Directed-flow hierarchy: layer hints bias states into a near-hard top-to-bottom reading
        // direction (kHier = 1.0) so transitions mostly flow downward.
        var connectivity = ConnectivityAnalyzer.Analyze(
            [.. states.Select(s => new ConnectivityNode(s.Name))],
            [.. flowEdges.Select(e => new ConnectivityEdge(e.A, e.B))]);

        // Place state boxes with the force-directed engine using transitions as springs.
        var margin = theme.LabelPadding * 4.0;
        var force = ForceDirectedEngine.Place(
            [.. states.Select(s => new ForceNode(s.Width, s.Height))],
            flowEdges,
            spacing: StateSpacing,
            padding: margin + InitialMarkerSize,
            kHier: 1.0,
            layerHints: connectivity.LayerHints);

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

        // Highway routing: bundle parallel transitions onto shared corridors with cost bands so the
        // detailed router prefers them, reducing wire overlap. The force/layer placement is preserved
        // (no compression pass) so the established state arrangement and back-edge clearance are kept.
        var highwayBoxes = stateRects.Select((r, i) => new HighwayBox(r.X, r.Y, r.Width, r.Height, i.ToString())).ToList();
        var highwayEdges = transitions.Where(t => t.Source != t.Target).Select(t => new HighwayEdge(t.Source, t.Target, "transition")).ToList();
        var highway = HighwayAssigner.Assign(highwayBoxes, highwayEdges, theme.LabelPadding * 2.0, TransitionClearance, TransitionClearance * 2.0);
        var costBands = highway.Corridors
            .Where(c => c.IsHighway)
            .Select(c => new CostBand(c.IsHorizontal, c.Position - (c.ReservedWidth / 2.0), c.Position + (c.ReservedWidth / 2.0), 0.6))
            .ToList();

        // Transition edges with guard labels.
        var crossings = AddTransitions(transitions, stateRects, nodes, connectivity.LayerHints, costBands);

        var warnings = LayoutWarnings.ForCrossings(context.ViewName, crossings);
        return new LayoutTree(force.Width, force.Height, nodes) { Warnings = warnings };
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

    /// <summary>
    /// Adds transition edges (with guard labels) between state boxes, returning the number of edges
    /// that could not be routed without crossing a state box.
    /// </summary>
    /// <remarks>
    /// Each transition end attaches to the side of its box that faces the other state. When several
    /// transitions share the same box side, their anchor points are distributed evenly along that
    /// side (ordered to face their counterparts) instead of all stacking on the side midpoint, so an
    /// incoming arrowhead never coincides with another transition's endpoint.
    /// </remarks>
    private static int AddTransitions(
        IReadOnlyList<TransitionItem> transitions,
        Rect[] stateRects,
        List<LayoutNode> nodes,
        IReadOnlyList<int> layerHints,
        IReadOnlyList<CostBand> costBands)
    {
        var count = transitions.Count;
        var srcSide = new PortSide[count];
        var tgtSide = new PortSide[count];
        var srcPoint = new Point2D[count];
        var tgtPoint = new Point2D[count];

        // Pass 1: determine the side each transition end attaches to.
        for (var i = 0; i < count; i++)
        {
            var transition = transitions[i];
            if (transition.Source == transition.Target)
            {
                continue;
            }

            srcSide[i] = SideToward(stateRects[transition.Source], Centre(stateRects[transition.Target]));
            tgtSide[i] = SideToward(stateRects[transition.Target], Centre(stateRects[transition.Source]));
        }

        // Pass 2: group endpoints by (state, side) and distribute them evenly along each side.
        var groups = new Dictionary<(int State, PortSide Side), List<(int Trans, bool IsSource, double Order)>>();
        for (var i = 0; i < count; i++)
        {
            var transition = transitions[i];
            if (transition.Source == transition.Target)
            {
                continue;
            }

            AddEndpoint(groups, transition.Source, srcSide[i], i, isSource: true, OrderKey(srcSide[i], Centre(stateRects[transition.Target])));
            AddEndpoint(groups, transition.Target, tgtSide[i], i, isSource: false, OrderKey(tgtSide[i], Centre(stateRects[transition.Source])));
        }

        foreach (var group in groups)
        {
            var ordered = group.Value.OrderBy(e => e.Order).ToList();

            // Collapse runs of consecutive same-direction endpoints into shared anchor slots: this
            // keeps inputs and outputs on separate points (so direction is never ambiguous) while
            // reducing the number of distinct points on a busy edge. The crossing-minimizing order
            // (by counterpart position) is preserved, so only adjacent same-direction edges merge.
            var slots = new List<(bool IsSource, List<int> Trans)>();
            foreach (var endpoint in ordered)
            {
                if (slots.Count == 0 || slots[^1].IsSource != endpoint.IsSource)
                {
                    slots.Add((endpoint.IsSource, []));
                }

                slots[^1].Trans.Add(endpoint.Trans);
            }

            for (var s = 0; s < slots.Count; s++)
            {
                var frac = (s + 1.0) / (slots.Count + 1.0);
                var point = PointOnSide(stateRects[group.Key.State], group.Key.Side, frac);
                foreach (var trans in slots[s].Trans)
                {
                    if (slots[s].IsSource)
                    {
                        srcPoint[trans] = point;
                    }
                    else
                    {
                        tgtPoint[trans] = point;
                    }
                }
            }
        }

        // Pass 3: route each transition and build its line.
        var crossings = 0;
        for (var i = 0; i < count; i++)
        {
            var transition = transitions[i];
            var label = transition.Guard is { Length: > 0 } g ? $"[{g}]" : null;

            if (transition.Source == transition.Target)
            {
                nodes.Add(BuildSelfLoop(stateRects[transition.Source], label));
                continue;
            }

            var obstacles = new List<Rect>();
            for (var j = 0; j < stateRects.Length; j++)
            {
                if (j != transition.Source && j != transition.Target)
                {
                    obstacles.Add(stateRects[j]);
                }
            }

            // Back edges (target sits in an earlier layer) detour around the flow with a wider
            // clearance, since the orthogonal router has no arc support.
            var isBackEdge = transition.Source < layerHints.Count && transition.Target < layerHints.Count &&
                layerHints[transition.Source] > layerHints[transition.Target];
            var clearance = isBackEdge ? TransitionClearance * 2.5 : TransitionClearance;
            var route = ChannelRouter.RouteWithStatus(srcPoint[i], tgtPoint[i], obstacles, clearance, srcSide[i], tgtSide[i], costBands);
            if (route.Crossed)
            {
                crossings++;
            }

            nodes.Add(new LayoutLine(
                Waypoints: route.Waypoints,
                SourceArrowhead: ArrowheadStyle.None,
                TargetArrowhead: ArrowheadStyle.Open,
                LineStyle: LineStyle.Solid,
                MidpointLabel: label));
        }

        return crossings;
    }

    /// <summary>Registers a transition endpoint against the (state, side) group it attaches to.</summary>
    private static void AddEndpoint(
        Dictionary<(int State, PortSide Side), List<(int Trans, bool IsSource, double Order)>> groups,
        int state,
        PortSide side,
        int trans,
        bool isSource,
        double order)
    {
        var key = (state, side);
        if (!groups.TryGetValue(key, out var list))
        {
            list = [];
            groups[key] = list;
        }

        list.Add((trans, isSource, order));
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
            TargetArrowhead: ArrowheadStyle.Open,
            LineStyle: LineStyle.Solid,
            MidpointLabel: label);
    }

    /// <summary>
    /// Returns the side of the box whose outward normal best points at the target.
    /// </summary>
    private static PortSide SideToward(Rect box, Point2D target)
    {
        var cx = box.X + (box.Width / 2.0);
        var cy = box.Y + (box.Height / 2.0);
        var dx = target.X - cx;
        var dy = target.Y - cy;

        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            return dx >= 0 ? PortSide.Right : PortSide.Left;
        }

        return dy >= 0 ? PortSide.Bottom : PortSide.Top;
    }

    /// <summary>
    /// Returns the point at fractional position <paramref name="frac"/> (0..1) along the given side
    /// of the box.
    /// </summary>
    private static Point2D PointOnSide(Rect box, PortSide side, double frac) => side switch
    {
        PortSide.Top => new Point2D(box.X + (frac * box.Width), box.Y),
        PortSide.Bottom => new Point2D(box.X + (frac * box.Width), box.Y + box.Height),
        PortSide.Left => new Point2D(box.X, box.Y + (frac * box.Height)),
        _ => new Point2D(box.X + box.Width, box.Y + (frac * box.Height)),
    };

    /// <summary>
    /// Returns the ordering key used to lay endpoints out along a side so their connectors fan out
    /// toward their counterparts without crossing: the counterpart coordinate along the side's axis.
    /// </summary>
    private static double OrderKey(PortSide side, Point2D counterpart) =>
        side is PortSide.Top or PortSide.Bottom ? counterpart.X : counterpart.Y;

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
