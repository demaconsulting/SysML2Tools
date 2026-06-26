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
        var label = new LayoutLabel(50, 75, 200, "Hello World", TextAlign.Center, FontWeight.Regular, FontStyle.Normal, 12.0);
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

    /// <summary>
    ///     Render a LayoutLine with 3 waypoints and a positive LineCornerRadius theme
    ///     produces SVG output containing an arc command (" A ") in the path data,
    ///     confirming that corner rounding generates arc segments.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SingleLine_WithCornerRadius_ProducesArcInPath()
    {
        // Arrange: a line with an interior bend that triggers arc generation
        var renderer = new SvgRenderer();
        var line = new LayoutLine(
            [new Point2D(10, 10), new Point2D(10, 50), new Point2D(90, 50)],
            ArrowheadStyle.None,
            ArrowheadStyle.None,
            LineStyle.Solid,
            null);
        var layout = new LayoutTree(200, 100, [line]);
        var options = new RenderOptions(Themes.Light); // LineCornerRadius = 4.0
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert: arc command is present in path data
        output.Position = 0;
        var svgText = new StreamReader(output).ReadToEnd();
        Assert.Contains(" A ", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a dashed LayoutLine produces SVG output containing the stroke-dasharray
    ///     attribute, confirming that dashed line style is mapped to SVG dash patterns.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SingleLine_Dashed_ProducesDashArray()
    {
        // Arrange: a dashed line
        var renderer = new SvgRenderer();
        var line = new LayoutLine(
            [new Point2D(10, 10), new Point2D(90, 10)],
            ArrowheadStyle.None,
            ArrowheadStyle.None,
            LineStyle.Dashed,
            null);
        var layout = new LayoutTree(200, 100, [line]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        output.Position = 0;
        var svgText = new StreamReader(output).ReadToEnd();
        Assert.Contains("stroke-dasharray", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a LayoutLine with an Open target arrowhead produces SVG output containing
    ///     a marker-end attribute, confirming arrowhead markers are referenced correctly.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SingleLine_WithOpenArrowhead_ProducesMarkerEnd()
    {
        // Arrange: a line with Open arrowhead at the target
        var renderer = new SvgRenderer();
        var line = new LayoutLine(
            [new Point2D(10, 10), new Point2D(90, 10)],
            ArrowheadStyle.None,
            ArrowheadStyle.Open,
            LineStyle.Solid,
            null);
        var layout = new LayoutTree(200, 100, [line]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        output.Position = 0;
        var svgText = new StreamReader(output).ReadToEnd();
        Assert.Contains("marker-end", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a LayoutLine with a Diamond source arrowhead produces SVG output containing
    ///     the arrowhead-diamond marker id, confirming diamond markers are defined and referenced.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SingleLine_WithDiamondArrowhead_ProducesDiamondMarker()
    {
        // Arrange: a line with Diamond arrowhead at the source
        var renderer = new SvgRenderer();
        var line = new LayoutLine(
            [new Point2D(10, 10), new Point2D(90, 10)],
            ArrowheadStyle.Diamond,
            ArrowheadStyle.None,
            LineStyle.Solid,
            null);
        var layout = new LayoutTree(200, 100, [line]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        output.Position = 0;
        var svgText = new StreamReader(output).ReadToEnd();
        Assert.Contains("arrowhead-diamond", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a LayoutBox with a LayoutCompartment produces SVG output containing a
    ///     line element (compartment divider) and compartment row text, confirming that
    ///     compartment rendering is complete.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_BoxWithCompartment_ProducesLineAndText()
    {
        // Arrange: a box with one compartment that has a body row
        var renderer = new SvgRenderer();
        var compartment = new LayoutCompartment(null, ["+ radius : Real"]);
        var box = new LayoutBox(10, 10, 150, 80, "MyBlock", 0, BoxShape.Rectangle, [compartment], []);
        var layout = new LayoutTree(200, 120, [box]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert: divider line and compartment row text are both present
        output.Position = 0;
        var svgText = new StreamReader(output).ReadToEnd();
        Assert.Contains("<line", svgText, StringComparison.Ordinal);
        Assert.Contains("+ radius : Real", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a LayoutBox with RoundedRectangle shape produces SVG output containing an
    ///     rx attribute, confirming that rounded corners are applied via the rx/ry attributes.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_BoxRoundedRectangle_ProducesRxAttribute()
    {
        // Arrange: a rounded-rectangle box
        var renderer = new SvgRenderer();
        var box = new LayoutBox(10, 10, 100, 50, "Rounded", 0, BoxShape.RoundedRectangle, [], []);
        var layout = new LayoutTree(200, 100, [box]);
        var options = new RenderOptions(Themes.Light); // LineCornerRadius = 4.0
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        output.Position = 0;
        var svgText = new StreamReader(output).ReadToEnd();
        Assert.Contains("rx=\"", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a LayoutPort produces SVG output containing a rect element,
    ///     confirming that ports are rendered as filled squares.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SinglePort_ProducesRect()
    {
        // Arrange: a port on the right side
        var renderer = new SvgRenderer();
        var port = new LayoutPort(100, 50, PortSide.Right, "p1");
        var layout = new LayoutTree(200, 100, [port]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        output.Position = 0;
        var svgText = new StreamReader(output).ReadToEnd();
        Assert.Contains("<rect", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a LayoutBadge with FilledCircle shape produces SVG output containing a
    ///     circle element, confirming that filled-circle badges are rendered as SVG circles.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SingleBadge_FilledCircle_ProducesCircle()
    {
        // Arrange: a filled-circle badge
        var renderer = new SvgRenderer();
        var badge = new LayoutBadge(50, 50, 12, BadgeShape.FilledCircle, "I");
        var layout = new LayoutTree(200, 100, [badge]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        output.Position = 0;
        var svgText = new StreamReader(output).ReadToEnd();
        Assert.Contains("<circle", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a LayoutBand produces SVG output containing a rect element,
    ///     confirming that swim-lane bands are rendered as rectangles.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SingleBand_ProducesRect()
    {
        // Arrange: a horizontal swim-lane band
        var renderer = new SvgRenderer();
        var band = new LayoutBand(10, 10, 300, 100, BandOrientation.Horizontal, "Lane A", []);
        var layout = new LayoutTree(400, 200, [band]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        output.Position = 0;
        var svgText = new StreamReader(output).ReadToEnd();
        Assert.Contains("<rect", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a LayoutLifeline produces SVG output containing both a rect element
    ///     (the header box) and a line element (the dashed stem), confirming that both
    ///     components of a lifeline are rendered.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SingleLifeline_ProducesRectAndLine()
    {
        // Arrange: a lifeline with a header box and a stem
        var renderer = new SvgRenderer();
        var lifeline = new LayoutLifeline(100, 10, 300, ":Actor", 80, 30);
        var layout = new LayoutTree(300, 400, [lifeline]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        output.Position = 0;
        var svgText = new StreamReader(output).ReadToEnd();
        Assert.Contains("<rect", svgText, StringComparison.Ordinal);
        Assert.Contains("<line", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a LayoutActivation produces SVG output containing a rect element,
    ///     confirming that activation bars are rendered as narrow rectangles.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SingleActivation_ProducesRect()
    {
        // Arrange: a narrow activation bar
        var renderer = new SvgRenderer();
        var activation = new LayoutActivation(100, 50, 200);
        var layout = new LayoutTree(300, 400, [activation]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        output.Position = 0;
        var svgText = new StreamReader(output).ReadToEnd();
        Assert.Contains("<rect", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a LayoutGrid produces SVG output containing at least one rect element,
    ///     confirming that grid cells are rendered as bordered rectangles.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SingleGrid_ProducesRects()
    {
        // Arrange: a 1x2 grid with one header and one body row
        var renderer = new SvgRenderer();
        var headerRow = new LayoutGridRow(true, [new LayoutGridCell(100, 24, "Name", TextAlign.Left, 1)]);
        var bodyRow = new LayoutGridRow(false, [new LayoutGridCell(100, 24, "Alice", TextAlign.Left, 1)]);
        var grid = new LayoutGrid(10, 10, [headerRow, bodyRow]);
        var layout = new LayoutTree(200, 100, [grid]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        output.Position = 0;
        var svgText = new StreamReader(output).ReadToEnd();
        Assert.Contains("<rect", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a LayoutLabel with FontWeight.Bold produces SVG output containing
    ///     font-weight="bold", confirming that bold labels apply the bold font weight attribute.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_LabelWithBold_ProducesBoldAttribute()
    {
        // Arrange: a label with bold weight
        var renderer = new SvgRenderer();
        var label = new LayoutLabel(50, 50, 200, "Bold Text", TextAlign.Left, FontWeight.Bold, FontStyle.Normal, 14.0);
        var layout = new LayoutTree(300, 100, [label]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        output.Position = 0;
        var svgText = new StreamReader(output).ReadToEnd();
        Assert.Contains("font-weight=\"bold\"", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a LayoutLabel with FontStyle.Italic produces SVG output containing
    ///     font-style="italic", confirming that italic labels apply the italic font style attribute.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_LabelWithItalic_ProducesItalicAttribute()
    {
        // Arrange: a label with italic style
        var renderer = new SvgRenderer();
        var label = new LayoutLabel(50, 50, 200, "Italic Text", TextAlign.Left, FontWeight.Regular, FontStyle.Italic, 14.0);
        var layout = new LayoutTree(300, 100, [label]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        output.Position = 0;
        var svgText = new StreamReader(output).ReadToEnd();
        Assert.Contains("font-style=\"italic\"", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a LayoutLine with a non-null MidpointLabel produces SVG output containing
    ///     a text element, confirming that midpoint labels are rendered over the line.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_LineWithMidpointLabel_ProducesTextElement()
    {
        // Arrange: a line with a midpoint label
        var renderer = new SvgRenderer();
        var line = new LayoutLine(
            [new Point2D(10, 50), new Point2D(190, 50)],
            ArrowheadStyle.None,
            ArrowheadStyle.None,
            LineStyle.Solid,
            "uses");
        var layout = new LayoutTree(200, 100, [line]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        output.Position = 0;
        var svgText = new StreamReader(output).ReadToEnd();
        Assert.Contains("<text", svgText, StringComparison.Ordinal);
        Assert.Contains("uses", svgText, StringComparison.Ordinal);
    }
}
