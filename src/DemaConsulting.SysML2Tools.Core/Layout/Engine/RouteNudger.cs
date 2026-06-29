// <copyright file="RouteNudger.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout.Engine;

/// <summary>
/// Post-routing pass that separates coincident connector segments: finds pairs of routes that share
/// a vertical (or horizontal) segment and nudges each to its own lane so they no longer overlap.
/// </summary>
/// <remarks>
/// Only vertical segments have their X nudged (preserving orthogonality), and only horizontal
/// segments have their Y nudged. Conflicting segments are detected by grouping axis-aligned segments
/// whose shared coordinate lies within a small tolerance, then checking for overlap on the
/// perpendicular axis. Within each conflict group, segments are sorted by route index for a
/// deterministic lane assignment: lane 0 stays at the original coordinate, lane 1 is nudged by one
/// <c>edgeSpacing</c>, lane 2 by two, and so on. This keeps the most-senior route on the original
/// track while spreading later routes outward.
/// </remarks>
internal static class RouteNudger
{
    /// <summary>Tolerance below which two coordinates on the same axis are treated as coincident.</summary>
    private const double CoincidenceTolerance = 1e-6;

    /// <summary>Tolerance within which two axis coordinates are grouped into the same lane band.</summary>
    private const double GroupTolerance = 0.5;

    /// <summary>
    /// Nudges conflicting co-linear segments in the given routes so they no longer overlap.
    /// </summary>
    /// <param name="routes">The routes to inspect and adjust; each is an ordered list of waypoints.</param>
    /// <param name="edgeSpacing">Perpendicular distance between adjacent nudged lanes.</param>
    /// <returns>New routes with conflicting segments separated onto distinct lanes.</returns>
    public static IReadOnlyList<IReadOnlyList<Point2D>> NudgeConflicts(
        IReadOnlyList<IReadOnlyList<Point2D>> routes,
        double edgeSpacing)
    {
        ArgumentNullException.ThrowIfNull(routes);

        if (routes.Count <= 1)
        {
            return routes;
        }

        // Make mutable copies of each route's waypoints so nudging does not alter the originals.
        var pts = routes.Select(r => r.ToArray()).ToArray();

        // Separate coincident vertical segments (nudge X) then horizontal segments (nudge Y).
        NudgeAxis(pts, edgeSpacing, vertical: true);
        NudgeAxis(pts, edgeSpacing, vertical: false);

        return [.. pts.Select(p => (IReadOnlyList<Point2D>)p)];
    }

    /// <summary>
    /// Identifies all segments aligned to one axis (vertical or horizontal), groups them by their
    /// shared axis coordinate, detects overlapping pairs, and nudges conflicting groups onto distinct
    /// perpendicular lanes so they no longer coincide.
    /// </summary>
    /// <param name="routes">Mutable route arrays to update in place.</param>
    /// <param name="edgeSpacing">Step distance between consecutive lanes.</param>
    /// <param name="vertical">
    /// <see langword="true"/> to process vertical segments (nudge X); <see langword="false"/> for
    /// horizontal segments (nudge Y).
    /// </param>
    private static void NudgeAxis(Point2D[][] routes, double edgeSpacing, bool vertical)
    {
        // Collect all axis-aligned segments: (axisCoord, rangeMin, rangeMax, routeIndex, segIndex).
        var segments = new List<(double Coord, double Min, double Max, int RouteIdx, int SegIdx)>();
        for (var r = 0; r < routes.Length; r++)
        {
            var p = routes[r];
            for (var s = 0; s < p.Length - 1; s++)
            {
                if (vertical && Math.Abs(p[s].X - p[s + 1].X) < CoincidenceTolerance)
                {
                    // Vertical segment: axis coordinate is X, perpendicular range is Y.
                    segments.Add((p[s].X, Math.Min(p[s].Y, p[s + 1].Y), Math.Max(p[s].Y, p[s + 1].Y), r, s));
                }
                else if (!vertical && Math.Abs(p[s].Y - p[s + 1].Y) < CoincidenceTolerance)
                {
                    // Horizontal segment: axis coordinate is Y, perpendicular range is X.
                    segments.Add((p[s].Y, Math.Min(p[s].X, p[s + 1].X), Math.Max(p[s].X, p[s + 1].X), r, s));
                }
            }
        }

        if (segments.Count <= 1)
        {
            return;
        }

        // Group segments whose axis coordinates are within GroupTolerance of each other.
        var groupCoords = new List<double>();
        var groupMembers = new List<List<int>>();
        for (var i = 0; i < segments.Count; i++)
        {
            var assigned = false;
            for (var g = 0; g < groupCoords.Count; g++)
            {
                if (Math.Abs(segments[i].Coord - groupCoords[g]) <= GroupTolerance)
                {
                    groupMembers[g].Add(i);
                    assigned = true;
                    break;
                }
            }

            if (!assigned)
            {
                groupCoords.Add(segments[i].Coord);
                groupMembers.Add([i]);
            }
        }

        // Process each group: detect conflicts and nudge conflicting segments onto distinct lanes.
        for (var g = 0; g < groupMembers.Count; g++)
        {
            var members = groupMembers[g];
            if (members.Count <= 1)
            {
                continue;
            }

            // Check whether any two members overlap on the perpendicular axis.
            var hasConflict = false;
            for (var i = 0; i < members.Count && !hasConflict; i++)
            {
                var si = segments[members[i]];
                for (var j = i + 1; j < members.Count && !hasConflict; j++)
                {
                    var sj = segments[members[j]];
                    if (si.Min < sj.Max && sj.Min < si.Max)
                    {
                        hasConflict = true;
                    }
                }
            }

            if (!hasConflict)
            {
                continue;
            }

            // Sort by route index for stable, deterministic lane assignment across identical inputs.
            members.Sort((a, b) => segments[a].RouteIdx.CompareTo(segments[b].RouteIdx));

            // Assign each position in the sorted list its own lane; lane 0 stays at baseCoord.
            var baseCoord = groupCoords[g];
            for (var k = 0; k < members.Count; k++)
            {
                if (k == 0)
                {
                    // Lane 0 keeps the original coordinate — no movement required.
                    continue;
                }

                var (_, _, _, routeIdx, segIdx) = segments[members[k]];
                var newCoord = baseCoord + (k * edgeSpacing);
                var p = routes[routeIdx];

                if (vertical)
                {
                    // Nudge the vertical segment's endpoints along X to the assigned lane.
                    p[segIdx] = p[segIdx] with { X = newCoord };
                    p[segIdx + 1] = p[segIdx + 1] with { X = newCoord };
                }
                else
                {
                    // Nudge the horizontal segment's endpoints along Y to the assigned lane.
                    p[segIdx] = p[segIdx] with { Y = newCoord };
                    p[segIdx + 1] = p[segIdx + 1] with { Y = newCoord };
                }
            }
        }
    }
}
