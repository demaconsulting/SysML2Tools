// <copyright file="LabelClearance.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;
using DemaConsulting.SysML2Tools.Stdlib;

namespace DemaConsulting.SysML2Tools.Tests.Layout;

/// <summary>
///     Test helpers that load a gallery model (failing, never skipping, when it is missing) and check
///     that every placed connector label is drawn clear of the state/action boxes, of every other
///     connector, and of the label's own connector's end decorations.
/// </summary>
/// <remarks>
///     The renderers draw an opaque background around each label, wider than the text rectangle the
///     label placer reasons about, so every label rectangle is inflated by <see cref="BackgroundPad"/>
///     before it is compared with the geometry it must not hide.
/// </remarks>
internal static class LabelClearance
{
    /// <summary>
    ///     The padding the renderers' opaque label background adds above and below the placer's text
    ///     rectangle. The placer's rectangle is already wider than the rendered text (0.6 em per
    ///     character against about 0.52 em for the real glyphs, plus a 2 px gap), so no horizontal
    ///     padding is added.
    /// </summary>
    public const double BackgroundPad = 4.0;

    /// <summary>
    ///     Loads a gallery model from <c>docs/gallery/models</c> (found by walking up from the test
    ///     binary). A gallery model that cannot be found is a failure, not a skipped test.
    /// </summary>
    /// <param name="fileName">Gallery model file name.</param>
    /// <returns>The loaded workspace.</returns>
    public static async Task<SysmlWorkspace> LoadGalleryWorkspaceAsync(string fileName)
    {
        var dir = AppContext.BaseDirectory;
        string? modelPath = null;
        while (dir is not null && modelPath is null)
        {
            var candidate = Path.Combine(dir, "docs", "gallery", "models", fileName);
            modelPath = File.Exists(candidate) ? candidate : null;
            dir = Directory.GetParent(dir)?.FullName;
        }

        if (modelPath is null)
        {
            Assert.Fail($"Gallery model not found: {fileName}");
        }

        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([modelPath], stdlibTable);
        Assert.NotNull(result.Workspace);
        return result.Workspace;
    }

    /// <summary>
    ///     Returns a description of every label that, with its background padding, hides something it
    ///     must not: a leaf box, a segment of another connector, or the first
    ///     <paramref name="startZone"/> / last <paramref name="endZone"/> of its own connector.
    /// </summary>
    /// <param name="content">The nodes to check; the leaf boxes and all lines are taken from here.</param>
    /// <param name="theme">Theme whose font size and marker scale the renderers use.</param>
    /// <param name="startZone">Length of the own connector's start that must stay visible.</param>
    /// <param name="endZone">Length of the own connector's end (its arrowhead) that must stay visible.</param>
    /// <param name="containers">Container boxes (such as a frame) the renderers also pass to the label placer.</param>
    /// <returns>One message per violation; empty when every label is clear.</returns>
    public static List<string> FindViolations(
        IReadOnlyList<LayoutNode> content,
        Theme theme,
        double startZone,
        double endZone,
        IReadOnlyList<LayoutBox>? containers = null)
    {
        var boxes = content.OfType<LayoutBox>().Where(b => b.Children.Count == 0).ToList();
        var lines = content.OfType<LayoutLine>().ToList();
        var labeled = lines.Where(l => l.MidpointLabel is not null).ToList();
        var violations = new List<string>();
        if (labeled.Count == 0)
        {
            return violations;
        }

        // The renderers hand the placer every box, containers (the frame) included.
        var obstacles = (containers ?? []).Concat(boxes).ToList();
        var placements = ConnectorLabelPlacer.Place(labeled, theme.FontSizeBody, obstacles, NotationMetrics.MarkerScale(theme));
        foreach (var line in labeled)
        {
            var p = placements[line];
            var rect = (L: p.X - p.HalfWidth, T: p.Y - p.HalfHeight - BackgroundPad, R: p.X + p.HalfWidth, B: p.Y + p.HalfHeight + BackgroundPad);

            foreach (var box in boxes)
            {
                if (rect.L < box.X + box.Width && rect.R > box.X && rect.T < box.Y + box.Height && rect.B > box.Y)
                {
                    violations.Add($"Label '{line.MidpointLabel}' at ({p.X:F1},{p.Y:F1}) covers box '{box.Label}' ({box.X:F1},{box.Y:F1},{box.Width:F1}x{box.Height:F1}).");
                }
            }

            foreach (var other in lines.Where(l => !ReferenceEquals(l, line)))
            {
                if (PathHits(other.Waypoints, rect))
                {
                    violations.Add($"Label '{line.MidpointLabel}' at ({p.X:F1},{p.Y:F1}) covers another connector ending at {other.Waypoints[^1]}.");
                }
            }

            if (PathHits(PathPrefix(line.Waypoints, startZone), rect))
            {
                violations.Add($"Label '{line.MidpointLabel}' covers the first {startZone} px of its own connector.");
            }

            if (PathHits([.. PathPrefix([.. line.Waypoints.Reverse()], endZone)], rect))
            {
                violations.Add($"Label '{line.MidpointLabel}' covers the last {endZone} px of its own connector.");
            }
        }

        return violations;
    }

    /// <summary>Returns the leading part of a polyline no longer than <paramref name="length"/>.</summary>
    private static List<Point2D> PathPrefix(IReadOnlyList<Point2D> points, double length)
    {
        var result = new List<Point2D> { points[0] };
        var remaining = length;
        for (var i = 1; i < points.Count && remaining > 0.0; i++)
        {
            var (dx, dy) = (points[i].X - points[i - 1].X, points[i].Y - points[i - 1].Y);
            var segment = Math.Sqrt((dx * dx) + (dy * dy));
            if (segment <= remaining)
            {
                result.Add(points[i]);
                remaining -= segment;
                continue;
            }

            var t = remaining / segment;
            result.Add(new Point2D(points[i - 1].X + (dx * t), points[i - 1].Y + (dy * t)));
            break;
        }

        return result;
    }

    /// <summary>Returns whether any segment of the polyline passes through the interior of the rectangle.</summary>
    private static bool PathHits(IReadOnlyList<Point2D> points, (double L, double T, double R, double B) rect)
    {
        for (var i = 0; i + 1 < points.Count; i++)
        {
            if (SegmentHits(points[i], points[i + 1], rect))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Clips a segment against the interior of a rectangle (slab method).</summary>
    private static bool SegmentHits(Point2D a, Point2D b, (double L, double T, double R, double B) rect)
    {
        var (dx, dy) = (b.X - a.X, b.Y - a.Y);
        double[] p = [-dx, dx, -dy, dy];
        double[] q = [a.X - rect.L, rect.R - a.X, a.Y - rect.T, rect.B - a.Y];
        var (lower, upper) = (0.0, 1.0);
        for (var i = 0; i < 4; i++)
        {
            if (Math.Abs(p[i]) < 1e-9)
            {
                if (q[i] <= 0.0)
                {
                    return false;
                }

                continue;
            }

            var r = q[i] / p[i];
            if (p[i] < 0.0)
            {
                lower = Math.Max(lower, r);
            }
            else
            {
                upper = Math.Min(upper, r);
            }
        }

        return lower < upper - 1e-9;
    }
}
