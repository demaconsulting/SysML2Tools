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
/// (possibly cyclic) succession graph is made acyclic by the algorithm's cycle-breaking stage. A
/// filled-circle start marker enters the actions named by an explicit <c>first start then X;</c>
/// (the actions with no predecessor when none is declared) and a bullseye done marker is reached from
/// the actions named by an explicit <c>then done;</c> (the actions with no successor when none is
/// declared), with a reserved marker band at the top and bottom of the canvas. Fork and join nodes
/// are thick bars, decisions and merges are diamonds, a <c>terminate</c> node is a crossed circle,
/// and accept and send actions are boxes with the specification's corner icon. A succession leaving
/// a decision is labeled with its <c>[guard]</c> or <c>else</c>.
/// </remarks>
internal sealed class ActionFlowViewLayoutStrategy : ILayoutStrategy
{
    /// <summary>Minimum width of an action box.</summary>
    private const double MinActionWidth = 110.0;

    /// <summary>Approximate width-per-character factor relative to font size.</summary>
    private const double CharWidthFactor = 0.62;

    /// <summary>Diameter of the start, done, and terminate markers.</summary>
    private const double MarkerSize = 20.0;

    /// <summary>
    /// Intrinsic size (both width and height) of a decision/merge diamond badge, independent of the
    /// box-sizing heuristics used for ordinary actions and accept/send boxes.
    /// </summary>
    private const double ControlNodeBadgeSize = 24.0;

    /// <summary>Shortest length of a fork/join bar.</summary>
    private const double MinBarLength = 64.0;

    /// <summary>
    /// Bar length granted per connection on a fork/join bar. The layout engine spreads the
    /// connections on a face at least 16 px apart, so a bar sized to its fan-out keeps them on its
    /// long face rather than spilling onto the short sides.
    /// </summary>
    private const double BarLengthPerConnection = 24.0;

    /// <summary>
    /// Width and height granted per connection on a decision/merge diamond: the layout engine needs
    /// this much of a face for each connection it spreads (a diamond with two connections on a face
    /// is 40 px wide, with three 60 px).
    /// </summary>
    private const double DiamondLengthPerConnection = 20.0;

    /// <summary>
    /// Gap between a start/done marker and the adjacent action layer. Mirrors the layered algorithm's
    /// between-layer corridor width (70 px) so the control markers keep the same vertical rhythm as
    /// the action layers.
    /// </summary>
    private const double MarkerLayerGap = 70.0;

    /// <summary>
    /// How far below its source a labeled branch's horizontal run is placed: the 4 px corner radius of
    /// the connector's rounded bends plus 1 px, the shortest first run whose bend still starts on the
    /// connector's own vertical stub, which leaves the final drop as long as possible.
    /// </summary>
    private const double JogClearance = 5.0;
    /// <summary>The reserved name of the start pseudo-node (<c>first start;</c>).</summary>
    private const string StartName = "start";

    /// <summary>The reserved name of the done pseudo-node (<c>then done;</c>).</summary>
    private const string DoneName = "done";

    /// <summary>
    /// An action-flow node with its computed intrinsic size and kind. <paramref name="Kind"/> is one
    /// of <c>"action"</c> (ordinary action box, or a succession-endpoint-only fallback — the default),
    /// <c>"merge"</c>/<c>"decide"</c>/<c>"join"</c>/<c>"fork"</c>/<c>"terminate"</c> (rendered as a
    /// badge), or <c>"accept"</c>/<c>"send"</c> (rendered as a rounded-rectangle box with its corner
    /// icon). <paramref name="NameLine"/> is the displayed name (<c>name : Type [m]</c>) and
    /// <paramref name="PayloadRows"/> the accept/send payload shown in the box body.
    /// </summary>
    private sealed record ActionItem(
        string Name,
        double Width,
        double Height,
        string Kind = "action",
        string NameLine = "",
        IReadOnlyList<string>? PayloadRows = null);

    /// <summary>A resolved succession between two action indices with an optional <c>[guard]</c> or <c>else</c> label.</summary>
    private sealed record SuccessionItem(int From, int To, string? Label);

    /// <summary>
    /// The explicit start and done markers a model declares: the actions entered by
    /// <c>first start then X;</c> and the actions that declare <c>then done;</c>.
    /// </summary>
    private sealed record FlowMarkers(IReadOnlyList<int> StartTargets, IReadOnlyList<int> DoneSources);

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

