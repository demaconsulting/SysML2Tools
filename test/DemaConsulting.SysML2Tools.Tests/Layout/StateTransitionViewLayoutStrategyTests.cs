// <copyright file="StateTransitionViewLayoutStrategyTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.SysML2Tools.Layout.Internal;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;
using DemaConsulting.SysML2Tools.Stdlib;

namespace DemaConsulting.SysML2Tools.Tests.Layout;

/// <summary>
///     Tests for <see cref="StateTransitionViewLayoutStrategy"/>.
/// </summary>
public sealed class StateTransitionViewLayoutStrategyTests
{
    /// <summary>
    ///     Returns the content (states, initial marker, transition lines) the layout encloses in its
    ///     single state-definition frame.
    /// </summary>
    private static IReadOnlyList<LayoutNode> Content(LayoutTree layout) =>
        Assert.IsType<LayoutBox>(Assert.Single(layout.Nodes)).Children;

    /// <summary>
    ///     A state definition with states and transitions produces a state box per state, an initial
    ///     pseudo-state badge, and a transition line carrying its guard label.
    /// </summary>
    [Fact]
    public void StateTransitionView_BuildLayout_StatesAndTransitions_ProducesBoxesBadgeAndLines()
    {
        // Arrange: a Light state def with two states and a guarded transition, plus an explicit
        // initial transition so an initial marker is drawn
        var strategy = new StateTransitionViewLayoutStrategy();
        var light = new SysmlDefinitionNode
        {
            Name = "Light",
            QualifiedName = "SM::Light",
            DefinitionKeyword = "state def",
            Children =
            [
                new SysmlTransitionNode { Source = "start", Target = "stop" },
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

        // Assert: two state boxes, an initial badge, and a guard-labeled transition line
        Assert.Equal(2, Content(layout).OfType<LayoutBox>().Count(b => b.Keyword == "state"));
        Assert.Contains(Content(layout).OfType<LayoutBadge>(), b => b.Shape == BadgeShape.FilledCircle);
        Assert.Contains(Content(layout).OfType<LayoutLine>(), l => l.MidpointLabel == "[t]");
    }

    /// <summary>
    ///     The whole diagram is enclosed in one frame box headed with the state definition's keyword
    ///     and name; the frame is inset symmetrically from the canvas edge so its outline is not
    ///     clipped, and the states, initial marker, and transitions are its children and sit below
    ///     the frame's title band.
    /// </summary>
    [Fact]
    public void StateTransitionView_BuildLayout_RootDefinition_IsEnclosedInStateDefFrame()
    {
        // Arrange: a state def with an initial transition and one guarded transition
        var machine = new SysmlDefinitionNode
        {
            Name = "Machine",
            QualifiedName = "SM::Machine",
            DefinitionKeyword = "state def",
            Children =
            [
                new SysmlTransitionNode { Source = "start", Target = "a" },
                new SysmlFeatureNode { Name = "a", QualifiedName = "SM::Machine::a", FeatureKeyword = "state" },
                new SysmlFeatureNode { Name = "b", QualifiedName = "SM::Machine::b", FeatureKeyword = "state" },
                new SysmlTransitionNode { Source = "a", Target = "b", Guard = "g" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["SM::Machine"] = machine }
        };
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = new StateTransitionViewLayoutStrategy().BuildLayout(new ViewContext("v", workspace), options);

        // Assert: one top-level frame labeled «state def» Machine, inset symmetrically inside the canvas
        // so its outline stroke is not clipped by the canvas edge
        var frame = Assert.IsType<LayoutBox>(Assert.Single(layout.Nodes));
        Assert.Equal("state def", frame.Keyword);
        Assert.Equal("Machine", frame.Label);
        Assert.True(frame.X > 0, "the frame must be inset from the left canvas edge.");
        Assert.True(frame.Y > 0, "the frame must be inset from the top canvas edge.");
        Assert.Equal(frame.X, frame.Y, precision: 3);
        Assert.Equal(layout.Width, frame.X + frame.Width + frame.X, precision: 3);
        Assert.Equal(layout.Height, frame.Y + frame.Height + frame.Y, precision: 3);

        // Assert: all content is inside the frame, with the states clear of the title band
        var titleArea = BoxMetrics.TitleAreaHeight(options.Theme, hasLabel: true, hasKeyword: true);
        var states = frame.Children.OfType<LayoutBox>().Where(b => b.Keyword == "state").ToList();
        Assert.Equal(2, states.Count);
        Assert.All(states, s =>
        {
            Assert.True(s.Y >= titleArea, "states must start below the frame's title band.");
            Assert.True(s.X + s.Width <= frame.Width && s.Y + s.Height <= frame.Height, "states must lie inside the frame.");
        });
        Assert.Single(frame.Children.OfType<LayoutBadge>());
        Assert.Contains(frame.Children.OfType<LayoutLine>(), l => l.MidpointLabel == "[g]");
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
        Assert.Equal(2, Content(layout).OfType<LayoutBox>().Count(b => b.Keyword == "state"));
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
        var lines = Content(layout).OfType<LayoutLine>().ToList();
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
        var transitionLine = Content(layout).OfType<LayoutLine>()
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
        var boxes = Content(layout).OfType<LayoutBox>().Where(b => b.Keyword == "state").ToList();
        var a = boxes.Single(b => b.Label == "a");
        var b = boxes.Single(b => b.Label == "b");
        var c = boxes.Single(b => b.Label == "c");
        Assert.True(a.Y < b.Y, "state 'a' should be placed above state 'b'.");
        Assert.True(b.Y < c.Y, "state 'b' should be placed above state 'c'.");

        // Assert: every transition polyline is orthogonal.
        foreach (var line in Content(layout).OfType<LayoutLine>().Where(l => l.MidpointLabel is "[ab]" or "[bc]"))
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

        var boxes = Content(layout).OfType<LayoutBox>().Where(b => b.Keyword == "state").ToList();
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

        var boxes = Content(layout).OfType<LayoutBox>().Where(b => b.Keyword == "state").ToList();
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

        var boxes = Content(layout).OfType<LayoutBox>().Where(b => b.Keyword == "state").ToList();
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

        var boxes = Content(layout).OfType<LayoutBox>().Where(b => b.Keyword == "state").ToList();
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

        var scopedLabels = Content(scoped).OfType<LayoutBox>().Where(b => b.Keyword == "state").Select(b => b.Label).ToList();
        var fullLabels = Content(full).OfType<LayoutBox>().Where(b => b.Keyword == "state").Select(b => b.Label).ToList();

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

        var boxes = Content(layout).OfType<LayoutBox>().Where(b => b.Keyword == "state").ToList();
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
        var badge = Assert.Single(Content(layout).OfType<LayoutBadge>());
        var bBox = Content(layout).OfType<LayoutBox>().Single(b => b.Keyword == "state" && b.Label == "b");
        var aBox = Content(layout).OfType<LayoutBox>().Single(b => b.Keyword == "state" && b.Label == "a");
        var markerArrow = InitialArrow(layout, badge);
        var arrowEndY = markerArrow.Waypoints[^1].Y;
        Assert.Equal(bBox.Y, arrowEndY, precision: 3);
        Assert.NotEqual(aBox.Y, arrowEndY, precision: 3);
    }

    /// <summary>
    ///     No state box is ever labeled <c>"start"</c> — a pseudostate-sourced transition's source
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
        var stateBoxes = Content(layout).OfType<LayoutBox>().Where(b => b.Keyword == "state").ToList();
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
        var stateBoxes = Content(layout).OfType<LayoutBox>().Where(b => b.Keyword == "state").ToList();
        Assert.Equal(2, stateBoxes.Count);
        Assert.DoesNotContain(stateBoxes, b => b.Label == "initial");

        var badge = Assert.Single(Content(layout).OfType<LayoutBadge>());
        var offBox = stateBoxes.Single(b => b.Label == "off");
        var markerArrow = InitialArrow(layout, badge);
        var arrowEndX = markerArrow.Waypoints[^1].X;
        Assert.InRange(arrowEndX, offBox.X, offBox.X + offBox.Width);
    }

    /// <summary>
    ///     When no explicit initial transition (<c>first start then X;</c> or an entry-action-sourced
    ///     transition) exists, no initial marker is drawn: neither a badge nor an arrow, and no top
    ///     band is reserved for one — the first-declared state is never guessed.
    /// </summary>
    [Fact]
    public void StateTransitionView_BuildLayout_NoExplicitInitialTransition_DrawsNoInitialMarker()
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

        // Assert: no badge and no two-point marker arrow; the only line is the guarded transition
        var content = Content(layout);
        Assert.Empty(content.OfType<LayoutBadge>());
        Assert.Single(content.OfType<LayoutLine>());

        // Assert: the first state sits directly below the frame's top edge plus title band plus margin
        // (no marker band)
        var titleArea = BoxMetrics.TitleAreaHeight(options.Theme, hasLabel: true, hasKeyword: true);
        var frameTop = Assert.IsType<LayoutBox>(Assert.Single(layout.Nodes)).Y;
        var aBox = content.OfType<LayoutBox>().Single(b => b.Keyword == "state" && b.Label == "a");
        Assert.Equal(frameTop + titleArea + (options.Theme.LabelPadding * 4.0), aBox.Y, precision: 3);
    }

    /// <summary>
    ///     A transition sourced from the completion pseudostate <c>done</c> is an ordinary
    ///     (non-initial) transition: it never draws the initial marker at its target.
    /// </summary>
    [Fact]
    public void StateTransitionView_BuildLayout_DoneSourcedTransition_DrawsNoInitialMarker()
    {
        // Arrange: only "done"-sourced transition to a declared state
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
                new SysmlTransitionNode { Source = "done", Target = "b" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["P::M"] = machine }
        };

        // Act
        var layout = strategy.BuildLayout(new ViewContext("v", workspace), new RenderOptions(Themes.Light));

        // Assert: no initial badge
        Assert.Empty(Content(layout).OfType<LayoutBadge>());
    }

