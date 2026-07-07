// <copyright file="ExposeScopeResolverTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

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
    ///     A resolved <c>Expose</c> edge targeting a definition resolves to a scope containing
    ///     exactly that definition's qualified name.
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
            ResolvedEdges = [new SysmlEdge("Root::V", "Root::A", SysmlEdgeKind.Expose)]
        };

        var scope = ExposeScopeResolver.ResolveExposedScope(workspace, viewNode);

        Assert.NotNull(scope);
        Assert.Equal(["Root::A"], scope);
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
            ResolvedEdges = [new SysmlEdge("Root::V", "Root::myVehicle", SysmlEdgeKind.Expose)]
        };

        var scope = ExposeScopeResolver.ResolveExposedScope(workspace, viewNode);

        Assert.NotNull(scope);
        Assert.Contains("Root::myVehicle", scope);
        Assert.Contains("Root::Vehicle", scope);
    }

    /// <summary>An exact qualified-name match is in scope.</summary>
    [Fact]
    public void IsInSubjectScope_ExactMatch_ReturnsTrue()
    {
        Assert.True(ExposeScopeResolver.IsInSubjectScope("Root::A", ["Root::A"]));
    }

    /// <summary>A qualified name nested under a subject's containment subtree is in scope.</summary>
    [Fact]
    public void IsInSubjectScope_SubtreeMatch_ReturnsTrue()
    {
        Assert.True(ExposeScopeResolver.IsInSubjectScope("Root::A::Child", ["Root::A"]));
    }

    /// <summary>
    ///     A qualified name that merely shares a string prefix with a subject, without the
    ///     <c>"::"</c> separator, is not considered a subtree match (e.g. <c>Root::AB</c> is not in
    ///     scope for subject <c>Root::A</c>).
    /// </summary>
    [Fact]
    public void IsInSubjectScope_PrefixWithoutSeparator_ReturnsFalse()
    {
        Assert.False(ExposeScopeResolver.IsInSubjectScope("Root::AB", ["Root::A"]));
    }

    /// <summary>An unrelated qualified name is not in scope.</summary>
    [Fact]
    public void IsInSubjectScope_UnrelatedName_ReturnsFalse()
    {
        Assert.False(ExposeScopeResolver.IsInSubjectScope("Root::B", ["Root::A"]));
    }

    /// <summary>A candidate root that is itself an exposed subject is relevant to the scope.</summary>
    [Fact]
    public void IsRootRelevantToScope_CandidateEqualsSubject_ReturnsTrue()
    {
        Assert.True(ExposeScopeResolver.IsRootRelevantToScope("Root::A", ["Root::A"]));
    }

    /// <summary>
    ///     A candidate root nested within an exposed subject's containment subtree is relevant to
    ///     the scope.
    /// </summary>
    [Fact]
    public void IsRootRelevantToScope_CandidateNestedInSubject_ReturnsTrue()
    {
        Assert.True(ExposeScopeResolver.IsRootRelevantToScope("Root::A::Child", ["Root::A"]));
    }

    /// <summary>
    ///     A candidate root that contains an exposed subject within its own containment subtree
    ///     (the common "expose an inner element of the heuristic root" case) is relevant to the
    ///     scope.
    /// </summary>
    [Fact]
    public void IsRootRelevantToScope_SubjectNestedInCandidate_ReturnsTrue()
    {
        Assert.True(ExposeScopeResolver.IsRootRelevantToScope("Root::A", ["Root::A::Child"]));
    }

    /// <summary>A candidate root unrelated to any exposed subject is not relevant to the scope.</summary>
    [Fact]
    public void IsRootRelevantToScope_UnrelatedCandidate_ReturnsFalse()
    {
        Assert.False(ExposeScopeResolver.IsRootRelevantToScope("Root::B", ["Root::A"]));
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
            ResolvedEdges =
            [
                new SysmlEdge("Root::V", "Root::A", SysmlEdgeKind.Expose),
                new SysmlEdge("Root::V", "Root::myVehicle", SysmlEdgeKind.Expose)
            ]
        };

        var scope = ExposeScopeResolver.ResolveExposedScope(workspace, viewNode);

        Assert.NotNull(scope);
        Assert.Contains("Root::A", scope);
        Assert.Contains("Root::myVehicle", scope);
        Assert.Contains("Root::Vehicle", scope);
    }
}
