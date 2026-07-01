// <copyright file="LongEdgeSplitterTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Engine;
using DemaConsulting.SysML2Tools.Layout.Engine.Layered;

namespace DemaConsulting.SysML2Tools.Tests.Layout.Engine.Layered;

/// <summary>
///     Tests for <see cref="LongEdgeSplitter"/> covering that unit-span edges add no dummy nodes
///     and that a long edge inserts one dummy node per intermediate layer.
/// </summary>
public sealed class LongEdgeSplitterTests
{
    /// <summary>
    ///     A chain of unit-span edges (0-&gt;1-&gt;2) produces an augmented node list equal in
    ///     count to the input node list, with no dummy nodes.
    /// </summary>
    [Fact]
    public void LongEdgeSplitter_Apply_SpanOneEdge_AddsNoDummyNodes()
    {
        // Arrange: a three-node chain (all edges span one layer).
        var nodes = new List<LayerNode> { new(60, 40), new(60, 40), new(60, 40) };
        var edges = new List<LayerEdge> { new(0, 1), new(1, 2) };
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);

        // Act: run the prerequisite stages, then split long edges.
        new CycleBreaker().Apply(graph);
        new LayerAssigner().Apply(graph);
        new LongEdgeSplitter().Apply(graph);

        // Assert: no dummy nodes were added.
        Assert.Equal(graph.N, graph.AugNodes.Count);
        Assert.DoesNotContain(graph.AugNodes, a => a.IsDummy);
    }

    /// <summary>
    ///     A span-three edge (0-&gt;3 over the chain 0-&gt;1-&gt;2-&gt;3) is split with one dummy
    ///     node in each of the two intermediate layers.
    /// </summary>
    [Fact]
    public void LongEdgeSplitter_Apply_LongEdge_InsertsDummyNodesPerIntermediateLayer()
    {
        // Arrange: a chain plus a span-three edge 0->3.
        var nodes = new List<LayerNode> { new(60, 40), new(60, 40), new(60, 40), new(60, 40) };
        var edges = new List<LayerEdge> { new(0, 1), new(1, 2), new(2, 3), new(0, 3) };
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);

        // Act: run the prerequisite stages, then split long edges.
        new CycleBreaker().Apply(graph);
        new LayerAssigner().Apply(graph);
        new LongEdgeSplitter().Apply(graph);

        // Assert: exactly two dummy nodes were inserted (layers 1 and 2).
        Assert.Equal(graph.N + 2, graph.AugNodes.Count);
        Assert.Equal(2, graph.AugNodes.Count(a => a.IsDummy));
    }
}
