// <copyright file="LayeredLayoutEngineTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Engine;

namespace DemaConsulting.SysML2Tools.Tests.Layout.Engine;

/// <summary>
///     Tests for <see cref="LayeredLayoutEngine"/> Sugiyama-style layered layout.
/// </summary>
public sealed class LayeredLayoutEngineTests
{
    /// <summary>An empty input yields a padding-only region.</summary>
    [Fact]
    public void Place_EmptyList_ReturnsPaddingOnlyRegion()
    {
        var result = LayeredLayoutEngine.Place([], [], layerGap: 40, nodeGap: 20, padding: 10);

        Assert.Empty(result.Rects);
        Assert.Equal(20.0, result.Width);
        Assert.Equal(20.0, result.Height);
    }

    /// <summary>A simple chain assigns each node to a strictly increasing layer.</summary>
    [Fact]
    public void Place_Chain_AssignsIncreasingLayers()
    {
        // Arrange: a -> b -> c -> d
        var nodes = Enumerable.Range(0, 4).Select(_ => new LayeredNode(60, 30)).ToList();
        var edges = new[] { new LayeredEdge(0, 1), new LayeredEdge(1, 2), new LayeredEdge(2, 3) };

        // Act
        var result = LayeredLayoutEngine.Place(nodes, edges, layerGap: 40, nodeGap: 20, padding: 10);

        // Assert: layers are 0,1,2,3 and Y increases with layer
        Assert.Equal([0, 1, 2, 3], result.Layers);
        Assert.True(result.Rects[0].Y < result.Rects[1].Y);
        Assert.True(result.Rects[1].Y < result.Rects[2].Y);
        Assert.True(result.Rects[2].Y < result.Rects[3].Y);
    }

    /// <summary>Each edge points from a lower layer to a higher layer (top-to-bottom flow).</summary>
    [Fact]
    public void Place_Branching_EdgesPointDownward()
    {
        // Arrange: start -> {a, b} -> join
        var nodes = Enumerable.Range(0, 4).Select(_ => new LayeredNode(60, 30)).ToList();
        var edges = new[]
        {
            new LayeredEdge(0, 1), new LayeredEdge(0, 2),
            new LayeredEdge(1, 3), new LayeredEdge(2, 3),
        };

        // Act
        var result = LayeredLayoutEngine.Place(nodes, edges, layerGap: 40, nodeGap: 20, padding: 10);

        // Assert: every edge has the source in a strictly smaller layer than the target
        foreach (var e in edges)
        {
            Assert.True(result.Layers[e.From] < result.Layers[e.To],
                $"Edge {e.From}->{e.To} does not point downward.");
        }
    }

    /// <summary>Nodes sharing a layer do not overlap horizontally.</summary>
    [Fact]
    public void Place_SameLayerNodes_DoNotOverlap()
    {
        // Arrange: a hub fanning out to four nodes on the same layer
        var nodes = Enumerable.Range(0, 5).Select(_ => new LayeredNode(70, 30)).ToList();
        var edges = new[]
        {
            new LayeredEdge(0, 1), new LayeredEdge(0, 2),
            new LayeredEdge(0, 3), new LayeredEdge(0, 4),
        };

        // Act
        var result = LayeredLayoutEngine.Place(nodes, edges, layerGap: 40, nodeGap: 20, padding: 10);

        // Assert: the four layer-1 nodes do not overlap pairwise
        for (var i = 1; i <= 4; i++)
        {
            for (var j = i + 1; j <= 4; j++)
            {
                Assert.False(Overlaps(result.Rects[i], result.Rects[j]),
                    $"Nodes {i} and {j} overlap.");
            }
        }
    }

    /// <summary>A cycle is broken so layering terminates and produces a valid result.</summary>
    [Fact]
    public void Place_Cycle_TerminatesAndPlacesAllNodes()
    {
        // Arrange: a -> b -> c -> a (a cycle)
        var nodes = Enumerable.Range(0, 3).Select(_ => new LayeredNode(60, 30)).ToList();
        var edges = new[] { new LayeredEdge(0, 1), new LayeredEdge(1, 2), new LayeredEdge(2, 0) };

        // Act
        var result = LayeredLayoutEngine.Place(nodes, edges, layerGap: 40, nodeGap: 20, padding: 10);

        // Assert: all nodes placed within bounds
        Assert.Equal(3, result.Rects.Count);
        foreach (var r in result.Rects)
        {
            Assert.True(r.X + r.Width <= result.Width + 1e-6);
            Assert.True(r.Y + r.Height <= result.Height + 1e-6);
        }
    }

    /// <summary>Determines whether two rectangles overlap with a positive-area intersection.</summary>
    private static bool Overlaps(PackedRect a, PackedRect b) =>
        a.X < b.X + b.Width &&
        b.X < a.X + a.Width &&
        a.Y < b.Y + b.Height &&
        b.Y < a.Y + a.Height;
}
