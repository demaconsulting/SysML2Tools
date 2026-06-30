// <copyright file="PngEndMarkerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout;
using DemaConsulting.SysML2Tools.Png;
using DemaConsulting.SysML2Tools.Rendering;
using SkiaSharp;

namespace DemaConsulting.SysML2Tools.Png.Tests;

/// <summary>
///     Tests for PNG line-end (connector decoration) markers. These confirm that the open chevron
///     is drawn OPEN (two strokes, no closing base edge), and that the PNG marker geometry matches
///     the shared <see cref="NotationMetrics"/> source used by the SVG renderer (along-line overshoot
///     and across-line width), so SVG and PNG produce identical end-marker geometry.
/// </summary>
public sealed class PngEndMarkerTests
{
    private static SKBitmap RenderToBitmap(LayoutTree layout, RenderOptions options)
    {
        var renderer = new PngRenderer();
        using var ms = new MemoryStream();
        renderer.Render(layout, options, ms);
        ms.Position = 0;
        using var data = SKData.Create(ms);
        return SKBitmap.Decode(data);
    }

    /// <summary>A pixel is "ink" when it is noticeably darker than the white background.</summary>
    private static bool IsInk(SKColor c) => c.Alpha > 32 && (c.Red + c.Green + c.Blue) < 600;

    /// <summary>Counts ink pixels within the given inclusive pixel rectangle.</summary>
    private static int CountInk(SKBitmap bmp, int x0, int y0, int x1, int y1)
    {
        var count = 0;
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                if (IsInk(bmp.GetPixel(x, y)))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static LayoutTree HorizontalLineTo(EndMarkerStyle target)
    {
        // Horizontal line from (10,50) to (150,50); marker is drawn at the (150,50) endpoint.
        var line = new LayoutLine(
            [new Point2D(10, 50), new Point2D(150, 50)],
            EndMarkerStyle.None,
            target,
            LineStyle.Solid,
            null);
        return new LayoutTree(200, 100, [line]);
    }

    /// <summary>
    ///     The open chevron is drawn OPEN: it has strictly fewer ink pixels in the marker zone than
    ///     the closed hollow triangle (which adds the closing base edge).
    /// </summary>
    [Fact]
    public void OpenChevron_HasFewerInkPixelsThanClosedTriangle()
    {
        var options = new RenderOptions(Themes.Light);
        using var chevron = RenderToBitmap(HorizontalLineTo(EndMarkerStyle.OpenChevron), options);
        using var triangle = RenderToBitmap(HorizontalLineTo(EndMarkerStyle.HollowTriangle), options);

        // Marker zone: the back edge of the triangle marker sits near x=141 (refX 9 back from the
        // x=150 endpoint). Count ink strictly behind the shaft join so the base edge is isolated.
        var chevronInk = CountInk(chevron, 138, 42, 144, 58);
        var triangleInk = CountInk(triangle, 138, 42, 144, 58);

        Assert.True(
            chevronInk < triangleInk,
            $"Expected open chevron ({chevronInk}) to have fewer base-edge ink pixels than closed triangle ({triangleInk}).");
    }

    /// <summary>
    ///     The PNG filled-arrow spans <see cref="NotationMetrics.EndMarkerLength"/> along the line —
    ///     from the base (at <see cref="NotationMetrics.EndMarkerRefX"/> behind the endpoint) to the apex
    ///     (overshooting the endpoint) — matching the SVG marker box length. Measured from the widest
    ///     (base) column to the furthest ink column.
    /// </summary>
    [Fact]
    public void FilledArrow_AlongLength_MatchesNotationMetrics()
    {
        var options = new RenderOptions(Themes.Light);
        using var bmp = RenderToBitmap(HorizontalLineTo(EndMarkerStyle.FilledArrow), options);

        // For each column near the endpoint, measure the vertical ink extent. The base column has the
        // greatest extent (full marker width); the tip is the furthest column carrying marker ink.
        var baseX = -1;
        var baseExtent = 0;
        var tipX = -1;
        for (var x = 135; x <= 155; x++)
        {
            var minY = int.MaxValue;
            var maxY = int.MinValue;
            for (var y = 43; y <= 57; y++)
            {
                if (IsInk(bmp.GetPixel(x, y)))
                {
                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                }
            }

            if (maxY < minY)
            {
                continue;
            }

            var extent = maxY - minY + 1;
            if (extent > baseExtent)
            {
                baseExtent = extent;
                baseX = x;
            }

            tipX = x; // furthest column with any ink
        }

        Assert.True(baseX >= 0, "Expected to locate the marker base column.");
        var alongLength = tipX - baseX;

        // Allow +/-2 px for anti-aliasing around the canonical 10-unit along length.
        Assert.InRange(alongLength, NotationMetrics.EndMarkerLength - 2, NotationMetrics.EndMarkerLength + 2);
    }

    /// <summary>
    ///     The PNG arrow base spans the shared <see cref="NotationMetrics.EndMarkerWidth"/> across the
    ///     line, matching the SVG marker height. Measured at the marker base column.
    /// </summary>
    [Fact]
    public void FilledArrow_BaseWidth_MatchesNotationMetrics()
    {
        var options = new RenderOptions(Themes.Light);
        using var bmp = RenderToBitmap(HorizontalLineTo(EndMarkerStyle.FilledArrow), options);

        // Base column: refX (9) back from the x=150 endpoint => x≈141.
        var baseX = (int)Math.Round(150 - NotationMetrics.EndMarkerRefX);
        var minY = int.MaxValue;
        var maxY = int.MinValue;
        for (var y = 40; y <= 60; y++)
        {
            if (IsInk(bmp.GetPixel(baseX, y)))
            {
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }
        }

        Assert.True(maxY >= minY, "Expected ink at the marker base column.");
        var measuredWidth = maxY - minY + 1;

        // Allow +/-2 px for anti-aliasing around the canonical 7-unit width.
        Assert.InRange(measuredWidth, NotationMetrics.EndMarkerWidth - 2, NotationMetrics.EndMarkerWidth + 2);
    }
}
