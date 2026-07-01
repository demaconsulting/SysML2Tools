// <copyright file="CrossingMinimizer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>
using static DemaConsulting.SysML2Tools.Layout.Engine.Layered.LayeredLayoutMetrics;

namespace DemaConsulting.SysML2Tools.Layout.Engine.Layered;

/// <summary>
/// Pipeline stage that reduces edge crossings via Barycenter ordering over the augmented graph.
/// </summary>
internal sealed class CrossingMinimizer : ILayoutStage
{
    /// <inheritdoc/>
    public void Apply(LayeredGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        graph.Groups = GroupByLayerAug(graph.AugNodes);
        OrderLayersAug(graph.Groups, graph.AugNodes.Count, graph.AugEdges);
    }

    /// <summary>Groups augmented-node indices by layer.</summary>
    private static List<List<int>> GroupByLayerAug(List<AugNode> augNodes)
    {
        var maxLayer = augNodes.Max(a => a.Layer);
        var groups = new List<List<int>>(maxLayer + 1);
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
    /// Runs <see cref="BarycentricSweeps"/> Barycenter sweeps over the augmented graph
    /// (real nodes and dummies) to reduce edge crossings.
    /// </summary>
    private static void OrderLayersAug(List<List<int>> groups, int numAug, List<AugEdge> augEdges)
    {
        var leftNeighbors = new List<int>[numAug];
        var rightNeighbors = new List<int>[numAug];
        for (var i = 0; i < numAug; i++)
        {
            leftNeighbors[i] = [];
            rightNeighbors[i] = [];
        }

        foreach (var ae in augEdges)
        {
            rightNeighbors[ae.Source].Add(ae.Target);
            leftNeighbors[ae.Target].Add(ae.Source);
        }

        for (var sweep = 0; sweep < BarycentricSweeps; sweep++)
        {
            if (sweep % 2 == 0)
            {
                for (var l = 1; l < groups.Count; l++)
                {
                    SortByBarycenter(groups[l], groups[l - 1], leftNeighbors);
                }
            }
            else
            {
                for (var l = groups.Count - 2; l >= 0; l--)
                {
                    SortByBarycenter(groups[l], groups[l + 1], rightNeighbors);
                }
            }
        }
    }

    /// <summary>
    /// Sorts <paramref name="layer"/> by the average position of each node's neighbors in
    /// <paramref name="adjacentLayer"/>; nodes without neighbors keep their current relative order.
    /// </summary>
    private static void SortByBarycenter(List<int> layer, List<int> adjacentLayer, List<int>[] neighbors)
    {
        var position = new Dictionary<int, int>();
        for (var i = 0; i < adjacentLayer.Count; i++)
        {
            position[adjacentLayer[i]] = i;
        }

        var keyed = new List<(int Node, double Key, int Original)>(layer.Count);
        for (var i = 0; i < layer.Count; i++)
        {
            var node = layer[i];
            var ns = neighbors[node].Where(position.ContainsKey).ToList();
            var key = ns.Count > 0 ? ns.Average(x => position[x]) : i;
            keyed.Add((node, key, i));
        }

        keyed.Sort((a, b) =>
        {
            var c = a.Key.CompareTo(b.Key);
            return c != 0 ? c : a.Original.CompareTo(b.Original);
        });

        for (var i = 0; i < layer.Count; i++)
        {
            layer[i] = keyed[i].Node;
        }
    }
}
