// <copyright file="RenderIntegrationTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Png;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Svg;

namespace DemaConsulting.SysML2Tools.Tests.Rendering;

/// <summary>
///     Integration tests for the full rendering pipeline: WorkspaceLoader → DiagramRenderer → IRenderer.
/// </summary>
public sealed class RenderIntegrationTests
{
    /// <summary>
    ///     Path to the software-structure test model that contains no view definitions.
    /// </summary>
    private static readonly string SoftwareStructureModel =
        Path.Combine("SysMLModels", "software-structure.sysml");

    /// <summary>
    ///     DiagramRenderer.RenderWorkspace on a workspace loaded from the software-structure
    ///     model (which has no view definitions) returns an empty list of render outputs.
    /// </summary>
    [Fact]
    public async Task DiagramRenderer_RenderWorkspace_SoftwareStructureModel_ReturnsEmptyList()
    {
        // Arrange: load workspace from the software-structure model file
        var result = await WorkspaceLoader.LoadAsync([SoftwareStructureModel]);
        Assert.NotNull(result.Workspace); // Pre-condition: workspace must load
        var diagramRenderer = new DiagramRenderer();
        var svgRenderer = new SvgRenderer();
        var options = new RenderOptions(Themes.Light);

        // Act: render the workspace (which has no views)
        var outputs = diagramRenderer.RenderWorkspace(result.Workspace, svgRenderer, options);

        // Assert: no outputs because the model has no view declarations
        Assert.Empty(outputs);
    }

    /// <summary>
    ///     DiagramRenderer.RenderWorkspace on a workspace loaded from the software-structure
    ///     model produces no PNG outputs either, confirming the empty-list result is
    ///     renderer-agnostic.
    /// </summary>
    [Fact]
    public async Task DiagramRenderer_RenderWorkspace_SoftwareStructureModel_PngRenderer_ReturnsEmptyList()
    {
        // Arrange: load workspace from the software-structure model file
        var result = await WorkspaceLoader.LoadAsync([SoftwareStructureModel]);
        Assert.NotNull(result.Workspace); // Pre-condition: workspace must load
        var diagramRenderer = new DiagramRenderer();
        var pngRenderer = new PngRenderer();
        var options = new RenderOptions(Themes.Light);

        // Act: render the workspace (which has no views)
        var outputs = diagramRenderer.RenderWorkspace(result.Workspace, pngRenderer, options);

        // Assert: no outputs because the model has no view declarations
        Assert.Empty(outputs);
    }
}
