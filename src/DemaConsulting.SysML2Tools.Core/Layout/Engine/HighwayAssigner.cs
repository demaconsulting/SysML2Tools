// <copyright file="HighwayAssigner.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout.Engine;

/// <summary>
/// A block participating in highway assignment, identified by its rectangle. Callers map results
/// back to model elements by index.
/// </summary>
/// <param name="X">Left edge in logical pixels.</param>
/// <param name="Y">Top edge in logical pixels.</param>
/// <param name="Width">Width in logical pixels.</param>
/// <param name="Height">Height in logical pixels.</param>
/// <param name="Id">Stable identifier, used only for deterministic tie-breaking.</param>
internal readonly record struct HighwayBox(double X, double Y, double Width, double Height, string Id);

/// <summary>
/// A connection between two blocks (by index) whose wire must be assigned to a corridor.
/// </summary>
/// <param name="FromBox">Index of the source block.</param>
/// <param name="ToBox">Index of the target block.</param>
/// <param name="ConnectorType">Connector category (e.g. "flow", "binding") used for bundling decisions.</param>
internal readonly record struct HighwayEdge(int FromBox, int ToBox, string ConnectorType);

/// <summary>
/// A routing corridor (a gap between two clusters of blocks) reserving width for the wires routed
/// through it. A corridor is promoted to a highway when its required width exceeds the minimum gap.
/// </summary>
/// <param name="IsHorizontal">True for a horizontal corridor (a gap between rows), false for vertical.</param>
/// <param name="Position">Centre position of the corridor on its perpendicular axis, in logical pixels.</param>
/// <param name="ReservedWidth">Width to reserve for the bundled wires, in logical pixels.</param>
/// <param name="IsHighway">True when the corridor carries more than one wire and needs a reserved trunk.</param>
internal readonly record struct Corridor(bool IsHorizontal, double Position, double ReservedWidth, bool IsHighway);

/// <summary>Maps one edge to the corridor it was coarse-routed through.</summary>
/// <param name="EdgeIndex">Index of the edge in the input list.</param>
/// <param name="CorridorIndex">Index of the corridor in the result; -1 when unassigned.</param>
internal readonly record struct EdgeAssignment(int EdgeIndex, int CorridorIndex);

/// <summary>The outcome of highway assignment.</summary>
/// <param name="Corridors">Corridors detected and sized, in detection order.</param>
/// <param name="Assignments">One assignment per input edge, in edge order.</param>
/// <param name="CostMultipliers">Routing-cost multiplier per corridor (highway 0.6, otherwise 1.0).</param>
internal sealed record HighwayResult(
    IReadOnlyList<Corridor> Corridors,
    IReadOnlyList<EdgeAssignment> Assignments,
    IReadOnlyList<double> CostMultipliers);

/// <summary>
/// Performs coarse "global routing" over the gaps between block clusters: it finds the horizontal
/// corridors between rows and vertical corridors between columns, coarse-routes each edge through the
/// least-congested corridor, measures peak concurrent wire occupancy, and reserves a trunk width for
/// corridors that carry too many wires to fit in a normal gap.
/// </summary>
/// <remarks>
/// Bundling wires into shared corridors keeps a dense diagram readable: rather than every connector
/// finding its own path, parallel wires collapse into highways with a discounted routing cost so the
/// detailed router prefers them. The assigner is pure, geometric, and deterministic — identical input
/// produces identical corridors and assignments — and references no SysML semantic type.
/// </remarks>
internal static class HighwayAssigner
{
    /// <summary>Cost multiplier applied to highway corridors so the detailed router bundles wires.</summary>
    private const double HighwayCostMultiplier = 0.6;

    /// <summary>Cost multiplier applied to ordinary (non-highway) corridors.</summary>
    private const double NormalCostMultiplier = 1.0;

    /// <summary>Tolerance within which block centres are treated as sharing a row or column.</summary>
    private const double ClusterTolerance = 1.0;

