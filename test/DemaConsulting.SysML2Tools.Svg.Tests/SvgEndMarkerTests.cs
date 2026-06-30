// <copyright file="SvgEndMarkerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Svg;

namespace DemaConsulting.SysML2Tools.Svg.Tests;

/// <summary>
///     Tests for the SVG line-end (connector decoration) markers: open chevron is drawn OPEN, the
///     closed shapes stay closed, and every marker dimension is derived from <see cref="NotationMetrics"/>
///     (so SVG and PNG share one size source).
/// </summary>
public sealed class SvgEndMarkerTests
{
    private static string RenderLine(EndMarkerStyle source, EndMarkerStyle target)
    {
        var renderer = new SvgRenderer();
        var line = new LayoutLine(
            [new Point2D(10, 10), new Point2D(90, 10)],
            source,
            target,
            LineStyle.Solid,
            null);
        var layout = new LayoutTree(200, 100, [line]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();
        renderer.Render(layout, options, output);
        output.Position = 0;
        return new StreamReader(output).ReadToEnd();
    }

    /// <summary>The open-chevron marker is defined as an OPEN <c>&lt;polyline&gt;</c>, not a closed polygon.</summary>
    [Fact]
    public void OpenChevron_IsDefinedAsPolyline()
    {
        var svg = RenderLine(EndMarkerStyle.None, EndMarkerStyle.OpenChevron);

        // The marker definition immediately following the open-chevron id must be a polyline (open),
        // never a polygon (closed).
        var markerIndex = svg.IndexOf("id=\"line-end-open-chevron\"", StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, "Expected the open-chevron marker definition to be present.");

        var markerEnd = svg.IndexOf("</marker>", markerIndex, StringComparison.Ordinal);
        var markerBody = svg[markerIndex..markerEnd];
        Assert.Contains("<polyline", markerBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<polygon", markerBody, StringComparison.Ordinal);
    }

    /// <summary>The hollow-triangle marker stays a CLOSED <c>&lt;polygon&gt;</c>.</summary>
    [Fact]
    public void HollowTriangle_IsDefinedAsClosedPolygon()
    {
        var svg = RenderLine(EndMarkerStyle.None, EndMarkerStyle.HollowTriangle);

        var markerIndex = svg.IndexOf("id=\"line-end-hollow-triangle\"", StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, "Expected the hollow-triangle marker definition to be present.");

        var markerEnd = svg.IndexOf("</marker>", markerIndex, StringComparison.Ordinal);
        var markerBody = svg[markerIndex..markerEnd];
        Assert.Contains("<polygon", markerBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<polyline", markerBody, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The triangle marker box dimensions are exactly the canonical <see cref="NotationMetrics"/>
    ///     values (10 x 7, refX 9), proving the SVG markers derive from the single source.
    /// </summary>
    [Fact]
    public void TriangleMarker_DimensionsDeriveFromNotationMetrics()
    {
        var svg = RenderLine(EndMarkerStyle.None, EndMarkerStyle.HollowTriangle);

        var expected =
            $"markerWidth=\"{Num(NotationMetrics.EndMarkerLength)}\" " +
            $"markerHeight=\"{Num(NotationMetrics.EndMarkerWidth)}\" " +
            $"refX=\"{Num(NotationMetrics.EndMarkerRefX)}\" " +
            $"refY=\"{Num(NotationMetrics.EndMarkerHalfWidth)}\"";
        Assert.Contains(expected, svg, StringComparison.Ordinal);

        // The polygon points are the box mapping of the shared triangle vertices.
        Assert.Contains("points=\"0 0, 10 3.5, 0 7\"", svg, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The diamond marker box dimensions are exactly the canonical <see cref="NotationMetrics"/>
    ///     values (14 x 8, refX 13), and its points are the shared diamond vertices.
    /// </summary>
    [Fact]
    public void DiamondMarker_DimensionsDeriveFromNotationMetrics()
    {
        var svg = RenderLine(EndMarkerStyle.None, EndMarkerStyle.HollowDiamond);

        var expected =
            $"markerWidth=\"{Num(NotationMetrics.DiamondLength)}\" " +
            $"markerHeight=\"{Num(NotationMetrics.DiamondWidth)}\" " +
            $"refX=\"{Num(NotationMetrics.DiamondRefX)}\" " +
            $"refY=\"{Num(NotationMetrics.DiamondHalfWidth)}\"";
        Assert.Contains(expected, svg, StringComparison.Ordinal);
        Assert.Contains("points=\"1 4, 7 0, 13 4, 7 8\"", svg, StringComparison.Ordinal);
    }

    /// <summary>An open-chevron line references the open-chevron marker via <c>marker-end</c>.</summary>
    [Fact]
    public void OpenChevronLine_ReferencesOpenChevronMarker()
    {
        var svg = RenderLine(EndMarkerStyle.None, EndMarkerStyle.OpenChevron);
        Assert.Contains("marker-end=\"url(#line-end-open-chevron)\"", svg, StringComparison.Ordinal);
    }

    private static string Num(double value) =>
        Math.Round(value, 6).ToString(System.Globalization.CultureInfo.InvariantCulture);
}
