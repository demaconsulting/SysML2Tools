// <copyright file="OrthogonalRouterBackEdgeTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout;
using DemaConsulting.SysML2Tools.Layout.Engine;
using DemaConsulting.SysML2Tools.Layout.Engine.Layered;
using DemaConsulting.SysML2Tools.Rendering;

using static DemaConsulting.SysML2Tools.Layout.Engine.Layered.LayeredLayoutMetrics;

namespace DemaConsulting.SysML2Tools.Tests.Layout.Engine.Layered;

/// <summary>
///     Tests for the reversed (back) edge entry-approach clamp in <see cref="OrthogonalRouter"/>,
///     driven by the <see cref="LayeredGraph.BackEdgeEntryApproach"/> parameter. A reversed edge is
///     stored flipped, so the consumer draws the end marker on the augmented-source face of the first
///     sub-edge; the clamp guarantees that final straight approach is at least
///     <see cref="LayeredGraph.BackEdgeEntryApproach"/> long so the marker reads as a clean
///     perpendicular entry. The clamp uses <c>Math.Max</c>, so at the default approach (equal to
///     <see cref="LayeredLayoutMetrics.ConnectorClearance"/>) it is a provable no-op and forward and
///     acyclic geometry stay byte-identical to the original engine.
/// </summary>
public sealed class OrthogonalRouterBackEdgeTests
{
    /// <summary>
    ///     At the default approach the clamp is a no-op: every edge in a cyclic graph (forward and
    ///     reversed alike) is byte-for-byte identical to the true-original reference engine.
    /// </summary>
    [Fact]
    public void OrthogonalRouter_DefaultApproach_IsByteIdenticalToLegacy()
    {
        var nodes = new List<LayerNode>
        {
            new(120, 50), new(150, 60), new(130, 40), new(140, 70), new(110, 55),
        };
        var edges = new List<LayerEdge>
        {
            new(0, 1), new(1, 2), new(2, 3), new(3, 4), new(4, 0),
        };

        // The product engine uses the default BackEdgeEntryApproach (== ConnectorClearance), so it
        // must reproduce the unmodified reference engine exactly, reversed edges included.
        var expected = LegacyInterconnectionLayoutEngineOracle.Place(nodes, edges).ConnectorWaypoints;
        var actual = InterconnectionLayoutEngine.Place(nodes, edges).ConnectorWaypoints;

        Assert.Equal(expected.Count, actual.Count);
        for (var e = 0; e < expected.Count; e++)
        {
            AssertWaypointsBitIdentical($"edge {e}", expected[e], actual[e]);
        }
    }

    /// <summary>
    ///     Raising <see cref="LayeredGraph.BackEdgeEntryApproach"/> pushes every reversed edge's first
    ///     (marker-side) straight segment outward to at least the requested approach — longer than the
    ///     default — so a longer end decoration always sits on a clean straight leg.
    /// </summary>
    [Fact]
    public void OrthogonalRouter_CustomApproach_PushesEntryStubOutward()
    {
        // Arrange: a five-node cycle forces a surviving reversed (long) edge.
        var nodes = new List<LayerNode>
        {
            new(120, 50), new(150, 60), new(130, 40), new(140, 70), new(110, 55),
        };
        var edges = new List<LayerEdge>
        {
            new(0, 1), new(1, 2), new(2, 3), new(3, 4), new(4, 0),
        };

        const double customApproach = 40.0;
        Assert.True(customApproach > ConnectorClearance, "Test requires a custom approach above the default.");

        // Act: route once at the default approach and once at the larger custom approach.
        var defaultGraph = BuildJoinedGraph(nodes, edges, ConnectorClearance);
        var customGraph = BuildJoinedGraph(nodes, edges, customApproach);

        // Assert: every reversed bent edge clears the custom threshold, and at least one such edge is
        // strictly farther out than at the default (proving the parameter actually moved geometry).
        var reversedBendCount = 0;
        var movedCount = 0;
        for (var e = 0; e < customGraph.Acyclic.Count; e++)
        {
            if (!customGraph.AcyclicReversed[e])
            {
                continue;
            }

            var customWaypoints = customGraph.Waypoints[e];
            var defaultWaypoints = defaultGraph.Waypoints[e];

            // A bent edge has interior waypoints; the first segment is the marker-side stub.
            if (customWaypoints.Count < 3)
            {
                continue;
            }

            reversedBendCount++;
            var customFirst = Math.Abs(customWaypoints[1].X - customWaypoints[0].X);
            Assert.True(
                customFirst >= customApproach,
                $"Reversed edge {e} entry approach {customFirst:R} < custom approach {customApproach:R}.");

            if (defaultWaypoints.Count >= 3)
            {
                var defaultFirst = Math.Abs(defaultWaypoints[1].X - defaultWaypoints[0].X);
                if (customFirst > defaultFirst)
                {
                    movedCount++;
                }
            }
        }

        Assert.True(reversedBendCount > 0, "Expected at least one reversed edge with a routing bend.");
        Assert.True(movedCount > 0, "Expected the custom approach to push at least one reversed stub outward.");
    }

