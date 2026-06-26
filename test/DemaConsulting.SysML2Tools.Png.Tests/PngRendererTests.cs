// <copyright file="PngRendererTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout;
using DemaConsulting.SysML2Tools.Png;
using DemaConsulting.SysML2Tools.Rendering;
using SkiaSharp;

namespace DemaConsulting.SysML2Tools.Png.Tests;

/// <summary>
///     Tests for the PNG renderer.
/// </summary>
public sealed class PngRendererTests
{
    /// <summary>
    ///     Helper: renders a <see cref="LayoutTree"/> to a decoded <see cref="SKBitmap"/> so
    ///     pixel values can be inspected in tests.
    /// </summary>
    /// <param name="layout">Layout tree to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    /// <returns>Decoded bitmap. Caller is responsible for disposing.</returns>
    private static SKBitmap RenderToBitmap(LayoutTree layout, RenderOptions options)
    {
        var renderer = new PngRenderer();
        using var ms = new MemoryStream();
        renderer.Render(layout, options, ms);
        ms.Position = 0;
        using var data = SKData.Create(ms);
        return SKBitmap.Decode(data);
    }

    /// <summary>
    ///     Helper: parses a CSS hex color string such as <c>#RRGGBB</c> or <c>#RRGGBBAA</c>
    ///     using SkiaSharp.
    /// </summary>
    /// <param name="hex">Hex color string to parse.</param>
    /// <returns>Parsed <see cref="SKColor"/>.</returns>
    private static SKColor ParseHex(string hex) => SKColor.Parse(hex);

    /// <summary>
    ///     Helper: returns true when each RGB channel of <paramref name="actual"/> is within
    ///     <paramref name="tolerance"/> of the corresponding channel of <paramref name="expected"/>.
    ///     A tolerance of 2 is sufficient to absorb sub-pixel anti-aliasing at well-interior pixels.
    /// </summary>
    /// <param name="expected">Expected color.</param>
    /// <param name="actual">Actual sampled color.</param>
    /// <param name="tolerance">Maximum allowed difference per channel (0–255).</param>
    /// <returns><see langword="true"/> when all channels are within tolerance.</returns>
    private static bool ColorNear(SKColor expected, SKColor actual, int tolerance = 2) =>
        Math.Abs(expected.Red - actual.Red) <= tolerance &&
        Math.Abs(expected.Green - actual.Green) <= tolerance &&
        Math.Abs(expected.Blue - actual.Blue) <= tolerance;

    /// <summary>
    ///     Render with an empty LayoutTree produces a non-empty output stream whose first
    ///     four bytes are the PNG signature bytes, confirming that a valid PNG is produced
    ///     for a minimal empty layout.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_EmptyTree_WritesPngSignature()
    {
        // Arrange: a renderer with a zero-size LayoutTree and default options
        var renderer = new PngRenderer();
        var layout = new LayoutTree(0, 0, []);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act: render the empty tree
        renderer.Render(layout, options, output);

        // Assert: PNG signature bytes 0x89 0x50 0x4E 0x47 are present at offset 0
        Assert.True(output.Length > 4);
        output.Position = 0;
        var header = new byte[4];
        _ = output.Read(header, 0, 4);
        Assert.Equal(0x89, header[0]);
        Assert.Equal(0x50, header[1]);
        Assert.Equal(0x4E, header[2]);
        Assert.Equal(0x47, header[3]);
    }

