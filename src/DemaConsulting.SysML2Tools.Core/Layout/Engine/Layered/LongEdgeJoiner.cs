// <copyright file="LongEdgeJoiner.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>
namespace DemaConsulting.SysML2Tools.Layout.Engine.Layered;

/// <summary>
/// Pipeline stage that assembles per-original-edge orthogonal waypoints by concatenating the bend
/// points of each sub-edge in source-to-target layer order (ELK's <c>LongEdgeJoiner</c>).
/// </summary>
internal sealed class LongEdgeJoiner : ILayoutStage
{
    /// <inheritdoc/>
    public void Apply(LayeredGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var augNodes = graph.AugNodes;
        var augEdges = graph.AugEdges;
        var augX = graph.AugX;
        var augPortYSrc = graph.AugPortYSrc;
        var augPortYTgt = graph.AugPortYTgt;
        var augBendPoints = graph.AugBendPoints;
        var numAugEdges = augEdges.Count;
        var numOrigEdges = graph.Acyclic.Count;

        // Assemble per-original-edge waypoints from sub-edge bend points (ELK LongEdgeJoiner).
        var subEdgesByOrig = new List<int>[numOrigEdges];
        for (var ei = 0; ei < numOrigEdges; ei++)
        {
            subEdgesByOrig[ei] = [];
        }

        for (var ei = 0; ei < numAugEdges; ei++)
        {
            subEdgesByOrig[augEdges[ei].OrigEdgeIndex].Add(ei);
        }

        // Sub-edges must be in source-to-target layer order for concatenation.
        for (var ei = 0; ei < numOrigEdges; ei++)
        {
            subEdgesByOrig[ei].Sort((a, b) =>
                augNodes[augEdges[a].Source].Layer.CompareTo(augNodes[augEdges[b].Source].Layer));
        }

        var result = new IReadOnlyList<Point2D>[numOrigEdges];
        for (var origIdx = 0; origIdx < numOrigEdges; origIdx++)
        {
            var subEdges = subEdgesByOrig[origIdx];
            if (subEdges.Count == 0)
            {
                result[origIdx] = [];
                continue;
            }

            var firstSubEdge = augEdges[subEdges[0]];
            var lastSubEdge = augEdges[subEdges[^1]];

            var srcNodeIdx = firstSubEdge.Source;
            var tgtNodeIdx = lastSubEdge.Target;

            var srcRight = augX[srcNodeIdx] + augNodes[srcNodeIdx].Width;
            var tgtLeft = augX[tgtNodeIdx];
            var srcPortY = augPortYSrc[subEdges[0]];
            var tgtPortY = augPortYTgt[subEdges[^1]];

            var wps = new List<Point2D>
            {
                new(srcRight, srcPortY),
            };

            foreach (var subEdgeIdx in subEdges)
            {
                wps.AddRange(augBendPoints[subEdgeIdx]);
            }

            wps.Add(new Point2D(tgtLeft, tgtPortY));
            result[origIdx] = wps;
        }

        graph.Waypoints = result;
    }
}
