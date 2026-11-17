// <copyright file="NotationStylingRenderTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Globalization;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.CanvasNet;
using DemaConsulting.Rendering.Svg;

namespace DemaConsulting.SysML2Tools.Tests.Rendering;

/// <summary>
///     Focused renderer tests for the SysML v2 notation styling of DemaConsulting.Rendering (dashed
///     outlines, italic names, name-in-tab folders, centered italic compartment titles, the
///     two-dot, four-dot, and crossbar end markers, the port glyph with its direction arrow, and the
///     connector end labels). Each test builds a small hand-made
///     <see cref="LayoutTree"/> so the SVG and PNG renderers are isolated from the layout strategies,
///     then asserts on the renderer output rather than on a generic "output exists" check.
/// </summary>
public sealed class NotationStylingRenderTests
{
    /// <summary>
    ///     The SVG XML namespace.
    /// </summary>
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    /// <summary>
    ///     Width of the hand-made trees in layout units (one pixel per unit at the default render scale).
    /// </summary>
    private const double TreeWidth = 400;

    /// <summary>
    ///     Height of the hand-made trees in layout units.
    /// </summary>
    private const double TreeHeight = 300;

    /// <summary>
    ///     Builds a layout box with the notation inputs the tests vary.
    /// </summary>
    /// <param name="x">Left edge.</param>
    /// <param name="y">Top edge.</param>
    /// <param name="width">Box width.</param>
    /// <param name="height">Box height.</param>
    /// <param name="label">Box name.</param>
    /// <param name="shape">Box outline shape.</param>
    /// <param name="appearance">Per-box appearance, or <see langword="null"/> for the default.</param>
    /// <param name="compartments">Compartments of the box.</param>
    /// <param name="folderTab">Folder tab size, or <see langword="null"/> for a non-folder box.</param>
    /// <returns>The layout box.</returns>
    private static LayoutBox Box(
        double x,
        double y,
        double width,
        double height,
        string label,
        BoxShape shape,
        BoxAppearance? appearance = null,
        IReadOnlyList<LayoutCompartment>? compartments = null,
        (double Width, double Height)? folderTab = null) =>
        new(
            x,
            y,
            width,
            height,
            label,
            0,
            shape,
            compartments ?? [],
            [],
            FolderTabWidth: folderTab?.Width,
            FolderTabHeight: folderTab?.Height,
            Appearance: appearance);

    /// <summary>
    ///     Builds a straight horizontal connector that ends in the given marker.
    /// </summary>
    /// <param name="y">Vertical position of the connector.</param>
    /// <param name="targetEnd">End marker at the target end.</param>
    /// <returns>The layout line.</returns>
    private static LayoutLine Connector(double y, EndMarkerStyle targetEnd) =>
        new([new Point2D(20, y), new Point2D(120, y)], EndMarkerStyle.None, targetEnd, LineStyle.Solid, null);

    /// <summary>
    ///     Renders a layout node list to an SVG document.
    /// </summary>
    /// <param name="nodes">Nodes of the layout tree.</param>
    /// <returns>The parsed SVG document.</returns>
    private static XDocument RenderSvg(params LayoutNode[] nodes)
    {
        using var output = new MemoryStream();
        new SvgRenderer().Render(new LayoutTree(TreeWidth, TreeHeight, nodes), new RenderOptions(Themes.Light), output);
        return XDocument.Parse(System.Text.Encoding.UTF8.GetString(output.ToArray()));
    }

    /// <summary>
    ///     Renders a layout node list to a decoded PNG image.
    /// </summary>
    /// <param name="nodes">Nodes of the layout tree.</param>
    /// <returns>The decoded image; the caller disposes it.</returns>
    private static DemaConsulting.CanvasNet.Canvas.Surface RenderPng(params LayoutNode[] nodes)
    {
        using var output = new MemoryStream();
        new PngRenderer().Render(new LayoutTree(TreeWidth, TreeHeight, nodes), new RenderOptions(Themes.Light), output);
        return ImageDecoder.Load(output.ToArray());
    }

