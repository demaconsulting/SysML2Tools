// <copyright file="StateTransitionViewLayoutStrategy.cs" company="DemaConsulting">
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
/// Layout strategy for State Transition View diagrams. Renders state usages as rounded boxes placed
/// top-to-bottom by the layered layout algorithm inside a frame headed <c>«state def» Name</c>, an
/// initial pseudo-state marker entering the explicitly declared initial state, a final (done)
/// pseudo-state, and transitions as orthogonal arrows labeled <c>trigger [guard] / effect</c>.
/// </summary>
/// <remarks>
/// States are placed by the bundled layered algorithm flowing top-to-bottom (down) so the state
/// machine reads top-to-bottom: a transition leaves its source on the SOUTH face and enters its target
/// on the NORTH face, and each transition follows the orthogonal polyline the algorithm routed for it.
/// The cyclic transition graph is made acyclic by the algorithm's cycle-breaking stage; self-transitions
/// are drawn as a small loop above the state. The initial state is the target of the explicit initial
/// transition (<c>first start then X;</c> or an entry-action-sourced transition); a state machine that
/// declares none is drawn without an initial marker. A transition whose target is the reserved name
/// <c>done</c> ends in one shared final badge (a bullseye) rather than a state box. A state that
/// declares <c>entry</c>, <c>do</c>, or <c>exit</c> actions lists them in compartments titled with
/// the keyword. The whole diagram is one frame box (labeled with the state definition's keyword and
/// name) whose children are the state boxes, the pseudo-state badges, and the transition lines in the
/// frame's own absolute coordinate space. Composite (nested) states and control nodes are not drawn:
/// a composite state renders as an ordinary box and its nested states are ignored, and the grammar
/// admits no fork, join, decision, merge, or terminate node inside a state body.
/// </remarks>
internal sealed class StateTransitionViewLayoutStrategy : ILayoutStrategy
{
    /// <summary>Minimum width of a state box.</summary>
    private const double MinStateWidth = 100.0;

    /// <summary>Approximate width-per-character factor relative to font size.</summary>
    private const double CharWidthFactor = 0.62;

    /// <summary>Diameter of the initial pseudo-state marker.</summary>
    private const double InitialMarkerSize = 18.0;

    /// <summary>Diameter of the final (done) pseudo-state badge.</summary>
    private const double FinalMarkerSize = 18.0;

    /// <summary>The reserved name of the final (completion) pseudo-state, the target of <c>then done;</c>.</summary>
    private const string DoneName = "done";

    /// <summary>Vertical gap between the initial pseudo-state marker and the first state box.</summary>
    private const double InitialMarkerGap = 10.0;

    /// <summary>
    /// Lateral offset applied to successive transitions that share a routed corridor (parallel guards
    /// or a forward/back-edge pair), so their anchor points and labels do not coincide.
    /// </summary>
    private const double AnchorSpread = 12.0;

    /// <summary>
    /// Inset between the canvas edge and the state definition frame on every side. The frame's 1 px
    /// outline is centered on its geometric edge, so the frame must sit at least half the stroke
    /// width plus one anti-aliasing pixel inside the canvas for the outline to be fully visible.
    /// </summary>
    private const double FrameInset = 2.0;

    /// <summary>
    /// A state with its computed box size, displayed name line, and entry/do/exit compartments. A
    /// final item (<paramref name="IsFinal"/>) is the shared completion pseudo-state drawn as a badge
    /// rather than a box.
    /// </summary>
    private sealed record StateItem(
        string Name,
        double Width,
        double Height,
        string NameLine,
        IReadOnlyList<LayoutCompartment> Compartments,
        bool IsFinal = false);

    /// <summary>A resolved transition between two state indices with an optional <c>trigger [guard] / effect</c> label.</summary>
    private sealed record TransitionItem(int Source, int Target, string? Label);

