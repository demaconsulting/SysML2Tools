// <copyright file="ActionFlowViewLayoutStrategyTests.cs" company="DemaConsulting">
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
///     Tests for <see cref="ActionFlowViewLayoutStrategy"/>.
/// </summary>
public sealed class ActionFlowViewLayoutStrategyTests
{
    /// <summary>
    ///     An action definition with actions and successions produces action boxes, a start marker
    ///     (filled circle), a done marker (bullseye), and flow lines.
    /// </summary>
    [Fact]
    public void ActionFlowView_BuildLayout_ActionsAndSuccessions_ProducesBoxesMarkersAndFlows()
    {
        // Arrange: a chain a -> b -> c
        var strategy = new ActionFlowViewLayoutStrategy();
        var process = new SysmlDefinitionNode
        {
            Name = "Process",
            QualifiedName = "P::Process",
            DefinitionKeyword = "action def",
            Children =
            [
                new SysmlFeatureNode { Name = "a", QualifiedName = "P::Process::a", FeatureKeyword = "action" },
                new SysmlFeatureNode { Name = "b", QualifiedName = "P::Process::b", FeatureKeyword = "action" },
                new SysmlFeatureNode { Name = "c", QualifiedName = "P::Process::c", FeatureKeyword = "action" },
                new SysmlTransitionNode { Source = "a", Target = "b" },
                new SysmlTransitionNode { Source = "b", Target = "c" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["P::Process"] = process }
        };
        var context = new ViewContext("ActionFlow", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: three action boxes, a start circle, a done bullseye, and flow lines
        Assert.Equal(3, layout.Nodes.OfType<LayoutBox>().Count(b => b.Keyword == "action"));
        Assert.Contains(layout.Nodes.OfType<LayoutBadge>(), b => b.Shape == BadgeShape.FilledCircle);
        Assert.Contains(layout.Nodes.OfType<LayoutBadge>(), b => b.Shape == BadgeShape.Bullseye);
        Assert.True(layout.Nodes.OfType<LayoutLine>().Count() >= 2);
    }

    /// <summary>
    ///     Successive actions are placed top-to-bottom: a target action sits below its source.
    /// </summary>
    [Fact]
    public void ActionFlowView_BuildLayout_Successions_FlowTopToBottom()
    {
        // Arrange: a -> b
        var strategy = new ActionFlowViewLayoutStrategy();
        var process = new SysmlDefinitionNode
        {
            Name = "P",
            QualifiedName = "M::P",
            DefinitionKeyword = "action def",
            Children =
            [
                new SysmlFeatureNode { Name = "a", QualifiedName = "M::P::a", FeatureKeyword = "action" },
                new SysmlFeatureNode { Name = "b", QualifiedName = "M::P::b", FeatureKeyword = "action" },
                new SysmlTransitionNode { Source = "a", Target = "b" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["M::P"] = process }
        };
        var context = new ViewContext("ActionFlow", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: action "a" is positioned above action "b"
        var boxes = layout.Nodes.OfType<LayoutBox>().Where(b => b.Keyword == "action").ToList();
        var a = boxes.First(b => b.Label == "a");
        var b = boxes.First(box => box.Label == "b");
        Assert.True(a.Y < b.Y, "Source action should be above its successor.");
    }

    /// <summary>An empty workspace yields a minimal canvas.</summary>
    [Fact]
    public void ActionFlowView_BuildLayout_EmptyWorkspace_ReturnsMinimalCanvas()
    {
        var strategy = new ActionFlowViewLayoutStrategy();
        var workspace = new SysmlWorkspace();
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        Assert.Empty(layout.Nodes);
    }

    /// <summary>
    ///     A succession flow edge is drawn as a dashed line with an open arrowhead at the target.
    /// </summary>
    [Fact]
    public void ActionFlowView_BuildLayout_SuccessionEdge_IsDashedWithOpenArrowhead()
    {
        // Arrange: a -> b
        var strategy = new ActionFlowViewLayoutStrategy();
        var process = new SysmlDefinitionNode
        {
            Name = "P",
            QualifiedName = "M::P",
            DefinitionKeyword = "action def",
            Children =
            [
                new SysmlFeatureNode { Name = "a", QualifiedName = "M::P::a", FeatureKeyword = "action" },
                new SysmlFeatureNode { Name = "b", QualifiedName = "M::P::b", FeatureKeyword = "action" },
                new SysmlTransitionNode { Source = "a", Target = "b" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["M::P"] = process }
        };
        var context = new ViewContext("ActionFlow", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: the succession line between a and b is dashed with an open arrowhead
        var successionLines = layout.Nodes.OfType<LayoutLine>()
            .Where(l => l.MidpointLabel is null && l.LineStyle == LineStyle.Dashed)
            .ToList();
        Assert.NotEmpty(successionLines);
        Assert.All(successionLines, l => Assert.Equal(EndMarkerStyle.OpenChevron, l.TargetEnd));
    }

    /// <summary>
    ///     A forward chain of successions flows top-to-bottom: each target box is placed below its
    ///     source box, and every succession polyline is orthogonal (axis-aligned segments).
    /// </summary>
    [Fact]
    public void ActionFlowView_BuildLayout_ForwardChain_FlowsTopToBottomOrthogonally()
    {
        // Arrange: a three-action forward chain a -> b -> c.
        var strategy = new ActionFlowViewLayoutStrategy();
        var process = new SysmlDefinitionNode
        {
            Name = "P",
            QualifiedName = "M::P",
            DefinitionKeyword = "action def",
            Children =
            [
                new SysmlFeatureNode { Name = "a", QualifiedName = "M::P::a", FeatureKeyword = "action" },
                new SysmlFeatureNode { Name = "b", QualifiedName = "M::P::b", FeatureKeyword = "action" },
                new SysmlFeatureNode { Name = "c", QualifiedName = "M::P::c", FeatureKeyword = "action" },
                new SysmlTransitionNode { Source = "a", Target = "b" },
                new SysmlTransitionNode { Source = "b", Target = "c" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["M::P"] = process }
        };
        var context = new ViewContext("ActionFlow", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: boxes flow top-to-bottom (a above b above c).
        var boxes = layout.Nodes.OfType<LayoutBox>().Where(box => box.Keyword == "action").ToList();
        var a = boxes.Single(box => box.Label == "a");
        var b = boxes.Single(box => box.Label == "b");
        var c = boxes.Single(box => box.Label == "c");
        Assert.True(a.Y < b.Y, "action 'a' should be placed above action 'b'.");
        Assert.True(b.Y < c.Y, "action 'b' should be placed above action 'c'.");

        // Assert: every succession polyline is orthogonal (each segment is horizontal or vertical).
        var successions = layout.Nodes.OfType<LayoutLine>()
            .Where(l => l.MidpointLabel is null && l.LineStyle == LineStyle.Dashed)
            .ToList();
        Assert.NotEmpty(successions);
        foreach (var line in successions)
        {
            for (var i = 0; i + 1 < line.Waypoints.Count; i++)
            {
                var dx = Math.Abs(line.Waypoints[i + 1].X - line.Waypoints[i].X);
                var dy = Math.Abs(line.Waypoints[i + 1].Y - line.Waypoints[i].Y);
                Assert.True(dx < 1e-6 || dy < 1e-6, "succession segments must be axis-aligned.");
            }
        }
    }

    /// <summary>
    ///     The layered pipeline places action boxes without overlap.
    /// </summary>
    [Fact]
    public void ActionFlowView_BuildLayout_NoOverlap()
    {
        // Arrange: a small branch/join graph so several boxes share layers.
        var strategy = new ActionFlowViewLayoutStrategy();
        var process = new SysmlDefinitionNode
        {
            Name = "P",
            QualifiedName = "M::P",
            DefinitionKeyword = "action def",
            Children =
            [
                new SysmlFeatureNode { Name = "a", QualifiedName = "M::P::a", FeatureKeyword = "action" },
                new SysmlFeatureNode { Name = "b", QualifiedName = "M::P::b", FeatureKeyword = "action" },
                new SysmlFeatureNode { Name = "c", QualifiedName = "M::P::c", FeatureKeyword = "action" },
                new SysmlFeatureNode { Name = "d", QualifiedName = "M::P::d", FeatureKeyword = "action" },
                new SysmlTransitionNode { Source = "a", Target = "b" },
                new SysmlTransitionNode { Source = "a", Target = "c" },
                new SysmlTransitionNode { Source = "b", Target = "d" },
                new SysmlTransitionNode { Source = "c", Target = "d" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["M::P"] = process }
        };
        var context = new ViewContext("ActionFlow", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: no two action boxes overlap.
        var boxes = layout.Nodes.OfType<LayoutBox>().Where(box => box.Keyword == "action").ToList();
        for (var i = 0; i < boxes.Count; i++)
        {
            for (var j = i + 1; j < boxes.Count; j++)
            {
                var overlap =
                    boxes[i].X < boxes[j].X + boxes[j].Width &&
                    boxes[i].X + boxes[i].Width > boxes[j].X &&
                    boxes[i].Y < boxes[j].Y + boxes[j].Height &&
                    boxes[i].Y + boxes[i].Height > boxes[j].Y;
                Assert.False(overlap, $"action boxes '{boxes[i].Label}' and '{boxes[j].Label}' overlap.");
            }
        }
    }

    /// <summary>
    ///     A branch-and-join graph renders all four action boxes, a start marker that enters only the
    ///     source action, a done marker that leaves only the sink action, and four dashed
    ///     open-chevron successions.
    /// </summary>
    [Fact]
    public void ActionFlowView_BuildLayout_BranchAndJoin()
    {
        // Arrange: a -> b, a -> c, b -> d, c -> d (fork then join).
        var strategy = new ActionFlowViewLayoutStrategy();
        var process = new SysmlDefinitionNode
        {
            Name = "P",
            QualifiedName = "M::P",
            DefinitionKeyword = "action def",
            Children =
            [
                new SysmlFeatureNode { Name = "a", QualifiedName = "M::P::a", FeatureKeyword = "action" },
                new SysmlFeatureNode { Name = "b", QualifiedName = "M::P::b", FeatureKeyword = "action" },
                new SysmlFeatureNode { Name = "c", QualifiedName = "M::P::c", FeatureKeyword = "action" },
                new SysmlFeatureNode { Name = "d", QualifiedName = "M::P::d", FeatureKeyword = "action" },
                new SysmlTransitionNode { Source = "a", Target = "b" },
                new SysmlTransitionNode { Source = "a", Target = "c" },
                new SysmlTransitionNode { Source = "b", Target = "d" },
                new SysmlTransitionNode { Source = "c", Target = "d" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["M::P"] = process }
        };
        var context = new ViewContext("ActionFlow", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: all four action boxes are present.
        var boxes = layout.Nodes.OfType<LayoutBox>().Where(box => box.Keyword == "action").ToList();
        Assert.Equal(4, boxes.Count);
        Assert.Contains(boxes, box => box.Label == "a");
        Assert.Contains(boxes, box => box.Label == "d");

        // Assert: the start marker enters only 'a' and the done marker leaves only 'd'.
        var a = boxes.Single(box => box.Label == "a");
        var d = boxes.Single(box => box.Label == "d");
        var solidFlows = layout.Nodes.OfType<LayoutLine>()
            .Where(l => l.LineStyle == LineStyle.Solid && l.TargetEnd == EndMarkerStyle.FilledArrow)
            .ToList();
        var startFlows = solidFlows
            .Where(l => Math.Abs(l.Waypoints[^1].Y - a.Y) < 1e-6)
            .ToList();
        Assert.Single(startFlows);
        Assert.True(Math.Abs(startFlows[0].Waypoints[^1].X - (a.X + (a.Width / 2.0))) < 1e-6,
            "the start connector should enter the top centre of action 'a'.");
        var doneFlows = solidFlows
            .Where(l => Math.Abs(l.Waypoints[0].Y - (d.Y + d.Height)) < 1e-6)
            .ToList();
        Assert.Single(doneFlows);

        // Assert: all four successions are dashed with an open chevron at the target.
        var successions = layout.Nodes.OfType<LayoutLine>()
            .Where(l => l.MidpointLabel is null && l.LineStyle == LineStyle.Dashed)
            .ToList();
        Assert.Equal(4, successions.Count);
        Assert.All(successions, l => Assert.Equal(EndMarkerStyle.OpenChevron, l.TargetEnd));
    }

    /// <summary>
    ///     A two-action cycle a -> b, b -> a has its back edge broken by the pipeline, yet both
    ///     successions are still emitted with an open chevron end marker at their true targets.
    /// </summary>
    [Fact]
    public void ActionFlowView_BuildLayout_Cycle_IsBroken()
    {
        // Arrange: a -> b and b -> a (a cycle).
        var strategy = new ActionFlowViewLayoutStrategy();
        var process = new SysmlDefinitionNode
        {
            Name = "P",
            QualifiedName = "M::P",
            DefinitionKeyword = "action def",
            Children =
            [
                new SysmlFeatureNode { Name = "a", QualifiedName = "M::P::a", FeatureKeyword = "action" },
                new SysmlFeatureNode { Name = "b", QualifiedName = "M::P::b", FeatureKeyword = "action" },
                new SysmlTransitionNode { Source = "a", Target = "b" },
                new SysmlTransitionNode { Source = "b", Target = "a" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["M::P"] = process }
        };
        var context = new ViewContext("ActionFlow", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: both successions are emitted, dashed, with an open chevron at the target.
        var boxes = layout.Nodes.OfType<LayoutBox>().Where(box => box.Keyword == "action").ToList();
        var a = boxes.Single(box => box.Label == "a");
        var b = boxes.Single(box => box.Label == "b");
        var successions = layout.Nodes.OfType<LayoutLine>()
            .Where(l => l.MidpointLabel is null && l.LineStyle == LineStyle.Dashed)
            .ToList();
        Assert.Equal(2, successions.Count);
        Assert.All(successions, l => Assert.Equal(EndMarkerStyle.OpenChevron, l.TargetEnd));

        // Assert: one succession terminates near 'a' and one near 'b' (true targets preserved).
        static bool EndsNear(LayoutLine line, LayoutBox box) =>
            line.Waypoints[^1].X >= box.X - 1.0 &&
            line.Waypoints[^1].X <= box.X + box.Width + 1.0 &&
            line.Waypoints[^1].Y >= box.Y - MarkerBandTolerance &&
            line.Waypoints[^1].Y <= box.Y + box.Height + MarkerBandTolerance;
        Assert.Contains(successions, l => EndsNear(l, a));
        Assert.Contains(successions, l => EndsNear(l, b));
    }

    /// <summary>Tolerance for matching a back-edge chevron to its target box face.</summary>
    private const double MarkerBandTolerance = 60.0;

    /// <summary>
    ///     Builds a workspace with two candidate roots: <c>M::ProcessA</c> (two successions, three
    ///     actions — the heuristic's default pick), <c>M::ProcessB</c> (one succession, two
    ///     actions), and an unrelated sibling definition <c>M::Unrelated</c> with no
    ///     actions/successions.
    /// </summary>
    private static SysmlWorkspace BuildTwoRootWorkspace()
    {
        var processA = new SysmlDefinitionNode
        {
            Name = "ProcessA",
            QualifiedName = "M::ProcessA",
            DefinitionKeyword = "action def",
            Children =
            [
                new SysmlFeatureNode { Name = "a1", QualifiedName = "M::ProcessA::a1", FeatureKeyword = "action" },
                new SysmlFeatureNode { Name = "a2", QualifiedName = "M::ProcessA::a2", FeatureKeyword = "action" },
                new SysmlFeatureNode { Name = "a3", QualifiedName = "M::ProcessA::a3", FeatureKeyword = "action" },
                new SysmlFeatureNode { Name = "a4", QualifiedName = "M::ProcessA::a4", FeatureKeyword = "action" },
                new SysmlTransitionNode { Source = "a1", Target = "a2" },
                new SysmlTransitionNode { Source = "a2", Target = "a3" }
            ]
        };
        var processB = new SysmlDefinitionNode
        {
            Name = "ProcessB",
            QualifiedName = "M::ProcessB",
            DefinitionKeyword = "action def",
            Children =
            [
                new SysmlFeatureNode { Name = "b1", QualifiedName = "M::ProcessB::b1", FeatureKeyword = "action" },
                new SysmlFeatureNode { Name = "b2", QualifiedName = "M::ProcessB::b2", FeatureKeyword = "action" },
                new SysmlTransitionNode { Source = "b1", Target = "b2" }
            ]
        };
        var unrelated = new SysmlDefinitionNode { Name = "Unrelated", QualifiedName = "M::Unrelated", DefinitionKeyword = "action def" };
        return new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["M::ProcessA"] = processA,
                ["M::ProcessB"] = processB,
                ["M::Unrelated"] = unrelated
            }
        };
    }

    /// <summary>
    ///     With no <c>expose</c> statement (null <c>ViewNode</c>), the heuristic picks the
    ///     definition with the highest successions/actions score (<c>ProcessA</c>), unchanged from
    ///     pre-scoping behavior — the critical --auto/no-expose regression guard.
    /// </summary>
    [Fact]
    public void ActionFlowView_BuildLayout_NullViewNode_PicksHeuristicRootUnchanged()
    {
        var strategy = new ActionFlowViewLayoutStrategy();
        var workspace = BuildTwoRootWorkspace();
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var boxes = layout.Nodes.OfType<LayoutBox>().Where(b => b.Keyword == "action").ToList();
        Assert.Contains(boxes, b => b.Label == "a1");
        Assert.Contains(boxes, b => b.Label == "a3");
    }

    /// <summary>
    ///     An <c>expose</c> edge pointing at a definition other than the heuristic's default root
    ///     (<c>ProcessB</c>, which has a lower score than <c>ProcessA</c>) selects <c>ProcessB</c>
    ///     as the root instead.
    /// </summary>
    [Fact]
    public void ActionFlowView_BuildLayout_ExposeNonHeuristicRoot_SelectsExposedRoot()
    {
        var strategy = new ActionFlowViewLayoutStrategy();
        var workspace = BuildTwoRootWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "M::V",
            ExposeMembers = [new ExposeMember("ProcessB", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("M::V", "M::ProcessB", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var boxes = layout.Nodes.OfType<LayoutBox>().Where(b => b.Keyword == "action").ToList();
        Assert.Contains(boxes, b => b.Label == "b1");
        Assert.Contains(boxes, b => b.Label == "b2");
        Assert.DoesNotContain(boxes, b => b.Label is "a1" or "a2" or "a3" or "a4");
    }

    /// <summary>
    ///     An <c>expose</c> edge pointing at an inner child of a non-heuristic root
    ///     (<c>M::ProcessB::b1</c>) still selects <c>ProcessB</c> as the root, since the exposed
    ///     subject lies within the candidate root's own containment subtree.
    /// </summary>
    [Fact]
    public void ActionFlowView_BuildLayout_ExposeInnerChildOfNonHeuristicRoot_SelectsItsRoot()
    {
        var strategy = new ActionFlowViewLayoutStrategy();
        var workspace = BuildTwoRootWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "M::V",
            ExposeMembers = [new ExposeMember("b1", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("M::V", "M::ProcessB::b1", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var boxes = layout.Nodes.OfType<LayoutBox>().Where(b => b.Keyword == "action").ToList();
        Assert.Contains(boxes, b => b.Label == "b1");
    }

    /// <summary>
    ///     Builds a workspace where <c>M::ProcessA</c> (higher succession/action score) genuinely
    ///     nests <c>M::ProcessA::ProcessC</c> (lower score) as one of its own <c>Children</c>, while
    ///     both are also independently registered in <c>Declarations</c> under their own qualified
    ///     names — the shape needed to make both candidates scope-relevant for a subject exposed only
    ///     inside <c>ProcessC</c>.
    /// </summary>
    private static SysmlWorkspace BuildNestedCandidateWorkspace()
    {
        var processC = new SysmlDefinitionNode
        {
            Name = "ProcessC",
            QualifiedName = "M::ProcessA::ProcessC",
            DefinitionKeyword = "action def",
            Children =
            [
                new SysmlFeatureNode { Name = "c1", QualifiedName = "M::ProcessA::ProcessC::c1", FeatureKeyword = "action" },
                new SysmlFeatureNode { Name = "c2", QualifiedName = "M::ProcessA::ProcessC::c2", FeatureKeyword = "action" },
                new SysmlTransitionNode { Source = "c1", Target = "c2" }
            ]
        };
        var processA = new SysmlDefinitionNode
        {
            Name = "ProcessA",
            QualifiedName = "M::ProcessA",
            DefinitionKeyword = "action def",
            Children =
            [
                new SysmlFeatureNode { Name = "a1", QualifiedName = "M::ProcessA::a1", FeatureKeyword = "action" },
                new SysmlFeatureNode { Name = "a2", QualifiedName = "M::ProcessA::a2", FeatureKeyword = "action" },
                new SysmlFeatureNode { Name = "a3", QualifiedName = "M::ProcessA::a3", FeatureKeyword = "action" },
                new SysmlFeatureNode { Name = "a4", QualifiedName = "M::ProcessA::a4", FeatureKeyword = "action" },
                new SysmlTransitionNode { Source = "a1", Target = "a2" },
                new SysmlTransitionNode { Source = "a2", Target = "a3" },
                processC
            ]
        };
        return new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["M::ProcessA"] = processA,
                ["M::ProcessA::ProcessC"] = processC
            }
        };
    }

    /// <summary>
    ///     Exposing an inner action of the nested definition <c>ProcessC</c> selects <c>ProcessC</c>
    ///     as the root even though its ancestor <c>ProcessA</c> has a higher succession/action score
    ///     and would win the old pure-score tie-break, proving <c>FindRoot</c> now prefers the most
    ///     specific (deepest-qualified-name) scope-relevant candidate over a less specific ancestor.
    /// </summary>
    [Fact]
    public void ActionFlowView_BuildLayout_ExposeInnerActionOfNestedDefinition_SelectsNestedDefinitionNotAncestor()
    {
        var strategy = new ActionFlowViewLayoutStrategy();
        var workspace = BuildNestedCandidateWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "M::V",
            ExposeMembers = [new ExposeMember("c1", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("M::V", "M::ProcessA::ProcessC::c1", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var boxes = layout.Nodes.OfType<LayoutBox>().Where(b => b.Keyword == "action").ToList();
        Assert.Contains(boxes, b => b.Label == "c1");
        Assert.DoesNotContain(boxes, b => b.Label is "a1" or "a2" or "a3" or "a4");
    }

    /// <summary>
    ///     An <c>expose</c> edge pointing at a definition unrelated to every candidate root makes no
    ///     root scope-relevant, so no root is chosen and an empty canvas results.
    /// </summary>
    [Fact]
    public void ActionFlowView_BuildLayout_ExposeUnrelatedDefinition_NoRootSelected_ReturnsMinimalCanvas()
    {
        var strategy = new ActionFlowViewLayoutStrategy();
        var workspace = BuildTwoRootWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "M::V",
            ExposeMembers = [new ExposeMember("Unrelated", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("M::V", "M::Unrelated", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        Assert.Empty(layout.Nodes);
    }

    /// <summary>
    ///     Exposing action <c>a1</c> of <c>ProcessA</c> narrows the declared actions to those in
    ///     scope: the isolated declared action <c>a4</c> (never referenced by a succession) is
    ///     dropped, while <c>a2</c>/<c>a3</c> remain because they are re-synthesized from the
    ///     <c>a1</c>-&gt;<c>a2</c>-&gt;<c>a3</c> succession endpoints (the existing
    ///     synthesized-action mechanism, unaffected by scope, per the containment design). This
    ///     produces strictly fewer action boxes than the unscoped rendering.
    /// </summary>
    [Fact]
    public void ActionFlowView_BuildLayout_ExposeSingleAction_DropsOutOfScopeAction()
    {
        var strategy = new ActionFlowViewLayoutStrategy();
        var workspace = BuildTwoRootWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "M::V",
            ExposeMembers = [new ExposeMember("a1", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("M::V", "M::ProcessA::a1", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var scoped = strategy.BuildLayout(context, options);
        var full = strategy.BuildLayout(new ViewContext("full", workspace), options);

        var scopedLabels = scoped.Nodes.OfType<LayoutBox>().Where(b => b.Keyword == "action").Select(b => b.Label).ToList();
        var fullLabels = full.Nodes.OfType<LayoutBox>().Where(b => b.Keyword == "action").Select(b => b.Label).ToList();

        Assert.Contains("a1", scopedLabels);
        Assert.Contains("a2", scopedLabels);
        Assert.Contains("a3", scopedLabels);
        Assert.DoesNotContain("a4", scopedLabels);
        Assert.True(scopedLabels.Count < fullLabels.Count, $"expected scoped ({scopedLabels.Count}) < full ({fullLabels.Count})");
    }

    /// <summary>
    ///     An <c>expose</c> edge that resolves to a feature usage (not a definition) still selects
    ///     the definition it types via the shared usage-to-type fallback in
    ///     <c>ExposeScopeResolver.ResolveExposedScope</c>: exposing a usage <c>myProcess</c> typed
    ///     by <c>ProcessB</c> selects <c>ProcessB</c> as the root.
    /// </summary>
    [Fact]
    public void ActionFlowView_BuildLayout_ExposedUsage_ResolvesThroughTypingToRoot()
    {
        var strategy = new ActionFlowViewLayoutStrategy();
        var workspace = BuildTwoRootWorkspace();
        workspace.AddDeclaration("M::myProcess", new SysmlFeatureNode
        {
            Name = "myProcess",
            QualifiedName = "M::myProcess",
            FeatureTyping = "ProcessB",
            ResolvedEdges = [new SysmlEdge("M::myProcess", "M::ProcessB", SysmlEdgeKind.Typing)]
        });
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "M::V",
            ExposeMembers = [new ExposeMember("myProcess", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("M::V", "M::myProcess", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var boxes = layout.Nodes.OfType<LayoutBox>().Where(b => b.Keyword == "action").ToList();
        Assert.Contains(boxes, b => b.Label == "b1");
        Assert.Contains(boxes, b => b.Label == "b2");
    }

    /// <summary>
    ///     A named fork feeding two actions that later join renders a
    ///     <see cref="BadgeShape.HorizontalBar"/> badge at both the fork and the join, with the
    ///     succession edges wired between them (regression-guarding the ordinary-action rendering
    ///     path: <c>b1</c>/<c>b2</c> still render as plain boxes).
    /// </summary>
    [Fact]
    public void ActionFlowView_BuildLayout_ForkAndJoin_RenderHorizontalBarBadges()
    {
        var strategy = new ActionFlowViewLayoutStrategy();
        var process = new SysmlDefinitionNode
        {
            Name = "Process",
            QualifiedName = "P::Process",
            DefinitionKeyword = "action def",
            Children =
            [
                new SysmlFeatureNode { Name = "a", QualifiedName = "P::Process::a", FeatureKeyword = "action" },
                new SysmlFeatureNode { Name = "f", QualifiedName = null, FeatureKeyword = "fork" },
                new SysmlFeatureNode { Name = "b1", QualifiedName = "P::Process::b1", FeatureKeyword = "action" },
                new SysmlFeatureNode { Name = "b2", QualifiedName = "P::Process::b2", FeatureKeyword = "action" },
                new SysmlFeatureNode { Name = "j", QualifiedName = null, FeatureKeyword = "join" },
                new SysmlTransitionNode { Source = "a", Target = "f" },
                new SysmlTransitionNode { Source = "f", Target = "b1" },
                new SysmlTransitionNode { Source = "f", Target = "b2" },
                new SysmlTransitionNode { Source = "b1", Target = "j" },
                new SysmlTransitionNode { Source = "b2", Target = "j" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["P::Process"] = process }
        };
        var context = new ViewContext("ActionFlow", workspace);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var bars = layout.Nodes.OfType<LayoutBadge>().Where(b => b.Shape == BadgeShape.HorizontalBar).ToList();
        Assert.Equal(2, bars.Count);
        Assert.Contains(bars, b => b.Label == "f");
        Assert.Contains(bars, b => b.Label == "j");

        var actionBoxes = layout.Nodes.OfType<LayoutBox>().Where(b => b.Keyword == "action").ToList();
        Assert.Equal(3, actionBoxes.Count);
        Assert.Contains(actionBoxes, b => b.Label == "a");
        Assert.Contains(actionBoxes, b => b.Label == "b1");
        Assert.Contains(actionBoxes, b => b.Label == "b2");
    }

    /// <summary>
    ///     A guarded decision feeding a merge renders a <see cref="BadgeShape.Diamond"/> badge for
    ///     both the decision and the merge node.
    /// </summary>
    [Fact]
    public void ActionFlowView_BuildLayout_DecisionAndMerge_RenderDiamondBadges()
    {
        var strategy = new ActionFlowViewLayoutStrategy();
        var process = new SysmlDefinitionNode
        {
            Name = "Process",
            QualifiedName = "P::Process",
            DefinitionKeyword = "action def",
            Children =
            [
                new SysmlFeatureNode { Name = "monitor", QualifiedName = "P::Process::monitor", FeatureKeyword = "action" },
                new SysmlFeatureNode { Name = "d", QualifiedName = null, FeatureKeyword = "decide" },
                new SysmlFeatureNode { Name = "addCharge", QualifiedName = "P::Process::addCharge", FeatureKeyword = "action" },
                new SysmlFeatureNode { Name = "m", QualifiedName = null, FeatureKeyword = "merge" },
                new SysmlTransitionNode { Source = "monitor", Target = "d" },
                new SysmlTransitionNode { Source = "d", Target = "addCharge", Guard = "monitor.charge<100" },
                new SysmlTransitionNode { Source = "d", Target = "m" },
                new SysmlTransitionNode { Source = "addCharge", Target = "m" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["P::Process"] = process }
        };
        var context = new ViewContext("ActionFlow", workspace);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var diamonds = layout.Nodes.OfType<LayoutBadge>().Where(b => b.Shape == BadgeShape.Diamond).ToList();
        Assert.Equal(2, diamonds.Count);
        Assert.Contains(diamonds, b => b.Label == "d");
        Assert.Contains(diamonds, b => b.Label == "m");
    }

    /// <summary>
    ///     Accept/send nodes render as rounded-rectangle boxes (like ordinary actions) but keep
    ///     their own distinct keyword instead of the hard-coded <c>"action"</c> keyword.
    /// </summary>
    [Fact]
    public void ActionFlowView_BuildLayout_AcceptAndSend_RenderBoxesWithDistinctKeyword()
    {
        var strategy = new ActionFlowViewLayoutStrategy();
        var process = new SysmlDefinitionNode
        {
            Name = "Process",
            QualifiedName = "P::Process",
            DefinitionKeyword = "action def",
            Children =
            [
                new SysmlFeatureNode { Name = "sig", QualifiedName = "P::Process::sig", FeatureKeyword = "accept" },
                new SysmlFeatureNode { Name = "msg", QualifiedName = "P::Process::msg", FeatureKeyword = "send" },
                new SysmlTransitionNode { Source = "sig", Target = "msg" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["P::Process"] = process }
        };
        var context = new ViewContext("ActionFlow", workspace);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var boxes = layout.Nodes.OfType<LayoutBox>().ToList();
        Assert.Contains(boxes, b => b.Keyword == "accept" && b.Label == "sig");
        Assert.Contains(boxes, b => b.Keyword == "send" && b.Label == "msg");
        Assert.DoesNotContain(layout.Nodes.OfType<LayoutBadge>(), b => b.Shape is BadgeShape.Diamond or BadgeShape.HorizontalBar);
    }

    /// <summary>
    ///     A synthetic <c>$</c>-prefixed anonymous control-node name (as synthesized by
    ///     <c>AstBuilder</c> for an unnamed fork/decide/etc.) is never shown as the badge's label.
    /// </summary>
    [Fact]
    public void ActionFlowView_BuildLayout_SyntheticControlNodeName_RendersBlankLabel()
    {
        var strategy = new ActionFlowViewLayoutStrategy();
        var process = new SysmlDefinitionNode
        {
            Name = "Process",
            QualifiedName = "P::Process",
            DefinitionKeyword = "action def",
            Children =
            [
                new SysmlFeatureNode { Name = "a", QualifiedName = "P::Process::a", FeatureKeyword = "action" },
                new SysmlFeatureNode { Name = "$fork0", QualifiedName = null, FeatureKeyword = "fork" },
                new SysmlFeatureNode { Name = "b1", QualifiedName = "P::Process::b1", FeatureKeyword = "action" },
                new SysmlTransitionNode { Source = "a", Target = "$fork0" },
                new SysmlTransitionNode { Source = "$fork0", Target = "b1" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["P::Process"] = process }
        };
        var context = new ViewContext("ActionFlow", workspace);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var bar = Assert.Single(layout.Nodes.OfType<LayoutBadge>(), b => b.Shape == BadgeShape.HorizontalBar);
        Assert.Null(bar.Label);
    }

    /// <summary>
    ///     The compact <c>action a; then b;</c> idiom (an action followed directly by an attached
    ///     succession, as produced by <c>AstBuilder.VisitActionBodyItem</c>) resolves both nodes and
    ///     wires the succession between them.
    /// </summary>
    [Fact]
    public void ActionFlowView_BuildLayout_CompactActionThenIdiom_ResolvesBothNodes()
    {
        var strategy = new ActionFlowViewLayoutStrategy();
        var process = new SysmlDefinitionNode
        {
            Name = "Process",
            QualifiedName = "P::Process",
            DefinitionKeyword = "action def",
            Children =
            [
                new SysmlFeatureNode { Name = "a", QualifiedName = "P::Process::a", FeatureKeyword = "action" },
                new SysmlTransitionNode { Source = "a", Target = "b" },
                new SysmlFeatureNode { Name = "b", QualifiedName = "P::Process::b", FeatureKeyword = "action" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["P::Process"] = process }
        };
        var context = new ViewContext("ActionFlow", workspace);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var boxes = layout.Nodes.OfType<LayoutBox>().Where(b => b.Keyword == "action").ToList();
        Assert.Equal(2, boxes.Count);
        Assert.Contains(boxes, b => b.Label == "a");
        Assert.Contains(boxes, b => b.Label == "b");
        Assert.Contains(layout.Nodes.OfType<LayoutLine>(), l => l.LineStyle == LineStyle.Dashed);
    }
}
