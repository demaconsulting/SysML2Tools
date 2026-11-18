// <copyright file="RenderIntegrationTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.CanvasNet;
using DemaConsulting.Rendering.Svg;
using DemaConsulting.SysML2Tools.Parser;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Stdlib;

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
    ///     Path to the safety-metadata-filter test fixture: a package with a <c>metadata def</c>,
    ///     part definitions carrying <c>@Safety</c> annotations, two views whose <c>filter</c>
    ///     statements exercise Phase 1 filter-expression evaluation end-to-end, and (Phase 2a) a
    ///     named <c>view</c> usage whose bracket-form <c>expose</c> statement exercises bracket-filter
    ///     scope narrowing end-to-end.
    /// </summary>
    private static string SafetyMetadataFilterModel =>
        Path.Combine(FindSysMLModelsRoot() ?? "SysMLModels", "Custom", "safety-metadata-filter.sysml");

    /// <summary>
    ///     Rendering the <c>SafetyPartsView</c> (<c>filter @Safety;</c>) from the safety-metadata-filter
    ///     fixture produces SVG output that includes only the definitions carrying the <c>@Safety</c>
    ///     metadata annotation (<c>Actuator</c>, <c>Gripper</c>) and excludes the unannotated
    ///     <c>Bracket</c> definition — proving the evaluator actually narrows the rendered scope.
    /// </summary>
    [Fact]
    public async Task DiagramRenderer_RenderWorkspace_SafetyPartsView_FiltersToAnnotatedParts()
    {
        // Arrange: load the safety-metadata-filter fixture workspace
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([SafetyMetadataFilterModel], stdlibTable);
        Assert.NotNull(result.Workspace); // Pre-condition: workspace must load
        var diagramRenderer = new DiagramRenderer();
        var svgRenderer = new SvgRenderer();
        var options = new RenderOptions(Themes.Light);

        // Act: render the workspace, and locate the SafetyPartsView output by name
        var viewNames = DiagramRenderer.GetViewNames(result.Workspace);
        var outputs = diagramRenderer.RenderWorkspace(result.Workspace, svgRenderer, options);
        var index = viewNames.ToList().FindIndex(n => n.Contains("SafetyPartsView"));
        Assert.True(index >= 0, "SafetyPartsView not found among rendered views");
        var svgText = System.Text.Encoding.UTF8.GetString(((MemoryStream)outputs[index].Data).ToArray());

        // Assert: only the @Safety-annotated definitions are rendered
        Assert.Contains("Actuator", svgText);
        Assert.Contains("Gripper", svgText);
        Assert.DoesNotContain("Bracket", svgText);
    }

    /// <summary>
    ///     Rendering the <c>MandatorySafetyPartsView</c> (<c>filter @Safety and (as Safety).isMandatory;</c>)
    ///     from the safety-metadata-filter fixture produces SVG output that includes only the
    ///     <c>Actuator</c> definition (the sole definition whose <c>@Safety</c> annotation has
    ///     <c>isMandatory = true</c>) — proving boolean-connective and attribute-read evaluation
    ///     compose correctly end-to-end through the rendering pipeline.
    /// </summary>
    [Fact]
    public async Task DiagramRenderer_RenderWorkspace_MandatorySafetyPartsView_FiltersToMandatoryPart()
    {
        // Arrange: load the safety-metadata-filter fixture workspace
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([SafetyMetadataFilterModel], stdlibTable);
        Assert.NotNull(result.Workspace); // Pre-condition: workspace must load
        var diagramRenderer = new DiagramRenderer();
        var svgRenderer = new SvgRenderer();
        var options = new RenderOptions(Themes.Light);

        // Act: render the workspace, and locate the MandatorySafetyPartsView output by name
        var viewNames = DiagramRenderer.GetViewNames(result.Workspace);
        var outputs = diagramRenderer.RenderWorkspace(result.Workspace, svgRenderer, options);
        var index = viewNames.ToList().FindIndex(n => n.Contains("MandatorySafetyPartsView"));
        Assert.True(index >= 0, "MandatorySafetyPartsView not found among rendered views");
        var svgText = System.Text.Encoding.UTF8.GetString(((MemoryStream)outputs[index].Data).ToArray());

        // Assert: only the mandatory-Safety-annotated definition is rendered
        Assert.Contains("Actuator", svgText);
        Assert.DoesNotContain("Gripper", svgText);
        Assert.DoesNotContain("Bracket", svgText);
    }

    /// <summary>
    ///     Rendering the <c>BracketExposeMandatorySafetyView</c> (a named <c>view</c> usage declaring
    ///     <c>expose RobotArm::**[@Safety and (as Safety).isMandatory];</c>) from the
    ///     safety-metadata-filter fixture produces SVG output that includes only the <c>Actuator</c>
    ///     definition — proving Phase 2a's bracket-form <c>expose</c> filter evaluation narrows the
    ///     rendered scope end-to-end through the rendering pipeline, and that no spurious
    ///     "unevaluated" bracket-filter warning is emitted for a filter that evaluates successfully.
    /// </summary>
    [Fact]
    public async Task DiagramRenderer_RenderWorkspace_BracketExposeMandatorySafetyView_FiltersToMandatoryPart()
    {
        // Arrange: load the safety-metadata-filter fixture workspace
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([SafetyMetadataFilterModel], stdlibTable);
        Assert.NotNull(result.Workspace); // Pre-condition: workspace must load
        var diagramRenderer = new DiagramRenderer();
        var svgRenderer = new SvgRenderer();
        var options = new RenderOptions(Themes.Light);

        // Act: render the workspace, and locate the BracketExposeMandatorySafetyView output by name
        var viewNames = DiagramRenderer.GetViewNames(result.Workspace);
        var outputs = diagramRenderer.RenderWorkspace(result.Workspace, svgRenderer, options);
        var index = viewNames.ToList().FindIndex(n => n.Contains("BracketExposeMandatorySafetyView"));
        Assert.True(index >= 0, "BracketExposeMandatorySafetyView not found among rendered views");
        var svgText = System.Text.Encoding.UTF8.GetString(((MemoryStream)outputs[index].Data).ToArray());

        // Assert: only the mandatory-Safety-annotated definition is rendered, and no
        // "unevaluated" bracket-filter warning is present for this successfully-evaluated filter
        Assert.Contains("Actuator", svgText);
        Assert.DoesNotContain("Gripper", svgText);
        Assert.DoesNotContain("Bracket", svgText);
        Assert.DoesNotContain(outputs[index].Warnings, w => w.Contains("could not be evaluated"));
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

    /// <summary>
    ///     Loads <paramref name="sysml"/> from a temp file, renders it to SVG while collecting
    ///     render diagnostics, and returns the outputs and diagnostics.
    /// </summary>
    /// <param name="sysml">The SysML source text to render.</param>
    /// <param name="tempFile">The temp file the source was written to (caller deletes it).</param>
    /// <returns>The render outputs and the collected render diagnostics.</returns>
    private static async Task<(IReadOnlyList<RenderOutput> Outputs, List<SysmlDiagnostic> Diagnostics)>
        RenderWithDiagnosticsAsync(string sysml, string tempFile)
    {
        await File.WriteAllTextAsync(tempFile, sysml, TestContext.Current.CancellationToken);
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);
        Assert.NotNull(result.Workspace); // Pre-condition: workspace must load

        var diagnostics = new List<SysmlDiagnostic>();
        var outputs = new DiagramRenderer().RenderWorkspace(
            result.Workspace, new SvgRenderer(), new RenderOptions(Themes.Light), viewFilter: null, diagnostics: diagnostics);
        return (outputs, diagnostics);
    }

    /// <summary>
    ///     A view whose only expose is a bare <c>expose Pkg;</c> (a MembershipExpose of the package
    ///     element itself, SysML 8.3.26.3) renders the package as a package node without its
    ///     contents; the renderer reports a located informational hint (not a warning) suggesting
    ///     <c>expose Pkg::**;</c> to show the members.
    /// </summary>
    [Fact]
    public async Task DiagramRenderer_RenderWorkspace_ViewExposesBarePackage_RendersPackageNodeWithInfoHint()
    {
        // Arrange: a package with members exposed only as the bare package element
        const string sysml = """
            package Pkg {
                part def Block1 {}
                part def Block2 {}
            }
            package Diagrams {
                view StructureView {
                    expose Pkg;
                }
            }
            """;
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".sysml");
        try
        {
            // Act: render the workspace collecting diagnostics
            var (outputs, diagnostics) = await RenderWithDiagnosticsAsync(sysml, tempFile);

            // Assert: the package node is drawn (its name, not its members), and one located
            // informational hint is reported
            var output = Assert.Single(outputs);
            var svg = System.Text.Encoding.UTF8.GetString(((MemoryStream)output.Data).ToArray());
            Assert.Contains(">Pkg<", svg);
            Assert.DoesNotContain("Block1", svg);
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal(DiagnosticSeverity.Info, diagnostic.Severity);
            Assert.Equal(tempFile, diagnostic.FilePath);
            Assert.Equal(6, diagnostic.Line);
            Assert.Contains("'StructureView'", diagnostic.Message);
            Assert.Contains("'expose Pkg::**;'", diagnostic.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A bare <c>expose Pkg;</c> in a view routed to a layout that does not draw package nodes
    ///     (here an interconnection view, which draws its exposed root part def) reports no
    ///     lone-package hint, since no package node is drawn.
    /// </summary>
    [Fact]
    public async Task DiagramRenderer_RenderWorkspace_NonGeneralViewExposesBarePackage_ReportsNoHint()
    {
        // Arrange: an interconnection view exposing a renderable root plus a bare package
        const string sysml = """
            package Pkg { part def Block1 {} }
            package Sys { part def Leaf; part def Root { part a : Leaf; part b : Leaf; } }
            package Diagrams {
                view WiringView {
                    expose Sys::Root::**;
                    expose Pkg;
                    render asInterconnectionDiagram;
                }
            }
            """;
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".sysml");
        try
        {
            // Act
            var (outputs, diagnostics) = await RenderWithDiagnosticsAsync(sysml, tempFile);

            // Assert: the view renders and no diagnostic claims a package node was drawn
            Assert.Single(outputs);
            Assert.Empty(diagnostics);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A view whose exposed scope contains nothing renderable (here the members of an empty
    ///     package, <c>expose Pkg::*;</c>) is still rendered, and a located warning is reported.
    /// </summary>
    [Fact]
    public async Task DiagramRenderer_RenderWorkspace_ViewWithNothingRenderable_ReportsEmptyViewWarning()
    {
        // Arrange
        const string sysml = """
            package Pkg { }
            package Diagrams {
                view EmptyView {
                    expose Pkg::*;
                }
            }
            """;
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".sysml");
        try
        {
            // Act
            var (outputs, diagnostics) = await RenderWithDiagnosticsAsync(sysml, tempFile);

            // Assert
            Assert.Single(outputs);
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("'EmptyView'", diagnostic.Message);
            Assert.Contains("nothing renderable", diagnostic.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A view exposing a package's members recursively (<c>expose Pkg::**;</c>) renders
    ///     content and reports no empty-view warning.
    /// </summary>
    [Fact]
    public async Task DiagramRenderer_RenderWorkspace_ViewExposesPackageRecursively_ReportsNoWarning()
    {
        // Arrange: the same package exposed with the recursive '::**' form
        const string sysml = """
            package Pkg {
                part def Block1 {}
                part def Block2 {}
            }
            package Diagrams {
                view StructureView {
                    expose Pkg::**;
                }
            }
            """;
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".sysml");
        try
        {
            // Act: render the workspace collecting diagnostics
            var (outputs, diagnostics) = await RenderWithDiagnosticsAsync(sysml, tempFile);

            // Assert: the view is rendered and no diagnostic is reported
            Assert.Single(outputs);
            Assert.Empty(diagnostics);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A view exposing a single element that has renderable content (a bare
    ///     <c>expose Pkg::Block1;</c> of a part definition) reports no empty-view warning.
    /// </summary>
    [Fact]
    public async Task DiagramRenderer_RenderWorkspace_ViewExposesRenderableElement_ReportsNoWarning()
    {
        // Arrange: a non-recursive expose of a part definition, which renders as its own box
        const string sysml = """
            package Pkg {
                part def Block1 {}
                part def Block2 {}
            }
            package Diagrams {
                view BlockView {
                    expose Pkg::Block1;
                }
            }
            """;
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".sysml");
        try
        {
            // Act: render the workspace collecting diagnostics
            var (outputs, diagnostics) = await RenderWithDiagnosticsAsync(sysml, tempFile);

            // Assert: the view is rendered and no diagnostic is reported
            Assert.Single(outputs);
            Assert.Empty(diagnostics);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Locates the <c>docs/gallery/models</c> directory by walking up from the assembly's base directory.
    /// </summary>
    /// <returns>The absolute path of the gallery models directory.</returns>
    private static string FindGalleryModels()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "docs", "gallery", "models");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new DirectoryNotFoundException("docs/gallery/models was not found above the test assembly.");
    }

    /// <summary>
    ///     Renders one view of a gallery model to SVG with the plain SysML theme.
    /// </summary>
    /// <param name="model">The gallery model file name.</param>
    /// <param name="view">The name of the view to render.</param>
    /// <returns>The SVG text.</returns>
    private static async Task<string> RenderGalleryViewAsync(string model, string view)
    {
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([Path.Combine(FindGalleryModels(), model)], stdlibTable);
        Assert.NotNull(result.Workspace);
        var outputs = new DiagramRenderer().RenderWorkspace(
            result.Workspace, new SvgRenderer(), new RenderOptions(SysmlThemes.Plain), view);
        var output = Assert.Single(outputs);
        return System.Text.Encoding.UTF8.GetString(((MemoryStream)output.Data).ToArray());
    }

    /// <summary>
    ///     Returns the attribute text of every <c>&lt;text&gt;</c> element whose content is exactly the given text.
    /// </summary>
    /// <param name="svg">The SVG document.</param>
    /// <param name="content">The exact text content to look for.</param>
    /// <returns>The attribute lists of the matching text elements.</returns>
    private static List<string> TextAttributes(string svg, string content) =>
        [.. System.Text.RegularExpressions.Regex
            .Matches(svg, "<text\\b([^>]*)>" + System.Text.RegularExpressions.Regex.Escape(content) + "</text>")
            .Select(m => m.Groups[1].Value)];

    /// <summary>
    ///     The name of a node is drawn in regular weight (no <c>font-weight</c>) in every view kind: the
    ///     name of a definition and a package tab (general), a part and the frame (interconnection),
    ///     a state and its frame, an action, a lifeline header, and a browser row.
    /// </summary>
    /// <param name="model">The gallery model file.</param>
    /// <param name="view">The view to render.</param>
    /// <param name="name">The exact text of a node name drawn by that view.</param>
    [Theory]
    [InlineData("01-drone-general.sysml", "DroneGeneralView", "Drone")]
    [InlineData("01-drone-general.sysml", "DroneGeneralView", "QuadcopterDrone")]
    [InlineData("01-drone-general.sysml", "DroneGeneralView", "airframe : Frame")]
    [InlineData("08-nested-interconnection.sysml", "ComputerSystemInterconnectionView", "Computer")]
    [InlineData("08-nested-interconnection.sysml", "ComputerSystemInterconnectionView", "psu : PowerSupply")]
    [InlineData("03-elevator-state.sysml", "ElevatorStateTransitionView", "ElevatorStates")]
    [InlineData("03-elevator-state.sysml", "ElevatorStateTransitionView", "idle")]
    [InlineData("04-pipeline-action-flow.sysml", "PipelineActionFlowView", "checkout")]
    [InlineData("05-oauth-sequence.sysml", "OAuthSequenceView", "user : User")]
    [InlineData("07-avionics-browser.sysml", "AvionicsBrowserView", "«package» AvionicsSystem")]
    public async Task DiagramRenderer_RenderWorkspace_NodeName_IsRegularWeight(string model, string view, string name)
    {
        // Act: render the view
        var svg = await RenderGalleryViewAsync(model, view);

        // Assert: the name is drawn, and none of its text elements is bold
        var elements = TextAttributes(svg, name);
        Assert.NotEmpty(elements);
        Assert.All(elements, attributes => Assert.DoesNotContain("font-weight", attributes));
    }

    /// <summary>
    ///     An abstract definition's name stays italic in regular weight, its keyword line stays italic,
    ///     and a concrete definition's name is upright regular weight.
    /// </summary>
    [Fact]
    public async Task DiagramRenderer_RenderWorkspace_AbstractDefinitionName_IsItalicRegularWeight()
    {
        // Act: render the drive unit view, which holds the abstract definition Motor and the concrete DriveUnit
        var svg = await RenderGalleryViewAsync("14-usages-typing-general.sysml", "DriveUnitGeneralView");

        // Assert: Motor is italic and not bold; DriveUnit is neither; the abstract keyword is italic
        var abstractName = Assert.Single(TextAttributes(svg, "Motor"));
        Assert.Contains("font-style=\"italic\"", abstractName);
        Assert.DoesNotContain("font-weight", abstractName);
        var concreteName = Assert.Single(TextAttributes(svg, "DriveUnit"));
        Assert.DoesNotContain("font-style", concreteName);
        Assert.DoesNotContain("font-weight", concreteName);
        Assert.Contains("font-style=\"italic\"", Assert.Single(TextAttributes(svg, "«abstract part def»")));
    }
}

