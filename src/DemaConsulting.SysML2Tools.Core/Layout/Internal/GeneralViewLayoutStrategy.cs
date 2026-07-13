// <copyright file="GeneralViewLayoutStrategy.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

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
/// Layout strategy for GeneralView diagrams. Renders every user-defined <c>def</c> element
/// (part, port, interface, requirement, action, …) as a keyword-labelled box, groups boxes by
/// their owning package inside folder-shaped containers, and routes specialization, membership, and
/// attribute-typing edges orthogonally between the boxes.
/// </summary>
/// <remarks>
/// Every definition and package folder is expressed as a single <see cref="LayoutGraph"/> — packages
/// as <see cref="BoxShape.Folder"/>-shaped container nodes, definitions as leaf nodes carrying their
/// <see cref="LayoutGraphNode.Keyword"/> and <see cref="LayoutGraphNode.Compartments"/> — and the
/// whole graph is placed with a single <see cref="HierarchicalLayoutAlgorithm.Apply"/> call: the root
/// scope packs package folders and top-level definitions by reading order
/// (<see cref="ContainmentLayoutAlgorithm"/>), while each folder's own contents are ordered by their
/// intra-package edges with the bundled layered algorithm (<see cref="LayeredLayoutAlgorithm"/>). All
/// box sizing (title bands, compartment rows) remains this strategy's responsibility, since the
/// layout stage is theme-agnostic. Standard-library declarations are excluded via
/// <see cref="StdlibFilter"/>. Depth-limited (truncated) package contents are never added to the
/// graph as individual boxes — the truncated folder becomes a single leaf node sized like a plain
/// ellipsis indicator, and the "+N more…" label is stamped onto its placed box once the layout is
/// known.
/// </remarks>
internal sealed class GeneralViewLayoutStrategy : ILayoutStrategy
{
    /// <summary>Minimum width of a definition box in logical pixels.</summary>
    private const double MinBoxWidth = 130.0;

    /// <summary>Approximate width-per-character factor relative to font size.</summary>
    private const double CharWidthFactor = 0.62;

    /// <summary>
    /// A feature membership: the keyword, raw typing reference (if any), simple name, raw
    /// redefined-feature reference (if any), and raw subsetted-feature reference(s) (if any) of
    /// one owned feature. <see cref="TypeName"/> is nullable because a feature may declare a
    /// redefinition/subsetting without an explicit type annotation.
    /// </summary>
    private sealed record FeatureMembership(
        string Keyword,
        string? TypeName,
        string? Name,
        string? RedefinedFeatureName,
        IReadOnlyList<string> SubsettedFeatureNames);

    /// <summary>
    /// The classification of an edge, which selects its line style so the renderer can distinguish
    /// the three relationships the General view draws between definition boxes.
    /// </summary>
    private enum EdgeKind
    {
        /// <summary>Subtype → supertype specialization: solid line, hollow triangle at the supertype.</summary>
        Specialization,

        /// <summary>Member-type → owner structural/reference membership: solid line, diamond at the owner.</summary>
        Membership,

        /// <summary>
        /// Owner → attribute-type dependency (attribute typing): dashed line, open chevron at the type.
        /// Attribute typing is a usage-type dependency, not composition, so it uses the OMG dependency
        /// notation (dashed + open arrowhead) rather than a membership diamond.
        /// </summary>
        Typing,

        /// <summary>
        /// Subtype feature redefinition → the owning definition of the redefined feature: solid
        /// line, hollow-triangle-with-crossbar at the owner.
        /// </summary>
        Redefinition,

        /// <summary>
        /// Subtype feature subsetting → the owning definition of the subsetted (ancestor) feature:
        /// dashed line, hollow triangle at the owner (the same marker as
        /// <see cref="Specialization"/>, distinguished purely by line style, mirroring how
        /// <see cref="Typing"/> is distinguished from <see cref="Membership"/>).
        /// </summary>
        Subsetting,

        /// <summary>
        /// A resolved <c>connect A to B</c>/message reference between two definitions (each
        /// endpoint's owning box resolved via <see cref="ResolveOwningBox"/>): solid line, no
        /// end marker.
        /// </summary>
        Connect,

        /// <summary>
        /// A resolved <c>allocate A to B</c> reference: dashed line, open chevron at the target,
        /// with a <c>«allocate»</c> midpoint label.
        /// </summary>
        Allocate,

        /// <summary>
        /// A resolved standalone <c>dependency</c> reference, and the <c>ref</c>-keyword's
        /// feature-typing dependency (sharing this same rendering so both are visually
        /// identical): dashed line, open chevron at the depended-upon element.
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
    /// A model edge between two definitions, expressed by qualified name so it can be resolved into
    /// the correct graph scope once every definition's node (or absence, if depth-truncated) is known.
    /// </summary>
    /// <param name="SourceQualified">Qualified name of the source definition.</param>
    /// <param name="TargetQualified">Qualified name of the target (supertype, owner, or attribute-type) definition.</param>
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

