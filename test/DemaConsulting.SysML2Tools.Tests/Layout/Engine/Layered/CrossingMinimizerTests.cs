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

    /// <summary>
    ///     A two-layer ordering whose natural (index) order crosses every edge is reordered by the
    ///     barycenter sweep so that the total number of edge crossings strictly decreases.
    /// </summary>
    [Fact]
    public void CrossingMinimizer_Apply_CrossingProneOrdering_ReducesCrossings()
    {
        // Arrange: two layers wired in fully-reversed order (0->5, 1->4, 2->3) so that the
        // natural index order crosses every pair of edges.
        var nodes = new List<LayerNode>
        {
            new(60, 40), new(60, 40), new(60, 40), new(60, 40), new(60, 40), new(60, 40),
        };
        var edges = new List<LayerEdge> { new(0, 5), new(1, 4), new(2, 3) };
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);
        new CycleBreaker().Apply(graph);
        new LayerAssigner().Apply(graph);
        new LongEdgeSplitter().Apply(graph);

        // Baseline crossing count for the natural (index-order) layer grouping.
        var initialGroups = GroupByLayerInIndexOrder(graph.AugNodes);
        var before = CountCrossings(initialGroups, graph.AugEdges, graph.AugNodes);

        // Act: run the barycenter crossing-minimization sweep.
        new CrossingMinimizer().Apply(graph);

        // Assert: the sweep strictly reduces the number of edge crossings.
        var after = CountCrossings(graph.Groups, graph.AugEdges, graph.AugNodes);
        Assert.True(before > 0, "the natural ordering should contain crossings to reduce");
        Assert.True(after < before, $"expected fewer crossings after ordering (before={before}, after={after})");
    }

    /// <summary>Groups augmented-node indices by layer, preserving the natural index order.</summary>
    /// <param name="augNodes">The augmented nodes.</param>
    /// <returns>One ordered index list per layer, in ascending node-index order.</returns>
    private static List<List<int>> GroupByLayerInIndexOrder(List<AugNode> augNodes)
    {
        var maxLayer = augNodes.Max(a => a.Layer);
        var groups = new List<List<int>>();
        for (var l = 0; l <= maxLayer; l++)
        {
            groups.Add([]);
        }

        for (var i = 0; i < augNodes.Count; i++)
        {
            groups[augNodes[i].Layer].Add(i);
        }

        return groups;
    }

    /// <summary>
    ///     Counts the total number of edge crossings implied by the given per-layer orderings.
    /// </summary>
    /// <param name="groups">The per-layer ordered index lists.</param>
    /// <param name="augEdges">The augmented sub-edges (each spanning one layer).</param>
    /// <param name="augNodes">The augmented nodes (for layer lookup).</param>
    /// <returns>The number of crossing edge pairs.</returns>
    private static int CountCrossings(List<List<int>> groups, List<AugEdge> augEdges, List<AugNode> augNodes)
    {
        var pos = new Dictionary<int, int>();
        foreach (var group in groups)
        {
            for (var i = 0; i < group.Count; i++)
            {
                pos[group[i]] = i;
            }
        }

        var crossings = 0;
        for (var a = 0; a < augEdges.Count; a++)
        {
            for (var b = a + 1; b < augEdges.Count; b++)
            {
                var e1 = augEdges[a];
                var e2 = augEdges[b];

                // Only edges leaving the same layer can cross in the corridor between two layers.
                if (augNodes[e1.Source].Layer != augNodes[e2.Source].Layer)
                {
                    continue;
                }

                int u1 = pos[e1.Source], v1 = pos[e1.Target];
                int u2 = pos[e2.Source], v2 = pos[e2.Target];
                if ((u1 < u2 && v1 > v2) || (u1 > u2 && v1 < v2))
                {
                    crossings++;
                }
            }
        }

        return crossings;
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
