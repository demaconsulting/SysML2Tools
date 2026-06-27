// <copyright file="SequenceViewLayoutStrategyTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout;
using DemaConsulting.SysML2Tools.Layout.Internal;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Internal;

namespace DemaConsulting.SysML2Tools.Tests.Layout;

/// <summary>
///     Tests for <see cref="SequenceViewLayoutStrategy"/>.
/// </summary>
public sealed class SequenceViewLayoutStrategyTests
{
    /// <summary>
    ///     A definition with messages produces a lifeline per participant and a message line per
    ///     message, ordered top-to-bottom by declaration order.
    /// </summary>
    [Fact]
    public void SequenceView_BuildLayout_Messages_ProducesLifelinesAndOrderedLines()
    {
        // Arrange: client/server with two messages
        var strategy = new SequenceViewLayoutStrategy();
        var protocol = new SysmlDefinitionNode
        {
            Name = "Protocol",
            QualifiedName = "P::Protocol",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlConnectionNode { Name = "request", ConnectionKeyword = "message", EndpointA = "client.a", EndpointB = "server.b" },
                new SysmlConnectionNode { Name = "response", ConnectionKeyword = "message", EndpointA = "server.c", EndpointB = "client.d" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["P::Protocol"] = protocol }
        };
        var context = new ViewContext("ProtocolSequenceView", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: two lifelines (client, server) and two message lines
        var lifelines = layout.Nodes.OfType<LayoutLifeline>().ToList();
        Assert.Equal(2, lifelines.Count);
        Assert.Contains(lifelines, l => l.Label == "client");
        Assert.Contains(lifelines, l => l.Label == "server");

        var lines = layout.Nodes.OfType<LayoutLine>().ToList();
        Assert.Equal(2, lines.Count);

        // The "request" line is above the "response" line (declaration order).
        var request = lines.First(l => l.MidpointLabel == "request");
        var response = lines.First(l => l.MidpointLabel == "response");
        Assert.True(request.Waypoints[0].Y < response.Waypoints[0].Y);
    }

    /// <summary>A message arrow runs horizontally from the sender lifeline to the receiver lifeline.</summary>
    [Fact]
    public void SequenceView_BuildLayout_Message_IsHorizontalBetweenLifelines()
    {
        // Arrange: a single message client -> server
        var strategy = new SequenceViewLayoutStrategy();
        var protocol = new SysmlDefinitionNode
        {
            Name = "P",
            QualifiedName = "M::P",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlConnectionNode { Name = "m", ConnectionKeyword = "message", EndpointA = "client.s", EndpointB = "server.r" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["M::P"] = protocol }
        };
        var context = new ViewContext("Sequence", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: the message line is horizontal (equal Y) and has a filled arrowhead at the target
        var line = Assert.Single(layout.Nodes.OfType<LayoutLine>());
        Assert.Equal(line.Waypoints[0].Y, line.Waypoints[^1].Y, 6);
        Assert.NotEqual(line.Waypoints[0].X, line.Waypoints[^1].X);
        Assert.Equal(ArrowheadStyle.Filled, line.TargetArrowhead);
    }

    /// <summary>A workspace with no messages yields a minimal canvas.</summary>
    [Fact]
    public void SequenceView_BuildLayout_NoMessages_ReturnsMinimalCanvas()
    {
        var strategy = new SequenceViewLayoutStrategy();
        var workspace = new SysmlWorkspace();
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        Assert.Empty(layout.Nodes);
    }
}
