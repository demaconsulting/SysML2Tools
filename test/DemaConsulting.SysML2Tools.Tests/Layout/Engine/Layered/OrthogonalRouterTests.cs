// <copyright file="OrthogonalRouterTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Engine;
using DemaConsulting.SysML2Tools.Layout.Engine.Layered;

namespace DemaConsulting.SysML2Tools.Tests.Layout.Engine.Layered;

/// <summary>
///     Tests for <see cref="OrthogonalRouter"/> covering that an aligned edge produces no bend
///     points and that every bend list is empty or a two-point vertical segment.
/// </summary>
public sealed class OrthogonalRouterTests
{
    /// <summary>
    ///     A single edge between two equal-height nodes is routed straight, so it produces no bend
    ///     points (its ports are aligned by the placer).
    /// </summary>
    [Fact]
    public void OrthogonalRouter_Apply_StraightEdge_ProducesNoBendPoints()
    {
        // Arrange / Act: route a single 0->1 edge.
        var graph = BuildRoutedGraph(
            new List<LayerNode> { new(60, 40), new(60, 40) },
            new List<LayerEdge> { new(0, 1) });

        // Assert: the single sub-edge has no bend points.
        Assert.Single(graph.AugBendPoints);
        Assert.Empty(graph.AugBendPoints[0]);
    }

    /// <summary>
    ///     Every sub-edge's bend list is either empty (a straight run) or exactly two points that
    ///     share an X coordinate (a vertical routing segment).
    /// </summary>
    [Fact]
    public void OrthogonalRouter_Apply_EveryBendListIsEmptyOrVerticalSegment()
    {
        // Arrange / Act: route a four-node diamond.
        var graph = BuildRoutedGraph(
            new List<LayerNode> { new(60, 40), new(60, 40), new(60, 40), new(60, 40) },
            new List<LayerEdge> { new(0, 1), new(0, 2), new(1, 3), new(2, 3) });

        // Assert: each bend list is empty or a vertical two-point segment.
        Assert.All(graph.AugBendPoints, bend =>
            Assert.True(
                bend.Count == 0 || (bend.Count == 2 && Math.Abs(bend[0].X - bend[1].X) < 1e-9),
                $"Unexpected bend geometry with {bend.Count} points."));
    }

    /// <summary>Runs the stages up to and including orthogonal routing and returns the graph.</summary>
    /// <param name="nodes">Input nodes.</param>
    /// <param name="edges">Input edges.</param>
    /// <returns>The graph after the orthogonal-routing stage.</returns>
    private static LayeredGraph BuildRoutedGraph(List<LayerNode> nodes, List<LayerEdge> edges)
    {
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);
        new CycleBreaker().Apply(graph);
        new LayerAssigner().Apply(graph);
        new LongEdgeSplitter().Apply(graph);
        new CrossingMinimizer().Apply(graph);
        new BrandesKopfPlacer().Apply(graph);
        new PortDistributor().Apply(graph);
        new OrthogonalRouter().Apply(graph);
        return graph;
    }
}
