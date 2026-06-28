// <copyright file="ConnectorLabelPlacer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout;

/// <summary>
/// Computes non-overlapping screen positions for connector (midpoint) labels.
/// </summary>
/// <remarks>
/// Each labelled line prefers the midpoint of its longest segment (an open run between boxes), but
/// when two labels would land on top of each other — for example where two connectors cross — the
/// placer falls back to a shorter segment or nudges the label perpendicular to its segment until it
/// no longer collides with an already-placed label. Lines are processed in the supplied order so the
/// result is deterministic. Both the SVG and PNG renderers share this logic so their label layouts
/// match.
/// </remarks>
public static class ConnectorLabelPlacer
{
    /// <summary>Approximate width of one character as a fraction of the font size.</summary>
    private const double CharWidthFactor = 0.6;

    /// <summary>Label box height as a multiple of the font size (cap height plus padding).</summary>
    private const double HeightFactor = 1.3;

    /// <summary>Extra clearance, in logical pixels, added around each label box when testing overlap.</summary>
    private const double Gap = 2.0;

    /// <summary>
    /// Computes a label position for every line that has a <see cref="LayoutLine.MidpointLabel"/>.
    /// </summary>
    /// <param name="lines">The lines to place labels for, in render order.</param>
    /// <param name="fontSize">Body font size, in logical pixels, used to estimate label box sizes.</param>
    /// <returns>
    /// A dictionary mapping each labelled line to its chosen (X, Y) label centre in logical pixels.
    /// Lines without a label are omitted.
    /// </returns>
    public static IReadOnlyDictionary<LayoutLine, (double X, double Y)> Place(
        IEnumerable<LayoutLine> lines,
        double fontSize)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var placed = new List<Rect>();
        var result = new Dictionary<LayoutLine, (double X, double Y)>();

        foreach (var line in lines)
        {
            if (line.MidpointLabel is null || line.Waypoints.Count == 0)
            {
                continue;
            }

            var halfWidth = (EstimateTextWidth(line.MidpointLabel, fontSize) / 2.0) + Gap;
            var halfHeight = (fontSize * HeightFactor / 2.0) + Gap;

            var position = ChoosePosition(line.Waypoints, halfWidth, halfHeight, placed);
            placed.Add(new Rect(position.X - halfWidth, position.Y - halfHeight, position.X + halfWidth, position.Y + halfHeight));
            result[line] = position;
        }

        return result;
    }

    /// <summary>Estimates the rendered width of a label string in logical pixels.</summary>
    /// <param name="text">The label text.</param>
    /// <param name="fontSize">Font size in logical pixels.</param>
    /// <returns>The approximate text width.</returns>
    private static double EstimateTextWidth(string text, double fontSize) =>
        text.Length * fontSize * CharWidthFactor;

    /// <summary>
    /// Selects a label position for a single line, preferring the midpoint of the longest segment and
    /// falling back to shorter segments or perpendicular nudges to avoid overlapping placed labels.
    /// </summary>
    /// <param name="waypoints">The line's waypoints.</param>
    /// <param name="halfWidth">Half the label box width (including gap).</param>
    /// <param name="halfHeight">Half the label box height (including gap).</param>
    /// <param name="placed">Boxes of labels already placed.</param>
    /// <returns>The chosen label centre.</returns>
    private static (double X, double Y) ChoosePosition(
        IReadOnlyList<Point2D> waypoints,
        double halfWidth,
        double halfHeight,
        List<Rect> placed)
    {
        if (waypoints.Count == 1)
        {
            return (waypoints[0].X, waypoints[0].Y);
        }

        // Segment midpoints ordered by descending length (longest, most-open run first).
        var segments = new List<(double Length, double X, double Y, double DirX, double DirY)>();
        for (var i = 0; i < waypoints.Count - 1; i++)
        {
            var a = waypoints[i];
            var b = waypoints[i + 1];
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var length = Math.Sqrt((dx * dx) + (dy * dy));
            var dirX = length > 1e-9 ? dx / length : 0.0;
            var dirY = length > 1e-9 ? dy / length : 0.0;
            segments.Add((length, (a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0, dirX, dirY));
        }

        segments.Sort((p, q) => q.Length.CompareTo(p.Length));

        // First pass: take the first segment midpoint that does not collide.
        var firstClear = segments
            .Where(seg => !Collides(seg.X, seg.Y, halfWidth, halfHeight, placed))
            .Select(seg => ((double X, double Y)?)(seg.X, seg.Y))
            .FirstOrDefault();
        if (firstClear is not null)
        {
            return firstClear.Value;
        }

        // Second pass: nudge along the longest segment's perpendicular until clear.
        var best = segments[0];
        var perpX = -best.DirY;
        var perpY = best.DirX;
        var step = (halfHeight * 2.0) + Gap;
        for (var k = 1; k <= 4; k++)
        {
            var offset = step * k;
            if (!Collides(best.X + (perpX * offset), best.Y + (perpY * offset), halfWidth, halfHeight, placed))
            {
                return (best.X + (perpX * offset), best.Y + (perpY * offset));
            }

            if (!Collides(best.X - (perpX * offset), best.Y - (perpY * offset), halfWidth, halfHeight, placed))
            {
                return (best.X - (perpX * offset), best.Y - (perpY * offset));
            }
        }

        // Give up: fall back to the longest segment's midpoint.
        return (best.X, best.Y);
    }

    /// <summary>Tests whether a candidate label box overlaps any already-placed box.</summary>
    /// <param name="centreX">Candidate box centre X.</param>
    /// <param name="centreY">Candidate box centre Y.</param>
    /// <param name="halfWidth">Half the candidate box width.</param>
    /// <param name="halfHeight">Half the candidate box height.</param>
    /// <param name="placed">Boxes already placed.</param>
    /// <returns><see langword="true"/> if the candidate overlaps a placed box.</returns>
    private static bool Collides(double centreX, double centreY, double halfWidth, double halfHeight, List<Rect> placed)
    {
        var left = centreX - halfWidth;
        var top = centreY - halfHeight;
        var right = centreX + halfWidth;
        var bottom = centreY + halfHeight;
        foreach (var r in placed)
        {
            if (left < r.Right && right > r.Left && top < r.Bottom && bottom > r.Top)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>An axis-aligned rectangle used for label overlap tests.</summary>
    /// <param name="Left">Left edge.</param>
    /// <param name="Top">Top edge.</param>
    /// <param name="Right">Right edge.</param>
    /// <param name="Bottom">Bottom edge.</param>
    private readonly record struct Rect(double Left, double Top, double Right, double Bottom);
}