    /// <summary>Maps an edge kind to its rendered line style: typing/allocate/dependency/subsetting edges are dashed, all others solid.</summary>
    private static LineStyle LineStyleForKind(EdgeKind kind) =>
        kind is EdgeKind.Typing or EdgeKind.Allocate or EdgeKind.Dependency or EdgeKind.Subsetting
            ? LineStyle.Dashed
            : LineStyle.Solid;

    /// <summary>A user-defined definition or named usage together with its computed box size and supertypes.</summary>
    /// <param name="QualifiedName">The definition or usage's fully qualified name.</param>
    /// <param name="SimpleName">The definition or usage's simple (unqualified) name.</param>
    /// <param name="Keyword">The definition or usage keyword (e.g. <c>part</c>, <c>requirement</c>).</param>
    /// <param name="SupertypeNames">The declared supertype names (specialization/subsetting/redefinition targets).</param>
    /// <param name="Memberships">The owned feature memberships to render as compartment rows.</param>
    /// <param name="Compartments">The computed compartments to render inside the box.</param>
    /// <param name="Width">The computed box width.</param>
    /// <param name="Height">The computed box height.</param>
    /// <param name="Annotations">The applied annotation metadata associated with this node.</param>
    /// <param name="IsUsage">
    /// <see langword="true"/> when this box represents a <see cref="SysmlFeatureNode"/> usage
    /// (rather than a <see cref="SysmlDefinitionNode"/> definition). Used by
    /// <see cref="RemoveRedundantNestedUsages"/> to decide which boxes are eligible for
    /// nested-duplicate exclusion — definitions are never excluded by that rule.
    /// </param>
    private sealed record DefBox(
        string QualifiedName,
        string SimpleName,
        string Keyword,
        IReadOnlyList<string> SupertypeNames,
        IReadOnlyList<FeatureMembership> Memberships,
        IReadOnlyList<LayoutCompartment> Compartments,
        double Width,
        double Height,
        IReadOnlyList<SysmlAnnotation> Annotations,
        bool IsUsage);

    /// <summary>Where a located definition's node lives: the node itself and its owning package.</summary>
    private readonly record struct Location(LayoutGraphNode Node, string Package);

    /// <summary>
    /// A package folder that was depth-truncated: replaced by a leaf node sized as an ellipsis
    /// indicator, decorated with its "+N more…" label once the layout places it.
    /// </summary>
    /// <param name="Node">The leaf graph node standing in for the folder.</param>
    /// <param name="HiddenCount">Number of hidden definitions the ellipsis label reports.</param>
    private sealed record TruncatedFolder(LayoutGraphNode Node, int HiddenCount);

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

        // Collect all user-defined definitions, sized for rendering, restricted to the resolved
        // scope when one applies.
        var defs = CollectDefinitions(context.Workspace, theme, scope);
        if (defs.Count == 0)
        {
            return new LayoutTree(200.0, 100.0, []);
        }

        // Apply the view's standalone `filter [<expr>];` statement (SysmlViewNode.FilterExpressionText),
        // narrowing `defs` to the subset the Phase 1 expression subset matches. A parse/evaluation
        // failure (syntax error or a construct outside the Phase 1 subset — see
        // FilterExpressionParser) falls back to rendering the unfiltered resolved scope, surfaced
        // via a warning rather than silently dropping the filter or crashing.
        string? filterFailureReason = null;
        var filterExpressionText = context.ViewNode?.FilterExpressionText;
        if (filterExpressionText is { Length: > 0 })
        {
            var parseResult = FilterExpressionParser.Parse(filterExpressionText);
            if (parseResult.Expression is { } expression)
            {
                var matched = FilterExpressionEvaluator.Evaluate(
                    context.Workspace, defs.Select(d => d.QualifiedName).ToList(), expression).MatchedQualifiedNames;
                var matchedSet = new HashSet<string>(matched, StringComparer.Ordinal);
                defs = defs.Where(d => matchedSet.Contains(d.QualifiedName)).ToList();
                if (defs.Count == 0)
                {
                    return new LayoutTree(200.0, 100.0, []);
                }
            }
            else
            {
                filterFailureReason = parseResult.Diagnostics.FirstOrDefault()?.Message;
            }
        }

        // Drop any usage-level box that would duplicate content already shown as a compartment row
        // of its nearest still-rendered ancestor box (see RemoveRedundantNestedUsages, which
        // processes boxes shallowest-first so that a usage excluded earlier in the same pass is
        // treated as absent for its own children's test — cascading correctly through any nesting
        // depth instead of silently dropping a deeper-nested usage). This must run after the
        // standalone filter narrowing above (not before it and not inside CollectDefinitions), so
        // that a usage whose parent was excluded by the filter — rather than by scope — is
        // correctly kept as its own standalone box instead of being wrongly dropped by an
        // earlier-run dedup pass that saw the parent still present in a pre-filter set.
        defs = RemoveRedundantNestedUsages(defs);
        if (defs.Count == 0)
        {
            return new LayoutTree(200.0, 100.0, []);
        }

