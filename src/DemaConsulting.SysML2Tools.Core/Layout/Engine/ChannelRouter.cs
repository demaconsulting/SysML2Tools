// <copyright file="ChannelRouter.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout.Engine;

/// <summary>
/// An axis-aligned rectangle obstacle used by <see cref="ChannelRouter"/>.
/// </summary>
/// <param name="X">Absolute X coordinate of the left edge in logical pixels.</param>
/// <param name="Y">Absolute Y coordinate of the top edge in logical pixels.</param>
/// <param name="Width">Width in logical pixels.</param>
/// <param name="Height">Height in logical pixels.</param>
internal readonly record struct Rect(double X, double Y, double Width, double Height);

/// <summary>
/// Routes orthogonal (right-angle) connector lines between two points while avoiding a set of
/// rectangular obstacles.
/// </summary>
/// <remarks>
/// <para>
/// The router builds a sparse "Hanan-style" routing grid whose vertical lines are the source and
/// target X coordinates plus each obstacle's left/right edges offset outward by a clearance, and
/// whose horizontal lines are the analogous Y coordinates. It then runs an A* search over the grid,
/// preferring straight runs via a turn penalty. Because the grid lines include the exact source and
/// target coordinates, the returned path starts at the source and ends at the target exactly, and
/// every segment is strictly horizontal or vertical.
/// </para>
/// <para>
/// The caller must exclude the boxes that own the source and target anchors from
/// <c>obstacles</c>; otherwise the very first segment would be blocked by the source's own box.
/// When no obstacle-free path exists, the router falls back to a simple L-shaped route so that a
/// result is always returned.
/// </para>
/// </remarks>
internal static class ChannelRouter
{
    /// <summary>Direction of travel along a segment, used for turn-penalty accounting.</summary>
    private enum Dir
    {
        /// <summary>No prior direction (search start).</summary>
        None,

        /// <summary>Horizontal travel.</summary>
        Horizontal,

        /// <summary>Vertical travel.</summary>
        Vertical,
    }

    /// <summary>
    /// Computes an orthogonal route from <paramref name="source"/> to <paramref name="target"/>
    /// avoiding the interiors of the given obstacles.
    /// </summary>
    /// <param name="source">Start point (typically an anchor on the source box boundary).</param>
    /// <param name="target">End point (typically an anchor on the target box boundary).</param>
    /// <param name="obstacles">
    /// Rectangles to route around, excluding the boxes that own the source and target anchors.
    /// </param>
    /// <param name="clearance">Minimum gap kept between routed segments and obstacles.</param>
    /// <returns>
    /// An ordered list of waypoints beginning with <paramref name="source"/> and ending with
    /// <paramref name="target"/>. Consecutive waypoints always share an X or a Y coordinate.
    /// </returns>
    public static IReadOnlyList<Point2D> Route(
        Point2D source,
        Point2D target,
        IReadOnlyList<Rect> obstacles,
        double clearance)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(obstacles);

        // Build the candidate grid coordinates from endpoints and clearance-offset obstacle edges.
        var xs = BuildAxis(source.X, target.X, obstacles, clearance, horizontal: true);
        var ys = BuildAxis(source.Y, target.Y, obstacles, clearance, horizontal: false);

        var startI = IndexOf(xs, source.X);
        var startJ = IndexOf(ys, source.Y);
        var goalI = IndexOf(xs, target.X);
        var goalJ = IndexOf(ys, target.Y);

        var path = AStar(xs, ys, startI, startJ, goalI, goalJ, obstacles);
        if (path is null)
        {
            // No obstacle-free route found: fall back to an L-shaped path.
            return BuildFallback(source, target);
        }