    /// <summary>
    ///     Render with a LayoutTree containing one LayoutBox produces a non-empty output
    ///     stream, confirming that box rendering does not throw and produces valid PNG.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_SingleBox_ProducesNonEmptyOutput()
    {
        // Arrange: a renderer with a tree containing one LayoutBox
        var renderer = new PngRenderer();
        var box = new LayoutBox(10, 10, 100, 50, "TestBox", 0, BoxShape.Rectangle, [], []);
        var layout = new LayoutTree(200, 100, [box]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act: render the tree with one box
        renderer.Render(layout, options, output);

        // Assert: output is a non-empty PNG stream
        Assert.True(output.Length > 4);
        output.Position = 0;
        var header = new byte[4];
        _ = output.Read(header, 0, 4);
        Assert.Equal(0x89, header[0]);
        Assert.Equal(0x50, header[1]);
    }

    /// <summary>
    ///     Render a LayoutBox at depth 0 and sample a pixel at the box center. The pixel
    ///     color must match the depth-0 fill color from Themes.Light, confirming that
    ///     boxes are filled with the correct theme color.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_SingleBox_FillColorMatchesTheme()
    {
        // Arrange: box at (10,10) 100×60 depth 0; center pixel is at (60, 40)
        var box = new LayoutBox(10, 10, 100, 60, null, 0, BoxShape.Rectangle, [], []);
        var layout = new LayoutTree(200, 100, [box]);
        var options = new RenderOptions(Themes.Light);

        // Act
        using var bmp = RenderToBitmap(layout, options);

        // Assert: interior pixel matches depth-0 fill
        var expected = ParseHex(Themes.Light.DepthFillColors[0]);
        var actual = bmp.GetPixel(60, 40);
        Assert.True(ColorNear(expected, actual), $"Expected {expected} ≈ {actual}");
    }

    /// <summary>
    ///     Render a LayoutBox at depth 1 and sample a pixel at the box center. The pixel
    ///     color must match the depth-1 fill color from Themes.Light, confirming that
    ///     depth-based fill colors are applied correctly.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_SingleBox_DepthOneUsesSecondColor()
    {
        // Arrange: box at (10,10) 100×60 depth 1; center pixel is at (60, 40)
        var box = new LayoutBox(10, 10, 100, 60, null, 1, BoxShape.Rectangle, [], []);
        var layout = new LayoutTree(200, 100, [box]);
        var options = new RenderOptions(Themes.Light);

        // Act
        using var bmp = RenderToBitmap(layout, options);

        // Assert: interior pixel matches depth-1 fill
        var expected = ParseHex(Themes.Light.DepthFillColors[1]);
        var actual = bmp.GetPixel(60, 40);
        Assert.True(ColorNear(expected, actual), $"Expected {expected} ≈ {actual}");
    }

    /// <summary>
    ///     Render an empty LayoutTree and sample the pixel at (0, 0). The background fill
    ///     must be white, confirming the canvas is initialized to white before any drawing.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_BackgroundIsWhite()
    {
        // Arrange: empty tree at 100×100
        var layout = new LayoutTree(100, 100, []);
        var options = new RenderOptions(Themes.Light);

        // Act
        using var bmp = RenderToBitmap(layout, options);

        // Assert: top-left pixel is white
        var actual = bmp.GetPixel(0, 0);
        Assert.True(ColorNear(SKColors.White, actual), $"Expected white ≈ {actual}");
    }

    /// <summary>
    ///     Render a horizontal LayoutLine and sample a pixel on the line. The sampled pixel
    ///     color must approximate the theme stroke color, confirming that lines are drawn
    ///     with the correct stroke color.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_SingleLine_PixelOnLineIsStrokeColor()
    {
        // Arrange: horizontal line from (10,50) to (190,50)
        var line = new LayoutLine(
            [new Point2D(10, 50), new Point2D(190, 50)],
            ArrowheadStyle.None,
            ArrowheadStyle.None,
            LineStyle.Solid,
            null);
        var layout = new LayoutTree(200, 100, [line]);
        var options = new RenderOptions(Themes.Light);

        // Act
        using var bmp = RenderToBitmap(layout, options);

        // Assert: pixel at the line midpoint is close to the stroke color
        var strokeColor = ParseHex(Themes.Light.StrokeColor);
        var actual = bmp.GetPixel(100, 50);
        // Use a wider tolerance (80) for anti-aliased line pixels against white background
        Assert.True(ColorNear(strokeColor, actual, tolerance: 80), $"Expected stroke {strokeColor} ≈ {actual}");
    }

    /// <summary>
    ///     Render a LayoutPort and sample a pixel at the port center. The pixel must
    ///     approximate the theme stroke color, confirming ports are rendered as filled squares.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_SinglePort_CenterPixelIsStrokeColor()
    {
        // Arrange: port at (50,50) right side; sample at center (50,50)
        var port = new LayoutPort(50, 50, PortSide.Right, null);
        var layout = new LayoutTree(200, 100, [port]);
        var options = new RenderOptions(Themes.Light);

        // Act
        using var bmp = RenderToBitmap(layout, options);

        // Assert: center pixel is close to the stroke color (port is a filled square)
        var strokeColor = ParseHex(Themes.Light.StrokeColor);
        var actual = bmp.GetPixel(50, 50);
        Assert.True(ColorNear(strokeColor, actual, tolerance: 10), $"Expected stroke {strokeColor} ≈ {actual}");
    }

    /// <summary>
    ///     Render a LayoutActivation bar and sample a pixel in its interior. The pixel must
    ///     be white, confirming activation bars are filled with white (not the background color).
    /// </summary>
    [Fact]
    public void PngRenderer_Render_SingleActivation_CenterPixelIsWhite()
    {
        // Arrange: activation at CentreX=100, TopY=20, BottomY=80; interior pixel at (100,50)
        var activation = new LayoutActivation(100, 20, 80);
        var layout = new LayoutTree(200, 100, [activation]);
        var options = new RenderOptions(Themes.Light);

        // Act
        using var bmp = RenderToBitmap(layout, options);

        // Assert: interior pixel is white
        var actual = bmp.GetPixel(100, 50);
        Assert.True(ColorNear(SKColors.White, actual), $"Expected white ≈ {actual}");
    }

    /// <summary>
    ///     Render a LayoutLifeline and sample the stem pixel. The pixel at the CentreX
    ///     midway down the stem must approximate the stroke color, confirming the stem is drawn.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_SingleLifeline_StemPixelIsStrokeColor()
    {
        // Arrange: lifeline at CentreX=100, TopY=10, BottomY=200; stem starts at y=50; sample at (100,125)
        var lifeline = new LayoutLifeline(100, 10, 200, ":Actor", 80, 40);
        var layout = new LayoutTree(300, 300, [lifeline]);
        var options = new RenderOptions(Themes.Light);

        // Act
        using var bmp = RenderToBitmap(layout, options);

        // Assert: a pixel on the dashed stem is close to the stroke color
        // Note: dashed lines may have gaps, so use wider tolerance and try a few Y positions
        var strokeColor = ParseHex(Themes.Light.StrokeColor);
        var foundStroke = false;
        for (var y = 55; y < 90; y += 3)
        {
            var actual = bmp.GetPixel(100, y);
            if (ColorNear(strokeColor, actual, tolerance: 80))
            {
                foundStroke = true;
                break;
            }
        }
        Assert.True(foundStroke, "Expected to find stroke-colored pixel on lifeline stem");
    }

    /// <summary>
    ///     Render a LayoutGrid with a header row and sample the header cell pixel. The pixel
    ///     must match the depth-1 fill color, confirming header rows use the secondary fill color.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_SingleGrid_HeaderFillMatchesTheme()
    {
        // Arrange: grid at (10,10) with a 100×30 header cell; center pixel at (60, 25)
        var headerRow = new LayoutGridRow(true, [new LayoutGridCell(100, 30, "Name", TextAlign.Left, 1)]);
        var grid = new LayoutGrid(10, 10, [headerRow]);
        var layout = new LayoutTree(200, 100, [grid]);
        var options = new RenderOptions(Themes.Light);

        // Act
        using var bmp = RenderToBitmap(layout, options);

        // Assert: header cell interior pixel matches depth-1 fill
        var expected = ParseHex(Themes.Light.DepthFillColors[1]);
        var actual = bmp.GetPixel(60, 25);
        Assert.True(ColorNear(expected, actual), $"Expected {expected} ≈ {actual}");
    }

    /// <summary>
    ///     Render a LayoutBadge with FilledCircle shape and sample the badge center pixel.
    ///     The pixel must approximate the theme stroke color, confirming that the filled-circle
    ///     badge is drawn with the stroke color as fill.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_SingleBadge_FilledCircle_CenterPixelIsStrokeColor()
    {
        // Arrange: filled-circle badge at (50,50) with size 20; center pixel at (50,50)
        var badge = new LayoutBadge(50, 50, 20, BadgeShape.FilledCircle, null);
        var layout = new LayoutTree(200, 100, [badge]);
        var options = new RenderOptions(Themes.Light);

        // Act
        using var bmp = RenderToBitmap(layout, options);

        // Assert: badge center is stroke-colored
        var strokeColor = ParseHex(Themes.Light.StrokeColor);
        var actual = bmp.GetPixel(50, 50);
        Assert.True(ColorNear(strokeColor, actual, tolerance: 10), $"Expected stroke {strokeColor} ≈ {actual}");
    }
}