        // Group definitions by their owning package (prefix before the last "::").
        var groups = GroupByPackage(defs);

        // Resolve the specialization/membership/attribute-typing edge set by qualified name; the
        // graph-construction pass below drops any edge touching a definition that never received a
        // node (because its folder was depth-truncated).
        var (modelEdges, droppedEdges) = BuildModelEdges(defs, context.Workspace, scope is not null);

        // Build the single input graph: package folders as containers, definitions as leaves.
        var (graph, truncated) = BuildGraph(groups, modelEdges, theme, options.DepthLimit);

        // Lay out the whole graph in one call: the root scope packs folders/top-level definitions by
        // reading order (containment), while each folder's own contents are ordered by their
        // intra-package edges with the layered algorithm — selected per folder node, per the
        // established per-container-algorithm convention.
        var rootOptions = LayoutOptions.ForAlgorithm(ContainmentLayoutAlgorithm.AlgorithmId);
        var tree = new HierarchicalLayoutAlgorithm().Apply(graph, rootOptions);

        // Stamp the "+N more…" ellipsis label onto each truncated folder's placed box. The leaf
        // algorithm emits one box per root node in Nodes order, so the boxes portion of the placed
        // tree aligns with graph.Nodes by index.
        var placed = truncated.Count == 0 ? tree : DecorateTruncatedFolders(tree, graph, truncated, theme);

        // Surface any standalone `filter [<expr>];` evaluation failure, plus a distinct warning
        // for each `expose <path>::**[<expr>]` bracket filter that failed to parse or evaluate
        // (see ExposeScopeResolver.ResolveExposedScope's ExposedScope.Failures) — a
        // successfully-evaluated bracket filter already narrowed `scope` above and needs no
        // warning — through the standard layout-warnings channel.
        var warnings = LayoutWarnings.ForUnevaluatedFilter(context.ViewName, filterFailureReason is null ? null : filterExpressionText, filterFailureReason)
            .Concat(LayoutWarnings.ForUnevaluatedExposeBracketFilter(context.ViewName, scope?.Failures ?? []))
            .Concat(LayoutWarnings.ForDroppedRelationshipEdges(context.ViewName, droppedEdges
                .Select(d => (d.Kind, d.Source, d.Target, d.Reason))
                .ToList()))
            .ToList();
        return warnings.Count == 0 ? placed : placed with { Warnings = warnings };
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
    /// box; the second stage, <see cref="RemoveRedundantNestedUsages"/>, runs later in
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
    /// <seealso cref="RemoveRedundantNestedUsages"/>
    private static IReadOnlyList<DefBox> CollectDefinitions(
        SysmlWorkspace workspace,
        Theme theme,
        ExposedScope? scope)
    {
        var result = new List<DefBox>();

        foreach (var (qualifiedName, declaration) in workspace.Declarations)
        {
            if (declaration is not (SysmlDefinitionNode or SysmlFeatureNode) ||
                (declaration is SysmlFeatureNode && declaration.Name is null))
            {
                continue;
            }

            if (StdlibFilter.IsStdlibElement(qualifiedName, workspace.StdlibNames))
            {
                continue;
            }

            if (scope is not null && !ExposeScopeResolver.IsInSubjectScope(qualifiedName, scope))
            {
                continue;
            }

            var simpleName = declaration.Name ?? qualifiedName;
            var rawKeyword = declaration switch
            {
                SysmlDefinitionNode def => def.DefinitionKeyword,
                SysmlFeatureNode feature => feature.FeatureKeyword,
                _ => string.Empty,
            };
            var keyword = string.IsNullOrEmpty(rawKeyword) ? "def" : rawKeyword;

            // Specialization edges (below, in BuildModelEdges) are drawn from DefBox.SupertypeNames.
            // On a SysmlFeatureNode, SupertypeNames is populated only from a usage-level `subsets`/`:>`
            // clause (see SysmlNode.SupertypeNames's remarks) — subsetting, not specialization — so it
            // must not be surfaced here as a specialization target; only definition-level SupertypeNames
            // (`part def X :> Y`) are genuine specialization targets.
            var specializationSupertypeNames = declaration is SysmlDefinitionNode
                ? declaration.SupertypeNames
                : Array.Empty<string>();

            // Build compartments from the declaration's owned usages (attributes, ports, parts, …).
            var compartments = BuildCompartments(declaration);

            var memberships = CollectMemberships(declaration);
            var (width, height) = ComputeBoxSize(simpleName, keyword, compartments, theme);
            result.Add(new DefBox(qualifiedName, simpleName, keyword, specializationSupertypeNames, memberships, compartments, width, height, declaration.Annotations, declaration is SysmlFeatureNode));
        }

        return result;
    }