    /// <summary>
    ///     Finds the SVG <c>rect</c> whose top-left corner matches the given box position.
    /// </summary>
    /// <param name="svg">SVG document.</param>
    /// <param name="x">Left edge of the box.</param>
    /// <param name="y">Top edge of the box.</param>
    /// <returns>The matching rectangle element.</returns>
    private static XElement FindRect(XDocument svg, double x, double y) =>
        svg.Descendants(Svg + "rect").Single(r =>
            Math.Abs(Number(r, "x") - x) < 0.01 && Math.Abs(Number(r, "y") - y) < 0.01);

    /// <summary>
    ///     Finds the single SVG <c>text</c> element with the given content.
    /// </summary>
    /// <param name="svg">SVG document.</param>
    /// <param name="content">Exact text content.</param>
    /// <returns>The matching text element.</returns>
    private static XElement FindText(XDocument svg, string content) =>
        svg.Descendants(Svg + "text").Single(t => t.Value == content);

    /// <summary>
    ///     Reads a numeric SVG attribute using the invariant culture.
    /// </summary>
    /// <param name="element">Element carrying the attribute.</param>
    /// <param name="attribute">Attribute name.</param>
    /// <returns>The attribute value.</returns>
    private static double Number(XElement element, string attribute) =>
        double.Parse((string)element.Attribute(attribute)!, CultureInfo.InvariantCulture);

    /// <summary>
    ///     Gets the SVG marker definition for the given end-marker identifier.
    /// </summary>
    /// <param name="svg">SVG document.</param>
    /// <param name="id">Marker identifier.</param>
    /// <returns>The marker element.</returns>
    private static XElement FindMarker(XDocument svg, string id) =>
        svg.Descendants(Svg + "marker").Single(m => (string?)m.Attribute("id") == id);

    /// <summary>
    ///     Gets the grayscale intensity of a pixel.
    /// </summary>
    /// <param name="surface">Decoded image.</param>
    /// <param name="x">Pixel column.</param>
    /// <param name="y">Pixel row.</param>
    /// <returns>The mean of the red, green, and blue channels (0 is black, 255 is white).</returns>
    private static int Intensity(DemaConsulting.CanvasNet.Canvas.Surface surface, int x, int y)
    {
        var pixel = surface[x, y];
        return (pixel.R + pixel.G + pixel.B) / 3;
    }

    /// <summary>
    ///     Counts the dark-to-light and light-to-dark transitions along the top edge of a box, sampling the
    ///     two pixel rows that the 1.5-unit outline straddles so that anti-aliasing does not hide a gap.
    /// </summary>
    /// <param name="surface">Decoded image.</param>
    /// <param name="edgeY">Vertical position of the outline.</param>
    /// <param name="fromX">First column of the probe.</param>
    /// <param name="toX">Last column of the probe (exclusive).</param>
    /// <returns>The number of transitions between dark outline and white background.</returns>
    private static int CountOutlineTransitions(DemaConsulting.CanvasNet.Canvas.Surface surface, int edgeY, int fromX, int toX)
    {
        var transitions = 0;
        bool? previousDark = null;
        for (var x = fromX; x < toX; x++)
        {
            var darkest = Math.Min(Intensity(surface, x, edgeY - 1), Intensity(surface, x, edgeY));
            bool? dark = null;
            if (darkest < 110)
            {
                dark = true;
            }
            else if (darkest > 200)
            {
                dark = false;
            }

            if (dark is null)
            {
                continue;
            }

            if (previousDark is not null && previousDark != dark)
            {
                transitions++;
            }

            previousDark = dark;
        }

        return transitions;
    }

