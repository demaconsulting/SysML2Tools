// <copyright file="NotationMetricsTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Linq;
using DemaConsulting.SysML2Tools.Layout;
using DemaConsulting.SysML2Tools.Rendering;

namespace DemaConsulting.SysML2Tools.Tests.Rendering;

/// <summary>
///     Tests for <see cref="NotationMetrics"/>, the single home for intrinsic notation geometry.
///     These tests pin the canonical values and prove every end-marker shape is a documented
///     derivation of named metrics (no geometry literal appears twice in the rendering path).
/// </summary>
public sealed class NotationMetricsTests
{
    /// <summary>The triangle-family canonical values match the historical SVG marker (10x7, refX 9).</summary>
    [Fact]
    public void TriangleFamily_HasCanonicalValues()
    {
        // Assert: the triangle-family primitives match the historical SVG marker.
        Assert.Equal(10.0, NotationMetrics.EndMarkerLength);
        Assert.Equal(7.0, NotationMetrics.EndMarkerWidth);
        Assert.Equal(9.0, NotationMetrics.EndMarkerRefX);
        Assert.Equal(3.5, NotationMetrics.EndMarkerHalfWidth);

        // The apex overshoots the endpoint by markerLength - refX = 1.
        Assert.Equal(1.0, NotationMetrics.EndMarkerTipOvershoot);
    }

    /// <summary>The diamond canonical values match the historical SVG marker (14x8, refX 13).</summary>
    [Fact]
    public void Diamond_HasCanonicalValues()
    {
        // Assert: the diamond primitives match the historical SVG marker.
        Assert.Equal(14.0, NotationMetrics.DiamondLength);
        Assert.Equal(8.0, NotationMetrics.DiamondWidth);
        Assert.Equal(13.0, NotationMetrics.DiamondRefX);
        Assert.Equal(4.0, NotationMetrics.DiamondHalfWidth);
        Assert.Equal(7.0, NotationMetrics.DiamondMidX);
        Assert.Equal(1.0, NotationMetrics.DiamondNearX);
    }

    /// <summary>Circle and bar canonical values match the historical SVG markers (r4 / 4x12).</summary>
    [Fact]
    public void CircleAndBar_HaveCanonicalValues()
    {
        // Assert: the circle and bar primitives match the historical SVG markers.
        Assert.Equal(4.0, NotationMetrics.CircleRadius);
        Assert.Equal(10.0, NotationMetrics.CircleMarkerBox);
        Assert.Equal(5.0, NotationMetrics.CircleCenter);
        Assert.Equal(9.0, NotationMetrics.CircleRefX);

        Assert.Equal(4.0, NotationMetrics.BarAlong);
        Assert.Equal(12.0, NotationMetrics.BarAcross);
        Assert.Equal(2.0, NotationMetrics.BarHalfAlong);
        Assert.Equal(6.0, NotationMetrics.BarHalf);
    }

    /// <summary>The crossbar position is the documented fraction of the marker length (0.7 x 10 = 7).</summary>
    [Fact]
    public void Crossbar_IsDerivedFraction()
    {
        // Assert: the crossbar sits at the documented fraction of the marker length.
        Assert.Equal(0.7, NotationMetrics.CrossbarFraction);
        Assert.Equal(7.0, Math.Round(NotationMetrics.CrossbarX, 6));
    }

    /// <summary>
    ///     The triangle vertices map to the historical SVG marker-box points
    ///     <c>0 0, 10 3.5, 0 7</c> using <c>boxX = refX - Along</c>, <c>boxY = refY + Across</c>.
    /// </summary>
    [Fact]
    public void TriangleVertices_ReproduceSvgBoxPoints()
    {
        // Arrange: the shared triangle vertices in tip-relative units.
        var vertices = NotationMetrics.TriangleVertices();

        // Act: map them back to SVG marker-box coordinates.
        var box = MapToBox(vertices, NotationMetrics.EndMarkerRefX, NotationMetrics.EndMarkerHalfWidth);

        // Assert: they reproduce the historical marker-box points.
        Assert.Equal("0 0, 10 3.5, 0 7", box);
    }

    /// <summary>
    ///     The diamond vertices map to the historical SVG marker-box points <c>1 4, 7 0, 13 4, 7 8</c>.
    /// </summary>
    [Fact]
    public void DiamondVertices_ReproduceSvgBoxPoints()
    {
        // Arrange: the shared diamond vertices in tip-relative units.
        var vertices = NotationMetrics.DiamondVertices();

        // Act: map them back to SVG marker-box coordinates.
        var box = MapToBox(vertices, NotationMetrics.DiamondRefX, NotationMetrics.DiamondHalfWidth);

        // Assert: they reproduce the historical marker-box points.
        Assert.Equal("1 4, 7 0, 13 4, 7 8", box);
    }

    /// <summary>The diamond far point lands exactly on the line endpoint (Along == 0).</summary>
    [Fact]
    public void DiamondVertices_FarPoint_LandsOnEndpoint()
    {
        // Arrange: the shared diamond vertices.
        var vertices = NotationMetrics.DiamondVertices();

        // Assert: one vertex sits exactly on the line endpoint (Along == Across == 0).
        Assert.Contains(vertices, v => Math.Abs(v.Along) < 1e-9 && Math.Abs(v.Across) < 1e-9);
    }

