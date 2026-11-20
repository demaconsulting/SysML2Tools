// <copyright file="InterconnectionViewLayoutStrategy.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.Layout;
using DemaConsulting.SysML2Tools.Filtering;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Rendering.Internal;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;

namespace DemaConsulting.SysML2Tools.Layout.Internal;

/// <summary>
/// Layout strategy for Interconnection View diagrams (SysML v2.0 clause 8.2.3.11-8.2.3.13).
/// </summary>
/// <remarks>
/// <para>
/// Shows the internal structure of a single part definition or part usage: its nested usages
/// (<c>part</c>, <c>item</c>, <c>action</c>, <c>state</c>, and <c>occurrence</c>) as rounded boxes placed by the
/// bundled layered algorithm, the declared ports as glyphs on the box boundaries, and the connection,
/// binding, interface, and flow usages routed as orthogonal connector polylines between them, all enclosed by
/// a frame for the host element headed by an italic <c>interconnection</c> compartment title.
/// </para>
/// <para>
/// Each nested part's declared ports (its own <c>port</c> usages, those of its type, and those inherited from
/// the type's supertypes) are modeled as named <c>LayoutGraphPort</c>s on the layered algorithm's input graph
/// (via <see cref="LayeredPlacement.PlaceWithPorts"/>), one per declared port however many connectors name it,
/// so the engine resolves port sides, spacing, and box-height growth itself. A port is drawn as the
/// specification's white rounded square with a direction arrow (<c>in</c>, <c>out</c>, <c>inout</c>), a dashed
/// outline for a reference port, and an outside label <c>name : Type</c> (<c>name : ~Type</c> for a conjugated
/// port). A declared port that no drawn connector names is not emitted by the engine, so this strategy places
/// it itself on a free slot of the part's left face (right face for an <c>out</c> port) after the layout.
/// A connector endpoint that names a segment that is not a declared port keeps that segment as a bare label.
/// An endpoint that names only a part attaches to the part itself, without a port glyph.
/// </para>
/// <para>
/// A connector is styled by its kind (SysML v2.0 clauses 8.2.3.13-8.2.3.16): <c>connection</c> is a plain
/// line, <c>binding</c> a plain line labeled <c>=</c>, <c>interface</c> a plain line labeled
/// <c>«interface»</c>, and <c>flow</c> a line with a filled arrowhead at the target end labeled
/// <c>«flow»</c> (followed by the name and the <c>of</c> payload when declared). <c>message</c> and
/// <c>allocation</c> usages are not interconnection connectors and are not drawn. A role name and
/// multiplicity declared on a connector end are drawn beside that end (role name above, multiplicity below).
/// </para>
/// <para>
/// When a nested part is itself a container (typed by a <c>part def</c> that has its own internal parts, or
/// declaring nested parts in an inline body), the strategy lays out that inner structure recursively
/// (bottom-up): the inner structure is laid out first with the same flat algorithm, the container part is then
/// treated as an atomic fixed-size node by the parent, and the inner content is nested as the container box's
/// <see cref="LayoutBox.Children"/>. A single-level model is a strict no-op for the recursion. This recursion is
/// unconditional only for a part's own branch when that branch was matched into the resolved <c>expose</c> scope
/// by a subject with no depth limit — i.e. a subject with unlimited-depth recursion (see
/// <see cref="ExposeScopeResolver.MatchesUnlimitedSubject"/>) — or when there is no scope at all. This decision
/// is made per branch, not once for the whole diagram: a scope combining a recursive subject (e.g.
/// <c>expose X::**;</c>) with a non-recursive subject (e.g. <c>expose Y;</c>) recurses fully into <c>X</c>'s
/// branch while <c>Y</c>'s branch stops expanding after its own direct children (depth 0), rendering any deeper
/// container in <c>Y</c>'s branch as an intrinsic-sized leaf box instead of recursing further.
/// </para>
/// <para>
/// Not every resolved <c>expose</c> scope names a single element worth treating as "the" subject: per SysML v2
/// §8.3.26.11/§9.2.20.2.6, an InterconnectionView's exposed content can be one or more concrete feature usages
/// directly, with no enclosing definition of its own. When <see cref="FindRoots"/> selects no root but the scope
/// directly includes one or more top-level <c>part</c> feature usages, those features are rendered as boxless
/// nodes side by side (via <see cref="CollectTopLevelScopedParts"/>), each recursing into its own interior
/// exactly as a normal nested container part would, reusing <see cref="BuildPartItem"/> so the container-versus-
/// leaf logic is never duplicated. Because these boxless top-level parts may be declared inside different
/// containing definitions, any connection between them is resolved against only its own declaring definition's
/// own children (see <see cref="ResolveTopLevelConnections"/>'s remarks) — never a single flat cross-workspace
/// name index, which would silently collide when two different definitions each declare a same-simple-named part.
/// </para>
/// </remarks>
internal sealed class InterconnectionViewLayoutStrategy : ILayoutStrategy
{
    /// <summary>Minimum width of a nested part box.</summary>
    private const double MinPartWidth = 110.0;

    /// <summary>Approximate width-per-character factor relative to the title font size.</summary>
    private const double CharWidthFactor = 0.62;

    /// <summary>
    /// Uniform padding the layered algorithm adds around placed content (mirrors its internal
    /// content padding), used to give routed connectors the same trailing inset as the boxes.
    /// </summary>
    private const double LayeredContentPadding = 20.0;

    /// <summary>
    /// Height of one port label as a multiple of the body font size — the same factor the layout engine
    /// uses to space the ports of a face, so a port this strategy places is spaced like an engine port.
    /// </summary>
    private const double PortLabelHeightFactor = 1.3;

    /// <summary>Vertical step, in logical pixels, between the candidate slots tried for an unconnected port.</summary>
    private const double PortSlotStep = 3.0;

    /// <summary>Clearance, in logical pixels, kept around a port glyph and its label against other content.</summary>
    private const double PortClearance = 1.0;

    /// <summary>Title of the compartment that labels the frame of the interconnection (SysML v2.0 clause 8.2.3.11).</summary>
    private const string InterconnectionTitle = "interconnection";

    /// <summary>The usage keywords that are drawn as nodes of an interconnection view.</summary>
    private static readonly HashSet<string> NodeKeywords = new(StringComparer.Ordinal)
    {
        "part", "item", "action", "state", "occurrence",
    };

    /// <summary>The connection keywords that are drawn as connectors of an interconnection view.</summary>
    private static readonly HashSet<string> ConnectorKeywords = new(StringComparer.Ordinal)
    {
        "connection", "binding", "interface", "flow",
    };

    /// <summary>
    /// The appearance of a port that a connector endpoint names but no declaration provides: the
    /// specification's outlined square without a direction arrow, labeled outside the part.
    /// </summary>
    private static readonly PortAppearance UndeclaredPortAppearance =
        new(PortDirection.None, PortGlyph.Outlined, LineStyle.Solid, PortLabelPlacement.Outward);

    /// <summary>A declared port of a part, ready to be drawn.</summary>
    /// <param name="Name">The port's declared name; the identity connector endpoints are resolved against.</param>
    /// <param name="Label">The outside label: <c>name : Type</c>, <c>name : ~Type</c>, or the bare name when untyped.</param>
    /// <param name="Appearance">Glyph, direction arrow, outline style, and label placement.</param>
    private sealed record PortInfo(string Name, string Label, PortAppearance Appearance);

    /// <summary>
    /// A nested usage with its computed intrinsic box size. When the part is a container (it has its own
    /// internal parts), <see cref="InnerContent"/> holds the pre-laid-out interior content positioned
    /// relative to the part box's own top-left <c>(0, 0)</c>; for a leaf part it is <see langword="null"/>.
    /// <see cref="Name"/> is the identity used to resolve connector endpoints; <see cref="Label"/> and
    /// <see cref="Keyword"/> are display-only.
    /// </summary>
    /// <param name="Name">The usage name, or its type/keyword when anonymous; the connector endpoint identity.</param>
    /// <param name="Keyword">The keyword line text (<c>part</c>, <c>ref part</c>, <c>item</c>, ...), without guillemet marks.</param>
    /// <param name="Label">The name line (<c>name : Type [multiplicity]</c>).</param>
    /// <param name="Width">Box width.</param>
    /// <param name="Height">Box height (includes the room reserved for every declared port).</param>
    /// <param name="InnerContent">The pre-laid-out interior of a container part, or null for a leaf.</param>
    /// <param name="IsReference">Whether the usage is a reference usage, drawn with a dashed outline.</param>
    /// <param name="Compartments">The <c>doc</c> and <c>attributes</c> compartments of a leaf part.</param>
    /// <param name="Ports">The declared ports of the usage (own, typed, and inherited).</param>
    private sealed record PartItem(
        string Name,
        string Keyword,
        string Label,
        double Width,
        double Height,
        IReadOnlyList<LayoutNode>? InnerContent,
        bool IsReference,
        IReadOnlyList<LayoutCompartment> Compartments,
        IReadOnlyList<PortInfo> Ports);

    /// <summary>
    /// One top-level scoped feature collected by <see cref="CollectTopLevelScopedParts"/>, pairing
    /// its laid-out <see cref="Part"/> with the qualified name of the definition that directly owns
    /// it (its containing <c>part def</c>), so <see cref="ResolveTopLevelConnections"/> can group
    /// top-level parts by owner and resolve each definition's own connections against only its own
    /// children — never a flat cross-workspace name index, which would silently collide when two
    /// different definitions each own a same-simple-named part (see <see cref="OwnerQualifiedName"/>).
    /// </summary>
    /// <param name="Part">The collected part's laid-out box data.</param>
    /// <param name="OwnerQualifiedName">
    /// The qualified name of the <c>part def</c> that directly declares this feature — the feature's
    /// own qualified name with its last <c>"::"</c>-separated segment removed — or
    /// <see langword="null"/> when the feature's qualified name has no <c>"::"</c> separator at all
    /// (declared at the absolute root namespace with no containing definition; such a feature cannot
    /// be the endpoint of any connection resolved by <see cref="ResolveTopLevelConnections"/>, since
    /// connections are only ever children of an actual <see cref="SysmlDefinitionNode"/>, matching
    /// pre-existing behavior for this shape).
    /// </param>
    private sealed record TopLevelPart(PartItem Part, string? OwnerQualifiedName);

    /// <summary>How a connector is drawn beyond its route: end decoration, midpoint label, and end labels.</summary>
    /// <param name="TargetEnd">The end decoration at the target end.</param>
    /// <param name="Label">The midpoint label, or null.</param>
    /// <param name="SourceEndLabel">Role name and multiplicity beside the source end, or null.</param>
    /// <param name="TargetEndLabel">Role name and multiplicity beside the target end, or null.</param>
    private sealed record ConnectorStyle(
        EndMarkerStyle TargetEnd,
        string? Label,
        LineEndLabel? SourceEndLabel,
        LineEndLabel? TargetEndLabel);

    /// <summary>
    /// A resolved binary connector between two nested-part indices, together with the port each end
    /// attaches through (null when the endpoint names the part directly) and how the connector is drawn.
    /// </summary>
    /// <param name="A">Index of the first (source) part.</param>
    /// <param name="B">Index of the second (target) part.</param>
    /// <param name="PortA">The port request at the source end, or null.</param>
    /// <param name="PortB">The port request at the target end, or null.</param>
    /// <param name="Style">How the connector is drawn.</param>
    private sealed record ConnItem(int A, int B, EdgePortRef? PortA, EdgePortRef? PortB, ConnectorStyle Style);