    /// <summary>
    ///     A reference usage whose box requests a dashed outline is drawn with a dash pattern, while a
    ///     composite usage and a default box keep a solid outline.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_DashedOutline_ReferenceUsageHasDashArrayAndCompositeUsageIsSolid()
    {
        // Arrange: a dashed-outline box and a default box, both rounded like usages
        var svg = RenderSvg(
            Box(20, 20, 160, 80, "refUsage", BoxShape.RoundedRectangle, new BoxAppearance(LineStyle.Dashed)),
            Box(220, 20, 160, 80, "compUsage", BoxShape.RoundedRectangle));

        // Act: locate the two outline rectangles
        var dashed = FindRect(svg, 20, 20);
        var solid = FindRect(svg, 220, 20);

        // Assert: only the reference usage carries a stroke dash pattern
        var pattern = (string?)dashed.Attribute("stroke-dasharray");
        Assert.False(string.IsNullOrWhiteSpace(pattern), "dashed outline must carry stroke-dasharray");
        Assert.Null(solid.Attribute("stroke-dasharray"));
    }

    /// <summary>
    ///     An abstract definition's name is drawn italic, while the name of a concrete definition is not.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_ItalicName_AbstractDefinitionIsItalicAndConcreteIsNot()
    {
        // Arrange: a box with an italic name style and a default box
        var svg = RenderSvg(
            Box(20, 20, 160, 80, "AbstractDef", BoxShape.Rectangle, new BoxAppearance(NameStyle: TextStyle.BoldItalic)),
            Box(220, 20, 160, 80, "ConcreteDef", BoxShape.Rectangle));

        // Act: locate the two name texts
        var abstractName = FindText(svg, "AbstractDef");
        var concreteName = FindText(svg, "ConcreteDef");

        // Assert: the abstract name is italic (and stays bold); the concrete name has no italic style
        Assert.Equal("italic", (string?)abstractName.Attribute("font-style"));
        Assert.Equal("bold", (string?)abstractName.Attribute("font-weight"));
        Assert.Null(concreteName.Attribute("font-style"));
        Assert.Equal("bold", (string?)concreteName.Attribute("font-weight"));
    }

    /// <summary>
    ///     A compartment that requests a centered italic title is drawn with a middle text anchor at the
    ///     box center and an italic style; the requested style is honored (a regular-style title is not
    ///     italic) and a default compartment title is not centered.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_CompartmentTitle_CenteredAndItalicWhenRequested()
    {
        // Arrange: boxes whose compartment titles are centered italic, centered regular, and default
        var svg = RenderSvg(
            Box(
                20,
                20,
                160,
                120,
                "Styled",
                BoxShape.Rectangle,
                compartments: [new LayoutCompartment("styledTitle", ["row"], TextAlign.Center, TextStyle.Italic)]),
            Box(
                220,
                20,
                160,
                120,
                "Regular",
                BoxShape.Rectangle,
                compartments: [new LayoutCompartment("regularTitle", ["row"], TextAlign.Center, TextStyle.Regular)]),
            Box(
                20,
                160,
                160,
                120,
                "Plain",
                BoxShape.Rectangle,
                compartments: [new LayoutCompartment("plainTitle", ["row"])]));

        // Act: locate the compartment title texts
        var styled = FindText(svg, "styledTitle");
        var regular = FindText(svg, "regularTitle");
        var plain = FindText(svg, "plainTitle");

        // Assert: the requested title is centered on the box and italic
        Assert.Equal("middle", (string?)styled.Attribute("text-anchor"));
        Assert.Equal("italic", (string?)styled.Attribute("font-style"));
        Assert.InRange(Number(styled, "x"), 20 + (160 / 2.0) - 1, 20 + (160 / 2.0) + 1);

        // Assert: the title style is carried through (regular is not italic) and the default is not centered
        Assert.Equal("middle", (string?)regular.Attribute("text-anchor"));
        Assert.Null(regular.Attribute("font-style"));
        Assert.NotEqual("middle", (string?)plain.Attribute("text-anchor"));
    }


