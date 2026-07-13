// <copyright file="ExposeScopeResolverTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

// cspell:ignore istype

using DemaConsulting.SysML2Tools.Layout.Internal;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;

namespace DemaConsulting.SysML2Tools.Tests.Layout;

/// <summary>
///     Direct unit tests for the shared <see cref="ExposeScopeResolver"/> used by every layout
///     strategy to honor <c>expose</c> scoping.
/// </summary>
public sealed class ExposeScopeResolverTests
{
    /// <summary>
    ///     A null <c>ViewNode</c> (the synthetic <c>--auto</c> view) resolves to <c>null</c> scope,
    ///     meaning "no scoping applies" — every non-stdlib element is included.
    /// </summary>
    [Fact]
    public void ResolveExposedScope_NullViewNode_ReturnsNull()
    {
        var workspace = new SysmlWorkspace();

        var scope = ExposeScopeResolver.ResolveExposedScope(workspace, null);

        Assert.Null(scope);
    }

    /// <summary>
    ///     A <c>ViewNode</c> with no resolved <c>Expose</c> edges (no <c>expose</c> statement, or
    ///     every entry failed to resolve) resolves to <c>null</c> scope.
    /// </summary>
    [Fact]
    public void ResolveExposedScope_NoResolvedExposeEdges_ReturnsNull()
    {
        var workspace = new SysmlWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            ResolvedEdges = []
        };

        var scope = ExposeScopeResolver.ResolveExposedScope(workspace, viewNode);

