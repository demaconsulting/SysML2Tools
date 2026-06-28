// <copyright file="ContainmentPackerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Engine;

namespace DemaConsulting.SysML2Tools.Tests.Layout.Engine;

/// <summary>
///     Tests for <see cref="ContainmentPacker"/> shelf bin-packing.
/// </summary>
public sealed class ContainmentPackerTests
{
    /// <summary>
    ///     Packing an empty list returns a region consisting only of padding and no rectangles.
    /// </summary>
    [Fact]
    public void Pack_EmptyList_ReturnsPaddingOnlyRegion()
    {
        // Act: pack no items with padding 10
        var result = ContainmentPacker.Pack([], maxContentWidth: 100, horizontalGap: 5, verticalGap: 5, padding: 10);

        // Assert: region is 2*padding on each axis with no rectangles
        Assert.Empty(result.Rects);
        Assert.Equal(20.0, result.Width);
        Assert.Equal(20.0, result.Height);
    }

    /// <summary>
    ///     A single item is positioned at the padding origin and the region fits it exactly.
    /// </summary>
    [Fact]
    public void Pack_SingleItem_PositionsAtPaddingOrigin()
    {
        // Arrange: one 40x20 item
        var items = new[] { new PackItem(40, 20) };

        // Act
        var result = ContainmentPacker.Pack(items, maxContentWidth: 200, horizontalGap: 5, verticalGap: 5, padding: 10);

        // Assert: positioned at (10, 10); region = item + 2*padding
        Assert.Single(result.Rects);
        Assert.Equal(10.0, result.Rects[0].X);
        Assert.Equal(10.0, result.Rects[0].Y);
        Assert.Equal(60.0, result.Width);
        Assert.Equal(40.0, result.Height);
    }

    /// <summary>
    ///     Items that fit within the max content width are placed on a single row sharing a Y.
    /// </summary>
    [Fact]
    public void Pack_ItemsFitInRow_ShareSameRow()
    {
        // Arrange: three 30-wide items; max content width 200 fits all in one row
        var items = new[] { new PackItem(30, 20), new PackItem(30, 20), new PackItem(30, 20) };

        // Act
        var result = ContainmentPacker.Pack(items, maxContentWidth: 200, horizontalGap: 5, verticalGap: 5, padding: 10);

        // Assert: all three share the same top Y (single row)
        Assert.Equal(result.Rects[0].Y, result.Rects[1].Y);
        Assert.Equal(result.Rects[1].Y, result.Rects[2].Y);

        // And X positions increase left-to-right with the horizontal gap
        Assert.Equal(10.0, result.Rects[0].X);
        Assert.Equal(45.0, result.Rects[1].X);
        Assert.Equal(80.0, result.Rects[2].X);
    }

    /// <summary>
    ///     Items exceeding the max content width wrap to a new row positioned below the first.
    /// </summary>
    [Fact]
    public void Pack_ItemsExceedWidth_WrapToNewRow()
    {
        // Arrange: three 80-wide items; max content width 200 fits only two per row
        var items = new[] { new PackItem(80, 20), new PackItem(80, 20), new PackItem(80, 20) };

        // Act
        var result = ContainmentPacker.Pack(items, maxContentWidth: 200, horizontalGap: 5, verticalGap: 5, padding: 10);

        // Assert: first two on row 0, third wraps to row 1 with a greater Y
        Assert.Equal(result.Rects[0].Y, result.Rects[1].Y);
        Assert.True(result.Rects[2].Y > result.Rects[0].Y);

        // Third item starts a new row at the left padding origin
        Assert.Equal(10.0, result.Rects[2].X);
    }

    /// <summary>
    ///     For a mixed-size set, no two packed rectangles overlap.
    /// </summary>
    [Fact]
    public void Pack_MixedSizes_ProducesNoOverlaps()
    {
        // Arrange: a varied mix of sizes that forces multiple rows
        var items = new[]
        {
            new PackItem(60, 30), new PackItem(120, 20), new PackItem(40, 50),
            new PackItem(90, 25), new PackItem(70, 40), new PackItem(50, 30),
            new PackItem(110, 35), new PackItem(30, 20),
        };

        // Act
        var result = ContainmentPacker.Pack(items, maxContentWidth: 250, horizontalGap: 8, verticalGap: 8, padding: 12);

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

    /// <summary>
    ///     Every packed rectangle lies fully within the reported region bounds.
    /// </summary>
    [Fact]
    public void Pack_MixedSizes_AllRectsWithinBounds()
    {
        // Arrange: a varied mix of sizes
        var items = new[]
        {
            new PackItem(60, 30), new PackItem(120, 20), new PackItem(40, 50),
            new PackItem(90, 25), new PackItem(70, 40),
        };

        // Act
        var result = ContainmentPacker.Pack(items, maxContentWidth: 200, horizontalGap: 8, verticalGap: 8, padding: 12);

        // Assert: each rectangle is contained within [0, Width] x [0, Height]
        foreach (var r in result.Rects)
        {
            Assert.True(r.X >= 0);
            Assert.True(r.Y >= 0);
            Assert.True(r.X + r.Width <= result.Width + 1e-9);
            Assert.True(r.Y + r.Height <= result.Height + 1e-9);
        }
    }

    /// <summary>
    ///     An item wider than the content width is placed alone and the region widens to fit it.
    /// </summary>
    [Fact]
    public void Pack_ItemWiderThanContentWidth_PlacedAloneAndRegionWidens()
    {
        // Arrange: a 300-wide item with only 100 content width available
        var items = new[] { new PackItem(50, 20), new PackItem(300, 20) };

        // Act
        var result = ContainmentPacker.Pack(items, maxContentWidth: 100, horizontalGap: 5, verticalGap: 5, padding: 10);

        // Assert: the oversized item wrapped to its own row and the region widened to contain it
        Assert.True(result.Rects[1].Y > result.Rects[0].Y);
        Assert.True(result.Width >= 320.0);
    }

    /// <summary>
    ///     Determines whether two rectangles overlap with a positive-area intersection.
    /// </summary>
    private static bool Overlaps(PackedRect a, PackedRect b)
    {
        return a.X < b.X + b.Width &&
               b.X < a.X + a.Width &&
               a.Y < b.Y + b.Height &&
               b.Y < a.Y + a.Height;
    }
}
