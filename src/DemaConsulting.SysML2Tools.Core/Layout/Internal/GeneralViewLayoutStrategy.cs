// <copyright file="GeneralViewLayoutStrategy.cs" company="DemaConsulting">
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
/// Layout strategy for GeneralView diagrams (SysML v2 graphical notation, clause 8.2.3.6). Renders
/// every user-defined <c>def</c> element (part, port, interface, requirement, action, …) as a
/// keyword-labeled square box and every occurrence-kind usage and port (the Pilot
/// <see cref="NodePolicy"/>) as a keyword-labeled rounded box of its own, groups boxes by their
/// owning package inside folder-shaped containers (a nested package inside its owner's folder, the
/// package name in the folder's tab), and routes the specification's relationship glyphs
/// orthogonally between the boxes: specialization, owner feature membership (filled or hollow
/// diamond), feature typing, subsetting, reference subsetting, redefinition, satisfy, verify and
/// import edges, whichever packages their endpoints sit in.
/// </summary>
/// <remarks>
/// Every definition, usage and package folder is expressed as a single <see cref="LayoutGraph"/> —
/// packages as <see cref="BoxShape.Folder"/>-shaped container nodes, definitions and usages as leaf
/// nodes carrying their <see cref="LayoutGraphNode.Keyword"/> and
/// <see cref="LayoutGraphNode.Compartments"/> — and the whole graph is placed with a single
/// <see cref="LayoutAlgorithmBase.Apply(LayoutGraph)"/> call via <see cref="AutoLayoutAlgorithm"/>
/// flowing top-to-bottom (<see cref="LayoutFlowDirection.Down"/>): every layout edge runs from the
/// element drawn above (owner, general type, subsetted usage) to the element drawn below (owned
/// usage, specific type, typed usage's definition), and the connectors of the edges whose arrow
/// points up are turned back after the layout. Root-scope folders/top-level boxes are classified by connectivity, with connected groups
/// routed through the bundled layered algorithm (<see cref="LayeredLayoutAlgorithm"/>) and
/// disconnected/singleton nodes packed by reading order (<see cref="ContainmentLayoutAlgorithm"/>);
/// each folder's own contents use the same automatic selection. All
/// box sizing (title bands, compartment rows) remains this strategy's responsibility, since the
/// layout stage is theme-agnostic. Standard-library declarations are excluded via
/// <see cref="StdlibFilter"/>. <see cref="RenderOptions.DepthLimit"/> caps nesting depth
/// uniformly regardless of whether the extra nesting comes from a package folder, from a
/// definition containing another definition (nested-definition containment, via
/// <see cref="PlaceDef"/>) or from a usage node owned by a definition (one level below its
/// owner): depth-limited (truncated) content is never added to the graph as
/// individual boxes — a truncated package folder becomes a single leaf node showing an ellipsis
/// (<c>…</c>), and a truncated definition or usage stays a leaf box whose last compartment is a
/// trailing untitled <c>…</c> row (the specification's marker for contents that are not shown).
/// Because the engine places a folder's contents without knowing where
/// the folder's external relationships lead, the items of each package folder are ordered by the
/// horizontal position of those relationships' far ends (see <see cref="ComputeItemOrder"/>), so
/// relationships leaving a folder do not need to cross one another.
/// </remarks>
internal sealed class GeneralViewLayoutStrategy : ILayoutStrategy
{
    /// <summary>Minimum width of a definition box in logical pixels.</summary>
    private const double MinBoxWidth = 130.0;

    /// <summary>Approximate width-per-character factor relative to font size.</summary>
    private const double CharWidthFactor = 0.62;

    /// <summary>
    /// The semantic model's answer for one raw reference: when <see cref="Known"/> is
    /// <see langword="true"/> the answer is authoritative and <see cref="Target"/> is the resolved
    /// target (null when the reference is semantically unresolved, so nothing is drawn); when
    /// <see langword="false"/> the declaration carries no edge identity and the reference is
    /// resolved by name resolution instead.
    /// </summary>
    private readonly record struct EdgeAnswer(bool Known, string? Target)
    {
        /// <summary>Looks up the answer for <paramref name="rawReference"/> on <paramref name="declaration"/>.</summary>
        public static EdgeAnswer For(SysmlNode? declaration, SysmlEdgeKind kind, string rawReference) =>
            new(TryGetResolvedTarget(declaration, kind, rawReference, out var target), target);
    }

    /// <summary>How the General view decides which nested usages become nodes of their own.</summary>
    internal enum UsageNodeMode
    {
        /// <summary>
        /// The OMG SysML v2 Pilot default: every occurrence-kind usage (part, item, port, action,
        /// state, requirement, …) is its own node; attribute usages, enumeration values, plain
        /// reference usages and the other non-occurrence features stay compartment rows.
        /// </summary>
        Pilot,

        /// <summary>Every named usage with a keyword is its own node (except constraint expressions and enumeration values).</summary>
        AllUsages,

        /// <summary>No nested usage is a node: every usage owned by a rendered box is a compartment row and no usage relationship edge is drawn.</summary>
        RowsOnly,
    }

    /// <summary>
    /// The policy deciding which usages nested in a rendered definition or usage are drawn as nodes
    /// of their own (joined to their owner by a composite/reference membership edge) and which stay
    /// compartment rows. Internal on purpose: the default (<see cref="Pilot"/>) reproduces the
    /// SysML v2 Pilot implementation, and the other variants exist so tests (and a later public
    /// option) can flip the decision with a single line.
    /// </summary>
    internal sealed class NodePolicy
    {
        /// <summary>The usage keywords the Pilot implementation draws as separate nodes (occurrence-kind usages).</summary>
        private static readonly HashSet<string> PilotNodeKeywords = new(StringComparer.Ordinal)
        {
            "part", "item", "occurrence", "action", "state", "requirement", "concern",
            "analysis", "verification", "use case", "case",
        };

        /// <summary>The keyword of a port usage, a node under <see cref="UsageNodeMode.Pilot"/> only when it has nested content.</summary>
        private const string PortKeyword = "port";

        /// <summary>The usage keywords that never become a node under <see cref="UsageNodeMode.AllUsages"/>.</summary>
        private static readonly HashSet<string> NeverNodeKeywords = new(StringComparer.Ordinal)
        {
            "constraint", "assume constraint", "require constraint", "enum value",
        };

        /// <summary>Initializes a policy of the given mode.</summary>
        private NodePolicy(UsageNodeMode mode)
        {
            Mode = mode;
        }

        /// <summary>Gets the Pilot policy (the default): occurrence-kind usages and ports with nested content are nodes.</summary>
        public static NodePolicy Pilot { get; } = new(UsageNodeMode.Pilot);

        /// <summary>Gets the policy that draws every named usage as a node.</summary>
        public static NodePolicy AllUsages { get; } = new(UsageNodeMode.AllUsages);

        /// <summary>Gets the policy that draws no nested usage as a node.</summary>
        public static NodePolicy RowsOnly { get; } = new(UsageNodeMode.RowsOnly);

        /// <summary>Gets the policy's mode.</summary>
        public UsageNodeMode Mode { get; }

        /// <summary>
        /// Returns whether <paramref name="usage"/>, nested in a rendered box, is drawn as a node of
        /// its own. The decision is by usage keyword only; whether the usage is a reference usage
        /// changes its look (dashed outline, hollow diamond) but never whether it is a node.
        /// </summary>
        /// <param name="usage">The nested usage.</param>
        /// <returns><see langword="true"/> for a node; <see langword="false"/> for a compartment row.</returns>
        public bool IsNode(SysmlFeatureNode usage)
        {
            ArgumentNullException.ThrowIfNull(usage);
            if (Mode == UsageNodeMode.RowsOnly ||
                string.IsNullOrEmpty(usage.Name) ||
                string.IsNullOrEmpty(usage.FeatureKeyword) ||
                usage.ExpressionText is { Length: > 0 })
            {
                return false;
            }

            if (Mode == UsageNodeMode.AllUsages)
            {
                return !NeverNodeKeywords.Contains(usage.FeatureKeyword);
            }

            // A plain port (no nested features) is a compartment row of its owner, like an
            // attribute; a port that owns nested content (features, ports, parts, ...) is a node
            // of its own so that the content has somewhere to be drawn.
            return usage.FeatureKeyword == PortKeyword
                ? HasNestedContent(usage)
                : PilotNodeKeywords.Contains(usage.FeatureKeyword);
        }

        /// <summary>Returns whether <paramref name="usage"/> owns at least one named nested feature.</summary>
        private static bool HasNestedContent(SysmlFeatureNode usage) =>
            usage.Children.Any(child => child is SysmlFeatureNode { Name: { Length: > 0 } });
    }

    /// <summary>The policy deciding which nested usages are nodes.</summary>
    private readonly NodePolicy _policy;

    /// <summary>Initializes the strategy with the default (Pilot) node policy.</summary>
    internal GeneralViewLayoutStrategy()
        : this(NodePolicy.Pilot)
    {
    }

    /// <summary>Initializes the strategy with an explicit node policy (used by tests to flip the decision).</summary>
    /// <param name="policy">The policy deciding which nested usages are nodes.</param>
    internal GeneralViewLayoutStrategy(NodePolicy policy)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    /// <summary>
    /// The classification of an edge, which selects its line style so the renderer can distinguish
    /// the relationships the General view draws between its boxes.
    /// </summary>
    private enum EdgeKind
    {
        /// <summary>Subtype → supertype specialization: solid line, hollow triangle at the supertype.</summary>
        Specialization,

        /// <summary>
        /// Owned usage → owner feature membership: solid line, filled diamond (composite) or hollow
        /// diamond (referential) at the owner.
        /// </summary>
        Membership,

        /// <summary>Usage → its definition (feature typing): solid line, hollow triangle with two dots at the definition.</summary>
        FeatureTyping,

        /// <summary>Usage → the usage it redefines: solid line, hollow triangle with crossbar at the redefined usage.</summary>
        Redefinition,

        /// <summary>Usage → the usage it subsets: solid line, hollow triangle at the subsetted usage.</summary>
        Subsetting,

        /// <summary>Usage → the usage it reference-subsets (<c>::&gt;</c>): solid line, hollow triangle with four dots at the referenced usage.</summary>
        ReferenceSubsetting,

        /// <summary>A <c>satisfy</c> relationship, satisfier → requirement: solid line, open arrowhead, <c>«satisfy»</c> label.</summary>
        Satisfy,

        /// <summary>A <c>verify</c> relationship, verifier → requirement: solid line, open arrowhead, <c>«verify»</c> label.</summary>
        Verify,

        /// <summary>An <c>import</c>, importing namespace → imported namespace: dashed line, open arrowhead, <c>«import»</c> label.</summary>
        Import,

        /// <summary>
        /// A resolved <c>connect A to B</c>/message reference between two definitions (each
        /// endpoint's owning box resolved via <see cref="ResolveOwningBox"/>): solid line, no
        /// end marker.
        /// </summary>
        Connect,

        /// <summary>
        /// A resolved <c>allocate A to B</c> reference: solid line, open chevron at the target,
        /// with a <c>«allocate»</c> midpoint label.
        /// </summary>
        Allocate,

        /// <summary>
        /// A resolved standalone <c>dependency</c> reference: dashed line, open chevron at the
        /// depended-upon element.
        /// </summary>
        Dependency,

        /// <summary>
        /// A resolved <c>bind A = B</c> binding connector reference (each endpoint's owning box
        /// resolved via <see cref="ResolveOwningBox"/>): solid line, no end marker, with a
        /// <c>=</c> midpoint label distinguishing it from <see cref="Connect"/>.
        /// </summary>
        Binding,
    }

    /// <summary>
    /// A model edge between two boxes, expressed by qualified name so it can be resolved into
    /// the correct graph scope once every box's node (or absence, if depth-truncated) is known.
    /// A package folder endpoint is keyed <c>folder:&lt;package qualified name&gt;</c>.
    /// </summary>
    /// <param name="SourceQualified">Qualified name of the source box (the dependent, specific or owned element).</param>
    /// <param name="TargetQualified">Qualified name of the target box (the general, owning or depended-upon element).</param>
    /// <param name="Arrowhead">Arrowhead drawn at the target end.</param>
    /// <param name="Kind">The edge classification, which selects the rendered line style.</param>
    /// <param name="Label">
    /// Optional midpoint label (e.g. <c>«allocate»</c>, <c>=</c>), or <see langword="null"/> for
    /// the majority of kinds that render no label.
    /// </param>
    private sealed record ModelEdge(
        string SourceQualified,
        string TargetQualified,
        EndMarkerStyle Arrowhead,
        EdgeKind Kind,
        string? Label = null);

    /// <summary>
    /// Maps an edge kind to its rendered line style: dependency and import edges are dashed, all
    /// others (including allocate, which the specification draws as a solid line) are solid.
    /// </summary>
    private static LineStyle LineStyleForKind(EdgeKind kind) =>
        kind is EdgeKind.Dependency or EdgeKind.Import
            ? LineStyle.Dashed
            : LineStyle.Solid;

    /// <summary>
    /// The prefix of a <see cref="ModelEdge"/> endpoint key that names a package folder rather than
    /// a definition or usage box.
    /// </summary>
    private const string FolderKeyPrefix = "folder:";

    /// <summary>
    /// The name style of an abstract definition: bold italic (the specification, Table 4, sets the
    /// name of an abstract definition in italics; the engine's default name style is bold). The
    /// name of an abstract <em>usage</em> is not italic in the specification and stays default.
    /// </summary>
    private static readonly TextStyle AbstractDefinitionNameStyle = TextStyle.BoldItalic;

    /// <summary>A user-defined definition or named usage together with its computed box size and supertypes.</summary>
    /// <param name="QualifiedName">The definition or usage's fully qualified name.</param>
    /// <param name="SimpleName">The definition or usage's simple (unqualified) name; the box's identity for grouping, ordering, and edges.</param>
    /// <param name="Label">
    /// The displayed name line (see <see cref="FormatNameLine"/>): short name, name, and — for a
    /// definition — its <c>:&gt;</c> specialization, for a usage its typing, multiplicity and
    /// subsetting/redefinition, as written in the model.
    /// </param>
    /// <param name="Keyword">
    /// The definition or usage keyword including its prefix words (e.g. <c>part def</c>,
    /// <c>abstract part def</c>, <c>ref part</c>); the engine adds the guillemet marks.
    /// </param>
    /// <param name="SupertypeNames">The declared specialization targets of a definition (empty for a usage).</param>
    /// <param name="Compartments">The computed compartments to render inside the box (filled in by <see cref="FinalizeBoxes"/>).</param>
    /// <param name="Width">The computed box width.</param>
    /// <param name="Height">The computed box height.</param>
    /// <param name="Annotations">The element's <c>comment</c> annotations, drawn as an attached note.</param>
    /// <param name="Declaration">The definition or usage declaration the box represents.</param>
    /// <param name="DocVisible">Whether the element's documentation is exposed and drawn as a <c>doc</c> compartment.</param>
    /// <param name="Appearance">The box's outline/name styling, or <see langword="null"/> for the default look.</param>
    /// <param name="OwnerQualifiedName">
    /// The qualified name of the rendered box that owns this usage (set by
    /// <see cref="RemoveRowUsages"/>), or <see langword="null"/> for a definition and for a usage
    /// whose owner is not rendered.
    /// </param>
    private sealed record DefBox(
        string QualifiedName,
        string SimpleName,
        string Label,
        string Keyword,
        IReadOnlyList<string> SupertypeNames,
        IReadOnlyList<LayoutCompartment> Compartments,
        double Width,
        double Height,
        IReadOnlyList<SysmlAnnotation> Annotations,
        SysmlNode Declaration,
        bool DocVisible,
        BoxAppearance? Appearance = null,
        string? OwnerQualifiedName = null)
    {
        /// <summary>Gets the usage declaration this box represents, or <see langword="null"/> for a definition.</summary>
        public SysmlFeatureNode? Feature => Declaration as SysmlFeatureNode;

        /// <summary>Gets a value indicating whether this box represents a usage rather than a definition.</summary>
        public bool IsUsage => Declaration is SysmlFeatureNode;
    }

    /// <summary>
    /// One container scope of the layout graph: the root graph, a package folder's own
    /// <see cref="LayoutGraphNode.Children"/> graph, or a definition-as-container's own
    /// <see cref="LayoutGraphNode.Children"/> graph. The scopes form a tree through
    /// <see cref="Parent"/> (the root scope's parent is <see langword="null"/>), which is what
    /// <see cref="LowestCommonScope"/> walks to find the scope an edge belongs to. Scopes are compared
    /// by reference identity.
    /// </summary>
    /// <param name="parent">The enclosing scope, or <see langword="null"/> for the root scope.</param>
    /// <param name="graph">The layout graph holding this scope's own nodes (and the edges declared in it).</param>
    /// <param name="owner">
    /// The container node whose <see cref="LayoutGraphNode.Children"/> graph this scope is, or
    /// <see langword="null"/> for the root scope. The external-edge anchor lookup uses it to find
    /// which direct member of a common scope contains a given box.
    /// </param>
    private sealed class ScopeInfo(ScopeInfo? parent, LayoutGraph graph, LayoutGraphNode? owner = null)
    {
        /// <summary>Gets the enclosing scope, or <see langword="null"/> for the root scope.</summary>
        public ScopeInfo? Parent { get; } = parent;

        /// <summary>Gets the layout graph holding this scope's own nodes.</summary>
        public LayoutGraph Graph { get; } = graph;

        /// <summary>Gets the container node that owns this scope's graph, or <see langword="null"/> for the root scope.</summary>
        public LayoutGraphNode? Owner { get; } = owner;
    }

    /// <summary>Where a located definition's node lives: the node itself and the scope that directly holds it.</summary>
    private readonly record struct Location(LayoutGraphNode Node, ScopeInfo Scope);

    /// <summary>
    /// The result of one <see cref="BuildGraph"/> pass: the input graph together with the
    /// bookkeeping the second, order-refining pass needs (<see cref="ComputeItemOrder"/>).
    /// </summary>
    /// <param name="Graph">The root layout graph, ready to be handed to the layout engine.</param>
    /// <param name="Truncated">The depth-truncated containers to decorate once the layout is known.</param>
    /// <param name="Located">Every definition's node and directly holding scope, keyed by qualified name.</param>
    /// <param name="Plans">The planned package folders, keyed by package qualified name.</param>
    /// <param name="UsagesByOwner">The usage nodes placed beside their owner, keyed by the owner's qualified name.</param>
    private sealed record GraphBuild(
        LayoutGraph Graph,
        List<TruncatedContainer> Truncated,
        IReadOnlyDictionary<string, Location> Located,
        IReadOnlyDictionary<string, FolderPlan> Plans,
        IReadOnlyDictionary<string, List<DefBox>> UsagesByOwner);

    /// <summary>
    /// The planned shape of one package folder, computed before any graph node is created so the
    /// folders can then be materialized parent-first.
    /// </summary>
    /// <param name="key">The folder's package qualified name.</param>
    /// <param name="parentKey">
    /// The qualified name of the nearest enclosing folder, or <see langword="null"/> when the folder
    /// sits directly on the root scope.
    /// </param>
    /// <param name="items">The definitions owned directly by the package (empty for a pure enclosing folder).</param>
    private sealed class FolderPlan(string key, string? parentKey, IReadOnlyList<DefBox> items)
    {
        /// <summary>Gets the folder's package qualified name.</summary>
        public string Key { get; } = key;

        /// <summary>Gets the nearest enclosing folder's key, or <see langword="null"/> for a root-level folder.</summary>
        public string? ParentKey { get; } = parentKey;

        /// <summary>Gets the definitions owned directly by the package.</summary>
        public IReadOnlyList<DefBox> Items { get; } = items;

        /// <summary>Gets the keys of the folders nested directly inside this one, in drawing order.</summary>
        public List<string> ChildKeys { get; } = [];

        /// <summary>Gets or sets the number of enclosing folders (0 for a root-level folder).</summary>
        public int NestingLevel { get; set; }

        /// <summary>Gets or sets the number of hidden elements reported when this folder is truncated.</summary>
        public int SubtreeCount { get; set; }
    }

    /// <summary>
    /// A package folder whose contents were depth-truncated: replaced by a leaf placeholder folder
    /// node, decorated with an ellipsis (<c>…</c>) label once the layout places it. Covers
    /// <see cref="BuildGraph"/>'s package-folder-contents truncation; a truncated nested definition
    /// needs no entry because it stays an ordinary leaf box carrying its own trailing <c>…</c>
    /// compartment row (see <see cref="WithTruncationRow"/>), so <see cref="RenderOptions.DepthLimit"/>
    /// still caps nesting depth uniformly regardless of whether the extra nesting level comes from a
    /// package folder or from a definition containing another definition.
    /// </summary>
    /// <param name="Node">The leaf graph node standing in for the truncated folder.</param>
    /// <param name="HiddenCount">
    /// Number of hidden definitions and nested packages. Retained for diagnostics; the rendered
    /// marker is the bare <c>…</c> glyph and does not report the count.
    /// </param>
    /// <param name="TitleOffset">
    /// Vertical offset, in logical pixels, from the placed leaf node's own top edge down to where
    /// the ellipsis label should be drawn. A truncated package folder's placeholder node carries its
    /// own title band (it stands in for the whole folder, not just its contents), so its label sits
    /// below that reserved band.
    /// </param>
    private sealed record TruncatedContainer(LayoutGraphNode Node, int HiddenCount, double TitleOffset);

    /// <summary>
    /// A <see cref="EdgeKind.Connect"/>/<see cref="EdgeKind.Allocate"/>/
    /// <see cref="EdgeKind.Dependency"/>/<see cref="EdgeKind.Binding"/> edge from
    /// <c>workspace.Index.AllEdges</c> that was not rendered because an endpoint failed to resolve
    /// to a rendered box, or because both endpoints resolved to the same box (a genuine self-loop).
    /// Reported via <see cref="LayoutWarnings.ForDroppedRelationshipEdges"/> so the drop is visible
    /// instead of silent.
    /// </summary>
    /// <param name="Kind">The short edge-kind label (e.g. <c>"Connect"</c>).</param>
    /// <param name="Source">The edge's raw source qualified/dotted-chain-resolved reference.</param>
    /// <param name="Target">The edge's raw target qualified/dotted-chain-resolved reference.</param>
    /// <param name="Reason">A short human-readable explanation of why the edge was dropped.</param>
    private sealed record DroppedRelationshipEdge(string Kind, string Source, string Target, string Reason);