    /// <inheritdoc/>
    public LayoutTree BuildLayout(ViewContext context, RenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        var theme = options.Theme;

        var scope = ExposeScopeResolver.ResolveExposedScope(context.Workspace, context.ViewNode);

        var root = FindRoot(context.Workspace, scope);
        if (root is null)
        {
            return new LayoutTree(200.0, 100.0, []);
        }

        var (states, index, pseudoSourceNames) = CollectStates(root, theme, scope);
        if (states.Count == 0)
        {
            return new LayoutTree(200.0, 100.0, []);
        }

        var transitions = ResolveTransitions(root, index, states);

        // Place state boxes with the layered algorithm flowing top-to-bottom (down). Non-self
        // transitions become directed edges; the algorithm's cycle-breaking stage makes the (cyclic)
        // state graph acyclic, so it tolerates back edges and loops. MergeParallelEdges is disabled so
        // AddTransitions' 1:1 edgePolylines[e] indexing (see its own remarks) stays valid: with merging
        // enabled, a back edge sharing a node pair with a forward edge (a cycle) collapses onto one
        // shared line, leaving fewer routed lines than input transitions.
        var flowTransitions = transitions.Where(t => t.Source != t.Target).ToList();
        var placed = LayeredPlacement.Place(
            states.Select(s => (s.Width, s.Height)).ToList(),
            flowTransitions.Select(t => (t.Source, t.Target)).ToList(),
            LayoutFlowDirection.Down,
            mergeParallelEdges: false,
            edgeTargetEnd: EndMarkerStyle.OpenChevron);

        // Compute the top-left of the content bounding box over the real state nodes and the screen
        // offset that normalizes it into the canvas. The state definition is drawn as an enclosing
        // frame (see below), so the top reserves room for the frame's «state def» title band and,
        // only when an initial marker is drawn, for that marker and its arrow.
        var margin = theme.LabelPadding * 4.0;
        var titleArea = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true);
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        for (var i = 0; i < states.Count; i++)
        {
            minX = Math.Min(minX, placed.Rects[i].X);
            minY = Math.Min(minY, placed.Rects[i].Y);
        }

        // An initial marker exists only for an explicit initial transition (`first start then X;` or
        // an entry-action-sourced transition); the specification draws none for a state machine
        // that does not declare one, so no first-declared-state guess is made.
        var initialIndex = FindInitialTargetIndex(root, pseudoSourceNames, index);
        var markerReserve = initialIndex is null ? 0.0 : InitialMarkerSize + InitialMarkerGap;
        var topReserve = titleArea + margin + markerReserve;
        var offsetX = FrameInset + margin - minX;
        var offsetY = FrameInset + topReserve - minY;

        // Builds the state boxes, the final badge, the initial marker and the transition lines for a
        // given horizontal offset, in the frame's absolute coordinate space.
        (List<LayoutNode> Nodes, int Crossings) Assemble(double shiftX)
        {
            var stateRects = new Rect[states.Count];
            for (var i = 0; i < states.Count; i++)
            {
                stateRects[i] = new Rect(placed.Rects[i].X + shiftX, placed.Rects[i].Y + offsetY, states[i].Width, states[i].Height);
            }

            var built = new List<LayoutNode>();

            // State boxes (rounded rectangles) and the final badge.
            for (var i = 0; i < states.Count; i++)
            {
                built.Add(states[i].IsFinal ? MakeFinalBadge(stateRects[i]) : MakeStateBox(states[i], stateRects[i]));
            }

            // Initial pseudo-state entering the semantically-resolved initial target state.
            if (initialIndex is { } initial)
            {
                AddInitialMarker(stateRects[initial], built);
            }

            // Transition edges (with labels), mapped from the algorithm's routed polylines.
            var crossingCount = AddTransitions(transitions, flowTransitions, placed.EdgePolylines, stateRects, shiftX, offsetY, built);
            return (built, crossingCount);
        }

        // A transition label is centered on its placed position and may overhang the states on either
        // side. When one overhangs the left edge of the frame content, shift everything right so the
        // label keeps a margin inside the frame (the renderers would otherwise clip it).
        var (nodes, crossings) = Assemble(offsetX);
        var leftOverhang = FrameInset + theme.LabelPadding - LabelLeftExtent(nodes, theme);
        if (leftOverhang > 0.0)
        {
            offsetX += leftOverhang;
            (nodes, crossings) = Assemble(offsetX);
        }

