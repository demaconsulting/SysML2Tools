// <copyright file="ExposeScopeResolver.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Filtering;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Rendering.Internal;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;

namespace DemaConsulting.SysML2Tools.Layout.Internal;

/// <summary>
/// The resolved qualified-name scope a view's <c>expose</c> statements restrict a diagram to,
/// distinguishing unfiltered exposed paths — whose match kind (exact, direct-children-only, or
/// whole containment subtree) depends on each entry's SysML v2 <c>expose</c> grammar form and
/// recursion setting (see <see cref="ExposeRecursionKind"/>) — from bracket-filtered
/// (<c>expose &lt;path&gt;::**[&lt;expr&gt;]</c>) exposed paths that narrow to specific matched
/// descendant definitions and/or named usages only.
/// </summary>
/// <param name="Subjects">
/// Exposed subject qualified names paired with the recursion kind that governs how they match a
/// candidate qualified name (see <see cref="ExposeScopeResolver.IsInSubjectScope"/>) — the
/// existing Phase 1 behavior for <c>expose</c> entries with no bracket filter (now correctly
/// narrowed for non-recursive forms), and the fallback (always whole-subtree) behavior for a
/// bracket-filtered entry whose expression failed to parse or evaluate.
/// </param>
/// <param name="ExplicitMembers">
/// Individual definition or named-usage qualified names matched by a successfully-evaluated
/// bracket-filter expression — exact matches only; a matched declaration's own nested members are
/// not automatically included unless they themselves also match the filter.
/// </param>
internal sealed record ExposedScope(
    IReadOnlyList<ExposeSubject> Subjects,
    IReadOnlyList<string> ExplicitMembers)
{
    /// <summary>
    /// Gets each bracket-filter expression that failed to parse or evaluate, with a short
    /// human-readable reason, so the caller can surface a "bracket filter evaluation failed"
    /// warning (see <see cref="LayoutWarnings.ForUnevaluatedExposeBracketFilter"/>). Empty when
    /// every bracket filter in the view (if any) parsed and evaluated successfully.
    /// </summary>
    public IReadOnlyList<BracketFilterFailure> Failures { get; init; } = Array.Empty<BracketFilterFailure>();

    /// <summary>
    /// Gets each successfully-parsed bracket-filtered expose entry (<c>expose P::**[expr];</c>) with
    /// its resolved target and filter, so elements that are not definitions or usages (e.g. owned
    /// <c>doc</c>/<c>comment</c> annotating elements) can be tested against the same filter
    /// (see <see cref="ExposeScopeResolver.IsAnnotationExposed"/>).
    /// </summary>
    public IReadOnlyList<FilteredExpose> FilteredEntries { get; init; } = Array.Empty<FilteredExpose>();
}

/// <summary>
/// A bracket-filtered expose entry whose filter parsed successfully.
/// </summary>
/// <param name="QualifiedName">The exposed path's resolved qualified name.</param>
/// <param name="Filter">The parsed bracket filter expression.</param>
internal sealed record FilteredExpose(string QualifiedName, FilterExpression Filter);

/// <summary>
/// A single resolved <c>expose</c> subject qualified name paired with the
/// <see cref="ExposeRecursionKind"/> that governs how it matches a candidate qualified name.
/// </summary>
/// <param name="QualifiedName">The exposed subject's resolved qualified name.</param>
/// <param name="Recursion">
/// The recursion kind (exact, direct-children-only, or whole-subtree) governing how this subject
/// matches candidate qualified names — see <see cref="ExposeScopeResolver.IsInSubjectScope"/>.
/// </param>
/// <param name="IsTypingTarget">
/// <see langword="true"/> when the subject was not written in an <c>expose</c> statement but added
/// by <see cref="ExposeScopeResolver.ResolveExposedScope"/> as the typing target of an exposed usage
/// (<c>expose myVehicle;</c> adds <c>Vehicle</c>). It matches candidates exactly like an expose
/// subject, but a consumer that must know what the view itself exposes — for example the General
/// View's part-tree rule — ignores it. <see langword="false"/> for every written expose entry.
/// </param>
/// <param name="ExcludedSubtrees">
/// Qualified names of containment subtrees a recursive subject does <em>not</em> cover — the
/// non-public descendants of a membership reached through an ordinary (non-<c>all</c>) import, which
/// contributes its public memberships only. Null (or empty) for every other subject.
/// </param>
internal sealed record ExposeSubject(
    string QualifiedName,
    ExposeRecursionKind Recursion,
    bool IsTypingTarget = false,
    IReadOnlyList<string>? ExcludedSubtrees = null)
{
    /// <summary>
    /// Returns whether <paramref name="qualifiedName"/> lies in one of this subject's
    /// <see cref="ExcludedSubtrees"/> (the excluded element itself or anything beneath it).
    /// </summary>
    /// <param name="qualifiedName">The candidate qualified name.</param>
    /// <returns>Whether the candidate is excluded from this subject's coverage.</returns>
    public bool IsExcluded(string qualifiedName) =>
        ExcludedSubtrees is { Count: > 0 } &&
        ExcludedSubtrees.Any(excluded =>
            qualifiedName == excluded || qualifiedName.StartsWith(excluded + "::", StringComparison.Ordinal));

    /// <summary>
    /// Returns whether <paramref name="qualifiedName"/> lies strictly beneath this subject's
    /// containment subtree and is not one of its <see cref="ExcludedSubtrees"/>. This is the single
    /// containment test every scope decision applies to a subject's descendants, so no path can
    /// forget the imported-side visibility exclusions.
    /// </summary>
    /// <param name="qualifiedName">The candidate qualified name.</param>
    /// <returns>Whether the candidate is a covered descendant of the subject.</returns>
    public bool CoversDescendant(string qualifiedName) =>
        qualifiedName.StartsWith(QualifiedName + "::", StringComparison.Ordinal) && !IsExcluded(qualifiedName);

    /// <summary>
    /// Returns whether <paramref name="qualifiedName"/> is this subject itself or a covered
    /// descendant (see <see cref="CoversDescendant"/>).
    /// </summary>
    /// <param name="qualifiedName">The candidate qualified name.</param>
    /// <returns>Whether the candidate is the subject or one of its covered descendants.</returns>
    public bool CoversSelfOrDescendant(string qualifiedName) =>
        qualifiedName == QualifiedName || CoversDescendant(qualifiedName);
}

