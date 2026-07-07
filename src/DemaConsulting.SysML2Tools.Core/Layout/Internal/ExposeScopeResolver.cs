// <copyright file="ExposeScopeResolver.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Rendering.Internal;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;

namespace DemaConsulting.SysML2Tools.Layout.Internal;

/// <summary>
/// Shared helper resolving the qualified-name containment-subtree scope a view's <c>expose</c>
/// statements restrict a diagram to, used by every <see cref="ILayoutStrategy"/> so each view kind
/// honors <c>expose</c> scoping identically.
/// </summary>
internal static class ExposeScopeResolver
{
    /// <summary>
    /// Resolves the qualified-name containment-subtree scope a view's <c>expose</c> statements
    /// restrict the diagram to, or <see langword="null"/> when the view has no resolved
    /// <see cref="SysmlEdgeKind.Expose"/> edge — meaning every non-stdlib element is included,
    /// unchanged from the pre-scoping behavior. This covers every "no scoping" case uniformly: a
    /// null <paramref name="viewNode"/> (the <c>--auto</c> synthetic view, which never carries
    /// expose/render/filter data), a view with no <c>expose</c> statement, and a view whose every
    /// <c>expose</c> entry failed to resolve. <c>RenderTargetName</c> (a rendering-style/format
    /// selector, not content) and <c>FilterExpressionText</c> never affect this decision.
    /// </summary>
    /// <remarks>
    /// When an exposed target resolves to a <see cref="SysmlFeatureNode"/> (a usage, e.g.
    /// <c>part myVehicle : Vehicle;</c>) rather than a <see cref="SysmlDefinitionNode"/>, the
    /// usage's own containment subtree is typically empty — the real content lives under its
    /// type's subtree. To avoid silently scoping to nothing, this also resolves the usage's own
    /// <see cref="SysmlEdgeKind.Typing"/> edge (if any) and adds that type's qualified name to the
    /// scope as well, so both the usage and its type's subtree are included.
    /// </remarks>
    /// <param name="workspace">The workspace, used to look up each exposed target's declaration.</param>
    /// <param name="viewNode">The view's AST node, or null for the synthetic <c>--auto</c> view.</param>
    /// <returns>
    /// The list of subject qualified names (each exposed name, plus the resolved type of any
    /// exposed name that names a usage) whose containment subtrees are in scope, or null when no
    /// scoping applies.
    /// </returns>
    public static IReadOnlyList<string>? ResolveExposedScope(SysmlWorkspace workspace, SysmlViewNode? viewNode)
    {
        var exposedTargets = viewNode?.ResolvedEdges
            .Where(edge => edge.Kind == SysmlEdgeKind.Expose)
            .Select(edge => edge.TargetQualifiedName)
            .ToList();
        if (exposedTargets is not { Count: > 0 })
        {
            return null;
        }

        var subjects = new List<string>();
        foreach (var target in exposedTargets)
        {
            subjects.Add(target);

            if (workspace.Declarations.TryGetValue(target, out var declaration) &&
                declaration is SysmlFeatureNode { } feature)
            {
                var typeTarget = feature.ResolvedEdges
                    .FirstOrDefault(edge => edge.Kind == SysmlEdgeKind.Typing)
                    ?.TargetQualifiedName;
                if (typeTarget is not null)
                {
                    subjects.Add(typeTarget);
                }
            }
        }

        return subjects;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="qualifiedName"/> is one of
    /// <paramref name="subjects"/> or lies within one of their containment subtrees (a
    /// <c>"{subject}::"</c> prefix match), reusing the same qualified-name-prefix idiom
    /// <see cref="StdlibFilter.IsStdlibElement(string, IReadOnlySet{string})"/> already uses for
    /// stdlib-prefix matching.
    /// </summary>
    public static bool IsInSubjectScope(string qualifiedName, IReadOnlyList<string> subjects) =>
        subjects.Any(subject =>
            qualifiedName == subject || qualifiedName.StartsWith(subject + "::", StringComparison.Ordinal));

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="candidateQualifiedName"/> (a candidate
    /// single-root diagram root, e.g. the definition a <see cref="InterconnectionViewLayoutStrategy"/>,
    /// <see cref="StateTransitionViewLayoutStrategy"/>, <see cref="ActionFlowViewLayoutStrategy"/>, or
    /// <see cref="SequenceViewLayoutStrategy"/> would otherwise pick by its own heuristic) is related
    /// to the resolved <c>expose</c> scope in <paramref name="subjects"/>, in either containment
    /// direction: the candidate itself is an exposed subject, the candidate lies within an exposed
    /// subject's containment subtree, or an exposed subject lies within the candidate's own
    /// containment subtree (the common "expose an inner state/action/part/lifeline of the root"
    /// case).
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
    /// <param name="subjects">The resolved <c>expose</c> scope subject qualified names.</param>
    /// <returns>
    /// <see langword="true"/> when the candidate root is relevant to the resolved scope;
    /// otherwise <see langword="false"/>.
    /// </returns>
    public static bool IsRootRelevantToScope(string candidateQualifiedName, IReadOnlyList<string> subjects) =>
        subjects.Any(subject =>
            candidateQualifiedName == subject ||
            candidateQualifiedName.StartsWith(subject + "::", StringComparison.Ordinal) ||
            subject.StartsWith(candidateQualifiedName + "::", StringComparison.Ordinal));

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
