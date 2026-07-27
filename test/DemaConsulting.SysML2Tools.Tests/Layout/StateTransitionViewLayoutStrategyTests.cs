// <copyright file="StateTransitionViewLayoutStrategyTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.SysML2Tools.Layout.Internal;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;

namespace DemaConsulting.SysML2Tools.Tests.Layout;

/// <summary>
///     Tests for <see cref="StateTransitionViewLayoutStrategy"/>.
/// </summary>
public sealed class StateTransitionViewLayoutStrategyTests
{
    /// <summary>
    ///     A state definition with states and transitions produces a state box per state, an initial
    ///     pseudo-state badge, and a transition line carrying its guard label.
    /// </summary>
    [Fact]
    public void StateTransitionView_BuildLayout_StatesAndTransitions_ProducesBoxesBadgeAndLines()
    {
        // Arrange: a Light state def with two states and a guarded transition
        var strategy = new StateTransitionViewLayoutStrategy();
        var light = new SysmlDefinitionNode
        {
            Name = "Light",
            QualifiedName = "SM::Light",
            DefinitionKeyword = "state def",
            Children =
            [
                new SysmlFeatureNode { Name = "stop", QualifiedName = "SM::Light::stop", FeatureKeyword = "state" },
                new SysmlFeatureNode { Name = "go", QualifiedName = "SM::Light::go", FeatureKeyword = "state" },
                new SysmlTransitionNode { Source = "stop", Target = "go", Guard = "t" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["SM::Light"] = light }
        };
        var context = new ViewContext("StateTransition", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: two state boxes, an initial badge, and a guard-labelled transition line
        Assert.Equal(2, layout.Nodes.OfType<LayoutBox>().Count(b => b.Keyword == "state"));
        Assert.Contains(layout.Nodes.OfType<LayoutBadge>(), b => b.Shape == BadgeShape.FilledCircle);
        Assert.Contains(layout.Nodes.OfType<LayoutLine>(), l => l.MidpointLabel == "[t]");
    }

    /// <summary>States referenced only by transitions are still created as boxes.</summary>
    [Fact]
    public void StateTransitionView_BuildLayout_UndeclaredStateInTransition_IsCreated()
    {
        // Arrange: only one declared state; the transition references an undeclared target
        var strategy = new StateTransitionViewLayoutStrategy();
        var machine = new SysmlDefinitionNode
        {
            Name = "M",
            QualifiedName = "P::M",
            DefinitionKeyword = "state def",
            Children =
            [
                new SysmlFeatureNode { Name = "a", QualifiedName = "P::M::a", FeatureKeyword = "state" },
                new SysmlTransitionNode { Source = "a", Target = "b", Guard = null }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["P::M"] = machine }
        };
        var context = new ViewContext("StateTransition", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: two state boxes exist (a declared, b synthesized from the transition)
        Assert.Equal(2, layout.Nodes.OfType<LayoutBox>().Count(b => b.Keyword == "state"));
    }

    /// <summary>An empty workspace yields a minimal canvas.</summary>
    [Fact]
    public void StateTransitionView_BuildLayout_EmptyWorkspace_ReturnsMinimalCanvas()
    {
        var strategy = new StateTransitionViewLayoutStrategy();
        var workspace = new SysmlWorkspace();
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        Assert.Empty(layout.Nodes);
    }

    /// <summary>
    ///     A state with both an outgoing and an incoming transition on the same edge anchors them at
    ///     distinct points so the two arrows do not coincide (which would hide their direction).
    /// </summary>
    [Fact]
    public void StateTransitionView_BuildLayout_InAndOutOnSameEdge_UseDistinctAnchors()
    {
        // Arrange: two states with transitions in both directions (a->b and b->a).
        var strategy = new StateTransitionViewLayoutStrategy();
        var machine = new SysmlDefinitionNode
        {
            Name = "M",
            QualifiedName = "P::M",
            DefinitionKeyword = "state def",
            Children =
            [
                new SysmlFeatureNode { Name = "a", QualifiedName = "P::M::a", FeatureKeyword = "state" },
                new SysmlFeatureNode { Name = "b", QualifiedName = "P::M::b", FeatureKeyword = "state" },
                new SysmlTransitionNode { Source = "a", Target = "b", Guard = "fwd" },
                new SysmlTransitionNode { Source = "b", Target = "a", Guard = "rev" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["P::M"] = machine }
        };
        var context = new ViewContext("StateTransition", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // The forward line leaves state 'a' at its first waypoint; the reverse line enters state 'a'
        // at its last waypoint. Both are on a's edge facing b, so they must be different points.
        var lines = layout.Nodes.OfType<LayoutLine>().ToList();
        var forward = lines.Single(l => l.MidpointLabel == "[fwd]");
        var reverse = lines.Single(l => l.MidpointLabel == "[rev]");
        var outAnchor = forward.Waypoints[0];
        var inAnchor = reverse.Waypoints[^1];

        Assert.False(
            Math.Abs(outAnchor.X - inAnchor.X) < 1e-6 && Math.Abs(outAnchor.Y - inAnchor.Y) < 1e-6,
            "Outgoing and incoming transitions on the same edge must not share an anchor point.");
    }

    /// <summary>
    ///     A state transition edge carries an open arrowhead at the target end.
    /// </summary>
    [Fact]
    public void StateTransitionView_BuildLayout_TransitionEdge_HasOpenArrowhead()
    {
        // Arrange: a simple two-state machine with one transition
        var strategy = new StateTransitionViewLayoutStrategy();
        var machine = new SysmlDefinitionNode
        {
            Name = "M",
            QualifiedName = "P::M",
            DefinitionKeyword = "state def",
            Children =
            [
                new SysmlFeatureNode { Name = "a", QualifiedName = "P::M::a", FeatureKeyword = "state" },
                new SysmlFeatureNode { Name = "b", QualifiedName = "P::M::b", FeatureKeyword = "state" },
                new SysmlTransitionNode { Source = "a", Target = "b", Guard = "g" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["P::M"] = machine }
        };
        var context = new ViewContext("StateTransition", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: the transition line has an open arrowhead at the target end
        var transitionLine = layout.Nodes.OfType<LayoutLine>()
            .FirstOrDefault(l => l.MidpointLabel == "[g]");
        Assert.NotNull(transitionLine);
        Assert.Equal(EndMarkerStyle.OpenChevron, transitionLine.TargetEnd);
    }

    /// <summary>
    ///     A forward chain of transitions flows top-to-bottom: each transition's target box is placed
    ///     below its source box, and every transition polyline is orthogonal (axis-aligned segments).
    /// </summary>
    [Fact]
    public void StateTransitionView_BuildLayout_ForwardChain_FlowsTopToBottomOrthogonally()
    {
        // Arrange: a three-state forward chain a -> b -> c.
        var strategy = new StateTransitionViewLayoutStrategy();
        var machine = new SysmlDefinitionNode
        {
            Name = "M",
            QualifiedName = "P::M",
            DefinitionKeyword = "state def",
            Children =
            [
                new SysmlFeatureNode { Name = "a", QualifiedName = "P::M::a", FeatureKeyword = "state" },
                new SysmlFeatureNode { Name = "b", QualifiedName = "P::M::b", FeatureKeyword = "state" },
                new SysmlFeatureNode { Name = "c", QualifiedName = "P::M::c", FeatureKeyword = "state" },
                new SysmlTransitionNode { Source = "a", Target = "b", Guard = "ab" },
                new SysmlTransitionNode { Source = "b", Target = "c", Guard = "bc" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["P::M"] = machine }
        };
        var context = new ViewContext("StateTransition", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: boxes flow top-to-bottom (a above b above c).
        var boxes = layout.Nodes.OfType<LayoutBox>().Where(b => b.Keyword == "state").ToList();
        var a = boxes.Single(b => b.Label == "a");
        var b = boxes.Single(b => b.Label == "b");
        var c = boxes.Single(b => b.Label == "c");
        Assert.True(a.Y < b.Y, "state 'a' should be placed above state 'b'.");
        Assert.True(b.Y < c.Y, "state 'b' should be placed above state 'c'.");

        // Assert: every transition polyline is orthogonal.
        foreach (var line in layout.Nodes.OfType<LayoutLine>().Where(l => l.MidpointLabel is "[ab]" or "[bc]"))
        {
            for (var i = 0; i + 1 < line.Waypoints.Count; i++)
            {
                var dx = Math.Abs(line.Waypoints[i + 1].X - line.Waypoints[i].X);
                var dy = Math.Abs(line.Waypoints[i + 1].Y - line.Waypoints[i].Y);
                Assert.True(dx < 1e-6 || dy < 1e-6, "transition segments must be axis-aligned.");
            }
        }
    }

    /// <summary>
    ///     Builds a workspace with two candidate roots: <c>SM::MachineA</c> (two transitions — the
    ///     heuristic's default pick, one of its declared states <c>s3</c> is isolated with no
    ///     transitions), <c>SM::MachineB</c> (one transition), and an unrelated sibling definition
    ///     <c>SM::Unrelated</c> with no transitions.
    /// </summary>
    private static SysmlWorkspace BuildTwoRootWorkspace()
    {
        var machineA = new SysmlDefinitionNode
        {
            Name = "MachineA",
            QualifiedName = "SM::MachineA",
            DefinitionKeyword = "state def",
            Children =
            [
                new SysmlFeatureNode { Name = "s1", QualifiedName = "SM::MachineA::s1", FeatureKeyword = "state" },
                new SysmlFeatureNode { Name = "s2", QualifiedName = "SM::MachineA::s2", FeatureKeyword = "state" },
                new SysmlFeatureNode { Name = "s3", QualifiedName = "SM::MachineA::s3", FeatureKeyword = "state" },
                new SysmlTransitionNode { Source = "s1", Target = "s2", Guard = "g1" },
                new SysmlTransitionNode { Source = "s2", Target = "s1", Guard = "g2" }
            ]
        };
        var machineB = new SysmlDefinitionNode
        {
            Name = "MachineB",
            QualifiedName = "SM::MachineB",
            DefinitionKeyword = "state def",
            Children =
            [
                new SysmlFeatureNode { Name = "b1", QualifiedName = "SM::MachineB::b1", FeatureKeyword = "state" },
                new SysmlFeatureNode { Name = "b2", QualifiedName = "SM::MachineB::b2", FeatureKeyword = "state" },
                new SysmlTransitionNode { Source = "b1", Target = "b2", Guard = "g" }
            ]
        };
        var unrelated = new SysmlDefinitionNode { Name = "Unrelated", QualifiedName = "SM::Unrelated", DefinitionKeyword = "state def" };
        return new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["SM::MachineA"] = machineA,
                ["SM::MachineB"] = machineB,
                ["SM::Unrelated"] = unrelated
            }
        };
    }

    /// <summary>
    ///     With no <c>expose</c> statement (null <c>ViewNode</c>), the heuristic picks the
    ///     definition with the most transitions (<c>MachineA</c>), unchanged from pre-scoping
    ///     behavior — the critical --auto/no-expose regression guard.
    /// </summary>
    [Fact]
    public void StateTransitionView_BuildLayout_NullViewNode_PicksHeuristicRootUnchanged()
    {
        var strategy = new StateTransitionViewLayoutStrategy();
        var workspace = BuildTwoRootWorkspace();
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var boxes = layout.Nodes.OfType<LayoutBox>().Where(b => b.Keyword == "state").ToList();
        Assert.Contains(boxes, b => b.Label == "s1");
        Assert.Contains(boxes, b => b.Label == "s2");
    }

    /// <summary>
    ///     An <c>expose</c> edge pointing at a definition other than the heuristic's default root
    ///     (<c>MachineB</c>, which has fewer transitions than <c>MachineA</c>) selects
    ///     <c>MachineB</c> as the root instead.
    /// </summary>
    [Fact]
    public void StateTransitionView_BuildLayout_ExposeNonHeuristicRoot_SelectsExposedRoot()
    {
        var strategy = new StateTransitionViewLayoutStrategy();
        var workspace = BuildTwoRootWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "SM::V",
            ExposeMembers = [new ExposeMember("MachineB", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("SM::V", "SM::MachineB", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var boxes = layout.Nodes.OfType<LayoutBox>().Where(b => b.Keyword == "state").ToList();
        Assert.Contains(boxes, b => b.Label == "b1");
        Assert.Contains(boxes, b => b.Label == "b2");
        Assert.DoesNotContain(boxes, b => b.Label is "s1" or "s2" or "s3");
    }

    /// <summary>
    ///     An <c>expose</c> edge pointing at an inner child of a non-heuristic root
    ///     (<c>SM::MachineB::b1</c>) still selects <c>MachineB</c> as the root, since the exposed
    ///     subject lies within the candidate root's own containment subtree.
    /// </summary>
    [Fact]
    public void StateTransitionView_BuildLayout_ExposeInnerChildOfNonHeuristicRoot_SelectsItsRoot()
    {
        var strategy = new StateTransitionViewLayoutStrategy();
        var workspace = BuildTwoRootWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "SM::V",
            ExposeMembers = [new ExposeMember("b1", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("SM::V", "SM::MachineB::b1", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var boxes = layout.Nodes.OfType<LayoutBox>().Where(b => b.Keyword == "state").ToList();
        Assert.Contains(boxes, b => b.Label == "b1");
    }

    /// <summary>
    ///     Builds a workspace where <c>SM::MachineA</c> (more transitions) genuinely nests
    ///     <c>SM::MachineA::MachineB</c> (fewer transitions) as one of its own <c>Children</c>, while
    ///     both are also independently registered in <c>Declarations</c> under their own qualified
    ///     names — the shape needed to make both candidates scope-relevant for a subject exposed only
    ///     inside <c>MachineB</c>.
    /// </summary>
    private static SysmlWorkspace BuildNestedCandidateWorkspace()
    {
        var machineB = new SysmlDefinitionNode
        {
            Name = "MachineB",
            QualifiedName = "SM::MachineA::MachineB",
            DefinitionKeyword = "state def",
            Children =
            [
                new SysmlFeatureNode { Name = "b1", QualifiedName = "SM::MachineA::MachineB::b1", FeatureKeyword = "state" },
                new SysmlFeatureNode { Name = "b2", QualifiedName = "SM::MachineA::MachineB::b2", FeatureKeyword = "state" },
                new SysmlTransitionNode { Source = "b1", Target = "b2", Guard = "g" }
            ]
        };
        var machineA = new SysmlDefinitionNode
        {
            Name = "MachineA",
            QualifiedName = "SM::MachineA",
            DefinitionKeyword = "state def",
            Children =
            [
                new SysmlFeatureNode { Name = "s1", QualifiedName = "SM::MachineA::s1", FeatureKeyword = "state" },
                new SysmlFeatureNode { Name = "s2", QualifiedName = "SM::MachineA::s2", FeatureKeyword = "state" },
                new SysmlFeatureNode { Name = "s3", QualifiedName = "SM::MachineA::s3", FeatureKeyword = "state" },
                new SysmlTransitionNode { Source = "s1", Target = "s2", Guard = "g1" },
                new SysmlTransitionNode { Source = "s2", Target = "s1", Guard = "g2" },
                machineB
            ]
        };
        return new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["SM::MachineA"] = machineA,
                ["SM::MachineA::MachineB"] = machineB
            }
        };
    }

    /// <summary>
    ///     Exposing an inner state of the nested definition <c>MachineB</c> selects <c>MachineB</c>
    ///     as the root even though its ancestor <c>MachineA</c> has more transitions and would win the
    ///     old pure-score tie-break, proving <c>FindRoot</c> now prefers the most specific
    ///     (deepest-qualified-name) scope-relevant candidate over a less specific ancestor.
    /// </summary>
    [Fact]
    public void StateTransitionView_BuildLayout_ExposeInnerStateOfNestedDefinition_SelectsNestedDefinitionNotAncestor()
    {
        var strategy = new StateTransitionViewLayoutStrategy();
        var workspace = BuildNestedCandidateWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "SM::V",
            ExposeMembers = [new ExposeMember("b1", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("SM::V", "SM::MachineA::MachineB::b1", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var boxes = layout.Nodes.OfType<LayoutBox>().Where(b => b.Keyword == "state").ToList();
        Assert.Contains(boxes, b => b.Label == "b1");
        Assert.DoesNotContain(boxes, b => b.Label is "s1" or "s2" or "s3");
    }

    /// <summary>
    ///     An <c>expose</c> edge pointing at a definition unrelated to every candidate root makes no
    ///     root scope-relevant, so no root is chosen and an empty canvas results.
    /// </summary>
    [Fact]
    public void StateTransitionView_BuildLayout_ExposeUnrelatedDefinition_NoRootSelected_ReturnsMinimalCanvas()
    {
        var strategy = new StateTransitionViewLayoutStrategy();
        var workspace = BuildTwoRootWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "SM::V",
            ExposeMembers = [new ExposeMember("Unrelated", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("SM::V", "SM::Unrelated", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        Assert.Empty(layout.Nodes);
    }

    /// <summary>
    ///     Exposing state <c>s1</c> of <c>MachineA</c> narrows the declared states to those in
    ///     scope: the isolated declared state <c>s3</c> (never referenced by a transition) is
    ///     dropped, while <c>s2</c> remains because it is re-synthesized from the <c>s1</c>-&gt;
    ///     <c>s2</c> transition endpoint (the existing synthesized-state mechanism, unaffected by
    ///     scope, per the containment design). This produces strictly fewer state boxes than the
    ///     unscoped rendering.
    /// </summary>
    [Fact]
    public void StateTransitionView_BuildLayout_ExposeSingleState_DropsIsolatedOutOfScopeState()
    {
        var strategy = new StateTransitionViewLayoutStrategy();
        var workspace = BuildTwoRootWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "SM::V",
            ExposeMembers = [new ExposeMember("s1", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("SM::V", "SM::MachineA::s1", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var scoped = strategy.BuildLayout(context, options);
        var full = strategy.BuildLayout(new ViewContext("full", workspace), options);

        var scopedLabels = scoped.Nodes.OfType<LayoutBox>().Where(b => b.Keyword == "state").Select(b => b.Label).ToList();
        var fullLabels = full.Nodes.OfType<LayoutBox>().Where(b => b.Keyword == "state").Select(b => b.Label).ToList();

        Assert.Contains("s1", scopedLabels);
        Assert.Contains("s2", scopedLabels);
        Assert.DoesNotContain("s3", scopedLabels);
        Assert.True(scopedLabels.Count < fullLabels.Count, $"expected scoped ({scopedLabels.Count}) < full ({fullLabels.Count})");
    }

    /// <summary>
    ///     An <c>expose</c> edge that resolves to a feature usage (not a definition) still selects
    ///     the definition it types via the shared usage-to-type fallback in
    ///     <c>ExposeScopeResolver.ResolveExposedScope</c>: exposing a usage <c>myMachine</c> typed
    ///     by <c>MachineB</c> selects <c>MachineB</c> as the root.
    /// </summary>
    [Fact]
    public void StateTransitionView_BuildLayout_ExposedUsage_ResolvesThroughTypingToRoot()
    {
        var strategy = new StateTransitionViewLayoutStrategy();
        var workspace = BuildTwoRootWorkspace();
        workspace.AddDeclaration("SM::myMachine", new SysmlFeatureNode
        {
            Name = "myMachine",
            QualifiedName = "SM::myMachine",
            FeatureTyping = "MachineB",
            ResolvedEdges = [new SysmlEdge("SM::myMachine", "SM::MachineB", SysmlEdgeKind.Typing)]
        });
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "SM::V",
            ExposeMembers = [new ExposeMember("myMachine", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("SM::V", "SM::myMachine", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var boxes = layout.Nodes.OfType<LayoutBox>().Where(b => b.Keyword == "state").ToList();
        Assert.Contains(boxes, b => b.Label == "b1");
        Assert.Contains(boxes, b => b.Label == "b2");
    }

    /// <summary>
    ///     A pseudostate-sourced initial transition (<c>first start then X;</c>) makes the initial
    ///     marker's arrow land on the semantically-resolved target, not the first-declared state —
    ///     confirmed here with declaration order deliberately chosen so the two disagree.
    /// </summary>
    [Fact]
    public void StateTransitionView_BuildLayout_PseudostateSourceTransition_MarksResolvedTargetNotFirstDeclared()
    {
        // Arrange: "b" is declared first, but "first start then b" is not present — instead
        // "start" (an inherited pseudostate feature, never declared as its own state box) targets
        // "b" while "a" is declared first. This confirms the marker follows the resolved target
        // ("b") rather than the first-declared state ("a").
        var strategy = new StateTransitionViewLayoutStrategy();
        var machine = new SysmlDefinitionNode
        {
            Name = "M",
            QualifiedName = "P::M",
            DefinitionKeyword = "state def",
            Children =
            [
                new SysmlTransitionNode { Source = "start", Target = "b" },
                new SysmlFeatureNode { Name = "a", QualifiedName = "P::M::a", FeatureKeyword = "state" },
                new SysmlFeatureNode { Name = "b", QualifiedName = "P::M::b", FeatureKeyword = "state" },
                new SysmlTransitionNode { Source = "a", Target = "b", Guard = null }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["P::M"] = machine }
        };
        var context = new ViewContext("StateTransition", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: the initial marker's arrow terminates at "b"'s top edge (Y), not "a"'s — using Y
        // rather than X since a linear a->b chain may share a column (same X) under the layered
        // placement algorithm, but "a" (flow source) and "b" (flow target) never share the same Y.
        var badge = Assert.Single(layout.Nodes.OfType<LayoutBadge>());
        var bBox = layout.Nodes.OfType<LayoutBox>().Single(b => b.Keyword == "state" && b.Label == "b");
        var aBox = layout.Nodes.OfType<LayoutBox>().Single(b => b.Keyword == "state" && b.Label == "a");
        var markerArrow = layout.Nodes.OfType<LayoutLine>()
            .Single(l => l.TargetEnd == EndMarkerStyle.FilledArrow && l.Waypoints.Count == 2 &&
                         Math.Abs(l.Waypoints[0].X - badge.CentreX) < 0.01);
        var arrowEndY = markerArrow.Waypoints[^1].Y;
        Assert.Equal(bBox.Y, arrowEndY, precision: 3);
        Assert.NotEqual(aBox.Y, arrowEndY, precision: 3);
    }

    /// <summary>
    ///     No state box is ever labelled <c>"start"</c> — a pseudostate-sourced transition's source
    ///     must be excluded from ordinary state-box rendering (it would otherwise be synthesized as
    ///     an "additional state referenced only by a transition endpoint").
    /// </summary>
    [Fact]
    public void StateTransitionView_BuildLayout_PseudostateSourceTransition_NoSpuriousBox()
    {
        // Arrange
        var strategy = new StateTransitionViewLayoutStrategy();
        var machine = new SysmlDefinitionNode
        {
            Name = "M",
            QualifiedName = "P::M",
            DefinitionKeyword = "state def",
            Children =
            [
                new SysmlTransitionNode { Source = "start", Target = "off" },
                new SysmlFeatureNode { Name = "off", QualifiedName = "P::M::off", FeatureKeyword = "state" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["P::M"] = machine }
        };
        var context = new ViewContext("StateTransition", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: exactly one state box ("off"), never a "start" box.
        var stateBoxes = layout.Nodes.OfType<LayoutBox>().Where(b => b.Keyword == "state").ToList();
        Assert.Single(stateBoxes);
        Assert.DoesNotContain(stateBoxes, b => b.Label == "start");
    }

    /// <summary>
    ///     The <c>entryActionMember (entryTransitionMember)*</c> shape's implicit source (a named
    ///     entry-action feature, e.g. <c>entry action initial; then off;</c>) is likewise excluded
    ///     from ordinary state-box rendering, and the initial marker still resolves to the correct
    ///     target via the entry action's name.
    /// </summary>
    [Fact]
    public void StateTransitionView_BuildLayout_EntryActionSourceTransition_NoSpuriousBoxUsesResolvedTarget()
    {
        // Arrange
        var strategy = new StateTransitionViewLayoutStrategy();
        var machine = new SysmlDefinitionNode
        {
            Name = "M",
            QualifiedName = "P::M",
            DefinitionKeyword = "state def",
            Children =
            [
                new SysmlFeatureNode { Name = "initial", QualifiedName = "P::M::initial", FeatureKeyword = "entry" },
                new SysmlFeatureNode { Name = "a", QualifiedName = "P::M::a", FeatureKeyword = "state" },
                new SysmlFeatureNode { Name = "off", QualifiedName = "P::M::off", FeatureKeyword = "state" },
                new SysmlTransitionNode { Source = "initial", Target = "off" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["P::M"] = machine }
        };
        var context = new ViewContext("StateTransition", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: two state boxes ("a", "off"), no "initial" box, and the initial marker arrow
        // lands on "off" (the entry action's declared target) rather than "a" (first declared).
        var stateBoxes = layout.Nodes.OfType<LayoutBox>().Where(b => b.Keyword == "state").ToList();
        Assert.Equal(2, stateBoxes.Count);
        Assert.DoesNotContain(stateBoxes, b => b.Label == "initial");

        var badge = Assert.Single(layout.Nodes.OfType<LayoutBadge>());
        var offBox = stateBoxes.Single(b => b.Label == "off");
        var markerArrow = layout.Nodes.OfType<LayoutLine>()
            .Single(l => l.TargetEnd == EndMarkerStyle.FilledArrow && l.Waypoints.Count == 2 &&
                         Math.Abs(l.Waypoints[0].X - badge.CentreX) < 0.01);
        var arrowEndX = markerArrow.Waypoints[^1].X;
        Assert.InRange(arrowEndX, offBox.X, offBox.X + offBox.Width);
    }

    /// <summary>
    ///     Regression guard: when no pseudostate-sourced transition exists, the initial marker's
    ///     arrow still lands on the first-declared state — the pre-existing heuristic — exactly as
    ///     before this fix (protects every pre-existing passing test in this file, and the gallery's
    ///     <c>03-elevator-state.sysml</c>, which uses only guarded <c>transition first idle if ...
    ///     then ...;</c> chains with no pseudostate source at all).
    /// </summary>
    [Fact]
    public void StateTransitionView_BuildLayout_NoExplicitInitialTransition_FallsBackToFirstDeclared()
    {
        // Arrange: ordinary declared-state-to-state transitions only, no "start"/entry source.
        var strategy = new StateTransitionViewLayoutStrategy();
        var machine = new SysmlDefinitionNode
        {
            Name = "M",
            QualifiedName = "P::M",
            DefinitionKeyword = "state def",
            Children =
            [
                new SysmlFeatureNode { Name = "a", QualifiedName = "P::M::a", FeatureKeyword = "state" },
                new SysmlFeatureNode { Name = "b", QualifiedName = "P::M::b", FeatureKeyword = "state" },
                new SysmlTransitionNode { Source = "a", Target = "b", Guard = "t" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["P::M"] = machine }
        };
        var context = new ViewContext("StateTransition", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: the initial marker's arrow lands on "a" (the first-declared state).
        var badge = Assert.Single(layout.Nodes.OfType<LayoutBadge>());
        var aBox = layout.Nodes.OfType<LayoutBox>().Single(b => b.Keyword == "state" && b.Label == "a");
        var markerArrow = layout.Nodes.OfType<LayoutLine>()
            .Single(l => l.TargetEnd == EndMarkerStyle.FilledArrow && l.Waypoints.Count == 2 &&
                         Math.Abs(l.Waypoints[0].X - badge.CentreX) < 0.01);
        var arrowEndX = markerArrow.Waypoints[^1].X;
        Assert.InRange(arrowEndX, aBox.X, aBox.X + aBox.Width);
    }
}
