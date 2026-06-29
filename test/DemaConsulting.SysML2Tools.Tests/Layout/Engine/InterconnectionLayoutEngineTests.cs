// <copyright file="InterconnectionLayoutEngineTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Engine;

namespace DemaConsulting.SysML2Tools.Tests.Layout.Engine;

/// <summary>
///     Tests for <see cref="InterconnectionLayoutEngine"/> covering longest-path layering,
///     dummy-node transparency, non-overlapping placement, and connector waypoints.
/// </summary>
public sealed class InterconnectionLayoutEngineTests
{
    /// <summary>
    ///     A simple directed chain A(0)→B(1)→C(2) produces three monotonically increasing
    ///     layer indices: 0, 1, 2. Longest-path layering assigns each node to the length
    ///     of the longest path from any source.
    /// </summary>
    [Fact]
    public void Place_LinearChain_MonotonicLayerAssignment()
    {
        var nodes = new List<LayerNode> { new(60, 40), new(60, 40), new(60, 40) };
        var edges = new List<LayerEdge> { new(0, 1), new(1, 2) };

        var result = InterconnectionLayoutEngine.Place(nodes, edges);

        Assert.Equal(0, result.NodeLayers[0]);
        Assert.Equal(1, result.NodeLayers[1]);
        Assert.Equal(2, result.NodeLayers[2]);
    }

    /// <summary>
    ///     A span-1 edge produces exactly four waypoints forming a Z-path:
    ///     source-right-port → slot → slot → target-left-port.
    /// </summary>
    [Fact]
    public void Place_SingleEdge_ProducesFourWaypointZPath()
    {
        var nodes = new List<LayerNode> { new(60, 40), new(60, 40) };
        var edges = new List<LayerEdge> { new(0, 1) };

        var result = InterconnectionLayoutEngine.Place(nodes, edges);

        Assert.Single(result.ConnectorWaypoints);
        Assert.Equal(4, result.ConnectorWaypoints[0].Count);
    }

    /// <summary>
    ///     Dummy nodes inserted for long edges do not appear in <see cref="LayerResult.Rects"/>:
    ///     the rect count always equals the number of input nodes.
    ///     A diamond topology with an extra 0→3 edge makes it span two corridors and requires
    ///     a dummy node at layer 1.
    /// </summary>
    [Fact]
    public void Place_LongEdge_RectCountEqualsInputNodeCount()
    {
        // 0→1→3 and 0→2→3 place: 0 at layer 0, 1/2 at layer 1, 3 at layer 2.
        // Adding 0→3 creates a span-2 edge requiring a dummy at layer 1.
        var nodes = new List<LayerNode>
        {
            new(80, 50),
            new(80, 50),
            new(80, 50),
            new(80, 50),
        };
        var edges = new List<LayerEdge>
        {
            new(0, 1),
            new(0, 2),
            new(1, 3),
            new(2, 3),
            new(0, 3),  // span-2: dummy inserted at layer 1
        };

        var result = InterconnectionLayoutEngine.Place(nodes, edges);

        Assert.Equal(4, result.Rects.Count);
        Assert.Equal(5, result.ConnectorWaypoints.Count);
    }

    /// <summary>
    ///     A long edge (span > 1) produces more than four waypoints because its path is
    ///     stitched through dummy-node positions in each intermediate corridor.
    /// </summary>
    [Fact]
    public void Place_LongEdge_WaypointsExceedFour()
    {
        // chain 0→1→2→3 sets layers 0,1,2,3; adding 0→3 spans 3 layers.
        var nodes = Enumerable.Repeat(new LayerNode(80, 50), 4).ToList();
        var edges = new List<LayerEdge>
        {
            new(0, 1),
            new(1, 2),
            new(2, 3),
            new(0, 3),  // span-3: two dummies, three corridor segments
        };

        var result = InterconnectionLayoutEngine.Place(nodes, edges);

        var longEdgeWp = result.ConnectorWaypoints[3];
        Assert.True(longEdgeWp.Count > 4, $"Expected >4 waypoints for long edge, got {longEdgeWp.Count}.");
    }

    /// <summary>
    ///     The Workstation topology (7 parts, 8 connections) produces the exact layer
    ///     assignments that longest-path layering guarantees and all seven rects are
    ///     non-overlapping.
    /// </summary>
    [Fact]
    public void Place_WorkstationTopology_CorrectLayersAndNoOverlap()
    {
        // Part order matches SysML file: cpu=0 memory=1 graphics=2 storage=3 psu=4 network=5 board=6
        var nodes = Enumerable.Repeat(new LayerNode(130, 60), 7).ToList();
        var edges = new List<LayerEdge>
        {
            new(6, 0),  // board → cpu
            new(6, 1),  // board → memory
            new(6, 2),  // board → graphics
            new(6, 3),  // board → storage
            new(6, 5),  // board → network
            new(4, 6),  // psu   → board
            new(4, 2),  // psu   → graphics
            new(0, 1),  // cpu   → memory
        };

        var result = InterconnectionLayoutEngine.Place(nodes, edges);

        // Longest-path layers.
        Assert.Equal(0, result.NodeLayers[4]);  // psu:     no incoming
        Assert.Equal(1, result.NodeLayers[6]);  // board:   psu → board
        Assert.Equal(2, result.NodeLayers[0]);  // cpu:     board → cpu
        Assert.Equal(2, result.NodeLayers[2]);  // graphics: max(psu→2, board→2) = 2
        Assert.Equal(2, result.NodeLayers[3]);  // storage: board → storage
        Assert.Equal(2, result.NodeLayers[5]);  // network: board → network
        Assert.Equal(3, result.NodeLayers[1]);  // memory:  max(board→2+1, cpu→2+1) = 3

        // Output counts.
        Assert.Equal(7, result.Rects.Count);
        Assert.Equal(8, result.ConnectorWaypoints.Count);

        // No two rects overlap.
        for (var i = 0; i < result.Rects.Count; i++)
        {
            for (var j = i + 1; j < result.Rects.Count; j++)
            {
                Assert.False(
                    Overlaps(result.Rects[i], result.Rects[j]),
                    $"Rects {i} and {j} overlap: {result.Rects[i]} / {result.Rects[j]}");
            }
        }
    }

    private static bool Overlaps(Rect a, Rect b) =>
        a.X < b.X + b.Width &&
        b.X < a.X + a.Width &&
        a.Y < b.Y + b.Height &&
        b.Y < a.Y + a.Height;
}