        var (collected, index) = CollectActions(root, theme, scope);
        if (collected.Count == 0)
        {
            return new LayoutTree(200.0, 100.0, []);
        }

        var (edges, markers) = ResolveSuccessions(root, index);
        var actions = SizeBars(collected, edges);

        // Place action boxes with the layered algorithm flowing top-to-bottom (down). Each action
        // becomes a node and each succession a directed edge; the algorithm's cycle-breaking stage
        // makes the (possibly cyclic) flow graph acyclic, so it tolerates back edges. Self-loops are
        // already excluded by ResolveSuccessions (it keeps only from != to). MergeParallelEdges is
        // disabled so AddSuccessionEdges' 1:1 edgePolylines[e] indexing (see its own remarks) stays
        // valid: with merging enabled, a back edge sharing a node pair with a forward edge (a cycle)
        // collapses onto one shared line, leaving fewer routed lines than input successions. The open
        // chevron drawn at every succession's target is declared on the graph so the engine sizes each
        // final approach (a back edge's included) to the marker.
        var placed = LayeredPlacement.Place(
            actions.Select(a => (a.Width, a.Height)).ToList(),
            edges.Select(e => (e.From, e.To)).ToList(),
            LayoutFlowDirection.Down,
            mergeParallelEdges: false,
            edgeTargetEnd: EndMarkerStyle.OpenChevron);

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

        // Builds the action boxes, the succession lines and the start/done markers for a given
        // horizontal offset, in the canvas' absolute coordinate space.
        (List<LayoutNode> Nodes, int Crossings) Assemble(double shiftX)
        {
            var rects = new Rect[actions.Count];
            for (var i = 0; i < actions.Count; i++)
            {
                // Use the rectangle the engine placed. It is never smaller than the requested node, but it
                // clamps a node's height to a minimum, so a thin fork/join bar is placed in a taller slot;
                // the bar is drawn centered in the slot and its connectors are snapped to it afterward.
                rects[i] = new Rect(placed.Rects[i].X + shiftX, placed.Rects[i].Y + offsetY, placed.Rects[i].Width, placed.Rects[i].Height);
            }

            var built = new List<LayoutNode>();
            for (var i = 0; i < actions.Count; i++)
            {
                built.Add(MakeActionNode(actions[i], rects[i]));
            }

            var crossingCount = AddSuccessionEdges(actions, edges, placed.EdgePolylines, rects, shiftX, offsetY, built);
            AddStartAndDone(actions, rects, edges, markers, margin, built);
            return (built, crossingCount);
        }

        // A succession label is centered on its placed position and may overhang the action boxes on
        // either side. When one overhangs the left edge of the canvas margin, shift everything right so
        // the label keeps the margin (the renderers would otherwise clip it).
        var assembled = Assemble(offsetX);
        var leftOverhang = margin - LabelLeftExtent(assembled.Nodes, theme);
        if (leftOverhang > 0.0)
        {
            assembled = Assemble(offsetX + leftOverhang);
        }

        var (nodes, crossings) = assembled;