    /// <summary>
    ///     The initial marker's arrow is a solid line with an open (two-stroke) arrowhead, not a
    ///     filled one.
    /// </summary>
    [Fact]
    public void StateTransitionView_BuildLayout_InitialMarkerArrow_IsSolidWithOpenArrowhead()
    {
        // Arrange: a state def with an explicit initial transition
        var strategy = new StateTransitionViewLayoutStrategy();
        var machine = new SysmlDefinitionNode
        {
            Name = "M",
            QualifiedName = "P::M",
            DefinitionKeyword = "state def",
            Children =
            [
                new SysmlTransitionNode { Source = "start", Target = "a" },
                new SysmlFeatureNode { Name = "a", QualifiedName = "P::M::a", FeatureKeyword = "state" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["P::M"] = machine }
        };

        // Act
        var layout = strategy.BuildLayout(new ViewContext("v", workspace), new RenderOptions(Themes.Light));

        // Assert: the marker arrow is solid with an open chevron head
        var badge = Assert.Single(Content(layout).OfType<LayoutBadge>());
        var arrow = InitialArrow(layout, badge);
        Assert.Equal(EndMarkerStyle.OpenChevron, arrow.TargetEnd);
        Assert.Equal(LineStyle.Solid, arrow.LineStyle);
    }

    /// <summary>
    ///     Finds the initial marker's arrow: the two-point line that starts at the bottom of the
    ///     initial badge (real transitions never start on the badge).
    /// </summary>
    private static LayoutLine InitialArrow(LayoutTree layout, LayoutBadge badge) =>
        Content(layout).OfType<LayoutLine>().Single(l =>
            l.Waypoints.Count == 2 &&
            Math.Abs(l.Waypoints[0].X - badge.CentreX) < 0.01 &&
            Math.Abs(l.Waypoints[0].Y - (badge.CentreY + (badge.Size / 2.0))) < 0.01);

    /// <summary>Builds a state definition from the given children and lays it out with the Light theme.</summary>
    private static LayoutTree LayOut(params SysmlNode[] children)
    {
        var machine = new SysmlDefinitionNode
        {
            Name = "M",
            QualifiedName = "P::M",
            DefinitionKeyword = "state def",
            Children = children,
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["P::M"] = machine },
        };
        return new StateTransitionViewLayoutStrategy().BuildLayout(new ViewContext("v", workspace), new RenderOptions(Themes.Light));
    }

