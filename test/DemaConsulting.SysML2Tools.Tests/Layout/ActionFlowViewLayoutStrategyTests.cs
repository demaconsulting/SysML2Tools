// <copyright file="ActionFlowViewLayoutStrategyTests.cs" company="DemaConsulting">
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
    ///     open-chevron successions; the start and done connectors use the same dashed open-chevron
    ///     style as the successions.
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

        // Assert: the start marker enters only 'a' and the done marker leaves only 'd'; both
        // connectors are dashed with an open chevron, exactly like the successions.
        var a = boxes.Single(box => box.Label == "a");
        var d = boxes.Single(box => box.Label == "d");
        var startBadge = layout.Nodes.OfType<LayoutBadge>().Single(b => b.Shape == BadgeShape.FilledCircle);
        var doneBadge = layout.Nodes.OfType<LayoutBadge>().Single(b => b.Shape == BadgeShape.Bullseye);
        var allLines = layout.Nodes.OfType<LayoutLine>().ToList();
        var startFlows = allLines
            .Where(l => Math.Abs(l.Waypoints[0].Y - (startBadge.CentreY + (startBadge.Size / 2.0))) < 1e-6)
            .ToList();
        var startFlow = Assert.Single(startFlows);
        Assert.True(Math.Abs(startFlow.Waypoints[^1].Y - a.Y) < 1e-6 &&
                    Math.Abs(startFlow.Waypoints[^1].X - (a.X + (a.Width / 2.0))) < 1e-6,
            "the start connector should enter the top center of action 'a'.");
        var doneFlows = allLines
            .Where(l => Math.Abs(l.Waypoints[^1].Y - (doneBadge.CentreY - (doneBadge.Size / 2.0))) < 1e-6)
            .ToList();
        var doneFlow = Assert.Single(doneFlows);
        Assert.True(Math.Abs(doneFlow.Waypoints[0].Y - (d.Y + d.Height)) < 1e-6,
            "the done connector should leave the bottom of action 'd'.");
        Assert.All(new[] { startFlow, doneFlow }, l =>
        {
            Assert.Equal(LineStyle.Dashed, l.LineStyle);
            Assert.Equal(EndMarkerStyle.OpenChevron, l.TargetEnd);
        });

        // Assert: all four successions are dashed with an open chevron at the target.
        var successions = allLines.Except([startFlow, doneFlow]).ToList();
        Assert.Equal(4, successions.Count);
        Assert.All(successions, l =>
        {
            Assert.Null(l.MidpointLabel);
            Assert.Equal(LineStyle.Dashed, l.LineStyle);
            Assert.Equal(EndMarkerStyle.OpenChevron, l.TargetEnd);
        });
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
    ///     A named fork feeding two actions that later join renders a thick bar
    ///     (<see cref="BadgeShape.ThickHorizontalBar"/>) at both the fork and the join, with no label
    ///     (the notation draws the bar alone), and the succession edges wired between them
    ///     (regression-guarding the ordinary-action rendering path: <c>b1</c>/<c>b2</c> still render
    ///     as plain boxes).
    /// </summary>
    [Fact]
    public void ActionFlowView_BuildLayout_ForkAndJoin_RenderThickBarBadges_WithoutLabel()
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

        var bars = layout.Nodes.OfType<LayoutBadge>().Where(b => b.Shape == BadgeShape.ThickHorizontalBar).ToList();
        Assert.Equal(2, bars.Count);
        Assert.All(bars, b => Assert.Null(b.Label));
        Assert.DoesNotContain(layout.Nodes.OfType<LayoutBadge>(), b => b.Shape == BadgeShape.HorizontalBar);

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
    ///     Accept/send nodes render as rounded-rectangle boxes carrying the specification's corner icon
    ///     (the icon replaces the keyword line), so the box has no keyword and keeps its name.
    /// </summary>
    [Fact]
    public void ActionFlowView_BuildLayout_AcceptAndSend_RenderBoxesWithCornerIcons()
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
        Assert.Contains(boxes, b => b.Label == "sig" && b.Appearance?.CornerIcon == BoxCornerIcon.Accept && b.Keyword is null);
        Assert.Contains(boxes, b => b.Label == "msg" && b.Appearance?.CornerIcon == BoxCornerIcon.Send && b.Keyword is null);
        Assert.DoesNotContain(layout.Nodes.OfType<LayoutBadge>(), b => b.Shape is BadgeShape.Diamond or BadgeShape.ThickHorizontalBar);
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
                new SysmlFeatureNode { Name = "$decide0", QualifiedName = null, FeatureKeyword = "decide" },
                new SysmlFeatureNode { Name = "b1", QualifiedName = "P::Process::b1", FeatureKeyword = "action" },
                new SysmlTransitionNode { Source = "a", Target = "$decide0" },
                new SysmlTransitionNode { Source = "$decide0", Target = "b1" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["P::Process"] = process }
        };
        var context = new ViewContext("ActionFlow", workspace);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var diamond = Assert.Single(layout.Nodes.OfType<LayoutBadge>(), b => b.Shape == BadgeShape.Diamond);
        Assert.Null(diamond.Label);
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

    /// <summary>Builds an action definition from the given children and lays it out with the Light theme.</summary>
    private static LayoutTree LayOut(params SysmlNode[] children)
    {
        var process = new SysmlDefinitionNode
        {
            Name = "Process",
            QualifiedName = "P::Process",
            DefinitionKeyword = "action def",
            Children = children,
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["P::Process"] = process },
        };
        return new ActionFlowViewLayoutStrategy().BuildLayout(new ViewContext("v", workspace), new RenderOptions(Themes.Light));
    }

    /// <summary>A feature of the given action-flow keyword (<c>action</c>, <c>fork</c>, <c>accept</c>, ...).</summary>
    private static SysmlFeatureNode Node(string name, string keyword = "action", string? actionText = null) =>
        new() { Name = name, QualifiedName = name.StartsWith('$') ? null : $"P::Process::{name}", FeatureKeyword = keyword, ActionText = actionText };

    /// <summary>A succession between two named nodes.</summary>
    private static SysmlTransitionNode Then(string source, string target, string? guard = null, bool isElse = false) =>
        new() { Source = source, Target = target, Guard = guard, IsElse = isElse };

    /// <summary>The fork's thick bar badge of a layout.</summary>
    private static LayoutBadge Bar(LayoutTree layout) =>
        Assert.Single(layout.Nodes.OfType<LayoutBadge>(), b => b.Shape == BadgeShape.ThickHorizontalBar);

    /// <summary>A fork bar grows with its fan-out so its connections fit on its long face.</summary>
    [Fact]
    public void ActionFlowView_BuildLayout_ThickBar_LengthGrowsWithFanOut()
    {
        // Arrange / Act
        var narrow = LayOut(Node("a"), Node("f", "fork"), Node("b1"), Node("b2"), Then("a", "f"), Then("f", "b1"), Then("f", "b2"));
        var wide = LayOut(
            Node("a"), Node("f", "fork"), Node("b1"), Node("b2"), Node("b3"), Node("b4"), Node("b5"),
            Then("a", "f"), Then("f", "b1"), Then("f", "b2"), Then("f", "b3"), Then("f", "b4"), Then("f", "b5"));

        // Assert
        Assert.True(Bar(wide).Size > Bar(narrow).Size);
        Assert.True(Bar(wide).Size >= 6 * 24.0);
        Assert.True(Bar(narrow).Size >= 64.0);
    }

    /// <summary>The connections leaving a fork attach at least 16 px apart along the bar's face.</summary>
    [Fact]
    public void ActionFlowView_BuildLayout_ThickBar_AnchorsAtLeast16pxApart()
    {
        // Arrange / Act
        var layout = LayOut(
            Node("a"), Node("f", "fork"), Node("b1"), Node("b2"), Node("b3"),
            Then("a", "f"), Then("f", "b1"), Then("f", "b2"), Then("f", "b3"));
        var bar = Bar(layout);

        // Assert: the three outgoing connections start at distinct X positions on the bar
        var starts = layout.Nodes.OfType<LayoutLine>()
            .Where(l => Math.Abs(l.Waypoints[0].Y - (bar.CentreY + (NotationMetrics.ThickBarThickness / 2.0))) < 0.01)
            .Select(l => l.Waypoints[0].X)
            .Order()
            .ToList();
        Assert.Equal(3, starts.Count);
        Assert.True(starts[1] - starts[0] >= 16.0 - 0.01 && starts[2] - starts[1] >= 16.0 - 0.01);
        Assert.All(starts, x => Assert.InRange(x, bar.CentreX - (bar.Size / 2.0), bar.CentreX + (bar.Size / 2.0)));
    }

    /// <summary>Connectors stop at and start from the drawn bar, not the taller slot the engine reserved for it.</summary>
    [Fact]
    public void ActionFlowView_BuildLayout_ThickBar_ConnectorsTouchTheBar()
    {
        // Arrange / Act
        var layout = LayOut(Node("a"), Node("f", "fork"), Node("b"), Then("a", "f"), Then("f", "b"));
        var bar = Bar(layout);
        var half = NotationMetrics.ThickBarThickness / 2.0;
        var lines = layout.Nodes.OfType<LayoutLine>().ToList();

        // Assert
        Assert.Contains(lines, l => Math.Abs(l.Waypoints[^1].Y - (bar.CentreY - half)) < 0.01);
        Assert.Contains(lines, l => Math.Abs(l.Waypoints[0].Y - (bar.CentreY + half)) < 0.01);
    }

    /// <summary>Connectors meet a decision diamond's outline, not the corner of its bounding box.</summary>
    [Fact]
    public void ActionFlowView_BuildLayout_Decision_ConnectorsTouchTheDiamondOutline()
    {
        // Arrange / Act
        var layout = LayOut(
            Node("a"), Node("d", "decide"), Node("b"), Node("c"),
            Then("a", "d"), Then("d", "b", "ok"), Then("d", "c", isElse: true));
        var diamond = Assert.Single(layout.Nodes.OfType<LayoutBadge>(), b => b.Shape == BadgeShape.Diamond);

        // Assert: every connector end on the diamond satisfies |dx| + |dy| = half the diagonal
        var half = diamond.Size / 2.0;
        var ends = layout.Nodes.OfType<LayoutLine>()
            .SelectMany(l => new[] { l.Waypoints[0], l.Waypoints[^1] })
            .Where(p => Math.Abs(p.X - diamond.CentreX) <= half + 0.01 && Math.Abs(p.Y - diamond.CentreY) <= half + 0.01)
            .ToList();
        Assert.Equal(3, ends.Count);
        Assert.All(ends, p => Assert.Equal(half, Math.Abs(p.X - diamond.CentreX) + Math.Abs(p.Y - diamond.CentreY), precision: 2));
    }

    /// <summary>A bar's only connection attaches where the other end lies, so it runs straight instead of kinking.</summary>
    [Fact]
    public void ActionFlowView_BuildLayout_ThickBar_SingleConnection_RunsStraight()
    {
        // Arrange / Act: a fork and a join whose branches differ in length (so the join is off-center
        // from the action below it), then one action after the join
        var layout = LayOut(
            Node("a"), Node("f", "fork"), Node("b1"), Node("c1"), Node("b2"), Node("j", "join"), Node("d"),
            Then("a", "f"), Then("f", "b1"), Then("f", "b2"), Then("b1", "c1"), Then("c1", "j"), Then("b2", "j"), Then("j", "d"));
        var join = layout.Nodes.OfType<LayoutBadge>().Single(b => b.Shape == BadgeShape.ThickHorizontalBar && b.CentreY > layout.Nodes.OfType<LayoutBox>().Single(x => x.Label == "c1").Y);
        var bottom = join.CentreY + (NotationMetrics.ThickBarThickness / 2.0);

        // Assert: the connector that leaves the join is one straight vertical segment
        var line = Assert.Single(layout.Nodes.OfType<LayoutLine>(), l => Math.Abs(l.Waypoints[0].Y - bottom) < 0.01);
        Assert.Equal(2, line.Waypoints.Count);
        Assert.Equal(line.Waypoints[0].X, line.Waypoints[1].X, precision: 3);
    }

    /// <summary>A guarded succession is labeled with its bracketed guard.</summary>
    [Fact]
    public void ActionFlowView_BuildLayout_GuardedSuccession_LabelsEdge()
    {
        // Arrange / Act
        var layout = LayOut(Node("d", "decide"), Node("b"), Then("d", "b", "ok"));

        // Assert
        Assert.Contains(layout.Nodes.OfType<LayoutLine>(), l => l.MidpointLabel == "[ok]");
    }

    /// <summary>The default branch of a decision is labeled <c>else</c>, without brackets.</summary>
    [Fact]
    public void ActionFlowView_BuildLayout_ElseSuccession_LabelsEdgeElse()
    {
        // Arrange / Act
        var layout = LayOut(Node("d", "decide"), Node("b"), Node("c"), Then("d", "b", "ok"), Then("d", "c", isElse: true), Then("b", "c"));

        // Assert
        var labels = layout.Nodes.OfType<LayoutLine>().Select(l => l.MidpointLabel).ToList();
        Assert.Contains("[ok]", labels);
        Assert.Contains("else", labels);
        Assert.Equal(2, labels.Count(l => l is not null));
    }

    /// <summary>A terminate node is a crossed circle that only receives successions.</summary>
    [Fact]
    public void ActionFlowView_BuildLayout_TerminateNode_RendersCrossedCircle_NoOutgoing()
    {
        // Arrange / Act
        var layout = LayOut(Node("a"), Node("t", "terminate"), Then("a", "t"));

        // Assert
        var badge = Assert.Single(layout.Nodes.OfType<LayoutBadge>(), b => b.Shape == BadgeShape.CrossedCircle);
        Assert.Null(badge.Label);
        var lines = layout.Nodes.OfType<LayoutLine>().ToList();
        Assert.Contains(lines, l => Math.Abs(l.Waypoints[^1].Y - (badge.CentreY - (badge.Size / 2.0))) < 0.01);
        Assert.DoesNotContain(lines, l => Math.Abs(l.Waypoints[0].Y - (badge.CentreY + (badge.Size / 2.0))) < 0.01);
    }

    /// <summary>A terminate node ends its flow, so it is not an inferred end that the done marker follows.</summary>
    [Fact]
    public void ActionFlowView_BuildLayout_TerminateNode_NotTreatedAsDoneSource()
    {
        // Arrange: a -> b -> (end), a -> t (terminate); no explicit done
        var layout = LayOut(Node("a"), Node("b"), Node("t", "terminate"), Then("a", "b"), Then("a", "t"));
        var done = Assert.Single(layout.Nodes.OfType<LayoutBadge>(), b => b.Shape == BadgeShape.Bullseye);
        var terminate = Assert.Single(layout.Nodes.OfType<LayoutBadge>(), b => b.Shape == BadgeShape.CrossedCircle);

        // Act: lines that arrive at the done marker
        var intoDone = layout.Nodes.OfType<LayoutLine>()
            .Where(l => Math.Abs(l.Waypoints[^1].X - done.CentreX) < 0.01 && Math.Abs(l.Waypoints[^1].Y - (done.CentreY - (done.Size / 2.0))) < 0.01)
            .ToList();

        // Assert: only b's flow reaches done; the terminate node has no outgoing line
        Assert.Single(intoDone);
        Assert.DoesNotContain(layout.Nodes.OfType<LayoutLine>(), l => Math.Abs(l.Waypoints[0].Y - (terminate.CentreY + (terminate.Size / 2.0))) < 0.01);
    }

    /// <summary>When every flow ends in a terminate node, no done marker is inferred.</summary>
    [Fact]
    public void ActionFlowView_BuildLayout_OnlyTerminateEnds_DrawsNoDoneMarker()
    {
        // Arrange / Act
        var layout = LayOut(Node("a"), Node("t", "terminate"), Then("a", "t"));

        // Assert
        Assert.DoesNotContain(layout.Nodes.OfType<LayoutBadge>(), b => b.Shape == BadgeShape.Bullseye);
    }

    /// <summary>
    ///     An explicit <c>first start then X;</c> draws no <c>start</c> box and the start marker enters
    ///     only X, even when another action has no predecessor.
    /// </summary>
    [Fact]
    public void ActionFlowView_BuildLayout_ExplicitStart_NoStartBox_MarkerEntersTarget()
    {
        // Arrange: a -> b, and c with no predecessor; start enters a
        var layout = LayOut(Node("a"), Node("b"), Node("c"), Then("start", "a"), Then("a", "b"));
        var marker = Assert.Single(layout.Nodes.OfType<LayoutBadge>(), b => b.Shape == BadgeShape.FilledCircle);
        var boxes = layout.Nodes.OfType<LayoutBox>().ToList();
        var a = boxes.Single(b => b.Label == "a");

        // Act
        var fromMarker = layout.Nodes.OfType<LayoutLine>()
            .Where(l => Math.Abs(l.Waypoints[0].X - marker.CentreX) < 0.01 && Math.Abs(l.Waypoints[0].Y - (marker.CentreY + (marker.Size / 2.0))) < 0.01)
            .ToList();

        // Assert
        Assert.DoesNotContain(boxes, b => b.Label == "start");
        var line = Assert.Single(fromMarker);
        Assert.InRange(line.Waypoints[^1].X, a.X, a.X + a.Width);
        Assert.Equal(a.Y, line.Waypoints[^1].Y, precision: 2);
    }

    /// <summary>An explicit <c>then done;</c> draws no <c>done</c> box and the done marker follows only that action.</summary>
    [Fact]
    public void ActionFlowView_BuildLayout_ExplicitDone_ConnectsOnlyDeclaredSources()
    {
        // Arrange: a -> b and a -> c; only c declares done (b would otherwise be an inferred end)
        var layout = LayOut(Node("a"), Node("b"), Node("c"), Then("a", "b"), Then("a", "c"), Then("c", "done"));
        var done = Assert.Single(layout.Nodes.OfType<LayoutBadge>(), b => b.Shape == BadgeShape.Bullseye);

        // Act
        var intoDone = layout.Nodes.OfType<LayoutLine>()
            .Where(l => Math.Abs(l.Waypoints[^1].X - done.CentreX) < 0.01 && Math.Abs(l.Waypoints[^1].Y - (done.CentreY - (done.Size / 2.0))) < 0.01)
            .ToList();

        // Assert
        Assert.DoesNotContain(layout.Nodes.OfType<LayoutBox>(), b => b.Label == "done");
        var line = Assert.Single(intoDone);
        var c = layout.Nodes.OfType<LayoutBox>().Single(b => b.Label == "c");
        Assert.Equal(c.Y + c.Height, line.Waypoints[0].Y, precision: 2);
    }

    /// <summary>Without explicit start or done successions the markers are still inferred from the topology.</summary>
    [Fact]
    public void ActionFlowView_BuildLayout_NoExplicitMarkers_HeuristicUnchanged()
    {
        // Arrange / Act
        var layout = LayOut(Node("a"), Node("b"), Then("a", "b"));

        // Assert: one start marker, one done marker, and a connector at each
        Assert.Single(layout.Nodes.OfType<LayoutBadge>(), b => b.Shape == BadgeShape.FilledCircle);
        Assert.Single(layout.Nodes.OfType<LayoutBadge>(), b => b.Shape == BadgeShape.Bullseye);
        Assert.Equal(3, layout.Nodes.OfType<LayoutLine>().Count());
    }

    /// <summary>A typed action shows <c>name : Type [multiplicity]</c> under its keyword.</summary>
    [Fact]
    public void ActionFlowView_BuildLayout_TypedAction_LabelShowsTypeAndMultiplicity()
    {
        // Arrange
        var typed = new SysmlFeatureNode
        {
            Name = "build",
            QualifiedName = "P::Process::build",
            FeatureKeyword = "action",
            FeatureTyping = "BuildStep",
            Multiplicity = "[2]",
        };

        // Act
        var layout = LayOut(typed, Node("test"), Then("build", "test"));

        // Assert
        Assert.Single(layout.Nodes.OfType<LayoutBox>(), b => b.Keyword == "action" && b.Label == "build : BuildStep [2]");
        Assert.Single(layout.Nodes.OfType<LayoutBox>(), b => b.Keyword == "action" && b.Label == "test");
    }

    /// <summary>An accept or send node lists its payload in the box body and is wide enough for it.</summary>
    [Fact]
    public void ActionFlowView_BuildLayout_AcceptPayload_RendersTextRow()
    {
        // Arrange / Act
        var payload = "accept " + new string('x', 40);
        var layout = LayOut(
            Node("waitFor", "accept", payload),
            Node("tell", "send", "send report to carrier"),
            Then("waitFor", "tell"));

        // Assert
        var accept = layout.Nodes.OfType<LayoutBox>().Single(b => b.Label == "waitFor");
        var row = Assert.Single(Assert.Single(accept.Compartments).Rows);
        Assert.Equal(payload, row);
        Assert.True(accept.Width >= row.Length * Themes.Light.FontSizeBody * 0.62);
        var send = layout.Nodes.OfType<LayoutBox>().Single(b => b.Label == "tell");
        Assert.Equal(["send report to carrier"], Assert.Single(send.Compartments).Rows);
    }

    /// <summary>
    ///     A succession that returns to an earlier action ends in an open chevron on a final straight
    ///     approach at least as long as the marker.
    /// </summary>
    [Fact]
    public void ActionFlowView_BuildLayout_BackEdge_OpenChevronWithFlushApproach()
    {
        // Arrange / Act: a -> b -> c -> a
        var layout = LayOut(Node("a"), Node("b"), Node("c"), Then("a", "b"), Then("b", "c"), Then("c", "a"));

        // Assert
        var marker = NotationMetrics.RenderedAlongLineLength(EndMarkerStyle.OpenChevron, Themes.Light);
        var successions = layout.Nodes.OfType<LayoutLine>().Where(l => l.LineStyle == LineStyle.Dashed).ToList();
        Assert.All(successions, line =>
        {
            Assert.Equal(EndMarkerStyle.OpenChevron, line.TargetEnd);
            var (last, previous) = (line.Waypoints[^1], line.Waypoints[^2]);
            Assert.True(Math.Abs(last.X - previous.X) + Math.Abs(last.Y - previous.Y) >= marker - 0.01);
        });
    }

    /// <summary>
    ///     The gallery's pipeline model exercises the notation: thick bars, a labeled decision, an
    ///     accept and a send icon, a typed action, a terminate node, and the explicit start and done.
    /// </summary>
    [Fact]
    public async Task ActionFlowView_BuildLayout_PipelineGalleryModel_ShowsActionFlowNotation()
    {
        // Arrange
        var dir = AppContext.BaseDirectory;
        string? modelPath = null;
        while (dir is not null && modelPath is null)
        {
            var candidate = Path.Combine(dir, "docs", "gallery", "models", "04-pipeline-action-flow.sysml");
            modelPath = File.Exists(candidate) ? candidate : null;
            dir = Directory.GetParent(dir)?.FullName;
        }

        if (modelPath is null)
        {
            return;
        }

        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([modelPath], stdlibTable);
        Assert.NotNull(result.Workspace);

        // Act
        var layout = new ActionFlowViewLayoutStrategy().BuildLayout(new ViewContext("v", result.Workspace), new RenderOptions(Themes.Light));

        // Assert
        var badges = layout.Nodes.OfType<LayoutBadge>().ToList();
        Assert.Equal(2, badges.Count(b => b.Shape == BadgeShape.ThickHorizontalBar));
        Assert.Single(badges, b => b.Shape == BadgeShape.Diamond);
        Assert.Single(badges, b => b.Shape == BadgeShape.CrossedCircle);
        Assert.Single(badges, b => b.Shape == BadgeShape.Bullseye);
        Assert.Single(badges, b => b.Shape == BadgeShape.FilledCircle);
        var boxes = layout.Nodes.OfType<LayoutBox>().ToList();
        Assert.Contains(boxes, b => b.Appearance?.CornerIcon == BoxCornerIcon.Accept && b.Label == "awaitApproval");
        Assert.Contains(boxes, b => b.Appearance?.CornerIcon == BoxCornerIcon.Send && b.Label == "notifySuccess");
        Assert.Contains(boxes, b => b.Label == "build : BuildStep");
        Assert.DoesNotContain(boxes, b => b.Label is "start" or "done");
        var labels = layout.Nodes.OfType<LayoutLine>().Select(l => l.MidpointLabel).ToList();
        Assert.Contains("[smokePassed]", labels);
        Assert.Contains("else", labels);
    }
}
