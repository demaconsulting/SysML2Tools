// <copyright file="ChannelRouter.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout.Engine;

/// <summary>
/// The outcome of a routing request: the computed waypoints and whether the route had to cross an
/// obstacle (i.e. no obstacle-free orthogonal path could be found).
/// </summary>
/// <param name="Waypoints">Ordered orthogonal waypoints from source to target.</param>
/// <param name="Crossed">
/// <see langword="true"/> when the router fell back to a path that may cross a box; this indicates a
/// degenerate (over-dense or overlapping) placement worth surfacing as a layout warning.
/// </param>
internal readonly record struct RouteResult(IReadOnlyList<Point2D> Waypoints, bool Crossed);

/// <summary>
/// A cost band: a region of the canvas where routing is cheaper (or dearer) than normal, used to bias
/// the router toward bundling wires along committed highways.
/// </summary>
/// <param name="IsHorizontal">True when the band spans a horizontal stripe (Y range), false for vertical (X range).</param>
/// <param name="Start">Lower bound of the band on its perpendicular axis, in logical pixels.</param>
/// <param name="End">Upper bound of the band on its perpendicular axis, in logical pixels.</param>
/// <param name="Multiplier">Cost factor applied to segments inside the band (0.6 cheaper, 1.0 neutral).</param>
internal readonly record struct CostBand(bool IsHorizontal, double Start, double End, double Multiplier);

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
    /// <param name="sourceSide">
    /// Optional box side the source anchor sits on. When given, the route leaves the source with a
    /// short stub perpendicular to that side before routing freely, so connectors exit boxes cleanly.
    /// </param>
    /// <param name="targetSide">Optional box side the target anchor sits on; see <paramref name="sourceSide"/>.</param>
    /// <param name="costBands">Optional cost bands biasing the route toward highway corridors; null leaves cost neutral.</param>
    /// <returns>
    /// An ordered list of waypoints beginning with <paramref name="source"/> and ending with
    /// <paramref name="target"/>. Consecutive waypoints always share an X or a Y coordinate.
    /// </returns>
    public static IReadOnlyList<Point2D> Route(
        Point2D source,
        Point2D target,
        IReadOnlyList<Rect> obstacles,
        double clearance,
        PortSide? sourceSide = null,
        PortSide? targetSide = null,
        IReadOnlyList<CostBand>? costBands = null) =>
        RouteWithStatus(source, target, obstacles, clearance, sourceSide, targetSide, costBands).Waypoints;

    /// <summary>
    /// Computes an orthogonal route and reports whether it had to cross an obstacle. The route is
    /// attempted with progressively smaller clearances; only when no obstacle-free orthogonal path
    /// exists at any clearance does it fall back to a (possibly crossing) L-shape, in which case
    /// <see cref="RouteResult.Crossed"/> is <see langword="true"/>.
    /// </summary>
    /// <param name="source">Start point (typically an anchor on the source box boundary).</param>
    /// <param name="target">End point (typically an anchor on the target box boundary).</param>
    /// <param name="obstacles">Rectangles to route around, excluding the source and target boxes.</param>
    /// <param name="clearance">Preferred gap between routed segments and obstacles.</param>
    /// <param name="sourceSide">Optional box side the source anchor sits on (adds a perpendicular stub).</param>
    /// <param name="targetSide">Optional box side the target anchor sits on (adds a perpendicular stub).</param>
    /// <param name="costBands">Optional cost bands biasing the route toward highway corridors; null leaves cost neutral.</param>
    /// <returns>The waypoints and a flag indicating whether the route crosses an obstacle.</returns>
    public static RouteResult RouteWithStatus(
        Point2D source,
        Point2D target,
        IReadOnlyList<Rect> obstacles,
        double clearance,
        PortSide? sourceSide = null,
        PortSide? targetSide = null,
        IReadOnlyList<CostBand>? costBands = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(obstacles);

        // Step off each anchor's box edge with a perpendicular stub so connectors enter and leave
        // boxes at right angles instead of sliding along the edge. The stub is capped so that two
        // stubs facing each other across a small gap meet at the midline instead of overshooting
        // (which would force a back-and-forth jog right at the arrowhead).
        // FUTURE (Phase 14c cleanup): replace this magic 8.0 with theme.ConnectorApproachZone.
        var stub = clearance + 8.0;
        var routeSource = StepOff(source, sourceSide, StubLength(source, sourceSide, target, stub));
        var routeTarget = StepOff(target, targetSide, StubLength(target, targetSide, source, stub));

        // Try to find an obstacle-free orthogonal path, preferring the largest clearance that works.
        foreach (var c in ClearanceLevels(clearance))
        {
            var xs = BuildAxis(routeSource.X, routeTarget.X, obstacles, c, horizontal: true);
            var ys = BuildAxis(routeSource.Y, routeTarget.Y, obstacles, c, horizontal: false);

            var path = AStar(
                xs, ys,
                IndexOf(xs, routeSource.X), IndexOf(ys, routeSource.Y),
                IndexOf(xs, routeTarget.X), IndexOf(ys, routeTarget.Y),
                obstacles, c, costBands);

            if (path is not null)
            {
                return new RouteResult(Finalize(source, target, sourceSide, targetSide, path), Crossed: false);
            }
        }

        // No clean path at any clearance: fall back to the least-bad L-shape (it may cross a box).
        var fallback = BuildObstacleAwareFallback(routeSource, routeTarget, obstacles);
        return new RouteResult(Finalize(source, target, sourceSide, targetSide, fallback), Crossed: true);
    }

    /// <summary>
    /// Yields the clearances to attempt, from the requested value down to zero, so the router prefers
    /// a spacious route but still hugs box edges (clearance 0) rather than crossing them.
    /// </summary>
    private static IEnumerable<double> ClearanceLevels(double clearance)
    {
        var seen = new HashSet<double>();
        foreach (var c in new[] { clearance, clearance / 2.0, clearance / 4.0, 0.0 })
        {
            var v = Math.Max(0.0, c);
            if (seen.Add(v))
            {
                yield return v;
            }
        }
    }

    /// <summary>
    /// Re-attaches the original anchor points outside their stubs and simplifies the path.
    /// </summary>
    private static IReadOnlyList<Point2D> Finalize(
        Point2D source,
        Point2D target,
        PortSide? sourceSide,
        PortSide? targetSide,
        IReadOnlyList<Point2D> path)
    {
        var full = new List<Point2D>();
        if (sourceSide is not null)
        {
            full.Add(source);
        }

        full.AddRange(path);

        if (targetSide is not null)
        {
            full.Add(target);
        }

        return Simplify(full);
    }

    /// <summary>
    /// Returns the point offset from <paramref name="anchor"/> by <paramref name="distance"/> in the
    /// outward-normal direction of <paramref name="side"/>, or the anchor unchanged when no side.
    /// </summary>
    private static Point2D StepOff(Point2D anchor, PortSide? side, double distance) => side switch
    {
        PortSide.Top => new Point2D(anchor.X, anchor.Y - distance),
        PortSide.Bottom => new Point2D(anchor.X, anchor.Y + distance),
        PortSide.Left => new Point2D(anchor.X - distance, anchor.Y),
        PortSide.Right => new Point2D(anchor.X + distance, anchor.Y),
        _ => anchor,
    };

    /// <summary>
    /// Returns the stub length to step off <paramref name="anchor"/>'s edge: the base length, but
    /// capped to half the distance to <paramref name="other"/> measured along the side's outward
    /// normal when <paramref name="other"/> lies in that direction. This keeps two stubs that face
    /// each other across a narrow gap from overshooting past the midline (which produces a visible
    /// reversal at the connector's end). When the projection is exactly zero (ports at the same
    /// coordinate on facing sides, e.g. touching-edge boxes), the stub collapses to zero so the
    /// connector routes directly between the ports without stepping into the opposite box.
    /// </summary>
    private static double StubLength(Point2D anchor, PortSide? side, Point2D other, double baseStub)
    {
        var projection = side switch
        {
            PortSide.Top => anchor.Y - other.Y,
            PortSide.Bottom => other.Y - anchor.Y,
            PortSide.Left => anchor.X - other.X,
            PortSide.Right => other.X - anchor.X,
            _ => double.PositiveInfinity,
        };

        return projection >= 0 ? Math.Min(baseStub, projection / 2.0) : baseStub;
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
        IReadOnlyList<Rect> obstacles,
        double clearance,
        IReadOnlyList<CostBand>? costBands)
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
                // Skip moves whose segment passes within the clearance of an obstacle.
                if (SegmentBlocked(xs, ys, ci, cj, ni, nj, obstacles, clearance))
                {
                    continue;
                }

                var stepLength = nd == Dir.Horizontal
                    ? Math.Abs(xs[ni] - xs[ci])
                    : Math.Abs(ys[nj] - ys[cj]);
                var bandMultiplier = SegmentCostMultiplier(xs, ys, ci, cj, ni, nj, costBands);
                var turnCost = cd != Dir.None && cd != nd ? TurnPenalty : 0.0;
                var tentative = g + (stepLength * bandMultiplier) + turnCost;

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
    /// Returns the cheapest cost multiplier for a segment by testing whether its midpoint lies inside
    /// any cost band; a highway band (multiplier &lt; 1) makes bundled runs cheaper so the router
    /// prefers them. When no band applies the cost is neutral (1.0).
    /// </summary>
    private static double SegmentCostMultiplier(
        double[] xs,
        double[] ys,
        int i1,
        int j1,
        int i2,
        int j2,
        IReadOnlyList<CostBand>? bands)
    {
        if (bands is null || bands.Count == 0)
        {
            return 1.0;
        }

        // Sample the segment midpoint; bands are stripes, so a point test is enough to classify it.
        var mx = (xs[i1] + xs[i2]) / 2.0;
        var my = (ys[j1] + ys[j2]) / 2.0;
        var best = 1.0;
        foreach (var band in bands)
        {
            var coord = band.IsHorizontal ? my : mx;
            if (coord >= band.Start && coord <= band.End)
            {
                best = Math.Min(best, band.Multiplier);
            }
        }

        return best;
    }

    /// <summary>
    /// Determines whether the straight grid segment between two adjacent nodes passes within
    /// <paramref name="clearance"/> of any obstacle (the obstacle rectangles are inflated by the
    /// clearance and tested with strict inequalities, so a segment exactly one clearance away is
    /// allowed).
    /// </summary>
    private static bool SegmentBlocked(
        double[] xs,
        double[] ys,
        int i1,
        int j1,
        int i2,
        int j2,
        IReadOnlyList<Rect> obstacles,
        double clearance)
    {
        if (j1 == j2)
        {
            // Horizontal segment at y = ys[j1] spanning the two x grid lines.
            var y = ys[j1];
            var xa = Math.Min(xs[i1], xs[i2]);
            var xb = Math.Max(xs[i1], xs[i2]);
            foreach (var r in obstacles)
            {
                if (r.Y - clearance < y && y < r.Y + r.Height + clearance &&
                    Math.Max(xa, r.X - clearance) < Math.Min(xb, r.X + r.Width + clearance))
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
                if (r.X - clearance < x && x < r.X + r.Width + clearance &&
                    Math.Max(ya, r.Y - clearance) < Math.Min(yb, r.Y + r.Height + clearance))
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

            // Drop exact duplicates of the previous point (stubs can introduce these).
            if (Math.Abs(prev.X - cur.X) < 1e-9 && Math.Abs(prev.Y - cur.Y) < 1e-9)
            {
                continue;
            }

            // Drop the middle point only when prev→cur→next is collinear AND monotonic (same
            // direction). A direction reversal (U-turn) on the same axis must be preserved, e.g. a
            // perpendicular stub that briefly overshoots before entering a box.
            var collinearX = Math.Abs(prev.X - cur.X) < 1e-9 && Math.Abs(cur.X - next.X) < 1e-9 &&
                             (cur.Y - prev.Y) * (next.Y - cur.Y) >= 0;
            var collinearY = Math.Abs(prev.Y - cur.Y) < 1e-9 && Math.Abs(cur.Y - next.Y) < 1e-9 &&
                             (cur.X - prev.X) * (next.X - cur.X) >= 0;
            if (!collinearX && !collinearY)
            {
                result.Add(cur);
            }
        }

        // Append the final point unless it duplicates the current last point.
        if (Math.Abs(result[^1].X - points[^1].X) >= 1e-9 || Math.Abs(result[^1].Y - points[^1].Y) >= 1e-9)
        {
            result.Add(points[^1]);
        }

        return result;
    }

    /// <summary>
    /// Builds the least-bad L-shaped fallback route used when A* cannot find an obstacle-free path:
    /// it tries the horizontal-first and vertical-first elbows and returns whichever crosses fewer
    /// obstacles.
    /// </summary>
    private static IReadOnlyList<Point2D> BuildObstacleAwareFallback(
        Point2D source,
        Point2D target,
        IReadOnlyList<Rect> obstacles)
    {
        // Aligned endpoints need only a straight segment.
        if (Math.Abs(source.X - target.X) < 1e-9 || Math.Abs(source.Y - target.Y) < 1e-9)
        {
            return [source, target];
        }

        // Two candidate elbows: horizontal-first and vertical-first.
        var horizontalFirst = new List<Point2D> { source, new(target.X, source.Y), target };
        var verticalFirst = new List<Point2D> { source, new(source.X, target.Y), target };

        var hCrossings = CountCrossings(horizontalFirst, obstacles);
        var vCrossings = CountCrossings(verticalFirst, obstacles);

        return hCrossings <= vCrossings ? horizontalFirst : verticalFirst;
    }

    /// <summary>Counts how many (segment, obstacle) pairs along a path cross an obstacle interior.</summary>
    private static int CountCrossings(IReadOnlyList<Point2D> path, IReadOnlyList<Rect> obstacles)
    {
        var count = 0;
        for (var i = 0; i < path.Count - 1; i++)
        {
            var a = path[i];
            var b = path[i + 1];
            count += obstacles.Count(r => SegmentCrossesRect(a, b, r));
        }

        return count;
    }

    /// <summary>Returns true when an axis-aligned segment passes through a rectangle's strict interior.</summary>
    private static bool SegmentCrossesRect(Point2D a, Point2D b, Rect r)
    {
        if (Math.Abs(a.Y - b.Y) < 1e-9)
        {
            var y = a.Y;
            var xa = Math.Min(a.X, b.X);
            var xb = Math.Max(a.X, b.X);
            return r.Y < y && y < r.Y + r.Height && Math.Max(xa, r.X) < Math.Min(xb, r.X + r.Width);
        }

        var x = a.X;
        var ya = Math.Min(a.Y, b.Y);
        var yb = Math.Max(a.Y, b.Y);
        return r.X < x && x < r.X + r.Width && Math.Max(ya, r.Y) < Math.Min(yb, r.Y + r.Height);
    }
}
