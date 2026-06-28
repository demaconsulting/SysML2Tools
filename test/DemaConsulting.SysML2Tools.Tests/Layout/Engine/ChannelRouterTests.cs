// <copyright file="ChannelRouterTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout;
using DemaConsulting.SysML2Tools.Layout.Engine;

namespace DemaConsulting.SysML2Tools.Tests.Layout.Engine;

/// <summary>
///     Tests for <see cref="ChannelRouter"/> orthogonal edge routing.
/// </summary>
public sealed class ChannelRouterTests
{
    /// <summary>
    ///     A route with no obstacles still produces a valid orthogonal path from source to target.
    /// </summary>
    [Fact]
    public void Route_NoObstacles_ProducesOrthogonalPath()
    {
        // Act: route between two diagonal points with no obstacles
        var path = ChannelRouter.Route(new Point2D(0, 0), new Point2D(100, 80), [], clearance: 10);

        // Assert: path starts at source, ends at target, and every segment is axis-aligned
        AssertEndpoints(path, new Point2D(0, 0), new Point2D(100, 80));
        AssertAllSegmentsOrthogonal(path);
    }

    /// <summary>
    ///     With an obstacle directly between source and target, the route avoids the obstacle interior.
    /// </summary>
    [Fact]
    public void Route_ObstacleBetween_RoutesAround()
    {
        // Arrange: an obstacle squarely between the horizontal line from source to target
        var source = new Point2D(0, 50);
        var target = new Point2D(200, 50);
        var obstacles = new[] { new Rect(80, 0, 40, 100) };

        // Act
        var path = ChannelRouter.Route(source, target, obstacles, clearance: 10);

        // Assert: valid orthogonal path that does not cross the obstacle interior
        AssertEndpoints(path, source, target);
        AssertAllSegmentsOrthogonal(path);
        AssertNoSegmentCrossesObstacle(path, obstacles);
    }

    /// <summary>
    ///     With multiple staggered obstacles, the route remains orthogonal and obstacle-free.
    /// </summary>
    [Fact]
    public void Route_MultipleObstacles_RemainsValid()
    {
        // Arrange: several obstacles forming a partial maze between source and target
        var source = new Point2D(0, 0);
        var target = new Point2D(300, 200);
        var obstacles = new[]
        {
            new Rect(60, -20, 40, 160),
            new Rect(160, 60, 40, 200),
            new Rect(220, 0, 40, 120),
        };

        // Act
        var path = ChannelRouter.Route(source, target, obstacles, clearance: 12);

        // Assert
        AssertEndpoints(path, source, target);
        AssertAllSegmentsOrthogonal(path);
        AssertNoSegmentCrossesObstacle(path, obstacles);
    }

    /// <summary>
    ///     Horizontally aligned endpoints with no obstacle produce a single straight segment.
    /// </summary>
    [Fact]
    public void Route_AlignedEndpoints_ProducesStraightLine()
    {
        // Act: source and target share a Y coordinate with no obstacles
        var path = ChannelRouter.Route(new Point2D(0, 30), new Point2D(150, 30), [], clearance: 10);

        // Assert: a simple two-point straight segment
        Assert.Equal(2, path.Count);
        AssertEndpoints(path, new Point2D(0, 30), new Point2D(150, 30));
    }

    /// <summary>
    ///     When a source side is given, the route leaves the source with a perpendicular stub: the
    ///     first segment runs in the side's outward direction.
    /// </summary>
    [Fact]
    public void Route_WithSourceSide_LeavesPerpendicular()
    {
        // Arrange: source on the right side of its box, target up and to the right
        var source = new Point2D(100, 100);
        var target = new Point2D(200, 20);

        // Act: the source anchor is on the Right side, so the first move must go right (+x)
        var path = ChannelRouter.Route(source, target, [], clearance: 10, sourceSide: PortSide.Right);

        // Assert: first segment is horizontal and heads to the right (outward from the Right side)
        Assert.True(path.Count >= 2);
        Assert.Equal(source.X, path[0].X, 6);
        Assert.Equal(source.Y, path[0].Y, 6);
        Assert.Equal(path[0].Y, path[1].Y, 6); // horizontal first segment
        Assert.True(path[1].X > path[0].X, "First segment should leave the Right side going right.");
        AssertAllSegmentsOrthogonal(path);
    }

