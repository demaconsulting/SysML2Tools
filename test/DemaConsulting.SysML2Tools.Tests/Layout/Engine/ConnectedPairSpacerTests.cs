// <copyright file="ConnectedPairSpacerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Engine;

namespace DemaConsulting.SysML2Tools.Tests.Layout.Engine;

/// <summary>
///     Tests for <see cref="ConnectedPairSpacer"/> directional clearance of connected box pairs.
/// </summary>
public sealed class ConnectedPairSpacerTests
{
    /// <summary>A touching connected pair is pushed apart to twice the approach zone.</summary>
    [Fact]
    public void Space_ZeroGapPair_PushedByTwoApproachZones()
    {
        // Arrange: two horizontally adjacent boxes touching with no gap
        var boxes = new[]
        {
            new Rect(0, 0, 100, 40),
            new Rect(100, 0, 100, 40),
        };
        var pairs = new[] { new ConnectedPair(0, 1) };

        // Act
        var spaced = ConnectedPairSpacer.Space(boxes, pairs, approachZone: 10.0);

        // Assert: facing gap becomes 2 * approachZone = 20
        var gap = spaced[1].X - (spaced[0].X + spaced[0].Width);
        Assert.Equal(20.0, gap, 6);
    }

    /// <summary>An unconnected pair sharing an edge is not moved.</summary>
    [Fact]
    public void Space_UnconnectedPair_Unmoved()
    {
        // Arrange: two touching boxes but no connectivity between them
        var boxes = new[]
        {
            new Rect(0, 0, 100, 40),
            new Rect(100, 0, 100, 40),
        };

        // Act
        var spaced = ConnectedPairSpacer.Space(boxes, [], approachZone: 10.0);

        // Assert: positions unchanged
        Assert.Equal(0.0, spaced[0].X, 6);
        Assert.Equal(100.0, spaced[1].X, 6);
    }

    /// <summary>A pair already wider than two approach zones stays put.</summary>
    [Fact]
    public void Space_SeparatedPair_Unmoved()
    {
        // Arrange: boxes already 100px apart, far beyond 2 * approachZone
        var boxes = new[]
        {
            new Rect(0, 0, 100, 40),
            new Rect(200, 0, 100, 40),
        };
        var pairs = new[] { new ConnectedPair(0, 1) };

        // Act
        var spaced = ConnectedPairSpacer.Space(boxes, pairs, approachZone: 10.0);

        // Assert: unchanged
        Assert.Equal(0.0, spaced[0].X, 6);
        Assert.Equal(200.0, spaced[1].X, 6);
    }

    /// <summary>Identical input produces identical output.</summary>
    [Fact]
    public void Space_SameInput_IsDeterministic()
    {
        var boxes = new[]
        {
            new Rect(0, 0, 100, 40),
            new Rect(105, 0, 100, 40),
        };
        var pairs = new[] { new ConnectedPair(0, 1) };

        var a = ConnectedPairSpacer.Space(boxes, pairs, approachZone: 12.0);
        var b = ConnectedPairSpacer.Space(boxes, pairs, approachZone: 12.0);

        Assert.Equal(a[0].X, b[0].X, 6);
        Assert.Equal(a[1].X, b[1].X, 6);
    }
}