        // Size the canvas to the actual rendered content: action boxes plus routed succession
        // polylines (back edges can bulge beyond the box columns), the start/done markers, and the
        // succession labels as the renderers place them (obstacle-aware, clear of the action boxes).
        var (maxX, maxY) = ContentExtent(nodes);
        maxX = Math.Max(maxX, LabelRightExtent(nodes, theme));
        var warnings = LayoutWarnings.ForCrossings(context.ViewName, crossings);
        return new LayoutTree(maxX + margin, maxY + margin, nodes) { Warnings = warnings };
    }

    /// <summary>
    /// Finds the definition with the most successions to use as the diagram root. When
    /// <paramref name="scope"/> is non-null (the view's resolved <c>expose</c> containment-subtree
    /// scope), candidates are first restricted to those relevant to the scope via
    /// <see cref="ExposeScopeResolver.IsRootRelevantToScope"/>; because a nested definition and its
    /// ancestor can both be scope-relevant, ties among relevant candidates are then broken by
    /// specificity (deepest/longest qualified name wins) via
    /// <see cref="ExposeScopeResolver.IsMoreSpecificCandidate"/>, with the succession/action score
    /// used only to break ties between equally specific candidates. When no candidate is
    /// scope-relevant, no root is chosen (an empty canvas results). When <paramref name="scope"/> is
    /// <see langword="null"/>, selection is the plain succession/action-count heuristic, unchanged.
    /// </summary>
    private static SysmlDefinitionNode? FindRoot(SysmlWorkspace workspace, ExposedScope? scope)
    {
        SysmlDefinitionNode? best = null;
        string? bestQualifiedName = null;
        var bestScore = -1;

        foreach (var (qualifiedName, node) in workspace.Declarations)
        {
            // Members modeled for name resolution only are never drawn, nor chosen as a root.
            if (node.IsExcludedFromDiagrams)
            {
                continue;
            }

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

            var successions = def.DiagramChildren.OfType<SysmlTransitionNode>().Count();
            var actions = def.DiagramChildren.OfType<SysmlFeatureNode>().Count(f => IsActionFlowKeyword(f.FeatureKeyword));
            var score = (successions * 100) + actions;
            var scoreBetter = score > bestScore;
            var isBetter = scope is not null
                ? ExposeScopeResolver.IsMoreSpecificCandidate(qualifiedName, bestQualifiedName, scoreBetter)
                : scoreBetter;

            if (isBetter && (successions > 0 || actions > 0))
            {
                best = def;
                bestQualifiedName = qualifiedName;
                bestScore = score;
            }
        }

        return best;
    }

    /// <summary>
    /// Collects the action usages of the root definition and builds a name → index lookup. When
    /// <paramref name="scope"/> is non-null, an <c>action</c>-keyword feature whose own
    /// <c>QualifiedName</c> fails <see cref="ExposeScopeResolver.IsInSubjectScope"/> is skipped;
    /// actions synthesized only from succession endpoints have no independent qualified name and
    /// are always added, since they exist only because a succession endpoint that already named
    /// them exists on the (already scope-selected) root.
    /// </summary>
    private static (IReadOnlyList<ActionItem> Actions, Dictionary<string, int> Index) CollectActions(
        SysmlDefinitionNode root,
        Theme theme,
        ExposedScope? scope)
    {
        var actions = new List<ActionItem>();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);

        void Add(string name, string kind, SysmlFeatureNode? declaration)
        {
            // The reserved start/done names are pseudo-nodes drawn as markers, never as actions.
            if (index.ContainsKey(name) || name is StartName or DoneName)
            {
                return;
            }

            index[name] = actions.Count;
            actions.Add(BuildActionItem(name, kind, declaration, theme));
        }

        foreach (var feature in root.DiagramChildren.OfType<SysmlFeatureNode>())
        {
            if (!IsActionFlowKeyword(feature.FeatureKeyword) || feature.Name is null)
            {
                continue;
            }

            if (scope is not null && feature.QualifiedName is { Length: > 0 } fqn &&
                !ExposeScopeResolver.IsInSubjectScope(fqn, scope))
            {
                continue;
            }

            Add(feature.Name, feature.FeatureKeyword, feature);
        }

        foreach (var succession in root.DiagramChildren.OfType<SysmlTransitionNode>())
        {
            if (LastSegment(succession.Source) is { } s)
            {
                Add(s, "action", null);
            }

            if (LastSegment(succession.Target) is { } t)
            {
                Add(t, "action", null);
            }
        }

        return (actions, index);
    }

    /// <summary>
    /// Resolves succession endpoints to action indices via their last name segment, labeling each
    /// succession with its <c>[guard]</c> or <c>else</c>, and records the explicit start and done
    /// markers: a succession from the reserved name <c>start</c> names an action the start marker
    /// enters, and one to <c>done</c> names an action the done marker is reached from. Those
    /// successions are not edges of the layered graph.
    /// </summary>
    private static (IReadOnlyList<SuccessionItem> Edges, FlowMarkers Markers) ResolveSuccessions(
        SysmlDefinitionNode root,
        Dictionary<string, int> index)
    {
        var result = new List<SuccessionItem>();
        var startTargets = new List<int>();
        var doneSources = new List<int>();
        foreach (var succession in root.DiagramChildren.OfType<SysmlTransitionNode>())
        {
            var source = LastSegment(succession.Source);
            var target = LastSegment(succession.Target);
            if (source is null || target is null)
            {
                continue;
            }

            if (source == StartName && index.TryGetValue(target, out var startTarget))
            {
                startTargets.Add(startTarget);
            }
            else if (target == DoneName && index.TryGetValue(source, out var doneSource))
            {
                doneSources.Add(doneSource);
            }
            else if (index.TryGetValue(source, out var from) && index.TryGetValue(target, out var to) && from != to)
            {
                result.Add(new SuccessionItem(from, to, SuccessionLabel(succession)));
            }
        }

        return (result, new FlowMarkers(startTargets, doneSources));
    }

    /// <summary>
    /// The label of a succession: <c>[guard]</c> for a guarded one, <c>else</c> (the specification's
    /// terminal, without brackets) for the default branch of a decision, null otherwise.
    /// </summary>
    private static string? SuccessionLabel(SysmlTransitionNode succession)
    {
        if (succession.Guard is { Length: > 0 } guard)
        {
            return $"[{guard}]";
        }

        return succession.IsElse ? "else" : null;
    }

    /// <summary>Builds an action-flow node's item: its displayed name line, payload rows, and size.</summary>
    private static ActionItem BuildActionItem(string name, string kind, SysmlFeatureNode? declaration, Theme theme)
    {
        var nameLine = GeneralViewLayoutStrategy.FormatNameLine(
            null,
            name,
            typing: declaration?.FeatureTyping,
            multiplicity: declaration?.Multiplicity);
        IReadOnlyList<string> payloadRows = kind is "accept" or "send" && declaration?.ActionText is { Length: > 0 } text
            ? [string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))]
            : [];
        var (width, height) = ComputeActionSize(IsSynthetic(name) ? string.Empty : nameLine, kind, payloadRows, theme);
        return new ActionItem(name, width, height, kind, nameLine, payloadRows);
    }

    /// <summary>Returns whether <paramref name="name"/> is an <c>AstBuilder</c>-assigned name for an anonymous node.</summary>
    private static bool IsSynthetic(string name) => name.StartsWith('$');

    /// <summary>
    /// Gives every control node its final size from its fan-out. A fork/join becomes a thick bar long
    /// enough for its connections (see <see cref="BarLengthPerConnection"/>) and as thick as the
    /// notation's bar; a decision/merge becomes a square diamond wide enough for its connections
    /// (<see cref="DiamondLengthPerConnection"/> each). The layout engine spreads the connections on a
    /// face at least 16 px apart and widens a node that is too narrow for them, so sizing the node to
    /// its fan-out up front keeps the drawn shape and the slot the engine routes to the same size.
    /// </summary>
    private static IReadOnlyList<ActionItem> SizeBars(IReadOnlyList<ActionItem> actions, IReadOnlyList<SuccessionItem> edges)
    {
        var sized = new List<ActionItem>(actions.Count);
        for (var i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            var fan = Math.Max(edges.Count(e => e.To == i), edges.Count(e => e.From == i));
            if (action.Kind is "fork" or "join")
            {
                var length = Math.Max(MinBarLength, (fan + 1) * BarLengthPerConnection);
                action = action with { Width = length, Height = NotationMetrics.ThickBarThickness };
            }
            else if (action.Kind is "decide" or "merge")
            {
                var size = Math.Max(ControlNodeBadgeSize, fan * DiamondLengthPerConnection);
                action = action with { Width = size, Height = size };
            }

            sized.Add(action);
        }

        return sized;
    }

    /// <summary>Computes the intrinsic size of an action-flow node for the given kind.</summary>
    /// <remarks>
    /// Badge kinds use fixed sizes independent of the node's name: the diamond
    /// (<see cref="ControlNodeBadgeSize"/>) and the terminate circle (<see cref="MarkerSize"/>); a
    /// fork/join bar's length is set later by <see cref="SizeBars"/>. Ordinary actions and accept/send
    /// boxes are wide enough for their name line and payload and tall enough for the title area plus
    /// the payload row; an accept/send box also reserves the corner icon's width on both sides of
    /// its centered name so the icon never touches the text.
    /// </remarks>
    private static (double Width, double Height) ComputeActionSize(
        string nameLine,
        string kind,
        IReadOnlyList<string> payloadRows,
        Theme theme)
    {
        switch (kind)
        {
            case "merge":
            case "decide":
                return (ControlNodeBadgeSize, ControlNodeBadgeSize);
            case "terminate":
                return (MarkerSize, MarkerSize);
            case "fork":
            case "join":
                return (MinBarLength, NotationMetrics.ThickBarThickness);
        }

        var hasIcon = kind is "accept" or "send";
        var iconReserve = hasIcon ? 2.0 * (NotationMetrics.CornerIconWidth + NotationMetrics.CornerIconInset) : 0.0;
        var width = Math.Max(MinActionWidth, (nameLine.Length * theme.FontSizeTitle * CharWidthFactor) + (4.0 * theme.LabelPadding) + iconReserve);
        foreach (var row in payloadRows)
        {
            width = Math.Max(width, (row.Length * theme.FontSizeBody * CharWidthFactor) + (3.0 * theme.LabelPadding));
        }

        var hasKeyword = !hasIcon;
        var height = BoxMetrics.TitleAreaHeight(theme, hasLabel: nameLine.Length > 0, hasKeyword: hasKeyword) + theme.LabelPadding;
        if (payloadRows.Count > 0)
        {
            height += (payloadRows.Count * (theme.LabelPadding + theme.FontSizeBody)) + theme.LabelPadding;
        }

        if (hasIcon)
        {
            // The corner icon sits below the box's top inset; keep the box tall enough for it.
            height = Math.Max(height, (2.0 * NotationMetrics.CornerIconInset) + NotationMetrics.CornerIconHeight);
        }

        return (width, height);
    }

    /// <summary>
    /// Returns whether <paramref name="keyword"/> names an action-flow node kind recognized by this
    /// layout strategy: the ordinary <c>"action"</c> plus the control-node, accept, send, and
    /// terminate keywords.
    /// </summary>
    private static bool IsActionFlowKeyword(string keyword) =>
        keyword is "action" or "merge" or "decide" or "join" or "fork" or "accept" or "send" or "terminate";

    /// <summary>
    /// Creates the layout node for an action-flow item at the given placed rectangle: a
    /// <see cref="BadgeShape.Diamond"/> badge for decision/merge, a <see cref="BadgeShape.ThickHorizontalBar"/>
    /// badge (no label) for fork/join, a <see cref="BadgeShape.CrossedCircle"/> badge for terminate, a
    /// rounded-rectangle box with the send or accept corner icon (and its payload row) for send/accept
    /// nodes, or a rounded-rectangle box labeled <c>«action» name : Type [m]</c> for ordinary actions. A
    /// synthetic <c>$</c>-prefixed internal name (assigned by <c>AstBuilder</c> to an anonymous node)
    /// is never shown as a label.
    /// </summary>
    private static LayoutNode MakeActionNode(ActionItem action, Rect rect)
    {
        var centreX = rect.X + (rect.Width / 2.0);
        var centreY = rect.Y + (rect.Height / 2.0);
        var label = IsSynthetic(action.Name) ? null : action.Name;

        switch (action.Kind)
        {
            case "merge":
            case "decide":
                return new LayoutBadge(centreX, centreY, rect.Width, BadgeShape.Diamond, label);

            case "fork":
            case "join":
                return new LayoutBadge(centreX, centreY, rect.Width, BadgeShape.ThickHorizontalBar, null);

            case "terminate":
                return new LayoutBadge(centreX, centreY, rect.Width, BadgeShape.CrossedCircle, null);

            case "accept":
            case "send":
                return new LayoutBox(
                    X: rect.X,
                    Y: rect.Y,
                    Width: rect.Width,
                    Height: rect.Height,
                    Label: IsSynthetic(action.Name) ? string.Empty : action.NameLine,
                    Depth: 1,
                    Shape: BoxShape.RoundedRectangle,
                    Compartments: action.PayloadRows is { Count: > 0 } rows ? [new LayoutCompartment(null, rows)] : [],
                    Children: [],
                    Keyword: null,
                    Appearance: new BoxAppearance(CornerIcon: action.Kind == "accept" ? BoxCornerIcon.Accept : BoxCornerIcon.Send));

            default:
                return new LayoutBox(
                    X: rect.X,
                    Y: rect.Y,
                    Width: rect.Width,
                    Height: rect.Height,
                    Label: action.NameLine,
                    Depth: 1,
                    Shape: BoxShape.RoundedRectangle,
                    Compartments: [],
                    Children: [],
                    Keyword: "action");
        }
    }

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
        IReadOnlyList<ActionItem> actions,
        IReadOnlyList<SuccessionItem> edges,
        IReadOnlyList<IReadOnlyList<Point2D>> edgePolylines,
        Rect[] rects,
        double offsetX,
        double offsetY,
        List<LayoutNode> nodes)
    {
        var crossings = 0;
        var drawn = new List<(int Edge, string? Label, List<Point2D> Waypoints)>();
        for (var e = 0; e < edges.Count; e++)
        {
            var (from, to, label) = edges[e];
            var poly = edgePolylines[e];

            // The algorithm routes every edge, so poly normally has >= 2 points; fall back to a
            // straight segment between the (already-offset) box centers only if it does not.
            var waypoints = poly.Count >= 2
                ? poly.Select(p => new Point2D(p.X + offsetX, p.Y + offsetY)).ToList()
                : [Centre(rects[from]), Centre(rects[to])];

            StraightenThroughBar(waypoints, actions[from], rects[from], edges.Count(x => x.From == from), actions[to], rects[to], edges.Count(x => x.To == to));
            SnapEndsToBadges(waypoints, actions[from], rects[from], actions[to], rects[to]);
            drawn.Add((e, label, waypoints));
        }

        MoveLabeledJogsTowardsSource(edges, drawn);

        foreach (var (e, label, waypoints) in drawn)
        {
            if (CrossesNonEndpointBox(waypoints, rects, edges[e].From, edges[e].To))
            {
                crossings++;
            }

            nodes.Add(new LayoutLine(
                Waypoints: waypoints,
                SourceEnd: EndMarkerStyle.None,
                TargetEnd: EndMarkerStyle.OpenChevron,
                LineStyle: LineStyle.Dashed,
                MidpointLabel: label));
        }

        return crossings;
    }

    /// <summary>
    /// Moves the horizontal run of a labeled down-across-down (Z) branch up to just below its source, so
    /// the branch's final vertical drop, not the horizontal run, is its longest segment and carries the
    /// label. The label placer centers a label on the longest segment of its connector; on a decision's
    /// branch the engine puts the jog a corridor-width above the target, so the sideways run is the
    /// longest and a wide <c>[guard]</c> would sit on it, hiding the run and the connector's start at the
    /// diamond. All the Z branches that leave one source are moved by the same amount (the smallest first
    /// run less <see cref="JogClearance"/>), which keeps the spacing between sibling lanes, so two
    /// branches never share a horizontal run. The move is made only when the final drop then becomes
    /// longer than every labeled branch's horizontal run; otherwise the engine's route is kept.
    /// </summary>
    private static void MoveLabeledJogsTowardsSource(
        IReadOnlyList<SuccessionItem> edges,
        IReadOnlyList<(int Edge, string? Label, List<Point2D> Waypoints)> drawn)
    {
        foreach (var branches in drawn.Where(d => IsDownAcrossDown(d.Waypoints)).GroupBy(d => edges[d.Edge].From))
        {
            var group = branches.ToList();
            if (group.All(b => b.Label is null))
            {
                continue;
            }

            var delta = group.Min(b => b.Waypoints[1].Y - b.Waypoints[0].Y) - JogClearance;
            if (delta <= 0.5 || group.Where(b => b.Label is not null).Any(b =>
                    b.Waypoints[3].Y - (b.Waypoints[1].Y - delta) <= Math.Abs(b.Waypoints[2].X - b.Waypoints[1].X)))
            {
                continue;
            }

            foreach (var (_, _, waypoints) in group)
            {
                waypoints[1] = waypoints[1] with { Y = waypoints[1].Y - delta };
                waypoints[2] = waypoints[2] with { Y = waypoints[2].Y - delta };
            }
        }
    }

    /// <summary>Returns whether a polyline is a four-point connector that runs down, across, then down.</summary>
    private static bool IsDownAcrossDown(IReadOnlyList<Point2D> waypoints) =>
        waypoints.Count == 4 &&
        Math.Abs(waypoints[0].X - waypoints[1].X) < 1e-6 &&
        Math.Abs(waypoints[1].Y - waypoints[2].Y) < 1e-6 &&
        Math.Abs(waypoints[2].X - waypoints[3].X) < 1e-6 &&
        waypoints[0].Y < waypoints[1].Y &&
        waypoints[1].Y < waypoints[3].Y;

    /// <summary>
    /// Removes the small sideways jog from a connector whose bar end may sit anywhere along the bar. The
    /// engine centers a bar's only connection on the bar, so when the node at the other end is a few
    /// pixels off-center the connector kinks (down, across, down); a bar is as long as its fan-out
    /// needs, so the connection can instead attach where the other end lies and run straight. Applies
    /// only to a four-point down-across-down connector whose bar carries a single connection and whose
    /// other end lies within the bar's span (less a margin for the bar's ends).
    /// </summary>
    private static void StraightenThroughBar(
        List<Point2D> waypoints,
        ActionItem from,
        Rect fromRect,
        int fromConnections,
        ActionItem to,
        Rect toRect,
        int toConnections)
    {
        const double EndMargin = 6.0;
        if (waypoints.Count != 4)
        {
            return;
        }

        var (p0, p1, p2, p3) = (waypoints[0], waypoints[1], waypoints[2], waypoints[3]);
        var isZ = Math.Abs(p0.X - p1.X) < 1e-6 && Math.Abs(p1.Y - p2.Y) < 1e-6 && Math.Abs(p2.X - p3.X) < 1e-6;
        if (!isZ)
        {
            return;
        }

        if (from.Kind is "fork" or "join" && fromConnections == 1 &&
            p3.X >= fromRect.X + EndMargin && p3.X <= fromRect.X + fromRect.Width - EndMargin)
        {
            waypoints.Clear();
            waypoints.AddRange([new Point2D(p3.X, p0.Y), p3]);
        }
        else if (to.Kind is "fork" or "join" && toConnections == 1 &&
                 p0.X >= toRect.X + EndMargin && p0.X <= toRect.X + toRect.Width - EndMargin)
        {
            waypoints.Clear();
            waypoints.AddRange([p0, new Point2D(p0.X, p3.Y)]);
        }
    }

    /// <summary>
    /// Moves a succession's end points from the layout engine's slot faces onto the drawn outline of a
    /// badge that does not fill its slot. The engine attaches a connector to the face of the node's
    /// rectangle, but a fork/join bar is drawn only <see cref="NotationMetrics.ThickBarThickness"/>
    /// thick in the middle of a taller slot, and a diamond's outline falls away from its bounding box
    /// everywhere except the vertex; unsnapped, the connectors would start or stop in empty space. Only
    /// vertical end segments (a connector leaving the bottom face or entering the top face, the only
    /// shapes the top-to-bottom flow produces for these badges) are adjusted.
    /// </summary>
    private static void SnapEndsToBadges(List<Point2D> waypoints, ActionItem from, Rect fromRect, ActionItem to, Rect toRect)
    {
        if (waypoints.Count < 2)
        {
            return;
        }

        var first = waypoints[0];
        var firstIsVertical = Math.Abs(first.X - waypoints[1].X) < 1e-6;
        if (firstIsVertical && Math.Abs(first.Y - (fromRect.Y + fromRect.Height)) < 1e-6)
        {
            waypoints[0] = first with { Y = first.Y - BadgeInset(from, fromRect, first.X) };
        }

        var last = waypoints[^1];
        var lastIsVertical = Math.Abs(last.X - waypoints[^2].X) < 1e-6;
        if (lastIsVertical && Math.Abs(last.Y - toRect.Y) < 1e-6)
        {
            waypoints[^1] = last with { Y = last.Y + BadgeInset(to, toRect, last.X) };
        }
    }

    /// <summary>
    /// How far the drawn outline of a badge lies inside its slot's top or bottom face at the given X: half
    /// the slack of a thick bar's slot, or the distance a diamond's slanted edge falls inside its bounding
    /// box at that offset from its center. Zero for every other node.
    /// </summary>
    private static double BadgeInset(ActionItem action, Rect slot, double x) =>
        action.Kind switch
        {
            "fork" or "join" => Math.Max(0.0, (slot.Height - NotationMetrics.ThickBarThickness) / 2.0),
            "decide" or "merge" => Math.Abs(x - (slot.X + (slot.Width / 2.0))) * (slot.Height / slot.Width),
            _ => 0.0,
        };

    /// <summary>
    /// Adds the start marker (filled circle) entering the actions named by an explicit
    /// <c>first start then X;</c> — or, when the model declares none, the actions with no
    /// predecessor — and the done marker (bullseye) reached from the actions that declare
    /// <c>then done;</c> — or, when the model declares none, the actions with no successor — each
    /// joined by a dashed open-arrowhead succession line. A terminate node ends its flow itself, so
    /// it is never one of the inferred ends, and no done marker is inferred when every flow ends in a
    /// terminate node.
    /// </summary>
    private static void AddStartAndDone(
        IReadOnlyList<ActionItem> actions,
        Rect[] rects,
        IReadOnlyList<SuccessionItem> edges,
        FlowMarkers markers,
        double margin,
        List<LayoutNode> nodes)
    {
        var hasIncoming = new bool[actions.Count];
        var hasOutgoing = new bool[actions.Count];
        foreach (var (from, to, _) in edges)
        {
            hasOutgoing[from] = true;
            hasIncoming[to] = true;
        }

        var fallbackX = rects.Average(r => r.X + (r.Width / 2.0));

        // Centre the start marker over the action(s) it enters so the entry arrow stays vertical.
        var starts = markers.StartTargets.Count > 0
            ? [.. markers.StartTargets.Distinct()]
            : Enumerable.Range(0, actions.Count).Where(i => !hasIncoming[i]).ToList();
        var startX = starts.Count > 0
            ? starts.Average(i => rects[i].X + (rects[i].Width / 2.0))
            : fallbackX;
        var startY = margin + (MarkerSize / 2.0);
        nodes.Add(new LayoutBadge(startX, startY, MarkerSize, BadgeShape.FilledCircle, null));
        foreach (var i in starts)
        {
            nodes.Add(SuccessionLine(new Point2D(startX, startY + (MarkerSize / 2.0)),
                new Point2D(rects[i].X + (rects[i].Width / 2.0), rects[i].Y)));
        }

        // Centre the done marker under the action(s) that reach it.
        var ends = markers.DoneSources.Count > 0
            ? [.. markers.DoneSources.Distinct()]
            : Enumerable.Range(0, actions.Count).Where(i => !hasOutgoing[i] && actions[i].Kind != "terminate").ToList();
        if (ends.Count == 0 && actions.Any(a => a.Kind == "terminate"))
        {
            return;
        }

        var actionsBottom = rects.Max(r => r.Y + r.Height);
        var doneX = ends.Count > 0
            ? ends.Average(i => rects[i].X + (rects[i].Width / 2.0))
            : fallbackX;
        var doneY = actionsBottom + MarkerLayerGap + (MarkerSize / 2.0);
        nodes.Add(new LayoutBadge(doneX, doneY, MarkerSize, BadgeShape.Bullseye, null));
        foreach (var i in ends)
        {
            nodes.Add(SuccessionLine(new Point2D(rects[i].X + (rects[i].Width / 2.0), rects[i].Y + rects[i].Height),
                new Point2D(doneX, doneY - (MarkerSize / 2.0))));
        }
    }

    /// <summary>
    /// Builds a straight downward succession line from a start/done marker to an action (or back),
    /// drawn exactly like an action-to-action succession: dashed with an open arrowhead at the target.
    /// </summary>
    private static LayoutLine SuccessionLine(Point2D source, Point2D target) =>
        new(
            Waypoints: Math.Abs(source.X - target.X) < 1e-9
                ? [source, target]
                : [source, new Point2D(source.X, (source.Y + target.Y) / 2.0), new Point2D(target.X, (source.Y + target.Y) / 2.0), target],
            SourceEnd: EndMarkerStyle.None,
            TargetEnd: EndMarkerStyle.OpenChevron,
            LineStyle: LineStyle.Dashed,
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
                    // A thick bar is Size long and one bar-thickness thick; every other badge is a
                    // Size-wide circle or diamond.
                    var halfHeight = badge.Shape is BadgeShape.ThickHorizontalBar
                        ? (badge.Thickness ?? NotationMetrics.ThickBarThickness) / 2.0
                        : badge.Size / 2.0;
                    maxX = Math.Max(maxX, badge.CentreX + (badge.Size / 2.0));
                    maxY = Math.Max(maxY, badge.CentreY + halfHeight);
                    break;
                default:
                    break;
            }
        }

        return (maxX, maxY);
    }

    /// <summary>
    /// Returns the maximum X reached by any rendered succession label. Labels are placed with the
    /// same obstacle-aware <see cref="ConnectorLabelPlacer"/> overload the renderers use (the action
    /// boxes are the obstacles) and drawn centered on the chosen position, so a label's true right
    /// edge is its center X plus half its estimated text width.
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
    /// Returns the minimum X reached by any rendered succession label (its center X minus half its
    /// estimated text width), or <see cref="double.PositiveInfinity"/> when there are no labels. The
    /// counterpart of <see cref="LabelRightExtent"/> for the canvas' left side.
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
    /// Places every labeled succession line with the renderers' obstacle-aware placer and returns each
    /// label's placed center and half text width.
    /// </summary>
    private static List<(LabelPlacement Position, double HalfWidth)> PlaceLabels(IReadOnlyList<LayoutNode> nodes, Theme theme)
    {
        var result = new List<(LabelPlacement, double)>();
        var lines = nodes.OfType<LayoutLine>().Where(line => line.MidpointLabel is not null).ToList();
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
