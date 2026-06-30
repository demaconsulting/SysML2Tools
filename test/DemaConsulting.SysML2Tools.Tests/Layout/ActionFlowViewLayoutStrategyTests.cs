// <copyright file="ActionFlowViewLayoutStrategyTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout;
using DemaConsulting.SysML2Tools.Layout.Internal;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Internal;

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
}
