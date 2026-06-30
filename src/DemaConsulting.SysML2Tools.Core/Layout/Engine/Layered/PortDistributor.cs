// <copyright file="PortDistributor.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>
using static DemaConsulting.SysML2Tools.Layout.Engine.Layered.LayeredLayoutMetrics;

namespace DemaConsulting.SysML2Tools.Layout.Engine.Layered;

/// <summary>
/// Pipeline stage that distributes connector ports evenly along each box face and records the
/// source-side and target-side port Y coordinate for every augmented sub-edge.
/// </summary>
internal sealed class PortDistributor : ILayoutStage
{
    /// <inheritdoc/>
    public void Apply(LayeredGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var n = graph.N;
        var nodes = graph.Nodes;
        var augNodes = graph.AugNodes;
        var augEdges = graph.AugEdges;
        var augY = graph.AugY;
        var numAugEdges = augEdges.Count;

        // Port Y values: augPortYSrc[i] = source (right face) Y; augPortYTgt[i] = target (left face) Y.
        var augPortYSrc = new double[numAugEdges];
        var augPortYTgt = new double[numAugEdges];

        // Distribute outgoing (source-side) ports on each real node's right face.
        var outByNode = new Dictionary<int, List<int>>();
        for (var ei = 0; ei < numAugEdges; ei++)
        {
            var src = augEdges[ei].Source;
            if (!outByNode.TryGetValue(src, out var list))
            {
                list = [];
                outByNode[src] = list;
            }

            list.Add(ei);
        }

        foreach (var (ni, edgeList) in outByNode)
        {
            if (augNodes[ni].IsDummy)
            {
                // Dummies pass the wire straight through at their own Y.
                foreach (var ei in edgeList)
                {
                    augPortYSrc[ei] = augY[ni];
                }
            }
            else
            {
                // Sort by target Y center, then edge index for stability.
                var sorted = edgeList
                    .OrderBy(ei => augY[augEdges[ei].Target] + (augNodes[augEdges[ei].Target].Height / 2.0))
                    .ThenBy(ei => ei)
                    .ToList();
                DistributePorts(sorted, augY[ni], nodes[ni].Height, augPortYSrc);
            }
        }

        // Distribute incoming (target-side) ports on each real node's left face.
        var inByNode = new Dictionary<int, List<int>>();
        for (var ei = 0; ei < numAugEdges; ei++)
        {
            var tgt = augEdges[ei].Target;
            if (!inByNode.TryGetValue(tgt, out var list))
            {
                list = [];
                inByNode[tgt] = list;
            }

            list.Add(ei);
        }

        foreach (var (ni, edgeList) in inByNode)
        {
            if (augNodes[ni].IsDummy)
            {
                foreach (var ei in edgeList)
                {
                    augPortYTgt[ei] = augY[ni];
                }
            }
            else
            {
                var sorted = edgeList
                    .OrderBy(ei => augY[augEdges[ei].Source] + (augNodes[augEdges[ei].Source].Height / 2.0))
                    .ThenBy(ei => ei)
                    .ToList();
                DistributePorts(sorted, augY[ni], nodes[ni < n ? ni : 0].Height, augPortYTgt);
            }
        }

        graph.AugPortYSrc = augPortYSrc;
        graph.AugPortYTgt = augPortYTgt;
    }

    /// <summary>
    /// Evenly distributes port Y positions along a node face, with
    /// <see cref="ConnectorClearance"/> inset from the top and bottom edges.
    /// </summary>
    private static void DistributePorts(
        IReadOnlyList<int> sortedEdgeIndices,
        double nodeTop,
        double nodeHeight,
        double[] portY)
    {
        var count = sortedEdgeIndices.Count;
        for (var k = 0; k < count; k++)
        {
            double y;
            if (count == 1)
            {
                y = nodeTop + (nodeHeight / 2.0);
            }
            else
            {
                var usable = nodeHeight - (2.0 * ConnectorClearance);
                y = nodeTop + ConnectorClearance + (k * usable / (count - 1));
            }

            portY[sortedEdgeIndices[k]] = Math.Clamp(
                y,
                nodeTop + ConnectorClearance,
                nodeTop + nodeHeight - ConnectorClearance);
        }
    }
}
