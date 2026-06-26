// <copyright file="RenderingTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout;
using DemaConsulting.SysML2Tools.Png;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Svg;

namespace DemaConsulting.SysML2Tools.Tests.Rendering;

/// <summary>
///     Tests for Rendering subsystem types including IRenderer stubs, Themes, RenderOptions,
///     RenderOutput, DiagramRenderer, and ViewContext.
/// </summary>
public sealed class RenderingTests
{
    /// <summary>
    ///     SvgRenderer.MediaType returns the correct MIME type for SVG output.
    /// </summary>
    [Fact]
    public void SvgRenderer_MediaType_IsImageSvgXml()
    {
        // Arrange: construct the renderer
        var renderer = new SvgRenderer();

        // Act / Assert: MediaType equals the SVG MIME type
        Assert.Equal("image/svg+xml", renderer.MediaType);
    }

    /// <summary>
    ///     SvgRenderer.DefaultExtension returns the correct file extension for SVG output.
    /// </summary>
    [Fact]
    public void SvgRenderer_DefaultExtension_IsDotSvg()
    {
        // Arrange: construct the renderer
        var renderer = new SvgRenderer();

        // Act / Assert: DefaultExtension equals ".svg"
        Assert.Equal(".svg", renderer.DefaultExtension);
    }