    /// <summary>The laid-out interior of one definition: its full container size and content.</summary>
    /// <param name="Width">Full container width including title area and insets.</param>
    /// <param name="Height">Full container height including title area and insets.</param>
    /// <param name="Content">Part boxes, ports, and connector lines positioned with origin <c>(0, 0)</c>.</param>
    private sealed record InteriorLayout(double Width, double Height, IReadOnlyList<LayoutNode> Content);

    /// <summary>
    /// A laid-out root frame together with how far its outside port labels reach beyond its left and right
    /// edges, so the caller can make room on the canvas.
    /// </summary>
    /// <param name="Box">The frame box at origin <c>(0, 0)</c>.</param>
    /// <param name="LeftOverhang">Distance the frame's ports and labels extend left of the frame.</param>
    /// <param name="RightOverhang">Distance the frame's ports and labels extend right of the frame.</param>
    private sealed record RootFrame(LayoutBox Box, double LeftOverhang, double RightOverhang);

    /// <summary>An axis-aligned rectangle used to test glyphs, labels, boxes, and connector segments for overlap.</summary>
    /// <param name="Left">Left edge.</param>
    /// <param name="Top">Top edge.</param>
    /// <param name="Right">Right edge.</param>
    /// <param name="Bottom">Bottom edge.</param>
    private readonly record struct Bounds(double Left, double Top, double Right, double Bottom)
    {
        /// <summary>Creates the bounds of a rectangle given by its center and half extents.</summary>
        public static Bounds Around(double x, double y, double halfWidth, double halfHeight) =>
            new(x - halfWidth, y - halfHeight, x + halfWidth, y + halfHeight);

        /// <summary>Creates the bounds of a placed label.</summary>
        public static Bounds Of(LabelPlacement placement) =>
            Around(placement.X, placement.Y, placement.HalfWidth, placement.HalfHeight);

        /// <summary>Creates the bounds of a part rectangle.</summary>
        public static Bounds Of(Rect rect) => new(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height);

        /// <summary>Returns whether this rectangle, grown by <paramref name="pad"/>, overlaps <paramref name="other"/>.</summary>
        public bool Intersects(Bounds other, double pad) =>
            Left - pad < other.Right && Right + pad > other.Left && Top - pad < other.Bottom && Bottom + pad > other.Top;
    }

    /// <inheritdoc/>
    public LayoutTree BuildLayout(ViewContext context, RenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        // Index of candidate container definitions (non-stdlib part defs with at least one part child).
        // Built before FindRoots so root-selection can resolve which candidates are themselves used as
        // another candidate's nested part type (see FindRoots' composition-graph-root preference).
        var defsByName = BuildDefinitionIndex(context.Workspace);
        return BuildTree(context, options, defsByName);
    }

    /// <summary>Builds the interconnection layout tree for <see cref="BuildLayout"/>.</summary>
    private static LayoutTree BuildTree(ViewContext context, RenderOptions options, DefinitionIndex defsByName)
    {
        var theme = options.Theme;

        var scope = ExposeScopeResolver.ResolveExposedScope(context.Workspace, context.ViewNode);
        // Choose the element(s) whose internals to show.
        var roots = FindRoots(context.Workspace, scope, defsByName);
        if (roots.Count > 1)
        {
            return LayOutRootsSideBySide(roots, theme, defsByName, scope);
        }

        var root = roots.Count == 1 ? roots[0] : null;
        if (root is null)
        {
            // No single element qualifies as "the" root. Per SysML v2 §8.3.26.11/§9.2.20.2.6, an
            // InterconnectionView's subject need not be one definition — when the resolved expose
            // scope directly names one or more concrete feature usages, render those as boxless nodes
            // side by side (via the same sensible existing layout helper,
            // LayeredPlacement.PlaceWithPorts, used for every other case) instead of falling back to
            // a totally empty canvas. When there is nothing concrete to draw either (no scope at
            // all, or a scope that matches no usage), the existing empty-canvas fallback is
            // preserved unchanged.
            if (scope is not null)
            {
                var topLevelEntries = CollectTopLevelScopedParts(context.Workspace, scope, theme, defsByName);
                if (topLevelEntries.Count > 0)
                {
                    var topLevelParts = topLevelEntries.Select(e => e.Part).ToList();
                    var pairs = ResolveTopLevelConnections(defsByName, topLevelEntries, scope);
                    var layout = LayOutInteriorWithConnections(
                        topLevelParts, pairs, theme, boxDepth: 0, reserveTitleArea: false);
                    return new LayoutTree(layout.Width, layout.Height, layout.Content);
                }
            }

            return new LayoutTree(200.0, 100.0, []);
        }

        // No nested usages means there is nothing to draw.
        if (!root.DiagramChildren.OfType<SysmlFeatureNode>().Any(IsNodeUsage))
        {
            return new LayoutTree(200.0, 100.0, []);
        }

        var frame = BuildRootBox(root, theme, defsByName, scope);
        var nodes = TranslateNodes([frame.Box], frame.LeftOverhang, 0.0);
        return new LayoutTree(frame.LeftOverhang + frame.Box.Width + frame.RightOverhang, frame.Box.Height, nodes);
    }

    /// <summary>
    /// Lays out several independently exposed root elements side by side, left to right in
    /// declaration order, separated by a fixed gap, with their tops aligned. SysML v2 §8.2.3.11
    /// defines <c>interconnection-view</c> as a repeated list of interconnection elements, so a view
    /// exposing several disjoint composition trees draws every root, not just one. The labels of a
    /// frame's own ports reach outside its border, so the gap grows by those overhangs.
    /// </summary>
    private static LayoutTree LayOutRootsSideBySide(
        IReadOnlyList<SysmlNode> roots,
        Theme theme,
        DefinitionIndex defsByName,
        ExposedScope? scope)
    {
        var gap = theme.LabelPadding * 6.0;
        var nodes = new List<LayoutNode>();
        var x = 0.0;
        var height = 0.0;
        foreach (var root in roots)
        {
            var frame = BuildRootBox(root, theme, defsByName, scope);
            x += frame.LeftOverhang;
            nodes.AddRange(TranslateNodes([frame.Box], x, 0.0));
            x += frame.Box.Width + frame.RightOverhang + gap;
            height = Math.Max(height, frame.Box.Height);
        }

        return new LayoutTree(x - gap, height, nodes);
    }

    /// <summary>
    /// Lays out one root element's interior (recursing into container parts) and wraps it in the
    /// root's frame at origin <c>(0, 0)</c>, the same origin the interior content is positioned
    /// relative to, so the content nests directly as the frame's children. A part definition is a
    /// square-cornered frame headed <c>«part def» Name</c>; a part usage is a rounded frame headed
    /// <c>«part» name : Type</c> (dashed for a reference usage). The frame's own declared ports are drawn on
    /// its left and right faces; connectors to them are not routed (see the design documentation).
    /// </summary>
    private static RootFrame BuildRootBox(
        SysmlNode root,
        Theme theme,
        DefinitionIndex defsByName,
        ExposedScope? scope)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        if (root.QualifiedName is { Length: > 0 })
        {
            visited.Add(root.QualifiedName);
        }

        var interior = LayOutInterior(root, theme, depth: 0, defsByName, visited, scope, ancestorUnlimitedRecursion: null);

        // The frame's name compartment, shape, and outline follow the kind of element it frames.
        string keyword;
        string label;
        var shape = BoxShape.Rectangle;
        BoxAppearance? appearance = null;
        if (root is SysmlFeatureNode usage)
        {
            keyword = GeneralViewLayoutStrategy.UsageKeyword(usage);
            label = GeneralViewLayoutStrategy.FormatUsageNameLine(usage, usage.Name ?? "Interconnection");
            shape = BoxShape.RoundedRectangle;
            appearance = usage.IsReference ? new BoxAppearance(LineStyle.Dashed) : null;
        }
        else
        {
            var definition = (SysmlDefinitionNode)root;
            keyword = string.IsNullOrEmpty(definition.DefinitionKeyword) ? "part def" : GeneralViewLayoutStrategy.DefinitionKeyword(definition);
            label = GeneralViewLayoutStrategy.FormatNameLine(definition.ShortName, root.Name ?? "Interconnection", definition.SupertypeNames);
            appearance = definition.IsAbstract ? new BoxAppearance(NameStyle: GeneralViewLayoutStrategy.AbstractDefinitionNameStyle) : null;
        }

        // The frame is never narrower than its own name compartment.
        var width = Math.Max(interior.Width, ComputePartSize(label, keyword, theme).Width);

        // Draw the frame's declared ports on its left and right faces, below the title and compartment
        // title, and grow the frame so every port has room.
        var ports = ResolvePorts(root, defsByName);
        var bandBottom = FrameHeaderHeight(theme);
        var framePorts = PlaceFramePorts(ports, width, bandBottom, theme);
        var height = Math.Max(interior.Height, ports.Count == 0 ? 0.0 : bandBottom + (PortsOnBusiestFace(ports) + 0.5) * PortPitch(theme));
        var (leftOverhang, rightOverhang) = MeasurePortOverhang(framePorts, width, theme);

