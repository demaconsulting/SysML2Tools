// <copyright file="StateTransitionViewLayoutStrategy.cs" company="DemaConsulting">
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
/// Layout strategy for State Transition View diagrams. Renders state usages as rounded boxes placed
/// top-to-bottom by the layered layout pipeline, an initial pseudo-state marker entering the first
/// declared state, and transitions as orthogonal arrows annotated with their guard conditions.
/// </summary>
/// <remarks>
/// States are placed by <see cref="LayeredLayoutPipeline"/> with <see cref="LayoutDirection.Down"/> so
/// the state machine reads top-to-bottom: a transition leaves its source on the SOUTH face and enters
/// its target on the NORTH face, and each transition follows the orthogonal polyline the pipeline
/// routed for it. The cyclic transition graph is made acyclic by the pipeline's cycle-breaking stage;
/// self-transitions are drawn as a small loop above the state. The initial state is taken to be the
/// first state declared in the owning definition.
/// </remarks>
internal sealed class StateTransitionViewLayoutStrategy : ILayoutStrategy
{
    /// <summary>Minimum width of a state box.</summary>
    private const double MinStateWidth = 100.0;

    /// <summary>Approximate width-per-character factor relative to font size.</summary>
    private const double CharWidthFactor = 0.62;

    /// <summary>Diameter of the initial pseudo-state marker.</summary>
    private const double InitialMarkerSize = 18.0;

    /// <summary>Vertical gap between the initial pseudo-state marker and the first state box.</summary>
    private const double InitialMarkerGap = 10.0;

    /// <summary>
    /// Lateral offset applied to successive transitions that share a routed corridor (parallel guards
    /// or a forward/back-edge pair), so their anchor points and labels do not coincide.
    /// </summary>
    private const double AnchorSpread = 12.0;

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

        // Place state boxes with the layered pipeline flowing top-to-bottom (DOWN). Non-self
        // transitions become directed edges; the pipeline's cycle-breaking stage makes the (cyclic)
        // state graph acyclic, so it tolerates back edges and loops.
        var layerNodes = states.Select(s => new LayerNode(s.Width, s.Height)).ToList();
        var flowTransitions = transitions.Where(t => t.Source != t.Target).ToList();
        var layerEdges = flowTransitions.Select(t => new LayerEdge(t.Source, t.Target)).ToList();

        var graph = new LayeredGraph(layerNodes, layerEdges, LayoutDirection.Down)
        {
            // Reserve a clean straight approach for the open-chevron target marker that every
            // transition carries, so the pipeline pushes reversed (back) edges far enough out that the
            // renderer's rounded corner never intrudes into the decoration. The approach equals the
            // marker's along-line length plus one corner radius (consumed by the rounded corner) plus
            // a safety margin.
            BackEdgeEntryApproach = NotationMetrics.AlongLineLength(EndMarkerStyle.OpenChevron)
                + theme.LineCornerRadius + theme.CleanLegMargin,
        };
        var pipeline = LayeredLayoutPipeline.Builder()
            .Direction(LayoutDirection.Down)
            .Hierarchy(HierarchyHandling.Flat)
            .AddDefaultStages()
            .Build();
        pipeline.Run(graph);

        // Compute the top-left of the content bounding box over the real state nodes and the screen
        // offset that normalizes it into the canvas (leaving room at the top for the initial marker).
        var margin = theme.LabelPadding * 4.0;
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        for (var i = 0; i < states.Count; i++)
        {
            minX = Math.Min(minX, graph.AugX[i]);
            minY = Math.Min(minY, graph.AugY[i]);
        }

        var topReserve = margin + InitialMarkerSize + InitialMarkerGap;
        var offsetX = margin - minX;
        var offsetY = topReserve - minY;

        var stateRects = new Rect[states.Count];
        for (var i = 0; i < states.Count; i++)
        {
            stateRects[i] = new Rect(graph.AugX[i] + offsetX, graph.AugY[i] + offsetY, states[i].Width, states[i].Height);
        }