    /// <summary>
    /// Assigns each edge to a corridor, sizes the corridors, and produces per-corridor cost multipliers.
    /// </summary>
    /// <param name="boxes">Blocks to route between, in caller order. Must not be null.</param>
    /// <param name="edges">Connections to assign (indices into <paramref name="boxes"/>). Must not be null.</param>
    /// <param name="gridUnit">Grid unit; positions snap toward predictable lanes (must be positive to snap).</param>
    /// <param name="wireSpacing">Width one wire lane occupies inside a corridor, in logical pixels.</param>
    /// <param name="minGap">Minimum gap a corridor occupies before it must be widened into a highway.</param>
    /// <returns>A <see cref="HighwayResult"/>; an empty graph yields empty corridors, assignments, and multipliers.</returns>
    public static HighwayResult Assign(
        IReadOnlyList<HighwayBox> boxes,
        IReadOnlyList<HighwayEdge> edges,
        double gridUnit,
        double wireSpacing,
        double minGap)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        ArgumentNullException.ThrowIfNull(edges);

        // An empty graph has no corridors to detect and nothing to bundle.
        if (boxes.Count == 0 || edges.Count == 0)
        {
            return new HighwayResult([], [.. edges.Select((_, i) => new EdgeAssignment(i, -1))], []);
        }

        // Step 1: detect candidate corridors in the gaps between rows (horizontal) and columns (vertical).
        var horizontal = DetectCorridors(boxes, isHorizontal: true);
        var vertical = DetectCorridors(boxes, isHorizontal: false);
        var corridorCount = horizontal.Count + vertical.Count;

        // Step 2: coarse-route each edge into one corridor, preferring the lane with fewer wires.
        var wireCounts = new int[corridorCount];
        var lanes = new List<(double Entry, double Exit)>[corridorCount];
        for (var c = 0; c < corridorCount; c++)
        {
            lanes[c] = [];
        }

        var assignments = new EdgeAssignment[edges.Count];
        for (var e = 0; e < edges.Count; e++)
        {
            var corridor = RouteEdge(boxes, edges[e], horizontal, vertical, wireCounts);
            assignments[e] = new EdgeAssignment(e, corridor);
            if (corridor >= 0)
            {
                wireCounts[corridor]++;
                lanes[corridor].Add(SpanAlong(boxes, edges[e], corridor < horizontal.Count, horizontal, vertical));
            }
        }

        // Steps 3-5: size each corridor from its peak concurrent occupancy and derive its cost.
        var corridors = new Corridor[corridorCount];
        var multipliers = new double[corridorCount];
        for (var c = 0; c < corridorCount; c++)
        {
            var seed = c < horizontal.Count ? horizontal[c] : vertical[c - horizontal.Count];
            var peak = PeakConcurrency(lanes[c]);
            var required = (peak * wireSpacing) + (2.0 * minGap);
            var isHighway = required > minGap && peak > 1;
            var position = gridUnit > 0.0 ? Math.Round(seed.Position / gridUnit) * gridUnit : seed.Position;
            corridors[c] = seed with { Position = position, ReservedWidth = required, IsHighway = isHighway };
            multipliers[c] = isHighway ? HighwayCostMultiplier : NormalCostMultiplier;
        }