    /// <summary>
    ///     A package folder that requests the name in the tab draws the name once, inside the tab, and
    ///     does not draw a second title band in the folder body; a default folder keeps its name in the
    ///     body below the tab.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_FolderNameInTab_NameInsideTabWithoutSecondTitleBand()
    {
        // Arrange: a tab-titled folder and a default folder, each with an 80 x 24 tab
        const double tabWidth = 80;
        const double tabHeight = 24;
        var svg = RenderSvg(
            Box(
                20,
                20,
                160,
                200,
                "TabPkg",
                BoxShape.Folder,
                new BoxAppearance(FolderTitle: FolderTitlePlacement.Tab),
                folderTab: (tabWidth, tabHeight)),
            Box(220, 20, 160, 200, "BodyPkg", BoxShape.Folder, folderTab: (tabWidth, tabHeight)));

        // Act: locate the folder name texts
        var tabName = FindText(svg, "TabPkg");
        var bodyName = FindText(svg, "BodyPkg");

        // Assert: the tab name is the only text for that folder and lies inside the tab rectangle
        Assert.InRange(Number(tabName, "x"), 20, 20 + tabWidth);
        Assert.InRange(Number(tabName, "y"), 20, 20 + tabHeight);

        // Assert: the default folder draws its name in the body, below the tab
        Assert.True(Number(bodyName, "y") > 20 + tabHeight, "default folder name must sit below the tab");
    }

    /// <summary>
    ///     The hollow-triangle-with-two-dots end marker is defined as a triangle with exactly two dots and
    ///     is applied to a connector that requests it.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_HollowTriangleTwoDots_MarkerHasTriangleAndTwoDots()
    {
        // Arrange / Act: render one connector that ends in the two-dots triangle
        var svg = RenderSvg(Connector(40, EndMarkerStyle.HollowTriangleTwoDots));
        var marker = FindMarker(svg, "line-end-hollow-triangle-two-dots");

        // Assert: one hollow triangle and exactly two dots, used as the connector's end marker
        Assert.Single(marker.Elements(Svg + "polygon"));
        Assert.Equal(2, marker.Elements(Svg + "circle").Count());
        Assert.Contains(
            svg.Descendants(Svg + "path"),
            p => (string?)p.Attribute("marker-end") == "url(#line-end-hollow-triangle-two-dots)");
    }

    /// <summary>
    ///     The hollow-triangle-with-four-dots end marker is defined as a triangle with exactly four dots
    ///     (two columns of two) and is applied to a connector that requests it.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_HollowTriangleFourDots_MarkerHasTriangleAndFourDots()
    {
        // Arrange / Act: render one connector that ends in the four-dots triangle
        var svg = RenderSvg(Connector(40, EndMarkerStyle.HollowTriangleFourDots));
        var marker = FindMarker(svg, "line-end-hollow-triangle-four-dots");

        // Assert: one hollow triangle and exactly four dots in two distinct columns
        Assert.Single(marker.Elements(Svg + "polygon"));
        var dots = marker.Elements(Svg + "circle").ToList();
        Assert.Equal(4, dots.Count);
        Assert.Equal(2, dots.Select(d => Number(d, "cx")).Distinct().Count());
        Assert.Contains(
            svg.Descendants(Svg + "path"),
            p => (string?)p.Attribute("marker-end") == "url(#line-end-hollow-triangle-four-dots)");
    }

    /// <summary>
    ///     The hollow-triangle-with-crossbar end marker is defined as a triangle plus a perpendicular bar
    ///     (and no dots) and is applied to a connector that requests it.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_HollowTriangleCrossbar_MarkerHasTriangleAndBarWithoutDots()
    {
        // Arrange / Act: render one connector that ends in the crossbar triangle
        var svg = RenderSvg(Connector(40, EndMarkerStyle.HollowTriangleCrossbar));
        var marker = FindMarker(svg, "line-end-hollow-triangle-crossbar");

        // Assert: one hollow triangle, one vertical bar line, and no dots
        Assert.Single(marker.Elements(Svg + "polygon"));
        var bar = Assert.Single(marker.Elements(Svg + "line"));
        Assert.Equal(Number(bar, "x1"), Number(bar, "x2"));
        Assert.NotEqual(Number(bar, "y1"), Number(bar, "y2"));
        Assert.Empty(marker.Elements(Svg + "circle"));
        Assert.Contains(
            svg.Descendants(Svg + "path"),
            p => (string?)p.Attribute("marker-end") == "url(#line-end-hollow-triangle-crossbar)");
    }

