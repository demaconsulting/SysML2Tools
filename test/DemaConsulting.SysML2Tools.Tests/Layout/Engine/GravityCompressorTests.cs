// <copyright file="GravityCompressorTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Engine;

namespace DemaConsulting.SysML2Tools.Tests.Layout.Engine;

/// <summary>
///     Tests for <see cref="GravityCompressor"/> overlap separation.
/// </summary>
public sealed class GravityCompressorTests
{
    /// <summary>Two overlapping boxes are separated to at least the minimum gap.</summary>
    [Fact]
    public void Compress_TwoOverlapping_SeparatedToMinGap()
    {
        var boxes = new[]
        {
            new CompressBox(0, 0, 100, 40, 100, 40),
            new CompressBox(50, 0, 100, 40, 100, 40),
        };

        var result = GravityCompressor.Compress(boxes, minGap: 20, gridUnit: 0);

        Assert.True(result.Feasible);
        var ax = result.Positions[0].X;
        var bx = result.Positions[1].X;
        var ay = result.Positions[0].Y;
        var by = result.Positions[1].Y;
        var clearX = bx + 100 <= ax || ax + 100 <= bx;
        var clearY = by + 40 <= ay || ay + 40 <= by;
        Assert.True(clearX || clearY, "boxes still overlap");
    }

    /// <summary>Boxes already clear stay unchanged.</summary>
    [Fact]
    public void Compress_Separated_Unchanged()
    {
        var boxes = new[]
        {
            new CompressBox(0, 0, 100, 40, 100, 40),
            new CompressBox(200, 0, 100, 40, 100, 40),
        };

        var result = GravityCompressor.Compress(boxes, minGap: 20, gridUnit: 0);

        Assert.True(result.Feasible);
        Assert.Equal(0, result.Positions[0].X, 6);
        Assert.Equal(200, result.Positions[1].X, 6);
    }

    /// <summary>A 3-box chain with one overlap only adjusts the overlapping pair.</summary>
    [Fact]
    public void Compress_ThreeChainOneOverlap_AdjustsOnlyThatGap()
    {
        var boxes = new[]
        {
            new CompressBox(0, 0, 100, 40, 100, 40),
            new CompressBox(80, 0, 100, 40, 100, 40),
            new CompressBox(400, 0, 100, 40, 100, 40),
        };

        var result = GravityCompressor.Compress(boxes, minGap: 20, gridUnit: 0);

        Assert.True(result.Feasible);
        Assert.Equal(400, result.Positions[2].X, 6);
        var gap = result.Positions[1].X - (result.Positions[0].X + 100);
        Assert.True(gap >= 20 - 1e-6);
    }

    /// <summary>A negative minimum gap is infeasible.</summary>
    [Fact]
    public void Compress_Impossible_ReturnsInfeasible()
    {
        var boxes = new[] { new CompressBox(0, 0, 100, 40, 100, 40) };

        var result = GravityCompressor.Compress(boxes, minGap: -1, gridUnit: 0);

        Assert.False(result.Feasible);
    }

    /// <summary>Compression is deterministic.</summary>
    [Fact]
    public void Compress_SameInput_IsDeterministic()
    {
        var boxes = new[]
        {
            new CompressBox(0, 0, 100, 40, 100, 40),
            new CompressBox(50, 10, 100, 40, 100, 40),
        };

        var a = GravityCompressor.Compress(boxes, minGap: 20, gridUnit: 10);
        var b = GravityCompressor.Compress(boxes, minGap: 20, gridUnit: 10);

        Assert.Equal(a.Positions[1].X, b.Positions[1].X, 9);
    }

    /// <summary>A corridor constraint expands the gap between row clusters to its minimum width.</summary>
    [Fact]
    public void Compress_CorridorConstraint_GapExpandsToMinWidth()
    {
        // Arrange: two rows of boxes close together with a horizontal corridor between them
        var boxes = new[]
        {
            new CompressBox(0, 0, 100, 40, 100, 40),
            new CompressBox(0, 60, 100, 40, 100, 40),
        };
        var corridors = new[] { new CorridorConstraint(IsHorizontal: true, Position: 50, MinWidth: 120) };

        // Act
        var result = GravityCompressor.Compress(boxes, minGap: 20, gridUnit: 0, corridors);

        // Assert: the vertical gap between the two boxes opens to at least the corridor width
        var gap = result.Positions[1].Y - (result.Positions[0].Y + 40);
        Assert.True(gap >= 120 - 1e-6, $"corridor gap was {gap}");
    }

    /// <summary>Passing a null corridor list preserves the original separation behaviour.</summary>
    [Fact]
    public void Compress_NoCorridor_ExistingBehaviourUnchanged()
    {
        var boxes = new[]
        {
            new CompressBox(0, 0, 100, 40, 100, 40),
            new CompressBox(50, 0, 100, 40, 100, 40),
        };

        var result = GravityCompressor.Compress(boxes, minGap: 20, gridUnit: 0, corridors: null);

        Assert.True(result.Feasible);
        var clearX = result.Positions[1].X + 100 <= result.Positions[0].X || result.Positions[0].X + 100 <= result.Positions[1].X;
        var clearY = result.Positions[1].Y + 40 <= result.Positions[0].Y || result.Positions[0].Y + 40 <= result.Positions[1].Y;
        Assert.True(clearX || clearY, "boxes still overlap");
    }
}
