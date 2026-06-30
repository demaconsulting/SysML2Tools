// <copyright file="CrossingMinimizerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Engine;
using DemaConsulting.SysML2Tools.Layout.Engine.Layered;

namespace DemaConsulting.SysML2Tools.Tests.Layout.Engine.Layered;

/// <summary>
///     Tests for <see cref="CrossingMinimizer"/> covering grouping of augmented nodes by layer and
///     that the grouping partitions every augmented node exactly once.
/// </summary>
public sealed class CrossingMinimizerTests
{
    /// <summary>
    ///     A diamond (0-&gt;1, 0-&gt;2, 1-&gt;3, 2-&gt;3) yields three layer groups holding the
    ///     source, the two branches, and the join, with per-layer counts 1, 2, 1.
    /// </summary>
    [Fact]
    public void CrossingMinimizer_Apply_TwoLayerGraph_GroupsNodesByLayer()
    {
        // Arrange: a four-node diamond.
        var nodes = new List<LayerNode> { new(60, 40), new(60, 40), new(60, 40), new(60, 40) };
        var edges = new List<LayerEdge> { new(0, 1), new(0, 2), new(1, 3), new(2, 3) };
        var graph = BuildGroupedGraph(nodes, edges);

        // Assert: three layers with counts 1, 2, 1.
        Assert.Equal(3, graph.Groups.Count);
        Assert.Single(graph.Groups[0]);
        Assert.Equal(2, graph.Groups[1].Count);
        Assert.Single(graph.Groups[2]);
    }

    /// <summary>
    ///     The groups partition every augmented node (including the dummy nodes of a long edge)
    ///     exactly once.
    /// </summary>
    [Fact]
    public void CrossingMinimizer_Apply_AllAugmentedNodesAppearInGroups()
    {
        // Arrange: a chain plus a span-three edge (introduces two dummy nodes).
        var nodes = new List<LayerNode> { new(60, 40), new(60, 40), new(60, 40), new(60, 40) };
        var edges = new List<LayerEdge> { new(0, 1), new(1, 2), new(2, 3), new(0, 3) };
        var graph = BuildGroupedGraph(nodes, edges);

        // Act: collect every index that appears across all layer groups.
        var indices = graph.Groups.SelectMany(g => g).OrderBy(i => i).ToList();

        // Assert: the groups contain exactly the indices 0..AugNodes.Count-1, each once.
        Assert.Equal(graph.AugNodes.Count, indices.Count);
        Assert.Equal(Enumerable.Range(0, graph.AugNodes.Count), indices);
    }

    /// <summary>Runs the stages up to and including crossing minimization and returns the graph.</summary>
    /// <param name="nodes">Input nodes.</param>
    /// <param name="edges">Input edges.</param>
    /// <returns>The graph after the crossing-minimization stage.</returns>
    private static LayeredGraph BuildGroupedGraph(List<LayerNode> nodes, List<LayerEdge> edges)
    {
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);
        new CycleBreaker().Apply(graph);
        new LayerAssigner().Apply(graph);
        new LongEdgeSplitter().Apply(graph);
        new CrossingMinimizer().Apply(graph);
        return graph;
    }
}