    /// <inheritdoc/>
    public LayoutTree BuildLayout(ViewContext context, RenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        var theme = options.Theme;

        // Resolve the view's exposed-name scope (the union of each resolved Expose edge's
        // containment subtree), or null when the view has no resolved Expose edges — including a
        // null ViewNode (the --auto synthetic-view path), a view with no `expose` statement, and a
        // view whose every `expose` entry failed to resolve. A null scope means "render
        // everything", byte-identical to the pre-scoping behavior. RenderTargetName and
        // FilterExpressionText never affect this decision.
        var scope = ExposeScopeResolver.ResolveExposedScope(context.Workspace, context.ViewNode);

        // The view conditions (SysML 8.3.26.11 viewCondition): every `filter` the view owns plus
        // those inherited from its view definition (SysML 7.26.2). An exposed element is rendered
        // only when it satisfies all of them. A condition that fails to parse (or uses a construct
        // outside the supported subset) is skipped with a warning rather than dropped silently.
        var conditions = new List<FilterExpression>();
        var conditionWarnings = new List<string>();
        foreach (var conditionText in ExposeScopeResolver.CollectViewConditions(context.Workspace, context.ViewNode))
        {
            var parseResult = FilterExpressionParser.Parse(conditionText);
            if (parseResult.Expression is { } expression)
            {
                conditions.Add(expression);
            }
            else
            {
                conditionWarnings.AddRange(LayoutWarnings.ForUnevaluatedFilter(
                    context.ViewName, conditionText, parseResult.Diagnostics.FirstOrDefault()?.Message));
            }
        }

        // An owned `doc`/`comment` of a rendered element is itself an exposed element when the
        // expose that exposed its owner also exposes the owner's members (KerML 7.2.4.2:
        // documentation is an owned member of the documented element), and it is drawn only when
        // it also satisfies every view condition (e.g. `filter not @SysML::Documentation;`).
        bool AnnotationVisible(string ownerQualifiedName, string metaclass) =>
            ExposeScopeResolver.IsAnnotationExposed(context.Workspace, ownerQualifiedName, metaclass, scope) &&
            conditions.TrueForAll(condition => FilterExpressionEvaluator.EvaluateForMetaclass(context.Workspace, metaclass, condition));

        // Collect all user-defined definitions, sized for rendering, restricted to the resolved
        // scope when one applies.
        var defs = CollectDefinitions(context.Workspace, scope, AnnotationVisible);

        // Packages that are themselves exposed (SysML 8.3.26.3: `expose P;` and `expose P::**;`
        // expose the Package element P) are drawn as package nodes (SysML 8.2.3.5 package-node),
        // containing whichever of their members are also exposed — an empty package node when
        // none are. Packages that are only ancestors of exposed elements are not drawn.
        var exposedPackages = CollectExposedPackages(context.Workspace, scope);
        if (defs.Count == 0 && exposedPackages.Count == 0)
        {
            return new LayoutTree(200.0, 100.0, []) { Warnings = conditionWarnings };
        }

        // Narrow `defs` and the exposed packages to the elements satisfying every view condition.
        foreach (var condition in conditions)
        {
            var matched = FilterExpressionEvaluator.Evaluate(
                context.Workspace,
                defs.Select(d => d.QualifiedName).Concat(exposedPackages).ToList(),
                condition).MatchedQualifiedNames;
            var matchedSet = new HashSet<string>(matched, StringComparer.Ordinal);
            defs = defs.Where(d => matchedSet.Contains(d.QualifiedName)).ToList();
            exposedPackages.IntersectWith(matchedSet);
        }

        if (defs.Count == 0 && exposedPackages.Count == 0)
        {
            return new LayoutTree(200.0, 100.0, []) { Warnings = conditionWarnings };
        }

        // Drop every usage that is shown as a compartment row of its nearest still-rendered owner
        // instead of as a node of its own (see RemoveRowUsages, which processes boxes
        // shallowest-first so that a usage excluded earlier in the same pass is treated as absent
        // for its own children's test — cascading correctly through any nesting depth instead of
        // silently dropping a deeper-nested usage), and record the owner of every usage that stays.
        // This must run after the standalone filter narrowing above (not before it and not inside
        // CollectDefinitions), so that a usage whose parent was excluded by the filter — rather
        // than by scope — is correctly kept as its own standalone box instead of being wrongly
        // dropped by an earlier-run dedup pass that saw the parent still present in a pre-filter set.
        defs = RemoveRowUsages(defs, _policy);
        if (defs.Count == 0 && exposedPackages.Count == 0)
        {
            return new LayoutTree(200.0, 100.0, []);
        }

        // Now that the final set of boxes is known, build every box's compartments (usages that are
        // nodes are not rows) and size it.
        defs = FinalizeBoxes(defs, theme);

        // Partition the admitted boxes into three disjoint sets before grouping by package:
        // definitions whose immediate parent (the qualified-name prefix before the last "::") is
        // itself an admitted box are diverted into childrenByParent, so they are nested recursively
        // inside their owning box instead of being handed to GroupByPackage — which would otherwise
        // (incorrectly) bucket them under their parent's own qualified name and render that parent
        // a second time as a duplicate sibling folder (see PlaceDef); usages with a rendered owner
        // are diverted into usagesByOwner, to be placed beside their owner in the owner's own scope;
        // everything else (a bare top-level item, or an item whose immediate parent is a bare
        // package) flows into GroupByPackage.
        var defByQualified = defs.ToDictionary(d => d.QualifiedName, StringComparer.Ordinal);
        var childrenByParent = new Dictionary<string, List<DefBox>>(StringComparer.Ordinal);
        var usagesByOwner = new Dictionary<string, List<DefBox>>(StringComparer.Ordinal);
        var packageOrRootItems = new List<DefBox>();

        foreach (var def in defs)
        {
            if (def.OwnerQualifiedName is { } owner)
            {
                AddTo(usagesByOwner, owner, def);
                continue;
            }

            var sep = def.QualifiedName.LastIndexOf("::", StringComparison.Ordinal);
            var immediateParent = sep >= 0 ? def.QualifiedName[..sep] : string.Empty;

            if (immediateParent.Length > 0 && defByQualified.ContainsKey(immediateParent))
            {
                AddTo(childrenByParent, immediateParent, def);
            }
            else
            {
                packageOrRootItems.Add(def);
            }
        }

        // Group the remaining (non-nested) items by their owning package (prefix before the last
        // "::"; for a usage whose owner is not rendered, the nearest enclosing package — never the
        // owner's own qualified name, which would draw a folder for a definition or usage).
        var groups = GroupByPackage(packageOrRootItems, context.Workspace);

        // An exposed package none of whose members are exposed still gets its (empty) package node.
        var groupedPackages = groups.Select(g => g.Package).ToHashSet(StringComparer.Ordinal);
        groups = groups
            .Concat(exposedPackages
                .Where(package => !groupedPackages.Contains(package))
                .OrderBy(package => package, StringComparer.Ordinal)
                .Select(package => (Package: package, Items: new List<DefBox>())))
            .ToList();

        // Resolve the specialization/membership/typing/subsetting/redefinition/satisfy/verify/import
        // edge set by qualified name; the graph-construction pass below drops any edge touching a
        // box that never received a node (because its folder or owner was depth-truncated).
        var (modelEdges, droppedEdges) = BuildModelEdges(defs, context.Workspace, scope is not null, _policy);

        // Build the single input graph: package folders as containers (nested packages inside their
        // owner's folder), definitions and usages as leaves.
        var packageNames = CollectPackageNames(context.Workspace);
        var exposed = scope is not null ? exposedPackages : null;
        var build = BuildGraph(groups, childrenByParent, usagesByOwner, modelEdges, theme, options.DepthLimit, exposed, packageNames, itemOrder: null);

        // Lay out the whole graph in one call, delegating root-scope algorithm selection to "auto":
        // it classifies the root's own folders/top-level definitions by connectivity, routing
        // connected groups through the layered algorithm and packing disconnected/singleton nodes by
        // reading order (containment) — the same per-container-algorithm convention this strategy
        // already applies per folder, just resolved automatically at the root instead of being pinned
        // to a single fixed algorithm for every root-scope node regardless of its own connectivity.
        var tree = LayOut(build.Graph);

        // The engine lays out each package folder's contents without knowing where the folder's
        // relationships to the outside lead, and stacks unconnected groups of those contents in the
        // order they were added - so declaration order alone can force relationships that leave a
        // folder to cross one another. Refine once: reorder each folder's items by the horizontal
        // position (in the first layout) of the boxes their external relationships lead to, and lay
        // out again with that order. When no folder's order changes the first layout is kept as is.
        // NOTE: this compensates for the engine's SeparateChildren placement model and can be retired
        // when the engine orders a container's interior by its cross-hierarchy edges.
        var itemOrder = ComputeItemOrder(tree, build, modelEdges);
        if (itemOrder.Count > 0)
        {
            build = BuildGraph(groups, childrenByParent, usagesByOwner, modelEdges, theme, options.DepthLimit, exposed, packageNames, itemOrder);
            tree = LayOut(build.Graph);
        }

        // Stamp the ellipsis ("…") label onto each truncated package folder's placed box. Every placed
        // box is paired with its graph node by identity
        // (label, keyword, shape) within its own scope rather than by position, so the pairing
        // does not depend on the order in which the engine emits a scope's boxes; this recurses
        // through every nesting depth, not just the root.
        var placed = build.Truncated.Count == 0 ? tree : DecorateTruncated(tree, build.Graph, build.Truncated, theme);

        // The engine packs the definitions that nothing refers to into one shelf beside the connected
        // drawing, which in a wide tree leaves a large empty corner. Lay the connected part out on its
        // own and place those definitions in the free space instead, when they fit and the canvas gets
        // smaller (see TryTuckUnconnected); otherwise the engine's own packing above is kept.
        var tucked = TryTuckUnconnected(
            tree,
            build,
            deferred => BuildGraph(groups, childrenByParent, usagesByOwner, modelEdges, theme, options.DepthLimit, exposed, packageNames, itemOrder.Count > 0 ? itemOrder : null, deferred),
            theme);
        placed = tucked ?? placed;

        // The engine routes a connector between two root-level folders around the outside of the
        // drawing and does not grow the canvas for it (engine defect, see FitConnectorsToCanvas), so
        // the canvas is fitted to the connectors here.
        placed = FitConnectorsToCanvas(placed);
        placed = OrientEdges(placed);
        placed = ClearEndDecorations(placed, theme);
        // Surface any standalone `filter [<expr>];` evaluation failure, plus a distinct warning
        // for each `expose <path>::**[<expr>]` bracket filter that failed to parse or evaluate
        // (see ExposeScopeResolver.ResolveExposedScope's ExposedScope.Failures) — a
        // successfully-evaluated bracket filter already narrowed `scope` above and needs no
        // warning — through the standard layout-warnings channel.
        var warnings = conditionWarnings
            .Concat(LayoutWarnings.ForUnevaluatedExposeBracketFilter(context.ViewName, scope?.Failures ?? []))
            .Concat(LayoutWarnings.ForDroppedRelationshipEdges(context.ViewName, droppedEdges
                .Select(d => (d.Kind, d.Source, d.Target, d.Reason))
                .ToList()))
            .ToList();
        return warnings.Count == 0 ? placed : placed with { Warnings = warnings };
    }

    /// <summary>
    /// Returns the qualified names of the non-stdlib packages that are themselves members of the
    /// view's exposed scope (SysML 8.3.26.3/8.3.26.4: a MembershipExpose <c>expose P;</c> or
    /// <c>expose P::**;</c> exposes the Package P; a NamespaceExpose <c>expose P::*;</c> exposes only
    /// P's members). Empty when <paramref name="scope"/> is null (an unscoped view keeps drawing every
    /// package that contains definitions, as before).
    /// </summary>
    private static HashSet<string> CollectExposedPackages(SysmlWorkspace workspace, ExposedScope? scope)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (scope is null)
        {
            return result;
        }

        foreach (var (qualifiedName, declaration) in workspace.Declarations)
        {
            if (declaration is SysmlPackageNode &&
                !StdlibFilter.IsStdlibElement(qualifiedName, workspace.StdlibNames) &&
                ExposeScopeResolver.IsInSubjectScope(qualifiedName, scope))
            {
                result.Add(qualifiedName);
            }
        }