        var box = new LayoutBox(
            X: 0,
            Y: 0,
            Width: width,
            Height: height,
            Label: label,
            Depth: 0,
            Shape: shape,
            Compartments: [MakeInterconnectionTitle()],
            Children: [.. interior.Content, .. framePorts],
            Keyword: keyword,
            Appearance: appearance);
        return new RootFrame(box, leftOverhang, rightOverhang);
    }

    /// <summary>Creates the italic, centered <c>interconnection</c> compartment title of a root frame.</summary>
    private static LayoutCompartment MakeInterconnectionTitle() =>
        GeneralViewLayoutStrategy.MakeCompartment(InterconnectionTitle, []);

    /// <summary>
    /// Returns the height of a root frame's header: the keyword and name title area plus the
    /// <c>interconnection</c> compartment title band the renderer draws below it.
    /// </summary>
    private static double FrameHeaderHeight(Theme theme) =>
        BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true)
        + GeneralViewLayoutStrategy.ComputeCompartmentHeight(MakeInterconnectionTitle(), theme);

    /// <summary>
    /// Lays out the interior of one definition or usage: collects its nested usages (recursing into
    /// container parts), places them with the "auto" layout algorithm — which classifies parts by
    /// connectivity and packs disconnected/singleton parts via the containment algorithm while routing
    /// connected groups through the bundled layered algorithm — and emits one rounded box per part plus
    /// the ports and a connector line per connection, all positioned relative to the container's own
    /// top-left origin <c>(0, 0)</c>.
    /// </summary>
    /// <param name="def">The definition or usage whose interior to lay out.</param>
    /// <param name="theme">The active rendering theme.</param>
    /// <param name="depth">Nesting depth of this element's container box (0 for the root).</param>
    /// <param name="defsByName">Container-definition index and type resolver.</param>
    /// <param name="visited">Qualified names already on the recursion path, guarding against cycles.</param>
    /// <param name="scope">
    /// The view's resolved <c>expose</c> containment-subtree scope, or null when no scoping applies.
    /// </param>
    /// <param name="ancestorUnlimitedRecursion">
    /// The already-decided per-branch unlimited-recursion flag inherited from the ancestor call that
    /// recursed into this element, or <see langword="null"/> only for the true top-of-recursion
    /// call from <see cref="BuildLayout"/>'s root path (<paramref name="depth"/> is always 0 in that
    /// case). When non-null, it is propagated unchanged — the decision is made once, per branch, at
    /// depth 0 (see <see cref="CollectParts"/>), and every deeper recursive call simply inherits it;
    /// see <see cref="BuildPartItem"/>'s remarks for how this actually gates recursion.
    /// </param>
    /// <returns>The laid-out interior size and content.</returns>
    private static InteriorLayout LayOutInterior(
        SysmlNode def,
        Theme theme,
        int depth,
        DefinitionIndex defsByName,
        ISet<string> visited,
        ExposedScope? scope,
        bool? ancestorUnlimitedRecursion)
    {
        var parts = CollectParts(def, theme, depth, defsByName, visited, scope, ancestorUnlimitedRecursion);
        var partIndex = BuildPartIndex(parts);
        var pairs = ResolveConnections(def, partIndex, parts);

        // Only the root frame carries the italic "interconnection" compartment title, so only its
        // content is pushed down by that title band.
        var extraTopBand = depth == 0
            ? GeneralViewLayoutStrategy.ComputeCompartmentHeight(MakeInterconnectionTitle(), theme)
            : 0.0;
        return LayOutInteriorWithConnections(parts, pairs, theme, boxDepth: depth + 1, extraTopBand: extraTopBand);
    }

    /// <summary>
    /// Lays out a container's parts and connectors, delegating placement and orthogonal edge routing to
    /// the bundled layered algorithm, then places the declared ports no connector names.
    /// </summary>
    /// <param name="parts">The parts to place.</param>
    /// <param name="pairs">The resolved connections between the parts.</param>
    /// <param name="theme">The active rendering theme.</param>
    /// <param name="boxDepth">
    /// The <see cref="LayoutBox.Depth"/> to stamp on each placed part's own box (not the
    /// container's — the caller passes its own container depth, plus one, for a normal nested
    /// interior; the no-single-root fallback in <see cref="BuildLayout"/> passes <c>0</c> directly
    /// since there is no enclosing container box at all in that path).
    /// </param>
    /// <param name="reserveTitleArea">
    /// Whether to reserve a title band above the placed content, as a normal container box's own
    /// title requires. <see langword="true"/> (the default) for every existing container-interior
    /// caller; <see langword="false"/> only for the no-single-root boxless fallback in
    /// <see cref="BuildLayout"/>, where there is no enclosing frame/title to make room for — the
    /// returned size is then just the bounding box of the placed content plus normal padding.
    /// </param>
    /// <param name="extraTopBand">
    /// Additional height reserved below the title area for compartment titles of the container (the
    /// root frame's <c>interconnection</c> title); zero for every other container.
    /// </param>
    private static InteriorLayout LayOutInteriorWithConnections(
        IReadOnlyList<PartItem> parts,
        IReadOnlyList<ConnItem> pairs,
        Theme theme,
        int boxDepth,
        bool reserveTitleArea = true,
        double extraTopBand = 0.0)
    {
        var nodeSizes = parts.Select(p => (p.Width, p.Height, HasLabel: true, HasKeyword: true)).ToList();

        // Every connector carries its kind's decoration, label, and end labels onto the graph edge so the
        // layout reserves room for them; a port with a key is shared by every connector that names it.
        var portEdges = pairs
            .Select(p => new PortEdge(
                p.A,
                p.B,
                p.PortA,
                p.PortB,
                p.Style.TargetEnd,
                LineStyle.Solid,
                p.Style.SourceEndLabel,
                p.Style.TargetEndLabel,
                p.Style.Label))
            .ToList();

        // Delegate all placement and routing to the layered algorithm. Ports are modeled as named
        // LayoutGraphPorts on each connection's endpoints, so the engine resolves port sides,
        // spacing, and any resulting box-height growth needed to keep connectors visually distinct
        // regardless of connection count. Every part is flagged HasLabel/HasKeyword (matching the
        // hasLabel/hasKeyword: true convention already used for BoxMetrics.TitleAreaHeight below and
        // in ComputePartSize), which activates the engine's automatic title-vs-side-port reservation
        // so ports never land within the box's own title band. Parallel-edge merging is disabled so
        // distinct SysML connections between the same two parts (e.g. a "power" and an "encoder"
        // connection both wired between the same two nested parts) are each preserved as their own
        // independently-routed connector instead of collapsing onto one shared route.
        var placed = LayeredPlacement.PlaceWithPorts(nodeSizes, portEdges, LayoutFlowDirection.Right);

        // The engine reserves room between columns for port and end labels but not for a connector's
        // midpoint label (binding "=", «interface», «flow» payload), so open any corridor where one
        // would land on a box or a port label.
        placed = LayeredPlacement.WidenForMidpointLabels(placed, portEdges, theme);
        // Shift placed content down/right to sit inside the container box. When there is no
        // enclosing container (the boxless top-level fallback), no title band is reserved, so the
        // top offset collapses to the same padding-only inset used on every other side.
        var titleArea = reserveTitleArea
            ? BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true) + extraTopBand
            : 0.0;
        var offsetX = theme.LabelPadding * 2.0;
        var offsetY = titleArea + (theme.LabelPadding * 2.0);

        var containerWidth = placed.Width + (offsetX * 2.0);
        var containerHeight = placed.Height + offsetY + (theme.LabelPadding * 2.0);

        // The layered algorithm derives its size from box extents only, but a connector can route
        // beyond the boxes (e.g. wrapping below them). Extend the container so every waypoint is
        // enclosed with the same trailing inset the boxes already receive, so no connector scrapes the
        // container edge.
        var trailingInset = LayeredContentPadding + (theme.LabelPadding * 2.0);
        foreach (var wp in placed.EdgePolylines)
        {
            foreach (var p in wp)
            {
                containerWidth = Math.Max(containerWidth, p.X + offsetX + trailingInset);
                containerHeight = Math.Max(containerHeight, p.Y + offsetY + trailingInset);
            }
        }

        var content = new List<LayoutNode>();

        // One rounded box per nested usage; container parts carry their nested children.
        var rects = new List<Rect>(parts.Count);
        for (var i = 0; i < parts.Count; i++)
        {
            var r = placed.Rects[i];
            var rect = new Rect(r.X + offsetX, r.Y + offsetY, r.Width, r.Height);
            rects.Add(rect);
            content.Add(MakePartBox(parts[i], rect, boxDepth));
        }

        // One connector line per connection, exactly as the engine routed and decorated it (end
        // marker, midpoint label, end labels). The algorithm returns one line per input connection, in
        // input order and oriented source -> target, so connection k uses Lines[k] directly.
        foreach (var line in placed.Lines)
        {
            if (line.Waypoints.Count < 2)
            {
                continue;
            }

            content.Add(line with
            {
                Waypoints = line.Waypoints.Select(p => new Point2D(p.X + offsetX, p.Y + offsetY)).ToList(),
            });
        }

        // The engine-placed ports (labeled once per declared port), translated by the same container
        // offset as the boxes and waypoints.
        foreach (var port in placed.Ports)
        {
            content.Add(port with { CentreX = port.CentreX + offsetX, CentreY = port.CentreY + offsetY });
        }

        // A declared port that no drawn connector names is not emitted by the engine; place it on a free
        // slot of its part's face now that every box, port, and line has its final position.
        var connected = new HashSet<(int Part, string Name)>();
        foreach (var pair in pairs)
        {
            AddConnectedPort(connected, pair.A, pair.PortA);
            AddConnectedPort(connected, pair.B, pair.PortB);
        }

        var unconnected = PlaceUnconnectedPorts(parts, rects, connected, content, theme);
        content.AddRange(unconnected);

        // Outside labels of the ports placed above can reach beyond the content the layout measured, so
        // shift and widen to keep every glyph and label a margin inside the container.
        var (minX, maxX) = MeasurePortExtent(unconnected, theme);
        var shift = Math.Max(0.0, theme.LabelPadding - minX);
        if (shift > 0.0)
        {
            var shifted = TranslateNodes(content, shift, 0.0);
            content.Clear();
            content.AddRange(shifted);
            containerWidth += shift;
            maxX += shift;
        }

        containerWidth = Math.Max(containerWidth, maxX + theme.LabelPadding);

        return new InteriorLayout(containerWidth, containerHeight, content);
    }

    /// <summary>
    /// Records the declared port a connector end named, so the port is not also placed as an
    /// unconnected one. Only keyed requests that came from a declaration (<c>p:</c> prefix) count.
    /// </summary>
    private static void AddConnectedPort(HashSet<(int Part, string Name)> connected, int part, EdgePortRef? port)
    {
        if (port?.Key is { } key && key.StartsWith(DeclaredKeyPrefix, StringComparison.Ordinal))
        {
            connected.Add((part, key[DeclaredKeyPrefix.Length..]));
        }
    }

    /// <summary>The prefix of the shared-port key of a declared port (followed by the port name).</summary>
    private const string DeclaredKeyPrefix = "p:";

    /// <summary>The prefix of the shared-port key of an endpoint segment that is not a declared port.</summary>
    private const string UndeclaredKeyPrefix = "u:";

    /// <summary>
    /// Returns the distance between the centers of two ports on one face: room for the glyph and for the
    /// outside label of the next port (which sits above its connector line), the same spacing the layout
    /// engine keeps between the ports it places.
    /// </summary>
    private static double PortPitch(Theme theme) =>
        (theme.FontSizeBody * PortLabelHeightFactor) + NotationMetrics.PortLabelConnectorGap(theme) + theme.LabelPadding;

    /// <summary>
    /// Returns the height a part must have so every declared port has a slot on a face below the title
    /// area, whether the engine places it (connected) or this strategy does (unconnected).
    /// </summary>
    private static double PortReservedHeight(int portCount, Theme theme) =>
        portCount == 0
            ? 0.0
            : BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true) + ((portCount + 0.5) * PortPitch(theme));

    /// <summary>Returns whether a port is drawn on the right face (an <c>out</c> port); all others use the left face.</summary>
    private static bool PrefersRightFace(PortInfo port) => port.Appearance.Direction == PortDirection.Out;

    /// <summary>Returns the number of ports on the face that has more of them (the frame's left and right faces).</summary>
    private static int PortsOnBusiestFace(IReadOnlyList<PortInfo> ports)
    {
        var right = ports.Count(PrefersRightFace);
        return Math.Max(right, ports.Count - right);
    }

    /// <summary>
    /// Places the declared ports of a root frame on its left face (right face for <c>out</c> ports),
    /// stacked from just below the frame header at the engine's port pitch, as absolute coordinates
    /// relative to the frame's own origin.
    /// </summary>
    /// <param name="ports">The frame's declared ports, in declaration order.</param>
    /// <param name="width">The frame width (the x of the right face).</param>
    /// <param name="bandBottom">The y below which ports may be placed (the bottom of the frame header).</param>
    /// <param name="theme">The active rendering theme.</param>
    /// <returns>The placed ports.</returns>
    private static IReadOnlyList<LayoutPort> PlaceFramePorts(
        IReadOnlyList<PortInfo> ports,
        double width,
        double bandBottom,
        Theme theme)
    {
        var result = new List<LayoutPort>(ports.Count);
        var pitch = PortPitch(theme);
        var left = 0;
        var right = 0;
        foreach (var port in ports)
        {
            var onRight = PrefersRightFace(port);
            var slot = onRight ? right++ : left++;
            result.Add(new LayoutPort(
                CentreX: onRight ? width : 0.0,
                CentreY: bandBottom + (pitch * (slot + 1)),
                Side: onRight ? PortSide.Right : PortSide.Left,
                ExternalLabel: port.Label,
                InternalLabel: null,
                MaxLabelWidth: double.PositiveInfinity,
                SourcePort: null,
                Appearance: port.Appearance));
        }

        return result;
    }

    /// <summary>
    /// Returns how far the glyphs and outside labels of a frame's ports reach beyond its left edge (x = 0)
    /// and its right edge (x = <paramref name="width"/>), including a small margin, so the canvas can make
    /// room and nothing is clipped.
    /// </summary>
    private static (double Left, double Right) MeasurePortOverhang(IReadOnlyList<LayoutPort> ports, double width, Theme theme)
    {
        if (ports.Count == 0)
        {
            return (0.0, 0.0);
        }

        var (minX, maxX) = MeasurePortExtent(ports, theme);
        return (Math.Max(0.0, theme.LabelPadding - minX), Math.Max(0.0, maxX + theme.LabelPadding - width));
    }

    /// <summary>
    /// Returns the smallest and largest x reached by the glyphs and outside labels of the given ports, or
    /// <c>(+infinity, -infinity)</c> when there are none (so callers' <c>Math.Max</c>/<c>Math.Min</c> math
    /// leaves their own extents unchanged).
    /// </summary>
    private static (double MinX, double MaxX) MeasurePortExtent(IReadOnlyList<LayoutPort> ports, Theme theme)
    {
        var minX = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        foreach (var port in ports)
        {
            var glyph = Bounds.Of(NotationMetrics.PortGlyphBounds(port));
            minX = Math.Min(minX, glyph.Left);
            maxX = Math.Max(maxX, glyph.Right);
            var label = OutwardLabelBounds(port, theme);
            if (label is { } l)
            {
                minX = Math.Min(minX, l.Left);
                maxX = Math.Max(maxX, l.Right);
            }
        }

        return (minX, maxX);
    }

    /// <summary>
    /// Places every declared port that no drawn connector names. For each such port the left face is tried
    /// (the right face for an <c>out</c> port), from just below the title area downward, for the first slot
    /// where the glyph and its outside label overlap no other box, port, label, or connector segment; the
    /// other face is tried next, and when neither has a clear slot the slot with the fewest overlaps wins.
    /// </summary>
    /// <param name="parts">The parts, whose declared ports are inspected.</param>
    /// <param name="rects">The final rectangle of each part, in container coordinates.</param>
    /// <param name="connected">The declared ports a connector names, by part index and port name.</param>
    /// <param name="content">Everything already placed (boxes, lines, and ports), used as obstacles.</param>
    /// <param name="theme">The active rendering theme.</param>
    /// <returns>The placed ports, in part and declaration order.</returns>
    private static IReadOnlyList<LayoutPort> PlaceUnconnectedPorts(
        IReadOnlyList<PartItem> parts,
        IReadOnlyList<Rect> rects,
        IReadOnlySet<(int Part, string Name)> connected,
        IReadOnlyList<LayoutNode> content,
        Theme theme)
    {
        var result = new List<LayoutPort>();

        // Gather the obstacles once: every part box, every connector segment, and the glyph and outside
        // label of every port already placed (extended as this method places more).
        var boxes = rects.Select(Bounds.Of).ToList();
        var segments = new List<Bounds>();
        var occupied = new List<Bounds>();
        foreach (var node in content)
        {
            switch (node)
            {
                case LayoutLine line:
                    for (var k = 1; k < line.Waypoints.Count; k++)
                    {
                        var (a, b) = (line.Waypoints[k - 1], line.Waypoints[k]);
                        segments.Add(new Bounds(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)));
                    }

                    break;
                case LayoutPort port:
                    occupied.AddRange(PortFootprint(port, theme));
                    break;
                default:
                    break;
            }
        }

        for (var i = 0; i < parts.Count; i++)
        {
            foreach (var info in parts[i].Ports)
            {
                if (connected.Contains((i, info.Name)))
                {
                    continue;
                }

                var preferred = PrefersRightFace(info) ? PortSide.Right : PortSide.Left;
                var other = preferred == PortSide.Right ? PortSide.Left : PortSide.Right;
                var best = FindPortSlot(info, i, rects[i], preferred, boxes, segments, occupied, theme);
                if (best.Collisions > 0)
                {
                    var alternative = FindPortSlot(info, i, rects[i], other, boxes, segments, occupied, theme);
                    if (alternative.Collisions < best.Collisions)
                    {
                        best = alternative;
                    }
                }

                result.Add(best.Port);
                occupied.AddRange(PortFootprint(best.Port, theme));
            }
        }

        return result;
    }

    /// <summary>Returns the rectangles a port occupies: its glyph and, when drawn outward, its label.</summary>
    private static IEnumerable<Bounds> PortFootprint(LayoutPort port, Theme theme)
    {
        yield return Bounds.Of(NotationMetrics.PortGlyphBounds(port));
        if (OutwardLabelBounds(port, theme) is { } label)
        {
            yield return label;
        }
    }

    /// <summary>
    /// Returns the bounds of a port's outside label, or <see langword="null"/> when the label is not drawn
    /// outward or the port carries no label text (a repeated glyph of a shared keyed port is unlabeled; the
    /// first glyph carries the single label).
    /// </summary>
    private static Bounds? OutwardLabelBounds(LayoutPort port, Theme theme) =>
        NotationMetrics.IsOutwardLabel(port) && port.ExternalLabel is { } text
            ? Bounds.Of(NotationMetrics.PortOutwardLabelBounds(port, text, theme))
            : null;

    /// <summary>
    /// Finds the best slot for one port on one face of a part: scans downward from below the title area
    /// and returns the first collision-free slot, or the slot with the fewest overlaps when there is none.
    /// </summary>
    private static (LayoutPort Port, int Collisions) FindPortSlot(
        PortInfo info,
        int partIndex,
        Rect rect,
        PortSide side,
        IReadOnlyList<Bounds> boxes,
        IReadOnlyList<Bounds> segments,
        IReadOnlyList<Bounds> occupied,
        Theme theme)
    {
        var glyphHalf = NotationMetrics.PortGlyphHalfSizeFor(info.Appearance);
        var x = side == PortSide.Left ? rect.X : rect.X + rect.Width;
        var first = rect.Y + BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true) + glyphHalf + PortClearance;
        var last = Math.Max(first, rect.Y + rect.Height - glyphHalf - PortClearance);

        (LayoutPort Port, int Collisions)? best = null;
        for (var y = first; y <= last + 1e-9; y += PortSlotStep)
        {
            var port = new LayoutPort(x, y, side, info.Label, null, double.PositiveInfinity, null, info.Appearance);
            var collisions = CountPortCollisions(port, partIndex, boxes, segments, occupied, theme);
            if (collisions == 0)
            {
                return (port, 0);
            }

            if (best is null || collisions < best.Value.Collisions)
            {
                best = (port, collisions);
            }
        }

        return best!.Value;
    }

    /// <summary>
    /// Counts the obstacles a candidate port overlaps: its glyph against the other part boxes, other
    /// ports, and connector segments; its outside label against every part box (its own included),
    /// other ports' glyphs and labels, and connector segments.
    /// </summary>
    private static int CountPortCollisions(
        LayoutPort port,
        int partIndex,
        IReadOnlyList<Bounds> boxes,
        IReadOnlyList<Bounds> segments,
        IReadOnlyList<Bounds> occupied,
        Theme theme)
    {
        var glyph = Bounds.Of(NotationMetrics.PortGlyphBounds(port));
        Bounds? label = OutwardLabelBounds(port, theme);

        var count = 0;
        for (var j = 0; j < boxes.Count; j++)
        {
            if (j != partIndex && glyph.Intersects(boxes[j], 0.0))
            {
                count++;
            }

            if (label is { } l && l.Intersects(boxes[j], 0.0))
            {
                count++;
            }
        }

        foreach (var other in occupied.Concat(segments))
        {
            if (glyph.Intersects(other, PortClearance) || (label is { } l2 && l2.Intersects(other, PortClearance)))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Returns <see langword="true"/> when interior recursion should be unconditionally unlimited for
    /// an entire diagram — either because <paramref name="scope"/> is <see langword="null"/> (no
    /// <c>expose</c> statement resolved for this view / the synthetic <c>--auto</c> view, unchanged
    /// pre-scoping behavior), or because at least one of its <see cref="ExposedScope.Subjects"/>
    /// carries a recursion kind meaning "unlimited depth"
    /// (<see cref="ExposeRecursionKind.MembershipRecursive"/> or
    /// <see cref="ExposeRecursionKind.NamespaceRecursive"/>).
    /// </summary>
    /// <remarks>
    /// This diagram-wide check is no longer the primary recursion gate — that decision is now made
    /// per branch, at depth 0, by <see cref="CollectParts"/> via
    /// <see cref="ExposeScopeResolver.MatchesUnlimitedSubject"/>, which knows specifically which
    /// subject matched each feature into scope (so a scope combining a recursive subject, e.g.
    /// <c>expose X::**;</c>, with a non-recursive subject, e.g. <c>expose Y;</c>, correctly recurses
    /// only <c>X</c>'s branch while <c>Y</c>'s branch stays depth-limited to itself — superseding the
    /// earlier "known limitation" where this method's diagram-wide answer was applied to the whole
    /// diagram regardless of which subject actually matched a given part). This method now serves
    /// only as the conservative fallback <see cref="CollectParts"/> uses for the rare edge case of a
    /// depth-0 feature that carries no qualified name at all — a shape with no specific subject to
    /// attribute the decision to, so the diagram-wide answer is used instead, matching the
    /// pre-per-branch behavior for that one edge case.
    /// </remarks>
    /// <param name="scope">The view's resolved <c>expose</c> containment-subtree scope, or null.</param>
    /// <returns>
    /// <see langword="true"/> when interior recursion is unlimited for the whole diagram;
    /// <see langword="false"/> when the resolved scope has no unlimited-depth subject.
    /// </returns>
    private static bool HasUnlimitedRecursion(ExposedScope? scope) =>
        scope is null ||
        scope.Subjects.Any(s => s.Recursion is ExposeRecursionKind.MembershipRecursive or ExposeRecursionKind.NamespaceRecursive);

    /// <summary>Returns whether a node is a usage drawn as a node of the view (a part, item, action, state, or occurrence).</summary>
    private static bool IsNodeUsage(SysmlNode node) =>
        node is SysmlFeatureNode feature && NodeKeywords.Contains(feature.FeatureKeyword);

    /// <summary>Returns whether a node is a part usage (the kind that makes an element a container worth drawing).</summary>
    private static bool IsPartUsage(SysmlNode node) =>
        node is SysmlFeatureNode { FeatureKeyword: "part" };

    /// <summary>
    /// Returns whether a connection node is drawn as a connector: <c>connection</c>, <c>binding</c>,
    /// <c>interface</c>, and <c>flow</c> usages are; <c>message</c> (sequence view) and <c>allocation</c>
    /// usages are not interconnection connectors.
    /// </summary>
    private static bool IsDrawnConnector(SysmlConnectionNode connection) =>
        ConnectorKeywords.Contains(connection.ConnectionKeyword);

    /// <summary>Counts the connector usages directly owned by a node.</summary>
    private static int CountConnectors(SysmlNode node) =>
        node.DiagramChildren.OfType<SysmlConnectionNode>().Count(IsDrawnConnector);

    /// <summary>
    /// Finds the element(s) whose interior to render: among scope-relevant candidates (non-stdlib
    /// <c>part def</c>s and, when no definition qualifies or a scope names them, top-level part usages
    /// that declare nested parts and connectors), prefers the composition-graph root — a candidate that
    /// is not itself referenced as another candidate's own nested <c>part</c> feature type — falling back
    /// to the candidate with the most connectors (then most part usages) when no single such root exists.
    /// When <paramref name="scope"/> is non-null (the view's resolved <c>expose</c> containment-subtree
    /// scope), candidates are first restricted to those relevant to the scope via
    /// <see cref="ExposeScopeResolver.IsRootRelevantToScope"/> — the candidate itself is an exposed
    /// subject, lies within an exposed subject's subtree, or an exposed subject lies within the
    /// candidate's own subtree. A broad expose (e.g. an entire namespace recursively) makes every
    /// definition in it scope-relevant, so preferring the one candidate nothing else composes is what
    /// actually identifies "the" root — a plain specificity/qualified-name-depth comparison cannot,
    /// since sibling leaf definitions are equally specific to each other regardless of which one
    /// happens to be the true top of the tree. When one or more candidates qualify as composition-graph
    /// roots, selection narrows to just that set, and ties among them (or among the full candidate
    /// set, when none qualify — e.g. a cyclic composition graph where every candidate is someone's
    /// child) are broken by specificity (deepest/longest qualified name wins) via
    /// <see cref="ExposeScopeResolver.IsMoreSpecificCandidate"/>, with the connectors/parts heuristic
    /// used only to break ties between equally specific candidates. When no candidate is
    /// scope-relevant, no root is chosen (an empty canvas results, matching the existing null-root
    /// path). When <paramref name="scope"/> is <see langword="null"/>, selection is the plain
    /// connectors/parts heuristic over the part definitions, unchanged; a part usage root is then
    /// considered only when there is no part definition candidate at all. When <paramref name="scope"/> is
    /// non-null and more than one candidate qualifies as a composition-graph root — ignoring any that is
    /// merely a namespace ancestor of another — every one of them is returned, in declaration order, so
    /// independently exposed systems are all drawn (SysML v2 §8.2.3.11).
    /// </summary>
    private static IReadOnlyList<SysmlNode> FindRoots(
        SysmlWorkspace workspace,
        ExposedScope? scope,
        DefinitionIndex defsByName)
    {
        var candidates = new List<(string QualifiedName, SysmlNode Node, int Connections, int Parts)>();
        var usageCandidates = new List<(string QualifiedName, SysmlNode Node, int Connections, int Parts)>();
        foreach (var (qualifiedName, node) in workspace.Declarations)
        {
            // Members modeled for name resolution only are never drawn, nor chosen as a root.
            if (node.IsExcludedFromDiagrams)
            {
                continue;
            }

            var isDefinition = node is SysmlDefinitionNode { DefinitionKeyword: "part def" };
            if (!isDefinition && !IsRootUsage(qualifiedName, node, workspace))
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

            var entry = (qualifiedName, node, CountConnectors(node), node.DiagramChildren.Count(IsPartUsage));
            (isDefinition ? candidates : usageCandidates).Add(entry);
        }

        // A top-level part usage is a root only when a scope names it, or when no definition could be.
        if (scope is not null || candidates.Count == 0)
        {
            candidates.AddRange(usageCandidates);
        }

        if (candidates.Count == 0)
        {
            return [];
        }

        // Every candidate that some other candidate's own nested part feature resolves to as its
        // type — i.e. every candidate that is a child in the composition graph, not its top.
        var usedAsChildType = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (_, candidate, _, _) in candidates)
        {
            foreach (var feature in candidate.DiagramChildren.OfType<SysmlFeatureNode>())
            {
                if (feature.FeatureKeyword != "part")
                {
                    continue;
                }

                if (ResolveByTyping(feature, defsByName)?.QualifiedName is { Length: > 0 } usedName)
                {
                    usedAsChildType.Add(usedName);
                }
            }
        }

        // A true composition-graph root must itself compose something (Parts > 0); otherwise an
        // unrelated orphan leaf definition (composes nothing, is composed by nothing) would also
        // qualify as "unused," and could then win the specificity tie-break below purely by having
        // a longer qualified name, even though it has no interior worth rendering at all.
        var topCandidates = candidates.Where(c => c.Parts > 0 && !usedAsChildType.Contains(c.QualifiedName)).ToList();
        if (scope is not null)
        {
            // An ancestor is only relevant because it contains a more specific exposed root, so
            // only the most specific of nested candidates is drawn as its own root.
            var independentRoots = topCandidates
                .Where(c => !topCandidates.Any(d => d.QualifiedName.StartsWith(c.QualifiedName + "::", StringComparison.Ordinal)))
                .ToList();
            if (independentRoots.Count > 1)
            {
                return independentRoots.Select(c => c.Node).ToList();
            }
        }

        var pool = topCandidates.Count > 0 ? topCandidates : candidates;

        SysmlNode? best = null;
        string? bestQualifiedName = null;
        var bestConnections = -1;
        var bestParts = -1;

        foreach (var (qualifiedName, node, connections, partCount) in pool)
        {
            var scoreBetter = connections > bestConnections || (connections == bestConnections && partCount > bestParts);
            var isBetter = scope is not null
                ? ExposeScopeResolver.IsMoreSpecificCandidate(qualifiedName, bestQualifiedName, scoreBetter)
                : scoreBetter;

            if (isBetter)
            {
                best = node;
                bestQualifiedName = qualifiedName;
                bestConnections = connections;
                bestParts = partCount;
            }
        }

        return best is null ? [] : [best];
    }

    /// <summary>
    /// Returns whether a declaration is a part usage that can be the root of an interconnection view: a
    /// <c>part</c> usage declared directly in a package (not nested in a definition or another usage, which
    /// are drawn inside their owner) whose own body declares both nested parts and connectors.
    /// </summary>
    private static bool IsRootUsage(string qualifiedName, SysmlNode node, SysmlWorkspace workspace)
    {
        if (!IsPartUsage(node) || !node.DiagramChildren.Any(IsPartUsage) || CountConnectors(node) == 0)
        {
            return false;
        }

        var owner = OwnerOf(qualifiedName);
        return owner is null || (workspace.Declarations.TryGetValue(owner, out var ownerNode) && ownerNode is SysmlPackageNode);
    }

    /// <summary>
    /// Collects the nested usages of a definition or usage, sized for rendering. A usage that is a
    /// container (its type resolves to a non-stdlib <c>part def</c> with its own internal parts not already
    /// on the recursion path, or it declares nested parts in an inline body) is laid out recursively and
    /// sized to fit its interior; every other usage is sized intrinsically as a leaf. When
    /// <paramref name="scope"/> is non-null <em>and <paramref name="depth"/> is 0</em> (the root element's
    /// own direct usages), a usage whose own <c>QualifiedName</c> fails
    /// <see cref="ExposeScopeResolver.IsInSubjectScope"/> is skipped — this lets a narrow <c>expose</c>
    /// (e.g. one specific subsystem, not the whole system) select which of the root's own branches to draw.
    /// Scope is never re-applied to any deeper recursive call (<paramref name="depth"/> &gt; 0) by
    /// re-checking a deeper feature's own qualified name — a part def's own nested parts are not themselves
    /// separate members of the exposed namespace (namespace-declaration location and composition/usage depth
    /// are independent axes in SysML v2), so re-matching a deeper qualified name against the scope would be
    /// incorrect. Instead, whether a container part's interior is expanded at all past depth 0 is decided
    /// per branch: at depth 0, each feature's own branch-unlimited-recursion decision is computed once (via
    /// <see cref="DetermineBranchUnlimitedRecursion"/>, from <paramref name="ancestorUnlimitedRecursion"/>
    /// when already known — i.e. this is itself a recursive call from an ancestor branch — or freshly
    /// derived from which specific subject matched this feature otherwise) and then propagated unchanged to
    /// every one of that feature's own descendants (see <see cref="BuildPartItem"/>). A scope combining a
    /// recursive subject (e.g. <c>expose X::**;</c>) with a non-recursive subject (e.g. <c>expose Y;</c>)
    /// therefore correctly recurses only the branch matched by the recursive subject, while the branch
    /// matched only by the non-recursive subject stays depth-limited to itself — superseding the earlier
    /// diagram-wide simplification.
    /// </summary>
    /// <param name="root">The definition or usage whose direct node usages to collect.</param>
    /// <param name="theme">The active rendering theme.</param>
    /// <param name="depth">Nesting depth of this call (0 for the root element's own children).</param>
    /// <param name="defsByName">Container-definition index and type resolver.</param>
    /// <param name="visited">Qualified names already on the recursion path, guarding against cycles.</param>
    /// <param name="scope">
    /// The view's resolved <c>expose</c> containment-subtree scope, or null when no scoping applies.
    /// </param>
    /// <param name="ancestorUnlimitedRecursion">
    /// The already-decided per-branch unlimited-recursion flag inherited from an ancestor call, or
    /// <see langword="null"/> only at the true top of recursion (<paramref name="depth"/> is always 0
    /// in that case). See <see cref="LayOutInterior"/>'s remarks.
    /// </param>
    private static IReadOnlyList<PartItem> CollectParts(
        SysmlNode root,
        Theme theme,
        int depth,
        DefinitionIndex defsByName,
        ISet<string> visited,
        ExposedScope? scope,
        bool? ancestorUnlimitedRecursion)
    {
        var result = new List<PartItem>();
        foreach (var feature in root.DiagramChildren.OfType<SysmlFeatureNode>())
        {
            if (!IsNodeUsage(feature))
            {
                continue;
            }

            if (depth == 0 && scope is not null && feature.QualifiedName is { Length: > 0 } fqn &&
                !ExposeScopeResolver.IsInSubjectScope(fqn, scope))
            {
                continue;
            }

            var unlimitedRecursion = ancestorUnlimitedRecursion ?? DetermineBranchUnlimitedRecursion(feature, scope);
            result.Add(BuildPartItem(feature, theme, depth, defsByName, visited, unlimitedRecursion));
        }

        return result;
    }

    /// <summary>
    /// Decides, for one depth-0 feature, whether its own branch's interior recursion is
    /// unconditionally unlimited: <see langword="true"/> when <paramref name="scope"/> is
    /// <see langword="null"/> (no <c>expose</c> scoping at all — unchanged unconditional recursion);
    /// otherwise, when the feature carries a qualified name, whether it matches a subject with
    /// unlimited-depth recursion via <see cref="ExposeScopeResolver.MatchesUnlimitedSubject"/> — the
    /// specific subject that matched this feature into scope, not the scope as a whole. Falls back to
    /// the conservative diagram-wide <see cref="HasUnlimitedRecursion"/> check only for the rare edge
    /// case of a feature with no qualified name at all, since there is then no specific subject to
    /// attribute the decision to.
    /// </summary>
    /// <param name="feature">The depth-0 usage being collected.</param>
    /// <param name="scope">The view's resolved <c>expose</c> containment-subtree scope, or null.</param>
    /// <returns>Whether this feature's own branch should recurse without a depth limit.</returns>
    private static bool DetermineBranchUnlimitedRecursion(SysmlFeatureNode feature, ExposedScope? scope)
    {
        if (scope is null)
        {
            return true;
        }

        return feature.QualifiedName is { Length: > 0 } fqn
            ? ExposeScopeResolver.MatchesUnlimitedSubject(fqn, scope)
            : HasUnlimitedRecursion(scope);
    }

    /// <summary>
    /// Builds a single nested usage's <see cref="PartItem"/>, recursing into its interior when it is a
    /// container (its type resolves to a non-stdlib <c>part def</c> with its own internal parts, not
    /// already on the recursion path, or it declares nested parts in an inline body) <em>and</em>
    /// <paramref name="unlimitedRecursion"/> is <see langword="true"/>, and computing an intrinsic leaf size
    /// otherwise. Extracted from <see cref="CollectParts"/> so the same container-vs-leaf recursion is
    /// reused verbatim by <see cref="CollectTopLevelScopedParts"/> — the boxless top-level fallback path
    /// must lay out each top-level feature exactly as a normal nested part would be, per the "no
    /// duplicated logic" coding principle.
    /// </summary>
    /// <param name="feature">The usage to build a <see cref="PartItem"/> for.</param>
    /// <param name="theme">The active rendering theme.</param>
    /// <param name="depth">Nesting depth of the feature's own container box (0 for a top-level part).</param>
    /// <param name="defsByName">Container-definition index and type resolver.</param>
    /// <param name="visited">Qualified names already on the recursion path, guarding against cycles.</param>
    /// <param name="unlimitedRecursion">
    /// This branch's already-decided unlimited-recursion flag — computed once at depth 0 by
    /// <see cref="CollectParts"/> (via <see cref="DetermineBranchUnlimitedRecursion"/>) or
    /// <see cref="CollectTopLevelScopedParts"/>, from which specific subject matched this branch's
    /// own top-of-branch feature into scope, and propagated unchanged to every descendant of that
    /// feature (this method's own recursive <see cref="LayOutInterior"/> call below passes it on as
    /// <c>ancestorUnlimitedRecursion</c>, never re-deriving it). When <see langword="false"/>, a
    /// container part's own interior is never expanded — it always renders as an intrinsic-sized
    /// leaf box, regardless of <paramref name="depth"/>.
    /// </param>
    /// <returns>The built <see cref="PartItem"/>, container or leaf.</returns>
    private static PartItem BuildPartItem(
        SysmlFeatureNode feature,
        Theme theme,
        int depth,
        DefinitionIndex defsByName,
        ISet<string> visited,
        bool unlimitedRecursion)
    {
        var name = feature.Name ?? feature.FeatureTyping ?? feature.FeatureKeyword;
        var keyword = GeneralViewLayoutStrategy.UsageKeyword(feature);
        var label = GeneralViewLayoutStrategy.FormatUsageNameLine(feature, name);
        var ports = ResolvePorts(feature, defsByName);

        if (unlimitedRecursion && TryResolveContainer(feature, defsByName, visited, out var container))
        {
            // Container part: lay out its interior bottom-up and treat it as an atomic node.
            // Scope is intentionally not carried into this recursive call — see the remarks on
            // CollectParts for why nested composition structure's own qualified name is never
            // re-checked against scope; unlimitedRecursion (already decided for this branch) is
            // threaded through unchanged as the ancestor's decision so every descendant of this
            // branch shares the same depth-limiting decision.
            var childVisited = new HashSet<string>(visited, StringComparer.Ordinal) { container.QualifiedName! };
            var inner = LayOutInterior(container, theme, depth + 1, defsByName, childVisited, scope: null, ancestorUnlimitedRecursion: unlimitedRecursion);

            // The container is never narrower than its own formatted label (short name, type,
            // multiplicity); the interior width alone ignores the usage label. It is never shorter
            // than its ports need, either.
            var labelWidth = ComputePartSize(label, keyword, theme).Width;
            var containerHeight = Math.Max(inner.Height, PortReservedHeight(ports.Count, theme));
            return new PartItem(name, keyword, label, Math.Max(inner.Width, labelWidth), containerHeight, inner.Content, feature.IsReference, [], ports);
        }

        // Leaf part: intrinsic size, no nested content. Reached either because the usage is not a
        // container, or because unlimitedRecursion is false and interior expansion is intentionally
        // stopped here (the part still renders as its own node — its own nested parts are simply not
        // drawn). Its doc and attribute rows are drawn as compartments; its ports are glyphs.
        var compartments = BuildLeafCompartments(feature, label, keyword, theme);
        var (width, height) = compartments.Count > 0
            ? GeneralViewLayoutStrategy.ComputeBoxSize(label, keyword, compartments, theme)
            : ComputePartSize(label, keyword, theme);
        height = Math.Max(height, PortReservedHeight(ports.Count, theme));
        return new PartItem(name, keyword, label, width, height, null, feature.IsReference, compartments, ports);
    }

    /// <summary>
    /// Builds the compartments of a leaf usage box: <c>doc</c> for its documentation and <c>attributes</c>
    /// for its attribute usages (SysML v2.0 clause 8.2.3.5 compartment stack). Ports are drawn as glyphs on
    /// the border and nested parts as boxes, so they are not rows; connectors are drawn as lines.
    /// </summary>
    private static IReadOnlyList<LayoutCompartment> BuildLeafCompartments(
        SysmlFeatureNode feature,
        string label,
        string keyword,
        Theme theme)
    {
        var attributes = GeneralViewLayoutStrategy.BuildCompartments(
            feature,
            feature.QualifiedName ?? feature.Name ?? string.Empty,
            drawnAsNodes: null,
            onlyKeywords: AttributeKeywords);

        // The doc text is wrapped to the width the box has without it, as in the General view.
        var baseWidth = ComputePartSize(label, keyword, theme).Width;
        return GeneralViewLayoutStrategy.BuildDocCompartment(feature.Annotations, baseWidth, theme) is { } doc
            ? [doc, .. attributes]
            : attributes;
    }

    /// <summary>The usage keywords shown as rows of a leaf part's compartments.</summary>
    private static readonly IReadOnlySet<string> AttributeKeywords = new HashSet<string>(StringComparer.Ordinal) { "attribute" };

    /// <summary>
    /// Collects every top-level node usage (<c>part</c>, <c>item</c>, <c>action</c>, <c>state</c>, or
    /// <c>occurrence</c>) the resolved <c>expose</c> scope directly includes, for the no-single-root
    /// fallback path: <see cref="FindRoots"/> found no element worth rendering as a container, but the scope
    /// itself names one or more concrete features to draw. Per SysML v2 spec §9.2.20.2.6 ("exposed features
    /// as nodes, nested features as nested nodes") and §8.3.26.11 (an InterconnectionView's subject need not
    /// be a single definition), each matching feature is rendered directly as its own top-level node rather
    /// than the diagram falling back to an empty canvas.
    /// </summary>
    /// <param name="workspace">The workspace, scanned for every non-stdlib node usage.</param>
    /// <param name="scope">The view's resolved <c>expose</c> containment-subtree scope.</param>
    /// <param name="theme">The active rendering theme.</param>
    /// <param name="defsByName">Container-definition index and type resolver.</param>
    /// <returns>
    /// The matched top-level parts paired with their owning definition's qualified name, each
    /// recursively laid out via <see cref="BuildPartItem"/> exactly as a normal nested container part
    /// would be, in <paramref name="workspace"/>'s declaration order. Empty when no non-stdlib
    /// node usage satisfies the scope.
    /// </returns>
    private static IReadOnlyList<TopLevelPart> CollectTopLevelScopedParts(
        SysmlWorkspace workspace,
        ExposedScope scope,
        Theme theme,
        DefinitionIndex defsByName)
    {
        var matched = new List<SysmlFeatureNode>();
        foreach (var (qualifiedName, node) in workspace.Declarations)
        {
            // Members modeled for name resolution only are never drawn, nor chosen as a root.
            if (node.IsExcludedFromDiagrams)
            {
                continue;
            }

            if (node is not SysmlFeatureNode feature || !IsNodeUsage(feature))
            {
                continue;
            }

            if (StdlibFilter.IsStdlibElement(qualifiedName, workspace.StdlibNames))
            {
                continue;
            }

            if (!ExposeScopeResolver.IsInSubjectScope(qualifiedName, scope))
            {
                continue;
            }

            matched.Add(feature);
        }

        // Exclude any matched feature nested ("::"-prefixed) under another matched feature's own
        // qualified name: it is already reachable as that ancestor's own nested part (rendered via
        // the recursive BuildPartItem call below), so it must not also appear as its own separate
        // top-level node.
        var matchedNames = matched.Select(f => f.QualifiedName).Where(n => n is { Length: > 0 }).ToHashSet(StringComparer.Ordinal);
        var topLevel = matched
            .Where(f => f.QualifiedName is not { Length: > 0 } fqn ||
                        !matchedNames.Any(other => other != fqn && fqn.StartsWith(other + "::", StringComparison.Ordinal)))
            .ToList();

        var result = new List<TopLevelPart>();
        foreach (var feature in topLevel)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var unlimitedRecursion = DetermineBranchUnlimitedRecursion(feature, scope);
            var part = BuildPartItem(feature, theme, depth: 0, defsByName, visited, unlimitedRecursion);
            result.Add(new TopLevelPart(part, OwnerQualifiedNameOf(feature.QualifiedName)));
        }

        return result;
    }

    /// <summary>
    /// Derives a feature's owning definition's qualified name by stripping the last
    /// <c>"::"</c>-separated segment of its own qualified name — see <see cref="TopLevelPart"/>'s
    /// remarks for why this string-derivation is sufficient without a dedicated parent pointer.
    /// </summary>
    /// <param name="qualifiedName">The feature's own qualified name, or null.</param>
    /// <returns>The owning definition's qualified name, or null when it cannot be derived.</returns>
    private static string? OwnerQualifiedNameOf(string? qualifiedName)
    {
        if (qualifiedName is not { Length: > 0 })
        {
            return null;
        }

        var sep = qualifiedName.LastIndexOf("::", StringComparison.Ordinal);
        return sep >= 0 ? qualifiedName[..sep] : null;
    }

    /// <summary>
    /// Resolves connections between the boxless top-level parts collected by
    /// <see cref="CollectTopLevelScopedParts"/>, reusing <see cref="ResolveEndpoint"/> verbatim.
    /// Unlike <see cref="ResolveConnections"/> (which only looks at one element's own direct
    /// children), a top-level connection may be declared inside any definition in the workspace, so
    /// every definition's connections are scanned; a connection is only drawn when its own
    /// <c>QualifiedName</c> is itself in scope <em>and</em> both endpoints resolve into the top-level
    /// part set — an incidental connection between unrelated parts must never be surfaced just
    /// because it happens to share a name with one of the rendered top-level features.
    /// </summary>
    /// <remarks>
    /// A connection's endpoints are only meaningful relative to its own containing definition's
    /// direct children — two different containing definitions may each declare a same-simple-named
    /// part (e.g. both have <c>part logger : Logger;</c>). A single flat cross-workspace name index
    /// built from every collected top-level part (as a naive <see cref="BuildPartIndex"/> call would)
    /// would silently keep only the first such part under that name, causing bogus or dropped
    /// connections for every other definition sharing the name. Instead, this groups
    /// <paramref name="topLevelParts"/> by <see cref="TopLevelPart.OwnerQualifiedName"/> into a
    /// per-owner name → index map (<c>byOwner</c>, keyed against the overall <paramref name="topLevelParts"/>
    /// list position, matching the part ordering the caller lays out), then resolves each definition's
    /// own connections only against its own restricted, unambiguous index — never the flat union.
    /// </remarks>
    /// <param name="defsByName">Container-definition index and type resolver.</param>
    /// <param name="topLevelParts">
    /// The top-level parts collected by <see cref="CollectTopLevelScopedParts"/>, each paired with
    /// its owning definition's qualified name.
    /// </param>
    /// <param name="scope">The view's resolved <c>expose</c> containment-subtree scope.</param>
    /// <returns>The resolved connection pairs between top-level parts, if any.</returns>
    private static IReadOnlyList<ConnItem> ResolveTopLevelConnections(
        DefinitionIndex defsByName,
        IReadOnlyList<TopLevelPart> topLevelParts,
        ExposedScope scope)
    {
        // Group the collected top-level parts by their owning definition, building each owner's own
        // restricted simple-name → overall-list-index map so same-simple-named parts owned by
        // different definitions can never collide.
        var allParts = topLevelParts.Select(t => t.Part).ToList();
        var byOwner = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        for (var i = 0; i < topLevelParts.Count; i++)
        {
            var owner = topLevelParts[i].OwnerQualifiedName;
            if (owner is null)
            {
                continue;
            }

            if (!byOwner.TryGetValue(owner, out var ownerIndex))
            {
                ownerIndex = new Dictionary<string, int>(StringComparer.Ordinal);
                byOwner[owner] = ownerIndex;
            }

            ownerIndex.TryAdd(topLevelParts[i].Part.Name, i);
        }

        var pairs = new List<ConnItem>();
        foreach (var def in defsByName.Values.Distinct())
        {
            if (def.QualifiedName is not { Length: > 0 } ownerName || !byOwner.TryGetValue(ownerName, out var partIndex))
            {
                // This definition owns no collected top-level part, so none of its own connections
                // could possibly resolve to a top-level part; skip it rather than resolving against
                // an unrelated (or the wrong) owner's index.
                continue;
            }

            foreach (var conn in def.DiagramChildren.OfType<SysmlConnectionNode>().Where(IsDrawnConnector))
            {
                if (conn.QualifiedName is not { Length: > 0 } fqn || !ExposeScopeResolver.IsInSubjectScope(fqn, scope))
                {
                    continue;
                }

                if (TryResolveConnection(conn, partIndex, allParts) is { } item)
                {
                    pairs.Add(item);
                }
            }
        }

        return pairs;
    }

    /// <summary>
    /// The candidate container definitions — non-standard-library <c>part def</c>s that have at
    /// least one nested <c>part</c> usage — keyed by qualified name, together with the KerML
    /// <see cref="NameResolver"/> used to resolve a type reference and the workspace used to look up
    /// any definition (a part's type need not be a container to contribute ports).
    /// </summary>
    private sealed class DefinitionIndex
    {
        private readonly Dictionary<string, SysmlDefinitionNode> _byQualifiedName;
        private readonly NameResolver _resolver;
        private readonly SysmlWorkspace _workspace;

        /// <summary>Initializes the index.</summary>
        public DefinitionIndex(Dictionary<string, SysmlDefinitionNode> byQualifiedName, NameResolver resolver, SysmlWorkspace workspace)
        {
            _byQualifiedName = byQualifiedName;
            _resolver = resolver;
            _workspace = workspace;
        }

        /// <summary>Gets every indexed container definition.</summary>
        public IEnumerable<SysmlDefinitionNode> Values => _byQualifiedName.Values;

        /// <summary>Looks up a container definition by qualified name.</summary>
        public bool TryGetValue(string qualifiedName, [MaybeNullWhen(false)] out SysmlDefinitionNode def) =>
            _byQualifiedName.TryGetValue(qualifiedName, out def);

        /// <summary>
        /// Resolves a raw type reference relative to <paramref name="localNamespace"/> by KerML
        /// name resolution; returns null when it does not resolve.
        /// </summary>
        public string? Resolve(string reference, string? localNamespace) =>
            _resolver.Resolve(reference, localNamespace);

        /// <summary>
        /// Resolves a usage's type to its definition, whatever kind of definition it is: first via the
        /// resolved <see cref="SysmlEdgeKind.Typing"/> edge, otherwise by KerML name resolution of the raw
        /// typing text relative to the usage's owner. Returns null when the usage is untyped or the type does
        /// not resolve to a definition.
        /// </summary>
        public SysmlDefinitionNode? ResolveTypeDefinition(SysmlFeatureNode feature)
        {
            var typing = feature.FeatureTyping;
            if (string.IsNullOrEmpty(typing))
            {
                return null;
            }

            var resolved = feature.ResolvedEdges.FirstOrDefault(e => e.Kind == SysmlEdgeKind.Typing)?.TargetQualifiedName
                ?? Resolve(typing, OwnerOf(feature.QualifiedName));
            return Lookup(resolved);
        }

        /// <summary>
        /// Returns the definitions a definition specializes (<c>:&gt;</c>), by the resolved
        /// <see cref="SysmlEdgeKind.Supertype"/> edges or, when there are none, by name resolution of its
        /// raw supertype names relative to its own namespace.
        /// </summary>
        public IEnumerable<SysmlDefinitionNode> SupertypeDefinitions(SysmlDefinitionNode def)
        {
            var edges = def.ResolvedEdges.Where(e => e.Kind == SysmlEdgeKind.Supertype).Select(e => e.TargetQualifiedName).ToList();
            var targets = edges.Count > 0
                ? edges.Select(e => (string?)e).ToList()
                : def.SupertypeNames.Select(n => Resolve(n, OwnerOf(def.QualifiedName))).ToList();
            return targets.Select(Lookup).OfType<SysmlDefinitionNode>();
        }

        /// <summary>Looks up any definition declared in the workspace by qualified name.</summary>
        private SysmlDefinitionNode? Lookup(string? qualifiedName) =>
            qualifiedName is not null && _workspace.Declarations.TryGetValue(qualifiedName, out var node)
                ? node as SysmlDefinitionNode
                : null;
    }

    /// <summary>
    /// Builds the <see cref="DefinitionIndex"/> of candidate container definitions.
    /// </summary>
    private static DefinitionIndex BuildDefinitionIndex(SysmlWorkspace workspace)
    {
        var index = new Dictionary<string, SysmlDefinitionNode>(StringComparer.Ordinal);
        foreach (var (qualifiedName, node) in workspace.Declarations)
        {
            // Members modeled for name resolution only are never drawn, nor chosen as a root.
            if (node.IsExcludedFromDiagrams)
            {
                continue;
            }

            if (node is not SysmlDefinitionNode def || def.DefinitionKeyword != "part def")
            {
                continue;
            }

            if (StdlibFilter.IsStdlibElement(qualifiedName, workspace.StdlibNames))
            {
                continue;
            }

            var hasPartChild = def.DiagramChildren.Any(IsPartUsage);
            if (!hasPartChild)
            {
                continue;
            }

            index.TryAdd(qualifiedName, def);
        }

        return new DefinitionIndex(index, FilterExpressionEvaluator.CreateNameResolver(workspace), workspace);
    }

    /// <summary>
    /// Resolves the container a usage should be drawn as: the usage itself when its own inline body
    /// declares nested parts (and it is not already on the recursion path), otherwise the
    /// container definition its type resolves to (see <see cref="ResolveByTyping"/>), excluding any
    /// definition already on the recursion path (cycle guard). A usage that is neither — or whose type is on
    /// the path or does not resolve to a container — is treated as a leaf.
    /// </summary>
    private static bool TryResolveContainer(
        SysmlFeatureNode feature,
        DefinitionIndex defsByName,
        ISet<string> visited,
        [NotNullWhen(true)] out SysmlNode? container)
    {
        container = null;
        if (feature.DiagramChildren.Any(IsPartUsage))
        {
            if (feature.QualifiedName is { Length: > 0 } ownName && visited.Contains(ownName))
            {
                return false;
            }

            container = feature;
            return true;
        }

        var def = ResolveByTyping(feature, defsByName);
        if (def?.QualifiedName is null || visited.Contains(def.QualifiedName))
        {
            return false;
        }

        container = def;
        return true;
    }

    /// <summary>
    /// Resolves a part feature's type to its container definition: first via the semantic model's
    /// resolved <see cref="SysmlEdgeKind.Typing"/> edge, used as resolved (the semantic resolver
    /// already applies KerML 8.2.3.5 member visibility, filtered imports, and import scoping);
    /// otherwise by KerML 8.2.3.5 name resolution
    /// of the raw typing text relative to the feature's owning namespace (owned, imported, and
    /// inherited memberships, then outward). Returns <see langword="null"/> if nothing resolves to
    /// a container definition; an unresolved reference makes the part a leaf.
    /// </summary>
    private static SysmlDefinitionNode? ResolveByTyping(
        SysmlFeatureNode feature,
        DefinitionIndex defsByName)
    {
        var typing = feature.FeatureTyping;
        if (string.IsNullOrEmpty(typing))
        {
            return null;
        }

        // An authoritative resolution that is not a container means the part is a leaf; never
        // match a same-named container elsewhere in the workspace.
        var owner = OwnerOf(feature.QualifiedName);
        var resolved = feature.ResolvedEdges.FirstOrDefault(e => e.Kind == SysmlEdgeKind.Typing)?.TargetQualifiedName
            ?? defsByName.Resolve(typing, owner);
        return resolved is not null && defsByName.TryGetValue(resolved, out var def) ? def : null;
    }

    /// <summary>Returns the owning namespace's qualified name (the prefix before the last <c>::</c>), or null.</summary>
    private static string? OwnerOf(string? qualifiedName)
    {
        var sep = qualifiedName?.LastIndexOf("::", StringComparison.Ordinal) ?? -1;
        return sep >= 0 ? qualifiedName![..sep] : null;
    }

    /// <summary>
    /// Resolves the ports a node declares: its own <c>port</c> usages, then those of its type (for a usage)
    /// and of every definition in the specialization chain, nearest first. A port declared more than once
    /// (a redeclaration on a more specific element) is listed once, as its most specific declaration.
    /// </summary>
    /// <param name="node">A part usage or a definition.</param>
    /// <param name="defsByName">The index used to resolve the type and supertypes.</param>
    /// <returns>The declared ports, in declaration order.</returns>
    private static IReadOnlyList<PortInfo> ResolvePorts(SysmlNode node, DefinitionIndex defsByName)
    {
        var ports = new List<PortInfo>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        AddDeclaredPorts(node, ports, seen);

        // Breadth-first over the type (for a usage) or the supertypes (for a definition), guarding cycles.
        var pending = new Queue<SysmlDefinitionNode>();
        switch (node)
        {
            case SysmlFeatureNode feature when defsByName.ResolveTypeDefinition(feature) is { } type:
                pending.Enqueue(type);
                break;
            case SysmlDefinitionNode definition:
                foreach (var supertype in defsByName.SupertypeDefinitions(definition))
                {
                    pending.Enqueue(supertype);
                }

                break;
            default:
                break;
        }

        var visitedDefinitions = new HashSet<SysmlDefinitionNode>(ReferenceEqualityComparer.Instance);
        while (pending.Count > 0)
        {
            var next = pending.Dequeue();
            if (!visitedDefinitions.Add(next))
            {
                continue;
            }

            AddDeclaredPorts(next, ports, seen);
            foreach (var supertype in defsByName.SupertypeDefinitions(next))
            {
                pending.Enqueue(supertype);
            }
        }

        return ports;
    }

    /// <summary>Adds the named <c>port</c> usages directly owned by <paramref name="owner"/> that are not yet listed.</summary>
    private static void AddDeclaredPorts(SysmlNode owner, List<PortInfo> ports, HashSet<string> seen)
    {
        foreach (var port in owner.DiagramChildren.OfType<SysmlFeatureNode>())
        {
            if (port.FeatureKeyword == "port" && port.Name is { Length: > 0 } name && seen.Add(name))
            {
                ports.Add(MakePortInfo(port, name));
            }
        }
    }

    /// <summary>
    /// Builds how a declared port is drawn (SysML v2.0 clause 8.2.3.12): an outlined rounded square whose
    /// arrow follows the declared direction, with a dashed outline for a reference port, and the outside
    /// label <c>name : Type [multiplicity]</c> (<c>name : ~Type</c> for a conjugated port).
    /// </summary>
    private static PortInfo MakePortInfo(SysmlFeatureNode port, string name)
    {
        var direction = port.Direction switch
        {
            SysmlFeatureDirection.In => PortDirection.In,
            SysmlFeatureDirection.Out => PortDirection.Out,
            SysmlFeatureDirection.InOut => PortDirection.InOut,
            _ => PortDirection.None,
        };
        var appearance = new PortAppearance(
            direction,
            PortGlyph.Outlined,
            port.IsReference ? LineStyle.Dashed : LineStyle.Solid,
            PortLabelPlacement.Outward);
        var label = GeneralViewLayoutStrategy.FormatNameLine(
            shortName: null,
            name,
            typing: GeneralViewLayoutStrategy.FormatTyping(port),
            multiplicity: port.Multiplicity);
        return new PortInfo(name, label, appearance);
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
    /// Resolves each drawn connector's endpoints to nested-part indices by matching the first segment of
    /// the dotted endpoint reference against the part names, attaching each end through the declared port
    /// its next segment names.
    /// </summary>
    private static IReadOnlyList<ConnItem> ResolveConnections(
        SysmlNode root,
        Dictionary<string, int> partIndex,
        IReadOnlyList<PartItem> parts)
    {
        var pairs = new List<ConnItem>();
        foreach (var conn in root.DiagramChildren.OfType<SysmlConnectionNode>().Where(IsDrawnConnector))
        {
            if (TryResolveConnection(conn, partIndex, parts) is { } item)
            {
                pairs.Add(item);
            }
        }

        return pairs;
    }

    /// <summary>
    /// Resolves one connector to a <see cref="ConnItem"/>, or null when either endpoint does not name a
    /// part of <paramref name="parts"/> (by <paramref name="partIndex"/>) or both name the same part.
    /// </summary>
    private static ConnItem? TryResolveConnection(
        SysmlConnectionNode conn,
        Dictionary<string, int> partIndex,
        IReadOnlyList<PartItem> parts)
    {
        var (a, portA) = ResolveEndpoint(conn.EndpointA, partIndex, parts);
        var (b, portB) = ResolveEndpoint(conn.EndpointB, partIndex, parts);
        return a >= 0 && b >= 0 && a != b ? new ConnItem(a, b, portA, portB, StyleOf(conn)) : null;
    }

    /// <summary>
    /// Resolves a dotted endpoint reference (e.g. <c>"motor.encoder"</c>) to a part index by its first
    /// segment and to the port it attaches through. When the next segment names a port the part declares,
    /// the connector attaches to that port (keyed by its name, so every connector naming it shares one
    /// port) with its declared label and appearance. Any other remainder is kept as a bare label on an
    /// undirected outlined port. A reference with no further segments (a bare part name) attaches to the
    /// part itself, with no port.
    /// </summary>
    /// <remarks>
    /// This resolves the <em>full</em> dotted path for labeling purposes, so a deeper reference such
    /// as <c>"board.cpu"</c> (into a nested part inside a container) yields the label <c>"cpu"</c>
    /// rather than discarding it. The connector itself still terminates at the container box's own
    /// port — see the "Cross-boundary limitation" note in the design documentation for what one-level
    /// cross-boundary routing this strategy does and does not perform.
    /// </remarks>
    private static (int Index, EdgePortRef? Port) ResolveEndpoint(
        string? reference,
        Dictionary<string, int> partIndex,
        IReadOnlyList<PartItem> parts)
    {
        if (string.IsNullOrEmpty(reference))
        {
            return (-1, null);
        }

        var dot = reference.IndexOf('.', StringComparison.Ordinal);
        var head = dot >= 0 ? reference[..dot] : reference;
        if (!partIndex.TryGetValue(head, out var index))
        {
            return (-1, null);
        }

        if (dot < 0)
        {
            return (index, null);
        }

        // The first segment after the part names the port; a declared port keeps its own label and look.
        var remainder = reference[(dot + 1)..];
        var next = remainder.IndexOf('.', StringComparison.Ordinal);
        var portName = next >= 0 ? remainder[..next] : remainder;
        var declared = parts[index].Ports.FirstOrDefault(p => p.Name == portName);
        return declared is not null
            ? (index, new EdgePortRef(declared.Label, declared.Appearance, DeclaredKeyPrefix + declared.Name))
            : (index, new EdgePortRef(remainder, UndeclaredPortAppearance, UndeclaredKeyPrefix + remainder));
    }

    /// <summary>
    /// Returns how a connector is drawn according to its kind (SysML v2.0 clauses 8.2.3.13-8.2.3.16): a
    /// <c>binding</c> carries the midpoint label <c>=</c>; an <c>interface</c> the label <c>«interface»</c>
    /// (then its name); a <c>flow</c> a filled arrowhead at the target end and the label <c>«flow»</c> (then
    /// its name and <c>of</c> payload); a <c>connection</c> is a plain line. Role names and multiplicities
    /// declared on the ends become the end labels.
    /// </summary>
    private static ConnectorStyle StyleOf(SysmlConnectionNode conn)
    {
        var source = EndLabelOf(conn.EndpointAName, conn.EndpointAMultiplicity);
        var target = EndLabelOf(conn.EndpointBName, conn.EndpointBMultiplicity);
        return conn.ConnectionKeyword switch
        {
            "binding" => new ConnectorStyle(EndMarkerStyle.None, "=", source, target),
            "interface" => new ConnectorStyle(EndMarkerStyle.None, KeywordLabel("interface", conn.Name, payload: null), source, target),
            "flow" => new ConnectorStyle(EndMarkerStyle.FilledArrow, KeywordLabel("flow", conn.Name, conn.PayloadText), source, target),
            _ => new ConnectorStyle(EndMarkerStyle.None, null, source, target),
        };
    }

    /// <summary>
    /// Returns the end label of a connector end — role name above, multiplicity below — or null when the
    /// model declares neither.
    /// </summary>
    private static LineEndLabel? EndLabelOf(string? roleName, string? multiplicity)
    {
        var role = string.IsNullOrEmpty(roleName) ? null : roleName;
        var count = string.IsNullOrEmpty(multiplicity) ? null : multiplicity;
        return role is null && count is null ? null : new LineEndLabel(role, count);
    }

    /// <summary>
    /// Formats a connector's midpoint label as <c>«keyword» name of Payload</c>, leaving out the name and the
    /// payload when the model declares none.
    /// </summary>
    private static string KeywordLabel(string keyword, string? name, string? payload)
    {
        var label = $"«{keyword}»";
        if (!string.IsNullOrEmpty(name))
        {
            label += " " + name;
        }

        return string.IsNullOrEmpty(payload) ? label : $"{label} of {payload}";
    }

    /// <summary>
    /// Computes the intrinsic size of a nested part box from its already-formatted name line and keyword:
    /// wide enough for both, tall enough for the title area.
    /// </summary>
    private static (double Width, double Height) ComputePartSize(string label, string keyword, Theme theme)
    {
        var labelWidth = (label.Length * theme.FontSizeTitle * CharWidthFactor) + (2.0 * theme.LabelPadding);
        var keywordWidth = ((keyword.Length + 2) * theme.FontSizeBody * CharWidthFactor) + (2.0 * theme.LabelPadding);
        var width = Math.Max(MinPartWidth, Math.Max(labelWidth, keywordWidth));
        var height = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true) + theme.LabelPadding;
        return (width, height);
    }

    /// <summary>
    /// Creates a rounded-rectangle usage box at the given position. A leaf part has no children; a
    /// container part nests its pre-laid-out interior content, translated from the child's local origin
    /// <c>(0, 0)</c> to the box's absolute top-left so the inner part boxes land below the container's title
    /// and inside its border. A reference usage has a dashed outline.
    /// </summary>
    private static LayoutBox MakePartBox(PartItem part, Rect rect, int depth)
    {
        var children = part.InnerContent is null
            ? []
            : TranslateNodes(part.InnerContent, rect.X, rect.Y);

        return new LayoutBox(
            X: rect.X,
            Y: rect.Y,
            Width: rect.Width,
            Height: rect.Height,
            Label: part.Label,
            Depth: depth,
            Shape: BoxShape.RoundedRectangle,
            Compartments: part.Compartments,
            Children: children,
            Keyword: part.Keyword,
            Appearance: part.IsReference ? new BoxAppearance(LineStyle.Dashed) : null);
    }

    /// <summary>
    /// Recursively translates a list of layout nodes by <paramref name="dx"/>/<paramref name="dy"/>,
    /// shifting box positions (and their nested children), port centers, and connector waypoints.
    /// Used to re-anchor a container's interior content from its local origin to absolute coordinates.
    /// </summary>
    private static IReadOnlyList<LayoutNode> TranslateNodes(IReadOnlyList<LayoutNode> nodes, double dx, double dy)
    {
        var result = new List<LayoutNode>(nodes.Count);
        foreach (var node in nodes)
        {
            result.Add(node switch
            {
                LayoutBox box => box with
                {
                    X = box.X + dx,
                    Y = box.Y + dy,
                    Children = TranslateNodes(box.Children, dx, dy),
                },
                LayoutPort port => port with { CentreX = port.CentreX + dx, CentreY = port.CentreY + dy },
                LayoutLine line => line with
                {
                    Waypoints = line.Waypoints.Select(p => new Point2D(p.X + dx, p.Y + dy)).ToList(),
                },
                _ => node,
            });
        }

        return result;
    }
}
