// <copyright file="LayeredGraphTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Engine;
using DemaConsulting.SysML2Tools.Layout.Engine.Layered;

namespace DemaConsulting.SysML2Tools.Tests.Layout.Engine.Layered;

/// <summary>
///     Tests for <see cref="LayeredGraph"/> covering null-argument validation and that the
///     supplied nodes, edges, direction, and node count are preserved.
/// </summary>
public sealed class LayeredGraphTests
{
    /// <summary>
    ///     Constructing a graph with a null node list throws <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void LayeredGraph_Constructor_NullNodes_ThrowsArgumentNullException()
    {
        // Arrange: a valid edge list but a null node list.
        var edges = new List<LayerEdge>();

        // Act / Assert: construction rejects the null node list.
        Assert.Throws<ArgumentNullException>(
            () => new LayeredGraph(null!, edges, LayoutDirection.Right));
    }

    /// <summary>
    ///     Constructing a graph with a null edge list throws <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void LayeredGraph_Constructor_NullEdges_ThrowsArgumentNullException()
    {
        // Arrange: a valid node list but a null edge list.
        var nodes = new List<LayerNode> { new(60, 40) };

        // Act / Assert: construction rejects the null edge list.
        Assert.Throws<ArgumentNullException>(
            () => new LayeredGraph(nodes, null!, LayoutDirection.Right));
    }

    /// <summary>
    ///     Construction preserves the supplied nodes, edges, and direction and reports the
    ///     input node count.
    /// </summary>
    [Fact]
    public void LayeredGraph_Constructor_ValidInput_StoresNodesEdgesDirectionAndCount()
    {
        // Arrange: three nodes and two edges.
        var nodes = new List<LayerNode> { new(60, 40), new(60, 40), new(60, 40) };
        var edges = new List<LayerEdge> { new(0, 1), new(1, 2) };

        // Act: construct the shared graph state.
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);

        // Assert: the inputs are preserved unchanged.
        Assert.Same(nodes, graph.Nodes);
        Assert.Same(edges, graph.Edges);
        Assert.Equal(LayoutDirection.Right, graph.Direction);
        Assert.Equal(3, graph.N);
    }
}
