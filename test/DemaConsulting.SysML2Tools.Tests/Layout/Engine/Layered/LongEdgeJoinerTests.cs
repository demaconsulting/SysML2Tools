// <copyright file="LongEdgeJoinerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Engine;
using DemaConsulting.SysML2Tools.Layout.Engine.Layered;

namespace DemaConsulting.SysML2Tools.Tests.Layout.Engine.Layered;

/// <summary>
///     Tests for <see cref="LongEdgeJoiner"/> covering production of one polyline per original edge
///     and concatenation of a long edge's sub-edge bend points.
/// </summary>
public sealed class LongEdgeJoinerTests
{
    /// <summary>
    ///     A single short edge yields one waypoint polyline of exactly two points (its source and
    ///     target ports, with no bends).
    /// </summary>
    [Fact]
    public void LongEdgeJoiner_Apply_SingleEdge_ProducesWaypointsPerOriginalEdge()
    {
        // Arrange / Act: join a single 0->1 edge.
        var graph = BuildJoinedGraph(
            new List<LayerNode> { new(60, 40), new(60, 40) },
            new List<LayerEdge> { new(0, 1) });

        // Assert: one polyline of two points.
        Assert.Single(graph.Waypoints);
        Assert.Equal(2, graph.Waypoints[0].Count);
    }

    /// <summary>
    ///     A span-three edge's polyline begins at the source's right face and ends at the target's
    ///     left face, and its point count equals its sub-edges' bend points plus the two endpoints.
    /// </summary>
    [Fact]
    public void LongEdgeJoiner_Apply_LongEdge_ConcatenatesSubEdgeBendPoints()
    {
        // Arrange: a chain plus a span-three edge 0->3 (original edge index 3).
        var nodes = new List<LayerNode> { new(60, 40), new(60, 40), new(60, 40), new(60, 40) };
        var graph = BuildJoinedGraph(nodes, new List<LayerEdge> { new(0, 1), new(1, 2), new(2, 3), new(0, 3) });

        const int origIdx = 3;
        var polyline = graph.Waypoints[origIdx];

        // Sub-edge bend points that make up the long edge.
        var bendTotal = 0;
        for (var ei = 0; ei < graph.AugEdges.Count; ei++)
        {
            if (graph.AugEdges[ei].OrigEdgeIndex == origIdx)
            {
                bendTotal += graph.AugBendPoints[ei].Count;
            }
        }

        // Assert: one polyline per original edge, anchored to the boxes, count = endpoints + bends.
        Assert.Equal(4, graph.Waypoints.Count);
        Assert.Equal(graph.AugX[0] + nodes[0].Width, polyline[0].X);
        Assert.Equal(graph.AugX[3], polyline[^1].X);
        Assert.Equal(bendTotal + 2, polyline.Count);
    }

    /// <summary>Runs the stages up to and including long-edge joining and returns the graph.</summary>
    /// <param name="nodes">Input nodes.</param>
    /// <param name="edges">Input edges.</param>
    /// <returns>The graph after the long-edge-joining stage.</returns>
    private static LayeredGraph BuildJoinedGraph(List<LayerNode> nodes, List<LayerEdge> edges)
    {
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);
        new CycleBreaker().Apply(graph);
        new LayerAssigner().Apply(graph);
        new LongEdgeSplitter().Apply(graph);
        new CrossingMinimizer().Apply(graph);
        new BrandesKopfPlacer().Apply(graph);
        new PortDistributor().Apply(graph);
        new OrthogonalRouter().Apply(graph);
        new LongEdgeJoiner().Apply(graph);
        return graph;
    }
}