    /// <summary>A named state usage with the given sub-action children.</summary>
    private static SysmlFeatureNode State(string name, params SysmlNode[] children) =>
        new() { Name = name, QualifiedName = $"P::M::{name}", FeatureKeyword = "state", Children = children };

    /// <summary>An <c>entry</c>/<c>do</c>/<c>exit</c> sub-action feature with its declaration text.</summary>
    private static SysmlFeatureNode StateAction(string keyword, string? actionText, string? name = null) =>
        new() { Name = name, FeatureKeyword = keyword, ActionText = actionText };

    /// <summary>
    ///     A state's <c>entry</c>, <c>do</c>, and <c>exit</c> actions become compartments titled with
    ///     the keyword (centered, italic), in that order, whatever order they were declared in, each
    ///     row holding the action text without a leading <c>action</c> keyword.
    /// </summary>
    [Fact]
    public void StateTransitionView_BuildLayout_EntryDoExit_ProducesTitledCompartments()
    {
        // Arrange: actions declared out of order, one without the action keyword
        var layout = LayOut(
            State("open", StateAction("exit", "action chime", "chime"), StateAction("entry", "action openDoors", "openDoors"), StateAction("do", "holdOpen")));

        // Act
        var box = Content(layout).OfType<LayoutBox>().Single(b => b.Label == "open");

        // Assert
        Assert.Equal(["entry", "do", "exit"], box.Compartments.Select(c => c.Title).ToArray());
        Assert.Equal(["openDoors"], box.Compartments[0].Rows);
        Assert.Equal(["holdOpen"], box.Compartments[1].Rows);
        Assert.Equal(["chime"], box.Compartments[2].Rows);
        Assert.All(box.Compartments, c =>
        {
            Assert.Equal(TextAlign.Center, c.TitleAlign);
            Assert.Equal(TextStyle.Italic, c.TitleStyle);
        });
    }

