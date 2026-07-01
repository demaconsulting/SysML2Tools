// <copyright file="CycleBreaker.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>
namespace DemaConsulting.SysML2Tools.Layout.Engine.Layered;

/// <summary>
/// Pipeline stage that makes the input graph acyclic by reversing cycle-causing back edges,
/// following ELK's cycle-breaking phase.
/// </summary>
internal sealed class CycleBreaker : ILayoutStage
{
    /// <inheritdoc/>
    public void Apply(LayeredGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var (acyclic, reversed) = BreakCycles(graph.N, graph.Edges);
        graph.Acyclic = acyclic;
        graph.AcyclicReversed = reversed;
    }

    /// <summary>
    /// Returns the edge set with cycle-causing back edges reversed, using DFS to classify any
    /// edge to a node still on the recursion stack as a back edge. The second tuple element is a
    /// parallel flag array marking which retained edges were produced by reversing a back edge.
    /// </summary>
    private static (List<LayerEdge> Acyclic, bool[] Reversed) BreakCycles(int n, IReadOnlyList<LayerEdge> edges)
    {
        var adjacency = new List<int>[n];
        for (var i = 0; i < n; i++)
        {
            adjacency[i] = [];
        }

        foreach (var e in edges)
        {
            if (e.Source != e.Target)
            {
                adjacency[e.Source].Add(e.Target);
            }
        }

        var visited = new bool[n];
        var onStack = new bool[n];
        var backEdges = new HashSet<(int, int)>();

        void Dfs(int u)
        {
            visited[u] = true;
            onStack[u] = true;
            foreach (var v in adjacency[u])
            {
                if (onStack[v])
                {
                    backEdges.Add((u, v));
                }
                else if (!visited[v])
                {
                    Dfs(v);
                }
            }

            // S4143: standard DFS coloring — onStack[u] is read by recursive calls between the
            // true/false assignments; the analyzer cannot see across the recursion.
#pragma warning disable S4143
            onStack[u] = false;
#pragma warning restore S4143
        }

        for (var i = 0; i < n; i++)
        {
            if (!visited[i])
            {
                Dfs(i);
            }
        }

        var result = new List<LayerEdge>();
        var reversed = new List<bool>();
        var seen = new HashSet<(int, int)>();
        foreach (var e in edges)
        {
            if (e.Source == e.Target)
            {
                continue;
            }

            var isBack = backEdges.Contains((e.Source, e.Target));
            var (from, to) = isBack
                ? (e.Target, e.Source)
                : (e.Source, e.Target);

            if (from != to && seen.Add((from, to)))
            {
                result.Add(new LayerEdge(from, to));
                reversed.Add(isBack);
            }
        }

        return (result, [.. reversed]);
    }
}