    /// <summary>
    ///     When a target side is given, the route enters the target with a perpendicular stub: the
    ///     last segment runs into the side's inward direction.
    /// </summary>
    [Fact]
    public void Route_WithTargetSide_EntersPerpendicular()
    {
        // Arrange: target on the top side of its box, source below-left
        var source = new Point2D(20, 200);
        var target = new Point2D(150, 100);

        // Act: the target anchor is on the Top side, so the last move must arrive going down (+y)
        var path = ChannelRouter.Route(source, target, [], clearance: 10, targetSide: PortSide.Top);

        // Assert: last segment is vertical and arrives from above (entering the Top side)
        Assert.Equal(target.X, path[^1].X, 6);
        Assert.Equal(target.Y, path[^1].Y, 6);
        Assert.Equal(path[^1].X, path[^2].X, 6); // vertical last segment
        Assert.True(path[^2].Y < path[^1].Y, "Last segment should enter the Top side from above.");
        AssertAllSegmentsOrthogonal(path);
    }

    /// <summary>
    ///     A clean route (no blocking obstacle) reports it did not cross via RouteWithStatus.
    /// </summary>
    [Fact]
    public void RouteWithStatus_NoBlockingObstacle_ReportsNotCrossed()
    {
        // Act: route around a single obstacle that a channel exists past
        var result = ChannelRouter.RouteWithStatus(
            new Point2D(0, 50), new Point2D(200, 50), [new Rect(80, 0, 40, 100)], clearance: 10);

        // Assert: a valid orthogonal route was found, so Crossed is false
        Assert.False(result.Crossed);
        AssertAllSegmentsOrthogonal(result.Waypoints);
    }

    /// <summary>
    ///     An obstacle squarely between the endpoints is routed around (not crossed), demonstrating
    ///     the clearance-retry robustness.
    /// </summary>
    [Fact]
    public void RouteWithStatus_ObstacleBetween_RoutesAroundWithoutCrossing()
    {
        // Arrange: an obstacle blocking the straight path but with room to route around
        var obstacles = new[] { new Rect(40, 40, 40, 40) };

        // Act
        var result = ChannelRouter.RouteWithStatus(
            new Point2D(0, 60), new Point2D(120, 60), obstacles, clearance: 8);

        // Assert: routed cleanly (no crossing) and no segment passes through the obstacle interior
        Assert.False(result.Crossed);
        AssertNoSegmentCrossesObstacle(result.Waypoints, obstacles);
    }

    /// <summary>
    ///     When the target lies inside an obstacle (no obstacle-free approach exists), the router
    ///     reports that it had to cross.
    /// </summary>
    [Fact]
    public void RouteWithStatus_TargetEnclosedByObstacle_ReportsCrossed()
    {
        // Arrange: an obstacle that fully encloses the target point
        var obstacles = new[] { new Rect(50, 0, 200, 100) };

        // Act: target (100, 50) is strictly inside the obstacle
        var result = ChannelRouter.RouteWithStatus(
            new Point2D(0, 50), new Point2D(100, 50), obstacles, clearance: 10);

        // Assert: no clean path exists, so Crossed is reported true
        Assert.True(result.Crossed);
    }

