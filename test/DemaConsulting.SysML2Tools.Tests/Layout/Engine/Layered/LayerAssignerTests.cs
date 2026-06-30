// <copyright file="LayerAssignerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Engine;
using DemaConsulting.SysML2Tools.Layout.Engine.Layered;

namespace DemaConsulting.SysML2Tools.Tests.Layout.Engine.Layered;

/// <summary>
///     Tests for <see cref="LayerAssigner"/> covering monotonic layer assignment along a chain and
///     longest-path layer assignment for a diamond graph.
/// </summary>
public sealed class LayerAssignerTests
{
    /// <summary>
    ///     A linear chain (0-&gt;1-&gt;2) receives strictly increasing, contiguous layer
    ///     indices 0, 1, 2.
    /// </summary>
    [Fact]
    public void LayerAssigner_Apply_LinearChain_AssignsMonotonicLayers()
    {
        // Arrange: a three-node chain.
        var nodes = new List<LayerNode> { new(60, 40), new(60, 40), new(60, 40) };
        var edges = new List<LayerEdge> { new(0, 1), new(1, 2) };
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);

        // Act: break cycles, then assign layers.
        new CycleBreaker().Apply(graph);
        new LayerAssigner().Apply(graph);

        // Assert: contiguous, strictly increasing layers along the chain.
        Assert.Equal(0, graph.NodeLayers[0]);
        Assert.Equal(1, graph.NodeLayers[1]);
        Assert.Equal(2, graph.NodeLayers[2]);
    }

    /// <summary>
    ///     A diamond (0-&gt;1, 0-&gt;2, 1-&gt;3, 2-&gt;3) places the source at layer 0, both
    ///     branches at layer 1, and the join at the longest-path layer 2.
    /// </summary>
    [Fact]
    public void LayerAssigner_Apply_DiamondGraph_AssignsLongestPathLayers()
    {
        // Arrange: a four-node diamond.
        var nodes = new List<LayerNode> { new(60, 40), new(60, 40), new(60, 40), new(60, 40) };
        var edges = new List<LayerEdge> { new(0, 1), new(0, 2), new(1, 3), new(2, 3) };
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);

        // Act: break cycles, then assign layers.
        new CycleBreaker().Apply(graph);
        new LayerAssigner().Apply(graph);

        // Assert: source at 0, branches at 1, join at the longest-path layer 2.
        Assert.Equal(0, graph.NodeLayers[0]);
        Assert.Equal(1, graph.NodeLayers[1]);
        Assert.Equal(1, graph.NodeLayers[2]);
        Assert.Equal(2, graph.NodeLayers[3]);
    }
}
