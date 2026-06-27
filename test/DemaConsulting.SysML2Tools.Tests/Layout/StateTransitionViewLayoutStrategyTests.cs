// <copyright file="StateTransitionViewLayoutStrategyTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout;
using DemaConsulting.SysML2Tools.Layout.Internal;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Internal;

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
}