/// <summary>
/// A single <c>expose &lt;path&gt;::**[&lt;expr&gt;]</c> bracket-filter expression that failed to
/// parse or evaluate, degrading gracefully to whole-subtree inclusion for its exposed path.
/// </summary>
/// <param name="ExpressionText">The raw bracket-filter expression source text.</param>
/// <param name="Reason">A short human-readable explanation of why evaluation could not proceed.</param>
internal sealed record BracketFilterFailure(string ExpressionText, string? Reason);

/// <summary>
/// Shared helper resolving the qualified-name containment-subtree scope a view's <c>expose</c>
/// statements restrict a diagram to, used by every <see cref="ILayoutStrategy"/> so each view kind
/// honors <c>expose</c> scoping identically.
/// </summary>
internal static class ExposeScopeResolver
{
    /// <summary>
    /// Resolves the qualified-name scope a view's <c>expose</c> statements restrict the diagram
    /// to, or <see langword="null"/> when the view has no resolved <see cref="SysmlEdgeKind.Expose"/>
    /// edge — meaning every non-stdlib element is included, unchanged from the pre-scoping
    /// behavior. This covers every "no scoping" case uniformly: a null <paramref name="viewNode"/>
    /// (the <c>--auto</c> synthetic view, which never carries expose/render/filter data), a view
    /// with no <c>expose</c> statement, and a view whose every <c>expose</c> entry failed to
    /// resolve. <c>RenderTargetName</c> (a rendering-style/format selector, not content) and
    /// <c>FilterExpressionText</c> (the view's separate standalone <c>filter [&lt;expr&gt;];</c>
    /// statement) never affect this decision.
    /// </summary>
    /// <remarks>
    /// When an exposed target resolves to a <see cref="SysmlFeatureNode"/> (a usage, e.g.
    /// <c>part myVehicle : Vehicle;</c>) rather than a <see cref="SysmlDefinitionNode"/>, the
    /// usage's own containment subtree is typically empty — the real content lives under its
    /// type's subtree. To avoid silently scoping to nothing, this also resolves the usage's own
    /// <see cref="SysmlEdgeKind.Typing"/> edge (if any) and adds that type's qualified name to the
    /// scope as well, using the same recursion kind for both — so both the usage and its type's
    /// subtree are included, with the recursion kind (exact/direct-children/whole-subtree)
    /// controlling whether the type's own descendants are also in scope. This expansion applies to
    /// every <see cref="ExposedScope.Subjects"/> entry regardless of recursion kind — it never
    /// applies to <see cref="ExposedScope.ExplicitMembers"/>, since a successfully-evaluated
    /// bracket filter's matches already name the exact matched definitions or usages.
    /// </remarks>
    /// <param name="workspace">The workspace, used to look up each exposed target's declaration.</param>
    /// <param name="viewNode">The view's AST node, or null for the synthetic <c>--auto</c> view.</param>
    /// <returns>The resolved <see cref="ExposedScope"/>, or null when no scoping applies.</returns>
    public static ExposedScope? ResolveExposedScope(SysmlWorkspace workspace, SysmlViewNode? viewNode)
    {
        var resolvedExposeMembers = viewNode?.ResolvedExposeMembers;
        if (resolvedExposeMembers is not { Count: > 0 })
        {
            return null;
        }

        var subjects = new List<ExposeSubject>();
        var explicitMembers = new List<string>();
        var failures = new List<BracketFilterFailure>();
        var filteredEntries = new List<FilteredExpose>();

        // Each entry directly pairs a resolved qualified name with the specific ExposeMember it
        // originated from (populated by ReferenceResolver), so a bracket-filtered entry's
        // expression is always evaluated against its own containment subtree only -- not the
        // view's other, unrelated expose entries. This avoids re-deriving the pairing later via
        // ambiguous raw-text matching against resolved targets.
        foreach (var (member, target) in resolvedExposeMembers)
        {
            var bracketFilterText = member.BracketFilterExpressionText;
            var recursionKind = member.RecursionKind;
            if (bracketFilterText is not { Length: > 0 })
            {
                AddSubject(workspace, target, recursionKind, subjects);
                continue;
            }

            var parseResult = FilterExpressionParser.Parse(bracketFilterText);
            if (parseResult.Expression is not { } expression)
            {
                failures.Add(new BracketFilterFailure(bracketFilterText, parseResult.Diagnostics.FirstOrDefault()?.Message));

                // Bracket-filter failures always degrade to full-subtree inclusion, never to the
                // narrower Exact/DirectChildren behavior — force whichever Recursive variant
                // matches the entry's form.
                var fallbackKind = recursionKind is ExposeRecursionKind.NamespaceDirectChildren or ExposeRecursionKind.NamespaceRecursive
                    ? ExposeRecursionKind.NamespaceRecursive
                    : ExposeRecursionKind.MembershipRecursive;
                AddSubject(workspace, target, fallbackKind, subjects);
                continue;
            }

            var candidates = workspace.Declarations
                .Where(kvp =>
                    (kvp.Key == target || kvp.Key.StartsWith(target + "::", StringComparison.Ordinal)) &&
                    (kvp.Value is SysmlDefinitionNode || (kvp.Value is SysmlFeatureNode && kvp.Value.Name is not null)))
                .Select(kvp => kvp.Key)
                .ToList();
            var evaluation = FilterExpressionEvaluator.Evaluate(workspace, candidates, expression);
            explicitMembers.AddRange(evaluation.MatchedQualifiedNames);
            filteredEntries.Add(new FilteredExpose(target, expression));
        }

        return new ExposedScope(subjects, explicitMembers) { Failures = failures, FilteredEntries = filteredEntries };
    }

