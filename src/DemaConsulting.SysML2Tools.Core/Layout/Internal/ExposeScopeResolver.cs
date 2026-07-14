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
}

/// <summary>
/// A single resolved <c>expose</c> subject qualified name paired with the
/// <see cref="ExposeRecursionKind"/> that governs how it matches a candidate qualified name.
/// </summary>
/// <param name="QualifiedName">The exposed subject's resolved qualified name.</param>
/// <param name="Recursion">
/// The recursion kind (exact, direct-children-only, or whole-subtree) governing how this subject
/// matches candidate qualified names — see <see cref="ExposeScopeResolver.IsInSubjectScope"/>.
/// </param>
internal sealed record ExposeSubject(string QualifiedName, ExposeRecursionKind Recursion);

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
        var exposedTargets = viewNode?.ResolvedEdges
            .Where(edge => edge.Kind == SysmlEdgeKind.Expose)
            .Select(edge => edge.TargetQualifiedName)
            .ToList();
        if (exposedTargets is not { Count: > 0 })
        {
            return null;
        }

        var subjects = new List<ExposeSubject>();
        var explicitMembers = new List<string>();
        var failures = new List<BracketFilterFailure>();

        // Re-pair each resolved Expose edge's target with the ExposeMember it originated from, so
        // a bracket-filtered entry's expression can be evaluated against its own containment
        // subtree only — not the view's other, unrelated expose entries. ReferenceResolver builds
        // one edge per successfully-resolved ExposeMember in source order (skipping entries that
        // failed to resolve, without emitting an edge for them), so a forward scan that skips
        // non-matching entries reliably re-establishes the pairing even when an earlier entry
        // failed to resolve.
        var members = viewNode!.ExposeMembers;
        var memberIndex = 0;
        foreach (var target in exposedTargets)
        {
            ExposeMember? member = null;
            while (memberIndex < members.Count)
            {
                var candidate = members[memberIndex];
                memberIndex++;
                if (candidate.QualifiedName == target ||
                    target.EndsWith("::" + candidate.QualifiedName, StringComparison.Ordinal))
                {
                    member = candidate;
                    break;
                }
            }

            var bracketFilterText = member?.BracketFilterExpressionText;
            var recursionKind = member?.RecursionKind ?? ExposeRecursionKind.MembershipRecursive;
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
        }

        return new ExposedScope(subjects, explicitMembers) { Failures = failures };
    }

    /// <summary>
    /// Adds <paramref name="target"/> (and, when it resolves to a usage, its resolved type's
    /// qualified name — using the same <paramref name="kind"/> for both) to
    /// <paramref name="subjects"/> — the existing Phase 1 whole-subtree inclusion behavior for
    /// recursive kinds, now correctly narrowed to exact/direct-children matching for non-recursive
    /// kinds, shared by unfiltered <c>expose</c> entries and bracket-filtered entries that fell
    /// back after a parse/evaluation failure.
    /// </summary>
    private static void AddSubject(
        SysmlWorkspace workspace,
        string target,
        ExposeRecursionKind kind,
        List<ExposeSubject> subjects)
    {
        subjects.Add(new ExposeSubject(target, kind));

        if (workspace.Declarations.TryGetValue(target, out var declaration) &&
            declaration is SysmlFeatureNode { } feature)
        {
            var typeTarget = feature.ResolvedEdges
                .FirstOrDefault(edge => edge.Kind == SysmlEdgeKind.Typing)
                ?.TargetQualifiedName;
            if (typeTarget is not null)
            {
                subjects.Add(new ExposeSubject(typeTarget, kind));
            }
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="qualifiedName"/> matches one of
    /// <paramref name="scope"/>'s <see cref="ExposedScope.Subjects"/> per that subject's own
    /// <see cref="ExposeRecursionKind"/>:
    /// <list type="bullet">
    /// <item><description><see cref="ExposeRecursionKind.MembershipRecursive"/>/
    /// <see cref="ExposeRecursionKind.NamespaceRecursive"/>: the subject itself or lies within its
    /// containment subtree (a <c>"{subject}::"</c> prefix match, reusing the same
    /// qualified-name-prefix idiom
    /// <see cref="StdlibFilter.IsStdlibElement(string, IReadOnlySet{string})"/> already uses for
    /// stdlib-prefix matching).</description></item>
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
            ExposeRecursionKind.MembershipRecursive or ExposeRecursionKind.NamespaceRecursive =>
                qualifiedName == subject.QualifiedName ||
                qualifiedName.StartsWith(subject.QualifiedName + "::", StringComparison.Ordinal),
            ExposeRecursionKind.MembershipExact => qualifiedName == subject.QualifiedName,
            ExposeRecursionKind.NamespaceDirectChildren => IsDirectChildOf(qualifiedName, subject.QualifiedName),
            _ => false,
        }) ||
        scope.ExplicitMembers.Contains(qualifiedName, StringComparer.Ordinal);

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
    /// direction: the candidate itself is an exposed subject/matched member, the candidate lies
    /// within an exposed subject's containment subtree, or an exposed subject/matched member lies
    /// within the candidate's own containment subtree (the common "expose an inner
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
    /// </remarks>
    /// <param name="candidateQualifiedName">The candidate root definition's qualified name.</param>
    /// <param name="scope">The resolved <c>expose</c> scope.</param>
    /// <returns>
    /// <see langword="true"/> when the candidate root is relevant to the resolved scope;
    /// otherwise <see langword="false"/>.
    /// </returns>
    public static bool IsRootRelevantToScope(string candidateQualifiedName, ExposedScope scope)
    {
        bool RelevantTo(string subject) =>
            candidateQualifiedName == subject ||
            candidateQualifiedName.StartsWith(subject + "::", StringComparison.Ordinal) ||
            subject.StartsWith(candidateQualifiedName + "::", StringComparison.Ordinal);

        return scope.Subjects.Select(s => s.QualifiedName).Any(RelevantTo) || scope.ExplicitMembers.Any(RelevantTo);
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
