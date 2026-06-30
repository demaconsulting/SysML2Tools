// <copyright file="CycleBreakerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Engine;
using DemaConsulting.SysML2Tools.Layout.Engine.Layered;

namespace DemaConsulting.SysML2Tools.Tests.Layout.Engine.Layered;

/// <summary>
///     Tests for <see cref="CycleBreaker"/> covering production of an acyclic edge set from a
///     cyclic graph and removal of self-loops and duplicate edges.
/// </summary>
public sealed class CycleBreakerTests
{
    /// <summary>
    ///     A three-node cycle (0-&gt;1-&gt;2-&gt;0) is broken into an edge set with no directed
    ///     cycle, while keeping all three connections represented.
    /// </summary>
    [Fact]
    public void CycleBreaker_Apply_GraphWithCycle_ProducesAcyclicEdgeSet()
    {
        // Arrange: a tight three-node cycle.
        var nodes = new List<LayerNode> { new(60, 40), new(60, 40), new(60, 40) };
        var edges = new List<LayerEdge> { new(0, 1), new(1, 2), new(2, 0) };
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);

        // Act: break cycles.
        new CycleBreaker().Apply(graph);

        // Assert: all three connections survive and the result is acyclic.
        Assert.Equal(3, graph.Acyclic.Count);
        Assert.True(IsAcyclic(graph.N, graph.Acyclic), "Resulting edge set still contains a cycle.");
    }

    /// <summary>
    ///     Self-loops are dropped and duplicate source-target pairs are collapsed, so the acyclic
    ///     set contains each distinct connection once and no self-loop.
    /// </summary>
    [Fact]
    public void CycleBreaker_Apply_SelfLoopsAndDuplicates_AreRemoved()
    {
        // Arrange: a self-loop on node 0, a duplicated 0->1 edge, and a 1->2 edge.
        var nodes = new List<LayerNode> { new(60, 40), new(60, 40), new(60, 40) };
        var edges = new List<LayerEdge> { new(0, 0), new(0, 1), new(0, 1), new(1, 2) };
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);

        // Act: break cycles.
        new CycleBreaker().Apply(graph);

        // Assert: only the two distinct, non-self connections remain.
        Assert.Equal(2, graph.Acyclic.Count);
        Assert.DoesNotContain(graph.Acyclic, e => e.Source == e.Target);
    }

    /// <summary>Returns true when the edge set induces no directed cycle (Kahn's algorithm).</summary>
    /// <param name="n">Number of nodes.</param>
    /// <param name="edges">Directed edges to test.</param>
    /// <returns><c>true</c> if the edges form a directed acyclic graph; otherwise <c>false</c>.</returns>
    private static bool IsAcyclic(int n, IReadOnlyList<LayerEdge> edges)
    {
        var inDegree = new int[n];
        var outgoing = new List<int>[n];
        for (var i = 0; i < n; i++)
        {
            outgoing[i] = [];
        }

        foreach (var e in edges)
        {
            outgoing[e.Source].Add(e.Target);
            inDegree[e.Target]++;
        }

        var queue = new Queue<int>();
        for (var i = 0; i < n; i++)
        {
            if (inDegree[i] == 0)
            {
                queue.Enqueue(i);
            }
        }

        var processed = 0;
        while (queue.Count > 0)
        {
            var u = queue.Dequeue();
            processed++;
            foreach (var v in outgoing[u])
            {
                if (--inDegree[v] == 0)
                {
                    queue.Enqueue(v);
                }
            }
        }

        return processed == n;
    }
}
