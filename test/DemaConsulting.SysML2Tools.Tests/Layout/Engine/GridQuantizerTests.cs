// <copyright file="GridQuantizerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Engine;

namespace DemaConsulting.SysML2Tools.Tests.Layout.Engine;

/// <summary>
///     Tests for <see cref="GridQuantizer"/> grid snapping and column/row unification.
/// </summary>
public sealed class GridQuantizerTests
{
    /// <summary>Positions snap to the nearest grid multiple.</summary>
    [Fact]
    public void Quantize_SnapsToNearestMultiple()
    {
        var boxes = new[] { new QuantizeBox(23, 47, 100, 40) };

        var rects = GridQuantizer.Quantize(boxes, gridUnit: 10, clusterTolerance: 4);

        Assert.Equal(20, rects[0].X, 6);
        Assert.Equal(50, rects[0].Y, 6);
    }

    /// <summary>Similar widths in a column are unified to the wider value.</summary>
    [Fact]
    public void Quantize_SimilarWidths_UnifiedToWider()
    {
        var boxes = new[]
        {
            new QuantizeBox(0, 0, 100, 40),
            new QuantizeBox(2, 200, 120, 40),
        };

        var rects = GridQuantizer.Quantize(boxes, gridUnit: 10, clusterTolerance: 8);

        Assert.Equal(rects[0].Width, rects[1].Width, 6);
        Assert.Equal(120, rects[0].Width, 6);
    }

    /// <summary>Boxes in different columns keep their own widths.</summary>
    [Fact]
    public void Quantize_DifferentColumns_NotUnified()
    {
        var boxes = new[]
        {
            new QuantizeBox(0, 0, 100, 40),
            new QuantizeBox(500, 0, 120, 40),
        };

        var rects = GridQuantizer.Quantize(boxes, gridUnit: 10, clusterTolerance: 8);

        Assert.Equal(100, rects[0].Width, 6);
        Assert.Equal(120, rects[1].Width, 6);
    }

    /// <summary>Quantisation is deterministic.</summary>
    [Fact]
    public void Quantize_SameInput_IsDeterministic()
    {
        var boxes = new[] { new QuantizeBox(23, 47, 103, 41), new QuantizeBox(28, 53, 99, 38) };

        var a = GridQuantizer.Quantize(boxes, gridUnit: 10, clusterTolerance: 8);
        var b = GridQuantizer.Quantize(boxes, gridUnit: 10, clusterTolerance: 8);

        for (var i = 0; i < a.Count; i++)
        {
            Assert.Equal(a[i].X, b[i].X, 9);
            Assert.Equal(a[i].Width, b[i].Width, 9);
        }
    }
}