    /// <summary>
    /// Returns the raw text of every view condition that applies to <paramref name="viewNode"/>:
    /// the <c>filter</c> conditions it owns, followed by those of its view definition (resolved
    /// <see cref="SysmlEdgeKind.Typing"/> edge) and, transitively, of that definition's general view
    /// definitions (resolved <see cref="SysmlEdgeKind.Supertype"/> edges) — SysML 7.26.2: "A view
    /// usage inherits any view conditions from its view definition"; SysML 8.3.26.11: exposed
    /// elements must meet all owned and inherited view conditions. Empty for a null view node.
    /// </summary>
    /// <param name="workspace">The workspace used to look up view definitions.</param>
    /// <param name="viewNode">The view, or null for the synthetic <c>--auto</c> view.</param>
    /// <returns>The view conditions' raw expression texts, owned first.</returns>
    public static IReadOnlyList<string> CollectViewConditions(SysmlWorkspace workspace, SysmlViewNode? viewNode)
    {
        var result = new List<string>();
        var visited = new HashSet<SysmlNode>();
        var pending = new Queue<SysmlNode>();
        if (viewNode is not null)
        {
            pending.Enqueue(viewNode);
        }

        while (pending.Count > 0)
        {
            var node = pending.Dequeue();
            if (!visited.Add(node))
            {
                continue;
            }

            if (node is SysmlViewNode view)
            {
                // FilterExpressionText alone is honored for view nodes built without the full list
                // (e.g. a synthesized dynamic view carrying the `render --filter` expression).
                // Standard library view nodes are deserialized and may carry a null list.
                if (view.FilterExpressionTexts is { Count: > 0 } texts)
                {
                    result.AddRange(texts);
                }
                else if (view.FilterExpressionText is { Length: > 0 } single)
                {
                    result.Add(single);
                }
            }

            foreach (var edge in node.ResolvedEdges ?? [])
            {
                if (edge.Kind is SysmlEdgeKind.Typing or SysmlEdgeKind.Supertype &&
                    workspace.Declarations.TryGetValue(edge.TargetQualifiedName, out var general))
                {
                    pending.Enqueue(general);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Returns whether an owned annotating element (metaclass <paramref name="metaclass"/>, e.g.
    /// <c>Documentation</c> or <c>Comment</c>) of the element <paramref name="ownerQualifiedName"/> is
    /// exposed by the view's <c>expose</c> statements. Documentation and owned comments are owned
    /// members of their element (KerML 7.2.4.2), so they are exposed exactly when the owner's
    /// members are: by a recursive expose covering the owner (<c>expose X::**;</c>,
    /// <c>expose X::*::**;</c>), by <c>expose X::*;</c> of the owner itself, or by a bracket-filtered
    /// expose covering the owner whose filter the annotating element satisfies. A bare
    /// <c>expose X;</c> exposes only X, not its members. With no <c>expose</c> scope (a null
    /// <paramref name="scope"/>) everything is rendered, annotations included.
    /// </summary>
    /// <param name="workspace">The workspace (standard library metaclasses for filter tests).</param>
    /// <param name="ownerQualifiedName">The annotated (owning) element's qualified name.</param>
    /// <param name="metaclass">The annotating element's metaclass.</param>
    /// <param name="scope">The view's resolved expose scope, or null.</param>
    /// <returns>Whether the annotating element is exposed.</returns>
    public static bool IsAnnotationExposed(SysmlWorkspace workspace, string ownerQualifiedName, string metaclass, ExposedScope? scope)
    {
        if (scope is null)
        {
            return true;
        }

        bool Covers(string subject) =>
            ownerQualifiedName == subject || ownerQualifiedName.StartsWith(subject + "::", StringComparison.Ordinal);

        return scope.Subjects.Any(subject => subject.Recursion switch
            {
                ExposeRecursionKind.MembershipRecursive or ExposeRecursionKind.NamespaceRecursive =>
                    subject.CoversSelfOrDescendant(ownerQualifiedName),
                ExposeRecursionKind.NamespaceDirectChildren => ownerQualifiedName == subject.QualifiedName,
                _ => false,
            }) ||
            scope.FilteredEntries.Any(entry =>
                Covers(entry.QualifiedName) &&
                FilterExpressionEvaluator.EvaluateForMetaclass(workspace, metaclass, entry.Filter));
    }

    /// <summary>
    /// Adds <paramref name="target"/> (and, when it resolves to a usage, its resolved type's
    /// qualified name — using the same <paramref name="kind"/> for both) to
    /// <paramref name="subjects"/> — the existing Phase 1 whole-subtree inclusion behavior for
    /// recursive kinds, now correctly narrowed to exact/direct-children matching for non-recursive
    /// kinds, shared by unfiltered <c>expose</c> entries and bracket-filtered entries that fell
    /// back after a parse/evaluation failure. For a namespace-valued kind the memberships the
    /// target imports are added too (see <see cref="AddImportedMemberships"/>).
    /// </summary>
    private static void AddSubject(
        SysmlWorkspace workspace,
        string target,
        ExposeRecursionKind kind,
        List<ExposeSubject> subjects)
    {
        subjects.Add(new ExposeSubject(target, kind));
        AddImportedMemberships(workspace, target, kind, subjects);

        if (workspace.Declarations.TryGetValue(target, out var declaration) &&
            declaration is SysmlFeatureNode { } feature)
        {
            var typeTarget = feature.ResolvedEdges
                .FirstOrDefault(edge => edge.Kind == SysmlEdgeKind.Typing)
                ?.TargetQualifiedName;
            if (typeTarget is not null)
            {
                subjects.Add(new ExposeSubject(typeTarget, kind, IsTypingTarget: true));
            }
        }
    }

    /// <summary>
    /// Adds, for a namespace-valued <c>expose</c> subject (<c>A::*</c>, <c>A::*::**</c> or
    /// <c>A::**</c>), one subject per membership that <c>A</c> brings in through its <c>import</c>s —
    /// the imported memberships a SysML v2 namespace import of <c>A</c> includes in addition to
    /// <c>A</c>'s owned members (KerML <c>Namespace::visibleMemberships</c>; issue #148). A bare
    /// <c>expose A;</c> (<see cref="ExposeRecursionKind.MembershipExact"/>) exposes only <c>A</c> and
    /// follows nothing.
    /// </summary>
    /// <remarks>
    /// An <c>Expose</c> has <c>isImportAll = true</c> (SysML v2 §8.3.26.2), so — exactly as the OMG
    /// Pilot does (<c>Namespace::visibleMemberships</c> with <c>includeAll = true</c>, KerML
    /// §8.3.2.4) — the exposed namespace follows <em>every</em> one of its own imports whatever their
    /// visibility, and, when the expose is recursive, so does every namespace owned by it at any
    /// depth (<c>includeAll</c> is passed to the recursive calls; a private nested package is
    /// walked too). The namespaces those imports reach are different: an ordinary
    /// <c>Import</c> has <c>isImportAll = false</c> unless written <c>import all</c>, so such a
    /// namespace contributes only its <c>public</c> members and follows only its <c>public</c>
    /// imports, transitively. Recursion into a namespace reached through an import happens only
    /// when that import is itself recursive (<c>import B::**;</c>, <c>import B::*::**;</c>),
    /// never merely because the expose is recursive. A wildcard import (<c>import B::*;</c>) adds
    /// each named member of <c>B</c> its visibility admits; a membership import
    /// (<c>import B::Widget;</c>, <c>import B::**;</c>) adds exactly that member (and its subtree
    /// when recursive). Members are matched exactly for a non-recursive expose and as subtrees
    /// when either the expose or the import is recursive — but a subtree reached through an import
    /// keeps the import's visibility (KerML <c>visibleMemberships</c> with <c>includeAll</c> =
    /// <c>isImportAll</c>): an ordinary import covers only the <c>public</c> descendants, so a
    /// private nested member of an imported membership is never exposed. A bracket filter
    /// (<c>import B::**[@Safety];</c>) is tested against every candidate the import reaches, nested
    /// ones and those reached through the imported namespace's own imports included (all filters
    /// met on the way must accept), and each accepted candidate is exposed exactly. Import cycles are guarded by a
    /// visited set and duplicate subjects are not added twice. Imports of standard library
    /// namespaces and unresolved imports are skipped, and an unparsable bracket filter leaves the
    /// import followed unfiltered.
    /// </remarks>
    private static void AddImportedMemberships(
        SysmlWorkspace workspace,
        string target,
        ExposeRecursionKind kind,
        List<ExposeSubject> subjects)
    {
        if (kind is not (ExposeRecursionKind.NamespaceDirectChildren
            or ExposeRecursionKind.NamespaceRecursive
            or ExposeRecursionKind.MembershipRecursive))
        {
            return;
        }

        if (workspace.Declarations.TryGetValue(target, out var exposed) && IsNamespace(exposed))
        {
            var exposeRecursive = kind != ExposeRecursionKind.NamespaceDirectChildren;
            new ImportWalker(workspace, exposeRecursive, subjects)
                .FollowImports(exposed, target, recursive: exposeRecursive, includeAll: true, filters: []);
        }
    }

    /// <summary>Returns whether a declaration is a namespace whose imports an expose follows (a package or a definition).</summary>
    private static bool IsNamespace(SysmlNode node) => node is SysmlPackageNode or SysmlDefinitionNode;

    /// <summary>A parsed import bracket filter (<c>import P::**[@X]</c>) with its source text.</summary>
    /// <param name="Text">The filter's expression source text (identifies it for the import-cycle guard).</param>
    /// <param name="Expression">The parsed expression every candidate the import reaches must satisfy.</param>
    private sealed record ImportFilter(string Text, FilterExpression Expression);

    /// <summary>
    /// Walks the imports of an exposed namespace (and, transitively, of the namespaces it imports),
    /// appending the imported memberships as <see cref="ExposeSubject"/>s. One instance serves one
    /// exposed subject.
    /// </summary>
    private sealed class ImportWalker(SysmlWorkspace workspace, bool exposeRecursive, List<ExposeSubject> subjects)
    {
        /// <summary>The (namespace, recursive, includeAll, filters) tuples already walked — the import-cycle guard.</summary>
        private readonly HashSet<(string Namespace, bool Recursive, bool IncludeAll, string Filters)> _visited = [];

        /// <summary>
        /// Follows the imports of <paramref name="ns"/> — every import when
        /// <paramref name="includeAll"/>, otherwise only the <c>public</c> ones — and, when
        /// <paramref name="recursive"/>, those of every namespace nested in it (every nested
        /// namespace when <paramref name="includeAll"/>, otherwise only the <c>public</c> ones),
        /// mirroring <c>Namespace::visibleMemberships(excluded, isRecursive, includeAll)</c>.
        /// <paramref name="filters"/> are the bracket filters of the imports that led here; every
        /// membership reached below them — through further imports too — must satisfy all of them.
        /// </summary>
        public void FollowImports(
            SysmlNode ns,
            string namespaceName,
            bool recursive,
            bool includeAll,
            IReadOnlyList<ImportFilter> filters)
        {
            var filterKey = string.Join("\u0001", filters.Select(filter => filter.Text));
            if (!_visited.Add((namespaceName, recursive, includeAll, filterKey)))
            {
                return;
            }

            foreach (var import in ns.Children.OfType<SysmlImportNode>()
                .Where(import => includeAll || import.Visibility == SysmlVisibility.Public))
            {
                AddImport(import, recursive, filters);
            }

            if (!recursive)
            {
                return;
            }

            foreach (var child in ns.Children.Where(child =>
                IsNamespace(child) && (includeAll || child.Visibility == SysmlVisibility.Public)))
            {
                if (child.QualifiedName is { Length: > 0 } childName)
                {
                    FollowImports(child, childName, recursive: true, includeAll, filters);
                }
            }
        }

        /// <summary>Adds the memberships one import brings in, then follows the imported namespace's own imports.</summary>
        private void AddImport(SysmlImportNode import, bool recursive, IReadOnlyList<ImportFilter> inherited)
        {
            var importedName = import.ResolvedEdges.FirstOrDefault(edge => edge.Kind == SysmlEdgeKind.Import)?.TargetQualifiedName;
            if (importedName is null ||
                StdlibFilter.IsStdlibElement(importedName, workspace.StdlibNames) ||
                !workspace.Declarations.TryGetValue(importedName, out var imported))
            {
                return;
            }

            // A bracket filter applies to everything its import reaches, including what the imported
            // namespace itself imports (name resolution tests each candidate of a recursive lookup,
            // however it was reached), so it joins the filters inherited from the imports above it.
            var filters = import.BracketFilterExpressionText is { Length: > 0 } filterText &&
                FilterExpressionParser.Parse(filterText).Expression is { } expression
                    ? [.. inherited, new ImportFilter(filterText, expression)]
                    : inherited;
            var expandSubtrees = exposeRecursive || recursive || import.IsRecursive;
            if (import.IsMembershipImport)
            {
                AddMembers(import, [importedName], expandSubtrees, filters);
            }
            else if (IsNamespace(imported))
            {
                // A wildcard import brings in the imported namespace's public members (every member
                // for an `import all`), but never its imports — those are followed below.
                var members = imported.Children
                    .Where(child => child is not (SysmlImportNode or SysmlAliasNode) &&
                        child.QualifiedName is { Length: > 0 } &&
                        child.Name is { Length: > 0 } &&
                        (import.IsImportAll || child.Visibility == SysmlVisibility.Public))
                    .Select(child => child.QualifiedName!)
                    .ToList();
                AddMembers(import, members, expandSubtrees, filters);
            }
            else
            {
                return;
            }

            // The imported namespace is walked with the *import's* flags (Import::isRecursive and
            // Import::isImportAll), not the expose's: an ordinary import yields only public imports
            // and is not descended into unless it is itself recursive.
            if (IsNamespace(imported) && (import.IsRecursive || !import.IsMembershipImport))
            {
                FollowImports(imported, importedName, import.IsRecursive, import.IsImportAll, filters);
            }
        }

        /// <summary>
        /// Adds the subjects for the members one import brings in. Without a bracket filter (the
        /// import's own or one inherited from the imports that led to it) each member is added
        /// once — exactly, or, when <paramref name="expandSubtrees"/>, as a recursive subject
        /// covering its subtree minus the descendants the import's visibility hides (everything but
        /// <c>public</c> memberships unless the import is <c>import all</c>). With bracket filters,
        /// every candidate — each member and, when <paramref name="expandSubtrees"/>, each visible
        /// descendant — is tested individually against every filter (as <c>NameResolver</c> tests
        /// each recursive candidate) and each accepted one is added exactly.
        /// </summary>
        private void AddMembers(
            SysmlImportNode import,
            List<string> members,
            bool expandSubtrees,
            IReadOnlyList<ImportFilter> filters)
        {
            if (filters.Count > 0)
            {
                var candidates = new List<string>(members);
                if (expandSubtrees)
                {
                    foreach (var member in members)
                    {
                        CollectDescendants(member, import.IsImportAll, candidates, hidden: null);
                    }
                }

                foreach (var filter in filters)
                {
                    candidates = FilterExpressionEvaluator.Evaluate(workspace, candidates, filter.Expression)
                        .MatchedQualifiedNames.ToList();
                }

                foreach (var match in candidates)
                {
                    AddSubjectOnce(new ExposeSubject(match, ExposeRecursionKind.MembershipExact));
                }

                return;
            }

            foreach (var member in members)
            {
                if (!expandSubtrees)
                {
                    AddSubjectOnce(new ExposeSubject(member, ExposeRecursionKind.MembershipExact));
                    continue;
                }

                var hidden = new List<string>();
                if (!import.IsImportAll)
                {
                    CollectDescendants(member, includeAll: false, visible: [], hidden);
                }

                AddSubjectOnce(new ExposeSubject(member, ExposeRecursionKind.MembershipRecursive, ExcludedSubtrees: hidden));
            }
        }

        /// <summary>
        /// Collects the named descendants of <paramref name="member"/> that an import with the given
        /// visibility reaches into <paramref name="visible"/> — every descendant when
        /// <paramref name="includeAll"/>, otherwise only those reachable through <c>public</c>
        /// memberships — and, into <paramref name="hidden"/> when supplied, the topmost descendants it
        /// does not reach.
        /// </summary>
        private void CollectDescendants(string member, bool includeAll, List<string> visible, List<string>? hidden)
        {
            if (workspace.Declarations.TryGetValue(member, out var node))
            {
                Walk(node);
            }

            void Walk(SysmlNode owner)
            {
                foreach (var child in owner.Children)
                {
                    if (child.QualifiedName is not { Length: > 0 } childName)
                    {
                        continue;
                    }

                    if (!includeAll && child.Visibility != SysmlVisibility.Public)
                    {
                        hidden?.Add(childName);
                        continue;
                    }

                    if (child is not (SysmlImportNode or SysmlAliasNode) && child.Name is { Length: > 0 })
                    {
                        visible.Add(childName);
                    }

                    Walk(child);
                }
            }
        }

        /// <summary>Adds <paramref name="subject"/> unless an equivalent subject (same name, kind, and exclusions) is already present.</summary>
        private void AddSubjectOnce(ExposeSubject subject)
        {
            var excluded = subject.ExcludedSubtrees ?? [];
            if (!subjects.Any(existing =>
                existing.QualifiedName == subject.QualifiedName &&
                existing.Recursion == subject.Recursion &&
                (existing.ExcludedSubtrees ?? []).SequenceEqual(excluded)))
            {
                subjects.Add(subject);
            }
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when the view itself exposes the element
    /// <paramref name="qualifiedName"/> by name — a written (or import-derived) subject of kind
    /// <see cref="ExposeRecursionKind.MembershipExact"/> or
    /// <see cref="ExposeRecursionKind.MembershipRecursive"/> that names it — as opposed to reaching
    /// it only as an ancestor of a narrower exposed element or as the typing target
    /// <see cref="ResolveExposedScope"/> adds for an exposed usage. An element exposed by name is
    /// drawn together with its own structure (e.g. an <c>expose Car;</c> part definition draws its
    /// nested parts in an Interconnection View).
    /// </summary>
    /// <param name="qualifiedName">The element's qualified name.</param>
    /// <param name="scope">The resolved <c>expose</c> scope.</param>
    /// <returns>Whether a written membership subject names the element.</returns>
    public static bool ExposesByName(string qualifiedName, ExposedScope scope) =>
        scope.Subjects.Any(subject =>
            !subject.IsTypingTarget &&
            subject.QualifiedName == qualifiedName &&
            subject.Recursion is ExposeRecursionKind.MembershipExact or ExposeRecursionKind.MembershipRecursive);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="qualifiedName"/> matches one of
    /// <paramref name="scope"/>'s <see cref="ExposedScope.Subjects"/> per that subject's own
    /// <see cref="ExposeRecursionKind"/>:
    /// <list type="bullet">
    /// <item><description><see cref="ExposeRecursionKind.MembershipRecursive"/>: the subject
    /// itself or lies within its containment subtree (a <c>"{subject}::"</c> prefix match, reusing
    /// the same qualified-name-prefix idiom used elsewhere for containment-subtree matching).</description></item>
    /// <item><description><see cref="ExposeRecursionKind.NamespaceRecursive"/>: lies within the
    /// subject's containment subtree at any depth — never the subject itself. Per
    /// formal-26-03-02.md §8.3.26.4, a NamespaceExpose exposes the subject's Memberships (its
    /// members), and a namespace is never a member of itself, regardless of the recursive
    /// flag.</description></item>
    /// <item><description><see cref="ExposeRecursionKind.MembershipExact"/>: the subject itself
    /// only, exact match.</description></item>
    /// <item><description><see cref="ExposeRecursionKind.NamespaceDirectChildren"/>: a direct
    /// (one-level) child of the subject only — not the subject itself, not a deeper
    /// descendant.</description></item>
    /// </list>
    /// or is an exact match of one of <paramref name="scope"/>'s
    /// <see cref="ExposedScope.ExplicitMembers"/> (a bracket-filter-matched definition or usage).
    /// </summary>
    public static bool IsInSubjectScope(string qualifiedName, ExposedScope scope) =>
        scope.Subjects.Any(subject => subject.Recursion switch
        {
            ExposeRecursionKind.MembershipRecursive => subject.CoversSelfOrDescendant(qualifiedName),
            ExposeRecursionKind.NamespaceRecursive => subject.CoversDescendant(qualifiedName),
            ExposeRecursionKind.MembershipExact => qualifiedName == subject.QualifiedName,
            ExposeRecursionKind.NamespaceDirectChildren => IsDirectChildOf(qualifiedName, subject.QualifiedName),
            _ => false,
        }) ||
        scope.ExplicitMembers.Contains(qualifiedName, StringComparer.Ordinal);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="qualifiedName"/> matches one of
    /// <paramref name="scope"/>'s <see cref="ExposedScope.Subjects"/> whose own
    /// <see cref="ExposeSubject.Recursion"/> kind carries unlimited-depth recursion —
    /// <see cref="ExposeRecursionKind.MembershipRecursive"/> or
    /// <see cref="ExposeRecursionKind.NamespaceRecursive"/> — using that subject's same
    /// per-kind containment rule as <see cref="IsInSubjectScope"/>. This is a distinct, narrower
    /// predicate from <see cref="IsInSubjectScope"/>: the latter decides <em>whether</em> a
    /// qualified name is in scope at all (any subject kind, plus bracket-filtered
    /// <see cref="ExposedScope.ExplicitMembers"/>), while this method decides <em>how far</em> a
    /// branch that is already known to be in scope should recurse — a caller
    /// (<see cref="InterconnectionViewLayoutStrategy"/>) needs to know not just that a feature
    /// matched, but specifically which subject matched it and whether that subject's own recursion
    /// kind means the matched branch's interior should expand without a depth limit. A
    /// <see cref="ExposedScope.ExplicitMembers"/> match (a bracket-filter result) is never
    /// unlimited — bracket filters name exact matched declarations only, never a containment
    /// subtree, so they never contribute to this predicate.
    /// </summary>
    /// <param name="qualifiedName">The candidate qualified name.</param>
    /// <param name="scope">The resolved <c>expose</c> scope.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="qualifiedName"/> matches a subject whose
    /// recursion kind is <see cref="ExposeRecursionKind.MembershipRecursive"/> or
    /// <see cref="ExposeRecursionKind.NamespaceRecursive"/>; otherwise <see langword="false"/>.
    /// </returns>
    public static bool MatchesUnlimitedSubject(string qualifiedName, ExposedScope scope) =>
        scope.Subjects.Any(subject => subject.Recursion switch
        {
            ExposeRecursionKind.MembershipRecursive => subject.CoversSelfOrDescendant(qualifiedName),
            ExposeRecursionKind.NamespaceRecursive => subject.CoversDescendant(qualifiedName),
            _ => false,
        });

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="qualifiedName"/> is a direct (one-level)
    /// child of <paramref name="container"/> — starts with <c>"{container}::"</c> and the
    /// remainder after that prefix contains no further <c>"::"</c> separator. Not satisfied by
    /// <paramref name="container"/> itself, nor by any deeper descendant.
    /// </summary>
    /// <param name="qualifiedName">The candidate qualified name.</param>
    /// <param name="container">The container's qualified name.</param>
    /// <returns><see langword="true"/> when <paramref name="qualifiedName"/> is a direct child of <paramref name="container"/>.</returns>
    private static bool IsDirectChildOf(string qualifiedName, string container)
    {
        var prefix = container + "::";
        if (!qualifiedName.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var remainder = qualifiedName[prefix.Length..];
        return !remainder.Contains("::", StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="candidateQualifiedName"/> (a candidate
    /// single-root diagram root, e.g. the definition a <see cref="InterconnectionViewLayoutStrategy"/>,
    /// <see cref="StateTransitionViewLayoutStrategy"/>, <see cref="ActionFlowViewLayoutStrategy"/>, or
    /// <see cref="SequenceViewLayoutStrategy"/> would otherwise pick by its own heuristic) is related
    /// to the resolved <c>expose</c> scope in <paramref name="scope"/>, in either containment
    /// direction: the candidate itself is an exposed subject/matched member (subject to the same
    /// per-recursion-kind self-exclusion <see cref="IsInSubjectScope"/> applies — see below), the
    /// candidate lies within an exposed subject's containment subtree, or an exposed subject/matched
    /// member lies within the candidate's own containment subtree (the common "expose an inner
    /// state/action/part/lifeline of the root" case).
    /// </summary>
    /// <remarks>
    /// This method identifies the *set* of scope-relevant candidates only — because SysML v2
    /// definitions may nest, an ancestor definition and one of its nested descendant definitions can
    /// both be relevant to the same resolved scope (an exposed subject nested inside the descendant
    /// is transitively nested inside the ancestor too). Callers with more than one relevant candidate
    /// must break the tie themselves; the single-root <c>FindRoot</c> strategies do so via
    /// <see cref="IsMoreSpecificCandidate"/>, which prefers the most deeply nested relevant candidate
    /// over the plain per-strategy score.
    /// <para>
    /// The "candidate equals the subject" direction only applies when the subject's
    /// <see cref="ExposeSubject.Recursion"/> kind actually includes the subject itself
    /// (<see cref="ExposeRecursionKind.MembershipRecursive"/> or
    /// <see cref="ExposeRecursionKind.MembershipExact"/>). Per formal-26-03-02.md §8.3.26.4, a
    /// NamespaceExpose (<see cref="ExposeRecursionKind.NamespaceRecursive"/> and
    /// <see cref="ExposeRecursionKind.NamespaceDirectChildren"/>) exposes the subject's
    /// Memberships — its members — not the subject itself, so a candidate that is only reachable
    /// via "candidate == subject" for a namespace-form subject must not be treated as root-relevant;
    /// otherwise the excluded subject would still be drawn as the diagram's outer box.
    /// </para>
    /// </remarks>
    /// <param name="candidateQualifiedName">The candidate root definition's qualified name.</param>
    /// <param name="scope">The resolved <c>expose</c> scope.</param>
    /// <returns>
    /// <see langword="true"/> when the candidate root is relevant to the resolved scope;
    /// otherwise <see langword="false"/>.
    /// </returns>
    public static bool IsRootRelevantToScope(string candidateQualifiedName, ExposedScope scope)
    {
        bool RelevantToSubject(ExposeSubject subject)
        {
            var subjectName = subject.QualifiedName;

            // Containment in either direction is always relevant, regardless of recursion kind:
            // the candidate descending from the subject (unless the descendant is one the subject's
            // imported-side visibility hides), or an exposed subject/feature nested inside the
            // candidate.
            if (subject.CoversDescendant(candidateQualifiedName) ||
                subjectName.StartsWith(candidateQualifiedName + "::", StringComparison.Ordinal))
            {
                return true;
            }

            // "candidate == subject" is only relevant when this subject's recursion kind actually
            // includes the subject itself in scope -- NamespaceRecursive/NamespaceDirectChildren
            // exclude the subject (see remarks), so an excluded subject must not be root-relevant
            // purely by virtue of matching itself.
            return candidateQualifiedName == subjectName &&
                subject.Recursion is ExposeRecursionKind.MembershipRecursive or ExposeRecursionKind.MembershipExact;
        }

        bool RelevantToMember(string member) =>
            candidateQualifiedName == member ||
            candidateQualifiedName.StartsWith(member + "::", StringComparison.Ordinal) ||
            member.StartsWith(candidateQualifiedName + "::", StringComparison.Ordinal);

        return scope.Subjects.Any(RelevantToSubject) || scope.ExplicitMembers.Any(RelevantToMember);
    }

    /// <summary>
    /// Decides, for the scoped case, whether <paramref name="candidateQualifiedName"/> should replace
    /// the current best scope-relevant root candidate. Specificity (containment depth) is compared
    /// first: because SysML v2 qualified names are built by <c>parent::child</c> concatenation, any
    /// genuine descendant has strictly more <c>"::"</c>-separated segments than its ancestors, so the
    /// candidate with the greater containment depth always wins over a shallower one regardless of
    /// score. Each strategy's own score heuristic (transition/connection+part/succession+action/
    /// message count) is used only as a fallback to break ties between candidates of equal
    /// containment depth (e.g. unrelated siblings), via <paramref name="currentScoreIsBetter"/>.
    /// </summary>
    /// <param name="candidateQualifiedName">The candidate root definition's qualified name.</param>
    /// <param name="currentBestQualifiedName">
    /// The current best candidate's qualified name, or <see langword="null"/> when no candidate has
    /// been selected yet.
    /// </param>
    /// <param name="currentScoreIsBetter">
    /// Whether the candidate's own per-strategy score is better than the current best's, used only
    /// when the two qualified names are equally deeply nested.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the candidate should become the new best root; otherwise
    /// <see langword="false"/>.
    /// </returns>
    public static bool IsMoreSpecificCandidate(
        string candidateQualifiedName,
        string? currentBestQualifiedName,
        bool currentScoreIsBetter)
    {
        if (currentBestQualifiedName is null)
        {
            return true;
        }

        var candidateDepth = CountSegments(candidateQualifiedName);
        var currentBestDepth = CountSegments(currentBestQualifiedName);

        return candidateDepth != currentBestDepth
            ? candidateDepth > currentBestDepth
            : currentScoreIsBetter;
    }

    /// <summary>
    /// Counts the containment depth of a qualified name: the number of <c>"::"</c>-separated
    /// segments. A bare simple name with no <c>"::"</c> separator has depth 1, not 0.
    /// </summary>
    /// <param name="qualifiedName">The qualified name to measure.</param>
    /// <returns>The number of segments in <paramref name="qualifiedName"/>.</returns>
    private static int CountSegments(string qualifiedName) =>
        qualifiedName.Split("::", StringSplitOptions.None).Length;
}