    /// <summary>
    ///     At the default approach every reversed edge that bends still produces a first
    ///     (marker-side) straight segment at least <see cref="LayeredLayoutMetrics.ConnectorClearance"/>
    ///     long — the original engine's behavior, preserved.
    /// </summary>
    [Fact]
    public void OrthogonalRouter_ReversedEdge_DefaultApproachClearsClearance()
    {
        var nodes = new List<LayerNode>
        {
            new(120, 50), new(150, 60), new(130, 40), new(140, 70), new(110, 55),
        };
        var edges = new List<LayerEdge>
        {
            new(0, 1), new(1, 2), new(2, 3), new(3, 4), new(4, 0),
        };
        var graph = BuildJoinedGraph(nodes, edges, ConnectorClearance);

        var reversedBendCount = 0;
        for (var e = 0; e < graph.Acyclic.Count; e++)
        {
            if (!graph.AcyclicReversed[e])
            {
                continue;
            }

            var waypoints = graph.Waypoints[e];
            if (waypoints.Count < 3)
            {
                continue;
            }

            reversedBendCount++;
            var firstSegment = Math.Abs(waypoints[1].X - waypoints[0].X);
            Assert.True(
                firstSegment >= ConnectorClearance,
                $"Reversed edge {e} entry approach {firstSegment:R} < ConnectorClearance {ConnectorClearance:R}.");
        }

        Assert.True(reversedBendCount > 0, "Expected at least one reversed edge with a routing bend.");
    }

    /// <summary>
    ///     In a cyclic graph, every forward (non-reversed) edge's waypoints stay byte-for-byte
    ///     identical to the reference engine — the clamp never touches forward geometry.
    /// </summary>
    [Fact]
    public void OrthogonalRouter_ForwardEdges_GeometryUnchanged()
    {
        var nodes = new List<LayerNode>
        {
            new(120, 50), new(150, 60), new(130, 40), new(140, 70), new(110, 55),
        };
        var edges = new List<LayerEdge>
        {
            new(0, 1), new(1, 2), new(2, 3), new(3, 4), new(4, 0),
        };

        var reversed = ReversedFlags(nodes, edges);
        var expected = LegacyInterconnectionLayoutEngineOracle.Place(nodes, edges).ConnectorWaypoints;
        var actual = InterconnectionLayoutEngine.Place(nodes, edges).ConnectorWaypoints;

        Assert.Equal(expected.Count, actual.Count);
        var forwardChecked = 0;
        for (var e = 0; e < reversed.Length; e++)
        {
            if (reversed[e])
            {
                continue;
            }

            forwardChecked++;
            AssertWaypointsBitIdentical($"forward edge {e}", expected[e], actual[e]);
        }

        Assert.True(forwardChecked > 0, "Expected at least one forward edge in the cyclic graph.");
    }