        var nodes = new List<LayoutNode>();

        // State boxes (rounded rectangles).
        for (var i = 0; i < states.Count; i++)
        {
            nodes.Add(MakeStateBox(states[i], stateRects[i]));
        }

        // Initial pseudo-state entering the first declared state.
        AddInitialMarker(stateRects[0], nodes);

        // Transition edges (with guard labels), mapped from the pipeline's routed polylines.
        var crossings = AddTransitions(transitions, graph, stateRects, offsetX, offsetY, nodes);

        // Size the canvas to the actual rendered content: state boxes plus routed transition
        // polylines (back edges can bulge beyond the box columns), the initial marker, and the
        // guard labels. Guard labels are drawn centred on their segment midpoints, so most sit on
        // interior vertical segments already inside the content extent and add nothing; only the
        // part of a label that genuinely overhangs the polyline extent enlarges the canvas. We size
        // to each label's true rendered right edge (computed with the same placer the renderers use)
        // rather than adding the longest guard-label width wholesale.
        var (contentMaxX, contentMaxY) = ContentExtent(nodes);
        var labelMaxX = LabelRightExtent(nodes, theme);

        var width = Math.Max(contentMaxX, labelMaxX) + margin;
        var height = contentMaxY + margin + theme.FontSizeTitle;
        var warnings = LayoutWarnings.ForCrossings(context.ViewName, crossings);
        return new LayoutTree(width, height, nodes) { Warnings = warnings };
    }

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

    /// <summary>Character-width factor used by <see cref="ConnectorLabelPlacer"/> to size labels.</summary>
    private const double LabelCharWidthFactor = 0.6;

    /// <summary>
    /// Returns the maximum X reached by any rendered guard label. Labels are placed with the same
    /// <see cref="ConnectorLabelPlacer"/> the SVG and PNG renderers use and drawn centred
    /// (<c>text-anchor="middle"</c>) on their chosen segment midpoint, so a label's true right edge
    /// is its centre X plus half its rendered text width. Most guard labels sit on interior vertical
    /// segments and do not overhang the content, so this typically adds little or nothing.
    /// </summary>
    private static double LabelRightExtent(IReadOnlyList<LayoutNode> nodes, Theme theme)
    {
        var lines = nodes
            .OfType<LayoutLine>()
            .Where(line => line.MidpointLabel is not null)
            .ToList();
        if (lines.Count == 0)
        {
            return 0.0;
        }

        var positions = ConnectorLabelPlacer.Place(lines, theme.FontSizeBody);
        var maxX = 0.0;
        foreach (var line in lines)
        {
            if (!positions.TryGetValue(line, out var pos))
            {
                continue;
            }

            var halfTextWidth = line.MidpointLabel!.Length * theme.FontSizeBody * LabelCharWidthFactor / 2.0;
            maxX = Math.Max(maxX, pos.X + halfTextWidth);
        }

        return maxX;
    }
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
        var markerY = first.Y - InitialMarkerSize - InitialMarkerGap;

        nodes.Add(new LayoutBadge(markerX, markerY, InitialMarkerSize, BadgeShape.FilledCircle, null));

        // Straight arrow from the marker down to the top of the first state.
        nodes.Add(new LayoutLine(
            Waypoints: [new Point2D(markerX, markerY + (InitialMarkerSize / 2.0)), new Point2D(markerX, first.Y)],
            SourceEnd: EndMarkerStyle.None,
            TargetEnd: EndMarkerStyle.FilledArrow,
            LineStyle: LineStyle.Solid,
            MidpointLabel: null));
    }

    /// <summary>
    /// Adds transition edges (with guard labels) between state boxes, mapping each transition to the
    /// orthogonal polyline the layered pipeline routed for it, and returns the number of transitions
    /// whose polyline crosses a non-endpoint state box.
    /// </summary>
    /// <remarks>
    /// The pipeline's cycle-breaking stage drops self-loops, de-duplicates identical directed pairs,
    /// and reverses back edges, so <see cref="LayeredGraph.Waypoints"/> is not 1:1 with the input
    /// transitions. A lookup keyed by the routed <c>(source, target)</c> pair recovers each
    /// transition's polyline; a transition whose routed edge was reversed reuses that polyline in
    /// reverse so the open arrowhead lands on the true target. Successive transitions sharing one
    /// routed corridor (parallel guards, or a forward/back-edge pair) are spread laterally so their
    /// anchor points and labels do not coincide.
    /// </remarks>
    private static int AddTransitions(
        IReadOnlyList<TransitionItem> transitions,
        LayeredGraph graph,
        Rect[] stateRects,
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

        var corridorUse = new Dictionary<(int, int), int>();
        var crossings = 0;

        foreach (var transition in transitions)
        {
            var label = transition.Guard is { Length: > 0 } g ? $"[{g}]" : null;

            if (transition.Source == transition.Target)
            {
                nodes.Add(BuildSelfLoop(stateRects[transition.Source], label));
                continue;
            }

            var routedPoints = ResolveTransitionPolyline(transition, routed)
                ?? [Centre(stateRects[transition.Source]), Centre(stateRects[transition.Target])];

            // Spread successive transitions that share the same (undirected) corridor laterally so
            // their anchor points and guard labels do not overlap. The cross-axis for a DOWN layout
            // is the screen X axis, so the lateral offset keeps the orthogonal segments axis-aligned.
            var corridor = transition.Source < transition.Target
                ? (transition.Source, transition.Target)
                : (transition.Target, transition.Source);
            var occurrence = corridorUse.GetValueOrDefault(corridor, 0);
            corridorUse[corridor] = occurrence + 1;
            var spread = occurrence * AnchorSpread;

            var waypoints = routedPoints
                .Select(p => new Point2D(p.X + offsetX + spread, p.Y + offsetY))
                .ToList();

            if (CrossesNonEndpointBox(waypoints, stateRects, transition.Source, transition.Target))
            {
                crossings++;
            }

            nodes.Add(new LayoutLine(
                Waypoints: waypoints,
                SourceEnd: EndMarkerStyle.None,
                TargetEnd: EndMarkerStyle.OpenChevron,
                LineStyle: LineStyle.Solid,
                MidpointLabel: label));
        }

        return crossings;
    }

    /// <summary>
    /// Returns the routed polyline for a non-self transition, reversing it when only the opposite
    /// direction was routed (a reversed back edge), or null when neither direction was routed.
    /// </summary>
    private static IReadOnlyList<Point2D>? ResolveTransitionPolyline(
        TransitionItem transition,
        IReadOnlyDictionary<(int Source, int Target), IReadOnlyList<Point2D>> routed)
    {
        if (routed.TryGetValue((transition.Source, transition.Target), out var forward))
        {
            return forward;
        }

        if (routed.TryGetValue((transition.Target, transition.Source), out var backward))
        {
            // The pipeline reversed this back edge; reverse the polyline so it runs source -> target
            // with the open arrowhead at the true target.
            return [.. backward.Reverse()];
        }

        return null;
    }

    /// <summary>
    /// Returns whether any segment of the polyline passes through the interior of a state box other
    /// than the transition's own source or target.
    /// </summary>
    private static bool CrossesNonEndpointBox(
        IReadOnlyList<Point2D> waypoints,
        Rect[] stateRects,
        int source,
        int target)
    {
        for (var j = 0; j < stateRects.Length; j++)
        {
            if (j == source || j == target)
            {
                continue;
            }

            for (var s = 0; s + 1 < waypoints.Count; s++)
            {
                if (SegmentIntersectsRect(waypoints[s], waypoints[s + 1], stateRects[j]))
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
            SourceEnd: EndMarkerStyle.None,
            TargetEnd: EndMarkerStyle.OpenChevron,
            LineStyle: LineStyle.Solid,
            MidpointLabel: label);
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
