// <copyright file="LayeredPlacerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Engine;

namespace DemaConsulting.SysML2Tools.Tests.Layout.Engine;

/// <summary>
///     Tests for <see cref="LayeredPlacer"/> BFS layering, barycentric ordering, corridor scaling,
///     and non-overlapping rectangle guarantees.
/// </summary>
public sealed class LayeredPlacerTests
{
    /// <summary>
    ///     A linear chain A→B→C produces three distinct layer indices: the highest-degree node (B,
    ///     degree 2) lands in layer 0, and its two neighbours (A and C) land in layer 1.
    /// </summary>
    [Fact]
    public void LayeredPlacer_Place_LinearChain_ThreeLayers()
    {
        // Arrange: three equal-sized nodes connected in a chain A(0)→B(1)→C(2)
        var nodes = new List<LayerNode>
        {
            new(60, 40),  // A — index 0, degree 1
            new(60, 40),  // B — index 1, degree 2 (highest)
            new(60, 40),  // C — index 2, degree 1
        };
        var edges = new List<LayerEdge>
        {
            new(0, 1),  // A → B
            new(1, 2),  // B → C
        };

        // Act
        var result = LayeredPlacer.Place(nodes, edges);

        // Assert: B (highest degree) is the seed so it gets layer 0; A and C each get layer 1
        Assert.Equal(0, result.NodeLayers[1]);  // B at layer 0 (seed)
        Assert.NotEqual(result.NodeLayers[1], result.NodeLayers[0]);  // A in a different layer from B
        Assert.NotEqual(result.NodeLayers[1], result.NodeLayers[2]);  // C in a different layer from B

        // There should be exactly two distinct layer values (layer 0 and layer 1)
        var distinctLayers = result.NodeLayers.Distinct().OrderBy(l => l).ToList();
        Assert.Equal(2, distinctLayers.Count);
    }

    /// <summary>
    ///     A star topology with one high-degree centre and four degree-1 spokes places the centre
    ///     in layer 0 and all spokes in layer 1.
    /// </summary>
    [Fact]
    public void LayeredPlacer_Place_StarTopology_CenterInLayer0_SpokesInLayer1()
    {
        // Arrange: centre node (index 0, degree 4) connected to four spokes (indices 1–4)
        var nodes = new List<LayerNode>
        {
            new(80, 50),  // centre — degree 4
            new(60, 40),  // spoke 1 — degree 1
            new(60, 40),  // spoke 2 — degree 1
            new(60, 40),  // spoke 3 — degree 1
            new(60, 40),  // spoke 4 — degree 1
        };
        var edges = new List<LayerEdge>
        {
            new(0, 1),
            new(0, 2),
            new(0, 3),
            new(0, 4),
        };

        // Act
        var result = LayeredPlacer.Place(nodes, edges);

        // Assert: centre is in layer 0 (highest degree = seed)
        Assert.Equal(0, result.NodeLayers[0]);

        // All spokes are one BFS hop away, so each must be in layer 1
        Assert.Equal(1, result.NodeLayers[1]);
        Assert.Equal(1, result.NodeLayers[2]);
        Assert.Equal(1, result.NodeLayers[3]);
        Assert.Equal(1, result.NodeLayers[4]);
    }

    /// <summary>
    ///     When there are no edges every node is treated as isolated and all receive layer 0;
    ///     they are stacked vertically in a single column.
    /// </summary>
    [Fact]
    public void LayeredPlacer_Place_NoEdges_AllInLayer0()
    {
        // Arrange: three nodes with no edges between them
        var nodes = new List<LayerNode>
        {
            new(60, 40),
            new(60, 40),
            new(60, 40),
        };

        // Act
        var result = LayeredPlacer.Place(nodes, []);

        // Assert: all nodes are in layer 0 (no BFS propagation possible)
        Assert.All(result.NodeLayers, l => Assert.Equal(0, l));

        // Nodes are placed in a single vertical column so all share the same X coordinate
        var uniqueX = result.Rects.Select(r => r.X).Distinct().Count();
        Assert.Equal(1, uniqueX);
    }