        return new HighwayResult(corridors, assignments, multipliers);
    }

    /// <summary>
    /// Detects corridors as the gaps between consecutive clusters of block centres along one axis: a
    /// horizontal corridor sits between two rows, a vertical corridor between two columns.
    /// </summary>
    private static IReadOnlyList<Corridor> DetectCorridors(IReadOnlyList<HighwayBox> boxes, bool isHorizontal)
    {
        // Cluster centre positions on the perpendicular axis (rows by Y, columns by X).
        var centres = boxes
            .Select(b => isHorizontal ? b.Y + (b.Height / 2.0) : b.X + (b.Width / 2.0))
            .Distinct()
            .OrderBy(v => v)
            .ToList();

        var clusters = new List<double>();
        foreach (var c in centres)
        {
            if (clusters.Count == 0 || c - clusters[^1] > ClusterTolerance)
            {
                clusters.Add(c);
            }
        }

        // One corridor between each adjacent pair of clusters, positioned at their midpoint.
        var result = new List<Corridor>();
        for (var i = 0; i + 1 < clusters.Count; i++)
        {
            var position = (clusters[i] + clusters[i + 1]) / 2.0;
            result.Add(new Corridor(isHorizontal, position, 0.0, false));
        }

        return result;
    }

    /// <summary>
    /// Coarse-routes an edge into one corridor: it prefers the perpendicular axis with the larger box
    /// separation, then picks the nearest corridor on that axis, breaking ties toward fewer wires.
    /// </summary>
    private static int RouteEdge(
        IReadOnlyList<HighwayBox> boxes,
        HighwayEdge edge,
        IReadOnlyList<Corridor> horizontal,
        IReadOnlyList<Corridor> vertical,
        int[] wireCounts)
    {
        if (edge.FromBox < 0 || edge.ToBox < 0 || edge.FromBox >= boxes.Count || edge.ToBox >= boxes.Count)
        {
            return -1;
        }

        var a = boxes[edge.FromBox];
        var b = boxes[edge.ToBox];
        var ay = a.Y + (a.Height / 2.0);
        var by = b.Y + (b.Height / 2.0);
        var ax = a.X + (a.Width / 2.0);
        var bx = b.X + (b.Width / 2.0);

        // A vertical separation favours a horizontal corridor; a horizontal separation favours a vertical one.
        var useHorizontal = Math.Abs(by - ay) >= Math.Abs(bx - ax);
        var corridors = useHorizontal ? horizontal : vertical;
        var offset = useHorizontal ? 0 : horizontal.Count;
        var target = useHorizontal ? (ay + by) / 2.0 : (ax + bx) / 2.0;

        return NearestLeastBusy(corridors, offset, target, wireCounts);
    }

    /// <summary>Picks the corridor nearest the target position, breaking ties toward the lane with fewer wires.</summary>
    private static int NearestLeastBusy(IReadOnlyList<Corridor> corridors, int offset, double target, int[] wireCounts)
    {
        var best = -1;
        var bestDist = double.PositiveInfinity;
        for (var i = 0; i < corridors.Count; i++)
        {
            var idx = offset + i;
            var dist = Math.Abs(corridors[i].Position - target);
            if (dist < bestDist - 1e-9 ||
                (Math.Abs(dist - bestDist) <= 1e-9 && best >= 0 && wireCounts[idx] < wireCounts[best]))
            {
                best = idx;
                bestDist = dist;
            }
        }

        return best;
    }

    /// <summary>Returns the edge's entry/exit span along the corridor's running axis for occupancy analysis.</summary>
    private static (double Entry, double Exit) SpanAlong(
        IReadOnlyList<HighwayBox> boxes,
        HighwayEdge edge,
        bool isHorizontal,
        IReadOnlyList<Corridor> horizontal,
        IReadOnlyList<Corridor> vertical)
    {
        _ = horizontal;
        _ = vertical;
        var a = boxes[edge.FromBox];
        var b = boxes[edge.ToBox];

        // Horizontal corridors run along X; vertical corridors run along Y.
        var pa = isHorizontal ? a.X + (a.Width / 2.0) : a.Y + (a.Height / 2.0);
        var pb = isHorizontal ? b.X + (b.Width / 2.0) : b.Y + (b.Height / 2.0);
        return (Math.Min(pa, pb), Math.Max(pa, pb));
    }

    /// <summary>
    /// Computes peak concurrent occupancy of a corridor with a sweep line over wire entry/exit
    /// positions; entries are processed before exits at coincident coordinates so a touching pair
    /// still counts as overlapping.
    /// </summary>
    private static int PeakConcurrency(IReadOnlyList<(double Entry, double Exit)> spans)
    {
        if (spans.Count == 0)
        {
            return 0;
        }

        // Each wire contributes a +1 event at its entry and a -1 event at its exit; sort entries first.
        var events = new List<(double Pos, int Delta)>();
        foreach (var (entry, exit) in spans)
        {
            events.Add((entry, +1));
            events.Add((exit, -1));
        }

        events.Sort((x, y) => x.Pos.CompareTo(y.Pos) != 0 ? x.Pos.CompareTo(y.Pos) : y.Delta.CompareTo(x.Delta));

        var current = 0;
        var peak = 0;
        foreach (var (_, delta) in events)
        {
            current += delta;
            peak = Math.Max(peak, current);
        }

        return peak;
    }
}
