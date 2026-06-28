// <copyright file="GridQuantizer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout.Engine;

/// <summary>
/// A box to be quantised, identified by its position and size. Callers map results back to model
/// elements by index.
/// </summary>
/// <param name="X">Left edge in logical pixels.</param>
/// <param name="Y">Top edge in logical pixels.</param>
/// <param name="Width">Width in logical pixels.</param>
/// <param name="Height">Height in logical pixels.</param>
internal readonly record struct QuantizeBox(double X, double Y, double Width, double Height);

/// <summary>
/// Snaps box positions and sizes to a pixel grid and unifies the widths of boxes sharing a column
/// and the heights of boxes sharing a row, so aligned blocks become exactly equal and anchor points
/// fall on predictable grid lines.
/// </summary>
/// <remarks>
/// Columns and rows are detected by clustering left edges and top edges within a tolerance. Within a
/// cluster every box is widened (or heightened) to the largest member, never shrunk, so the pass is
/// non-overlapping and deterministic. Boxes in different columns are never unified together.
/// </remarks>
internal static class GridQuantizer
{
    /// <summary>
    /// Quantises every box to the grid and unifies aligned column widths and row heights.
    /// </summary>
    /// <param name="boxes">Boxes to quantise, in caller order.</param>
    /// <param name="gridUnit">Grid unit positions and sizes snap to.</param>
    /// <param name="clusterTolerance">Maximum edge-position difference for boxes to share a column/row.</param>
    /// <returns>The quantised rectangles, one per input box in input order.</returns>
    public static IReadOnlyList<PackedRect> Quantize(
        IReadOnlyList<QuantizeBox> boxes,
        double gridUnit,
        double clusterTolerance)
    {
        ArgumentNullException.ThrowIfNull(boxes);

        var n = boxes.Count;
        if (n == 0)
        {
            return [];
        }

        var x = new double[n];
        var y = new double[n];
        var w = new double[n];
        var h = new double[n];
        for (var i = 0; i < n; i++)
        {
            x[i] = Snap(boxes[i].X, gridUnit);
            y[i] = Snap(boxes[i].Y, gridUnit);
            w[i] = SnapUp(boxes[i].Width, gridUnit);
            h[i] = SnapUp(boxes[i].Height, gridUnit);
        }

        Unify(x, w, clusterTolerance);
        Unify(y, h, clusterTolerance);

        var rects = new PackedRect[n];
        for (var i = 0; i < n; i++)
        {
            rects[i] = new PackedRect(x[i], y[i], w[i], h[i]);
        }

        return rects;
    }

    /// <summary>Unifies the extents of boxes whose start edges cluster within the tolerance to the widest.</summary>
    private static void Unify(double[] start, double[] extent, double tolerance)
    {
        var order = Enumerable.Range(0, start.Length).OrderBy(i => start[i]).ToList();
        var c = 0;
        while (c < order.Count)
        {
            var anchor = start[order[c]];
            var max = extent[order[c]];
            var group = new List<int> { order[c] };
            var k = c + 1;
            while (k < order.Count && start[order[k]] - anchor <= tolerance)
            {
                group.Add(order[k]);
                max = Math.Max(max, extent[order[k]]);
                k++;
            }

            foreach (var idx in group)
            {
                extent[idx] = max;
            }

            c = k;
        }
    }

    /// <summary>Snaps a coordinate to the nearest multiple of the grid unit when one is supplied.</summary>
    private static double Snap(double value, double gridUnit) =>
        gridUnit > 0.0 ? Math.Round(value / gridUnit) * gridUnit : value;

    /// <summary>Rounds an extent up to the next multiple of the grid unit when one is supplied.</summary>
    private static double SnapUp(double value, double gridUnit) =>
        gridUnit > 0.0 ? Math.Ceiling(value / gridUnit) * gridUnit : value;
}
