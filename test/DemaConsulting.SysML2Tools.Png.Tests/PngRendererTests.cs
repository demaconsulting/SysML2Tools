// <copyright file="PngRendererTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout;
using DemaConsulting.SysML2Tools.Png;
using DemaConsulting.SysML2Tools.Rendering;

namespace DemaConsulting.SysML2Tools.Png.Tests;

/// <summary>
///     Tests for the PNG renderer.
/// </summary>
public sealed class PngRendererTests
{
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
}