        return Simplify(path);
    }

    /// <summary>
    /// Builds the sorted, de-duplicated set of grid coordinates for one axis: the two endpoint
    /// coordinates plus each obstacle's near/far edge offset outward by the clearance.
    /// </summary>
    private static double[] BuildAxis(
        double a,
        double b,
        IReadOnlyList<Rect> obstacles,
        double clearance,
        bool horizontal)
    {
        var set = new SortedSet<double> { a, b };
        foreach (var r in obstacles)
        {
            if (horizontal)
            {
                set.Add(r.X - clearance);
                set.Add(r.X + r.Width + clearance);
            }
            else
            {
                set.Add(r.Y - clearance);
                set.Add(r.Y + r.Height + clearance);
            }
        }

        return [.. set];
    }

    /// <summary>
    /// Returns the index of the grid line equal to <paramref name="value"/>. The value is always
    /// present because the axis was built to include it.
    /// </summary>
    private static int IndexOf(double[] axis, double value)
    {
        for (var i = 0; i < axis.Length; i++)
        {
            if (Math.Abs(axis[i] - value) < 1e-9)
            {
                return i;
            }
        }

        // Should never happen: endpoint coordinates are always added to the axis.
        return 0;
    }

    /// <summary>
    /// Runs an A* search over the grid, returning the sequence of grid points from start to goal,
    /// or <see langword="null"/> when no obstacle-free path exists.
    /// </summary>
    private static List<Point2D>? AStar(
        double[] xs,
        double[] ys,
        int startI,
        int startJ,
        int goalI,
        int goalJ,
        IReadOnlyList<Rect> obstacles)
    {
        var nx = xs.Length;
        var ny = ys.Length;

        // Visited cost keyed by (i, j, direction) so straight-through and turning arrivals differ.
        var best = new Dictionary<(int, int, Dir), double>();
        var cameFrom = new Dictionary<(int, int, Dir), (int, int, Dir)>();
        var open = new PriorityQueue<(int I, int J, Dir D), double>();

        var startState = (startI, startJ, Dir.None);
        best[startState] = 0.0;
        open.Enqueue((startI, startJ, Dir.None), Heuristic(xs, ys, startI, startJ, goalI, goalJ));

        // Turn penalty expressed in pixels; comparable to a short straight run so detours that
        // remove a bend are preferred only when not much longer.
        const double TurnPenalty = 20.0;

        while (open.Count > 0)
        {
            var (ci, cj, cd) = open.Dequeue();
            var current = (ci, cj, cd);
            var g = best[current];

            if (ci == goalI && cj == goalJ)
            {
                return Reconstruct(xs, ys, cameFrom, current);
            }

            foreach (var (ni, nj, nd) in Neighbors(ci, cj, nx, ny))
            {
                // Skip moves whose segment passes through an obstacle interior.
                if (SegmentBlocked(xs, ys, ci, cj, ni, nj, obstacles))
                {
                    continue;
                }

                var stepLength = nd == Dir.Horizontal
                    ? Math.Abs(xs[ni] - xs[ci])
                    : Math.Abs(ys[nj] - ys[cj]);
                var turnCost = cd != Dir.None && cd != nd ? TurnPenalty : 0.0;
                var tentative = g + stepLength + turnCost;

                var neighborState = (ni, nj, nd);
                if (best.TryGetValue(neighborState, out var existing) && tentative >= existing)
                {
                    continue;
                }

                best[neighborState] = tentative;
                cameFrom[neighborState] = current;
                var f = tentative + Heuristic(xs, ys, ni, nj, goalI, goalJ);
                open.Enqueue(neighborState, f);
            }
        }

        return null;
    }

    /// <summary>Enumerates the four grid neighbors of a node along with the travel direction.</summary>
    private static IEnumerable<(int I, int J, Dir D)> Neighbors(int i, int j, int nx, int ny)
    {
        if (i + 1 < nx)
        {
            yield return (i + 1, j, Dir.Horizontal);
        }

        if (i - 1 >= 0)
        {
            yield return (i - 1, j, Dir.Horizontal);
        }

        if (j + 1 < ny)
        {
            yield return (i, j + 1, Dir.Vertical);
        }

        if (j - 1 >= 0)
        {
            yield return (i, j - 1, Dir.Vertical);
        }
    }

    /// <summary>Manhattan-distance heuristic between two grid nodes.</summary>
    private static double Heuristic(double[] xs, double[] ys, int i, int j, int goalI, int goalJ) =>
        Math.Abs(xs[i] - xs[goalI]) + Math.Abs(ys[j] - ys[goalJ]);

    /// <summary>
    /// Determines whether the straight grid segment between two adjacent nodes passes through the
    /// interior of any obstacle.
    /// </summary>
    private static bool SegmentBlocked(
        double[] xs,
        double[] ys,
        int i1,
        int j1,
        int i2,
        int j2,
        IReadOnlyList<Rect> obstacles)
    {
        if (j1 == j2)
        {
            // Horizontal segment at y = ys[j1] spanning the two x grid lines.
            var y = ys[j1];
            var xa = Math.Min(xs[i1], xs[i2]);
            var xb = Math.Max(xs[i1], xs[i2]);
            foreach (var r in obstacles)
            {
                if (r.Y < y && y < r.Y + r.Height &&
                    Math.Max(xa, r.X) < Math.Min(xb, r.X + r.Width))
                {
                    return true;
                }
            }
        }
        else
        {
            // Vertical segment at x = xs[i1] spanning the two y grid lines.
            var x = xs[i1];
            var ya = Math.Min(ys[j1], ys[j2]);
            var yb = Math.Max(ys[j1], ys[j2]);
            foreach (var r in obstacles)
            {
                if (r.X < x && x < r.X + r.Width &&
                    Math.Max(ya, r.Y) < Math.Min(yb, r.Y + r.Height))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Reconstructs the grid-point path by walking the came-from chain back to the start.</summary>
    private static List<Point2D> Reconstruct(
        double[] xs,
        double[] ys,
        Dictionary<(int, int, Dir), (int, int, Dir)> cameFrom,
        (int, int, Dir) goal)
    {
        var points = new List<Point2D>();
        var cursor = goal;
        while (true)
        {
            var (i, j, _) = cursor;
            points.Add(new Point2D(xs[i], ys[j]));
            if (!cameFrom.TryGetValue(cursor, out var prev))
            {
                break;
            }

            cursor = prev;
        }

        points.Reverse();
        return points;
    }

    /// <summary>Collapses consecutive collinear waypoints into single straight segments.</summary>
    private static IReadOnlyList<Point2D> Simplify(List<Point2D> points)
    {
        if (points.Count <= 2)
        {
            return points;
        }

        var result = new List<Point2D> { points[0] };
        for (var k = 1; k < points.Count - 1; k++)
        {
            var prev = result[^1];
            var cur = points[k];
            var next = points[k + 1];

            // Drop the middle point when prev, cur, next are collinear (all share X or all share Y).
            var collinearX = Math.Abs(prev.X - cur.X) < 1e-9 && Math.Abs(cur.X - next.X) < 1e-9;
            var collinearY = Math.Abs(prev.Y - cur.Y) < 1e-9 && Math.Abs(cur.Y - next.Y) < 1e-9;
            if (!collinearX && !collinearY)
            {
                result.Add(cur);
            }
        }

        result.Add(points[^1]);
        return result;
    }

    /// <summary>
    /// Builds a simple L-shaped fallback route used when A* cannot find an obstacle-free path.
    /// </summary>
    private static IReadOnlyList<Point2D> BuildFallback(Point2D source, Point2D target)
    {
        // Aligned endpoints need only a straight segment.
        if (Math.Abs(source.X - target.X) < 1e-9 || Math.Abs(source.Y - target.Y) < 1e-9)
        {
            return [source, target];
        }

        // Otherwise route horizontally then vertically through the elbow point.
        return [source, new Point2D(target.X, source.Y), target];
    }
}
