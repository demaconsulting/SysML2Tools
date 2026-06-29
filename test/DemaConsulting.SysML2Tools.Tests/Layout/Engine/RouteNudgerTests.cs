// <copyright file="RouteNudgerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout;
using DemaConsulting.SysML2Tools.Layout.Engine;

namespace DemaConsulting.SysML2Tools.Tests.Layout.Engine;

/// <summary>
///     Tests for <see cref="RouteNudger"/> coincident-segment separation.
/// </summary>
public sealed class RouteNudgerTests
{
    /// <summary>Null routes argument throws <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void RouteNudger_NudgeConflicts_NullRoutes_ThrowsArgumentNullException()
    {
        // Act / Assert: passing null must throw before touching any segment data
        Assert.Throws<ArgumentNullException>(() =>
            RouteNudger.NudgeConflicts(null!, edgeSpacing: 10.0));
    }

    /// <summary>An empty route list is returned unchanged.</summary>
    [Fact]
    public void RouteNudger_NudgeConflicts_EmptyList_ReturnsEmpty()
    {
        // Act
        var result = RouteNudger.NudgeConflicts([], edgeSpacing: 10.0);

        // Assert: empty input → empty output
        Assert.Empty(result);
    }

    /// <summary>A single route with no peers is returned unchanged.</summary>
    [Fact]
    public void RouteNudger_NudgeConflicts_SingleRoute_ReturnsUnchanged()
    {
        // Arrange: one simple L-shaped route
        var route = new List<Point2D> { new(100, 0), new(100, 100), new(200, 100) };

        // Act
        var result = RouteNudger.NudgeConflicts([route], edgeSpacing: 10.0);

        // Assert: same waypoints, untouched
        Assert.Single(result);
        AssertRoute(result[0], route);
    }

    /// <summary>Two routes whose vertical segments share an X but have non-overlapping Y ranges are not nudged.</summary>
    [Fact]
    public void RouteNudger_NudgeConflicts_TwoRoutes_NoYOverlap_NoNudge()
    {
        // Arrange: route A occupies Y [0, 40], route B occupies Y [60, 100] — gap between them
        var routeA = new List<Point2D> { new(100, 0), new(100, 40), new(200, 40) };
        var routeB = new List<Point2D> { new(100, 60), new(100, 100), new(200, 100) };

        // Act
        var result = RouteNudger.NudgeConflicts([routeA, routeB], edgeSpacing: 10.0);

        // Assert: both routes unchanged because their Y ranges do not overlap
        AssertRoute(result[0], routeA);
        AssertRoute(result[1], routeB);
    }

    /// <summary>Two routes with overlapping vertical segments at the same X are nudged to distinct X lanes.</summary>
    [Fact]
    public void RouteNudger_NudgeConflicts_TwoRoutes_OverlappingVertical_NudgesSecond()
    {
        // Arrange: both routes have a vertical segment at X=100 with overlapping Y [20,80] vs [0,100]
        var routeA = new List<Point2D> { new(100, 0), new(100, 100), new(200, 100) };
        var routeB = new List<Point2D> { new(100, 20), new(100, 80), new(200, 80) };

        // Act
        var result = RouteNudger.NudgeConflicts([routeA, routeB], edgeSpacing: 10.0);

        // Assert: route A (lane 0) stays at X=100; route B (lane 1) is nudged to X=110
        Assert.Equal(100.0, result[0][0].X, 6);
        Assert.Equal(100.0, result[0][1].X, 6);
        Assert.Equal(110.0, result[1][0].X, 6);
        Assert.Equal(110.0, result[1][1].X, 6);

        // The horizontal tail of route B is not a conflicting vertical segment and stays at X=200
        Assert.Equal(200.0, result[1][2].X, 6);
    }

    /// <summary>Two routes with overlapping horizontal segments at the same Y are nudged to distinct Y lanes.</summary>
    [Fact]
    public void RouteNudger_NudgeConflicts_TwoRoutes_OverlappingHorizontal_NudgesSecond()
    {
        // Arrange: both routes share Y=50 for a horizontal segment that overlaps in X
        //          route A: (0,0)→(0,50)→(100,50)   route B: (50,0)→(50,50)→(150,50)
        var routeA = new List<Point2D> { new(0, 0), new(0, 50), new(100, 50) };
        var routeB = new List<Point2D> { new(50, 0), new(50, 50), new(150, 50) };

        // Act
        var result = RouteNudger.NudgeConflicts([routeA, routeB], edgeSpacing: 8.0);

        // Assert: route A (lane 0) stays at Y=50; route B (lane 1) nudged to Y=58
        Assert.Equal(50.0, result[0][1].Y, 6); // A's horizontal start
        Assert.Equal(50.0, result[0][2].Y, 6); // A's horizontal end
        Assert.Equal(58.0, result[1][1].Y, 6); // B's horizontal start, nudged
        Assert.Equal(58.0, result[1][2].Y, 6); // B's horizontal end, nudged
    }

    /// <summary>Three routes with the same overlapping vertical segment are spread across three lanes.</summary>
    [Fact]
    public void RouteNudger_NudgeConflicts_ThreeRoutes_ConflictingVertical_ThreeLanes()
    {
        // Arrange: three routes all pass through vertical segment at X=100, Y [0,100]
        var routeA = new List<Point2D> { new(100, 0), new(100, 100), new(200, 100) };
        var routeB = new List<Point2D> { new(100, 10), new(100, 90), new(200, 90) };
        var routeC = new List<Point2D> { new(100, 20), new(100, 80), new(200, 80) };

        // Act
        var result = RouteNudger.NudgeConflicts([routeA, routeB, routeC], edgeSpacing: 10.0);

        // Assert: lanes 0, 1, 2 → X = 100, 110, 120
        Assert.Equal(100.0, result[0][0].X, 6);
        Assert.Equal(110.0, result[1][0].X, 6);
        Assert.Equal(120.0, result[2][0].X, 6);
    }

    /// <summary>A nudged vertical segment does not disturb adjacent horizontal segments — they stretch cleanly.</summary>
    [Fact]
    public void RouteNudger_NudgeConflicts_NudgedRoute_AdjacentHorizontalsRemainOrthogonal()
    {
        // Arrange: full orthogonal path  (0,50)→(100,50)→(100,150)→(200,150)
        //          with a conflicting route at the same X=100 for the middle vertical
        var routeA = new List<Point2D> { new(0, 50), new(100, 50), new(100, 150), new(200, 150) };
        var routeB = new List<Point2D> { new(0, 60), new(100, 60), new(100, 140), new(200, 140) };

        // Act
        var result = RouteNudger.NudgeConflicts([routeA, routeB], edgeSpacing: 12.0);

        // Assert: routeB's vertical segment (index 1→2) is nudged to X=112
        Assert.Equal(112.0, result[1][1].X, 6);
        Assert.Equal(112.0, result[1][2].X, 6);

        // The horizontal segments before and after share endpoints with the vertical, so they remain
        // orthogonal: segment 0→1 (Y=60) and segment 2→3 (Y=140) are still horizontal.
        Assert.Equal(result[1][0].Y, result[1][1].Y, 6); // seg 0→1 stays horizontal
        Assert.Equal(result[1][2].Y, result[1][3].Y, 6); // seg 2→3 stays horizontal
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>Asserts that <paramref name="actual"/> has the same waypoints as <paramref name="expected"/>.</summary>
    private static void AssertRoute(IReadOnlyList<Point2D> actual, IReadOnlyList<Point2D> expected)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].X, actual[i].X, 6);
            Assert.Equal(expected[i].Y, actual[i].Y, 6);
        }
    }
}