        // Size the frame to the actual rendered content: state boxes plus routed transition
        // polylines (back edges can bulge beyond the box columns), the initial marker, and the
        // guard labels. Transition labels are drawn centered on their placed positions, so most sit on
        // interior vertical segments already inside the content extent and add nothing; only the
        // part of a label that genuinely overhangs the polyline extent enlarges the canvas. We size
        // to each label's true rendered right edge (computed with the same obstacle-aware placer the
        // renderers use) rather than adding the longest label width wholesale. The frame is also at
        // least as wide as its own title so a long definition name is never clipped.
        var (contentMaxX, contentMaxY) = ContentExtent(nodes);
        var labelMaxX = LabelRightExtent(nodes, theme);

        var frameLabel = root.Name ?? "State";
        var frameKeyword = string.IsNullOrEmpty(root.DefinitionKeyword) ? "state def" : root.DefinitionKeyword;
        var titleWidth = (Math.Max(frameLabel.Length * theme.FontSizeTitle, (frameKeyword.Length + 2) * theme.FontSizeBody) * CharWidthFactor)
            + (4.0 * theme.LabelPadding);
        var frameRight = Math.Max(Math.Max(contentMaxX, labelMaxX) + margin, FrameInset + titleWidth);
        var frameBottom = contentMaxY + margin + theme.FontSizeTitle;
        var frameWidth = frameRight - FrameInset;
        var frameHeight = frameBottom - FrameInset;

        // Enclose everything in one frame headed «state def» Name with the states and transitions
        // inside it. Children keep the absolute canvas coordinates they were built with (their
        // offsets already include FrameInset), so the layout needs no coordinate translation. The
        // frame sits FrameInset inside the canvas edge so its outline stroke is not clipped.
        var frame = new LayoutBox(
            X: FrameInset,
            Y: FrameInset,
            Width: frameWidth,
            Height: frameHeight,
            Label: frameLabel,
            Depth: 0,
            Shape: BoxShape.Rectangle,
            Compartments: [],
            Children: nodes,
            Keyword: frameKeyword);

        var warnings = LayoutWarnings.ForCrossings(context.ViewName, crossings);
        return new LayoutTree(frameRight + FrameInset, frameBottom + FrameInset, [frame]) { Warnings = warnings };
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

    /// <summary>
    /// Returns the maximum X reached by any rendered transition label. Labels are placed with the
    /// same obstacle-aware <see cref="ConnectorLabelPlacer"/> overload the SVG and PNG renderers use
    /// (the state boxes are the obstacles, so a label is moved clear of a box corner) and drawn
    /// centered on the chosen position, so a label's true right edge is its center X plus half its
    /// rendered text width. Most labels sit on interior vertical segments and do not overhang the
    /// content, so this typically adds little or nothing.
    /// </summary>
    private static double LabelRightExtent(IReadOnlyList<LayoutNode> nodes, Theme theme)
    {
        var maxX = 0.0;
        foreach (var (position, halfWidth) in PlaceLabels(nodes, theme))
        {
            maxX = Math.Max(maxX, position.X + halfWidth);
        }

        return maxX;
    }

    /// <summary>
    /// Returns the minimum X reached by any rendered transition label (its center X minus half its
    /// rendered text width), or <see cref="double.PositiveInfinity"/> when there are no labels. The
    /// counterpart of <see cref="LabelRightExtent"/> for the frame's left side.
    /// </summary>
    private static double LabelLeftExtent(IReadOnlyList<LayoutNode> nodes, Theme theme)
    {
        var minX = double.PositiveInfinity;
        foreach (var (position, halfWidth) in PlaceLabels(nodes, theme))
        {
            minX = Math.Min(minX, position.X - halfWidth);
        }

        return minX;
    }

    /// <summary>
    /// Places every labeled transition line with the renderers' obstacle-aware placer and returns each
    /// label's placed center and half text width.
    /// </summary>
    private static List<(LabelPlacement Position, double HalfWidth)> PlaceLabels(IReadOnlyList<LayoutNode> nodes, Theme theme)
    {
        var result = new List<(LabelPlacement, double)>();
        var lines = nodes
            .OfType<LayoutLine>()
            .Where(line => line.MidpointLabel is not null)
            .ToList();
        if (lines.Count == 0)
        {
            return result;
        }

        var positions = ConnectorLabelPlacer.Place(
            lines,
            theme.FontSizeBody,
            nodes.OfType<LayoutBox>().ToList(),
            NotationMetrics.MarkerScale(theme));
        foreach (var line in lines)
        {
            if (positions.TryGetValue(line, out var pos))
            {
                result.Add((pos, ConnectorLabelPlacer.EstimateLabelWidth(line.MidpointLabel!, theme.FontSizeBody) / 2.0));
            }
        }

        return result;
    }

