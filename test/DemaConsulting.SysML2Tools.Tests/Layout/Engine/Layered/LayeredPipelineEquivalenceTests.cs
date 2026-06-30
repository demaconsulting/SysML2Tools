// <copyright file="LayeredPipelineEquivalenceTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout;
using DemaConsulting.SysML2Tools.Layout.Engine;

namespace DemaConsulting.SysML2Tools.Tests.Layout.Engine.Layered;

/// <summary>
///     Behavior-preservation gate for the layered-pipeline refactor. Feeds many graphs through
///     both the legacy monolithic engine (<see cref="LegacyInterconnectionLayoutEngineOracle"/>)
///     and the refactored <see cref="InterconnectionLayoutEngine"/> façade and asserts that every
///     field of the resulting <c>LayerResult</c> is bit-for-bit identical: rectangles, total
///     dimensions, node layers, and every connector waypoint. No numeric tolerance is allowed.
/// </summary>
public sealed class LayeredPipelineEquivalenceTests
{
    /// <summary>
    ///     The pipeline reproduces the legacy engine exactly across two thousand pseudo-randomly
    ///     generated graphs spanning empty, disconnected, cyclic, parallel-edge, self-loop, and
    ///     long-edge topologies with varied node sizes.
    /// </summary>
    [Fact]
    public void Pipeline_MatchesLegacyOracle_OnRandomGraphs()
    {
        for (var seed = 0; seed < 2000; seed++)
        {
            var (nodes, edges) = BuildRandomGraph(seed);
            AssertEquivalent($"random seed {seed}", nodes, edges);
        }
    }

    /// <summary>An empty graph produces identical (degenerate) results from both engines.</summary>
    [Fact]
    public void Pipeline_MatchesLegacyOracle_OnEmptyGraph()
    {
        AssertEquivalent("empty", [], []);
    }

    /// <summary>
    ///     A drone-interconnect-style graph (seven heterogeneously sized parts with a mix of
    ///     short and long edges) produces identical results from both engines.
    /// </summary>
    [Fact]
    public void Pipeline_MatchesLegacyOracle_OnDroneLikeGraph()
    {
        var nodes = new List<LayerNode>
        {
            new(150, 54), // airframe
            new(150, 54), // battery
            new(230, 94), // controller
            new(150, 54), // gps
            new(150, 54), // imu
            new(153, 54), // motors
            new(209, 54), // propellers
        };
        var edges = new List<LayerEdge>
        {
            new(2, 0),
            new(2, 1),
            new(2, 3),
            new(2, 4),
            new(2, 5),
            new(5, 6),
            new(0, 6),
        };

        AssertEquivalent("drone-like", nodes, edges);
    }

    /// <summary>
    ///     A larger workstation-interconnect-style graph (twelve parts, multiple layers, a long
    ///     edge spanning three layers, and a back edge) produces identical results.
    /// </summary>
    [Fact]
    public void Pipeline_MatchesLegacyOracle_OnWorkstationLikeGraph()
    {
        var nodes = new List<LayerNode>();
        for (var i = 0; i < 12; i++)
        {
            nodes.Add(new LayerNode(120 + (i % 4 * 30), 50 + (i % 3 * 20)));
        }

        var edges = new List<LayerEdge>
        {
            new(0, 1),
            new(0, 2),
            new(1, 3),
            new(2, 3),
            new(3, 4),
            new(3, 5),
            new(4, 6),
            new(5, 7),
            new(6, 8),
            new(7, 8),
            new(0, 8), // long edge spanning several layers
            new(8, 0), // back edge (cycle)
            new(9, 3),
            new(10, 4),
            new(11, 5),
        };

        AssertEquivalent("workstation-like", nodes, edges);
    }

    /// <summary>
    ///     Canonical named topologies (chain, diamond, long edge, self loop, parallel edges,
    ///     disconnected components, and a tight cycle) each produce identical results.
    /// </summary>
    /// <param name="name">A human-readable name for the topology, used in failure messages.</param>
    [Theory]
    [InlineData("chain")]
    [InlineData("diamond")]
    [InlineData("longedge")]
    [InlineData("selfloop")]
    [InlineData("parallel")]
    [InlineData("disconnected")]
    [InlineData("cycle")]
    public void Pipeline_MatchesLegacyOracle_OnNamedTopologies(string name)
    {
        var (nodes, edges) = name switch
        {
            "chain" => (Sizes(3), Edges((0, 1), (1, 2))),
            "diamond" => (Sizes(4), Edges((0, 1), (0, 2), (1, 3), (2, 3))),
            "longedge" => (Sizes(4), Edges((0, 1), (1, 2), (2, 3), (0, 3))),
            "selfloop" => (Sizes(3), Edges((0, 0), (0, 1), (1, 2))),
            "parallel" => (Sizes(2), Edges((0, 1), (0, 1), (0, 1))),
            "disconnected" => (Sizes(6), Edges((0, 1), (2, 3), (4, 5))),
            "cycle" => (Sizes(3), Edges((0, 1), (1, 2), (2, 0))),
            _ => (Sizes(0), Edges()),
        };

        AssertEquivalent(name, nodes, edges);
    }

