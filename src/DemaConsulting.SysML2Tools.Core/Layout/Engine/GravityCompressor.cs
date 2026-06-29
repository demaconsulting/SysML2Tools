// <copyright file="GravityCompressor.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout.Engine;

/// <summary>
/// A box to be compressed, carrying its current position and its true (label-inclusive) minimum
/// extent. Callers map results back to model elements by index.
/// </summary>
/// <param name="X">Current left edge in logical pixels.</param>
/// <param name="Y">Current top edge in logical pixels.</param>
/// <param name="Width">Drawn width in logical pixels.</param>
/// <param name="Height">Drawn height in logical pixels.</param>
/// <param name="MinW">Minimum width that must be kept clear (includes label overflow).</param>
/// <param name="MinH">Minimum height that must be kept clear (includes label overflow).</param>
internal readonly record struct CompressBox(double X, double Y, double Width, double Height, double MinW, double MinH);

/// <summary>A compressed box position.</summary>
/// <param name="X">Resulting left edge in logical pixels.</param>
/// <param name="Y">Resulting top edge in logical pixels.</param>
internal readonly record struct CompressedPosition(double X, double Y);

/// <summary>The result of a compression pass.</summary>
/// <param name="Positions">Resulting positions, one per input box in input order.</param>
/// <param name="Feasible">True when a non-overlapping arrangement was achieved.</param>
internal sealed record CompressResult(IReadOnlyList<CompressedPosition> Positions, bool Feasible);

/// <summary>
/// A corridor floor that must remain clear between adjacent block clusters: boxes on either side of
/// the corridor are pushed apart until the gap is at least <paramref name="MinWidth"/>.
/// </summary>
/// <param name="IsHorizontal">True for a horizontal corridor (a gap between rows), false for vertical.</param>
/// <param name="Position">Centre of the corridor on its perpendicular axis, in logical pixels.</param>
/// <param name="MinWidth">Minimum clear width the corridor must keep, in logical pixels.</param>
internal readonly record struct CorridorConstraint(bool IsHorizontal, double Position, double MinWidth);

/// <summary>
/// Removes overlaps between placed boxes by separating each colliding pair to exactly the requested
/// minimum gap, leaving already-clear pairs untouched. Box positions move monotonically apart along
/// the axis of least penetration; the label-inclusive minimum extents reserve clearance for labels.
/// </summary>
/// <remarks>
/// When corridor constraints are supplied, the pass first widens each corridor gap to its reserved
/// width (so highway trunks fit) before separating residual overlaps; when no corridors are supplied
/// the behaviour is unchanged. The pass is deterministic and order-stable.
/// </remarks>
internal static class GravityCompressor
{
    /// <summary>Maximum separation passes before the arrangement is declared infeasible.</summary>
    private const int MaxPasses = 500;

    /// <summary>
    /// Separates overlapping boxes so every pair keeps at least <paramref name="minGap"/> clearance.
    /// </summary>
    /// <param name="boxes">Boxes to compress, in caller order.</param>
    /// <param name="minGap">Minimum clearance to keep between any two boxes.</param>
    /// <param name="gridUnit">Grid unit the resulting positions snap to (ignored when not positive).</param>
    /// <param name="corridors">Optional corridor floors; each gap is widened to its reserved width before separation.</param>
    /// <returns>A <see cref="CompressResult"/> with one position per box and a feasibility flag.</returns>
    public static CompressResult Compress(
        IReadOnlyList<CompressBox> boxes,
        double minGap,
        double gridUnit,
        IReadOnlyList<CorridorConstraint>? corridors = null)
    {
        ArgumentNullException.ThrowIfNull(boxes);

        var n = boxes.Count;
        if (n == 0)
        {
            return new CompressResult([], true);
        }

        if (minGap < 0.0)
        {
            return new CompressResult([.. boxes.Select(b => new CompressedPosition(b.X, b.Y))], false);
        }

        var px = new double[n];
        var py = new double[n];
        var halfW = new double[n];
        var halfH = new double[n];
        for (var i = 0; i < n; i++)
        {
            halfW[i] = Math.Max(boxes[i].Width, boxes[i].MinW) / 2.0;
            halfH[i] = Math.Max(boxes[i].Height, boxes[i].MinH) / 2.0;
            px[i] = boxes[i].X + (boxes[i].Width / 2.0);
            py[i] = boxes[i].Y + (boxes[i].Height / 2.0);
        }

        // Open each corridor to its reserved width before pairwise separation, so highway trunks fit.
        if (corridors is { Count: > 0 })
        {
            EnsureCorridors(corridors, boxes, px, py, halfW, halfH);
        }

        var feasible = SeparatePairs(n, px, py, halfW, halfH, minGap);

        var positions = new CompressedPosition[n];
        for (var i = 0; i < n; i++)
        {
            var x = px[i] - (boxes[i].Width / 2.0);
            var y = py[i] - (boxes[i].Height / 2.0);
            positions[i] = new CompressedPosition(Snap(x, gridUnit), Snap(y, gridUnit));
        }

        return new CompressResult(positions, feasible);
    }