    /// <summary>The triangle apex overshoots the endpoint (negative Along) by the documented amount.</summary>
    [Fact]
    public void TriangleVertices_Apex_OvershootsEndpoint()
    {
        // Arrange: the shared triangle vertices; the apex is the middle vertex.
        var vertices = NotationMetrics.TriangleVertices();
        var apex = vertices[1];

        // Assert: the apex overshoots the endpoint by the documented amount.
        Assert.Equal(-NotationMetrics.EndMarkerTipOvershoot, apex.Along);
        Assert.Equal(0.0, apex.Across);
    }

    /// <summary>Each end-marker style reports the documented along-line length.</summary>
    [Theory]
    [InlineData(EndMarkerStyle.None, 0.0)]
    [InlineData(EndMarkerStyle.OpenChevron, 10.0)]
    [InlineData(EndMarkerStyle.HollowTriangle, 10.0)]
    [InlineData(EndMarkerStyle.HollowTriangleCrossbar, 10.0)]
    [InlineData(EndMarkerStyle.FilledArrow, 10.0)]
    [InlineData(EndMarkerStyle.HollowDiamond, 14.0)]
    [InlineData(EndMarkerStyle.FilledDiamond, 14.0)]
    [InlineData(EndMarkerStyle.Circle, 10.0)]
    [InlineData(EndMarkerStyle.Bar, 4.0)]
    public void AlongLineLength_MatchesMarkerBox(EndMarkerStyle style, double expected)
    {
        // Assert: each marker style reports its documented along-line length.
        Assert.Equal(expected, NotationMetrics.AlongLineLength(style));
    }

    /// <summary>The rounded-rectangle radius is the theme corner radius scaled by the documented factor.</summary>
    [Fact]
    public void RoundedRectRadius_IsThemeRadiusTimesFactor()
    {
        // Arrange: a theme providing the base line-corner radius.
        var theme = Themes.Light;

        // Assert: the rounded-rectangle radius is the theme radius scaled by the documented factor.
        Assert.Equal(2.0, NotationMetrics.RoundedRectCornerFactor);
        Assert.Equal(theme.LineCornerRadius * 2.0, NotationMetrics.RoundedRectRadius(theme));
    }

    /// <summary>The label-background extent is symmetric about the documented inset.</summary>
    [Fact]
    public void LabelBackground_ExtentMatchesInset()
    {
        // Assert: the label-background extent is symmetric about the documented inset.
        Assert.Equal(0.05, NotationMetrics.LabelBgInset);
        Assert.Equal(1.1, NotationMetrics.LabelBgExtent);
    }

    /// <summary>The port square is a full side length of twice the documented half-size (4 → 8).</summary>
    [Fact]
    public void Port_SizeIsTwiceHalfSize()
    {
        // Assert: the full port side length is twice the half-size.
        Assert.Equal(4.0, NotationMetrics.PortHalfSize);
        Assert.Equal(8.0, NotationMetrics.PortSize);
        Assert.Equal(NotationMetrics.PortHalfSize * 2.0, NotationMetrics.PortSize);
    }

    /// <summary>The folder-tab constants pin the documented max-width fraction, min width, and label factor.</summary>
    [Fact]
    public void FolderTab_HasDocumentedConstants()
    {
        // Assert: the folder-tab sizing constants match their documented values.
        Assert.Equal(0.45, NotationMetrics.FolderTabMaxWidthFraction);
        Assert.Equal(60.0, NotationMetrics.FolderTabMinWidth);
        Assert.Equal(0.55, NotationMetrics.FolderLabelCharWidthFactor);
    }

    /// <summary>The note dog-ear fold constants pin the documented fraction and maximum size.</summary>
    [Fact]
    public void NoteFold_HasDocumentedConstants()
    {
        // Assert: the note-fold constants match their documented values.
        Assert.Equal(0.25, NotationMetrics.NoteFoldFraction);
        Assert.Equal(16.0, NotationMetrics.NoteFoldMaxSize);
    }

    /// <summary>The badge fraction constants pin the bullseye inner radius (1/3) and bar half-length (0.8) fractions.</summary>
    [Fact]
    public void BadgeFractions_HaveDocumentedValues()
    {
        // Assert: the badge fractions match their documented values.
        Assert.Equal(1.0 / 3.0, NotationMetrics.BadgeBullseyeInnerFraction);
        Assert.Equal(0.8, NotationMetrics.BadgeBarLengthFraction);
    }

    /// <summary>Maps tip-relative vertices to an SVG-style marker-box points string.</summary>
    private static string MapToBox(IReadOnlyList<MarkerVertex> vertices, double refAlong, double refAcross)
    {
        return string.Join(
            ", ",
            vertices.Select(v => $"{Num(refAlong - v.Along)} {Num(refAcross + v.Across)}"));
    }

    /// <summary>Formats a coordinate exactly as the renderers do (rounded, invariant culture).</summary>
    private static string Num(double value) =>
        Math.Round(value, 6).ToString(System.Globalization.CultureInfo.InvariantCulture);
}
