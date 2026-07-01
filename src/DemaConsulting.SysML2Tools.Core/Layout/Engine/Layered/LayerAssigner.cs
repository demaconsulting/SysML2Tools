// <copyright file="LayerAssigner.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>
namespace DemaConsulting.SysML2Tools.Layout.Engine.Layered;

/// <summary>
/// Pipeline stage that assigns each node to a layer using longest-path layering.
/// </summary>
internal sealed class LayerAssigner : ILayoutStage
{
    /// <inheritdoc/>
    public void Apply(LayeredGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        graph.NodeLayers = AssignLayers(graph.N, graph.Acyclic);
    }

    /// <summary>
    /// Assigns each node to a layer equal to the length of its longest incoming path
    /// (sources at layer 0, sinks at the maximum layer).
    /// </summary>
    private static int[] AssignLayers(int n, List<LayerEdge> edges)
    {
        var outgoing = new List<int>[n];
        var inDegree = new int[n];
        for (var i = 0; i < n; i++)
        {
            outgoing[i] = [];
        }

        foreach (var e in edges)
        {
            outgoing[e.Source].Add(e.Target);
            inDegree[e.Target]++;
        }

        var layer = new int[n];
        var queue = new Queue<int>();
        for (var i = 0; i < n; i++)
        {
            if (inDegree[i] == 0)
            {
                queue.Enqueue(i);
            }
        }

        var remaining = (int[])inDegree.Clone();
        while (queue.Count > 0)
        {
            var u = queue.Dequeue();
            foreach (var v in outgoing[u])
            {
                layer[v] = Math.Max(layer[v], layer[u] + 1);
                if (--remaining[v] == 0)
                {
                    queue.Enqueue(v);
                }
            }
        }

        return layer;
    }
}