    /// <summary>Builds a list of uniformly sized nodes.</summary>
    private static List<LayerNode> Sizes(int count)
    {
        var nodes = new List<LayerNode>(count);
        for (var i = 0; i < count; i++)
        {
            nodes.Add(new LayerNode(60, 40));
        }

        return nodes;
    }

    /// <summary>Builds an edge list from source/target index pairs.</summary>
    private static List<LayerEdge> Edges(params (int Source, int Target)[] pairs)
    {
        var edges = new List<LayerEdge>(pairs.Length);
        foreach (var (s, t) in pairs)
        {
            edges.Add(new LayerEdge(s, t));
        }

        return edges;
    }

    /// <summary>
    ///     Deterministically builds a pseudo-random graph for the given seed, exercising varied
    ///     node counts and sizes plus arbitrary edges (including self loops, parallel edges,
    ///     cycles, and multi-layer spans).
    /// </summary>
    private static (List<LayerNode> Nodes, List<LayerEdge> Edges) BuildRandomGraph(int seed)
    {
        var rng = new Random(seed);
        var n = rng.Next(0, 16);

        var nodes = new List<LayerNode>(n);
        for (var i = 0; i < n; i++)
        {
            nodes.Add(new LayerNode(rng.Next(40, 240), rng.Next(30, 120)));
        }

        var edges = new List<LayerEdge>();
        if (n > 0)
        {
            var m = rng.Next(0, (n * 2) + 1);
            for (var e = 0; e < m; e++)
            {
                edges.Add(new LayerEdge(rng.Next(0, n), rng.Next(0, n)));
            }
        }

        return (nodes, edges);
    }

    /// <summary>
    ///     Runs both engines on the same input and asserts bit-for-bit equality of every field
    ///     of the resulting <c>LayerResult</c>.
    /// </summary>
    private static void AssertEquivalent(string context, List<LayerNode> nodes, List<LayerEdge> edges)
    {
        var expected = LegacyInterconnectionLayoutEngineOracle.Place(nodes, edges);
        var actual = InterconnectionLayoutEngine.Place(nodes, edges);

        AssertExact($"{context}: TotalWidth", expected.TotalWidth, actual.TotalWidth);
        AssertExact($"{context}: TotalHeight", expected.TotalHeight, actual.TotalHeight);

        Assert.Equal(expected.Rects.Count, actual.Rects.Count);
        for (var i = 0; i < expected.Rects.Count; i++)
        {
            AssertExact($"{context}: Rects[{i}].X", expected.Rects[i].X, actual.Rects[i].X);
            AssertExact($"{context}: Rects[{i}].Y", expected.Rects[i].Y, actual.Rects[i].Y);
            AssertExact($"{context}: Rects[{i}].Width", expected.Rects[i].Width, actual.Rects[i].Width);
            AssertExact($"{context}: Rects[{i}].Height", expected.Rects[i].Height, actual.Rects[i].Height);
        }

        Assert.Equal(expected.NodeLayers.Count, actual.NodeLayers.Count);
        for (var i = 0; i < expected.NodeLayers.Count; i++)
        {
            Assert.Equal(expected.NodeLayers[i], actual.NodeLayers[i]);
        }

        Assert.Equal(expected.ConnectorWaypoints.Count, actual.ConnectorWaypoints.Count);
        for (var e = 0; e < expected.ConnectorWaypoints.Count; e++)
        {
            var ew = expected.ConnectorWaypoints[e];
            var aw = actual.ConnectorWaypoints[e];
            Assert.Equal(ew.Count, aw.Count);
            for (var w = 0; w < ew.Count; w++)
            {
                AssertExact($"{context}: Waypoint[{e}][{w}].X", ew[w].X, aw[w].X);
                AssertExact($"{context}: Waypoint[{e}][{w}].Y", ew[w].Y, aw[w].Y);
            }
        }
    }

    /// <summary>Asserts that two doubles are identical at the bit level (no tolerance).</summary>
    private static void AssertExact(string context, double expected, double actual)
    {
        Assert.True(
            BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(actual),
            $"{context}: expected {expected:R} but got {actual:R}");
    }
}
