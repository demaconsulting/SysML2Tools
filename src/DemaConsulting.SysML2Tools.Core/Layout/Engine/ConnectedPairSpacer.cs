// <copyright file="ConnectedPairSpacer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout.Engine;

/// <summary>
/// A connected pair of boxes, identified by their indices into a box array, that must keep a clear
/// approach zone between their facing edges so the connector between them is visible.
/// </summary>
/// <param name="IndexA">Index of the first box.</param>
/// <param name="IndexB">Index of the second box.</param>
internal readonly record struct ConnectedPair(int IndexA, int IndexB);

/// <summary>
/// Pushes connected box pairs apart along their dominant axis until each pair leaves room for two
/// approach zones (one per box face), so the connector between two boxes that share a boundary edge
/// is never invisible. Unconnected boxes and already-separated pairs are left untouched.
/// </summary>
/// <remarks>
/// The primary axis matches the routing direction: X when the centres are farther apart horizontally
/// than vertically, otherwise Y (ties pick X for determinism). Only the gap on that axis is widened,
/// so grid alignment on the perpendicular axis is preserved. Sizes never change. If the pass cannot
/// converge within the bound, the original arrangement is returned unchanged.
/// </remarks>
internal static class ConnectedPairSpacer
{
    /// <summary>
    /// Spaces connected box pairs so each pair clears two approach zones on its dominant axis.
    /// </summary>
    /// <param name="boxes">Boxes to space, in caller order; sizes are preserved.</param>
    /// <param name="pairs">Connected pairs referencing indices into <paramref name="boxes"/>.</param>
    /// <param name="approachZone">Required clear distance off each box face; the needed gap is twice this.</param>
    /// <param name="maxPasses">Maximum relaxation passes before declaring non-convergence.</param>
    /// <returns>Adjusted rectangles, or the input unchanged if no separation is needed or it did not converge.</returns>
    public static Rect[] Space(Rect[] boxes, IReadOnlyList<ConnectedPair> pairs, double approachZone, int maxPasses = 500)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        ArgumentNullException.ThrowIfNull(pairs);

        if (boxes.Length == 0 || pairs.Count == 0)
        {
            return boxes;
        }

        var result = (Rect[])boxes.Clone();
        var gapNeeded = 2.0 * approachZone;

        for (var pass = 0; pass < maxPasses; pass++)
        {
            var moved = false;
            foreach (var pair in pairs)
            {
                var a = result[pair.IndexA];
                var b = result[pair.IndexB];

                var cxA = a.X + (a.Width / 2.0);
                var cyA = a.Y + (a.Height / 2.0);
                var cxB = b.X + (b.Width / 2.0);
                var cyB = b.Y + (b.Height / 2.0);

                var horizontal = Math.Abs(cxB - cxA) >= Math.Abs(cyB - cyA);

                double facingGap;
                if (horizontal)
                {
                    facingGap = cxB >= cxA ? b.X - (a.X + a.Width) : a.X - (b.X + b.Width);
                }
                else
                {
                    facingGap = cyB >= cyA ? b.Y - (a.Y + a.Height) : a.Y - (b.Y + b.Height);
                }

                facingGap = Math.Max(0.0, facingGap);
                if (facingGap >= gapNeeded)
                {
                    continue;
                }

                var push = (gapNeeded - facingGap) / 2.0;
                if (horizontal)
                {
                    var sign = cxB >= cxA ? 1.0 : -1.0;
                    result[pair.IndexA] = a with { X = a.X - (sign * push) };
                    result[pair.IndexB] = b with { X = b.X + (sign * push) };
                }
                else
                {
                    var sign = cyB >= cyA ? 1.0 : -1.0;
                    result[pair.IndexA] = a with { Y = a.Y - (sign * push) };
                    result[pair.IndexB] = b with { Y = b.Y + (sign * push) };
                }

                moved = true;
            }

            if (!moved)
            {
                return result;
            }
        }

        // Did not converge within the bound; fall back to the input positions.
        return boxes;
    }
}