    /// <summary>
    /// Removes usage-level (<see cref="DefBox.IsUsage"/>) boxes from <paramref name="defs"/> whose
    /// nearest still-rendered ancestor is a definition/usage that is itself independently rendered
    /// as a box — i.e., a usage nested (directly, or transitively through one or more other
    /// excluded usages) inside a box that already shows it as a compartment row.
    /// </summary>
    /// <remarks>
    /// This is the second stage of <see cref="CollectDefinitions"/>'s admit-then-dedup design.
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
    private static IReadOnlyList<DefBox> RemoveRedundantNestedUsages(IReadOnlyList<DefBox> defs)
    {
        var qualifiedNames = new HashSet<string>(defs.Select(d => d.QualifiedName), StringComparer.Ordinal);
        var excluded = new HashSet<string>(StringComparer.Ordinal);

        // Process shallowest-first so that, by the time a usage is tested, its immediate parent's
        // own exclusion decision has already been finalized (parents always have strictly fewer
        // "::" occurrences than their descendants).
        var ordered = defs
            .Select((d, index) => (Box: d, Index: index))
            .OrderBy(t => CountOccurrences(t.Box.QualifiedName, "::"))
            .ThenBy(t => t.Index);

        foreach (var (box, _) in ordered)
        {
            if (!box.IsUsage)
            {
                continue;
            }

            var separatorIndex = box.QualifiedName.LastIndexOf("::", StringComparison.Ordinal);
            if (separatorIndex < 0)
            {
                continue;
            }

            var parentQualifiedName = box.QualifiedName[..separatorIndex];
            if (qualifiedNames.Contains(parentQualifiedName) && !excluded.Contains(parentQualifiedName))
            {
                excluded.Add(box.QualifiedName);
            }
        }

        return defs.Where(d => !excluded.Contains(d.QualifiedName)).ToList();
    }

    /// <summary>
    /// Counts the number of non-overlapping occurrences of <paramref name="token"/> in
    /// <paramref name="value"/>, used by <see cref="RemoveRedundantNestedUsages"/> as a nesting-depth
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
    /// Builds compartments for a definition or usage by grouping its owned usage features by
    /// keyword and formatting each as a <c>name : Type [n]</c> row.
    /// </summary>
    private static IReadOnlyList<LayoutCompartment> BuildCompartments(SysmlNode def)
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

            var keyword = string.IsNullOrEmpty(feature.FeatureKeyword) ? "feature" : feature.FeatureKeyword;
            if (!groups.TryGetValue(keyword, out var rows))
            {
                rows = [];
                groups[keyword] = rows;
                order.Add(keyword);
            }

            rows.Add(FormatFeatureRow(feature));
        }