    /// <summary>
    ///     SvgRenderer.Render produces a non-empty output stream that begins with the SVG root tag,
    ///     confirming that the renderer writes a valid SVG document for an empty layout tree.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_EmptyTree_WritesValidSvg()
    {
        // Arrange: an SvgRenderer with a minimal empty LayoutTree and default options
        var renderer = new SvgRenderer();
        var layout = new LayoutTree(200, 100, []);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act: render the empty tree to the output stream
        renderer.Render(layout, options, output);

        // Assert: output is non-empty and starts with the SVG opening tag
        Assert.True(output.Length > 0);
        output.Position = 0;
        var svgText = new System.IO.StreamReader(output).ReadToEnd();
        Assert.Contains("<svg", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     PngRenderer.MediaType returns the correct MIME type for PNG output.
    /// </summary>
    [Fact]
    public void PngRenderer_MediaType_IsImagePng()
    {
        // Arrange: construct the renderer
        var renderer = new PngRenderer();

        // Act / Assert: MediaType equals the PNG MIME type
        Assert.Equal("image/png", renderer.MediaType);
    }

    /// <summary>
    ///     PngRenderer.DefaultExtension returns the correct file extension for PNG output.
    /// </summary>
    [Fact]
    public void PngRenderer_DefaultExtension_IsDotPng()
    {
        // Arrange: construct the renderer
        var renderer = new PngRenderer();

        // Act / Assert: DefaultExtension equals ".png"
        Assert.Equal(".png", renderer.DefaultExtension);
    }

    /// <summary>
    ///     PngRenderer.Render produces a non-empty output stream whose first four bytes are the
    ///     PNG signature bytes, confirming valid PNG output for an empty layout tree.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_EmptyTree_WritesPngBytes()
    {
        // Arrange: a PngRenderer with a minimal empty LayoutTree and default options
        var renderer = new PngRenderer();
        var layout = new LayoutTree(0, 0, []);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act: render the empty tree to the output stream
        renderer.Render(layout, options, output);

        // Assert: output contains PNG magic bytes 0x89 0x50 0x4E 0x47
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
    ///     DiagramRenderer.RenderWorkspace returns an empty list when the workspace contains no
    ///     view declarations, confirming that the renderer does not fabricate output for
    ///     view-free workspaces.
    /// </summary>
    [Fact]
    public void DiagramRenderer_RenderWorkspace_NoViews_ReturnsEmptyList()
    {
        // Arrange: a DiagramRenderer, an empty SysmlWorkspace (no views), and default options
        var diagramRenderer = new DiagramRenderer();
        var workspace = new SysmlWorkspace();
        var renderer = new SvgRenderer();
        var options = new RenderOptions(Themes.Light);

        // Act: render the workspace with no view declarations
        var results = diagramRenderer.RenderWorkspace(workspace, renderer, options);

        // Assert: no render outputs are produced
        Assert.Empty(results);
    }

    /// <summary>
    ///     Themes.Light is non-null, has at least one DepthFillColor, and has a non-empty
    ///     StrokeColor. Confirms that static property initialization does not throw.
    /// </summary>
    [Fact]
    public void Themes_Light_IsInitialized()
    {
        // Arrange / Act: access the static Light theme
        var theme = Themes.Light;

        // Assert: non-null, at least one fill color, non-empty stroke color
        Assert.NotNull(theme);
        Assert.True(theme.DepthFillColors.Count >= 1);
        Assert.False(string.IsNullOrEmpty(theme.StrokeColor));
    }

    /// <summary>
    ///     Themes.Dark is non-null, has at least one DepthFillColor, and has a non-empty
    ///     StrokeColor.
    /// </summary>
    [Fact]
    public void Themes_Dark_IsInitialized()
    {
        // Arrange / Act: access the static Dark theme
        var theme = Themes.Dark;

        // Assert: non-null, at least one fill color, non-empty stroke color
        Assert.NotNull(theme);
        Assert.True(theme.DepthFillColors.Count >= 1);
        Assert.False(string.IsNullOrEmpty(theme.StrokeColor));
    }

    /// <summary>
    ///     Themes.Print is non-null, has at least one DepthFillColor, and has a non-empty
    ///     StrokeColor.
    /// </summary>
    [Fact]
    public void Themes_Print_IsInitialized()
    {
        // Arrange / Act: access the static Print theme
        var theme = Themes.Print;

        // Assert: non-null, at least one fill color, non-empty stroke color
        Assert.NotNull(theme);
        Assert.True(theme.DepthFillColors.Count >= 1);
        Assert.False(string.IsNullOrEmpty(theme.StrokeColor));
    }

    /// <summary>
    ///     A RenderOptions constructed with only the required Theme parameter has Scale == 1.0.
    /// </summary>
    [Fact]
    public void RenderOptions_DefaultScale_IsOne()
    {
        // Arrange / Act: construct RenderOptions with only the required Theme
        var options = new RenderOptions(Themes.Light);

        // Assert: Scale defaults to 1.0
        Assert.Equal(1.0, options.Scale);
    }

    /// <summary>
    ///     A RenderOptions constructed with only the required Theme parameter has Dpi == 96.0.
    /// </summary>
    [Fact]
    public void RenderOptions_DefaultDpi_Is96()
    {
        // Arrange / Act: construct RenderOptions with only the required Theme
        var options = new RenderOptions(Themes.Light);

        // Assert: Dpi defaults to 96.0
        Assert.Equal(96.0, options.Dpi);
    }

    /// <summary>
    ///     A RenderOptions constructed with only the required Theme parameter has DepthLimit == 0,
    ///     confirming the unlimited rendering sentinel value.
    /// </summary>
    [Fact]
    public void RenderOptions_DefaultDepthLimit_IsZero()
    {
        // Arrange / Act: construct RenderOptions with only the required Theme
        var options = new RenderOptions(Themes.Light);

        // Assert: DepthLimit defaults to 0 (unlimited)
        Assert.Equal(0, options.DepthLimit);
    }

    /// <summary>
    ///     A RenderOutput constructed with all three parameters stores each field as supplied.
    /// </summary>
    [Fact]
    public void RenderOutput_Construction_StoresAllFields()
    {
        // Arrange: a MemoryStream as the data payload
        using var data = new MemoryStream();

        // Act: construct a RenderOutput with all three parameters
        var output = new RenderOutput("diagram.svg", "image/svg+xml", data);

        // Assert: all three properties equal the supplied values
        Assert.Equal("diagram.svg", output.SuggestedFileName);
        Assert.Equal("image/svg+xml", output.MediaType);
        Assert.Same(data, output.Data);
    }

    /// <summary>
    ///     A ViewContext constructed with a ViewName and a SysmlWorkspace stores both fields
    ///     as supplied.
    /// </summary>
    [Fact]
    public void ViewContext_Construction_StoresAllFields()
    {
        // Arrange: a minimal SysmlWorkspace
        var workspace = new SysmlWorkspace();

        // Act: construct a ViewContext with a view name and the workspace
        var context = new ViewContext("myView", workspace);

        // Assert: both fields equal the supplied values
        Assert.Equal("myView", context.ViewName);
        Assert.Same(workspace, context.Workspace);
    }
}