    /// <summary>
    ///     When eight edges cross the same corridor the corridor width exceeds the minimum; an
    ///     arrangement with only one crossing edge produces a narrower corridor (at the minimum).
    ///     This confirms that <see cref="LayeredPlacer"/> scales corridors with edge density.
    /// </summary>
    [Fact]
    public void LayeredPlacer_Place_DenseCorridorEdges_CorridorWidthScales()
    {
        // Arrange — narrow case: hub (node 0) connected to a single spoke (node 1)
        // corridorWidth = max(60, 1 × 12 + 2 × 10) = 60 (at minimum)
        var nodesNarrow = new List<LayerNode> { new(60, 40), new(60, 40) };
        var edgesNarrow = new List<LayerEdge> { new(0, 1) };

        // Arrange — wide case: hub (node 0, degree 8) connected to eight spokes (nodes 1–8)
        // All eight edges cross the single corridor between layer 0 and layer 1.
        // corridorWidth = max(60, 8 × 12 + 2 × 10) = max(60, 116) = 116
        var nodesWide = Enumerable.Repeat(new LayerNode(60, 40), 9).ToList();
        var edgesWide = Enumerable.Range(1, 8)
            .Select(i => new LayerEdge(0, i))
            .ToList();

        // Act
        var narrow = LayeredPlacer.Place(nodesNarrow, edgesNarrow, minCorridorWidth: 60.0, edgeSpacing: 12.0);
        var wide = LayeredPlacer.Place(nodesWide, edgesWide, minCorridorWidth: 60.0, edgeSpacing: 12.0);

        // Assert: the wide layout's column span must exceed the narrow layout's column span by
        // more than 50 px, confirming that the wider corridor (116 vs 60 = 56 px difference)
        // is reflected in the placed geometry.
        var narrowColumnWidth = narrow.Rects.Max(r => r.X + r.Width) - narrow.Rects.Min(r => r.X);
        var wideColumnWidth = wide.Rects.Max(r => r.X + r.Width) - wide.Rects.Min(r => r.X);

        Assert.True(wideColumnWidth > narrowColumnWidth + 50.0,
            $"Expected wide corridor to add ≥50 px over narrow, but got: narrow={narrowColumnWidth:F1}, wide={wideColumnWidth:F1}");
    }

    /// <summary>
    ///     A six-node connected graph produces rectangles that do not overlap, confirming the
    ///     column-separation and vertical-stacking guarantees of <see cref="LayeredPlacer"/>.
    /// </summary>
    [Fact]
    public void LayeredPlacer_Place_AllRects_NonOverlapping()
    {
        // Arrange: six nodes with several cross-layer connections
        var nodes = new List<LayerNode>
        {
            new(80, 50),   // 0
            new(100, 40),  // 1
            new(60, 60),   // 2
            new(90, 45),   // 3
            new(70, 50),   // 4
            new(80, 40),   // 5
        };
        var edges = new List<LayerEdge>
        {
            new(0, 1),
            new(0, 2),
            new(1, 3),
            new(2, 3),
            new(3, 4),
            new(3, 5),
        };

        // Act
        var result = LayeredPlacer.Place(nodes, edges, nodeSpacing: 20.0);

        // Assert: every pair of rectangles is strictly non-overlapping
        var rects = result.Rects;
        for (var i = 0; i < rects.Count; i++)
        {
            for (var j = i + 1; j < rects.Count; j++)
            {
                Assert.False(Overlaps(rects[i], rects[j]), $"Rects {i} and {j} overlap: {rects[i]} / {rects[j]}");
            }
        }
    }

    /// <summary>Returns <see langword="true"/> when the two rectangles strictly overlap.</summary>
    private static bool Overlaps(Rect a, Rect b) =>
        a.X < b.X + b.Width &&
        b.X < a.X + a.Width &&
        a.Y < b.Y + b.Height &&
        b.Y < a.Y + a.Height;
}