        return [.. order.Select(k => new LayoutCompartment(Pluralize(k), groups[k]))];
    }

    /// <summary>Formats a usage feature as a compartment row: <c>name : Type [n]</c>.</summary>
    private static string FormatFeatureRow(SysmlFeatureNode feature)
    {
        // A constraint-kind feature's payload is a boolean expression, not a type reference — show
        // its raw expression text instead of the generic "name : Type [multiplicity]" shape. Unnamed
        // (the corpus-dominant case) shows the expression alone; named shows "name: expression".
        if (feature.ExpressionText is { Length: > 0 } expr)
        {
            return feature.Name is { Length: > 0 } exprName ? $"{exprName}: {expr}" : expr;
        }

        var name = feature.Name ?? string.Empty;
        var typing = feature.FeatureTyping is { Length: > 0 } t ? $" : {t}" : string.Empty;
        var multiplicity = feature.Multiplicity is { Length: > 0 } m ? $" {m}" : string.Empty;
        var row = $"{name}{typing}{multiplicity}".Trim();
        return row.Length == 0 ? "\u2014" : row;
    }

    /// <summary>Returns a simple plural form of a usage keyword for use as a compartment title.</summary>
    private static string Pluralize(string keyword) => keyword switch
    {
        "ref" => "references",

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

    /// <summary>
    /// Collects the feature memberships of a definition: the keyword, type reference (if any), simple
    /// name, redefined-feature reference (if any), and subsetted-feature reference(s) (if any) of
    /// each owned feature that carries a type annotation, a redefinition, and/or a subsetting.
    /// </summary>
    private static IReadOnlyList<FeatureMembership> CollectMemberships(SysmlNode def)
    {
        var result = new List<FeatureMembership>();
        foreach (var child in def.Children)
        {
            if (child is not SysmlFeatureNode feature)
            {
                continue;
            }

            var typing = feature.FeatureTyping is { Length: > 0 } ft ? ft : null;
            if (typing is not null || feature.RedefinedFeatureName is not null || feature.SupertypeNames.Count > 0)
            {
                var keyword = string.IsNullOrEmpty(feature.FeatureKeyword) ? "feature" : feature.FeatureKeyword;
                result.Add(new FeatureMembership(keyword, typing, feature.Name, feature.RedefinedFeatureName, feature.SupertypeNames));
            }
        }

        return result;
    }

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
    /// Groups definitions by their parent package name (the qualified-name prefix before the last
    /// <c>::</c>), preserving first-seen order. Top-level definitions use an empty package key.
    /// </summary>
    private static IReadOnlyList<(string Package, List<DefBox> Items)> GroupByPackage(IReadOnlyList<DefBox> defs)
    {
        var order = new List<string>();
        var map = new Dictionary<string, List<DefBox>>(StringComparer.Ordinal);

        foreach (var def in defs)
        {
            var sep = def.QualifiedName.LastIndexOf("::", StringComparison.Ordinal);
            var package = sep >= 0 ? def.QualifiedName[..sep] : string.Empty;

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
    /// Resolves the specialization, membership, attribute-typing, redefinition, subsetting,
    /// connect, allocate, dependency, and binding relationships across every definition (regardless
    /// of package) into a flat list of qualified-name edges. Self-references and unresolved targets
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
        IReadOnlyList<DefBox> defs, SysmlWorkspace workspace, bool isScoped)
    {
        // Index every definition by qualified and simple name (first-seen wins for simple names,
        // mirroring how packages may reuse a simple name across different qualified locations).
        var byQualified = new HashSet<string>(defs.Select(d => d.QualifiedName), StringComparer.Ordinal);
        var bySimple = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var def in defs)
        {
            bySimple.TryAdd(def.SimpleName, def.QualifiedName);
        }

        var defByQualified = defs.ToDictionary(d => d.QualifiedName, StringComparer.Ordinal);

        var edges = new List<ModelEdge>();
        foreach (var def in defs)
        {
            // Specialization: subtype → supertype, open arrowhead at the supertype (target) end.
            foreach (var supertype in def.SupertypeNames)
            {
                if (TryResolveQualified(supertype, byQualified, bySimple, out var target) &&
                    target != def.QualifiedName)
                {
                    edges.Add(new ModelEdge(def.QualifiedName, target, EndMarkerStyle.HollowTriangle, EdgeKind.Specialization));
                }
            }

            // Membership: member-type → owner, diamond at the owner (target) end. Structural keywords
            // (part/port) use a filled diamond; others emit none. (The "ref" keyword previously used
            // an obsolete hollow-diamond marker here — it has moved to its own Dependency-shaped edge
            // below, per current OMG SysML v2 notation for reference usages.)
            foreach (var membership in def.Memberships)
            {
                var arrowhead = membership.Keyword switch
                {
                    "part" or "port" => EndMarkerStyle.FilledDiamond,
                    _ => EndMarkerStyle.None,
                };

                if (arrowhead != EndMarkerStyle.None &&
                    membership.TypeName is { Length: > 0 } memberTypeName &&
                    TryResolveQualified(memberTypeName, byQualified, bySimple, out var memberType) &&
                    memberType != def.QualifiedName)
                {
                    edges.Add(new ModelEdge(memberType, def.QualifiedName, arrowhead, EdgeKind.Membership));
                }
            }

            // Attribute typing: owner → attribute-type dependency, open chevron at the type (target)
            // end. This is a usage-type dependency, not composition, so it uses the OMG dependency
            // notation rather than a membership diamond. Unresolved types and self-references are
            // skipped, exactly as above.
            foreach (var membership in def.Memberships)
            {
                if (membership.Keyword is not ("attribute" or "enum"))
                {
                    continue;
                }

                if (membership.TypeName is { Length: > 0 } attrTypeName &&
                    TryResolveQualified(attrTypeName, byQualified, bySimple, out var attrType) &&
                    attrType != def.QualifiedName)
                {
                    edges.Add(new ModelEdge(def.QualifiedName, attrType, EndMarkerStyle.OpenChevron, EdgeKind.Typing));
                }
            }

            // ref (reference usage) typing: owner → referenced-type dependency, sharing the same
            // Dependency rendering (dashed, open chevron) as the new public Dependency edge kind
            // below, per current OMG SysML v2 notation for reference usages (no longer an obsolete
            // hollow-diamond membership marker).
            foreach (var membership in def.Memberships)
            {
                if (membership.Keyword != "ref")
                {
                    continue;
                }

                if (membership.TypeName is { Length: > 0 } refTypeName &&
                    TryResolveQualified(refTypeName, byQualified, bySimple, out var refType) &&
                    refType != def.QualifiedName)
                {
                    edges.Add(new ModelEdge(def.QualifiedName, refType, EndMarkerStyle.OpenChevron, EdgeKind.Dependency));
                }
            }

            // Redefinition: subtype → the owning definition of the redefined feature, hollow
            // triangle with crossbar at the owner (target) end. A qualified reference
            // (Owner::feature) resolves the owner directly; a bare reference is looked up by
            // walking the definition's own supertype chain for a matching member name.
            foreach (var membership in def.Memberships)
            {
                if (membership.RedefinedFeatureName is not { Length: > 0 } redefinedRef)
                {
                    continue;
                }

                var owner = ResolveRedefinitionOwner(def, redefinedRef, byQualified, bySimple, defByQualified);
                if (owner is not null && owner != def.QualifiedName)
                {
                    edges.Add(new ModelEdge(def.QualifiedName, owner, EndMarkerStyle.HollowTriangleCrossbar, EdgeKind.Redefinition));
                }
            }

            // Subsetting: subtype → the owning definition of each subsetted (ancestor) feature,
            // hollow triangle (dashed) at the owner (target) end. Reuses ResolveRedefinitionOwner
            // verbatim — a subsetted-feature reference is resolved identically to a
            // redefined-feature reference (qualified reference resolves the owner directly; a bare
            // reference walks the definition's own supertype chain for a matching member name).
            foreach (var membership in def.Memberships)
            {
                foreach (var subsettedRef in membership.SubsettedFeatureNames)
                {
                    var owner = ResolveRedefinitionOwner(def, subsettedRef, byQualified, bySimple, defByQualified);
                    if (owner is not null && owner != def.QualifiedName)
                    {
                        edges.Add(new ModelEdge(def.QualifiedName, owner, EndMarkerStyle.HollowTriangle, EdgeKind.Subsetting));
                    }
                }
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
                        var source = ResolveOwningBox(sourceRef, workspace, byQualified, bySimple);
                        var target = ResolveOwningBox(edge.TargetQualifiedName, workspace, byQualified, bySimple);
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
                        var source = ResolveOwningBox(sourceRef, workspace, byQualified, bySimple);
                        var target = ResolveOwningBox(edge.TargetQualifiedName, workspace, byQualified, bySimple);
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
                        var source = ResolveOwningBox(sourceRef, workspace, byQualified, bySimple);
                        var target = ResolveOwningBox(edge.TargetQualifiedName, workspace, byQualified, bySimple);
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
                        var source = ResolveOwningBox(sourceRef, workspace, byQualified, bySimple);
                        var target = ResolveOwningBox(edge.TargetQualifiedName, workspace, byQualified, bySimple);
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
            }
        }

        return (edges, dropped);
    }

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
    /// reference that already names a definition resolves directly (the common case for
    /// definition-to-definition references, e.g. <see cref="EdgeKind.Allocate"/>/
    /// <see cref="EdgeKind.Dependency"/>); otherwise this walks successively shorter <c>"::"</c>
    /// prefixes of the (dotted-chain-resolved) qualified name, from longest to shortest, looking
    /// for a <see cref="SysmlFeatureNode"/> whose resolved <see cref="SysmlEdgeKind.Typing"/> edge
    /// targets a rendered box; the <em>shortest</em> matching prefix wins (the feature immediately
    /// owned by the rendered enclosing definition), which prevents the self-loop a naive
    /// "nearest enclosing definition" walk would produce for the dominant real-world shape where
    /// both sides of a <c>connect</c>/<c>bind</c> are nested inside the very same definition (e.g.
    /// <c>connect controller.power to battery.output;</c> inside <c>Drone</c> — walking to the
    /// nearest enclosing definition would resolve both sides to <c>Drone</c> itself).
    /// </summary>
    private static string? ResolveOwningBox(
        string qualifiedName,
        SysmlWorkspace workspace,
        HashSet<string> byQualified,
        Dictionary<string, string> bySimple)
    {
        if (TryResolveQualified(qualifiedName, byQualified, bySimple, out var direct))
        {
            return direct;
        }

        var segments = qualifiedName.Split("::");
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
    /// Resolves the owning definition of a redefined feature reference. A qualified reference
    /// (containing <c>::</c>) resolves the owner directly by stripping the trailing feature-name
    /// segment; a bare reference is resolved by walking the redefining definition's own supertype
    /// chain (transitively, with a cycle guard) for a matching member name.
    /// </summary>
    private static string? ResolveRedefinitionOwner(
        DefBox def,
        string redefinedRef,
        HashSet<string> byQualified,
        Dictionary<string, string> bySimple,
        IReadOnlyDictionary<string, DefBox> defByQualified)
    {
        var sep = redefinedRef.LastIndexOf("::", StringComparison.Ordinal);
        if (sep >= 0)
        {
            var ownerRef = redefinedRef[..sep];
            return TryResolveQualified(ownerRef, byQualified, bySimple, out var owner) ? owner : null;
        }

        return ResolveBareRedefinitionOwner(def, redefinedRef, byQualified, bySimple, defByQualified, new HashSet<string>(StringComparer.Ordinal));
    }

    /// <summary>
    /// Walks a definition's supertype chain (transitively, with a cycle guard) looking for a
    /// definition that declares a member feature with the given simple name.
    /// </summary>
    private static string? ResolveBareRedefinitionOwner(
        DefBox def,
        string bareName,
        HashSet<string> byQualified,
        Dictionary<string, string> bySimple,
        IReadOnlyDictionary<string, DefBox> defByQualified,
        HashSet<string> visited)
    {
        if (!visited.Add(def.QualifiedName))
        {
            return null;
        }

        foreach (var supertype in def.SupertypeNames)
        {
            if (!TryResolveQualified(supertype, byQualified, bySimple, out var superQualified) ||
                !defByQualified.TryGetValue(superQualified, out var superDef))
            {
                continue;
            }

            if (superDef.Memberships.Any(m => m.Name == bareName))
            {
                return superDef.QualifiedName;
            }

            var found = ResolveBareRedefinitionOwner(superDef, bareName, byQualified, bySimple, defByQualified, visited);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>Resolves a supertype/type reference to a definition's qualified name, by qualified then simple name.</summary>
    private static bool TryResolveQualified(
        string reference,
        HashSet<string> byQualified,
        Dictionary<string, string> bySimple,
        out string qualified)
    {
        if (byQualified.Contains(reference))
        {
            qualified = reference;
            return true;
        }

        var sep = reference.LastIndexOf("::", StringComparison.Ordinal);
        var simple = sep >= 0 ? reference[(sep + 2)..] : reference;
        return bySimple.TryGetValue(simple, out qualified!);
    }

    /// <summary>
    /// Builds the single input <see cref="LayoutGraph"/>: each package becomes a folder container
    /// node holding its definitions as leaves (or, when depth-truncated, a single leaf ellipsis
    /// placeholder); top-level (unpackaged) definitions become leaves directly on the root graph.
    /// Model edges are added once every definition's node placement is known: an edge whose endpoints
    /// share a non-empty package is added to that folder's own scope (an intra-package edge the
    /// layered algorithm can use to order the folder's contents); every other edge — including any
    /// crossing packages — is added at the root, referencing the descendant nodes directly, per the
    /// lowest-common-ancestor edge convention. An edge touching a depth-truncated (unrendered)
    /// definition has no node to reference and is dropped, exactly as before.
    /// </summary>
    private static (LayoutGraph Graph, List<TruncatedFolder> Truncated) BuildGraph(
        IReadOnlyList<(string Package, List<DefBox> Items)> groups,
        IReadOnlyList<ModelEdge> modelEdges,
        Theme theme,
        int depthLimit)
    {
        var graph = new LayoutGraph();

        // The General View intentionally draws one edge per source model relationship even when
        // several distinct edges share the same source and target node (for example two attributes
        // of the same type, or a redefinition edge that happens to coincide with another edge
        // between the same two definitions) — unlike InterconnectionViewLayoutStrategy, this graph
        // has never relied on the bundled algorithm's parallel-edge merging, so it opts out
        // explicitly to keep every distinct model relationship visible regardless of the layered
        // algorithm's own default.
        graph.Set(CoreOptions.MergeParallelEdges, false);
        var truncated = new List<TruncatedFolder>();

        // Reserve the full title area (package keyword + name) above a folder's contents so the
        // label never overlaps the first child box; the renderer draws the smaller tab notch within.
        var folderTitleHeight = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true);
        var margin = 2.0 * theme.LabelPadding;

        // Folder contents sit at depth 1; truncate them when the depth limit forbids that level.
        var truncateFolderContents = depthLimit > 0 && depthLimit <= 1;

        // Every located definition's node and owning package, so edges can be resolved and scoped.
        var located = new Dictionary<string, Location>(StringComparer.Ordinal);

        // Each non-truncated package folder's own child scope, keyed by package name, so intra-package
        // edges can be added there instead of at the root.
        var folderScopes = new Dictionary<string, LayoutGraph>(StringComparer.Ordinal);

        for (var g = 0; g < groups.Count; g++)
        {
            var (package, items) = groups[g];
            var isFolder = !string.IsNullOrEmpty(package);

            if (!isFolder)
            {
                // Top-level (unpackaged) definitions become plain leaves directly on the root graph.
                foreach (var def in items)
                {
                    var leaf = MakeDefNode(graph, def);
                    located[def.QualifiedName] = new Location(leaf, package);
                    AddAnnotationNote(graph, def, leaf, theme);
                }

                continue;
            }

            if (truncateFolderContents)
            {
                // Replace the folder's definition boxes with a single leaf ellipsis indicator. It
                // stays a leaf (its Children graph is never touched) so the hierarchical engine keeps
                // this caller-computed size rather than auto-sizing it as a container.
                var ellipsisWidth = Math.Max(
                    MinBoxWidth,
                    (2.0 * margin) + (items.Count.ToString(System.Globalization.CultureInfo.InvariantCulture).Length * 8.0) + 60.0);
                var ellipsisHeight = (2.0 * margin) + theme.FontSizeTitle;
                var placeholder = graph.AddNode($"folder:{package}", ellipsisWidth, folderTitleHeight + ellipsisHeight);
                placeholder.Label = SimplePackageName(package);
                placeholder.Shape = BoxShape.Folder;
                placeholder.Keyword = "package";
                truncated.Add(new TruncatedFolder(placeholder, items.Count));

                // None of the folder's definitions receive a node: every edge touching one is dropped.
                continue;
            }

            var folder = graph.AddNode($"folder:{package}", 0, 0);
            folder.Label = SimplePackageName(package);
            folder.Shape = BoxShape.Folder;
            folder.Keyword = "package";
            folder.TitleHeight = folderTitleHeight;
            folder.Set(CoreOptions.Algorithm, LayeredLayoutAlgorithm.AlgorithmId);
            folderScopes[package] = folder.Children;

            foreach (var def in items)
            {
                var leaf = MakeDefNode(folder.Children, def);
                located[def.QualifiedName] = new Location(leaf, package);
                AddAnnotationNote(folder.Children, def, leaf, theme);
            }
        }

        // Add every model edge whose endpoints both received a node, scoped per the
        // lowest-common-ancestor rule: same non-empty package → that folder's own scope; otherwise the
        // root graph, referencing the (possibly nested) endpoint nodes directly.
        var edgeId = 0;
        foreach (var edge in modelEdges)
        {
            if (!located.TryGetValue(edge.SourceQualified, out var source) ||
                !located.TryGetValue(edge.TargetQualified, out var target))
            {
                continue;
            }

            var scope = source.Package.Length > 0 &&
                source.Package == target.Package &&
                folderScopes.TryGetValue(source.Package, out var folderScope)
                ? folderScope
                : graph;

            var added = scope.AddEdge($"e{edgeId++}", source.Node, target.Node);
            added.TargetEnd = edge.Arrowhead;
            added.LineStyle = LineStyleForKind(edge.Kind);
            added.Label = edge.Label;
        }

        return (graph, truncated);
    }

    /// <summary>Creates a definition leaf node in the given scope, carrying its keyword and compartments.</summary>
    private static LayoutGraphNode MakeDefNode(LayoutGraph scope, DefBox def)
    {
        var node = scope.AddNode(def.QualifiedName, def.Width, def.Height);
        node.Label = def.SimpleName;
        node.Shape = BoxShape.Rectangle;
        node.Keyword = def.Keyword;
        node.Compartments = def.Compartments;
        return node;
    }

    /// <summary>
    /// Emits a companion <see cref="BoxShape.Note"/> node (plus a plain connecting edge) for a
    /// definition that carries <c>comment</c>/<c>doc</c> annotations, in the same scope the
    /// definition's own node was just added to (folder or root — mirrors the existing per-package
    /// folder scoping already used for def nodes and model edges). Implemented as an ordinary
    /// <see cref="LayoutGraph"/> node/edge (rather than a post-layout coordinate-decoration pass
    /// like <see cref="DecorateTruncatedFolders"/>) so the existing
    /// <see cref="LayeredLayoutAlgorithm"/>/<see cref="ContainmentLayoutAlgorithm"/> can position
    /// and route it automatically — a deliberate departure from the ROADMAP's suggested "adapt
    /// <c>StateTransitionViewLayoutStrategy.AddInitialMarker</c>'s marker-plus-line pattern": that
    /// pattern manipulates raw <c>Rect</c>/<c>LayoutNode</c> coordinates after a simpler,
    /// non-graph-based layout pass specific to state diagrams, which does not exist in this
    /// strategy's architecture (a single <see cref="LayoutGraph"/> handed once to
    /// <see cref="HierarchicalLayoutAlgorithm.Apply"/>). Emitting the note as an ordinary graph
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

        var edge = scope.AddEdge($"note-edge:{def.QualifiedName}", note, defNode);
        edge.TargetEnd = EndMarkerStyle.None;
        edge.LineStyle = LineStyle.Solid;
    }

    /// <summary>
    /// Combines an element's comment/documentation annotations into a single note's text: one note
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
    /// Replaces each truncated folder's placed box with one carrying its "+N more…" ellipsis label,
    /// positioned within the box's now-known absolute placement.
    /// </summary>
    private static LayoutTree DecorateTruncatedFolders(
        LayoutTree tree,
        LayoutGraph graph,
        IReadOnlyList<TruncatedFolder> truncated,
        Theme theme)
    {
        var hiddenByNode = truncated.ToDictionary(t => t.Node, t => t.HiddenCount);
        var folderTitleHeight = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true);

        var nodes = new List<LayoutNode>(tree.Nodes);
        for (var i = 0; i < graph.Nodes.Count && i < nodes.Count; i++)
        {
            if (!hiddenByNode.TryGetValue(graph.Nodes[i], out var hiddenCount) ||
                nodes[i] is not LayoutBox box)
            {
                continue;
            }

            var indicator = new LayoutLabel(
                X: box.X + theme.LabelPadding,
                Y: box.Y + folderTitleHeight + theme.LabelPadding + (theme.FontSizeTitle / 2.0),
                MaxWidth: box.Width - (2.0 * theme.LabelPadding),
                Text: $"+{hiddenCount} more\u2026",
                Align: TextAlign.Center,
                Weight: FontWeight.Regular,
                Style: FontStyle.Normal,
                FontSize: theme.FontSizeTitle);

            nodes[i] = box with { Children = [indicator] };
        }

        return tree with { Nodes = nodes };
    }

    /// <summary>Returns the last segment of a qualified package name for use as a folder label.</summary>
    private static string SimplePackageName(string package)
    {
        var sep = package.LastIndexOf("::", StringComparison.Ordinal);
        return sep >= 0 ? package[(sep + 2)..] : package;
    }
}