    /// <summary>
    ///     The PNG renderer draws a dashed outline as alternating dark and white runs along a straight edge,
    ///     while a default outline is one unbroken dark run.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_DashedOutline_TopEdgeAlternatesDarkAndWhiteAndSolidEdgeDoesNot()
    {
        // Arrange: a dashed rectangle and a solid rectangle whose top edges lie on whole-pixel rows
        using var surface = RenderPng(
            Box(20, 20, 160, 80, "dashed", BoxShape.Rectangle, new BoxAppearance(LineStyle.Dashed)),
            Box(220, 20, 160, 80, "solid", BoxShape.Rectangle));

        // Act: probe along the straight top edge, away from the corners and the name text
        var dashedTransitions = CountOutlineTransitions(surface, 20, 30, 170);
        var solidTransitions = CountOutlineTransitions(surface, 20, 230, 370);

        // Assert: the dashed edge flips between dark and white many times (about 15 dashes over 140 px);
        // the solid edge never shows a white gap
        Assert.True(dashedTransitions >= 10, $"dashed edge shows only {dashedTransitions} dark/white transitions");
        Assert.Equal(0, solidTransitions);
        Assert.True(Intensity(surface, 300, 20) < 110, "solid edge must be dark");
    }

    /// <summary>
    ///     The PNG renderer draws the dots of the four-dots triangle marker as two dark spots above and two
    ///     below the connector, behind the triangle; the plain hollow-triangle marker leaves those bands white.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_HollowTriangleFourDots_DrawsDarkDotsBehindTriangle()
    {
        // Arrange: a four-dots connector at y = 40 and a plain hollow-triangle connector at y = 100,
        // both ending at x = 120
        using var surface = RenderPng(
            Connector(40, EndMarkerStyle.HollowTriangleFourDots),
            Connector(100, EndMarkerStyle.HollowTriangle));

        // Act: count the dark spots along a band above and a band below each connector, in the columns
        // just left of the triangle (the dots sit in two columns at the back of the marker)
        var dotsAbove = CountDarkRuns(surface, 35, 37, 80, 102);
        var dotsBelow = CountDarkRuns(surface, 42, 44, 80, 102);
        var plainAbove = CountDarkRuns(surface, 95, 97, 80, 102);
        var plainBelow = CountDarkRuns(surface, 102, 104, 80, 102);

        // Assert: two dots per band for the four-dots marker (four in total), none for the plain triangle
        Assert.Equal(2, dotsAbove);
        Assert.Equal(2, dotsBelow);
        Assert.Equal(0, plainAbove);
        Assert.Equal(0, plainBelow);
    }

    /// <summary>
    ///     Builds a port on a box face with the SysML appearance the Interconnection View requests: an
    ///     outlined glyph with a direction arrow and an outside label.
    /// </summary>
    /// <param name="x">Center X of the port.</param>
    /// <param name="y">Center Y of the port.</param>
    /// <param name="side">Face of the owning box the port sits on.</param>
    /// <param name="direction">Direction arrow of the port.</param>
    /// <param name="label">Outside label.</param>
    /// <param name="outline">Outline style of the glyph.</param>
    /// <returns>The layout port.</returns>
    private static LayoutPort Port(double x, double y, PortSide side, PortDirection direction, string label, LineStyle outline = LineStyle.Solid) =>
        new(x, y, side, label, Appearance: new PortAppearance(direction, PortGlyph.Outlined, outline, PortLabelPlacement.Outward));

