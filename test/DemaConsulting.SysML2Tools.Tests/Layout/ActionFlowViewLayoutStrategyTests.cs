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
        Assert.All(successionLines, l => Assert.Equal(ArrowheadStyle.Open, l.TargetArrowhead));
    }
}