    /// <summary>
    /// Finds the state-machine definition whose states/transitions to render: the non-stdlib
    /// definition with the most <see cref="SysmlTransitionNode"/> children. When
    /// <paramref name="scope"/> is non-null (the view's resolved <c>expose</c> containment-subtree
    /// scope), candidates are first restricted to those relevant to the scope via
    /// <see cref="ExposeScopeResolver.IsRootRelevantToScope"/>; because a nested definition and its
    /// ancestor can both be scope-relevant, ties among relevant candidates are then broken by
    /// specificity (deepest/longest qualified name wins) via
    /// <see cref="ExposeScopeResolver.IsMoreSpecificCandidate"/>, with the transition-count heuristic
    /// used only to break ties between equally specific candidates. When no candidate is
    /// scope-relevant, no root is chosen (an empty canvas results). When <paramref name="scope"/> is
    /// <see langword="null"/>, selection is the plain transition-count heuristic, unchanged.
    /// </summary>
    private static SysmlDefinitionNode? FindRoot(SysmlWorkspace workspace, ExposedScope? scope)
    {
        SysmlDefinitionNode? best = null;
        string? bestQualifiedName = null;
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

            if (scope is not null && !ExposeScopeResolver.IsRootRelevantToScope(qualifiedName, scope))
            {
                continue;
            }

            var transitions = def.Children.OfType<SysmlTransitionNode>().Count();
            var scoreBetter = transitions > bestTransitions;
            var isBetter = scope is not null
                ? ExposeScopeResolver.IsMoreSpecificCandidate(qualifiedName, bestQualifiedName, scoreBetter)
                : scoreBetter;

            if (isBetter)
            {
                best = def;
                bestQualifiedName = qualifiedName;
                bestTransitions = transitions;
            }
        }

