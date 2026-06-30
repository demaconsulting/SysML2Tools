// <copyright file="BrandesKopfPlacerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Engine;
using DemaConsulting.SysML2Tools.Layout.Engine.Layered;

namespace DemaConsulting.SysML2Tools.Tests.Layout.Engine.Layered;

/// <summary>
///     Tests for <see cref="BrandesKopfPlacer"/> covering assignment of finite coordinate arrays
///     and left-to-right column placement in layer order.
/// </summary>
public sealed class BrandesKopfPlacerTests
{
    /// <summary>
    ///     A chain (0-&gt;1-&gt;2) receives a finite X and Y for every augmented node and per-column
    ///     arrays sized to the number of layers.
    /// </summary>
    [Fact]
    public void BrandesKopfPlacer_Apply_ChainGraph_AssignsCoordinateArrays()
    {
        // Arrange / Act: place a three-node chain.
        var graph = BuildPlacedGraph(
            new List<LayerNode> { new(60, 40), new(60, 40), new(60, 40) },
            new List<LayerEdge> { new(0, 1), new(1, 2) });

        // Assert: coordinate arrays are sized correctly and finite.
        Assert.Equal(graph.AugNodes.Count, graph.AugX.Length);
        Assert.Equal(graph.AugNodes.Count, graph.AugY.Length);
        Assert.Equal(graph.Groups.Count, graph.ColumnX.Length);
        Assert.Equal(graph.Groups.Count, graph.MaxColWidth.Length);
        Assert.All(graph.AugX, x => Assert.True(double.IsFinite(x)));
        Assert.All(graph.AugY, y => Assert.True(double.IsFinite(y)));
    }

    /// <summary>
    ///     Layer columns are placed left to right, so each column's left edge is strictly greater
    ///     than the previous column's left edge.
    /// </summary>
    [Fact]
    public void BrandesKopfPlacer_Apply_ColumnsAreLeftToRightInLayerOrder()
    {
        // Arrange / Act: place a three-node chain (three layers).
        var graph = BuildPlacedGraph(
            new List<LayerNode> { new(60, 40), new(60, 40), new(60, 40) },
            new List<LayerEdge> { new(0, 1), new(1, 2) });

        // Assert: column left edges strictly increase.
        for (var l = 1; l < graph.ColumnX.Length; l++)
        {
            Assert.True(
                graph.ColumnX[l] > graph.ColumnX[l - 1],
                $"Column {l} left edge {graph.ColumnX[l]} is not right of column {l - 1} ({graph.ColumnX[l - 1]}).");
        }
    }

    /// <summary>Runs the stages up to and including placement and returns the graph.</summary>
    /// <param name="nodes">Input nodes.</param>
    /// <param name="edges">Input edges.</param>
    /// <returns>The graph after the Brandes-Köpf placement stage.</returns>
    private static LayeredGraph BuildPlacedGraph(List<LayerNode> nodes, List<LayerEdge> edges)
    {
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);
        new CycleBreaker().Apply(graph);
        new LayerAssigner().Apply(graph);
        new LongEdgeSplitter().Apply(graph);
        new CrossingMinimizer().Apply(graph);
        new BrandesKopfPlacer().Apply(graph);
        return graph;
    }
}