    /// <summary>A state that declares no sub-actions has no compartments, and an empty form adds none.</summary>
    [Fact]
    public void StateTransitionView_BuildLayout_EntryDoExit_NoneDeclared_NoCompartments()
    {
        // Arrange
        var layout = LayOut(State("plain"), State("empty", StateAction("entry", null)));

        // Act / Assert
        Assert.All(Content(layout).OfType<LayoutBox>().Where(b => b.Keyword == "state"), b => Assert.Empty(b.Compartments));
    }

    /// <summary>The box is widened to the longest compartment row and is taller than a plain state.</summary>
    [Fact]
    public void StateTransitionView_BuildLayout_EntryDoExit_WidensBoxToLongestRow()
    {
        // Arrange
        var longRow = "action " + new string('x', 60);
        var layout = LayOut(State("plain"), State("busy", StateAction("do", longRow)));

        // Act
        var boxes = Content(layout).OfType<LayoutBox>().Where(b => b.Keyword == "state").ToList();
        var plain = boxes.Single(b => b.Label == "plain");
        var busy = boxes.Single(b => b.Label == "busy");

        // Assert
        var theme = Themes.Light;
        Assert.True(busy.Width >= 60 * theme.FontSizeBody * 0.62, "Box must be at least as wide as its longest row.");
        Assert.True(busy.Width > plain.Width);
        Assert.True(busy.Height > plain.Height);
    }

    /// <summary>A typed state usage shows <c>name : Type [multiplicity]</c> as its name line.</summary>
    [Fact]
    public void StateTransitionView_BuildLayout_TypedState_NameLineShowsTypeAndMultiplicity()
    {
        // Arrange
        var typed = new SysmlFeatureNode
        {
            Name = "door",
            QualifiedName = "P::M::door",
            FeatureKeyword = "state",
            FeatureTyping = "DoorState",
            Multiplicity = "[2]",
        };
        var layout = LayOut(typed);

        // Act / Assert
        Assert.Single(Content(layout).OfType<LayoutBox>(), b => b.Label == "door : DoorState [2]");
    }

    /// <summary>
    ///     A transition is labeled <c>trigger [guard] / effect</c>, each part omitted when the
    ///     transition does not declare it; a leading <c>action</c> keyword is dropped from the effect
    ///     and whitespace is normalized.
    /// </summary>
    [Theory]
    [InlineData("sig", "g", "act", "sig [g] / act")]
    [InlineData("sig", null, null, "sig")]
    [InlineData(null, "g", null, "[g]")]
    [InlineData(null, null, "act", "/ act")]
    [InlineData("sig", null, "act", "sig / act")]
    [InlineData(null, "g", "act", "[g] / act")]
    [InlineData("at   5", "g", "action  doIt", "at 5 [g] / doIt")]
    [InlineData(null, null, null, null)]
    public void StateTransitionView_BuildLayout_TransitionTriggerGuardEffect_LabelFormat(
        string? trigger, string? guard, string? effect, string? expected)
    {
        // Arrange
        var layout = LayOut(
            State("a"),
            State("b"),
            new SysmlTransitionNode { Source = "a", Target = "b", Trigger = trigger, Guard = guard, Effect = effect });

        // Act
        var line = Assert.Single(Content(layout).OfType<LayoutLine>());

        // Assert
        Assert.Equal(expected, line.MidpointLabel);
    }