        Assert.Null(scope);
    }

    /// <summary>
    ///     A resolved <c>Expose</c> edge targeting a definition, with no bracket filter, resolves to
    ///     a scope whose <see cref="ExposedScope.Subjects"/> contains exactly that
    ///     definition's qualified name.
    /// </summary>
    [Fact]
    public void ResolveExposedScope_ExposedDefinition_ReturnsThatQualifiedName()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["Root::A"] = new SysmlDefinitionNode { Name = "A", QualifiedName = "Root::A", DefinitionKeyword = "part def" }
            }
        };
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            ExposeMembers = [new ExposeMember("Root::A", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("Root::V", "Root::A", SysmlEdgeKind.Expose)]
        };

        var scope = ExposeScopeResolver.ResolveExposedScope(workspace, viewNode);

        Assert.NotNull(scope);
        Assert.Equal(["Root::A"], scope.Subjects.Select(s => s.QualifiedName));
        Assert.Empty(scope.ExplicitMembers);
        Assert.Empty(scope.Failures);
    }

    /// <summary>
    ///     When an exposed target resolves to a feature usage (not a definition), the resolved
    ///     scope also includes the usage's own <c>Typing</c> edge target, so the type's
    ///     containment subtree is included alongside the usage itself (usage-to-type fallback).
    /// </summary>
    [Fact]
    public void ResolveExposedScope_ExposedUsage_AlsoIncludesResolvedTypeTarget()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["Root::Vehicle"] = new SysmlDefinitionNode { Name = "Vehicle", QualifiedName = "Root::Vehicle", DefinitionKeyword = "part def" },
                ["Root::myVehicle"] = new SysmlFeatureNode
                {
                    Name = "myVehicle",
                    QualifiedName = "Root::myVehicle",
                    FeatureTyping = "Vehicle",
                    ResolvedEdges = [new SysmlEdge("Root::myVehicle", "Root::Vehicle", SysmlEdgeKind.Typing)]
                }
            }
        };
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            ExposeMembers = [new ExposeMember("Root::myVehicle", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("Root::V", "Root::myVehicle", SysmlEdgeKind.Expose)]
        };

        var scope = ExposeScopeResolver.ResolveExposedScope(workspace, viewNode);

        Assert.NotNull(scope);
        Assert.Contains("Root::myVehicle", scope.Subjects.Select(s => s.QualifiedName));
        Assert.Contains("Root::Vehicle", scope.Subjects.Select(s => s.QualifiedName));
    }

    /// <summary>An exact qualified-name match is in scope.</summary>
    [Fact]
    public void IsInSubjectScope_ExactMatch_ReturnsTrue()
    {
        Assert.True(ExposeScopeResolver.IsInSubjectScope("Root::A", new ExposedScope([new ExposeSubject("Root::A", ExposeRecursionKind.MembershipRecursive)], [])));
    }

    /// <summary>A qualified name nested under a subject's containment subtree is in scope.</summary>
    [Fact]
    public void IsInSubjectScope_SubtreeMatch_ReturnsTrue()
    {
        Assert.True(ExposeScopeResolver.IsInSubjectScope("Root::A::Child", new ExposedScope([new ExposeSubject("Root::A", ExposeRecursionKind.MembershipRecursive)], [])));
    }

    /// <summary>
    ///     A qualified name that merely shares a string prefix with a subject, without the
    ///     <c>"::"</c> separator, is not considered a subtree match (e.g. <c>Root::AB</c> is not in
    ///     scope for subject <c>Root::A</c>).
    /// </summary>
    [Fact]
    public void IsInSubjectScope_PrefixWithoutSeparator_ReturnsFalse()
    {
        Assert.False(ExposeScopeResolver.IsInSubjectScope("Root::AB", new ExposedScope([new ExposeSubject("Root::A", ExposeRecursionKind.MembershipRecursive)], [])));
    }

    /// <summary>An unrelated qualified name is not in scope.</summary>
    [Fact]
    public void IsInSubjectScope_UnrelatedName_ReturnsFalse()
    {
        Assert.False(ExposeScopeResolver.IsInSubjectScope("Root::B", new ExposedScope([new ExposeSubject("Root::A", ExposeRecursionKind.MembershipRecursive)], [])));
    }

    /// <summary>An explicit (bracket-filter-matched) member is in scope by exact match only.</summary>
    [Fact]
    public void IsInSubjectScope_ExplicitMemberExactMatch_ReturnsTrue()
    {
        Assert.True(ExposeScopeResolver.IsInSubjectScope("Root::A::Matched", new ExposedScope([], ["Root::A::Matched"])));
    }

    /// <summary>
    ///     An explicit (bracket-filter-matched) member's own descendants are not automatically in
    ///     scope — only the exact matched qualified name is included.
    /// </summary>
    [Fact]
    public void IsInSubjectScope_ExplicitMemberDescendant_ReturnsFalse()
    {
        Assert.False(ExposeScopeResolver.IsInSubjectScope("Root::A::Matched::Nested", new ExposedScope([], ["Root::A::Matched"])));
    }

    /// <summary>A candidate root that is itself an exposed subject is relevant to the scope.</summary>
    [Fact]
    public void IsRootRelevantToScope_CandidateEqualsSubject_ReturnsTrue()
    {
        Assert.True(ExposeScopeResolver.IsRootRelevantToScope("Root::A", new ExposedScope([new ExposeSubject("Root::A", ExposeRecursionKind.MembershipRecursive)], [])));
    }

    /// <summary>
    ///     A candidate root nested within an exposed subject's containment subtree is relevant to
    ///     the scope.
    /// </summary>
    [Fact]
    public void IsRootRelevantToScope_CandidateNestedInSubject_ReturnsTrue()
    {
        Assert.True(ExposeScopeResolver.IsRootRelevantToScope("Root::A::Child", new ExposedScope([new ExposeSubject("Root::A", ExposeRecursionKind.MembershipRecursive)], [])));
    }

    /// <summary>
    ///     A candidate root that contains an exposed subject within its own containment subtree
    ///     (the common "expose an inner element of the heuristic root" case) is relevant to the
    ///     scope.
    /// </summary>
    [Fact]
    public void IsRootRelevantToScope_SubjectNestedInCandidate_ReturnsTrue()
    {
        Assert.True(ExposeScopeResolver.IsRootRelevantToScope("Root::A", new ExposedScope([new ExposeSubject("Root::A::Child", ExposeRecursionKind.MembershipRecursive)], [])));
    }

    /// <summary>A candidate root unrelated to any exposed subject is not relevant to the scope.</summary>
    [Fact]
    public void IsRootRelevantToScope_UnrelatedCandidate_ReturnsFalse()
    {
        Assert.False(ExposeScopeResolver.IsRootRelevantToScope("Root::B", new ExposedScope([new ExposeSubject("Root::A", ExposeRecursionKind.MembershipRecursive)], [])));
    }

    /// <summary>An explicit (bracket-filter-matched) member is itself relevant to the scope.</summary>
    [Fact]
    public void IsRootRelevantToScope_ExplicitMember_ReturnsTrue()
    {
        Assert.True(ExposeScopeResolver.IsRootRelevantToScope("Root::Matched", new ExposedScope([], ["Root::Matched"])));
    }

    /// <summary>With no current best candidate, any candidate becomes the new best.</summary>
    [Fact]
    public void IsMoreSpecificCandidate_NoCurrentBest_ReturnsTrue()
    {
        Assert.True(ExposeScopeResolver.IsMoreSpecificCandidate("Root::A", null, currentScoreIsBetter: false));
    }

    /// <summary>
    ///     A candidate with a longer (more deeply nested) qualified name always wins over the
    ///     current best, even when its own score is worse.
    /// </summary>
    [Fact]
    public void IsMoreSpecificCandidate_LongerQualifiedName_ReturnsTrueRegardlessOfScore()
    {
        Assert.True(ExposeScopeResolver.IsMoreSpecificCandidate("Root::A::Child", "Root::A", currentScoreIsBetter: false));
    }

    /// <summary>
    ///     A candidate with a shorter qualified name than the current best always loses, even when
    ///     its own score is better.
    /// </summary>
    [Fact]
    public void IsMoreSpecificCandidate_ShorterQualifiedName_ReturnsFalseRegardlessOfScore()
    {
        Assert.False(ExposeScopeResolver.IsMoreSpecificCandidate("Root::A", "Root::A::Child", currentScoreIsBetter: true));
    }

    /// <summary>
    ///     When the candidate and current best have equal-length qualified names (e.g. unrelated
    ///     siblings), the decision falls back to the caller-supplied score comparison — true case.
    /// </summary>
    [Fact]
    public void IsMoreSpecificCandidate_EqualLength_FallsBackToScore_True()
    {
        Assert.True(ExposeScopeResolver.IsMoreSpecificCandidate("Root::SysB", "Root::SysA", currentScoreIsBetter: true));
    }

    /// <summary>
    ///     When the candidate and current best have equal-length qualified names (e.g. unrelated
    ///     siblings), the decision falls back to the caller-supplied score comparison — false case.
    /// </summary>
    [Fact]
    public void IsMoreSpecificCandidate_EqualLength_FallsBackToScore_False()
    {
        Assert.False(ExposeScopeResolver.IsMoreSpecificCandidate("Root::SysB", "Root::SysA", currentScoreIsBetter: false));
    }

    /// <summary>
    ///     Same-depth sibling candidates with different qualified-name *lengths* fall back to the
    ///     caller-supplied score comparison — depth, not raw string length, drives the decision — true
    ///     case (the shorter, better-scoring candidate wins).
    /// </summary>
    [Fact]
    public void IsMoreSpecificCandidate_SameDepthSiblingsDifferentLength_ShorterWithBetterScoreWins()
    {
        Assert.True(ExposeScopeResolver.IsMoreSpecificCandidate("Pkg::AB", "Pkg::MuchLongerSiblingName", currentScoreIsBetter: true));
    }

    /// <summary>
    ///     Same-depth sibling candidates with different qualified-name lengths fall back to the
    ///     caller-supplied score comparison — false case (the shorter candidate loses when its score
    ///     is not better).
    /// </summary>
    [Fact]
    public void IsMoreSpecificCandidate_SameDepthSiblingsDifferentLength_ShorterWithWorseScoreLoses()
    {
        Assert.False(ExposeScopeResolver.IsMoreSpecificCandidate("Pkg::AB", "Pkg::MuchLongerSiblingName", currentScoreIsBetter: false));
    }

    /// <summary>
    ///     Two resolved <c>Expose</c> edges on the same view — one targeting a plain definition, one
    ///     targeting a feature usage — union both targets plus the usage's resolved type, proving the
    ///     usage-to-type fallback fires per-target within a multi-expose view rather than only when a
    ///     single usage is exposed alone.
    /// </summary>
    [Fact]
    public void ResolveExposedScope_TwoExposeEdges_DefinitionAndUsageTarget_UnionsBothPlusResolvedType()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["Root::A"] = new SysmlDefinitionNode { Name = "A", QualifiedName = "Root::A", DefinitionKeyword = "part def" },
                ["Root::Vehicle"] = new SysmlDefinitionNode { Name = "Vehicle", QualifiedName = "Root::Vehicle", DefinitionKeyword = "part def" },
                ["Root::myVehicle"] = new SysmlFeatureNode
                {
                    Name = "myVehicle",
                    QualifiedName = "Root::myVehicle",
                    FeatureTyping = "Vehicle",
                    ResolvedEdges = [new SysmlEdge("Root::myVehicle", "Root::Vehicle", SysmlEdgeKind.Typing)]
                }
            }
        };
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            ExposeMembers = [new ExposeMember("Root::A", null, ExposeRecursionKind.MembershipRecursive), new ExposeMember("Root::myVehicle", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges =
            [
                new SysmlEdge("Root::V", "Root::A", SysmlEdgeKind.Expose),
                new SysmlEdge("Root::V", "Root::myVehicle", SysmlEdgeKind.Expose)
            ]
        };

        var scope = ExposeScopeResolver.ResolveExposedScope(workspace, viewNode);

        Assert.NotNull(scope);
        Assert.Contains("Root::A", scope.Subjects.Select(s => s.QualifiedName));
        Assert.Contains("Root::myVehicle", scope.Subjects.Select(s => s.QualifiedName));
        Assert.Contains("Root::Vehicle", scope.Subjects.Select(s => s.QualifiedName));
    }

    /// <summary>
    ///     A bracket-filtered <c>expose</c> entry's candidate set also includes usage-level
    ///     (<see cref="SysmlFeatureNode"/>) declarations, not only <see cref="SysmlDefinitionNode"/>s
    ///     (Phase 2d) — a metaclass-kind classification test (<c>@SysML::PartUsage</c>) can
    ///     therefore match a part usage nested within the exposed target's containment subtree.
    /// </summary>
    [Fact]
    public void ResolveExposedScope_BracketFilterMetaclassKind_MatchesUsageLevelCandidate()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["Root::Container"] = new SysmlDefinitionNode { Name = "Container", QualifiedName = "Root::Container", DefinitionKeyword = "part def" },
                ["Root::Container::myPart"] = new SysmlFeatureNode { Name = "myPart", QualifiedName = "Root::Container::myPart", FeatureKeyword = "part" },
                ["Root::Container::myRequirement"] = new SysmlFeatureNode { Name = "myRequirement", QualifiedName = "Root::Container::myRequirement", FeatureKeyword = "requirement" }
            }
        };
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            ExposeMembers = [new ExposeMember("Root::Container", "@SysML::PartUsage", ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("Root::V", "Root::Container", SysmlEdgeKind.Expose)]
        };

        var scope = ExposeScopeResolver.ResolveExposedScope(workspace, viewNode);

        Assert.NotNull(scope);
        Assert.Equal(["Root::Container::myPart"], scope.ExplicitMembers);
        Assert.Empty(scope.Failures);
    }

    /// <summary>
    ///     A bracket-filtered <c>expose</c> entry (<c>expose Root::Container::**[@Safety]</c>) that
    ///     parses and evaluates successfully narrows to only the descendant <em>definitions</em>
    ///     under the target's containment subtree that carry the <c>@Safety</c> metadata
    ///     annotation — the target itself and its unannotated sibling are excluded from
    ///     <see cref="ExposedScope.ExplicitMembers"/>, and the target is not added to
    ///     <see cref="ExposedScope.Subjects"/> (no whole-subtree fallback).
    /// </summary>
    [Fact]
    public void ResolveExposedScope_BracketFilterEvaluatesSuccessfully_NarrowsToMatchedDefinitionsOnly()
    {
        var safetyMetadata = new SysmlMetadataNode
        {
            TypeReference = "Safety",
            ResolvedEdges = [new SysmlEdge("Root::Container::Matched", "Root::Safety", SysmlEdgeKind.MetadataType)]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["Root::Container"] = new SysmlDefinitionNode { Name = "Container", QualifiedName = "Root::Container", DefinitionKeyword = "part def" },
                ["Root::Container::Matched"] = new SysmlDefinitionNode
                {
                    Name = "Matched",
                    QualifiedName = "Root::Container::Matched",
                    DefinitionKeyword = "part def",
                    Children = [safetyMetadata]
                },
                ["Root::Container::Unmatched"] = new SysmlDefinitionNode { Name = "Unmatched", QualifiedName = "Root::Container::Unmatched", DefinitionKeyword = "part def" }
            }
        };
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            ExposeMembers = [new ExposeMember("Root::Container", "@Safety", ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("Root::V", "Root::Container", SysmlEdgeKind.Expose)]
        };

        var scope = ExposeScopeResolver.ResolveExposedScope(workspace, viewNode);

        Assert.NotNull(scope);
        Assert.Empty(scope.Subjects);
        Assert.Equal(["Root::Container::Matched"], scope.ExplicitMembers);
        Assert.Empty(scope.Failures);
    }

    /// <summary>
    ///     Two <c>expose</c> entries on the same view, only one bracket-filtered, each narrow
    ///     independently: the unfiltered entry keeps its whole containment subtree
    ///     (<see cref="ExposedScope.Subjects"/>), while the bracket-filtered entry narrows to
    ///     only its own matched descendant definitions (<see cref="ExposedScope.ExplicitMembers"/>) —
    ///     proving a bracket filter on one entry does not affect an unrelated entry's contribution.
    /// </summary>
    [Fact]
    public void ResolveExposedScope_MixedFilteredAndUnfilteredEntries_NarrowsIndependently()
    {
        var safetyMetadata = new SysmlMetadataNode
        {
            TypeReference = "Safety",
            ResolvedEdges = [new SysmlEdge("Root::Container::Matched", "Root::Safety", SysmlEdgeKind.MetadataType)]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["Root::Plain"] = new SysmlDefinitionNode { Name = "Plain", QualifiedName = "Root::Plain", DefinitionKeyword = "part def" },
                ["Root::Container"] = new SysmlDefinitionNode { Name = "Container", QualifiedName = "Root::Container", DefinitionKeyword = "part def" },
                ["Root::Container::Matched"] = new SysmlDefinitionNode
                {
                    Name = "Matched",
                    QualifiedName = "Root::Container::Matched",
                    DefinitionKeyword = "part def",
                    Children = [safetyMetadata]
                },
                ["Root::Container::Unmatched"] = new SysmlDefinitionNode { Name = "Unmatched", QualifiedName = "Root::Container::Unmatched", DefinitionKeyword = "part def" }
            }
        };
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            ExposeMembers =
            [
                new ExposeMember("Root::Plain", null, ExposeRecursionKind.MembershipRecursive),
                new ExposeMember("Root::Container", "@Safety", ExposeRecursionKind.MembershipRecursive)
            ],
            ResolvedEdges =
            [
                new SysmlEdge("Root::V", "Root::Plain", SysmlEdgeKind.Expose),
                new SysmlEdge("Root::V", "Root::Container", SysmlEdgeKind.Expose)
            ]
        };

        var scope = ExposeScopeResolver.ResolveExposedScope(workspace, viewNode);

        Assert.NotNull(scope);
        Assert.Equal(["Root::Plain"], scope.Subjects.Select(s => s.QualifiedName));
        Assert.Equal(["Root::Container::Matched"], scope.ExplicitMembers);
        Assert.Empty(scope.Failures);
    }

    /// <summary>
    ///     A bracket-filter expression that fails to parse (an unsupported Phase 1 construct)
    ///     degrades gracefully to whole-subtree inclusion for that entry (via
    ///     <see cref="ExposedScope.Subjects"/>) and records the failure (with a reason) in
    ///     <see cref="ExposedScope.Failures"/>, rather than crashing or silently excluding the
    ///     entry.
    /// </summary>
    [Fact]
    public void ResolveExposedScope_BracketFilterFailsToParse_FallsBackToWholeSubtreeAndRecordsFailure()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["Root::Container"] = new SysmlDefinitionNode { Name = "Container", QualifiedName = "Root::Container", DefinitionKeyword = "part def" }
            }
        };
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            ExposeMembers = [new ExposeMember("Root::Container", "x istype Y", ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("Root::V", "Root::Container", SysmlEdgeKind.Expose)]
        };

        var scope = ExposeScopeResolver.ResolveExposedScope(workspace, viewNode);

        Assert.NotNull(scope);
        Assert.Equal(["Root::Container"], scope.Subjects.Select(s => s.QualifiedName));
        Assert.Empty(scope.ExplicitMembers);
        var failure = Assert.Single(scope.Failures);
        Assert.Equal("x istype Y", failure.ExpressionText);
        Assert.NotNull(failure.Reason);
    }

    /// <summary>
    ///     A non-recursive <c>MembershipExact</c> expose entry (<c>expose X;</c>) resolves to a
    ///     scope containing only the exposed subject itself — its containment subtree is not
    ///     included.
    /// </summary>
    [Fact]
    public void ResolveExposedScope_MembershipExact_ScopeIsExactOnly()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["Root::A"] = new SysmlDefinitionNode { Name = "A", QualifiedName = "Root::A", DefinitionKeyword = "part def" },
                ["Root::A::Child"] = new SysmlDefinitionNode { Name = "Child", QualifiedName = "Root::A::Child", DefinitionKeyword = "part def" }
            }
        };
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            ExposeMembers = [new ExposeMember("Root::A", null, ExposeRecursionKind.MembershipExact)],
            ResolvedEdges = [new SysmlEdge("Root::V", "Root::A", SysmlEdgeKind.Expose)]
        };

        var scope = ExposeScopeResolver.ResolveExposedScope(workspace, viewNode);

        Assert.NotNull(scope);
        Assert.True(ExposeScopeResolver.IsInSubjectScope("Root::A", scope));
        Assert.False(ExposeScopeResolver.IsInSubjectScope("Root::A::Child", scope));
    }

    /// <summary>
    ///     A non-recursive <c>MembershipExact</c> expose entry targeting a usage still adds the
    ///     usage's resolved type, using the same exact-match recursion kind — the type's own
    ///     descendants are not included either.
    /// </summary>
    [Fact]
    public void ResolveExposedScope_MembershipExactUsage_TypeFallbackIsAlsoExactOnly()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["Root::Vehicle"] = new SysmlDefinitionNode { Name = "Vehicle", QualifiedName = "Root::Vehicle", DefinitionKeyword = "part def" },
                ["Root::Vehicle::Engine"] = new SysmlDefinitionNode { Name = "Engine", QualifiedName = "Root::Vehicle::Engine", DefinitionKeyword = "part def" },
                ["Root::myVehicle"] = new SysmlFeatureNode
                {
                    Name = "myVehicle",
                    QualifiedName = "Root::myVehicle",
                    FeatureTyping = "Vehicle",
                    ResolvedEdges = [new SysmlEdge("Root::myVehicle", "Root::Vehicle", SysmlEdgeKind.Typing)]
                }
            }
        };
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            ExposeMembers = [new ExposeMember("Root::myVehicle", null, ExposeRecursionKind.MembershipExact)],
            ResolvedEdges = [new SysmlEdge("Root::V", "Root::myVehicle", SysmlEdgeKind.Expose)]
        };

        var scope = ExposeScopeResolver.ResolveExposedScope(workspace, viewNode);

        Assert.NotNull(scope);
        Assert.True(ExposeScopeResolver.IsInSubjectScope("Root::myVehicle", scope));
        Assert.True(ExposeScopeResolver.IsInSubjectScope("Root::Vehicle", scope));
        Assert.False(ExposeScopeResolver.IsInSubjectScope("Root::Vehicle::Engine", scope));
    }

    /// <summary>
    ///     A recursive <c>MembershipRecursive</c> expose entry (<c>expose X::**;</c>) resolves to a
    ///     scope containing the exposed subject's entire containment subtree — unchanged whole
    ///     subtree behavior.
    /// </summary>
    [Fact]
    public void ResolveExposedScope_MembershipRecursive_ScopeIsWholeSubtree()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["Root::A"] = new SysmlDefinitionNode { Name = "A", QualifiedName = "Root::A", DefinitionKeyword = "part def" },
                ["Root::A::Child"] = new SysmlDefinitionNode { Name = "Child", QualifiedName = "Root::A::Child", DefinitionKeyword = "part def" }
            }
        };
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            ExposeMembers = [new ExposeMember("Root::A", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("Root::V", "Root::A", SysmlEdgeKind.Expose)]
        };

        var scope = ExposeScopeResolver.ResolveExposedScope(workspace, viewNode);

        Assert.NotNull(scope);
        Assert.True(ExposeScopeResolver.IsInSubjectScope("Root::A", scope));
        Assert.True(ExposeScopeResolver.IsInSubjectScope("Root::A::Child", scope));
    }

    /// <summary>
    ///     A non-recursive <c>NamespaceDirectChildren</c> expose entry (<c>expose X::*;</c>)
    ///     resolves to a scope containing only <c>X</c>'s direct children — not <c>X</c> itself and
    ///     not deeper (grandchild) descendants.
    /// </summary>
    [Fact]
    public void ResolveExposedScope_NamespaceDirectChildren_ScopeIsDirectChildrenOnly()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["Root::A"] = new SysmlDefinitionNode { Name = "A", QualifiedName = "Root::A", DefinitionKeyword = "part def" },
                ["Root::A::Child"] = new SysmlDefinitionNode { Name = "Child", QualifiedName = "Root::A::Child", DefinitionKeyword = "part def" },
                ["Root::A::Child::Grandchild"] = new SysmlDefinitionNode { Name = "Grandchild", QualifiedName = "Root::A::Child::Grandchild", DefinitionKeyword = "part def" }
            }
        };
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            ExposeMembers = [new ExposeMember("Root::A", null, ExposeRecursionKind.NamespaceDirectChildren)],
            ResolvedEdges = [new SysmlEdge("Root::V", "Root::A", SysmlEdgeKind.Expose)]
        };

        var scope = ExposeScopeResolver.ResolveExposedScope(workspace, viewNode);

        Assert.NotNull(scope);
        Assert.False(ExposeScopeResolver.IsInSubjectScope("Root::A", scope));
        Assert.True(ExposeScopeResolver.IsInSubjectScope("Root::A::Child", scope));
        Assert.False(ExposeScopeResolver.IsInSubjectScope("Root::A::Child::Grandchild", scope));
    }

    /// <summary>
    ///     A recursive <c>NamespaceRecursive</c> expose entry (<c>expose X::*::**;</c>) resolves to
    ///     a scope containing <c>X</c> and its entire containment subtree — unchanged whole subtree
    ///     behavior.
    /// </summary>
    [Fact]
    public void ResolveExposedScope_NamespaceRecursive_ScopeIsWholeSubtree()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["Root::A"] = new SysmlDefinitionNode { Name = "A", QualifiedName = "Root::A", DefinitionKeyword = "part def" },
                ["Root::A::Child"] = new SysmlDefinitionNode { Name = "Child", QualifiedName = "Root::A::Child", DefinitionKeyword = "part def" },
                ["Root::A::Child::Grandchild"] = new SysmlDefinitionNode { Name = "Grandchild", QualifiedName = "Root::A::Child::Grandchild", DefinitionKeyword = "part def" }
            }
        };
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            ExposeMembers = [new ExposeMember("Root::A", null, ExposeRecursionKind.NamespaceRecursive)],
            ResolvedEdges = [new SysmlEdge("Root::V", "Root::A", SysmlEdgeKind.Expose)]
        };

        var scope = ExposeScopeResolver.ResolveExposedScope(workspace, viewNode);

        Assert.NotNull(scope);
        Assert.True(ExposeScopeResolver.IsInSubjectScope("Root::A", scope));
        Assert.True(ExposeScopeResolver.IsInSubjectScope("Root::A::Child", scope));
        Assert.True(ExposeScopeResolver.IsInSubjectScope("Root::A::Child::Grandchild", scope));
    }

    /// <summary>
    ///     A bracket-filtered expose entry that fails to parse falls back to whole-subtree
    ///     inclusion regardless of the entry's own recursion kind — even a non-recursive
    ///     <c>MembershipExact</c>-classified bracket-filter entry (a hypothetical malformed
    ///     grammar edge case) degrades to recursive fallback, per the documented
    ///     "don't silently lose content" fallback intent.
    /// </summary>
    [Fact]
    public void ResolveExposedScope_BracketFilterFailure_AlwaysFallsBackToRecursive()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["Root::A"] = new SysmlDefinitionNode { Name = "A", QualifiedName = "Root::A", DefinitionKeyword = "part def" },
                ["Root::A::Child"] = new SysmlDefinitionNode { Name = "Child", QualifiedName = "Root::A::Child", DefinitionKeyword = "part def" }
            }
        };
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            ExposeMembers = [new ExposeMember("Root::A", "x istype Y", ExposeRecursionKind.NamespaceDirectChildren)],
            ResolvedEdges = [new SysmlEdge("Root::V", "Root::A", SysmlEdgeKind.Expose)]
        };

        var scope = ExposeScopeResolver.ResolveExposedScope(workspace, viewNode);

        Assert.NotNull(scope);
        Assert.True(ExposeScopeResolver.IsInSubjectScope("Root::A", scope));
        Assert.True(ExposeScopeResolver.IsInSubjectScope("Root::A::Child", scope));
        Assert.Single(scope.Failures);
    }
}

