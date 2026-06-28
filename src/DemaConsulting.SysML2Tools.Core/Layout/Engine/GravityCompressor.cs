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
/// Removes overlaps between placed boxes by separating each colliding pair to exactly the requested
/// minimum gap, leaving already-clear pairs untouched. Box positions move monotonically apart along
/// the axis of least penetration; the label-inclusive minimum extents reserve clearance for labels.
/// </summary>
/// <remarks>
/// The optional corridor argument is accepted for forward compatibility with highway-constrained
/// compression and is ignored in this phase. The pass is deterministic and order-stable.
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
    /// <param name="corridor">Reserved corridor constraints; ignored in this phase.</param>
    /// <returns>A <see cref="CompressResult"/> with one position per box and a feasibility flag.</returns>
    public static CompressResult Compress(
        IReadOnlyList<CompressBox> boxes,
        double minGap,
        double gridUnit,
        IReadOnlyList<object>? corridor = null)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        _ = corridor; // Reserved for highway-constrained compression (phase 14b).

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