    /// <summary>
    ///     The SVG renderer draws a port as a white 12 x 12 rounded square with a direction arrow that points
    ///     into the part for an in port on a left face and out of the part for an out port on a right face,
    ///     a dashed outline for a reference port, and the label outside the box.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_PortGlyph_DirectionArrowAndDashedReference()
    {
        // Arrange: a box with an in port and a dashed inout reference port on its left face and an out port
        // on its right face
        var svg = RenderSvg(
            Box(100, 50, 160, 140, "part", BoxShape.RoundedRectangle),
            Port(100, 80, PortSide.Left, PortDirection.In, "in : Fuel"),
            Port(100, 140, PortSide.Left, PortDirection.InOut, "ref : Fuel", LineStyle.Dashed),
            Port(260, 80, PortSide.Right, PortDirection.Out, "out : Fuel"));

        // Act
        var inGlyph = FindRect(svg, 94, 74);
        var refGlyph = FindRect(svg, 94, 134);
        var outGlyph = FindRect(svg, 254, 74);
        var polylines = svg.Descendants(Svg + "polyline").Select(p => (string)p.Attribute("points")!).ToList();

        // Assert: a white rounded square, solid for the in and out ports and dashed for the reference port
        foreach (var glyph in new[] { inGlyph, refGlyph, outGlyph })
        {
            Assert.Equal(12.0, Number(glyph, "width"), 0.01);
            Assert.Equal(12.0, Number(glyph, "height"), 0.01);
            Assert.Equal("#FFFFFF", (string?)glyph.Attribute("fill"));
            Assert.True(Number(glyph, "rx") > 0.0, "the glyph must have rounded corners");
        }

        Assert.Null(inGlyph.Attribute("stroke-dasharray"));
        Assert.Null(outGlyph.Attribute("stroke-dasharray"));
        Assert.NotNull(refGlyph.Attribute("stroke-dasharray"));

        // Assert: the in arrow's tip is on the part side of the left face (right of its center), the out
        // arrow's tip is outside the part on the right face (right of its center), and the inout arrow has
        // a head at both ends
        Assert.Contains(polylines, p => p.Contains("103.00,80.00", StringComparison.Ordinal));
        Assert.Contains(polylines, p => p.Contains("263.00,80.00", StringComparison.Ordinal));
        Assert.Contains(polylines, p => p.Contains("103.00,140.00", StringComparison.Ordinal));
        Assert.Contains(polylines, p => p.Contains("97.00,140.00", StringComparison.Ordinal));

        // Assert: labels are outside the box: ending left of the left face and starting right of the right face
        Assert.Equal("end", (string?)FindText(svg, "in : Fuel").Attribute("text-anchor"));
        Assert.True(Number(FindText(svg, "in : Fuel"), "x") < 100.0);
        Assert.Equal("start", (string?)FindText(svg, "out : Fuel").Attribute("text-anchor"));
        Assert.True(Number(FindText(svg, "out : Fuel"), "x") > 260.0);
    }

    /// <summary>
    ///     The PNG renderer draws a port as an outlined white square that hides the box border beneath it: the
    ///     border is dark above the glyph, white inside the glyph, and the glyph's own outline is dark.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_PortGlyph_DrawsOutlinedSquare()
    {
        // Arrange: a box whose left border (x = 100) is crossed by an in port centered at y = 80
        using var surface = RenderPng(
            Box(100, 50, 160, 140, "part", BoxShape.Rectangle),
            Port(100, 80, PortSide.Left, PortDirection.In, "in : Fuel"));

        // Act
        var borderAbove = Math.Min(Intensity(surface, 99, 62), Intensity(surface, 100, 62));
        var insideGlyph = Intensity(surface, 100, 75);
        var glyphOutline = Math.Min(Intensity(surface, 93, 80), Intensity(surface, 94, 80));

        // Assert
        Assert.True(borderAbove < 110, "the box border above the glyph must be dark");
        Assert.True(insideGlyph > 200, $"the glyph must be filled white over the border (intensity {insideGlyph})");
        Assert.True(glyphOutline < 140, $"the glyph must have a dark outline (intensity {glyphOutline})");
    }

