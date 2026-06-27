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
}