    /// <summary>
    ///     A clean route keeps the requested clearance from obstacles it passes, rather than grazing
    ///     their edges.
    /// </summary>
    [Fact]
    public void RouteWithStatus_CleanRoute_KeepsClearanceFromObstacles()
    {
        // Arrange: source sits just to the right of an obstacle; a straight drop would graze it.
        var obstacle = new Rect(0, 40, 60, 80);
        var obstacles = new[] { obstacle };

        // Act: route from above-right of the obstacle to below it.
        var result = ChannelRouter.RouteWithStatus(
            new Point2D(62, 0), new Point2D(30, 200), obstacles, clearance: 10);

        // Assert: routed cleanly and every segment stays at least (nearly) the clearance away.
        Assert.False(result.Crossed);
        for (var i = 0; i < result.Waypoints.Count - 1; i++)
        {
            Assert.True(
                SegmentDistanceToRect(result.Waypoints[i], result.Waypoints[i + 1], obstacle) > 10.0 - 1e-6,
                $"Segment {i} runs closer than the clearance to the obstacle.");
        }
    }

    /// <summary>
    ///     Asserts that the path begins at the expected source and ends at the expected target.
    /// </summary>
    private static void AssertEndpoints(IReadOnlyList<Point2D> path, Point2D source, Point2D target)
    {
        Assert.True(path.Count >= 2);
        Assert.Equal(source.X, path[0].X, 6);
        Assert.Equal(source.Y, path[0].Y, 6);
        Assert.Equal(target.X, path[^1].X, 6);
        Assert.Equal(target.Y, path[^1].Y, 6);
    }

    /// <summary>
    ///     Asserts that every consecutive pair of waypoints forms a horizontal or vertical segment.
    /// </summary>
    private static void AssertAllSegmentsOrthogonal(IReadOnlyList<Point2D> path)
    {
        for (var i = 0; i < path.Count - 1; i++)
        {
            var a = path[i];
            var b = path[i + 1];
            var horizontal = Math.Abs(a.Y - b.Y) < 1e-6;
            var vertical = Math.Abs(a.X - b.X) < 1e-6;
            Assert.True(horizontal || vertical,
                $"Segment {i} from ({a.X},{a.Y}) to ({b.X},{b.Y}) is not orthogonal.");
        }
    }

    /// <summary>
    ///     Asserts that no segment of the path passes through the interior of any obstacle.
    /// </summary>
    private static void AssertNoSegmentCrossesObstacle(IReadOnlyList<Point2D> path, IReadOnlyList<Rect> obstacles)
    {
        for (var i = 0; i < path.Count - 1; i++)
        {
            var a = path[i];
            var b = path[i + 1];
            foreach (var r in obstacles)
            {
                Assert.False(SegmentCrossesRect(a, b, r),
                    $"Segment {i} from ({a.X},{a.Y}) to ({b.X},{b.Y}) crosses obstacle.");
            }
        }
    }

    /// <summary>
    ///     Returns true when the axis-aligned segment passes through the strict interior of the rect.
    /// </summary>
    private static bool SegmentCrossesRect(Point2D a, Point2D b, Rect r)
    {
        if (Math.Abs(a.Y - b.Y) < 1e-6)
        {
            // Horizontal segment
            var y = a.Y;
            var xa = Math.Min(a.X, b.X);
            var xb = Math.Max(a.X, b.X);
            return r.Y < y && y < r.Y + r.Height &&
                   Math.Max(xa, r.X) < Math.Min(xb, r.X + r.Width);
        }

        // Vertical segment
        var x = a.X;
        var ya = Math.Min(a.Y, b.Y);
        var yb = Math.Max(a.Y, b.Y);
        return r.X < x && x < r.X + r.Width &&
               Math.Max(ya, r.Y) < Math.Min(yb, r.Y + r.Height);
    }

    /// <summary>
    ///     Returns the Euclidean distance from an axis-aligned segment to an axis-aligned rectangle
    ///     (0 when they intersect).
    /// </summary>
    private static double SegmentDistanceToRect(Point2D a, Point2D b, Rect r)
    {
        var xlo = Math.Min(a.X, b.X);
        var xhi = Math.Max(a.X, b.X);
        var ylo = Math.Min(a.Y, b.Y);
        var yhi = Math.Max(a.Y, b.Y);

        var dx = Math.Max(0.0, Math.Max(r.X - xhi, xlo - (r.X + r.Width)));
        var dy = Math.Max(0.0, Math.Max(r.Y - yhi, ylo - (r.Y + r.Height)));
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