    /// <summary>
    ///     The SVG renderer draws the end labels of a line at its ends: the role name above the line and the
    ///     multiplicity below it, at both ends.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_EndLabels_RoleAboveMultiplicityBelow()
    {
        // Arrange: a horizontal line with a role name and a multiplicity at each end
        var line = new LayoutLine(
            [new Point2D(40, 100), new Point2D(340, 100)],
            EndMarkerStyle.None,
            EndMarkerStyle.None,
            LineStyle.Solid,
            null,
            new LineEndLabel("supply", "[1]"),
            new LineEndLabel("drain", "[0..*]"));

        // Act
        var svg = RenderSvg(line);

        // Assert: each role name is above the line (smaller y) and each multiplicity below it, near its end
        foreach (var (role, multiplicity, nearLeft) in new[] { ("supply", "[1]", true), ("drain", "[0..*]", false) })
        {
            var roleText = FindText(svg, role);
            var multiplicityText = FindText(svg, multiplicity);
            Assert.True(Number(roleText, "y") < 100.0, $"{role} must be above the line");
            Assert.True(Number(multiplicityText, "y") > 100.0, $"{multiplicity} must be below the line");
            var x = Number(roleText, "x");
            Assert.True(nearLeft ? x < 190.0 : x > 190.0, $"{role} must be near its own end of the line");
        }
    }

    /// <summary>
    ///     The SVG renderer draws a lifeline header as a rounded rectangle that shows the keyword over the
    ///     name line, with a dashed outline when the header appearance requests one, and the stem below it.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_LifelineHeader_RoundedWithKeyword()
    {
        // Arrange: a solid and a dashed lifeline header, both rounded and with a keyword
        var headerHeight = BoxMetrics.LifelineHeaderHeight(Themes.Light, hasKeyword: true);
        var svg = RenderSvg(
            new LayoutLifeline(90, 20, 250, "client : Client", 140, headerHeight, "part", BoxShape.RoundedRectangle, null, null),
            new LayoutLifeline(300, 20, 250, "server : Server", 140, headerHeight, "ref part", BoxShape.RoundedRectangle, new BoxAppearance(LineStyle.Dashed), null));

        // Act
        var solid = FindRect(svg, 20, 20);
        var dashed = FindRect(svg, 230, 20);
        var keyword = FindText(svg, "«part»");
        var name = FindText(svg, "client : Client");

        // Assert: both headers are rounded, only the reference header is dashed
        Assert.True(Number(solid, "rx") > 0.0, "the header must have rounded corners");
        Assert.True(Number(dashed, "rx") > 0.0, "the header must have rounded corners");
        Assert.Null(solid.Attribute("stroke-dasharray"));
        Assert.NotNull(dashed.Attribute("stroke-dasharray"));

        // Assert: the keyword is drawn above the name, both inside the header and centered on the lifeline
        Assert.True(Number(keyword, "y") < Number(name, "y"), "the keyword must be above the name line");
        Assert.True(Number(name, "y") < 20.0 + headerHeight, "the name must be inside the header");
        Assert.Equal(90.0, Number(keyword, "x"), 0.5);
        Assert.Equal(90.0, Number(name, "x"), 0.5);
        Assert.Contains(svg.Descendants(Svg + "text"), t => t.Value == "«ref part»");
    }

    /// <summary>
    ///     Counts the separate dark runs across a horizontal band of pixel rows.
    /// </summary>
    /// <param name="surface">Decoded image.</param>
    /// <param name="fromY">First row of the band.</param>
    /// <param name="toY">Last row of the band (inclusive).</param>
    /// <param name="fromX">First column of the probe.</param>
    /// <param name="toX">Last column of the probe (exclusive).</param>
    /// <returns>The number of maximal runs of columns that contain a dark pixel within the band.</returns>
    private static int CountDarkRuns(DemaConsulting.CanvasNet.Canvas.Surface surface, int fromY, int toY, int fromX, int toX)
    {
        var runs = 0;
        var inRun = false;
        for (var x = fromX; x < toX; x++)
        {
            var dark = false;
            for (var y = fromY; y <= toY; y++)
            {
                dark |= Intensity(surface, x, y) < 110;
            }

            if (dark && !inRun)
            {
                runs++;
            }

            inRun = dark;
        }

        return runs;
    }
}