        return best;
    }

    /// <summary>
    /// Collects the states of the root definition — both declared state usages and any state names
    /// referenced only by transitions — and builds a name → index lookup. When
    /// <paramref name="scope"/> is non-null, a declared <c>state</c>-keyword feature whose own
    /// <c>QualifiedName</c> fails <see cref="ExposeScopeResolver.IsInSubjectScope"/> is skipped;
    /// states synthesized only from transition endpoints have no independent qualified name and are
    /// always added, since they exist only because a transition endpoint that already named them
    /// exists on the (already scope-selected) root.
    /// <para/>
    /// Also collects and returns <c>PseudoSourceNames</c>: names that must never become an ordinary
    /// state box even when referenced only as a transition's source. This is the well-known,
    /// narrowly-scoped set of inherited pseudostate/action feature names (<c>"start"</c>/
    /// <c>"done"</c>, consistent with the same simplification <c>ReferenceResolver</c>'s
    /// <c>TryResolveInheritedActionMember</c> fallback uses) plus every declared <c>entry</c>-keyword
    /// feature's name (the <c>entryActionMember (entryTransitionMember)*</c> idiom's implicit
    /// transition source) declared directly on <paramref name="root"/>.
    /// </summary>
    private static (IReadOnlyList<StateItem> States, Dictionary<string, int> Index, HashSet<string> PseudoSourceNames) CollectStates(
        SysmlDefinitionNode root,
        Theme theme,
        ExposedScope? scope)
    {
        var states = new List<StateItem>();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        var pseudoSourceNames = new HashSet<string>(StringComparer.Ordinal) { "start", "done" };

        void Add(SysmlFeatureNode? declaration, string name)
        {
            if (index.ContainsKey(name))
            {
                return;
            }

            index[name] = states.Count;
            states.Add(BuildStateItem(name, declaration, theme));
        }

        // The completion pseudo-state is one shared badge, however many transitions reach it.
        void AddFinal()
        {
            if (index.ContainsKey(DoneName))
            {
                return;
            }

            index[DoneName] = states.Count;
            states.Add(new StateItem(DoneName, FinalMarkerSize, FinalMarkerSize, DoneName, [], IsFinal: true));
        }

        // Declared state usages first (preserves declaration order for the initial-state choice).
        // Entry/do/exit action features (FeatureKeyword != "state") are always skipped as ordinary
        // state boxes; a named entry action's name is additionally recorded in
        // pseudoSourceNames since it can be an implicit transition source (sub-problem 2's shape).
        foreach (var feature in root.Children.OfType<SysmlFeatureNode>())
        {
            if (feature.FeatureKeyword == "entry" && feature.Name is { } entryName)
            {
                pseudoSourceNames.Add(entryName);
            }

            if (feature.FeatureKeyword != "state" || feature.Name is null)
            {
                continue;
            }

            if (scope is not null && feature.QualifiedName is { Length: > 0 } fqn &&
                !ExposeScopeResolver.IsInSubjectScope(fqn, scope))
            {
                continue;
            }

            Add(feature, feature.Name);
        }

        // Any additional states referenced only by transition endpoints. The source side is
        // skipped when it names a pseudostate/entry-action feature — such a name must never be
        // drawn as an ordinary state box (it is rendered, if at all, only via the initial-marker
        // arrow computed by FindInitialTargetIndex). The target side is unaffected: a real state
        // reached only via such a transition is still a real state and must still get a box — except
        // the reserved name "done", which is the final pseudo-state and becomes a badge.
        foreach (var transition in root.Children.OfType<SysmlTransitionNode>())
        {
            if (LastSegment(transition.Source) is { } s && !pseudoSourceNames.Contains(s))
            {
                Add(null, s);
            }

            if (LastSegment(transition.Target) is { } t)
            {
                if (t == DoneName && !index.ContainsKey(DoneName))
                {
                    AddFinal();
                }
                else
                {
                    Add(null, t);
                }
            }
        }

        return (states, index, pseudoSourceNames);
    }

    /// <summary>
    /// Finds the state index that the semantically-resolved initial transition targets — the first
    /// transition (in declaration order) whose source names a pseudostate/entry-action feature (see
    /// <see cref="CollectStates"/>'s <c>pseudoSourceNames</c>) — or <see langword="null"/> when no
    /// such transition exists (or its target does not resolve to a known state), in which case the
    /// caller draws no initial marker.
    /// </summary>
    private static int? FindInitialTargetIndex(
        SysmlDefinitionNode root,
        HashSet<string> pseudoSourceNames,
        Dictionary<string, int> index)
    {
        foreach (var transition in root.Children.OfType<SysmlTransitionNode>())
        {
            // "done" is the completion pseudostate: a transition leaving it is an ordinary
            // transition, never the initial one (only "start" / entry-action sources are).
            if (LastSegment(transition.Source) is not { } source
                || source == "done"
                || !pseudoSourceNames.Contains(source))
            {
                continue;
            }

            if (LastSegment(transition.Target) is { } target && index.TryGetValue(target, out var targetIndex))
            {
                return targetIndex;
            }
        }

        return null;
    }

    /// <summary>Resolves transition endpoints to state indices via their last name segment.</summary>
    private static IReadOnlyList<TransitionItem> ResolveTransitions(
        SysmlDefinitionNode root,
        Dictionary<string, int> index,
        IReadOnlyList<StateItem> states)
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

            // Nothing leaves the final pseudo-state.
            if (states[si].IsFinal)
            {
                continue;
            }

            result.Add(new TransitionItem(si, ti, BuildTransitionLabel(transition)));
        }

        return result;
    }

    /// <summary>
    /// Builds the specification's transition label <c>trigger [guard] / effect</c> (graphical BNF,
    /// clause 8.2.3: <c>transition-label</c>), omitting every part the transition does not declare:
    /// <c>callReceived [doorsSafe] / raiseCar</c>, <c>[atFloor]</c>, <c>/ chime</c>. A leading
    /// <c>action</c> keyword is dropped from the effect, and runs of whitespace collapse to one space.
    /// Returns null when the transition declares none of the three.
    /// </summary>
    private static string? BuildTransitionLabel(SysmlTransitionNode transition)
    {
        var label = new System.Text.StringBuilder();
        if (NormalizeText(transition.Trigger) is { Length: > 0 } trigger)
        {
            label.Append(trigger);
        }

        if (transition.Guard is { Length: > 0 } guard)
        {
            label.Append(label.Length > 0 ? " " : string.Empty).Append('[').Append(guard).Append(']');
        }

        if (NormalizeText(StripActionKeyword(transition.Effect)) is { Length: > 0 } effect)
        {
            label.Append(label.Length > 0 ? " " : string.Empty).Append("/ ").Append(effect);
        }

        return label.Length > 0 ? label.ToString() : null;
    }

    /// <summary>Removes a leading <c>action</c> keyword (and the whitespace after it) from declaration text.</summary>
    private static string? StripActionKeyword(string? text) =>
        text is not null && text.StartsWith("action", StringComparison.Ordinal) &&
        (text.Length == 6 || char.IsWhiteSpace(text[6]))
            ? text[6..]
            : text;

    /// <summary>Collapses runs of whitespace to single spaces and trims the result; null stays null.</summary>
    private static string? NormalizeText(string? text) =>
        text is null ? null : string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// Builds a state's item: its name line (<c>name : Type [multiplicity]</c> for a typed usage), its
    /// <c>entry</c>/<c>do</c>/<c>exit</c> compartments, and the box size that fits them.
    /// </summary>
    private static StateItem BuildStateItem(string name, SysmlFeatureNode? declaration, Theme theme)
    {
        var nameLine = GeneralViewLayoutStrategy.FormatNameLine(
            null,
            name,
            typing: declaration?.FeatureTyping,
            multiplicity: declaration?.Multiplicity);
        var compartments = BuildActionCompartments(declaration);
        var (width, height) = ComputeStateSize(nameLine, compartments, theme);
        return new StateItem(name, width, height, nameLine, compartments);
    }

    /// <summary>
    /// Builds one compartment per <c>entry</c>, <c>do</c>, and <c>exit</c> sub-action kind the state
    /// declares, in that order, titled with the keyword (centered, italic) and listing each action's
    /// declaration text (the specification's textual form, with a leading <c>action</c> keyword
    /// dropped; the name when the declaration has no text). An empty form (<c>entry;</c>) adds no row.
    /// </summary>
    private static IReadOnlyList<LayoutCompartment> BuildActionCompartments(SysmlFeatureNode? state)
    {
        if (state is null)
        {
            return [];
        }

        var compartments = new List<LayoutCompartment>();
        foreach (var keyword in new[] { "entry", "do", "exit" })
        {
            var rows = state.Children
                .OfType<SysmlFeatureNode>()
                .Where(c => c.FeatureKeyword == keyword)
                .Select(c => NormalizeText(StripActionKeyword(c.ActionText)) ?? c.Name)
                .Where(row => !string.IsNullOrEmpty(row))
                .Select(row => row!)
                .ToList();
            if (rows.Count > 0)
            {
                compartments.Add(new LayoutCompartment(keyword, rows, TextAlign.Center, TextStyle.Italic));
            }
        }

        return compartments;
    }

    /// <summary>
    /// Computes the intrinsic size of a state box: wide enough for its name line, compartment titles
    /// and rows, and tall enough for the title area plus every compartment.
    /// </summary>
    private static (double Width, double Height) ComputeStateSize(
        string nameLine,
        IReadOnlyList<LayoutCompartment> compartments,
        Theme theme)
    {
        var width = Math.Max(MinStateWidth, (nameLine.Length * theme.FontSizeTitle * CharWidthFactor) + (4.0 * theme.LabelPadding));
        foreach (var compartment in compartments)
        {
            if (compartment.Title is { } title)
            {
                width = Math.Max(width, (title.Length * theme.FontSizeBody * CharWidthFactor) + (2.0 * theme.LabelPadding));
            }

            foreach (var row in compartment.Rows)
            {
                width = Math.Max(width, (row.Length * theme.FontSizeBody * CharWidthFactor) + (3.0 * theme.LabelPadding));
            }
        }

        var height = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true) + theme.LabelPadding;
        foreach (var compartment in compartments)
        {
            height += ComputeCompartmentHeight(compartment, theme);
        }

        return (width, height);
    }

    /// <summary>
    /// Computes the rendered height of a compartment, matching the renderer's layout: an optional
    /// title row followed by one row per entry and a closing gap. (Mirrors the General view's
    /// computation of the same renderer layout; it is repeated here so the two strategies stay
    /// independent units.)
    /// </summary>
    private static double ComputeCompartmentHeight(LayoutCompartment compartment, Theme theme)
    {
        var height = 0.0;
        if (compartment.Title is not null)
        {
            height += theme.LabelPadding + theme.FontSizeBody + theme.LabelPadding;
        }

        height += compartment.Rows.Count * (theme.LabelPadding + theme.FontSizeBody);
        height += theme.LabelPadding;
        return height;
    }

    /// <summary>Creates a rounded-rectangle state box at the given position.</summary>
    private static LayoutBox MakeStateBox(StateItem state, Rect rect) =>
        new(
            X: rect.X,
            Y: rect.Y,
            Width: rect.Width,
            Height: rect.Height,
            Label: state.NameLine,
            Depth: 1,
            Shape: BoxShape.RoundedRectangle,
            Compartments: state.Compartments,
            Children: [],
            Keyword: "state");

    /// <summary>Creates the final pseudo-state badge (a bullseye) centered in the given rectangle.</summary>
    private static LayoutBadge MakeFinalBadge(Rect rect) =>
        new(rect.X + (rect.Width / 2.0), rect.Y + (rect.Height / 2.0), FinalMarkerSize, BadgeShape.Bullseye, null);

    /// <summary>Adds the initial pseudo-state marker and its open-arrowhead arrow into the initial state.</summary>
    private static void AddInitialMarker(Rect first, List<LayoutNode> nodes)
    {
        // Place the marker above the first state, centered horizontally.
        var markerX = first.X + (first.Width / 2.0);
        var markerY = first.Y - InitialMarkerSize - InitialMarkerGap;

        nodes.Add(new LayoutBadge(markerX, markerY, InitialMarkerSize, BadgeShape.FilledCircle, null));

        // Straight solid arrow from the marker down to the top of the first state, ending in an
        // open (two-stroke) arrowhead — the specification's succession/transition head (graphical
        // BNF, p268) — rather than a filled one.
        nodes.Add(new LayoutLine(
            Waypoints: [new Point2D(markerX, markerY + (InitialMarkerSize / 2.0)), new Point2D(markerX, first.Y)],
            SourceEnd: EndMarkerStyle.None,
            TargetEnd: EndMarkerStyle.OpenChevron,
            LineStyle: LineStyle.Solid,
            MidpointLabel: null));
    }

    /// <summary>
    /// Adds transition edges (with guard labels) between state boxes, mapping each transition to the
    /// orthogonal polyline the layered pipeline routed for it, and returns the number of transitions
    /// whose polyline crosses a non-endpoint state box.
    /// </summary>
    /// <remarks>
    /// The layered algorithm returns exactly one routed polyline per input (non-self) transition, in
    /// input order and already oriented source-to-target. A lookup keyed by the <c>(source, target)</c>
    /// pair recovers each transition's polyline (self-transitions are drawn as a loop and carry no
    /// routed edge). Successive transitions sharing one routed corridor (parallel guards, or a
    /// forward/back-edge pair) are spread laterally so their anchor points and labels do not coincide.
    /// </remarks>
    private static int AddTransitions(
        IReadOnlyList<TransitionItem> transitions,
        IReadOnlyList<TransitionItem> flowTransitions,
        IReadOnlyList<IReadOnlyList<Point2D>> edgePolylines,
        Rect[] stateRects,
        double offsetX,
        double offsetY,
        List<LayoutNode> nodes)
    {
        // Build the routed (source, target) -> polyline lookup from the non-self transitions, which are
        // index-aligned with the algorithm's returned polylines.
        var routed = new Dictionary<(int Source, int Target), IReadOnlyList<Point2D>>();
        for (var k = 0; k < flowTransitions.Count; k++)
        {
            routed[(flowTransitions[k].Source, flowTransitions[k].Target)] = edgePolylines[k];
        }

        var corridorUse = new Dictionary<(int, int), int>();
        var crossings = 0;

        foreach (var transition in transitions)
        {
            var label = transition.Label;

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

    /// <summary>Returns the center point of a rectangle.</summary>
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
