// <copyright file="SvgRendererTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Svg;

namespace DemaConsulting.SysML2Tools.Svg.Tests;

/// <summary>
///     Tests for the SVG renderer.
/// </summary>
public sealed class SvgRendererTests
{
    /// <summary>
    ///     Render with an empty LayoutTree produces a non-empty output stream whose content
    ///     contains the SVG root element, confirming basic SVG document generation.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_EmptyTree_ProducesSvgDocument()
    {
        // Arrange: a renderer with an empty LayoutTree and default options
        var renderer = new SvgRenderer();
        var layout = new LayoutTree(400, 300, []);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act: render the empty tree
        renderer.Render(layout, options, output);

        // Assert: output is non-empty and contains the SVG root element
        Assert.True(output.Length > 0);
        output.Position = 0;
        var svgText = new StreamReader(output).ReadToEnd();
        Assert.Contains("<svg", svgText, StringComparison.Ordinal);
        Assert.Contains("</svg>", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render with a LayoutTree containing one LayoutBox produces SVG output that
    ///     contains a rect element, confirming that boxes are translated to SVG rectangles.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SingleBox_ProducesRectElement()
    {
        // Arrange: a renderer with a tree containing one LayoutBox
        var renderer = new SvgRenderer();
        var box = new LayoutBox(10, 10, 100, 50, "MyBox", 0, BoxShape.Rectangle, [], []);
        var layout = new LayoutTree(200, 100, [box]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act: render the tree with one box
        renderer.Render(layout, options, output);

        // Assert: output contains a rect element
        output.Position = 0;
        var svgText = new StreamReader(output).ReadToEnd();
        Assert.Contains("<rect", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render with a LayoutTree containing one LayoutLabel produces SVG output that
    ///     contains a text element, confirming that labels are translated to SVG text nodes.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SingleLabel_ProducesTextElement()
    {
        // Arrange: a renderer with a tree containing one LayoutLabel
        var renderer = new SvgRenderer();
        var label = new LayoutLabel(50, 75, 200, "Hello World", TextAlign.Center);
        var layout = new LayoutTree(200, 100, [label]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act: render the tree with one label
        renderer.Render(layout, options, output);

        // Assert: output contains a text element
        output.Position = 0;
        var svgText = new StreamReader(output).ReadToEnd();
        Assert.Contains("<text", svgText, StringComparison.Ordinal);
        Assert.Contains("Hello World", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render with a LayoutTree containing one LayoutLine produces SVG output that
    ///     contains a path element, confirming that lines are translated to SVG paths.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SingleLine_ProducesPathElement()
    {
        // Arrange: a renderer with a tree containing one LayoutLine
        var renderer = new SvgRenderer();
        var line = new LayoutLine(
            [new Point2D(10, 10), new Point2D(90, 90)],
            ArrowheadStyle.None,
            ArrowheadStyle.Open,
            LineStyle.Solid,
            null);
        var layout = new LayoutTree(200, 100, [line]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act: render the tree with one line
        renderer.Render(layout, options, output);

        // Assert: output contains a path element
        output.Position = 0;
        var svgText = new StreamReader(output).ReadToEnd();
        Assert.Contains("<path", svgText, StringComparison.Ordinal);
    }
}