        return result;
    }

    /// <summary>
    /// Returns the qualified names of every non-stdlib package declared in the workspace, whether or
    /// not the view exposes it. <see cref="BuildGraph"/> uses it to recognize which qualified-name
    /// prefixes are packages, so an unscoped view can draw an enclosing package's folder even when
    /// the package owns no definitions directly.
    /// </summary>
    private static HashSet<string> CollectPackageNames(SysmlWorkspace workspace)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (qualifiedName, declaration) in workspace.Declarations)
        {
            if (declaration is SysmlPackageNode &&
                !StdlibFilter.IsStdlibElement(qualifiedName, workspace.StdlibNames))
            {
                result.Add(qualifiedName);
            }
        }

        return result;
    }

    /// <summary>
    /// Collects every user-defined <see cref="SysmlDefinitionNode"/> (and every named
    /// <see cref="SysmlFeatureNode"/> usage, at any nesting depth) from the workspace and computes
    /// each box's intrinsic size from its keyword and name, restricted to <paramref name="scope"/>
    /// when non-null (the view's resolved <c>expose</c> containment subtrees).
    /// </summary>
    /// <remarks>
    /// This method implements the first of a two-stage admit-then-dedup design (Phase 2d, see
    /// <c>ROADMAP.md</c>'s "View <c>filter [&lt;expr&gt;];</c> expression evaluation" section):
    /// usage-level (<see cref="SysmlFeatureNode"/>) candidates are admitted here alongside
    /// definitions, unconditionally of nesting depth, because this method also doubles as the
    /// filter-candidate source — a metaclass-kind classification test like
    /// <c>filter @SysML::PartUsage;</c> is inherently about usages, not definitions, and every
    /// admitted usage must be present here for such a filter to have anything meaningful to
    /// narrow. This superset necessarily includes usages nested inside an already-admitted
    /// definition/usage, which would duplicate that parent's compartment row if rendered as its own
    /// box; the second stage, <see cref="RemoveRowUsages"/>, runs later in
    /// <see cref="BuildLayout"/> — after the standalone <c>filter [&lt;expr&gt;];</c> narrowing —
    /// and removes exactly those nested usages whose nearest still-rendered ancestor is present in
    /// the final, fully scope-and-filter-narrowed set (cascading through any nesting depth so a
    /// deeper-nested usage is never silently dropped merely because an intermediate ancestor was
    /// itself excluded). An unnamed <see cref="SysmlFeatureNode"/> usage (<see cref="SysmlNode.Name"/>
    /// is <see langword="null"/>) is excluded, since it has no stable qualified name to key a box on;
    /// an unnamed <see cref="SysmlDefinitionNode"/>, by contrast, is still admitted — it falls back to
    /// keying its box on the declaration's own qualified name, exactly as it did before usage-level
    /// candidates were introduced.
    /// </remarks>
    /// <seealso cref="RemoveRowUsages"/>
    private static IReadOnlyList<DefBox> CollectDefinitions(
        SysmlWorkspace workspace,
        ExposedScope? scope,
        Func<string, string, bool> annotationVisible)
    {
        var result = new List<DefBox>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // Visits one declaration (and, recursively, the usages nested in its Children): a real
        // workspace registers every nested declaration under its own qualified name in
        // Declarations, while a hand-built one may list nested usages only as Children, so both
        // sources are walked and each qualified name is admitted once.
        void Visit(string qualifiedName, SysmlNode declaration)
        {
            if (declaration is not (SysmlDefinitionNode or SysmlFeatureNode) ||
                (declaration is SysmlFeatureNode && declaration.Name is null) ||
                !seen.Add(qualifiedName))
            {
                return;
            }

            if (!StdlibFilter.IsStdlibElement(qualifiedName, workspace.StdlibNames) &&
                (scope is null || ExposeScopeResolver.IsInSubjectScope(qualifiedName, scope)))
            {
                result.Add(CreateBox(qualifiedName, declaration, annotationVisible));
            }

            foreach (var child in declaration.Children)
            {
                if (child is SysmlFeatureNode { Name: { } childName })
                {
                    Visit(child.QualifiedName ?? $"{qualifiedName}::{childName}", child);
                }
            }
        }

        foreach (var (qualifiedName, declaration) in workspace.Declarations)
        {
            Visit(qualifiedName, declaration);
        }

        return result;
    }

    /// <summary>
    /// Creates the (not yet sized) box of one definition or usage: its keyword with prefix words,
    /// name line, appearance, visible comments and documentation flag. The compartments and size are
    /// filled in by <see cref="FinalizeBoxes"/> once the final set of boxes is known.
    /// </summary>
    /// <param name="qualifiedName">The declaration's qualified name (the box's key).</param>
    /// <param name="declaration">The definition or named usage.</param>
    /// <param name="annotationVisible">Whether an owner's annotation of a metaclass is exposed and unfiltered.</param>
    /// <returns>The box.</returns>
    private static DefBox CreateBox(
        string qualifiedName,
        SysmlNode declaration,
        Func<string, string, bool> annotationVisible)
    {
        var simpleName = declaration.Name ?? qualifiedName;

        // Specialization edges (in BuildModelEdges) are drawn from DefBox.SupertypeNames. On a
        // SysmlFeatureNode, SupertypeNames is populated only from a usage-level `subsets`/`:>` clause
        // (see SysmlNode.SupertypeNames's remarks) — subsetting, not specialization — so it is not
        // surfaced here as a specialization target; only definition-level SupertypeNames
        // (`part def X :> Y`) are genuine specialization targets.
        var specializationSupertypeNames = declaration is SysmlDefinitionNode
            ? declaration.SupertypeNames
            : Array.Empty<string>();

        string keyword;
        string label;
        BoxAppearance? appearance = null;
        if (declaration is SysmlFeatureNode feature)
        {
            keyword = UsageKeyword(feature);
            label = FormatUsageNameLine(feature, simpleName);

            // A reference usage (not composite) is drawn with a dashed outline (SysML v2 graphical
            // BNF, clause 8.2.3.6: part-ref).
            if (feature.IsReference)
            {
                appearance = new BoxAppearance(LineStyle.Dashed);
            }
        }
        else
        {
            var definition = (SysmlDefinitionNode)declaration;
            keyword = DefinitionKeyword(definition);

            // The box's name line follows the specification's textual notation: the short name (if
            // any) before the name, then the specialization (:>) of the definition.
            label = FormatNameLine(declaration.ShortName, simpleName, specializationSupertypeNames);
            if (definition.IsAbstract)
            {
                appearance = new BoxAppearance(NameStyle: AbstractDefinitionNameStyle);
            }
        }

        // Exposed, unfiltered explicit comments are drawn as notes attached to their owner
        // (SysML 8.2.3.4 comment-node with annotation-link); documentation never is.
        var comments = annotationVisible(qualifiedName, "Comment")
            ? declaration.Annotations.Where(a => a.Kind == SysmlAnnotationKind.Comment).ToList()
            : new List<SysmlAnnotation>();

        // Exposed, unfiltered documentation is drawn as a "doc" compartment inside its owner's box
        // (SysML 8.2.3.4 documentation-compartment).
        var docVisible = annotationVisible(qualifiedName, "Documentation");
        return new DefBox(
            qualifiedName,
            simpleName,
            label,
            keyword,
            specializationSupertypeNames,
            [],
            MinBoxWidth,
            0.0,
            comments,
            declaration,
            docVisible,
            appearance);
    }

    /// <summary>
    /// Returns the keyword text of a definition, with the prefix words the specification places
    /// inside the guillemet marks (<c>abstract part def</c>, <c>variation part def</c>,
    /// <c>individual part def</c>); the engine adds the guillemet marks themselves.
    /// </summary>
    private static string DefinitionKeyword(SysmlDefinitionNode definition)
    {
        var words = new List<string>();
        if (definition.IsAbstract)
        {
            words.Add("abstract");
        }

        if (definition.IsVariation)
        {
            words.Add("variation");
        }

        if (definition.IsIndividual)
        {
            words.Add("individual");
        }

        words.Add(string.IsNullOrEmpty(definition.DefinitionKeyword) ? "def" : definition.DefinitionKeyword);
        return string.Join(' ', words);
    }

    /// <summary>
    /// Returns the keyword text of a usage, with the prefix words the specification places inside
    /// the guillemet marks (<c>abstract part</c>, <c>variation part</c>, <c>individual part</c>,
    /// <c>variant part</c>, <c>ref part</c>); a plain <c>ref</c> usage is not prefixed twice.
    /// </summary>
    private static string UsageKeyword(SysmlFeatureNode feature)
    {
        var words = new List<string>();
        if (feature.IsAbstract)
        {
            words.Add("abstract");
        }

        if (feature.IsVariation)
        {
            words.Add("variation");
        }

        if (feature.IsIndividual)
        {
            words.Add("individual");
        }

        if (feature.IsVariant)
        {
            words.Add("variant");
        }

        if (feature.IsReference && feature.FeatureKeyword != "ref")
        {
            words.Add("ref");
        }

        words.Add(string.IsNullOrEmpty(feature.FeatureKeyword) ? "usage" : feature.FeatureKeyword);
        return string.Join(' ', words);
    }

    /// <summary>
    /// Removes the usage-level (<see cref="DefBox.IsUsage"/>) boxes from <paramref name="defs"/>
    /// that <paramref name="policy"/> says are compartment rows of their rendered immediate owner,
    /// and records the owner (<see cref="DefBox.OwnerQualifiedName"/>) of every nested usage that
    /// stays as a node of its own.
    /// </summary>
    /// <remarks>
    /// This is the second stage of <see cref="CollectDefinitions"/>'s admit-then-dedup design.
    /// A usage nested in a rendered box is excluded only when it is not a node under the policy
    /// (<see cref="NodePolicy.IsNode"/>), because the owner's compartment then shows it as a row;
    /// a node-kind usage is kept and its owner recorded, so the owner and the usage are joined by
    /// a feature membership edge.
    /// Every node's qualified name is built as a strict <c>parent::child</c> containment chain, so
    /// a usage's immediate-parent qualified name is recovered by stripping the qualified name's
    /// last <c>"::"</c>-separated segment, and every ancestor is therefore strictly shallower
    /// (fewer <c>"::"</c>-separated segments) than its descendants. Boxes are processed in
    /// ascending depth order (fewest <c>"::"</c> occurrences first) while incrementally building an
    /// <c>excluded</c> set, so that by the time a usage is tested, every one of its ancestors has
    /// already been finally decided. A usage is excluded only when its immediate parent is present
    /// in <paramref name="defs"/> <em>and has not itself already been excluded</em> earlier in this
    /// same pass — i.e., only when the immediate parent is itself still rendered, and therefore
    /// already shows the usage as one of its compartment rows (see <see cref="BuildCompartments"/>,
    /// which renders a definition's direct <see cref="SysmlNode.Children"/> only, never a deeper
    /// descendant). Conversely, when the immediate parent has itself just been excluded in this
    /// same pass, it no longer renders anywhere, so its own compartment can no longer show this
    /// usage — the usage is therefore not excluded, and survives as its own standalone box. This
    /// cascades correctly through any nesting depth (two, three, or more levels of usage-in-usage
    /// nesting all resolve in this single ordered pass), guaranteeing a deeper-nested usage is never
    /// silently dropped merely because an intermediate ancestor between it and the nearest rendered
    /// box was itself excluded. A top-level usage (its qualified name has no <c>"::"</c>, i.e. it
    /// has no containing declaration) is never excluded. Definitions (<see cref="DefBox.IsUsage"/>
    /// is <see langword="false"/>) are never excluded by this rule, preserving all pre-Phase-2d
    /// definition-rendering behavior. The caller (<see cref="BuildLayout"/>) must invoke this after
    /// all scope and filter narrowing has been applied to <paramref name="defs"/>, so that a usage
    /// whose immediate parent was excluded by a metaclass filter (rather than genuinely absent from
    /// scope) is correctly kept as its own standalone box instead of being wrongly dropped against a
    /// pre-filter parent set.
    /// </remarks>
    private static IReadOnlyList<DefBox> RemoveRowUsages(IReadOnlyList<DefBox> defs, NodePolicy policy)
    {
        var qualifiedNames = new HashSet<string>(defs.Select(d => d.QualifiedName), StringComparer.Ordinal);
        var excluded = new HashSet<string>(StringComparer.Ordinal);
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);

        // Process shallowest-first so that, by the time a usage is tested, its immediate parent's
        // own exclusion decision has already been finalized (parents always have strictly fewer
        // "::" occurrences than their descendants).
        var ordered = defs
            .Select((d, index) => (Box: d, Index: index))
            .OrderBy(t => CountOccurrences(t.Box.QualifiedName, "::"))
            .ThenBy(t => t.Index);

        foreach (var (box, _) in ordered)
        {
            if (box.Feature is not { } feature)
            {
                continue;
            }

            var separatorIndex = box.QualifiedName.LastIndexOf("::", StringComparison.Ordinal);
            if (separatorIndex < 0)
            {
                continue;
            }

            var parentQualifiedName = box.QualifiedName[..separatorIndex];
            if (!qualifiedNames.Contains(parentQualifiedName) || excluded.Contains(parentQualifiedName))
            {
                continue;
            }

            if (policy.IsNode(feature))
            {
                owners[box.QualifiedName] = parentQualifiedName;
            }
            else
            {
                excluded.Add(box.QualifiedName);
            }
        }

        return defs
            .Where(d => !excluded.Contains(d.QualifiedName))
            .Select(d => owners.TryGetValue(d.QualifiedName, out var owner) ? d with { OwnerQualifiedName = owner } : d)
            .ToList();
    }

    /// <summary>
    /// Adds <paramref name="item"/> to the list stored under <paramref name="key"/>, creating the list on first use.
    /// </summary>
    private static void AddTo(Dictionary<string, List<DefBox>> map, string key, DefBox item)
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = [];
            map[key] = list;
        }

        list.Add(item);
    }

    /// <summary>
    /// Counts the number of non-overlapping occurrences of <paramref name="token"/> in
    /// <paramref name="value"/>, used by <see cref="RemoveRowUsages"/> as a nesting-depth
    /// proxy for ordering (only relative shallower-before-deeper order matters, not an exact count).
    /// </summary>
    private static int CountOccurrences(string value, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }

    /// <summary>
    /// Completes every box once the final set of boxes is known: builds its compartments (a usage
    /// owned by the box as a node is not a row), prepends the <c>doc</c> compartment, and sizes it.
    /// </summary>
    private static IReadOnlyList<DefBox> FinalizeBoxes(IReadOnlyList<DefBox> defs, Theme theme)
    {
        var nodeChildren = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var def in defs.Where(d => d.OwnerQualifiedName is not null))
        {
            if (!nodeChildren.TryGetValue(def.OwnerQualifiedName!, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                nodeChildren[def.OwnerQualifiedName!] = set;
            }

            set.Add(def.QualifiedName);
        }

        var result = new List<DefBox>(defs.Count);
        foreach (var def in defs)
        {
            nodeChildren.TryGetValue(def.QualifiedName, out var drawnAsNodes);
            var compartments = BuildCompartments(def.Declaration, def.QualifiedName, drawnAsNodes);
            var (width, height) = ComputeBoxSize(def.Label, def.Keyword, compartments, theme);

            // Exposed, unfiltered documentation is drawn as a "doc" compartment inside its owner's
            // box (SysML 8.2.3.4 documentation-compartment), wrapped to the box width.
            if (def.DocVisible &&
                BuildDocCompartment(def.Declaration.Annotations, width, theme) is { } docCompartment)
            {
                compartments = [docCompartment, .. compartments];
                (width, height) = ComputeBoxSize(def.Label, def.Keyword, compartments, theme);
            }

            result.Add(def with { Compartments = compartments, Width = width, Height = height });
        }

        return result;
    }

    /// <summary>
    /// Builds compartments for a definition or usage by grouping its owned usage features by
    /// keyword and formatting each as a row of the specification's textual notation (see
    /// <see cref="FormatFeatureRow"/>). A feature drawn as a node of its own
    /// (<paramref name="drawnAsNodes"/>) is not a row. Every compartment title is centered and italic.
    /// </summary>
    /// <param name="def">The definition or usage whose owned features become rows.</param>
    /// <param name="qualifiedName">The qualified name of <paramref name="def"/>.</param>
    /// <param name="drawnAsNodes">The qualified names of the owned features drawn as separate nodes, or null.</param>
    private static IReadOnlyList<LayoutCompartment> BuildCompartments(
        SysmlNode def,
        string qualifiedName,
        IReadOnlySet<string>? drawnAsNodes)
    {
        // Preserve keyword first-seen order so compartments appear in declaration order.
        var order = new List<string>();
        var groups = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var child in def.Children)
        {
            if (child is not SysmlFeatureNode feature)
            {
                continue;
            }

            if (drawnAsNodes is not null &&
                drawnAsNodes.Contains(child.QualifiedName ?? $"{qualifiedName}::{child.Name}"))
            {
                continue;
            }

            var keyword = string.IsNullOrEmpty(feature.FeatureKeyword) ? "feature" : feature.FeatureKeyword;
            if (!groups.TryGetValue(keyword, out var rows))
            {
                rows = [];
                groups[keyword] = rows;
                order.Add(keyword);
            }

            rows.Add(FormatFeatureRow(feature));
        }

        // Canonical compartments first, in the order the specification's compartment stack lists
        // them (attributes, enums, parts, ports), then every other keyword group in the
        // declaration order in which it was first seen.
        var emitted = CanonicalCompartmentKeywords.Where(groups.ContainsKey).ToList();
        emitted.AddRange(order.Where(k => !CanonicalCompartmentKeywords.Contains(k)));
        return [.. emitted.Select(k => MakeCompartment(Pluralize(k), groups[k]))];
    }

    /// <summary>
    /// Creates a compartment whose title is centered and italic, as the specification draws every
    /// compartment title (SysML v2 graphical BNF, clause 8.2.3: a separator line with the title
    /// centered in italics below it).
    /// </summary>
    private static LayoutCompartment MakeCompartment(string title, IReadOnlyList<string> rows) =>
        new(title, rows, TitleAlign: TextAlign.Center, TitleStyle: TextStyle.Italic);

    /// <summary>
    /// The usage keywords whose compartments are always emitted first, in this order, ahead of
    /// every other keyword group (which keep first-seen declaration order).
    /// </summary>
    private static readonly string[] CanonicalCompartmentKeywords = ["attribute", "enum value", "part", "port"];

    /// <summary>
    /// Minimum number of characters per wrapped <c>doc</c> compartment row, so documentation on a
    /// narrow box widens it to a readable column instead of wrapping into a tall sliver.
    /// </summary>
    private const int MinDocWrapChars = 36;

    /// <summary>
    /// Builds the <c>doc</c> compartment for an element's Documentation annotations, word-wrapped
    /// to fit <paramref name="boxWidth"/> (never narrower than <see cref="MinDocWrapChars"/>
    /// characters), or <see langword="null"/> when the element carries no non-empty documentation.
    /// Comment annotations are excluded; they are drawn as attached notes instead.
    /// </summary>
    /// <param name="annotations">The element's captured annotations.</param>
    /// <param name="boxWidth">The box width computed without the doc compartment.</param>
    /// <param name="theme">The theme supplying font size and padding.</param>
    /// <returns>The <c>doc</c> compartment, or null.</returns>
    private static LayoutCompartment? BuildDocCompartment(
        IReadOnlyList<SysmlAnnotation> annotations, double boxWidth, Theme theme)
    {
        var paragraphs = annotations
            .Where(a => a.Kind == SysmlAnnotationKind.Documentation)
            .SelectMany(a => SplitParagraphs(a.Text))
            .ToList();
        if (paragraphs.Count == 0)
        {
            return null;
        }

        var fitChars = (int)Math.Floor(((boxWidth - (3.0 * theme.LabelPadding)) / (theme.FontSizeBody * CharWidthFactor)) + 1e-9);
        var maxChars = Math.Max(MinDocWrapChars, fitChars);
        var rows = paragraphs.SelectMany(p => WrapText(p, maxChars)).ToList();
        return MakeCompartment("doc", rows);
    }

    /// <summary>
    /// Splits raw annotation text into paragraphs of single-spaced words: lines are trimmed (with a
    /// leading <c>*</c> comment-continuation marker removed), consecutive non-blank lines are
    /// joined, and blank lines separate paragraphs.
    /// </summary>
    private static IEnumerable<string> SplitParagraphs(string text)
    {
        var current = new List<string>();
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith('*'))
            {
                line = line[1..].Trim();
            }

            if (line.Length == 0)
            {
                if (current.Count > 0)
                {
                    yield return string.Join(' ', current);
                    current.Clear();
                }

                continue;
            }

            current.AddRange(line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }

        if (current.Count > 0)
        {
            yield return string.Join(' ', current);
        }
    }

    /// <summary>
    /// Greedily word-wraps a single-spaced paragraph into rows of at most
    /// <paramref name="maxChars"/> characters, hard-breaking any single word longer than that.
    /// </summary>
    internal static IReadOnlyList<string> WrapText(string paragraph, int maxChars)
    {
        var rows = new List<string>();
        var row = new System.Text.StringBuilder();
        foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var remaining = word;
            while (remaining.Length > maxChars)
            {
                if (row.Length > 0)
                {
                    rows.Add(row.ToString());
                    row.Clear();
                }

                rows.Add(remaining[..maxChars]);
                remaining = remaining[maxChars..];
            }

            if (row.Length > 0 && row.Length + 1 + remaining.Length > maxChars)
            {
                rows.Add(row.ToString());
                row.Clear();
            }

            if (row.Length > 0)
            {
                row.Append(' ');
            }

            row.Append(remaining);
        }

        if (row.Length > 0)
        {
            rows.Add(row.ToString());
        }

        return rows;
    }

    /// <summary>
    /// Formats a usage feature as a compartment row in the specification's textual notation
    /// (SysML v2 graphical BNF, clause 8.2.3: <c>el-prefix? UsagePrefix usage-cp</c>):
    /// <c>[in|out|inout] name : [~]Type [multiplicity] [ordered] [nonunique] [:&gt; subsetted]
    /// [:&gt;&gt; redefined] [::&gt; referenced] [= value]</c>, omitting every part the model does not
    /// declare. A constraint-kind feature shows its raw expression text instead.
    /// </summary>
    private static string FormatFeatureRow(SysmlFeatureNode feature)
    {
        // A constraint-kind feature's payload is a boolean expression, not a type reference — show
        // its raw expression text instead of the generic "name : Type [multiplicity]" shape. Unnamed
        // (the corpus-dominant case) shows the expression alone; named shows "name: expression".
        if (feature.ExpressionText is { Length: > 0 } expr)
        {
            return feature.Name is { Length: > 0 } exprName ? $"{exprName}: {expr}" : expr;
        }

        var row = FormatNameLine(
            shortName: null,
            feature.Name ?? string.Empty,
            supertypes: null,
            typing: FormatTyping(feature),
            multiplicity: feature.Multiplicity,
            subsets: feature.SupertypeNames,
            redefines: feature.RedefinedFeatureName,
            referenceSubsets: feature.ReferenceSubsettingName,
            modifiers: FormatModifiers(feature));

        // A declared direction prefixes the row; a declared value follows it verbatim (the value
        // clause already carries its own operator, e.g. "= 5 [kg]").
        if (feature.Direction is { } direction)
        {
            row = $"{DirectionKeyword(direction)} {row}";
        }

        if (feature.ValueText is { Length: > 0 } value)
        {
            row = $"{row} {value}";
        }

        row = row.Trim();
        return row.Length == 0 ? "\u2014" : row;
    }

    /// <summary>Returns the keyword of a feature direction.</summary>
    private static string DirectionKeyword(SysmlFeatureDirection direction) => direction switch
    {
        SysmlFeatureDirection.In => "in",
        SysmlFeatureDirection.Out => "out",
        _ => "inout",
    };

    /// <summary>Returns the typing text of a usage: the typing reference, or the conjugated one prefixed with <c>~</c>; null when untyped.</summary>
    private static string? FormatTyping(SysmlFeatureNode feature)
    {
        if (feature.ConjugatedFeatureTyping is { Length: > 0 } conjugated)
        {
            return $"~{conjugated}";
        }

        return feature.FeatureTyping is { Length: > 0 } typing ? typing : null;
    }

    /// <summary>Returns the <c>ordered</c>/<c>nonunique</c> multiplicity modifiers of a usage, or null when it has none.</summary>
    private static string? FormatModifiers(SysmlFeatureNode feature) =>
        (feature.IsOrdered, feature.IsNonunique) switch
        {
            (true, true) => "ordered nonunique",
            (true, false) => "ordered",
            (false, true) => "nonunique",
            _ => null,
        };

    /// <summary>
    /// Formats the name line of a usage node: <c>&lt;shortName&gt; name : Type [multiplicity] ordered
    /// nonunique :&gt; subsetted :&gt;&gt; redefined ::&gt; referenced</c>.
    /// </summary>
    private static string FormatUsageNameLine(SysmlFeatureNode feature, string name) =>
        FormatNameLine(
            feature.ShortName,
            name,
            supertypes: null,
            typing: FormatTyping(feature),
            multiplicity: feature.Multiplicity,
            subsets: feature.SupertypeNames,
            redefines: feature.RedefinedFeatureName,
            referenceSubsets: feature.ReferenceSubsettingName,
            modifiers: FormatModifiers(feature));

    /// <summary>
    /// Formats the name line of a node label in the specification's textual notation:
    /// <c>&lt;shortName&gt; name :&gt; Super1, Super2 : Type [multiplicity]</c>, omitting every piece that is
    /// <see langword="null"/> or empty.
    /// </summary>
    /// <remarks>
    /// Shared by the General, Interconnection, and Browser views so a given element reads the same
    /// way in every view (SysML v2 graphical BNF, clause 8.2.3: the name compartment lists the
    /// short name before the name, then the specialization, then the typing and
    /// multiplicity). Supertypes and typing are passed through exactly as written in the model
    /// (possibly qualified); nothing is resolved here. The optional trailing parameters
    /// (<paramref name="modifiers"/>, <paramref name="subsets"/>, <paramref name="redefines"/>,
    /// <paramref name="referenceSubsets"/>) extend the line the way the textual notation does for a
    /// usage and are appended after the multiplicity, in that order; callers that do not pass them
    /// are unaffected.
    /// </remarks>
    /// <param name="shortName">The declared short name (without angle brackets), or null.</param>
    /// <param name="name">The element's name; must not be null.</param>
    /// <param name="supertypes">The raw specialization (<c>:&gt;</c>) references, or null.</param>
    /// <param name="typing">The raw feature-typing reference (<c>: Type</c>), or null.</param>
    /// <param name="multiplicity">The multiplicity text including brackets (e.g. <c>[2]</c>), or null.</param>
    /// <param name="subsets">The raw subsetting (<c>:&gt;</c>) references of a usage, or null.</param>
    /// <param name="redefines">The raw redefinition (<c>:&gt;&gt;</c>) reference of a usage, or null.</param>
    /// <param name="referenceSubsets">The raw reference-subsetting (<c>::&gt;</c>) reference of a usage, or null.</param>
    /// <param name="modifiers">The multiplicity modifiers (<c>ordered nonunique</c>), or null.</param>
    /// <returns>The formatted name line.</returns>
    internal static string FormatNameLine(
        string? shortName,
        string name,
        IEnumerable<string>? supertypes = null,
        string? typing = null,
        string? multiplicity = null,
        IEnumerable<string>? subsets = null,
        string? redefines = null,
        string? referenceSubsets = null,
        string? modifiers = null)
    {
        var line = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(shortName))
        {
            line.Append('<').Append(shortName).Append("> ");
        }

        line.Append(name);

        var supers = supertypes?.Where(s => !string.IsNullOrEmpty(s)).ToList();
        if (supers is { Count: > 0 })
        {
            line.Append(" :> ").Append(string.Join(", ", supers));
        }

        if (!string.IsNullOrEmpty(typing))
        {
            line.Append(" : ").Append(typing);
        }

        if (!string.IsNullOrEmpty(multiplicity))
        {
            line.Append(' ').Append(multiplicity);
        }

        if (!string.IsNullOrEmpty(modifiers))
        {
            line.Append(' ').Append(modifiers);
        }

        var subsetted = subsets?.Where(s => !string.IsNullOrEmpty(s)).ToList();
        if (subsetted is { Count: > 0 })
        {
            line.Append(" :> ").Append(string.Join(", ", subsetted));
        }

        if (!string.IsNullOrEmpty(redefines))
        {
            line.Append(" :>> ").Append(redefines);
        }

        if (!string.IsNullOrEmpty(referenceSubsets))
        {
            line.Append(" ::> ").Append(referenceSubsets);
        }

        return line.ToString();
    }

    /// <summary>Returns a simple plural form of a usage keyword for use as a compartment title.</summary>
    private static string Pluralize(string keyword) => keyword switch
    {
        "ref" => "references",

        // The specification's compartment title for enumerated values is "enums", not the naive
        // plural of the "enum value" keyword.
        "enum value" => "enums",

        // Stereotype-style titles, per the OMG spec's Graphical Notation chapter conventions —
        // reusing this file's existing guillemet convention already established for edge midpoint
        // labels (e.g. "«allocate»" in BuildModelEdges), the dominant existing precedent for
        // stereotype-style text in this renderer. A requirement conventionally has exactly one
        // subject, so "subject" stays singular rather than pluralizing.
        "subject" => "«subject»",
        "assume constraint" => "«assume constraint»",
        "require constraint" => "«require constraint»",
        "constraint" => "«constraint»",
        _ => keyword + "s",
    };

    /// <summary>Computes the intrinsic box size needed for the title and any compartments.</summary>
    private static (double Width, double Height) ComputeBoxSize(
        string name,
        string keyword,
        IReadOnlyList<LayoutCompartment> compartments,
        Theme theme)
    {
        var nameWidth = (name.Length * theme.FontSizeTitle * CharWidthFactor) + (2.0 * theme.LabelPadding);
        var keywordWidth = ((keyword.Length + 2) * theme.FontSizeBody * CharWidthFactor) + (2.0 * theme.LabelPadding);
        var width = Math.Max(MinBoxWidth, Math.Max(nameWidth, keywordWidth));

        // Widen to fit the longest compartment title or row.
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

        // Title area holds the keyword line and the name line; add a little body breathing room.
        var height = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true) + theme.LabelPadding;
        foreach (var compartment in compartments)
        {
            height += ComputeCompartmentHeight(compartment, theme);
        }

        return (width, height);
    }

    /// <summary>
    /// Computes the rendered height of a compartment, matching the renderer's layout: an optional
    /// title row followed by one row per entry.
    /// </summary>
    private static double ComputeCompartmentHeight(LayoutCompartment compartment, Theme theme)
    {
        var height = 0.0;
        if (compartment.Title is not null)
        {
            height += theme.LabelPadding + theme.FontSizeBody + theme.LabelPadding;
        }

        height += compartment.Rows.Count * (theme.LabelPadding + theme.FontSizeBody);

        // Bottom gap added by the renderer after the last row.
        height += theme.LabelPadding;
        return height;
    }

    /// <summary>
    /// Groups boxes by their parent package name (the qualified-name prefix before the last
    /// <c>::</c>), preserving first-seen order. Top-level boxes use an empty package key. A usage
    /// (whose owner is not rendered) is keyed by the nearest enclosing declaration that is not a
    /// definition or another usage, so a usage nested in an excluded definition is never given a
    /// package folder named after that definition.
    /// </summary>
    private static IReadOnlyList<(string Package, List<DefBox> Items)> GroupByPackage(
        IReadOnlyList<DefBox> defs,
        SysmlWorkspace workspace)
    {
        var order = new List<string>();
        var map = new Dictionary<string, List<DefBox>>(StringComparer.Ordinal);

        foreach (var def in defs)
        {
            var sep = def.QualifiedName.LastIndexOf("::", StringComparison.Ordinal);
            var package = sep >= 0 ? def.QualifiedName[..sep] : string.Empty;
            if (def.IsUsage)
            {
                package = PlacementPackage(package, workspace);
            }

            if (!map.TryGetValue(package, out var list))
            {
                list = [];
                map[package] = list;
                order.Add(package);
            }

            list.Add(def);
        }

        return [.. order.Select(p => (p, map[p]))];
    }

    /// <summary>
    /// Returns the nearest enclosing namespace of a usage that is not itself a definition or usage:
    /// walks <paramref name="prefix"/> outward while it names a declared definition or usage.
    /// A prefix with no declaration (a hand-built workspace) or a package is returned as is.
    /// </summary>
    private static string PlacementPackage(string prefix, SysmlWorkspace workspace)
    {
        while (prefix.Length > 0 &&
               workspace.Declarations.TryGetValue(prefix, out var declaration) &&
               declaration is SysmlDefinitionNode or SysmlFeatureNode)
        {
            var sep = prefix.LastIndexOf("::", StringComparison.Ordinal);
            prefix = sep >= 0 ? prefix[..sep] : string.Empty;
        }

        return prefix;
    }

    /// <summary>
    /// Looks up the semantic model's answer for one raw reference of a declaration by the
    /// reference's identity: the target of the first <paramref name="kind"/> edge of
    /// <paramref name="declaration"/> whose <see cref="SysmlEdge.RawReference"/> equals
    /// <paramref name="rawReference"/> exactly (ordinal). Identity is exact text, never a name
    /// suffix, so a reference that did not resolve can never take another reference's target (for
    /// example the target an alias of the same symbol resolved to). Shared with
    /// <see cref="GridViewLayoutStrategy"/>.
    /// </summary>
    /// <param name="declaration">The declaration owning the reference, or null when unknown.</param>
    /// <param name="kind">The edge kind the reference produces.</param>
    /// <param name="rawReference">The reference text as written in the model.</param>
    /// <param name="target">
    /// The resolved target when the answer is known and the reference resolved; otherwise null.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the semantic model's answer is authoritative — the declaration
    /// carries at least one <paramref name="kind"/> edge with a raw reference, so a reference with
    /// no matching edge is semantically unresolved (<paramref name="target"/> is null and the
    /// caller draws nothing); <see langword="false"/> when the declaration carries no
    /// raw-reference-identified edge of that kind (a hand-built workspace, or data without edge
    /// identity), so the caller resolves the reference itself by name resolution.
    /// </returns>
    internal static bool TryGetResolvedTarget(
        SysmlNode? declaration, SysmlEdgeKind kind, string rawReference, out string? target)
    {
        target = null;
        var identified = false;
        if (declaration is null)
        {
            return false;
        }

        foreach (var edge in declaration.ResolvedEdges)
        {
            if (edge.Kind != kind || edge.RawReference is null)
            {
                continue;
            }

            identified = true;
            if (string.Equals(edge.RawReference, rawReference, StringComparison.Ordinal))
            {
                target = edge.TargetQualifiedName;
                return true;
            }
        }

        return identified;
    }

    /// <summary>
    /// Resolves the specialization, owner membership, feature typing, subsetting, reference
    /// subsetting, redefinition, connect, allocate, dependency, binding, satisfy, verify and import
    /// relationships across every definition and usage (regardless of package) into a flat list of
    /// qualified-name edges. The satisfy, verify and import edges are overlays: an endpoint that is
    /// not rendered draws nothing and is never reported. Self-references and unresolved targets
    /// are skipped; whether an edge's endpoints actually receive a graph node (i.e., were not
    /// depth-truncated) is decided later, in <see cref="BuildGraph"/>. The second tuple element
    /// reports every <see cref="EdgeKind.Connect"/>/<see cref="EdgeKind.Allocate"/>/
    /// <see cref="EdgeKind.Dependency"/>/<see cref="EdgeKind.Binding"/> edge from
    /// <c>workspace.Index.AllEdges</c> that was dropped because an endpoint failed to resolve to a
    /// rendered box, or because both endpoints resolved to the same box (a genuine self-loop) — for
    /// surfacing via <see cref="LayoutWarnings.ForDroppedRelationshipEdges"/>, so a residual/future
    /// collapse is visible instead of silently dropped. An unresolved-endpoint drop is only
    /// reported when <paramref name="isScoped"/> is <see langword="false"/> (the view renders the
    /// whole workspace, so an endpoint that fails to resolve to a box is genuinely wrong); when the
    /// view is narrowed by <c>expose</c>, an endpoint outside the exposed scope legitimately fails
    /// to resolve to a box by design and is not reported, to avoid misleading noise for ordinary,
    /// intentional scope narrowing. A genuine self-loop (both endpoints resolve, to the same box)
    /// is always reported, regardless of scoping, since it can never be an intended outcome.
    /// </summary>
    private static (List<ModelEdge> Edges, List<DroppedRelationshipEdge> Dropped) BuildModelEdges(
        IReadOnlyList<DefBox> defs, SysmlWorkspace workspace, bool isScoped, NodePolicy policy)
    {
        // Every reference is resolved to a rendered box by the semantic model's edge first, then
        // KerML name resolution (see TypeResolver); an unresolved reference draws no edge.
        var byQualified = new HashSet<string>(defs.Select(d => d.QualifiedName), StringComparer.Ordinal);
        var usageBoxes = new HashSet<string>(defs.Where(d => d.IsUsage).Select(d => d.QualifiedName), StringComparer.Ordinal);
        var types = new TypeResolver(byQualified, FilterExpressionEvaluator.CreateNameResolver(workspace));

        var edges = new List<ModelEdge>();
        foreach (var def in defs)
        {
            // Supertype targets are resolved by ReferenceResolver (owning namespace and imports
            // first); each raw reference is matched to its own edge by exact reference text (see
            // TryGetResolvedTarget), used in preference to re-resolving the reference below.
            workspace.Declarations.TryGetValue(def.QualifiedName, out var declaration);

            // Specialization: subtype → supertype, open arrowhead at the supertype (target) end.
            for (var i = 0; i < def.SupertypeNames.Count; i++)
            {
                var answer = EdgeAnswer.For(declaration, SysmlEdgeKind.Supertype, def.SupertypeNames[i]);
                if (types.TryResolveSupertype(def.SupertypeNames[i], answer, def.QualifiedName, out var target) &&
                    target != def.QualifiedName)
                {
                    edges.Add(new ModelEdge(def.QualifiedName, target, EndMarkerStyle.HollowTriangle, EdgeKind.Specialization));
                }
            }

            // The relationships of a usage node. A row-only policy draws none (everything is shown
            // in the compartments), and a usage drawn as a row has no box here at all.
            if (def.Feature is { } usage && policy.Mode != UsageNodeMode.RowsOnly)
            {
                AddUsageEdges(edges, def, usage, types);
            }
        }

        // Connect/Allocate/Dependency/Binding: resolved directly from the workspace's semantic
        // index (already computed by ReferenceResolver — no re-resolution needed here), each
        // endpoint mapped to its owning rendered box via ResolveOwningBox. An edge is only emitted
        // when both endpoints resolve to distinct boxes; a same-box result (e.g. two sibling
        // features of the same enclosing definition) is a genuine self-loop and is dropped, exactly
        // as every other edge kind in this method already does. Every dropped edge is also
        // considered for `dropped` (LayoutWarnings.ForDroppedRelationshipEdges) — see RecordDrop.
        var dropped = new List<DroppedRelationshipEdge>();

        void RecordDrop(string kind, string source0, string target0, string? source, string? target)
        {
            // A genuine self-loop (both endpoints resolved, to the same box) is always reported.
            // An unresolved endpoint is only reported when the view is not `expose`-scoped: within
            // a scoped view, an endpoint outside the exposed subtree legitimately fails to resolve
            // to a box by design, and warning about it would be misleading noise for ordinary,
            // intentional scope narrowing.
            if ((source is not null && target is not null) || !isScoped)
            {
                dropped.Add(new DroppedRelationshipEdge(kind, source0, target0, DropReason(source, target)));
            }
        }

        foreach (var edge in workspace.Index.AllEdges)
        {
            if (edge.SourceQualifiedName is not { Length: > 0 } sourceRef)
            {
                continue;
            }

            switch (edge.Kind)
            {
                case SysmlEdgeKind.Connect:
                    {
                        var source = ResolveOwningBox(sourceRef, workspace, byQualified, usageBoxes);
                        var target = ResolveOwningBox(edge.TargetQualifiedName, workspace, byQualified, usageBoxes);
                        if (source is not null && target is not null && source != target)
                        {
                            edges.Add(new ModelEdge(source, target, EndMarkerStyle.None, EdgeKind.Connect));
                        }
                        else
                        {
                            RecordDrop("Connect", sourceRef, edge.TargetQualifiedName, source, target);
                        }

                        break;
                    }

                case SysmlEdgeKind.Allocate:
                    {
                        var source = ResolveOwningBox(sourceRef, workspace, byQualified, usageBoxes);
                        var target = ResolveOwningBox(edge.TargetQualifiedName, workspace, byQualified, usageBoxes);
                        if (source is not null && target is not null && source != target)
                        {
                            edges.Add(new ModelEdge(source, target, EndMarkerStyle.OpenChevron, EdgeKind.Allocate, "«allocate»"));
                        }
                        else
                        {
                            RecordDrop("Allocate", sourceRef, edge.TargetQualifiedName, source, target);
                        }

                        break;
                    }

                case SysmlEdgeKind.Dependency:
                    {
                        var source = ResolveOwningBox(sourceRef, workspace, byQualified, usageBoxes);
                        var target = ResolveOwningBox(edge.TargetQualifiedName, workspace, byQualified, usageBoxes);
                        if (source is not null && target is not null && source != target)
                        {
                            edges.Add(new ModelEdge(source, target, EndMarkerStyle.OpenChevron, EdgeKind.Dependency));
                        }
                        else
                        {
                            RecordDrop("Dependency", sourceRef, edge.TargetQualifiedName, source, target);
                        }

                        break;
                    }

                case SysmlEdgeKind.Binding:
                    {
                        var source = ResolveOwningBox(sourceRef, workspace, byQualified, usageBoxes);
                        var target = ResolveOwningBox(edge.TargetQualifiedName, workspace, byQualified, usageBoxes);
                        if (source is not null && target is not null && source != target)
                        {
                            edges.Add(new ModelEdge(source, target, EndMarkerStyle.None, EdgeKind.Binding, "="));
                        }
                        else
                        {
                            RecordDrop("Binding", sourceRef, edge.TargetQualifiedName, source, target);
                        }

                        break;
                    }

                case SysmlEdgeKind.Satisfy:
                case SysmlEdgeKind.Verify:
                    {
                        // The satisfying/verifying element may be a nested declaration that is not a
                        // box of its own (an objective, a subject), so each endpoint maps to the
                        // nearest rendered box on its path. An unrendered endpoint or a same-box
                        // result draws nothing, silently: the relationship is an overlay, and the
                        // elements it relates are often outside the view's scope by design.
                        var isSatisfy = edge.Kind == SysmlEdgeKind.Satisfy;
                        var source = ResolveNearestBox(sourceRef, byQualified);
                        var target = ResolveNearestBox(edge.TargetQualifiedName, byQualified);
                        if (source is not null && target is not null && source != target)
                        {
                            AddOnce(
                                edges,
                                new ModelEdge(
                                    source,
                                    target,
                                    EndMarkerStyle.OpenChevron,
                                    isSatisfy ? EdgeKind.Satisfy : EdgeKind.Verify,
                                    isSatisfy ? "«satisfy»" : "«verify»"));
                        }

                        break;
                    }
            }
        }

        AddImportEdges(edges, workspace, byQualified);

        return (edges, dropped);
    }

    /// <summary>
    /// Adds <paramref name="edge"/> unless an identical edge (same endpoints, kind and label) is
    /// already present. The label is part of the key because it carries the distinction between two
    /// relationships of one kind between the same pair of endpoints, such as a namespace import
    /// (<c>«import»*</c>) and a recursive import (<c>«import»**</c>) of the same package.
    /// </summary>
    private static void AddOnce(List<ModelEdge> edges, ModelEdge edge)
    {
        if (!edges.Exists(e => e.SourceQualified == edge.SourceQualified &&
                               e.TargetQualified == edge.TargetQualified &&
                               e.Kind == edge.Kind &&
                               e.Label == edge.Label))
        {
            edges.Add(edge);
        }
    }

    /// <summary>
    /// Resolves the rendered box nearest to <paramref name="qualifiedName"/>: the name itself when it
    /// is a box, else the longest <c>"::"</c> prefix that is one, or <see langword="null"/> when no
    /// prefix is rendered. A satisfy or verify relationship names declarations (a verification
    /// objective, a nested subject) that are not boxes of their own; the relationship is drawn on
    /// the box that contains them.
    /// </summary>
    private static string? ResolveNearestBox(string qualifiedName, HashSet<string> byQualified)
    {
        var candidate = qualifiedName;
        while (candidate.Length > 0)
        {
            if (byQualified.Contains(candidate))
            {
                return candidate;
            }

            var separator = candidate.LastIndexOf("::", StringComparison.Ordinal);
            candidate = separator > 0 ? candidate[..separator] : string.Empty;
        }

        return null;
    }

    /// <summary>
    /// Adds an <see cref="EdgeKind.Import"/> edge for every resolved import of a namespace: the
    /// import nodes are found among the children of every package, definition and usage (an import's
    /// own qualified name is null, so it carries no usable source in <c>AllEdges</c>), the importing
    /// namespace is the source and the imported namespace the target. A package endpoint is its
    /// folder, a definition or usage endpoint its box; an endpoint that is not rendered (the standard
    /// library, a package without drawn content) and an import between a namespace and one nested
    /// in it draw nothing. The label reads <c>«import»</c>, <c>«import»*</c> for a namespace import
    /// and <c>«import»**</c> for a recursive one (SysML v2 graphical BNF, clause 8.2.3.5).
    /// </summary>
    private static void AddImportEdges(List<ModelEdge> edges, SysmlWorkspace workspace, HashSet<string> byQualified)
    {
        string? Endpoint(string qualifiedName)
        {
            if (byQualified.Contains(qualifiedName))
            {
                return qualifiedName;
            }

            return workspace.Declarations.TryGetValue(qualifiedName, out var declaration) && declaration is SysmlPackageNode
                ? FolderKeyPrefix + qualifiedName
                : null;
        }

        foreach (var (importer, declaration) in workspace.Declarations)
        {
            foreach (var import in declaration.Children.OfType<SysmlImportNode>())
            {
                var label = ImportLabel(import);
                foreach (var target in import.ResolvedEdges.Where(e => e.Kind == SysmlEdgeKind.Import).Select(e => e.TargetQualifiedName))
                {
                    var contained = target.StartsWith(importer + "::", StringComparison.Ordinal) ||
                                    importer.StartsWith(target + "::", StringComparison.Ordinal);
                    if (importer == target || contained)
                    {
                        continue;
                    }

                    if (Endpoint(importer) is { } source && Endpoint(target) is { } destination)
                    {
                        AddOnce(edges, new ModelEdge(source, destination, EndMarkerStyle.OpenChevron, EdgeKind.Import, label));
                    }
                }
            }
        }
    }

    /// <summary>Returns the label of an import edge: <c>«import»</c>, <c>«import»*</c> (namespace import) or <c>«import»**</c> (recursive).</summary>
    private static string ImportLabel(SysmlImportNode import)
    {
        if (import.IsRecursive)
        {
            return "«import»**";
        }

        return import.IsWildcard ? "«import»*" : "«import»";
    }

    /// <summary>
    /// Adds the relationship edges of one usage node. Every edge runs from the usage (the owned,
    /// dependent or specific element) to the general element, with the glyph at the general end:
    /// the owner (filled diamond for a composite usage, hollow diamond for a referential one), the
    /// definition (feature typing: hollow triangle with two dots), the subsetted usage (hollow
    /// triangle), the reference-subsetted usage (hollow triangle with four dots) and the redefined
    /// usage (hollow triangle with a crossbar), per SysML v2 graphical BNF, clause 8.2.3.6. An
    /// unresolved reference, an unrendered target and a self-reference draw no edge.
    /// </summary>
    private static void AddUsageEdges(List<ModelEdge> edges, DefBox box, SysmlFeatureNode usage, TypeResolver types)
    {
        var self = box.QualifiedName;
        var ownerNamespace = EnclosingPackage(self);

        void Add(string? target, EndMarkerStyle marker, EdgeKind kind)
        {
            if (target is not null && target != self)
            {
                edges.Add(new ModelEdge(self, target, marker, kind));
            }
        }

        // Feature membership: owned usage → owner, diamond at the owner.
        Add(box.OwnerQualifiedName, IsReferential(usage) ? EndMarkerStyle.HollowDiamond : EndMarkerStyle.FilledDiamond, EdgeKind.Membership);

        // Feature typing: usage → definition.
        Add(types.ResolveTyping(usage, ownerNamespace), EndMarkerStyle.HollowTriangleTwoDots, EdgeKind.FeatureTyping);

        // Subsetting: usage → each subsetted usage; each raw reference is matched to its own
        // semantic edge by exact reference text (see TryGetResolvedTarget).
        foreach (var raw in usage.SupertypeNames)
        {
            var answer = EdgeAnswer.For(usage, SysmlEdgeKind.Supertype, raw);
            Add(types.ResolveSubsetting(raw, answer, ownerNamespace), EndMarkerStyle.HollowTriangle, EdgeKind.Subsetting);
        }

        // Reference subsetting: usage → referenced usage.
        if (usage.ReferenceSubsettingName is { Length: > 0 } referenceSubsetting)
        {
            Add(types.ResolveReferenceSubsetting(referenceSubsetting, ownerNamespace), EndMarkerStyle.HollowTriangleFourDots, EdgeKind.ReferenceSubsetting);
        }

        // Redefinition: usage → redefined usage.
        if (usage.RedefinedFeatureName is { Length: > 0 } redefined)
        {
            Add(types.ResolveRedefinition(usage, redefined, ownerNamespace), EndMarkerStyle.HollowTriangleCrossbar, EdgeKind.Redefinition);
        }
    }

    /// <summary>
    /// Returns whether a usage is referential (not composite): a reference usage, or an attribute,
    /// plain reference or enumeration usage, which KerML/SysML declare non-composite by default
    /// (part, item, port, action, state, requirement … usages are composite). A referential usage
    /// joins its owner with a hollow diamond, a composite one with a filled diamond.
    /// </summary>
    private static bool IsReferential(SysmlFeatureNode usage) =>
        usage.IsReference || usage.FeatureKeyword is "attribute" or "ref" or "enum";

    /// <summary>
    /// Returns a short human-readable reason a Connect/Allocate/Dependency/Binding edge's endpoints
    /// did not resolve to two distinct rendered boxes, for
    /// <see cref="LayoutWarnings.ForDroppedRelationshipEdges"/>.
    /// </summary>
    private static string DropReason(string? source, string? target)
    {
        if (source is null && target is null)
        {
            return "neither endpoint resolved to a box";
        }

        if (source is null)
        {
            return "source endpoint did not resolve to a box";
        }

        if (target is null)
        {
            return "target endpoint did not resolve to a box";
        }

        return "both endpoints resolved to the same box";
    }

    /// <summary>
    /// Resolves the qualified name of the rendered box that "owns" the given endpoint reference,
    /// for <see cref="EdgeKind.Connect"/>/<see cref="EdgeKind.Binding"/> endpoint mapping. A
    /// reference that already names a rendered box resolves directly (the common case for
    /// definition-to-definition references, e.g. <see cref="EdgeKind.Allocate"/>/
    /// <see cref="EdgeKind.Dependency"/>). Otherwise the <em>shortest</em> <c>"::"</c> prefix that
    /// names a rendered <em>usage</em> box wins (the usage node immediately owned by the rendered
    /// enclosing definition): <c>connect controller.power to battery.output;</c> inside
    /// <c>Drone</c> joins the <c>controller</c> and <c>battery</c> nodes instead of resolving both
    /// sides to <c>Drone</c> itself and dropping a self-loop. Last, for an endpoint nested in a
    /// feature that is a row rather than a node, this walks successively shorter prefixes, longest
    /// first, looking for a <see cref="SysmlFeatureNode"/> whose resolved
    /// <see cref="SysmlEdgeKind.Typing"/> edge targets a rendered box; the shortest matching prefix wins.
    /// </summary>
    private static string? ResolveOwningBox(
        string qualifiedName,
        SysmlWorkspace workspace,
        HashSet<string> byQualified,
        HashSet<string> usageBoxes)
    {
        if (byQualified.Contains(qualifiedName))
        {
            return qualifiedName;
        }

        var segments = qualifiedName.Split("::");

        // The outermost rendered usage node on the path.
        for (var length = 1; length < segments.Length; length++)
        {
            var prefix = string.Join("::", segments.Take(length));
            if (usageBoxes.Contains(prefix))
            {
                return prefix;
            }
        }

        string? candidate = null;
        for (var length = segments.Length; length >= 1; length--)
        {
            var prefix = string.Join("::", segments.Take(length));
            if (workspace.Declarations.TryGetValue(prefix, out var declaration) &&
                declaration is SysmlFeatureNode &&
                declaration.ResolvedEdges.FirstOrDefault(e => e.Kind == SysmlEdgeKind.Typing) is { } typingEdge &&
                byQualified.Contains(typingEdge.TargetQualifiedName))
            {
                // Keep walking to shorter prefixes: the shortest (outermost) matching prefix wins.
                candidate = typingEdge.TargetQualifiedName;
            }
        }

        return candidate;
    }

    /// <summary>
    /// Resolves the type, supertype, redefinition, and subsetting references of rendered boxes to
    /// other rendered boxes. A reference already resolved by the semantic model is used as
    /// resolved — the semantic resolver applies KerML 8.2.3.5 itself (member visibility, filtered
    /// imports, and import scoping, including file-root imports), so its answer is authoritative.
    /// Supertype/subsetting references are matched to their edge by exact reference text (see
    /// <see cref="TryGetResolvedTarget"/>), so an unresolved reference never takes another
    /// reference's target. Where the declaration carries no edge identity (hand-built workspaces),
    /// the reference is resolved by KerML 8.2.3.5 name resolution (<see cref="NameResolver"/>:
    /// owned, imported, and inherited memberships — including aliases and short names — of the
    /// local namespace, then outward through owning namespaces to the global namespace, with later
    /// qualified-name segments by visible resolution). A reference neither step resolves is
    /// unresolved and draws no edge; there is no bare simple-name matching.
    /// </summary>
    private sealed class TypeResolver
    {
        private readonly HashSet<string> _rendered;
        private readonly NameResolver _resolver;

        /// <summary>Initializes the resolver.</summary>
        public TypeResolver(HashSet<string> rendered, NameResolver resolver)
        {
            _rendered = rendered;
            _resolver = resolver;
        }

        /// <summary>
        /// Resolves the rendered box a usage is typed by: the feature's resolved
        /// <see cref="SysmlEdgeKind.Typing"/> target, else the raw type reference relative to the
        /// feature's owning namespace (KerML 8.2.3.5.2, FeatureTyping is a Specialization). A
        /// conjugated typing (<c>: ~PortT</c>) resolves to the underlying port definition. An
        /// unresolved or unrendered type yields null.
        /// </summary>
        public string? ResolveTyping(SysmlFeatureNode usage, string? ownerNamespace)
        {
            if (usage.FeatureTyping is { Length: > 0 } typeName)
            {
                return usage.ResolvedEdges.FirstOrDefault(e => e.Kind == SysmlEdgeKind.Typing) is { } edge
                    ? RenderedOrNull(edge.TargetQualifiedName)
                    : Resolve(typeName, ownerNamespace);
            }

            return usage.ConjugatedFeatureTyping is { Length: > 0 } conjugated
                ? Resolve(conjugated, ownerNamespace)
                : null;
        }

        /// <summary>
        /// Resolves a definition's raw supertype reference: when the semantic model's
        /// <paramref name="answer"/> is known, its target (a reference with no matching resolved
        /// <see cref="SysmlEdgeKind.Supertype"/> edge is unresolved and draws nothing); else the
        /// reference relative to the definition's owning namespace (KerML 8.2.3.5.2: a
        /// Specialization's local namespace is its owning type's owning namespace).
        /// </summary>
        public bool TryResolveSupertype(
            string supertype,
            EdgeAnswer answer,
            string subtypeQualifiedName,
            [MaybeNullWhen(false)] out string qualified)
        {
            if (answer.Known)
            {
                qualified = answer.Target;
                return qualified is not null && _rendered.Contains(qualified);
            }

            qualified = Resolve(supertype, OwnerOf(subtypeQualifiedName));
            return qualified is not null;
        }

        /// <summary>
        /// Resolves the rendered usage box a usage redefines: the redefinition edge's resolved
        /// target when the semantic model supplied one; else per KerML 8.2.3.5.1 the reference is
        /// resolved with each general type of the redefining feature's owning type as the local
        /// namespace in turn, then (leniently, as the semantic resolver does) by ordinary
        /// resolution from the owning type. An unresolved reference yields null.
        /// </summary>
        public string? ResolveRedefinition(SysmlFeatureNode usage, string redefinedReference, string? ownerNamespace) =>
            RenderedOrNull(
                usage.ResolvedEdges.FirstOrDefault(e => e.Kind == SysmlEdgeKind.Redefinition)?.TargetQualifiedName ??
                (ownerNamespace is null ? null : _resolver.ResolveRedefinedFeature(redefinedReference, ownerNamespace)) ??
                _resolver.Resolve(redefinedReference, ownerNamespace));

        /// <summary>
        /// Resolves the rendered box a usage subsets: the target of the semantic model's
        /// <paramref name="answer"/> when it is known (a reference with no matching resolved
        /// <see cref="SysmlEdgeKind.Supertype"/> edge is unresolved and yields null); else the
        /// reference resolved relative to the subsetting feature's owning namespace, whose
        /// inherited memberships include the ancestor's features. An unresolved reference yields null.
        /// </summary>
        public string? ResolveSubsetting(string subsettedReference, EdgeAnswer answer, string? ownerNamespace) =>
            answer.Known ? RenderedOrNull(answer.Target) : Resolve(subsettedReference, ownerNamespace);

        /// <summary>
        /// Resolves the rendered box a usage reference-subsets (<c>::&gt;</c>). The semantic model
        /// records no edge for it, so the raw reference is resolved by name resolution relative to
        /// the feature's owning namespace. An unresolved reference yields null.
        /// </summary>
        public string? ResolveReferenceSubsetting(string reference, string? ownerNamespace) =>
            Resolve(reference, ownerNamespace);

        /// <summary>
        /// Resolves a raw reference to a rendered box; returns null when it resolves to nothing
        /// rendered.
        /// </summary>
        private string? Resolve(string reference, string? localNamespace) =>
            RenderedOrNull(_resolver.Resolve(reference, localNamespace));

        /// <summary>Returns <paramref name="qualifiedName"/> when it names a rendered box, else null.</summary>
        private string? RenderedOrNull(string? qualifiedName) =>
            qualifiedName is not null && _rendered.Contains(qualifiedName) ? qualifiedName : null;

        /// <summary>Returns the owning namespace's qualified name, or null at the root.</summary>
        private static string? OwnerOf(string? qualifiedName)
        {
            var sep = qualifiedName?.LastIndexOf("::", StringComparison.Ordinal) ?? -1;
            return sep >= 0 ? qualifiedName![..sep] : null;
        }
    }

    /// <summary>
    /// Builds the single input <see cref="LayoutGraph"/>: each package becomes a folder container
    /// node holding its definitions as leaves (or, when depth-truncated, a single leaf ellipsis
    /// placeholder); top-level (unpackaged) definitions become leaves directly on the root graph.
    /// A package nested inside another package is drawn inside its owner's folder: folders are
    /// materialized parent-first, a nested package's folder node is added to its nearest enclosing
    /// folder's own <see cref="LayoutGraphNode.Children"/> scope, and an enclosing package that
    /// owns no definitions directly still gets its folder whenever one of its nested packages is
    /// drawn. A definition that owns nested definitions (<paramref name="childrenByParent"/>) becomes a
    /// genuine container in its own right — its nested definitions are placed inside its own node's
    /// <see cref="LayoutGraphNode.Children"/> scope (recursively, to any depth) via
    /// <see cref="PlaceDef"/>, instead of being handed to <see cref="GroupByPackage"/> a second time
    /// under the parent's qualified name, which is what used to render the parent definition twice
    /// (once as an empty leaf inside its own package folder, once as a duplicate sibling folder
    /// holding its children). When <paramref name="exposedPackages"/> is non-null (the view is
    /// narrowed by a resolved <c>expose</c> scope), only a package that is itself exposed (SysML
    /// 8.3.26.3: <c>expose P;</c> or <c>expose P::**;</c>) becomes a package node — an empty leaf
    /// package node when it has neither exposed members nor exposed nested packages — while a
    /// package that is only an ancestor of admitted content is never rendered as a wrapping
    /// folder: its items are promoted directly onto the root graph instead, exactly like top-level
    /// (unpackaged) definitions, and a nested exposed package attaches to its nearest exposed
    /// enclosing package (or to the root when none is exposed). When <paramref name="exposedPackages"/>
    /// is null, every package that owns definitions, together with every enclosing package
    /// recognized through <paramref name="packageNames"/>, becomes a folder. Model edges are added
    /// once every definition's node placement is known: each edge is declared in the scope at the
    /// lowest common ancestor of its two endpoints' scopes (see <see cref="LowestCommonScope"/>),
    /// referencing the endpoint nodes directly even when they sit several containers deep, so the
    /// layout engine routes it between the actual endpoint boxes — across sibling packages,
    /// between a nested package and its owner, and across any depth of nesting. An edge touching a
    /// depth-truncated (unrendered) definition has no node to reference and is dropped.
    /// <paramref name="depthLimit"/> caps nesting depth uniformly whether the extra nesting comes
    /// from a package folder or from a definition containing another definition: a top-level
    /// folder's contents and top-level/scoped-promoted items placed directly on the root sit at depth
    /// 1, each enclosing folder adds one further level (a nested folder's contents sit at depth 2,
    /// and so on), and every level of nested-definition containment <see cref="PlaceDef"/>
    /// recurses into is one depth deeper again — see <see cref="PlaceDef"/>'s own remarks for how that
    /// deeper truncation is applied. A truncated folder is a single leaf placeholder showing an
    /// ellipsis (<c>…</c>).
    /// </summary>
    /// <param name="groups">The package groups (package qualified name and its directly owned definitions).</param>
    /// <param name="childrenByParent">Definitions nested inside a box, keyed by the owning box's qualified name.</param>
    /// <param name="usagesByOwner">Usage nodes keyed by the qualified name of the box that owns them; each is placed beside its owner.</param>
    /// <param name="modelEdges">The model relationships to draw as graph edges.</param>
    /// <param name="theme">The theme used to size title bands and ellipsis placeholders.</param>
    /// <param name="depthLimit">The nesting depth limit, or a non-positive value for none.</param>
    /// <param name="exposedPackages">The exposed packages of a scoped view, or null for an unscoped view.</param>
    /// <param name="packageNames">Every package the workspace declares, used to recognize enclosing packages.</param>
    /// <param name="itemOrder">
    /// Optional per-folder item order (package qualified name to the folder's definition qualified
    /// names in the wanted order) produced by <see cref="ComputeItemOrder"/>; null keeps declaration
    /// order. A folder or name it does not know is ignored.
    /// </param>
    /// <param name="deferred">
    /// Optional qualified names of leaf definitions directly held by a folder (or the root) that are
    /// left out of the graph, so <see cref="TryTuckUnconnected"/> can place them in the free space
    /// of the finished drawing instead; null defers nothing.
    /// </param>
    /// <returns>The graph and the bookkeeping for decoration and order refinement.</returns>
    private static GraphBuild BuildGraph(
        IReadOnlyList<(string Package, List<DefBox> Items)> groups,
        IReadOnlyDictionary<string, List<DefBox>> childrenByParent,
        IReadOnlyDictionary<string, List<DefBox>> usagesByOwner,
        IReadOnlyList<ModelEdge> modelEdges,
        Theme theme,
        int depthLimit,
        IReadOnlySet<string>? exposedPackages,
        IReadOnlySet<string> packageNames,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? itemOrder,
        IReadOnlySet<string>? deferred = null)
    {
        var graph = new LayoutGraph();
        var rootScope = new ScopeInfo(null, graph);

        // The General View intentionally draws one edge per source model relationship even when
        // several distinct edges share the same source and target node (for example two attributes
        // of the same type, or a redefinition edge that happens to coincide with another edge
        // between the same two definitions) — unlike InterconnectionViewLayoutStrategy, this graph
        // has never relied on the bundled algorithm's parallel-edge merging, so it opts out
        // explicitly to keep every distinct model relationship visible regardless of the layered
        // algorithm's own default. The option cascades from this root to every nested folder and
        // definition-container scope (edges are declared in those scopes too), so it is set once here.
        graph.Set(CoreOptions.MergeParallelEdges, false);

        // The graph flows top-to-bottom, and every layout edge runs from the element that is drawn
        // above to the element drawn below: owner → owned usage, general → specific, subsetted →
        // subsetting, requirement → satisfier, imported → importing, and usage → typing definition.
        // So the owner, supertype and general element read on top, as in the OMG Pilot
        // implementation and the SysML v2 training material, and the definition typing a usage hangs
        // below it. The bundled layered algorithm assigns layers by longest path from the sources, so
        // with every edge declared this way each element hangs directly below the top-most element
        // that reaches it. Edges whose arrow points up are declared reversed and turned back after the
        // layout (see OrientEdges). The direction cascades from this root to every nested folder and
        // definition-container scope.
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Down);
        var truncated = new List<TruncatedContainer>();

        // Reserve the title area (the package name only — the specification draws a package with no
        // «package» keyword) above a folder's contents so the label never overlaps the first child
        // box; the renderer draws the smaller tab notch within.
        var folderTitleHeight = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: false);

        // Folder contents (and top-level or scoped-promoted items placed directly on the root) sit at
        // depth 1 - the baseline every PlaceDef call below starts from; each enclosing folder adds
        // one level, and nested-definition containment recursion goes one depth deeper per level.
        const int rootDepth = 1;

        // Every located definition's node and the scope directly holding it, so edges can be scoped
        // to their lowest common ancestor.
        var located = new Dictionary<string, Location>(StringComparer.Ordinal);

        var (plans, steps) = PlanFolders(groups, exposedPackages, packageNames);

        // Each materialized (non-truncated, non-leaf) folder's own child scope, keyed by package
        // name. A folder absent from this map is a truncated placeholder (or hidden beneath one).
        var folderScopes = new Dictionary<string, ScopeInfo>(StringComparer.Ordinal);

        foreach (var (folderKey, groupIndex) in steps)
        {
            if (folderKey is null)
            {
                // A top-level (unpackaged) item, or an item of a package the scoped view does not
                // expose: promoted directly onto the root graph through PlaceDef so any of its own
                // nested-definition children are still handled.
                foreach (var def in groups[groupIndex].Items)
                {
                    if (deferred?.Contains(def.QualifiedName) == true)
                    {
                        continue;
                    }

                    PlaceDef(def, rootScope, childrenByParent, usagesByOwner, located, theme, rootDepth, depthLimit);
                }

                continue;
            }

            var plan = plans[folderKey];
            ScopeInfo parentScope;
            if (plan.ParentKey is null)
            {
                parentScope = rootScope;
            }
            else if (!folderScopes.TryGetValue(plan.ParentKey, out parentScope!))
            {
                // The enclosing folder is a truncated placeholder: this folder was already counted
                // in its hidden-element total and is not drawn.
                continue;
            }

            var label = SimplePackageName(folderKey);
            if (plan.Items.Count == 0 && plan.ChildKeys.Count == 0)
            {
                // An exposed package with no exposed members (checked before depth truncation,
                // which has nothing to hide here): a leaf package node (name inside) sized to its
                // label, since an empty container has no contents to size it.
                var width = Math.Max(MinBoxWidth, (label.Length * theme.FontSizeTitle * CharWidthFactor) + (4.0 * theme.LabelPadding));
                var packageNode = parentScope.Graph.AddNode($"folder:{folderKey}", width, folderTitleHeight + theme.FontSizeTitle + (2.0 * theme.LabelPadding));
                packageNode.Label = label;
                packageNode.Shape = BoxShape.Folder;
                located[FolderKeyPrefix + folderKey] = new Location(packageNode, parentScope);
                continue;
            }

            // The depth of this folder's own contents: one level per enclosing folder, plus one.
            var contentDepth = rootDepth + plan.NestingLevel;
            if (depthLimit > 0 && depthLimit <= contentDepth)
            {
                // Replace the folder's contents (definitions and nested packages) with a single leaf
                // ellipsis indicator. It stays a leaf (its Children graph is never touched) so the
                // hierarchical engine keeps this caller-computed size rather than auto-sizing it as
                // a container.
                var (ellipsisWidth, ellipsisHeight) = ComputeEllipsisSize(theme);
                var placeholder = parentScope.Graph.AddNode($"folder:{folderKey}", ellipsisWidth, folderTitleHeight + ellipsisHeight);
                placeholder.Label = label;
                placeholder.Shape = BoxShape.Folder;
                located[FolderKeyPrefix + folderKey] = new Location(placeholder, parentScope);
                truncated.Add(new TruncatedContainer(placeholder, plan.SubtreeCount, folderTitleHeight));

                // None of the folder's contents receive a node: every edge touching one is dropped.
                continue;
            }

            var folder = parentScope.Graph.AddNode($"folder:{folderKey}", 0, 0);
            folder.Label = label;
            folder.Shape = BoxShape.Folder;

            // The package name goes in the folder's tab (a package with content, per the
            // specification's package-with-name-in-tab); the engine reserves the tab's own height,
            // so no title band (TitleHeight) is requested on top of it.
            folder.Appearance = new BoxAppearance(FolderTitle: FolderTitlePlacement.Tab);
            // Each folder's contents use the "auto" algorithm: the layered algorithm places the
            // connected groups, while unconnected definitions are packed by the containment
            // algorithm. (A folder pinned to the layered algorithm with the bottom-to-top direction
            // was observed to stack several unconnected nodes of one layer on top of each other, see
            // TopDown_UnconnectedDefinitionsDoNotOverlap, so the automatic selection is kept.)
            folder.Set(CoreOptions.Algorithm, AutoLayoutAlgorithm.AlgorithmId);
            var folderScope = new ScopeInfo(parentScope, folder.Children, folder);
            folderScopes[folderKey] = folderScope;
            located[FolderKeyPrefix + folderKey] = new Location(folder, parentScope);

            foreach (var def in OrderedItems(plan, itemOrder))
            {
                if (deferred?.Contains(def.QualifiedName) == true)
                {
                    continue;
                }

                PlaceDef(def, folderScope, childrenByParent, usagesByOwner, located, theme, contentDepth, depthLimit);
            }
        }

        // Add every model edge whose endpoints both received a node, declared in the scope at the
        // lowest common ancestor of the two endpoints' scopes. Both endpoints may be nested several
        // containers below that scope; the layout engine routes the edge between the endpoint nodes
        // themselves. An edge between a container and one of its own descendants has the
        // container's own enclosing scope as its lowest common ancestor and is not drawn by the
        // engine (the containment is already visible).
        var edgeId = 0;
        foreach (var edge in modelEdges)
        {
            if (!located.TryGetValue(edge.SourceQualified, out var source) ||
                !located.TryGetValue(edge.TargetQualified, out var target))
            {
                continue;
            }

            // Declare the edge from the element drawn above to the element drawn below (see the
            // direction comment above): a typing edge keeps its direction (usage → definition),
            // every other directed edge is declared reversed, from its drawn target to its drawn
            // source, with the marker still on the layout target. OrientEdges turns each reversed
            // connector back so the marker ends up at the drawn target. An undirected edge
            // (connect, bind) keeps its direction.
            var scopeOfEdge = LowestCommonScope(source.Scope, target.Scope).Graph;
            var added = IsDeclaredReversed(edge)
                ? scopeOfEdge.AddEdge($"e{edgeId++}", target.Node, source.Node)
                : scopeOfEdge.AddEdge($"e{edgeId++}", source.Node, target.Node);
            added.TargetEnd = edge.Arrowhead;
            added.LineStyle = LineStyleForKind(edge.Kind);
            added.Label = edge.Label;
        }

        return new GraphBuild(graph, truncated, located, plans, usagesByOwner);
    }

    /// <summary>
    /// Returns a folder's items in the order the layout engine should receive them: the order named
    /// by <paramref name="itemOrder"/> for the folder when one exists, otherwise declaration order.
    /// Names the folder does not own are ignored and items the order omits keep their relative
    /// declaration order after the named ones, so a stale or partial order can never drop a
    /// definition from the drawing.
    /// </summary>
    private static IReadOnlyList<DefBox> OrderedItems(FolderPlan plan, IReadOnlyDictionary<string, IReadOnlyList<string>>? itemOrder)
    {
        if (itemOrder is null || !itemOrder.TryGetValue(plan.Key, out var wanted))
        {
            return plan.Items;
        }

        var byName = plan.Items.ToDictionary(d => d.QualifiedName, StringComparer.Ordinal);
        var names = wanted.Where(byName.ContainsKey).Distinct(StringComparer.Ordinal).ToList();
        var ordered = names.Select(name => byName[name]).ToList();
        var named = names.ToHashSet(StringComparer.Ordinal);
        ordered.AddRange(plan.Items.Where(d => !named.Contains(d.QualifiedName)));
        return ordered;
    }

    /// <summary>
    /// Returns the innermost scope that encloses both <paramref name="a"/> and <paramref name="b"/>
    /// (a scope encloses itself). Both scopes must belong to the same scope tree, so the root scope
    /// is always a common ancestor.
    /// </summary>
    private static ScopeInfo LowestCommonScope(ScopeInfo a, ScopeInfo b)
    {
        var ancestorsOfA = new HashSet<ScopeInfo>(ReferenceEqualityComparer.Instance);
        for (var scope = a; scope is not null; scope = scope.Parent)
        {
            ancestorsOfA.Add(scope);
        }

        var common = b;
        while (!ancestorsOfA.Contains(common))
        {
            common = common.Parent ?? throw new InvalidOperationException("Scopes belong to different scope trees.");
        }

        return common;
    }

    /// <summary>Returns the qualified name before the last <c>::</c> of <paramref name="qualifiedName"/>, or null when it has no separator.</summary>
    private static string? EnclosingPackage(string qualifiedName)
    {
        var separator = qualifiedName.LastIndexOf("::", StringComparison.Ordinal);
        return separator > 0 ? qualifiedName[..separator] : null;
    }

    /// <summary>
    /// Plans the package folders before any graph node exists: which packages become folders (every
    /// package that owns drawn definitions, plus every enclosing package recognized through
    /// <paramref name="packageNames"/> or, in a scoped view, <paramref name="exposedPackages"/>), how
    /// they nest, and the order in which they and the promoted (non-folder) groups are materialized
    /// — an enclosing folder always precedes everything nested in it.
    /// </summary>
    /// <returns>
    /// The plans keyed by package name, and the ordered materialization steps: a non-null folder key,
    /// or a null folder key plus the index of a <paramref name="groups"/> entry whose items are placed
    /// directly on the root scope.
    /// </returns>
    private static (Dictionary<string, FolderPlan> Plans, List<(string? FolderKey, int GroupIndex)> Steps) PlanFolders(
        IReadOnlyList<(string Package, List<DefBox> Items)> groups,
        IReadOnlySet<string>? exposedPackages,
        IReadOnlySet<string> packageNames)
    {
        // Packages that may serve as an enclosing folder: the exposed ones in a scoped view,
        // otherwise every package the workspace declares.
        var eligibleAncestors = exposedPackages ?? packageNames;

        // Pass 1: decide the folder set and its order (ancestors first).
        var itemsByFolder = new Dictionary<string, IReadOnlyList<DefBox>>(StringComparer.Ordinal);
        var folderOrder = new List<string>();
        var steps = new List<(string? FolderKey, int GroupIndex)>();

        for (var g = 0; g < groups.Count; g++)
        {
            var (package, items) = groups[g];
            if (string.IsNullOrEmpty(package) || (exposedPackages is not null && !exposedPackages.Contains(package)))
            {
                steps.Add((null, g));
                continue;
            }

            // Each enclosing package that is eligible gets its folder (outermost first) before the
            // first thing nested in it, even when it owns no definitions directly.
            var separator = package.IndexOf("::", StringComparison.Ordinal);
            while (separator > 0)
            {
                var ancestor = package[..separator];
                if (eligibleAncestors.Contains(ancestor) && !itemsByFolder.ContainsKey(ancestor))
                {
                    itemsByFolder[ancestor] = [];
                    folderOrder.Add(ancestor);
                    steps.Add((ancestor, g));
                }

                separator = package.IndexOf("::", separator + 2, StringComparison.Ordinal);
            }

            // A folder first added as a pure ancestor takes its own items here, keeping its position.
            var firstAdded = !itemsByFolder.ContainsKey(package);
            itemsByFolder[package] = items;
            if (firstAdded)
            {
                folderOrder.Add(package);
                steps.Add((package, g));
            }
        }

        // Pass 2: nest each folder in its nearest enclosing folder.
        var plans = new Dictionary<string, FolderPlan>(StringComparer.Ordinal);
        foreach (var key in folderOrder)
        {
            var parentKey = EnclosingPackage(key);
            while (parentKey is not null && !plans.ContainsKey(parentKey))
            {
                parentKey = EnclosingPackage(parentKey);
            }

            var plan = new FolderPlan(key, parentKey, itemsByFolder[key]);
            if (parentKey is not null)
            {
                plans[parentKey].ChildKeys.Add(key);
                plan.NestingLevel = plans[parentKey].NestingLevel + 1;
            }

            plans[key] = plan;
        }

        // Pass 3: hidden-element counts, children before parents (reverse materialization order).
        for (var i = folderOrder.Count - 1; i >= 0; i--)
        {
            var plan = plans[folderOrder[i]];
            plan.SubtreeCount = plan.Items.Count + plan.ChildKeys.Sum(child => 1 + plans[child].SubtreeCount);
        }

        return (plans, steps);
    }

    /// <summary>
    /// Places a single definition's node into <paramref name="scope"/>, then recursively
    /// places every nested definition that owns <paramref name="def"/> as its immediate parent (see
    /// <paramref name="childrenByParent"/>) inside <paramref name="def"/>'s own node's
    /// <see cref="LayoutGraphNode.Children"/> scope — to any nesting depth, one level per recursive
    /// call — instead of rendering the parent definition a second time as a duplicate sibling
    /// folder. When <paramref name="def"/> owns nested definitions, its node's
    /// <see cref="LayoutGraphNode.TitleHeight"/> is set to its own already-computed
    /// <see cref="DefBox.Height"/> (which already includes the title band and every compartment row
    /// for its own owned usages), reserving exactly that band above where its nested children begin.
    /// <paramref name="depth"/> is <paramref name="def"/>'s own nesting depth, using the same
    /// numbering <see cref="BuildGraph"/> already uses for package-folder contents (a top-level or
    /// folder-scoped definition is depth 1); nested-definition containment is one depth deeper per
    /// recursive call, so <paramref name="def"/>'s immediate nested children sit at
    /// <c>depth + 1</c>. When <paramref name="depthLimit"/> is positive and does not permit that
    /// next depth, the children are <em>not</em> recursed into or rendered as boxes at all:
    /// <paramref name="def"/> instead stays a leaf node whose last compartment is the
    /// specification's trailing untitled <c>…</c> row (see <see cref="WithTruncationRow"/>), so
    /// <see cref="RenderOptions.DepthLimit"/> caps nested-definition containment depth exactly as it
    /// already caps package-folder nesting. A truncated definition creates no child scope (nothing
    /// is ever placed inside it, so no intra-container edge could ever resolve there); an edge touching
    /// one of the hidden children has no <paramref name="located"/>
    /// entry to reference and is dropped, exactly like a depth-truncated package folder's contents.
    /// Otherwise, a child <see cref="ScopeInfo"/> (enclosed by <paramref name="scope"/>) is created for
    /// its nested children, so an edge between them — or between one of them and any node elsewhere —
    /// resolves to the lowest common scope rather than always the root.
    /// </summary>
    private static void PlaceDef(
        DefBox def,
        ScopeInfo scope,
        IReadOnlyDictionary<string, List<DefBox>> childrenByParent,
        IReadOnlyDictionary<string, List<DefBox>> usagesByOwner,
        Dictionary<string, Location> located,
        Theme theme,
        int depth,
        int depthLimit)
    {
        var childDepth = depth + 1;
        var hasChildren = childrenByParent.TryGetValue(def.QualifiedName, out var children);
        var hasUsages = usagesByOwner.TryGetValue(def.QualifiedName, out var usages);

        // When the depth limit forbids def's nested children (nested definitions and the usages it
        // owns) from being rendered at all, def stays a leaf whose last compartment is the
        // specification's trailing "…" row (see WithTruncationRow) instead of becoming a container
        // around a placeholder box. None of the hidden children receive a node, so every edge
        // touching one is dropped.
        var isTruncated = (hasChildren || hasUsages) && depthLimit > 0 && depthLimit <= childDepth;
        if (isTruncated)
        {
            def = WithTruncationRow(def, theme);
        }

        var node = MakeDefNode(scope.Graph, def);
        located[def.QualifiedName] = new Location(node, scope);
        AddAnnotationNote(scope.Graph, def, node, theme);

        if (isTruncated)
        {
            return;
        }

        // The usages the box owns are placed in the box's own scope, beside it: an edge between a
        // container and one of its own descendants is not drawn, so a usage can never be nested in
        // the owner it is joined to.
        if (hasUsages)
        {
            foreach (var usage in usages!)
            {
                PlaceDef(usage, scope, childrenByParent, usagesByOwner, located, theme, childDepth, depthLimit);
            }
        }

        if (!hasChildren)
        {
            return;
        }

        node.TitleHeight = def.Height;
        var childScope = new ScopeInfo(scope, node.Children, node);

        foreach (var child in children!)
        {
            PlaceDef(child, childScope, childrenByParent, usagesByOwner, located, theme, childDepth, depthLimit);
        }
    }

    /// <summary>
    /// Computes the width/height of a truncated package folder's ellipsis (<c>…</c>) placeholder
    /// leaf body. The placeholder shows only the ellipsis glyph, so its size does not depend on how
    /// many elements were hidden; it is tall enough for the folder's name (centered in the body by
    /// the renderer) and the ellipsis below it to stay clear of each other.
    /// </summary>
    private static (double Width, double Height) ComputeEllipsisSize(Theme theme)
    {
        var height = (3.0 * theme.FontSizeTitle) + (2.0 * theme.LabelPadding);
        return (MinBoxWidth, height);
    }

    /// <summary>
    /// Returns <paramref name="def"/> with a trailing untitled <c>…</c> compartment row appended and
    /// its size recomputed to include that row — the specification's marker (the trailing
    /// <c>'…'?</c> of every compartment-contents production in the graphical BNF, clause 8.2.3)
    /// that the box has further contents which are not shown.
    /// </summary>
    private static DefBox WithTruncationRow(DefBox def, Theme theme)
    {
        IReadOnlyList<LayoutCompartment> compartments = [.. def.Compartments, new LayoutCompartment(null, ["\u2026"])];
        var (width, height) = ComputeBoxSize(def.Label, def.Keyword, compartments, theme);
        return def with { Compartments = compartments, Width = Math.Max(def.Width, width), Height = height };
    }

    /// <summary>Creates a definition leaf node in the given scope, carrying its keyword and compartments.</summary>
    private static LayoutGraphNode MakeDefNode(LayoutGraph scope, DefBox def)
    {
        var node = scope.AddNode(def.QualifiedName, def.Width, def.Height);
        node.Label = def.Label;

        // A usage is drawn with rounded corners and a definition with square corners (SysML v2
        // graphical BNF, clause 8.2.3: definition nodes are plain rectangles, usage nodes rounded).
        node.Shape = def.IsUsage ? BoxShape.RoundedRectangle : BoxShape.Rectangle;
        node.Keyword = def.Keyword;
        node.Compartments = def.Compartments;
        if (def.Appearance is { } appearance)
        {
            node.Appearance = appearance;
        }

        return node;
    }

    /// <summary>
    /// Emits a companion <see cref="BoxShape.Note"/> node (plus a plain connecting edge) for a
    /// definition that carries explicit <c>comment</c> annotations (<c>doc</c> annotations are never
    /// drawn as notes — see <see cref="BuildDocCompartment"/>), in the same scope the
    /// definition's own node was just added to (folder or root — mirrors the existing per-package
    /// folder scoping already used for def nodes and model edges). Implemented as an ordinary
    /// <see cref="LayoutGraph"/> node/edge (rather than a post-layout coordinate-decoration pass
    /// like <see cref="DecorateTruncated(LayoutTree, LayoutGraph, IReadOnlyList{TruncatedContainer}, Theme)"/>) so the existing
    /// <see cref="LayeredLayoutAlgorithm"/>/<see cref="ContainmentLayoutAlgorithm"/> can position
    /// and route it automatically — a deliberate departure from the ROADMAP's suggested "adapt
    /// <c>StateTransitionViewLayoutStrategy.AddInitialMarker</c>'s marker-plus-line pattern": that
    /// pattern manipulates raw <c>Rect</c>/<c>LayoutNode</c> coordinates after a simpler,
    /// non-graph-based layout pass specific to state diagrams, which does not exist in this
    /// strategy's architecture (a single <see cref="LayoutGraph"/> handed once to
    /// <see cref="LayoutAlgorithmBase.Apply(LayoutGraph)"/>). Emitting the note as an ordinary graph
    /// node/edge is consistent with how every other box/edge in this file is already produced, and
    /// lower-risk than hand-computing satellite coordinates in a graph-based layout model that was
    /// not designed for post-hoc absolute placement. Does nothing when the definition has no
    /// annotations.
    /// </summary>
    private static void AddAnnotationNote(LayoutGraph scope, DefBox def, LayoutGraphNode defNode, Theme theme)
    {
        if (def.Annotations.Count == 0)
        {
            return;
        }

        var text = BuildNoteText(def.Annotations);
        var (width, height, lines) = ComputeNoteBoxSize(text, theme);

        var note = scope.AddNode($"note:{def.QualifiedName}", width, height);
        note.Shape = BoxShape.Note;
        note.Compartments = [new LayoutCompartment(null, lines)];

        var edge = scope.AddEdge($"note-edge:{def.QualifiedName}", defNode, note);
        edge.TargetEnd = EndMarkerStyle.None;
        edge.LineStyle = LineStyle.Dashed;
    }

    /// <summary>
    /// Combines an element's comment annotations into a single note's text: one note
    /// box per annotated element, not one per annotation, to avoid uncontrolled box proliferation
    /// for well-annotated elements — matches this renderer's existing "one compartment per
    /// keyword, not per feature" aggregation philosophy already used in <see cref="BuildCompartments"/>.
    /// Multiple annotations are concatenated with a blank-line separator.
    /// </summary>
    private static string BuildNoteText(IReadOnlyList<SysmlAnnotation> annotations) =>
        string.Join("\n\n", annotations.Select(a => a.Text.Trim()));

    /// <summary>
    /// Computes the intrinsic size of a note box from its already-combined text (see
    /// <see cref="BuildNoteText"/>), analogous to <see cref="ComputeBoxSize"/> but simpler — a
    /// note has no title/keyword band, only its own body text rendered as a single untitled
    /// compartment's rows. Sizing splits on the text's own newlines only; no word-wrap algorithm
    /// is applied (this codebase's <c>DemaConsulting.Rendering</c> dependency has no
    /// text-measurement primitive supporting wrapping elsewhere either), so an unusually long
    /// single line renders wide — an accepted minimal-implementation limitation.
    /// </summary>
    private static (double Width, double Height, IReadOnlyList<string> Lines) ComputeNoteBoxSize(string text, Theme theme)
    {
        var lines = text.Split('\n');

        var width = MinBoxWidth;
        foreach (var line in lines)
        {
            width = Math.Max(width, (line.Length * theme.FontSizeBody * CharWidthFactor) + (3.0 * theme.LabelPadding));
        }

        var height = BoxMetrics.TitleAreaHeight(theme, hasLabel: false, hasKeyword: false)
            + theme.LabelPadding
            + (lines.Length * (theme.LabelPadding + theme.FontSizeBody))
            + theme.LabelPadding;

        return (width, height, lines);
    }

    /// <summary>
    /// Replaces each truncated package folder's placed box with one carrying its
    /// ellipsis (<c>…</c>) label, positioned within the box's now-known absolute placement. Recurses
    /// into every placed container box's own <see cref="LayoutBox.Children"/> against its matching
    /// <see cref="LayoutGraphNode.Children"/>'s <see cref="LayoutGraph.Nodes"/>, so a truncated
    /// folder nested at any depth — inside another folder, inside a definition, or both — is
    /// found and decorated exactly like a root-level truncated folder, not just the ones directly on
    /// the root graph.
    /// </summary>
    private static LayoutTree DecorateTruncated(
        LayoutTree tree,
        LayoutGraph graph,
        IReadOnlyList<TruncatedContainer> truncated,
        Theme theme)
    {
        var hiddenByNode = truncated.ToDictionary(t => t.Node, t => (t.HiddenCount, t.TitleOffset));
        var nodes = DecorateTruncated(tree.Nodes, graph.Nodes, hiddenByNode, theme);
        return tree with { Nodes = nodes };
    }

    /// <summary>
    /// Recursive worker for <see cref="DecorateTruncated(LayoutTree, LayoutGraph, IReadOnlyList{TruncatedContainer}, Theme)"/>:
    /// walks one graph scope's placed nodes and pairs each <see cref="LayoutBox"/> with the graph
    /// node of that same scope it was placed from. <see cref="LayoutBox"/> carries no <c>Id</c>, so
    /// the pairing is by the identity signature the strategy itself set on the graph node and the
    /// engine copies verbatim onto the box: <see cref="LayoutGraphNode.Label"/>,
    /// <see cref="LayoutGraphNode.Keyword"/> and <see cref="LayoutGraphNode.Shape"/>. Each box takes the
    /// first not-yet-paired graph node with an equal signature, so the pairing does not depend on the
    /// order in which the engine emits a scope's boxes, nor on where it intersperses
    /// <see cref="LayoutLine"/> entries for the scope's own routed edges. Nodes that share a
    /// signature (which only duplicated, never-truncated nodes can) pair in order, and a box with no
    /// signature match falls back to the first unpaired node. Decorates any box whose paired graph
    /// node is a recorded truncation and recurses into every other container box's own children.
    /// </summary>
    private static List<LayoutNode> DecorateTruncated(
        IReadOnlyList<LayoutNode> nodes,
        IReadOnlyList<LayoutGraphNode> graphNodes,
        IReadOnlyDictionary<LayoutGraphNode, (int HiddenCount, double TitleOffset)> hiddenByNode,
        Theme theme)
    {
        var result = new List<LayoutNode>(nodes);
        foreach (var (i, box, graphNode) in PairBoxesWithNodes(nodes, graphNodes))
        {
            if (hiddenByNode.TryGetValue(graphNode, out var hidden))
            {
                // The renderers anchor a centered label at its X coordinate, so X is the box's
                // horizontal center (anchoring at the left padding would hang half the glyph out of
                // the box). The folder's name is drawn centered in the body, so the ellipsis sits at
                // the bottom of the box, below the name, and never above the reserved title band.
                var indicator = new LayoutLabel(
                    X: box.X + (box.Width / 2.0),
                    Y: Math.Max(
                        box.Y + hidden.TitleOffset + theme.LabelPadding + (theme.FontSizeTitle / 2.0),
                        box.Y + box.Height - theme.LabelPadding - (theme.FontSizeTitle / 2.0)),
                    MaxWidth: box.Width - (2.0 * theme.LabelPadding),
                    Text: "\u2026",
                    Align: TextAlign.Center,
                    Weight: FontWeight.Regular,
                    Style: FontStyle.Normal,
                    FontSize: theme.FontSizeTitle);

                result[i] = box with { Children = [indicator] };
                continue;
            }

            if (graphNode.HasChildren && box.Children.Count > 0)
            {
                var decoratedChildren = DecorateTruncated(box.Children, graphNode.Children.Nodes, hiddenByNode, theme);
                result[i] = box with { Children = decoratedChildren };
            }
        }

        return result;
    }

    /// <summary>
    /// Pairs every <see cref="LayoutBox"/> of one placed scope with the graph node of that same
    /// scope it was placed from, yielding the box's index in <paramref name="nodes"/>, the box, and
    /// its node. <see cref="LayoutBox"/> carries no <c>Id</c>, so the pairing is by the identity
    /// signature the strategy itself set on the graph node and the engine copies verbatim onto the
    /// box (see <see cref="FindUnpairedGraphNode"/>); it is shared by the truncation decoration and
    /// the item-order refinement so both agree on which node a box is. Non-box entries (routed lines)
    /// and boxes with no node left to pair are skipped.
    /// </summary>
    private static IEnumerable<(int Index, LayoutBox Box, LayoutGraphNode Node)> PairBoxesWithNodes(
        IReadOnlyList<LayoutNode> nodes,
        IReadOnlyList<LayoutGraphNode> graphNodes)
    {
        var paired = new bool[graphNodes.Count];
        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not LayoutBox box)
            {
                continue;
            }

            var graphIndex = FindUnpairedGraphNode(box, graphNodes, paired);
            if (graphIndex < 0)
            {
                continue;
            }

            paired[graphIndex] = true;
            yield return (i, box, graphNodes[graphIndex]);
        }
    }

    /// <summary>
    /// Returns the index of the first graph node not yet <paramref name="paired"/> whose
    /// <see cref="LayoutGraphNode.Label"/>, <see cref="LayoutGraphNode.Keyword"/> and
    /// <see cref="LayoutGraphNode.Shape"/> equal <paramref name="box"/>'s; when none matches, the
    /// index of the first unpaired node; or -1 when every node is already paired.
    /// </summary>
    private static int FindUnpairedGraphNode(LayoutBox box, IReadOnlyList<LayoutGraphNode> graphNodes, bool[] paired)
    {
        var firstUnpaired = -1;
        for (var j = 0; j < graphNodes.Count; j++)
        {
            if (paired[j])
            {
                continue;
            }

            firstUnpaired = firstUnpaired < 0 ? j : firstUnpaired;
            var candidate = graphNodes[j];
            if (candidate.Label == box.Label && candidate.Keyword == box.Keyword && candidate.Shape == box.Shape)
            {
                return j;
            }
        }

        return firstUnpaired;
    }

    /// <summary>The space kept between the drawing's outermost connector and the canvas edge.</summary>
    private const double CanvasMargin = 8.0;

    /// <summary>
    /// Grows and shifts the canvas so that every connector lies inside it. The engine can route a
    /// connector that joins two root-level folders through a channel
    /// outside the drawing (negative coordinates) and reports the node bounds only, so the renderer
    /// would clip that connector (seen with the bottom-to-top direction). When any waypoint lies outside the canvas the whole tree is
    /// translated so the leftmost/topmost waypoint sits <see cref="CanvasMargin"/> inside the canvas,
    /// and the canvas is widened/heightened to hold the rightmost/lowest waypoint; a tree whose
    /// connectors are all inside is returned unchanged.
    /// </summary>
    /// <param name="tree">The placed tree.</param>
    /// <returns>The tree with every connector inside its canvas.</returns>
    private static LayoutTree FitConnectorsToCanvas(LayoutTree tree)
    {
        double minX = 0.0, minY = 0.0, maxX = tree.Width, maxY = tree.Height;
        foreach (var point in CollectNodeLines(tree.Nodes).SelectMany(line => line.Waypoints))
        {
            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
        }

        var dx = minX < 0.0 ? CanvasMargin - minX : 0.0;
        var dy = minY < 0.0 ? CanvasMargin - minY : 0.0;
        var width = maxX > tree.Width ? maxX + dx + CanvasMargin : tree.Width + dx;
        var height = maxY > tree.Height ? maxY + dy + CanvasMargin : tree.Height + dy;
        if (minX >= 0.0 && minY >= 0.0 && maxX <= tree.Width && maxY <= tree.Height)
        {
            return tree;
        }

        return tree with
        {
            Width = width,
            Height = height,
            Nodes = [.. tree.Nodes.Select(node => Translate(node, dx, dy))],
        };
    }

    /// <summary>Collects every <see cref="LayoutLine"/> of a placed tree, at any depth.</summary>
    private static IEnumerable<LayoutLine> CollectNodeLines(IReadOnlyList<LayoutNode> nodes)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case LayoutLine line:
                    yield return line;
                    break;
                case LayoutBox box:
                    foreach (var inner in CollectNodeLines(box.Children))
                    {
                        yield return inner;
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// Returns <paramref name="node"/> moved by (<paramref name="dx"/>, <paramref name="dy"/>); the
    /// General view places boxes, connectors and labels only, so every other node is returned as is.
    /// </summary>
    private static LayoutNode Translate(LayoutNode node, double dx, double dy) => node switch
    {
        LayoutBox box => box with
        {
            X = box.X + dx,
            Y = box.Y + dy,
            Children = [.. box.Children.Select(child => Translate(child, dx, dy))],
        },
        LayoutLine line => line with { Waypoints = [.. line.Waypoints.Select(p => new Point2D(p.X + dx, p.Y + dy))] },
        LayoutLabel label => label with { X = label.X + dx, Y = label.Y + dy },
        _ => node,
    };

    /// <summary>
    /// Returns whether <paramref name="edge"/> is declared to the layout engine from its drawn
    /// target to its drawn source (so the target is drawn above). Every directed edge is, except
    /// feature typing, whose definition is drawn below the usage; connect and bind edges have no
    /// direction and are not reversed.
    /// </summary>
    private static bool IsDeclaredReversed(ModelEdge edge) =>
        edge.Kind is not (EdgeKind.FeatureTyping or EdgeKind.Connect or EdgeKind.Binding);

    /// <summary>
    /// Turns every connector declared reversed (see <see cref="IsDeclaredReversed"/>) back to its
    /// drawn direction. A reversed connector starts at its drawn target and ends, carrying the end
    /// marker, at its drawn source; this reverses its waypoints so the marker sits at the drawn target.
    /// A connector declared in its drawn direction carries either no marker (connect, bind, note
    /// link) or the typing marker, which no reversed edge uses, so the marker identifies the
    /// connectors to reverse.
    /// </summary>
    /// <param name="tree">The placed tree.</param>
    /// <returns>The tree with every connector pointing from its drawn source to its drawn target.</returns>
    private static LayoutTree OrientEdges(LayoutTree tree) =>
        tree with { Nodes = [.. tree.Nodes.Select(OrientEdges)] };

    /// <summary>Reverses the reversed-declared connectors in <paramref name="node"/> and, recursively, in its children.</summary>
    private static LayoutNode OrientEdges(LayoutNode node) => node switch
    {
        LayoutBox box => box with { Children = [.. box.Children.Select(OrientEdges)] },
        LayoutLine line when line.TargetEnd is not (EndMarkerStyle.None or EndMarkerStyle.HollowTriangleTwoDots) => line with
        {
            Waypoints = [.. line.Waypoints.Reverse()],
        },
        _ => node,
    };

    /// <summary>Clearance, in logical pixels, kept between a tucked box and every other box, connector and label; also the cell size of the placement grid.</summary>
    private const double TuckClearance = 12.0;

    /// <summary>Gap, in logical pixels, between two boxes of one tucked arrangement.</summary>
    private const double TuckGap = 16.0;

    /// <summary>The most columns tried when arranging the tucked boxes of one scope.</summary>
    private const int MaxTuckColumns = 6;

    /// <summary>The step, in grid cells, between the positions tried for a tucked arrangement.</summary>
    private const int TuckStride = 3;

    /// <summary>A rectangle in absolute layout coordinates.</summary>
    private readonly record struct Rect(double X, double Y, double Width, double Height)
    {
        /// <summary>Gets the x coordinate of the right edge.</summary>
        public double Right => X + Width;

        /// <summary>Gets the y coordinate of the bottom edge.</summary>
        public double Bottom => Y + Height;
    }

    /// <summary>
    /// Places the definitions that no relationship touches in the free space of the drawing instead of
    /// in the engine's shelf of unconnected boxes. The engine packs every childless, edgeless box of a
    /// scope into one block beside the connected part, which in a wide tree leaves a large empty corner
    /// (diagram-quality B4/B5). The connected part is laid out again on its own, and each such box is then
    /// placed, as an arrangement of one to six columns in declaration order, in the position inside the
    /// scope's content bounds that leaves the smallest empty square and keeps a clearance to every box,
    /// connector and connector label.
    /// </summary>
    /// <remarks>
    /// The result is accepted only when every moved box fits inside the scope's existing content
    /// bounds (so no folder is resized and no sibling is disturbed) and the canvas is strictly smaller
    /// than the engine's own packing; a scope whose boxes do not fit keeps the engine's shelf (the
    /// connected part is laid out again without that scope's boxes deferred), and when nothing can be
    /// tucked the method returns null and the caller keeps the engine's layout unchanged. A scope with
    /// no connected content (every box isolated) is never touched.
    /// </remarks>
    /// <param name="tree">The engine's layout of the whole graph.</param>
    /// <param name="build">The graph that produced <paramref name="tree"/>.</param>
    /// <param name="rebuild">Builds the graph again with the given qualified names left out.</param>
    /// <param name="theme">The theme whose font metrics size the connector labels.</param>
    /// <returns>The tree with the unconnected boxes tucked in, or null when the engine's layout is kept.</returns>
    private static LayoutTree? TryTuckUnconnected(
        LayoutTree tree,
        GraphBuild build,
        Func<IReadOnlySet<string>, GraphBuild> rebuild,
        Theme theme)
    {
        var candidates = FindUnconnectedDefinitions(build);
        while (candidates.Count > 0)
        {
            var deferred = candidates.Values.SelectMany(names => names).ToHashSet(StringComparer.Ordinal);
            var third = rebuild(deferred);
            var thirdTree = LayOut(third.Graph);
            if (third.Truncated.Count > 0)
            {
                thirdTree = DecorateTruncated(thirdTree, third.Graph, third.Truncated, theme);
            }

            if (thirdTree.Width * thirdTree.Height >= tree.Width * tree.Height)
            {
                return null;
            }

            var failed = new List<ScopeInfo>();
            foreach (var (scope, names) in candidates)
            {
                var tucked = TuckScope(tree, build, thirdTree, third, scope, names, theme);
                if (tucked is null)
                {
                    failed.Add(scope);
                }
                else
                {
                    thirdTree = tucked;
                }
            }

            if (failed.Count == 0)
            {
                return thirdTree;
            }

            foreach (var scope in failed)
            {
                candidates.Remove(scope);
            }
        }

        return null;
    }

    /// <summary>
    /// Finds, per scope, the leaf definitions that no edge of the graph touches, in placement order.
    /// Only the scopes of the root and of package folders qualify, and only when the scope also holds
    /// connected content (a box or folder that an edge reaches), so a scope of isolated boxes keeps the
    /// engine's packing.
    /// </summary>
    private static Dictionary<ScopeInfo, List<string>> FindUnconnectedDefinitions(GraphBuild build)
    {
        var incident = new HashSet<ILayoutConnectable>(ReferenceEqualityComparer.Instance);
        CollectIncidentEnds(build.Graph, incident);

        var result = new Dictionary<ScopeInfo, List<string>>(ReferenceEqualityComparer.Instance);
        foreach (var (name, location) in build.Located)
        {
            var scope = location.Scope;
            var unconnectedLeaf = !name.StartsWith(FolderKeyPrefix, StringComparison.Ordinal)
                && !location.Node.HasChildren
                && !incident.Contains(location.Node);
            var inFolderOrRoot = scope.Owner is null || scope.Owner.Shape == BoxShape.Folder;
            if (!unconnectedLeaf || !inFolderOrRoot || !HasConnectedContent(scope.Graph, incident))
            {
                continue;
            }

            if (!result.TryGetValue(scope, out var names))
            {
                names = [];
                result[scope] = names;
            }

            names.Add(name);
        }

        return result;
    }

    /// <summary>Adds the source and target of every edge of <paramref name="graph"/> and of its nested graphs to <paramref name="incident"/>.</summary>
    private static void CollectIncidentEnds(LayoutGraph graph, HashSet<ILayoutConnectable> incident)
    {
        foreach (var edge in graph.Edges)
        {
            incident.Add(edge.Source);
            incident.Add(edge.Target);
        }

        foreach (var node in graph.Nodes.Where(n => n.HasChildren))
        {
            CollectIncidentEnds(node.Children, incident);
        }
    }

    /// <summary>Returns whether a node of <paramref name="graph"/>, or of a graph nested in it, is reached by an edge.</summary>
    private static bool HasConnectedContent(LayoutGraph graph, HashSet<ILayoutConnectable> incident) =>
        graph.Nodes.Any(node => incident.Contains(node) || (node.HasChildren && HasConnectedContent(node.Children, incident)));

    /// <summary>
    /// Tucks the boxes <paramref name="names"/> of <paramref name="scope"/> (placed in
    /// <paramref name="tree"/>, built from <paramref name="build"/>) into the free space of the same scope
    /// in <paramref name="thirdTree"/>; returns the new tree, or null when they do not fit.
    /// </summary>
    private static LayoutTree? TuckScope(
        LayoutTree tree,
        GraphBuild build,
        LayoutTree thirdTree,
        GraphBuild third,
        ScopeInfo scope,
        IReadOnlyList<string> names,
        Theme theme)
    {
        var boxes = new List<LayoutBox>();
        foreach (var name in names)
        {
            var box = FindPlacedBox(tree.Nodes, build.Graph.Nodes, build.Located[name].Node);
            if (box is null)
            {
                return null;
            }

            boxes.Add(box);
        }

        LayoutGraphNode? host = null;
        if (scope.Owner is not null)
        {
            var key = build.Located
                .Where(entry => entry.Key.StartsWith(FolderKeyPrefix, StringComparison.Ordinal) && ReferenceEquals(entry.Value.Node, scope.Owner))
                .Select(entry => entry.Key)
                .FirstOrDefault();
            if (key is null || !third.Located.TryGetValue(key, out var hostLocation))
            {
                return null;
            }

            host = hostLocation.Node;
        }

        var children = host is null ? thirdTree.Nodes : FindPlacedBox(thirdTree.Nodes, third.Graph.Nodes, host)?.Children;
        var region = children is null ? null : ContentBounds(children);
        if (children is null || region is null)
        {
            return null;
        }

        var positions = PlaceInFreeSpace(region.Value, CollectObstacles(thirdTree, children, theme), [.. boxes.Select(b => (b.Width, b.Height))]);
        if (positions is null)
        {
            return null;
        }

        var additions = new List<LayoutNode>();
        for (var i = 0; i < boxes.Count; i++)
        {
            additions.Add(Translate(boxes[i], positions[i].X - boxes[i].X, positions[i].Y - boxes[i].Y));
        }

        if (host is null)
        {
            return thirdTree with { Nodes = [.. thirdTree.Nodes, .. additions] };
        }

        var nodes = AddChildren(thirdTree.Nodes, third.Graph.Nodes, host, additions);
        return nodes is null ? null : thirdTree with { Nodes = nodes };
    }

    /// <summary>Returns the placed box of <paramref name="target"/> anywhere in <paramref name="nodes"/>, paired with the graph nodes of the same scope, or null.</summary>
    private static LayoutBox? FindPlacedBox(IReadOnlyList<LayoutNode> nodes, IReadOnlyList<LayoutGraphNode> graphNodes, LayoutGraphNode target)
    {
        foreach (var (_, box, node) in PairBoxesWithNodes(nodes, graphNodes))
        {
            if (ReferenceEquals(node, target))
            {
                return box;
            }

            if (node.HasChildren && box.Children.Count > 0)
            {
                var inner = FindPlacedBox(box.Children, node.Children.Nodes, target);
                if (inner is not null)
                {
                    return inner;
                }
            }
        }

        return null;
    }

    /// <summary>Returns <paramref name="nodes"/> with <paramref name="additions"/> appended to the children of the placed box of <paramref name="host"/>, or null when it is not found.</summary>
    private static List<LayoutNode>? AddChildren(
        IReadOnlyList<LayoutNode> nodes,
        IReadOnlyList<LayoutGraphNode> graphNodes,
        LayoutGraphNode host,
        IReadOnlyList<LayoutNode> additions)
    {
        foreach (var (index, box, node) in PairBoxesWithNodes(nodes, graphNodes))
        {
            List<LayoutNode>? children = null;
            if (ReferenceEquals(node, host))
            {
                children = [.. box.Children, .. additions];
            }
            else if (node.HasChildren && box.Children.Count > 0)
            {
                children = AddChildren(box.Children, node.Children.Nodes, host, additions);
            }

            if (children is not null)
            {
                var result = new List<LayoutNode>(nodes);
                result[index] = box with { Children = children };
                return result;
            }
        }

        return null;
    }

    /// <summary>Returns the bounds of the boxes and connector waypoints of <paramref name="children"/>, or null when there are none.</summary>
    private static Rect? ContentBounds(IReadOnlyList<LayoutNode> children)
    {
        var rects = children.OfType<LayoutBox>().Select(b => new Rect(b.X, b.Y, b.Width, b.Height)).ToList();
        rects.AddRange(CollectNodeLines(children).SelectMany(l => l.Waypoints).Select(p => new Rect(p.X, p.Y, 0.0, 0.0)));
        if (rects.Count == 0)
        {
            return null;
        }

        var left = rects.Min(r => r.X);
        var top = rects.Min(r => r.Y);
        return new Rect(left, top, rects.Max(r => r.Right) - left, rects.Max(r => r.Bottom) - top);
    }

    /// <summary>
    /// Returns the rectangles a tucked box must keep clear of: the boxes placed directly in
    /// <paramref name="children"/>, every connector segment of the tree and every connector label.
    /// </summary>
    private static List<Rect> CollectObstacles(LayoutTree tree, IReadOnlyList<LayoutNode> children, Theme theme)
    {
        var obstacles = children.OfType<LayoutBox>().Select(b => new Rect(b.X, b.Y, b.Width, b.Height)).ToList();
        var lines = CollectNodeLines(tree.Nodes).ToList();
        foreach (var waypoints in lines.Select(l => l.Waypoints))
        {
            for (var i = 1; i < waypoints.Count; i++)
            {
                var (a, b) = (waypoints[i - 1], waypoints[i]);
                obstacles.Add(new Rect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)));
            }
        }

        var labeled = lines.Where(l => l.MidpointLabel is not null).ToList();
        if (labeled.Count > 0)
        {
            var positions = ConnectorLabelPlacer.Place(labeled, theme.FontSizeBody);
            foreach (var line in labeled)
            {
                if (positions.TryGetValue(line, out var center))
                {
                    var width = (line.MidpointLabel!.Length * theme.FontSizeBody * 0.6) + (2.0 * theme.LabelPadding);
                    var height = theme.FontSizeBody + (2.0 * theme.LabelPadding);
                    obstacles.Add(new Rect(center.X - (width / 2.0), center.Y - (height / 2.0), width, height));
                }
            }
        }

        return obstacles;
    }

    /// <summary>
    /// Finds the top-left positions that place boxes of the given <paramref name="sizes"/>, arranged in
    /// columns in the given order, inside <paramref name="region"/> clear of every obstacle by
    /// <see cref="TuckClearance"/>. Among the arrangements of one to <see cref="MaxTuckColumns"/> columns
    /// and the positions on a grid of <see cref="TuckClearance"/> cells it picks the one that leaves the
    /// smallest empty square in the region, then the fewest empty squares of any size, then the position
    /// nearest the bottom-right corner. Returns null when no arrangement fits.
    /// </summary>
    private static List<Point2D>? PlaceInFreeSpace(Rect region, IReadOnlyList<Rect> obstacles, IReadOnlyList<(double Width, double Height)> sizes)
    {
        var nx = (int)Math.Ceiling(region.Width / TuckClearance);
        var ny = (int)Math.Ceiling(region.Height / TuckClearance);
        if (nx < 1 || ny < 1)
        {
            return null;
        }

        var occupied = new bool[nx * ny];
        foreach (var obstacle in obstacles)
        {
            MarkOccupied(occupied, nx, ny, region, obstacle);
        }

        var prefix = new int[(nx + 1) * (ny + 1)];
        for (var y = 0; y < ny; y++)
        {
            for (var x = 0; x < nx; x++)
            {
                prefix[((y + 1) * (nx + 1)) + x + 1] = (occupied[(y * nx) + x] ? 1 : 0) + prefix[(y * (nx + 1)) + x + 1] + prefix[((y + 1) * (nx + 1)) + x] - prefix[(y * (nx + 1)) + x];
            }
        }

        List<Point2D>? best = null;
        var bestScore = (Largest: int.MaxValue, Count: long.MaxValue, Corner: long.MaxValue);
        for (var columns = 1; columns <= Math.Min(sizes.Count, MaxTuckColumns); columns++)
        {
            var (offsets, blockWidth, blockHeight) = ArrangeInColumns(sizes, columns);
            var cw = (int)Math.Ceiling(blockWidth / TuckClearance);
            var ch = (int)Math.Ceiling(blockHeight / TuckClearance);
            foreach (var cy in GridSteps(ny - ch))
            {
                foreach (var cx in GridSteps(nx - cw))
                {
                    var fits = region.X + (cx * TuckClearance) + blockWidth <= region.Right + GeometryEpsilon
                        && region.Y + (cy * TuckClearance) + blockHeight <= region.Bottom + GeometryEpsilon
                        && RectangleSum(prefix, nx, cx, cy, cw, ch) == 0;
                    if (!fits)
                    {
                        continue;
                    }

                    var (largest, count) = MeasureEmptySquares(occupied, nx, ny, (cx - 1, cy - 1, cx + cw + 1, cy + ch + 1));
                    var corner = ((long)(nx - (cx + cw)) * (nx - (cx + cw))) + ((long)(ny - (cy + ch)) * (ny - (cy + ch)));
                    if ((largest, count, corner).CompareTo(bestScore) < 0)
                    {
                        bestScore = (largest, count, corner);
                        var origin = new Point2D(region.X + (cx * TuckClearance), region.Y + (cy * TuckClearance));
                        best = [.. offsets.Select(o => new Point2D(origin.X + o.X, origin.Y + o.Y))];
                    }
                }
            }
        }

        return best;
    }

    /// <summary>Yields 0, <see cref="TuckStride"/>, 2 × <see cref="TuckStride"/>, … up to <paramref name="max"/>, and <paramref name="max"/> itself.</summary>
    private static IEnumerable<int> GridSteps(int max)
    {
        if (max < 0)
        {
            yield break;
        }

        var step = 0;
        for (; step < max; step += TuckStride)
        {
            yield return step;
        }

        yield return max;
    }

    /// <summary>Marks the grid cells that <paramref name="obstacle"/>, inflated by <see cref="TuckClearance"/>, touches.</summary>
    private static void MarkOccupied(bool[] occupied, int nx, int ny, Rect region, Rect obstacle)
    {
        var x0 = Math.Max(0, (int)Math.Floor((obstacle.X - TuckClearance - region.X) / TuckClearance));
        var x1 = Math.Min(nx - 1, (int)Math.Ceiling((obstacle.Right + TuckClearance - region.X) / TuckClearance) - 1);
        var y0 = Math.Max(0, (int)Math.Floor((obstacle.Y - TuckClearance - region.Y) / TuckClearance));
        var y1 = Math.Min(ny - 1, (int)Math.Ceiling((obstacle.Bottom + TuckClearance - region.Y) / TuckClearance) - 1);
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                occupied[(y * nx) + x] = true;
            }
        }
    }

    /// <summary>Returns the number of occupied cells in the cell rectangle from (<paramref name="cx"/>, <paramref name="cy"/>) of the given size, using the prefix sums.</summary>
    private static int RectangleSum(int[] prefix, int nx, int cx, int cy, int cw, int ch)
    {
        var stride = nx + 1;
        return prefix[((cy + ch) * stride) + cx + cw] - prefix[(cy * stride) + cx + cw] - prefix[((cy + ch) * stride) + cx] + prefix[(cy * stride) + cx];
    }

    /// <summary>
    /// Returns the side, in cells, of the largest empty square of the grid and the number of empty
    /// squares of every size, with the cells of <paramref name="extra"/> (left, top, right and bottom
    /// cell, the right and bottom ones exclusive) counted as occupied.
    /// </summary>
    private static (int Largest, long Count) MeasureEmptySquares(bool[] occupied, int nx, int ny, (int Left, int Top, int Right, int Bottom) extra)
    {
        var previous = new int[nx + 1];
        var current = new int[nx + 1];
        var largest = 0;
        long count = 0;
        for (var y = 0; y < ny; y++)
        {
            for (var x = 0; x < nx; x++)
            {
                var blocked = occupied[(y * nx) + x] || (x >= extra.Left && x < extra.Right && y >= extra.Top && y < extra.Bottom);
                var side = blocked ? 0 : 1 + Math.Min(Math.Min(previous[x], previous[x + 1]), current[x]);
                current[x + 1] = side;
                largest = Math.Max(largest, side);
                count += side;
            }

            (previous, current) = (current, previous);
        }

        return (largest, count);
    }

    /// <summary>
    /// Arranges boxes of the given <paramref name="sizes"/> in <paramref name="columns"/> columns in
    /// reading order, each box at the top-left of its grid cell, with <see cref="TuckGap"/> between
    /// them; returns each box's offset in the block and the block's size.
    /// </summary>
    private static (List<Point2D> Offsets, double Width, double Height) ArrangeInColumns(IReadOnlyList<(double Width, double Height)> sizes, int columns)
    {
        var columnCount = Math.Min(columns, sizes.Count);
        var rowCount = (sizes.Count + columnCount - 1) / columnCount;
        var columnWidths = new double[columnCount];
        var rowHeights = new double[rowCount];
        for (var i = 0; i < sizes.Count; i++)
        {
            columnWidths[i % columnCount] = Math.Max(columnWidths[i % columnCount], sizes[i].Width);
            rowHeights[i / columnCount] = Math.Max(rowHeights[i / columnCount], sizes[i].Height);
        }

        var offsets = new List<Point2D>();
        for (var i = 0; i < sizes.Count; i++)
        {
            var column = i % columnCount;
            var row = i / columnCount;
            offsets.Add(new Point2D(
                columnWidths.Take(column).Sum() + (column * TuckGap),
                rowHeights.Take(row).Sum() + (row * TuckGap)));
        }

        return (offsets, columnWidths.Sum() + ((columnCount - 1) * TuckGap), rowHeights.Sum() + ((rowCount - 1) * TuckGap));
    }

    /// <summary>The smallest gap kept between two parallel connector segments that run side by side.</summary>
    private const double LanePitch = 8.0;

    /// <summary>Tolerance, in logical pixels, for comparing waypoint coordinates.</summary>
    private const double GeometryEpsilon = 0.01;

    /// <summary>Identifies one face of one box: the box index and the outward unit direction of the face.</summary>
    private readonly record struct FaceKey(int Box, int Dx, int Dy, double Coordinate);

    /// <summary>
    /// Moves the bends of connectors whose end decoration would otherwise overlap the connector's
    /// last bend. After <see cref="OrientEdges(LayoutTree)"/> the end marker sits on the last waypoint. When a
    /// connector reaches it over a Z-shaped lane (two bends), the final straight leg must be at least
    /// the rendered marker length plus the corner radius and <see cref="Theme.CleanLegMargin"/>, as
    /// the engine itself guarantees at the target end of an edge: here the marker sits at the engine's
    /// source end, where only the stub of the connector is reserved, so the first lane out of an
    /// owner face lies closer to the box than the decoration is long.
    /// </summary>
    /// <remarks>
    /// All lanes of one box face are moved by the same distance (the largest shortfall of the face),
    /// which keeps their order and spacing. A face is left unchanged when the move would leave a
    /// remaining leg too short, run a segment through a box, or add a crossing or a near-parallel
    /// overlap between connectors. The pass is a pure function of the tree and the theme, makes one
    /// decision per face and is a no-op when no decorated end is too close to its bend.
    /// </remarks>
    /// <param name="tree">The placed tree, already oriented.</param>
    /// <param name="theme">The theme whose marker metrics and corner radius apply.</param>
    /// <returns>The tree with a clean straight approach at every decorated Z-shaped connector end.</returns>
    private static LayoutTree ClearEndDecorations(LayoutTree tree, Theme theme)
    {
        var lines = CollectNodeLines(tree.Nodes).ToList();
        var boxes = new List<LayoutBox>();
        CollectBoxes(tree.Nodes, boxes);

        // Group the Z-shaped connectors by the box face they end on, in first-seen order.
        var groups = new Dictionary<FaceKey, List<LayoutLine>>();
        foreach (var line in lines)
        {
            if (!TryGetLaneDirection(line, out var dx, out var dy))
            {
                continue;
            }

            var anchor = line.Waypoints[^1];
            var box = boxes.FindIndex(b => IsOnBoundary(b, anchor));
            var coordinate = 0.0;
            if (box < 0)
            {
                // The anchor is on no box: group by the face line it lies on instead.
                coordinate = Math.Round(dx != 0 ? anchor.X : anchor.Y, 1);
            }

            var key = new FaceKey(box, dx, dy, coordinate);
            if (!groups.TryGetValue(key, out var members))
            {
                members = [];
                groups[key] = members;
            }

            members.Add(line);
        }

        var current = lines.ToList();
        var replacements = new Dictionary<LayoutLine, LayoutLine>(ReferenceEqualityComparer.Instance);
        var minRemaining = (2.0 * theme.LineCornerRadius) + theme.CleanLegMargin;
        foreach (var (key, members) in groups)
        {
            var shift = members.Max(line => RequiredApproach(line, theme) - FinalLegLength(line));
            if (shift <= GeometryEpsilon)
            {
                continue;
            }

            var moved = new Dictionary<LayoutLine, LayoutLine>(ReferenceEqualityComparer.Instance);
            foreach (var member in members)
            {
                moved[member] = ShiftLane(member, key.Dx, key.Dy, shift);
            }
            if (moved.Keys.Any(line => RemainingLeg(line, key.Dx, key.Dy) - shift < minRemaining))
            {
                continue;
            }

            var candidate = current.Select(line => moved.TryGetValue(line, out var m) ? m : line).ToList();
            var obstructed = moved.Values.Any(line => HitsLeafBox(line, boxes));
            if (obstructed || CountConflicts(candidate) > CountConflicts(current))
            {
                continue;
            }

            current = candidate;
            foreach (var (original, changed) in moved)
            {
                replacements[original] = changed;
            }
        }

        return replacements.Count == 0
            ? tree
            : tree with { Nodes = [.. tree.Nodes.Select(node => ReplaceLines(node, replacements))] };
    }

    /// <summary>Applies the line replacements of <see cref="ClearEndDecorations"/> to <paramref name="node"/> and its children.</summary>
    private static LayoutNode ReplaceLines(LayoutNode node, IReadOnlyDictionary<LayoutLine, LayoutLine> replacements) => node switch
    {
        LayoutBox box => box with { Children = [.. box.Children.Select(child => ReplaceLines(child, replacements))] },
        LayoutLine line when replacements.TryGetValue(line, out var replacement) => replacement,
        _ => node,
    };

    /// <summary>Collects every <see cref="LayoutBox"/> of a placed tree, at any depth.</summary>
    private static void CollectBoxes(IReadOnlyList<LayoutNode> nodes, List<LayoutBox> boxes)
    {
        foreach (var box in nodes.OfType<LayoutBox>())
        {
            boxes.Add(box);
            CollectBoxes(box.Children, boxes);
        }
    }

    /// <summary>Returns whether <paramref name="point"/> lies on the outline of <paramref name="box"/>.</summary>
    private static bool IsOnBoundary(LayoutBox box, Point2D point)
    {
        var insideX = point.X >= box.X - GeometryEpsilon && point.X <= box.X + box.Width + GeometryEpsilon;
        var insideY = point.Y >= box.Y - GeometryEpsilon && point.Y <= box.Y + box.Height + GeometryEpsilon;
        var onVerticalSide = Math.Abs(point.X - box.X) <= GeometryEpsilon || Math.Abs(point.X - (box.X + box.Width)) <= GeometryEpsilon;
        var onHorizontalSide = Math.Abs(point.Y - box.Y) <= GeometryEpsilon || Math.Abs(point.Y - (box.Y + box.Height)) <= GeometryEpsilon;
        return insideX && insideY && (onVerticalSide || onHorizontalSide);
    }

    /// <summary>
    /// Returns whether <paramref name="line"/> reaches its last waypoint over a Z-shaped lane: at least
    /// four waypoints, a last leg, a lane perpendicular to it, and a leg before the lane parallel to
    /// it, all axis-aligned. The direction (<paramref name="dx"/>, <paramref name="dy"/>) points from
    /// the last waypoint back along the last leg.
    /// </summary>
    private static bool TryGetLaneDirection(LayoutLine line, out int dx, out int dy)
    {
        dx = 0;
        dy = 0;
        var points = line.Waypoints;
        var n = points.Count;
        if (n < 4)
        {
            return false;
        }

        var last = Direction(points[n - 1], points[n - 2]);
        var lane = Direction(points[n - 2], points[n - 3]);
        var before = Direction(points[n - 3], points[n - 4]);
        if (last is null || lane is null || before is null)
        {
            return false;
        }

        var (lx, ly) = last.Value;
        var (ax, ay) = lane.Value;
        var (bx, by) = before.Value;

        // The lane is perpendicular to the last leg and the leg before the lane runs back along
        // the direction of the last leg (the Z shape).
        if ((lx * ax) + (ly * ay) != 0 || (bx * lx) + (by * ly) != 1)
        {
            return false;
        }

        dx = lx;
        dy = ly;
        return true;
    }

    /// <summary>Returns the axis-aligned unit direction from <paramref name="from"/> to <paramref name="to"/>, or null for a zero-length or diagonal step.</summary>
    private static (int X, int Y)? Direction(Point2D from, Point2D to)
    {
        var ddx = to.X - from.X;
        var ddy = to.Y - from.Y;
        if (Math.Abs(ddx) > GeometryEpsilon && Math.Abs(ddy) <= GeometryEpsilon)
        {
            return (Math.Sign(ddx), 0);
        }

        return Math.Abs(ddy) > GeometryEpsilon && Math.Abs(ddx) <= GeometryEpsilon ? (0, Math.Sign(ddy)) : null;
    }

    /// <summary>The straight approach <paramref name="line"/> needs before its end marker: the marker, one corner radius and the safety margin.</summary>
    private static double RequiredApproach(LayoutLine line, Theme theme) =>
        line.TargetEnd == EndMarkerStyle.None
            ? 0.0
            : NotationMetrics.RenderedAlongLineLength(line.TargetEnd, theme) + theme.LineCornerRadius + theme.CleanLegMargin;

    /// <summary>The length of the last straight leg of <paramref name="line"/>.</summary>
    private static double FinalLegLength(LayoutLine line)
    {
        var n = line.Waypoints.Count;
        return Math.Abs(line.Waypoints[n - 1].X - line.Waypoints[n - 2].X) + Math.Abs(line.Waypoints[n - 1].Y - line.Waypoints[n - 2].Y);
    }

    /// <summary>The length of the leg before the lane (the leg that shortens when the lane moves away from the face).</summary>
    private static double RemainingLeg(LayoutLine line, int dx, int dy)
    {
        var n = line.Waypoints.Count;
        var a = line.Waypoints[n - 4];
        var b = line.Waypoints[n - 3];
        return ((a.X - b.X) * dx) + ((a.Y - b.Y) * dy);
    }

    /// <summary>Returns <paramref name="line"/> with its lane and last bend moved by <paramref name="shift"/> along (<paramref name="dx"/>, <paramref name="dy"/>), away from the face.</summary>
    private static LayoutLine ShiftLane(LayoutLine line, int dx, int dy, double shift)
    {
        var points = line.Waypoints.ToList();
        var n = points.Count;
        points[n - 2] = new Point2D(points[n - 2].X + (dx * shift), points[n - 2].Y + (dy * shift));
        points[n - 3] = new Point2D(points[n - 3].X + (dx * shift), points[n - 3].Y + (dy * shift));
        return line with { Waypoints = points };
    }

    /// <summary>Returns whether a segment of <paramref name="line"/> passes through the inside of a leaf box.</summary>
    private static bool HitsLeafBox(LayoutLine line, IReadOnlyList<LayoutBox> boxes)
    {
        var points = line.Waypoints;
        for (var i = 1; i < points.Count; i++)
        {
            var (minX, maxX) = (Math.Min(points[i - 1].X, points[i].X), Math.Max(points[i - 1].X, points[i].X));
            var (minY, maxY) = (Math.Min(points[i - 1].Y, points[i].Y), Math.Max(points[i - 1].Y, points[i].Y));
            foreach (var box in boxes.Where(b => b.Children.Count == 0))
            {
                if (maxX > box.X + 0.5 && minX < box.X + box.Width - 0.5 && maxY > box.Y + 0.5 && minY < box.Y + box.Height - 0.5)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Counts the conflicts between different connectors: perpendicular segments that cross, and
    /// parallel segments closer than <see cref="LanePitch"/> whose extents overlap or are less than
    /// <see cref="LanePitch"/> apart along the line (so two collinear pieces never read as one).
    /// </summary>
    private static int CountConflicts(IReadOnlyList<LayoutLine> lines)
    {
        var segments = lines
            .SelectMany((line, index) => line.Waypoints.Zip(line.Waypoints.Skip(1), (a, b) => (Index: index, A: a, B: b)))
            .ToList();
        var conflicts = 0;
        for (var i = 0; i < segments.Count; i++)
        {
            for (var j = i + 1; j < segments.Count; j++)
            {
                if (segments[i].Index != segments[j].Index && SegmentsConflict(segments[i].A, segments[i].B, segments[j].A, segments[j].B))
                {
                    conflicts++;
                }
            }
        }

        return conflicts;
    }

    /// <summary>Returns whether two axis-aligned segments cross, or run in parallel closer than <see cref="LanePitch"/> over, or within <see cref="LanePitch"/> of, a shared extent.</summary>
    private static bool SegmentsConflict(Point2D a1, Point2D a2, Point2D b1, Point2D b2)
    {
        var aHorizontal = Math.Abs(a1.Y - a2.Y) <= GeometryEpsilon;
        var bHorizontal = Math.Abs(b1.Y - b2.Y) <= GeometryEpsilon;
        var aVertical = Math.Abs(a1.X - a2.X) <= GeometryEpsilon;
        var bVertical = Math.Abs(b1.X - b2.X) <= GeometryEpsilon;
        if ((aHorizontal && aVertical) || (bHorizontal && bVertical))
        {
            return false;
        }

        if (aHorizontal && bVertical)
        {
            return StrictlyBetween(b1.X, a1.X, a2.X) && StrictlyBetween(a1.Y, b1.Y, b2.Y);
        }

        if (aVertical && bHorizontal)
        {
            return StrictlyBetween(a1.X, b1.X, b2.X) && StrictlyBetween(b1.Y, a1.Y, a2.Y);
        }

        if (aHorizontal && bHorizontal)
        {
            return Math.Abs(a1.Y - b1.Y) < LanePitch && Overlap(a1.X, a2.X, b1.X, b2.X) > -LanePitch;
        }

        return aVertical && bVertical && Math.Abs(a1.X - b1.X) < LanePitch && Overlap(a1.Y, a2.Y, b1.Y, b2.Y) > -LanePitch;
    }

    /// <summary>Returns whether <paramref name="value"/> lies strictly between <paramref name="a"/> and <paramref name="b"/> (in either order).</summary>
    private static bool StrictlyBetween(double value, double a, double b) =>
        value > Math.Min(a, b) + GeometryEpsilon && value < Math.Max(a, b) - GeometryEpsilon;

    /// <summary>Returns the length of the overlap of the intervals [<paramref name="a1"/>, <paramref name="a2"/>] and [<paramref name="b1"/>, <paramref name="b2"/>] (each in either order).</summary>
    private static double Overlap(double a1, double a2, double b1, double b2) =>
        Math.Min(Math.Max(a1, a2), Math.Max(b1, b2)) - Math.Max(Math.Min(a1, a2), Math.Min(b1, b2));

    /// <summary>
    /// Runs the layout engine over <paramref name="graph"/> with the root scope delegated to the
    /// "auto" algorithm, so both layout passes of <see cref="BuildLayout"/> are configured identically.
    /// </summary>
    private static LayoutTree LayOut(LayoutGraph graph)
    {
        graph.Set(CoreOptions.Algorithm, AutoLayoutAlgorithm.AlgorithmId);
        return LayoutEngine.Layout(graph);
    }

    /// <summary>
    /// Computes, for every package folder whose items should be stacked differently, the item order
    /// that keeps relationships leaving the folder from crossing one another: the folder's items
    /// that have external relationships are ordered by the mean horizontal center, in the first
    /// layout (<paramref name="tree"/>), of the boxes those relationships lead to.
    /// </summary>
    /// <remarks>
    /// The layered engine places a container's contents blind to the container's external edges and
    /// stacks unconnected groups of those contents by insertion index, so it is the caller that must
    /// supply the crossing-reducing (barycenter) order. Items without external relationships keep
    /// their positions; the keyed items are sorted into the slots the keyed items occupied. The key
    /// of an item aggregates the relationships of the item and of the usage nodes placed beside it.
    /// Only package-folder items are considered, the key is horizontal only (the graph flows
    /// top-to-bottom, so a layer is a row) and one refinement pass is made with no iteration, so the
    /// computation always terminates and is a no-op, returning an empty result, whenever no
    /// folder's order changes.
    /// </remarks>
    /// <param name="tree">The first-pass layout.</param>
    /// <param name="build">The first-pass graph with its located nodes and folder plans.</param>
    /// <param name="edges">The model edges the graph was built from.</param>
    /// <returns>Folder key to wanted item qualified-name order, only for folders whose order changes.</returns>
    private static Dictionary<string, IReadOnlyList<string>> ComputeItemOrder(
        LayoutTree tree,
        GraphBuild build,
        IReadOnlyList<ModelEdge> edges)
    {
        var order = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var centers = new Dictionary<LayoutGraphNode, double>();
        CollectCenters(tree.Nodes, build.Graph.Nodes, centers);

        foreach (var plan in build.Plans.Values)
        {
            if (plan.Items.Count < 2)
            {
                continue;
            }

            var keys = plan.Items.Select(item => ExternalPartnerKey(item.QualifiedName, build.UsagesByOwner, build.Located, edges, centers)).ToList();
            var wanted = OrderByPartnerPosition(plan.Items, keys);
            if (wanted is not null)
            {
                order[plan.Key] = wanted;
            }
        }

        return order;
    }

    /// <summary>
    /// Records the absolute horizontal center of every placed box against the graph node it was
    /// placed from, recursing through container boxes. Coordinates in the layout tree are absolute,
    /// so centers of boxes in different scopes are directly comparable.
    /// </summary>
    private static void CollectCenters(
        IReadOnlyList<LayoutNode> nodes,
        IReadOnlyList<LayoutGraphNode> graphNodes,
        Dictionary<LayoutGraphNode, double> centers)
    {
        // Several nodes of one scope can share label, keyword and shape (two ports "data :
        // SensorPort" of different owners): such a box cannot be told from its twin, so it is
        // never used as an ordering key.
        var signatureCounts = graphNodes
            .GroupBy(n => (n.Label, n.Keyword, n.Shape))
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var (_, box, graphNode) in PairBoxesWithNodes(nodes, graphNodes))
        {
            if (signatureCounts[(graphNode.Label, graphNode.Keyword, graphNode.Shape)] == 1)
            {
                centers[graphNode] = box.X + (box.Width / 2.0);
            }

            if (graphNode.HasChildren && box.Children.Count > 0)
            {
                CollectCenters(box.Children, graphNode.Children.Nodes, centers);
            }
        }
    }

    /// <summary>
    /// Returns the mean horizontal center of the boxes the external relationships of the box
    /// <paramref name="name"/> — and of the usage nodes placed beside it (<paramref name="beside"/>) —
    /// lead to, or <see langword="null"/> when it has none that can be placed (no relationship
    /// leaving its folder, or every partner is hidden by the depth limit).
    /// </summary>
    private static double? ExternalPartnerKey(
        string name,
        IReadOnlyDictionary<string, List<DefBox>> beside,
        IReadOnlyDictionary<string, Location> located,
        IReadOnlyList<ModelEdge> edges,
        IReadOnlyDictionary<LayoutGraphNode, double> centers)
    {
        if (!located.TryGetValue(name, out var self))
        {
            return null;
        }

        var sum = 0.0;
        var count = 0;
        foreach (var member in GroupMembers(name, beside))
        {
            foreach (var edge in edges)
            {
                var partnerName = PartnerOf(edge, member);
                if (partnerName is null || !located.TryGetValue(partnerName, out var partner))
                {
                    continue;
                }

                var anchor = ExternalAnchor(self.Scope, partner);
                if (anchor is not null && centers.TryGetValue(anchor, out var center))
                {
                    sum += center;
                    count++;
                }
            }
        }

        return count > 0 ? sum / count : null;
    }

    /// <summary>
    /// Returns <paramref name="name"/> together with every usage node placed beside it, transitively:
    /// the nodes that sit in the same scope as the box and move with it.
    /// </summary>
    private static IEnumerable<string> GroupMembers(string name, IReadOnlyDictionary<string, List<DefBox>> beside)
    {
        yield return name;
        if (!beside.TryGetValue(name, out var usages))
        {
            yield break;
        }

        foreach (var usage in usages)
        {
            foreach (var member in GroupMembers(usage.QualifiedName, beside))
            {
                yield return member;
            }
        }
    }

    /// <summary>Returns the other end of <paramref name="edge"/> as seen from <paramref name="name"/>, or null when it does not touch it (or is a self-loop).</summary>
    private static string? PartnerOf(ModelEdge edge, string name)
    {
        if (edge.SourceQualified == name && edge.TargetQualified != name)
        {
            return edge.TargetQualified;
        }

        return edge.TargetQualified == name && edge.SourceQualified != name ? edge.SourceQualified : null;
    }

    /// <summary>
    /// Returns the node a relationship from a definition held by <paramref name="selfScope"/> to
    /// <paramref name="partner"/> actually targets at the level where the two meet: the partner itself
    /// when it is a direct member of the lowest common scope, otherwise the container (folder or
    /// definition) that holds the partner and is a direct member of that scope. Returns null when the
    /// relationship stays inside <paramref name="selfScope"/> (the scope that holds the definition):
    /// that scope's own layout sees the edge, so it needs no ordering key.
    /// </summary>
    private static LayoutGraphNode? ExternalAnchor(ScopeInfo selfScope, Location partner)
    {
        var common = LowestCommonScope(selfScope, partner.Scope);
        if (ReferenceEquals(common, selfScope))
        {
            return null;
        }

        var anchor = partner.Node;
        for (var scope = partner.Scope; !ReferenceEquals(scope, common); scope = scope.Parent!)
        {
            anchor = scope.Owner ?? anchor;
        }

        return anchor;
    }

    /// <summary>
    /// Orders the items that have a key by that key (ties keep declaration order) into the slots the
    /// keyed items occupied, leaving the items without a key where they are. Returns null when fewer than two
    /// items have a key or the order does not change.
    /// </summary>
    private static List<string>? OrderByPartnerPosition(IReadOnlyList<DefBox> items, IReadOnlyList<double?> keys)
    {
        var slots = Enumerable.Range(0, items.Count).Where(i => keys[i].HasValue).ToList();
        if (slots.Count < 2)
        {
            return null;
        }

        // Round before comparing so sub-pixel differences between boxes that are effectively level
        // do not reorder them (and thereby make the result depend on layout noise).
        var sorted = slots.OrderBy(i => Math.Round(keys[i]!.Value)).ThenBy(i => i).ToList();
        if (sorted.SequenceEqual(slots))
        {
            return null;
        }

        var wanted = items.Select(item => item.QualifiedName).ToList();
        for (var k = 0; k < slots.Count; k++)
        {
            wanted[slots[k]] = items[sorted[k]].QualifiedName;
        }

        return wanted;
    }

    /// <summary>Returns the last segment of a qualified package name for use as a folder label.</summary>
    private static string SimplePackageName(string package)
    {
        var sep = package.LastIndexOf("::", StringComparison.Ordinal);
        return sep >= 0 ? package[(sep + 2)..] : package;
    }
}
