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
}