    /// <summary>A self-transition carries the same full label as any other transition.</summary>
    [Fact]
    public void StateTransitionView_BuildLayout_SelfTransition_CarriesFullLabel()
    {
        // Arrange
        var layout = LayOut(
            State("a"),
            new SysmlTransitionNode { Source = "a", Target = "a", Trigger = "tick", Guard = "ok", Effect = "count" });

        // Act / Assert
        var line = Assert.Single(Content(layout).OfType<LayoutLine>());
        Assert.Equal("tick [ok] / count", line.MidpointLabel);
    }

    /// <summary>A transition into <c>done</c> ends in a bullseye badge, never a state box named done.</summary>
    [Fact]
    public void StateTransitionView_BuildLayout_DoneTarget_RendersBullseyeNotStateBox()
    {
        // Arrange
        var layout = LayOut(State("a"), new SysmlTransitionNode { Source = "a", Target = "done", Trigger = "stop" });

        // Act
        var content = Content(layout);

        // Assert: one bullseye, only the real state box, and a line that ends at the badge
        var badge = Assert.Single(content.OfType<LayoutBadge>(), b => b.Shape == BadgeShape.Bullseye);
        Assert.Equal("a", Assert.Single(content.OfType<LayoutBox>(), b => b.Keyword == "state").Label);
        var line = Assert.Single(content.OfType<LayoutLine>());
        Assert.Equal("stop", line.MidpointLabel);
        var end = line.Waypoints[^1];
        Assert.True(Math.Abs(end.X - badge.CentreX) <= badge.Size / 2.0 + 0.5);
        Assert.True(Math.Abs(end.Y - badge.CentreY) <= badge.Size / 2.0 + 0.5);
    }

    /// <summary>However many transitions reach <c>done</c>, there is a single final badge.</summary>
    [Fact]
    public void StateTransitionView_BuildLayout_DoneTarget_MultipleSources_SingleFinalBadge()
    {
        // Arrange
        var layout = LayOut(
            State("a"),
            State("b"),
            new SysmlTransitionNode { Source = "a", Target = "done" },
            new SysmlTransitionNode { Source = "b", Target = "done" },
            new SysmlTransitionNode { Source = "a", Target = "b" });

        // Act
        var content = Content(layout);

        // Assert
        Assert.Single(content.OfType<LayoutBadge>(), b => b.Shape == BadgeShape.Bullseye);
        Assert.Equal(3, content.OfType<LayoutLine>().Count());
    }

    /// <summary>
    ///     A back edge (one that returns to an earlier state) ends in an open chevron on a final
    ///     straight approach at least as long as the marker, so the arrowhead enters cleanly.
    /// </summary>
    [Fact]
    public void StateTransitionView_BuildLayout_BackEdge_OpenChevronWithFlushApproach()
    {
        // Arrange: a -> b -> c -> a
        var layout = LayOut(
            State("a"),
            State("b"),
            State("c"),
            new SysmlTransitionNode { Source = "a", Target = "b" },
            new SysmlTransitionNode { Source = "b", Target = "c" },
            new SysmlTransitionNode { Source = "c", Target = "a" });

        // Act
        var lines = Content(layout).OfType<LayoutLine>().ToList();

        // Assert
        var marker = NotationMetrics.RenderedAlongLineLength(EndMarkerStyle.OpenChevron, Themes.Light);
        Assert.Equal(3, lines.Count);
        foreach (var line in lines)
        {
            Assert.Equal(EndMarkerStyle.OpenChevron, line.TargetEnd);
            var (last, previous) = (line.Waypoints[^1], line.Waypoints[^2]);
            var approach = Math.Abs(last.X - previous.X) + Math.Abs(last.Y - previous.Y);
            Assert.True(approach >= marker - 0.01, $"Final approach {approach} is shorter than the marker {marker}.");
        }
    }

