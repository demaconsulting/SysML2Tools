// <copyright file="RenderIntegrationTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Png;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Stdlib;
using DemaConsulting.SysML2Tools.Svg;

namespace DemaConsulting.SysML2Tools.Tests.Rendering;

/// <summary>
///     Integration tests for the full rendering pipeline: WorkspaceLoader → DiagramRenderer → IRenderer.
/// </summary>
public sealed class RenderIntegrationTests
{
    /// <summary>
    ///     Locates the <c>test/SysMLModels</c> root by walking up from the assembly's base directory.
    /// </summary>
    /// <returns>
    ///     The absolute path to the <c>test/SysMLModels</c> directory, or <see langword="null"/>
    ///     when the directory cannot be found (e.g., the test is running from an unexpected location).
    /// </returns>
    private static string? FindSysMLModelsRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "test", "SysMLModels");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        return null;
    }

    /// <summary>
    ///     Path to the nested-packages-with-view test fixture: two nested packages,
    ///     several part defs, and one view def.
    /// </summary>
    private static string SoftwareStructureModel =>
        Path.Combine(FindSysMLModelsRoot() ?? "SysMLModels", "Custom", "nested-packages-with-view.sysml");

    /// <summary>
    ///     Inline SysML source for the general-view end-to-end tests.  The package contains two
    ///     <c>part def</c> elements and a <c>view def</c> so the rendering pipeline produces output.
    /// </summary>
    private const string GeneralViewSysml = """
        package GeneralViewTest {
            part def ComponentA {}
            part def ComponentB specializes ComponentA {}
            view def GeneralView {}
        }
        """;

    /// <summary>
    ///     DiagramRenderer.RenderWorkspace on a workspace loaded from the sysml2tools-architecture
    ///     model produces SVG output for the declared view.
    /// </summary>
    [Fact]
    public async Task DiagramRenderer_RenderWorkspace_SoftwareStructureModel_ReturnsSvgOutput()
    {
        // Arrange: load workspace from the sysml2tools-architecture model file
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([SoftwareStructureModel], stdlibTable);
        Assert.NotNull(result.Workspace); // Pre-condition: workspace must load
        var diagramRenderer = new DiagramRenderer();
        var svgRenderer = new SvgRenderer();
        var options = new RenderOptions(Themes.Light);

        // Act: render the workspace
        var outputs = diagramRenderer.RenderWorkspace(result.Workspace, svgRenderer, options);

        // Assert: exactly one output for the single declared view, with non-empty SVG content
        var output = Assert.Single(outputs);
        Assert.True(((MemoryStream)output.Data).ToArray().Length > 0, "SVG output stream is empty");
    }

    /// <summary>
    ///     DiagramRenderer.RenderWorkspace on a workspace loaded from the sysml2tools-architecture
    ///     model produces PNG output for the declared view.
    /// </summary>
    [Fact]
    public async Task DiagramRenderer_RenderWorkspace_SoftwareStructureModel_PngRenderer_ReturnsPngOutput()
    {
        // Arrange: load workspace from the sysml2tools-architecture model file
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([SoftwareStructureModel], stdlibTable);
        Assert.NotNull(result.Workspace); // Pre-condition: workspace must load
        var diagramRenderer = new DiagramRenderer();
        var pngRenderer = new PngRenderer();
        var options = new RenderOptions(Themes.Light);

        // Act: render the workspace
        var outputs = diagramRenderer.RenderWorkspace(result.Workspace, pngRenderer, options);

        // Assert: exactly one output for the single declared view, with non-empty PNG content
        var output = Assert.Single(outputs);
        Assert.True(((MemoryStream)output.Data).ToArray().Length > 0, "PNG output stream is empty");
    }

    /// <summary>
    ///     DiagramRenderer.RenderWorkspace on a workspace that contains part definitions and
    ///     a view definition produces SVG output whose text content includes the names of
    ///     the rendered elements, proving the full pipeline produces meaningful output.
    /// </summary>
    [Fact]
    public async Task DiagramRenderer_RenderWorkspace_GeneralViewModel_SvgContainsElementNames()
    {
        // Arrange: write the inline model to a temp file and load the workspace
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, GeneralViewSysml, TestContext.Current.CancellationToken);
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);
            Assert.NotNull(result.Workspace); // Pre-condition: workspace must load
            var diagramRenderer = new DiagramRenderer();
            var svgRenderer = new SvgRenderer();
            var options = new RenderOptions(Themes.Light);

            // Act: render the workspace — expect one output for the single view definition
            var outputs = diagramRenderer.RenderWorkspace(result.Workspace, svgRenderer, options);

            // Assert: exactly one output for the single declared view
            var output = Assert.Single(outputs);

            // Assert: the SVG text is non-empty and includes the part definition element names
            var svgText = System.Text.Encoding.UTF8.GetString(((MemoryStream)output.Data).ToArray());
            Assert.NotEmpty(svgText);
            Assert.Contains("ComponentA", svgText);
            Assert.Contains("ComponentB", svgText);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     DiagramRenderer.RenderWorkspace on a workspace that contains a view definition
    ///     produces PNG output that is non-trivially sized and starts with the standard
    ///     PNG file signature, confirming the PNG pipeline generates a valid image.
    /// </summary>
    [Fact]
    public async Task DiagramRenderer_RenderWorkspace_GeneralViewModel_PngProducesValidOutput()
    {
        // Arrange: write the inline model to a temp file and load the workspace
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, GeneralViewSysml, TestContext.Current.CancellationToken);
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);
            Assert.NotNull(result.Workspace); // Pre-condition: workspace must load
            var diagramRenderer = new DiagramRenderer();
            var pngRenderer = new PngRenderer();
            var options = new RenderOptions(Themes.Light);

            // Act: render the workspace using the PNG renderer
            var outputs = diagramRenderer.RenderWorkspace(result.Workspace, pngRenderer, options);

            // Assert: exactly one output for the single declared view
            var output = Assert.Single(outputs);

            // Assert: the output stream starts with the PNG file signature (‰PNG)
            var bytes = ((MemoryStream)output.Data).ToArray();
            Assert.True(bytes.Length > 100, "PNG output is unexpectedly small — likely empty or degenerate");
            Assert.Equal(0x89, bytes[0]);
            Assert.Equal(0x50, bytes[1]); // 'P'
            Assert.Equal(0x4E, bytes[2]); // 'N'
            Assert.Equal(0x47, bytes[3]); // 'G'
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Loading a model that uses same-package short-name specialization produces no
    ///     unresolved-reference diagnostics originating from user-authored files, confirming
    ///     that the reference resolver handles unqualified names in the same package correctly.
    /// </summary>
    [Fact]
    public async Task DiagramRenderer_RenderWorkspace_GeneralViewModel_NoUnresolvedWarnings()
    {
        // Arrange: write the inline model to a temp file and load the workspace
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, GeneralViewSysml, TestContext.Current.CancellationToken);
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);
            Assert.NotNull(result.Workspace); // Pre-condition: workspace must load

            // Act / Assert: filter diagnostics to those originating from user files
            // (user file paths contain a directory separator, stdlib entries typically do not)
            var unresolvedFromUserFiles = result.Diagnostics
                .Where(d =>
                    d.Message.Contains("Unresolved reference") &&
                    d.FilePath.Contains(Path.DirectorySeparatorChar))
                .ToList();

            Assert.Empty(unresolvedFromUserFiles);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}