    /// <summary>
    /// Widens each corridor gap to its reserved width by pushing the boxes on either side of the
    /// corridor outward symmetrically. A horizontal corridor pushes along Y, a vertical along X.
    /// </summary>
    private static void EnsureCorridors(
        IReadOnlyList<CorridorConstraint> corridors,
        IReadOnlyList<CompressBox> boxes,
        double[] px,
        double[] py,
        double[] halfW,
        double[] halfH)
    {
        var n = boxes.Count;
        foreach (var corridor in corridors)
        {
            // Centres on the corridor's perpendicular axis and their half-extents toward the corridor.
            var centres = corridor.IsHorizontal ? py : px;
            var halves = corridor.IsHorizontal ? halfH : halfW;

            // Each box's nearest edge to the corridor centre defines the gap to be widened.
            var lowEdge = double.NegativeInfinity;
            var highEdge = double.PositiveInfinity;
            for (var i = 0; i < n; i++)
            {
                if (centres[i] <= corridor.Position)
                {
                    lowEdge = Math.Max(lowEdge, centres[i] + halves[i]);
                }
                else
                {
                    highEdge = Math.Min(highEdge, centres[i] - halves[i]);
                }
            }

            if (double.IsInfinity(lowEdge) || double.IsInfinity(highEdge))
            {
                continue;
            }

            // If the existing gap is already wide enough, leave the boxes untouched.
            var deficit = corridor.MinWidth - (highEdge - lowEdge);
            if (deficit <= 0.0)
            {
                continue;
            }

            var shift = deficit / 2.0;
            for (var i = 0; i < n; i++)
            {
                if (centres[i] <= corridor.Position)
                {
                    centres[i] -= shift;
                }
                else
                {
                    centres[i] += shift;
                }
            }
        }
    }

    /// <summary>Iteratively pushes apart overlapping boxes along the least-penetration axis.</summary>
    private static bool SeparatePairs(int n, double[] px, double[] py, double[] halfW, double[] halfH, double minGap)
    {
        for (var pass = 0; pass < MaxPasses; pass++)
        {
            var moved = false;
            for (var i = 0; i < n; i++)
            {
                for (var j = i + 1; j < n; j++)
                {
                    var minDx = halfW[i] + halfW[j] + minGap;
                    var minDy = halfH[i] + halfH[j] + minGap;
                    var dx = px[j] - px[i];
                    var dy = py[j] - py[i];
                    var overlapX = minDx - Math.Abs(dx);
                    var overlapY = minDy - Math.Abs(dy);
                    if (overlapX <= 0.0 || overlapY <= 0.0)
                    {
                        continue;
                    }

                    if (overlapX < overlapY)
                    {
                        var shift = (overlapX / 2.0) * (dx < 0 ? -1.0 : 1.0);
                        px[i] -= shift;
                        px[j] += shift;
                    }
                    else
                    {
                        var shift = (overlapY / 2.0) * (dy < 0 ? -1.0 : 1.0);
                        py[i] -= shift;
                        py[j] += shift;
                    }

                    moved = true;
                }
            }

            if (!moved)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Snaps a coordinate to the nearest multiple of the grid unit when one is supplied.</summary>
    private static double Snap(double value, double gridUnit) =>
        gridUnit > 0.0 ? Math.Round(value / gridUnit) * gridUnit : value;
}