    /// <summary>
    ///     An acyclic graph has no reversed edges, so the clamp is a no-op: the reversed flags are
    ///     all false and every waypoint is byte-for-byte identical to the reference engine.
    /// </summary>
    [Fact]
    public void OrthogonalRouter_AcyclicGraph_NoApproachChange()
    {
        var nodes = new List<LayerNode>
        {
            new(120, 50), new(150, 60), new(130, 40), new(140, 70),
        };
        var edges = new List<LayerEdge>
        {
            new(0, 1), new(0, 2), new(1, 3), new(2, 3), new(0, 3),
        };

        var reversed = ReversedFlags(nodes, edges);
        Assert.All(reversed, flag => Assert.False(flag));

        var expected = LegacyInterconnectionLayoutEngineOracle.Place(nodes, edges).ConnectorWaypoints;
        var actual = InterconnectionLayoutEngine.Place(nodes, edges).ConnectorWaypoints;

        Assert.Equal(expected.Count, actual.Count);
        for (var e = 0; e < expected.Count; e++)
        {
            AssertWaypointsBitIdentical($"acyclic edge {e}", expected[e], actual[e]);
        }
    }

    /// <summary>
    ///     Decoration-aware approach: when the back-edge entry approach is derived exactly as the
    ///     state-transition view derives it — the longest end-marker along-line length plus the corner
    ///     radius plus the clean-leg margin — every reversed bent edge presents a final straight leg at
    ///     least as long as the open-chevron decoration, so the rounded corner never intrudes the marker.
    /// </summary>
    [Fact]
    public void OrthogonalRouter_DecorationAwareApproach_ClearsMarkerAlongLength()
    {
        var nodes = new List<LayerNode>
        {
            new(120, 50), new(150, 60), new(130, 40), new(140, 70), new(110, 55),
        };
        var edges = new List<LayerEdge>
        {
            new(0, 1), new(1, 2), new(2, 3), new(3, 4), new(4, 0),
        };

        // Derive the approach exactly as StateTransitionViewLayoutStrategy does for its end markers.
        var theme = Themes.Light;
        var markerAlongLength = NotationMetrics.AlongLineLength(EndMarkerStyle.OpenChevron);
        var approach = markerAlongLength + theme.LineCornerRadius + theme.CleanLegMargin;

        var graph = BuildJoinedGraph(nodes, edges, approach);

        var reversedBendCount = 0;
        for (var e = 0; e < graph.Acyclic.Count; e++)
        {
            if (!graph.AcyclicReversed[e])
            {
                continue;
            }

            var waypoints = graph.Waypoints[e];
            if (waypoints.Count < 3)
            {
                continue;
            }

            reversedBendCount++;
            var firstSegment = Math.Abs(waypoints[1].X - waypoints[0].X);
            Assert.True(
                firstSegment >= markerAlongLength,
                $"Reversed edge {e} clean leg {firstSegment:R} < marker along length {markerAlongLength:R}.");
        }

        Assert.True(reversedBendCount > 0, "Expected at least one reversed edge with a routing bend.");
    }

    /// <summary>
    ///     Runs the pipeline stages up to and including long-edge joining at the requested back-edge
    ///     entry approach and returns the graph.
    /// </summary>
    private static LayeredGraph BuildJoinedGraph(List<LayerNode> nodes, List<LayerEdge> edges, double approach)
    {
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right)
        {
            BackEdgeEntryApproach = approach,
        };
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

    /// <summary>Computes the reversed-edge flags (parallel to the acyclic edge set) for an input.</summary>
    private static bool[] ReversedFlags(List<LayerNode> nodes, List<LayerEdge> edges)
    {
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);
        new CycleBreaker().Apply(graph);
        return graph.AcyclicReversed;
    }

    /// <summary>Asserts two waypoint polylines are bit-for-bit identical (no tolerance).</summary>
    private static void AssertWaypointsBitIdentical(
        string context,
        IReadOnlyList<Point2D> expected,
        IReadOnlyList<Point2D> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var w = 0; w < expected.Count; w++)
        {
            Assert.True(
                BitConverter.DoubleToInt64Bits(expected[w].X) == BitConverter.DoubleToInt64Bits(actual[w].X)
                && BitConverter.DoubleToInt64Bits(expected[w].Y) == BitConverter.DoubleToInt64Bits(actual[w].Y),
                $"{context}: waypoint {w} differs (expected ({expected[w].X:R},{expected[w].Y:R}), actual ({actual[w].X:R},{actual[w].Y:R})).");
        }
    }
}
