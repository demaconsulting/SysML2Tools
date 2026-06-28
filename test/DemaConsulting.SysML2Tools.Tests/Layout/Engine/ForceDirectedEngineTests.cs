// <copyright file="ForceDirectedEngineTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Engine;

namespace DemaConsulting.SysML2Tools.Tests.Layout.Engine;

/// <summary>
///     Tests for <see cref="ForceDirectedEngine"/> spring layout.
/// </summary>
public sealed class ForceDirectedEngineTests
{
    /// <summary>An empty input yields a padding-only region with no rectangles.</summary>
    [Fact]
    public void Place_EmptyList_ReturnsPaddingOnlyRegion()
    {
        var result = ForceDirectedEngine.Place([], [], spacing: 80, padding: 10);

        Assert.Empty(result.Rects);
        Assert.Equal(20.0, result.Width);
        Assert.Equal(20.0, result.Height);
    }

    /// <summary>A single node is placed at the padding origin.</summary>
    [Fact]
    public void Place_SingleNode_PositionsAtPadding()
    {
        var result = ForceDirectedEngine.Place([new ForceNode(40, 20)], [], spacing: 80, padding: 10);

        Assert.Single(result.Rects);
        Assert.Equal(10.0, result.Rects[0].X, 6);
        Assert.Equal(10.0, result.Rects[0].Y, 6);
    }

    /// <summary>After convergence no two node bounding boxes overlap.</summary>
    [Fact]
    public void Place_ConnectedGraph_ProducesNoOverlaps()
    {
        // Arrange: a small graph with a hub connected to several leaves
        var nodes = new[]
        {
            new ForceNode(60, 40), new ForceNode(60, 40), new ForceNode(60, 40),
            new ForceNode(60, 40), new ForceNode(60, 40), new ForceNode(60, 40),
        };
        var edges = new[]
        {
            new ForceEdge(0, 1), new ForceEdge(0, 2), new ForceEdge(0, 3),
            new ForceEdge(0, 4), new ForceEdge(0, 5), new ForceEdge(1, 2),
        };

        // Act
        var result = ForceDirectedEngine.Place(nodes, edges, spacing: 90, padding: 20);

        // Assert: every pair of rectangles is disjoint
        for (var i = 0; i < result.Rects.Count; i++)
        {
            for (var j = i + 1; j < result.Rects.Count; j++)
            {
                Assert.False(Overlaps(result.Rects[i], result.Rects[j]),
                    $"Rectangles {i} and {j} overlap.");
            }
        }
    }

    /// <summary>All placed rectangles lie within the reported region bounds.</summary>
    [Fact]
    public void Place_ConnectedGraph_AllRectsWithinBounds()
    {
        var nodes = new[]
        {
            new ForceNode(50, 30), new ForceNode(50, 30), new ForceNode(50, 30), new ForceNode(50, 30),
        };
        var edges = new[] { new ForceEdge(0, 1), new ForceEdge(1, 2), new ForceEdge(2, 3) };

        var result = ForceDirectedEngine.Place(nodes, edges, spacing: 80, padding: 15);

        foreach (var r in result.Rects)
        {
            Assert.True(r.X >= -1e-6);
            Assert.True(r.Y >= -1e-6);
            Assert.True(r.X + r.Width <= result.Width + 1e-6);
            Assert.True(r.Y + r.Height <= result.Height + 1e-6);
        }
    }

    /// <summary>The layout is deterministic: identical inputs yield identical outputs.</summary>
    [Fact]
    public void Place_SameInput_IsDeterministic()
    {
        var nodes = new[] { new ForceNode(50, 30), new ForceNode(50, 30), new ForceNode(50, 30) };
        var edges = new[] { new ForceEdge(0, 1), new ForceEdge(1, 2) };

        var a = ForceDirectedEngine.Place(nodes, edges, spacing: 80, padding: 10);
        var b = ForceDirectedEngine.Place(nodes, edges, spacing: 80, padding: 10);

        Assert.Equal(a.Width, b.Width, 9);
        Assert.Equal(a.Height, b.Height, 9);
        for (var i = 0; i < a.Rects.Count; i++)
        {
            Assert.Equal(a.Rects[i].X, b.Rects[i].X, 9);
            Assert.Equal(a.Rects[i].Y, b.Rects[i].Y, 9);
        }
    }

    /// <summary>Hierarchy gravity (kHier=1.0) produces more vertical spread than the flat case.</summary>
    [Fact]
    public void Place_HierarchyGravity_IncreasesVerticalSpread()
    {
        var nodes = Enumerable.Range(0, 5).Select(_ => new ForceNode(50, 30)).ToList();
        var edges = new[]
        {
            new ForceEdge(0, 1), new ForceEdge(1, 2), new ForceEdge(2, 3), new ForceEdge(3, 4),
        };
        var layers = new[] { 0, 1, 2, 3, 4 };

        var flat = ForceDirectedEngine.Place(nodes, edges, spacing: 90, padding: 10, kHier: 0.0, layerHints: layers);
        var hier = ForceDirectedEngine.Place(nodes, edges, spacing: 90, padding: 10, kHier: 1.0, layerHints: layers);

        Assert.True(hier.Height > flat.Height, $"flat={flat.Height} hier={hier.Height}");
    }

    /// <summary>The kinetic-energy overload remains deterministic and backward-compatible.</summary>
    [Fact]
    public void Place_KineticTermination_IsDeterministicAndCompatible()
    {
        var nodes = Enumerable.Range(0, 4).Select(_ => new ForceNode(50, 30)).ToList();
        var edges = new[] { new ForceEdge(0, 1), new ForceEdge(1, 2), new ForceEdge(2, 3) };

        var a = ForceDirectedEngine.Place(nodes, edges, spacing: 80, padding: 10);
        var b = ForceDirectedEngine.Place(nodes, edges, spacing: 80, padding: 10, kHier: 0.0, layerHints: null);

        Assert.Equal(a.Width, b.Width, 9);
        Assert.Equal(a.Height, b.Height, 9);
    }

    /// <summary>Determines whether two rectangles overlap with a positive-area intersection.</summary>
    private static bool Overlaps(PackedRect a, PackedRect b) =>
        a.X < b.X + b.Width &&
        b.X < a.X + a.Width &&
        a.Y < b.Y + b.Height &&
        b.Y < a.Y + a.Height;
}