    /// <summary>
    ///     The frame grows to the right edge of a long transition label as the renderers would place
    ///     it (obstacle-aware), so the label is never clipped by the frame.
    /// </summary>
    [Fact]
    public void StateTransitionView_BuildLayout_FrameWidth_AccountsForLongTransitionLabel()
    {
        // Arrange: a self-transition near the right edge with a very long label
        var layout = LayOut(
            State("a"),
            new SysmlTransitionNode { Source = "a", Target = "a", Trigger = new string('t', 80) });

        // Act
        var frame = Assert.IsType<LayoutBox>(Assert.Single(layout.Nodes));
        var line = Assert.Single(frame.Children.OfType<LayoutLine>());
        var obstacles = frame.Children.OfType<LayoutBox>().ToList();
        var placement = ConnectorLabelPlacer.Place([line], Themes.Light.FontSizeBody, obstacles, NotationMetrics.MarkerScale(Themes.Light))[line];

        // Assert
        Assert.True(frame.X + frame.Width >= placement.X + placement.HalfWidth - 0.5, "Frame must contain the placed label.");
    }

    /// <summary>
    ///     Transition labels, drawn with their opaque background padding, are clear of every state
    ///     box, of every other connector, and of the first and last marker length of their own
    ///     connector (so a label never erases a box border, hides another arrow, or covers its own
    ///     arrowhead). Checked on the Elevator gallery model.
    /// </summary>
    [Fact]
    public async Task StateTransitionView_BuildLayout_ElevatorGalleryModel_LabelsDoNotOverlapStateBoxes()
    {
        // Arrange
        var layout = await LayOutGalleryModelAsync("03-elevator-state.sysml");

        // Act / Assert
        AssertLabelsClear(layout, SysmlThemes.Plain);
    }

    /// <summary>The same label-clearance guarantee holds on the Heater gallery model.</summary>
    [Fact]
    public async Task StateTransitionView_BuildLayout_HeaterGalleryModel_LabelsAreClear()
    {
        // Arrange
        var layout = await LayOutGalleryModelAsync("12-heater-state.sysml");

        // Act / Assert
        AssertLabelsClear(layout, SysmlThemes.Plain);
    }

    /// <summary>
    ///     Two transitions with the same wide label that arrive on one face of a narrow state keep
    ///     their labels clear of each other and of the state's border: the engine (0.3.0-beta.4)
    ///     aligns the arrival ports straight, so the labels no longer collide and the strategy widens
    ///     the state only when they still would. The test measures the label gap directly, so it
    ///     fails if the labels were crowded (whether by the engine or by the strategy).
    /// </summary>
    [Fact]
    public void StateTransitionView_BuildLayout_SiblingLabelsOnSharedFace_StayClear()
    {
        // Arrange: a and b both reach c with the same wide guard (c would be only 100 px wide)
        var layout = LayOut(
            State("a"),
            State("b"),
            State("c"),
            new SysmlTransitionNode { Source = "a", Target = "c", Guard = "atFloorNow" },
            new SysmlTransitionNode { Source = "b", Target = "c", Guard = "atFloorNow" });

        // Act
        var frame = Assert.IsType<LayoutBox>(Assert.Single(layout.Nodes));
        var lines = frame.Children.OfType<LayoutLine>().Where(l => l.MidpointLabel is not null).ToList();
        var obstacles = frame.Children.OfType<LayoutBox>().ToList();
        var placements = ConnectorLabelPlacer.Place(lines, Themes.Light.FontSizeBody, obstacles, NotationMetrics.MarkerScale(Themes.Light));
        var first = placements[lines[0]];
        var second = placements[lines[1]];
        var padding = Themes.Light.LabelPadding;
        var horizontalGap = Math.Abs(first.X - second.X) - first.HalfWidth - second.HalfWidth;
        var verticalGap = Math.Abs(first.Y - second.Y) - first.HalfHeight - second.HalfHeight;

        // Assert: the two labels are at least one label padding apart, and nothing else collides
        Assert.Equal(2, lines.Count);
        Assert.True(
            Math.Max(horizontalGap, verticalGap) >= padding,
            $"The sibling labels must be separated by their padding (horizontal gap {horizontalGap:F2} px, vertical gap {verticalGap:F2} px).");
        AssertLabelsClear(layout, Themes.Light);
    }

    /// <summary>A state whose labels do not collide keeps its natural width.</summary>
    [Fact]
    public void StateTransitionView_BuildLayout_NoLabelCollision_KeepsNaturalWidth()
    {
        // Arrange: short labels that fit between the anchors of a 100 px state
        var layout = LayOut(
            State("a"),
            State("b"),
            State("c"),
            new SysmlTransitionNode { Source = "a", Target = "c", Guard = "g" },
            new SysmlTransitionNode { Source = "b", Target = "c", Guard = "h" });

        // Act
        var c = Content(layout).OfType<LayoutBox>().Single(b => b.Label == "c");

        // Assert
        AssertLabelsClear(layout, Themes.Light);
        Assert.Equal(100.0, c.Width, precision: 3);
    }

    /// <summary>
    ///     The frame ends one margin below the lowest content, matching the side margin, so no empty
    ///     band is left under the last state or final badge.
    /// </summary>
    [Fact]
    public void StateTransitionView_BuildLayout_FrameBottom_IsOneMarginBelowContent()
    {
        // Arrange
        var layout = LayOut(
            State("a"),
            new SysmlTransitionNode { Source = "a", Target = "done", Trigger = "stop" });

        // Act
        var frame = Assert.IsType<LayoutBox>(Assert.Single(layout.Nodes));
        var contentBottom = frame.Children.Max(n => n switch
        {
            LayoutBox b => b.Y + b.Height,
            LayoutBadge g => g.CentreY + (g.Size / 2.0),
            LayoutLine l => l.Waypoints.Max(p => p.Y),
            _ => 0.0,
        });

        // Assert
        Assert.Equal(contentBottom + (Themes.Light.LabelPadding * 4.0), frame.Y + frame.Height, precision: 3);
    }

    /// <summary>
    ///     Asserts that no label of the laid-out state diagram, with its renderer background padding,
    ///     covers a state box, another connector, or the first or last marker length of its own connector.
    /// </summary>
    private static void AssertLabelsClear(LayoutTree layout, Theme theme)
    {
        var marker = NotationMetrics.RenderedAlongLineLength(EndMarkerStyle.OpenChevron, theme);
        var content = Content(layout);
        Assert.Contains(content.OfType<LayoutLine>(), l => l.MidpointLabel is not null);
        var violations = LabelClearance.FindViolations(
            content, theme, marker, marker, containers: layout.Nodes.OfType<LayoutBox>().ToList());
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    /// <summary>
    ///     The gallery's elevator model exercises the notation: entry/do/exit compartments on
    ///     <c>doorsOpening</c>, a <c>trigger [guard] / effect</c> label, plain guard labels, and one
    ///     final badge.
    /// </summary>
    [Fact]
    public async Task StateTransitionView_BuildLayout_ElevatorGalleryModel_ShowsCompartmentsLabelsAndFinal()
    {
        // Arrange
        var layout = await LayOutGalleryModelAsync("03-elevator-state.sysml");

        // Act
        var content = Content(layout);

        // Assert
        var opening = content.OfType<LayoutBox>().Single(b => b.Label == "doorsOpening");
        Assert.Equal(["entry", "do", "exit"], opening.Compartments.Select(c => c.Title).ToArray());
        Assert.Contains(content.OfType<LayoutLine>(), l => l.MidpointLabel == "callReceived [doorsClear] / dimLights");
        Assert.Contains(content.OfType<LayoutLine>(), l => l.MidpointLabel == "[atFloor]");
        Assert.Contains(content.OfType<LayoutLine>(), l => l.MidpointLabel == "shutdown");
        Assert.Single(content.OfType<LayoutBadge>(), b => b.Shape == BadgeShape.Bullseye);
        Assert.DoesNotContain(content.OfType<LayoutBox>(), b => b.Label == "done");
    }

    /// <summary>
    ///     A transition label that is wider than the states it sits between and overhangs the left edge
    ///     of the content moves the content right so the label stays inside the frame.
    /// </summary>
    [Fact]
    public void StateTransitionView_BuildLayout_LongLabel_OverhangingLeft_IsContainedByFrame()
    {
        // Arrange: a -> b with a label far wider than either state
        var layout = LayOut(
            State("a"),
            State("b"),
            new SysmlTransitionNode { Source = "a", Target = "b", Trigger = new string('t', 60) });

        // Act
        var frame = Assert.IsType<LayoutBox>(Assert.Single(layout.Nodes));
        var line = Assert.Single(frame.Children.OfType<LayoutLine>());
        var obstacles = frame.Children.OfType<LayoutBox>().ToList();
        var placement = ConnectorLabelPlacer.Place([line], Themes.Light.FontSizeBody, obstacles, NotationMetrics.MarkerScale(Themes.Light))[line];

        // Assert
        Assert.True(placement.X - placement.HalfWidth >= frame.X, "The label must not overhang the frame's left edge.");
        Assert.True(placement.X + placement.HalfWidth <= frame.X + frame.Width, "The label must not overhang the frame's right edge.");
    }

    /// <summary>
    ///     Parallel transitions between the same two states each keep their own route from the layout
    ///     engine (they are not collapsed onto one route and spread apart), and each label sits on
    ///     its own transition's route.
    /// </summary>
    [Fact]
    public void StateTransitionView_BuildLayout_ParallelTransitions_EachUseTheirOwnEngineRoute()
    {
        // Arrange: two transitions a -> b
        var layout = LayOut(
            State("a"),
            State("b"),
            new SysmlTransitionNode { Source = "a", Target = "b", Trigger = "first" },
            new SysmlTransitionNode { Source = "a", Target = "b", Trigger = "second" });

        // Act: route the same graph with the engine, using the final state sizes
        var frame = Assert.IsType<LayoutBox>(Assert.Single(layout.Nodes));
        var boxes = frame.Children.OfType<LayoutBox>().ToList();
        var a = Assert.Single(boxes, x => x.Label == "a");
        var b = Assert.Single(boxes, x => x.Label == "b");
        var placed = LayeredPlacement.Place(
            [(a.Width, a.Height), (b.Width, b.Height)],
            [(0, 1), (0, 1)],
            LayoutFlowDirection.Down,
            mergeParallelEdges: false,
            edgeTargetEnd: EndMarkerStyle.OpenChevron);
        var (dx, dy) = (a.X - placed.Rects[0].X, a.Y - placed.Rects[0].Y);
        var lines = frame.Children.OfType<LayoutLine>().ToList();
        var placements = ConnectorLabelPlacer.Place(lines, Themes.Light.FontSizeBody, boxes, NotationMetrics.MarkerScale(Themes.Light));

        // Assert: the engine routes the two edges differently, and each drawn line is exactly its own route
        Assert.Equal(2, lines.Count);
        Assert.NotEqual(placed.EdgePolylines[0], placed.EdgePolylines[1]);
        var labels = new[] { "first", "second" };
        for (var k = 0; k < 2; k++)
        {
            var line = Assert.Single(lines, l => l.MidpointLabel!.Contains(labels[k]));
            var expected = placed.EdgePolylines[k].Select(p => new Point2D(p.X + dx, p.Y + dy)).ToList();
            Assert.Equal(expected.Count, line.Waypoints.Count);
            for (var i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i].X, line.Waypoints[i].X, precision: 6);
                Assert.Equal(expected[i].Y, line.Waypoints[i].Y, precision: 6);
            }

            // The label's center lies on its own route
            var p = placements[line];
            var onRoute = false;
            for (var i = 0; i + 1 < line.Waypoints.Count; i++)
            {
                var (p0, p1) = (line.Waypoints[i], line.Waypoints[i + 1]);
                onRoute |= p.X >= Math.Min(p0.X, p1.X) - 1e-6 && p.X <= Math.Max(p0.X, p1.X) + 1e-6
                    && p.Y >= Math.Min(p0.Y, p1.Y) - 1e-6 && p.Y <= Math.Max(p0.Y, p1.Y) + 1e-6;
            }

            Assert.True(onRoute, $"Label '{labels[k]}' is not on its own route.");
        }
    }

    /// <summary>Loads and lays out a gallery model with the theme the <c>render</c> command uses; a missing model fails the test.</summary>
    private static async Task<LayoutTree> LayOutGalleryModelAsync(string fileName)
    {
        var workspace = await LabelClearance.LoadGalleryWorkspaceAsync(fileName);
        return new StateTransitionViewLayoutStrategy().BuildLayout(new ViewContext("v", workspace), new RenderOptions(SysmlThemes.Plain));
    }
}
