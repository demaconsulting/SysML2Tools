// Copyright (c) DEMA Consulting
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using DemaConsulting.SysML2Tools.Cli;

namespace DemaConsulting.SysML2Tools.Tests.Render;

/// <summary>
///     Subsystem tests for the Render command covering file-pattern validation, workspace
///     loading, format selection, output directory routing, and empty-workspace behavior.
/// </summary>
[Collection("Sequential")]
public class RenderSubsystemTests
{
    /// <summary>
    ///     A minimal SysML model that contains a view definition and one part def.
    ///     Used by format and output-directory tests that require rendered output.
    /// </summary>
    private const string SysmlWithView = """
        package RenderTest {
            part def Block1 {}
            view def GeneralView {}
        }
        """;

    /// <summary>
    ///     RenderCommand reports an error when no file patterns are supplied.
    /// </summary>
    [Fact]
    public async Task RenderSubsystem_NoFiles_ReportsError()
    {
        // Arrange
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);

            // Act: render with no positional file arguments
            using var context = Context.Create(["render"]);
            await Program.RunAsync(context);

            // Assert: expected diagnostic text written and exit code indicates failure
            Assert.Contains("no input files specified", errWriter.ToString());
            Assert.Equal(1, context.ExitCode);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     RenderCommand loads a valid SysML workspace without error diagnostics.
    /// </summary>
    [Fact]
    public async Task RenderSubsystem_WithFiles_LoadsWorkspace()
    {
        // Arrange: write a minimal valid SysML file to a temp location
        var tempFile = Path.Combine(Path.GetTempPath(), $"render_load_{Guid.NewGuid():N}.sysml");
        await File.WriteAllTextAsync(tempFile, "package LoadTest {}", TestContext.Current.CancellationToken);

        var originalOut = Console.Out;
        var originalError = Console.Error;
        try
        {
            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();
            Console.SetOut(outWriter);
            Console.SetError(errWriter);

            // Act: render a valid model (no views expected, but load should succeed)
            using var context = Context.Create(["render", tempFile]);
            await Program.RunAsync(context);

            // Assert: no load errors; "Loading" progress message was written
            Assert.Contains("Loading", outWriter.ToString());
            Assert.DoesNotContain("workspace loading failed", errWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    /// <summary>
    ///     RenderCommand with --format svg writes output files with the .svg extension.
    /// </summary>
    [Fact]
    public async Task RenderSubsystem_FormatSvg_UsesSvgRenderer()
    {
        // Arrange: write a SysML model with a view definition; create temp output dir
        var tempDir = Path.Combine(Path.GetTempPath(), $"render_svg_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "model.sysml");
        await File.WriteAllTextAsync(tempFile, SysmlWithView, TestContext.Current.CancellationToken);

        var outputDir = Path.Combine(tempDir, "out");

        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);

            // Act: render with SVG format and explicit output directory
            using var context = Context.Create(
                ["render", "--format", "svg", "--output", outputDir, tempFile]);
            await Program.RunAsync(context);

            // Assert: at least one .svg file was written to the output directory
            var svgFiles = Directory.GetFiles(outputDir, "*.svg");
            Assert.True(svgFiles.Length > 0, "Expected at least one .svg output file");
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    ///     RenderCommand with --format png writes output files with the .png extension.
    /// </summary>
    [Fact]
    public async Task RenderSubsystem_FormatPng_UsesPngRenderer()
    {
        // Guard: check if SkiaSharp native library is loadable without triggering type
        // initializers. Return early when the library is absent so no TypeInitializationException
        // propagates through xUnit's cleanup infrastructure.
        if (!System.Runtime.InteropServices.NativeLibrary.TryLoad("libSkiaSharp", out var nativeHandle))
        {
            // SkiaSharp native runtime unavailable in this build environment; skip rendering.
            return;
        }

        System.Runtime.InteropServices.NativeLibrary.Free(nativeHandle);

        // Arrange: write a SysML model with a view definition; create temp output dir
        var tempDir = Path.Combine(Path.GetTempPath(), $"render_png_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "model.sysml");
        await File.WriteAllTextAsync(tempFile, SysmlWithView, TestContext.Current.CancellationToken);

        var outputDir = Path.Combine(tempDir, "out");

        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);

            // Act: render with PNG format and explicit output directory
            using var context = Context.Create(
                ["render", "--format", "png", "--output", outputDir, tempFile]);
            await Program.RunAsync(context);

            // Assert: at least one .png file was written to the output directory
            var pngFiles = Directory.GetFiles(outputDir, "*.png");
            Assert.True(pngFiles.Length > 0, "Expected at least one .png output file");
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    ///     RenderCommand without --output writes output files to the current working directory.
    /// </summary>
    [Fact]
    public async Task RenderSubsystem_NoOutputDir_UsesCurrentDirectory()
    {
        // Arrange: write a SysML model with a view definition to a temp directory;
        // set that temp directory as the CWD so output lands in a controlled location
        var tempDir = Path.Combine(Path.GetTempPath(), $"render_cwd_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "model.sysml");
        await File.WriteAllTextAsync(tempFile, SysmlWithView, TestContext.Current.CancellationToken);

        var originalCwd = Directory.GetCurrentDirectory();
        var originalOut = Console.Out;
        try
        {
            Directory.SetCurrentDirectory(tempDir);
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);

            // Act: render without --output; files should go to the current working directory
            using var context = Context.Create(["render", "--format", "svg", tempFile]);
            await Program.RunAsync(context);

            // Assert: at least one .svg file exists in the current working directory
            var svgFiles = Directory.GetFiles(tempDir, "*.svg");
            Assert.True(svgFiles.Length > 0,
                "Expected at least one .svg file in the current working directory");
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
            Directory.SetCurrentDirectory(originalCwd);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    ///     RenderCommand reports an informational message and writes no files when the
    ///     workspace contains no view declarations.
    /// </summary>
    [Fact]
    public async Task RenderSubsystem_NoViews_ReportsNoOutput()
    {
        // Arrange: write a SysML model with no view declarations
        var tempFile = Path.Combine(
            Path.GetTempPath(), $"render_noviews_{Guid.NewGuid():N}.sysml");
        await File.WriteAllTextAsync(
            tempFile,
            "package NoViews { part def A {} }",
            TestContext.Current.CancellationToken);

        var outputDir = Path.Combine(Path.GetTempPath(), $"render_noviews_out_{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);

            // Act: render a workspace with no view declarations
            using var context = Context.Create(
                ["render", "--output", outputDir, tempFile]);
            await Program.RunAsync(context);

            // Assert: informational message written; no output files created; exit code is success
            Assert.Contains("No view", outWriter.ToString());
            var outputFiles = Directory.GetFiles(outputDir);
            Assert.Empty(outputFiles);
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }

            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    /// <summary>
    ///     RenderCommand renders with --depth 1 and the SVG output contains an ellipsis
    ///     character indicating that children were truncated at depth limit.
    /// </summary>
    [Fact]
    public async Task RenderSubsystem_WithDepth_LimitsNesting()
    {
        // Arrange: write a SysML model with a view and part defs; create temp output dir
        var tempDir = Path.Combine(Path.GetTempPath(), $"render_depth_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "model.sysml");
        await File.WriteAllTextAsync(tempFile, SysmlWithView, TestContext.Current.CancellationToken);

        var outputDir = Path.Combine(tempDir, "out");

        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);

            // Act: render with depth=1 to trigger the ellipsis truncation
            using var context = Context.Create(
                ["render", "--format", "svg", "--depth", "1", "--output", outputDir, tempFile]);
            await Program.RunAsync(context);

            // Assert: SVG output exists and contains the ellipsis marker
            Assert.Equal(0, context.ExitCode);
            var svgFiles = Directory.GetFiles(outputDir, "*.svg");
            Assert.True(svgFiles.Length > 0, "Expected at least one .svg output file");
            var svgContent = await File.ReadAllTextAsync(svgFiles[0], TestContext.Current.CancellationToken);
            Assert.Contains("…", svgContent);
        }
        finally
        {
            Console.SetOut(originalOut);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    ///     A SysML model containing two view definitions used by multi-view tests.
    /// </summary>
    private const string SysmlWithTwoViews = """
        package MultiViewTest {
            part def BlockA {}
            part def BlockB {}
            view def ViewAlpha {}
            view def ViewBeta {}
        }
        """;

    /// <summary>
    ///     RenderCommand renders every declared view when the workspace contains multiple
    ///     views and --view is not specified, producing one output file per view.
    /// </summary>
    [Fact]
    public async Task RenderSubsystem_MultipleViews_NoViewFlag_RendersAllViews()
    {
        // Arrange: write a SysML model with two views
        var tempDir = Path.Combine(Path.GetTempPath(), $"render_multi_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "model.sysml");
        await File.WriteAllTextAsync(tempFile, SysmlWithTwoViews, TestContext.Current.CancellationToken);

        var outputDir = Path.Combine(tempDir, "out");
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);

            // Act: render without --view flag
            using var context = Context.Create(["render", "--output", outputDir, tempFile]);
            await Program.RunAsync(context);

            // Assert: exit code indicates success; exactly two .svg files were produced
            Assert.Equal(0, context.ExitCode);
            var svgFiles = Directory.GetFiles(outputDir, "*.svg");
            Assert.Equal(2, svgFiles.Length);
        }
        finally
        {
            Console.SetOut(originalOut);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    ///     RenderCommand reports a collision error and writes no output files when two views in
    ///     different packages share the same simple name (and therefore the same sanitized
    ///     output file name).
    /// </summary>
    [Fact]
    public async Task RenderSubsystem_DuplicateViewFileNames_ReportsCollisionError()
    {
        // Arrange: write a SysML model with two packages each declaring a view of the same
        // simple name ("SharedView"), producing qualified names "PkgA::SharedView" and
        // "PkgB::SharedView" that both sanitize to the output file name "SharedView.svg"
        const string sysmlWithDuplicateViewNames = """
            package PkgA {
                part def BlockA {}
                view def SharedView {}
            }
            package PkgB {
                part def BlockB {}
                view def SharedView {}
            }
            """;

        var tempDir = Path.Combine(Path.GetTempPath(), $"render_collision_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "model.sysml");
        await File.WriteAllTextAsync(
            tempFile, sysmlWithDuplicateViewNames, TestContext.Current.CancellationToken);

        var outputDir = Path.Combine(tempDir, "out");
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);

            // Act: render without --view so both colliding views are considered for rendering
            using var context = Context.Create(["render", "--output", outputDir, tempFile]);
            await Program.RunAsync(context);

            // Assert: exit code indicates failure; error message names both colliding qualified
            // views and the shared file name; no output directory/files were created
            Assert.Equal(1, context.ExitCode);
            var errorText = errWriter.ToString();
            Assert.Contains("PkgA::SharedView", errorText);
            Assert.Contains("PkgB::SharedView", errorText);
            Assert.Contains("SharedView.svg", errorText);
            Assert.False(Directory.Exists(outputDir), "Expected no output directory to be created");
        }
        finally
        {
            Console.SetError(originalError);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    ///     RenderCommand reports an error listing available view names when --view names a
    ///     view that does not exist in the workspace.
    /// </summary>
    [Fact]
    public async Task RenderSubsystem_UnknownViewFlag_ReportsErrorWithAvailableViews()
    {
        // Arrange: write a SysML model with two named views
        var tempDir = Path.Combine(Path.GetTempPath(), $"render_unknown_view_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "model.sysml");
        await File.WriteAllTextAsync(tempFile, SysmlWithTwoViews, TestContext.Current.CancellationToken);

        var outputDir = Path.Combine(tempDir, "out");
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);

            // Act: render with --view naming a view that does not exist; use log to capture
            // output for assertion
            var logFile = Path.Combine(tempDir, "output.log");
            using (var context = Context.Create(
                       ["render", "--silent", "--log", logFile, "--view", "NoSuchView", "--output", outputDir, tempFile]))
            {
                await Program.RunAsync(context);

                // Assert: exit code indicates failure
                Assert.Equal(1, context.ExitCode);
            }

            // Assert: log content lists available view names
            var logContent = await File.ReadAllTextAsync(logFile, TestContext.Current.CancellationToken);
            Assert.Contains("ViewAlpha", logContent);
            Assert.Contains("ViewBeta", logContent);
        }
        finally
        {
            Console.SetError(originalError);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    ///     RenderCommand with an unsupported --format value throws ArgumentException naming the
    ///     bad value; validation happens in RunAsync, not at Context.Create parse time, mirroring
    ///     the query command's --format validation.
    /// </summary>
    [Fact]
    public async Task RenderSubsystem_UnsupportedFormat_ThrowsArgumentException()
    {
        // Arrange: write a minimal valid SysML file to a temp location
        var tempFile = Path.Combine(Path.GetTempPath(), $"render_bad_format_{Guid.NewGuid():N}.sysml");
        await File.WriteAllTextAsync(tempFile, "package LoadTest {}", TestContext.Current.CancellationToken);

        try
        {
            // Act: create the context with an unsupported --format value
            using var context = Context.Create(["render", "--format", "xml", tempFile]);

            // Assert: Program.RunAsync throws, naming the bad value
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => Program.RunAsync(context));
            Assert.Contains("xml", exception.Message);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    /// <summary>
    ///     RenderCommand with --view selects a specific view and renders it successfully.
    /// </summary>
    [Fact]
    public async Task RenderSubsystem_MultipleViews_WithViewFlag_RendersSelectedView()
    {
        // Arrange: write a SysML model with two views
        var tempDir = Path.Combine(Path.GetTempPath(), $"render_view_select_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "model.sysml");
        await File.WriteAllTextAsync(tempFile, SysmlWithTwoViews, TestContext.Current.CancellationToken);

        var outputDir = Path.Combine(tempDir, "out");
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);

            // Act: render with --view specifying one of the two views
            using var context = Context.Create(
                ["render", "--format", "svg", "--view", "ViewAlpha", "--output", outputDir, tempFile]);
            await Program.RunAsync(context);

            // Assert: exactly one SVG file was produced; exit code indicates success
            Assert.Equal(0, context.ExitCode);
            var svgFiles = Directory.GetFiles(outputDir, "*.svg");
            Assert.Single(svgFiles);
        }
        finally
        {
            Console.SetOut(originalOut);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    ///     End-to-end regression test: a workspace with two views — one with an
    ///     <c>expose &lt;target&gt;;</c> body statement naming a resolvable target, and one with a
    ///     bogus <c>expose thisIdentifierDoesNotExistAnywhere;</c> — must produce two visibly
    ///     DIFFERENT rendered outputs (a view with no <c>expose</c> edges renders the full
    ///     workspace, while a view whose sole <c>expose</c> entry resolves scopes to that target's
    ///     subtree), and the bogus view's unresolved exposed name must surface a diagnostic in the
    ///     captured log output. <c>render &lt;target&gt;;</c> plays no role in scoping — only
    ///     <c>expose</c> does, per the corrected semantics.
    /// </summary>
    [Fact]
    public async Task RenderSubsystem_ViewsWithDistinctExposeTargets_ProduceDifferingOutputsAndDiagnostic()
    {
        // Arrange: a workspace with two named expose targets in disjoint subtrees, one view
        // whose expose statement resolves to "TargetA", and one whose expose statement names a
        // nonexistent identifier.
        const string sysmlWithExposeTargets = """
            package ExposeScopeTest {
                part def TargetA {
                    part childA1 : TargetA {}
                }
                part def TargetB {
                    part childB1 : TargetB {}
                }
                view ViewValid {
                    expose TargetA;
                }
                view ViewBogus {
                    expose thisIdentifierDoesNotExistAnywhere;
                }
            }
            """;

        var tempDir = Path.Combine(Path.GetTempPath(), $"expose_scope_bug_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "model.sysml");
        await File.WriteAllTextAsync(tempFile, sysmlWithExposeTargets, TestContext.Current.CancellationToken);

        var outputDir = Path.Combine(tempDir, "out");
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);

            // Act: render without --view so both views are rendered; capture output via --log
            var logFile = Path.Combine(tempDir, "output.log");
            using (var context = Context.Create(
                       ["render", "--log", logFile, "--output", outputDir, tempFile]))
            {
                await Program.RunAsync(context);

                // Assert: exit code indicates success
                Assert.Equal(0, context.ExitCode);
            }
            var validSvg = Path.Combine(outputDir, "ViewValid.svg");
            var bogusSvg = Path.Combine(outputDir, "ViewBogus.svg");
            Assert.True(File.Exists(validSvg), "Expected ViewValid.svg to be produced");
            Assert.True(File.Exists(bogusSvg), "Expected ViewBogus.svg to be produced");
            var validContent = await File.ReadAllTextAsync(validSvg, TestContext.Current.CancellationToken);
            var bogusContent = await File.ReadAllTextAsync(bogusSvg, TestContext.Current.CancellationToken);
            Assert.NotEqual(validContent, bogusContent);

            // Assert: the bogus exposed name surfaces a visible diagnostic naming the unresolved
            // identifier, rather than silently rendering everything with no signal.
            var logContent = await File.ReadAllTextAsync(logFile, TestContext.Current.CancellationToken);
            Assert.Contains("thisIdentifierDoesNotExistAnywhere", logContent);
        }
        finally
        {
            Console.SetOut(originalOut);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    ///     Regression guard for the <c>AstBuilder.VisitViewUsage</c> capability addition: rendering
    ///     the real OMG corpus fixture
    ///     <c>test/SysMLModels/OMG/validation/11-ViewAndViewpoint/11b-SafetyAndSecurityFeatureViews.sysml</c>
    ///     with no <c>--view</c> filter must produce exactly 5 output files — the 2 <c>view def</c>
    ///     declarations (<c>SafetyFeatureView</c>, <c>SafetyOrSecurityFeatureView</c>) plus the 3
    ///     named <c>view</c> usages (<c>vehicleSafetyFeatureView</c>,
    ///     <c>vehicleMandatorySafetyFeatureView</c>,
    ///     <c>vehicleMandatorySafetyFeatureViewStandalone</c>) — not just the 2 <c>view def</c>s
    ///     that were the only renderable declarations before <c>VisitViewUsage</c> was added. This
    ///     also confirms rendering this file produces no false unresolved-reference diagnostics for
    ///     its <c>render asTreeDiagram;</c>/<c>render asElementTable;</c> rendering-style members.
    /// </summary>
    [Fact]
    public async Task RenderSubsystem_OmgSafetyFeatureViewsCorpus_RendersAllNamedViewUsages()
    {
        // Arrange: locate the real OMG corpus fixture relative to the test assembly's repo root.
        var fixturePath = Path.Combine(
            FindOmgModelsRoot(),
            "validation", "11-ViewAndViewpoint", "11b-SafetyAndSecurityFeatureViews.sysml");
        Assert.True(File.Exists(fixturePath), $"Expected OMG corpus fixture at {fixturePath}");

        var tempDir = Path.Combine(Path.GetTempPath(), $"omg_11b_views_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var outputDir = Path.Combine(tempDir, "out");
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);

            // Act: render without --view so every declared view is rendered.
            var logFile = Path.Combine(tempDir, "output.log");
            using (var context = Context.Create(
                       ["render", "--log", logFile, "--output", outputDir, fixturePath]))
            {
                await Program.RunAsync(context);

                // Assert: exit code indicates success
                Assert.Equal(0, context.ExitCode);
            }

            // Assert: exactly 5 output files were produced — the 2 view defs plus the 3 named
            // view usages, once VisitViewUsage surfaces them as renderable declarations.
            var outputFiles = Directory.GetFiles(outputDir, "*.svg");
            Assert.Equal(5, outputFiles.Length);

            // Assert: no false "Unresolved reference" diagnostic for the rendering-style names.
            var logContent = await File.ReadAllTextAsync(logFile, TestContext.Current.CancellationToken);
            Assert.DoesNotContain("asTreeDiagram", logContent);
            Assert.DoesNotContain("asElementTable", logContent);
        }
        finally
        {
            Console.SetOut(originalOut);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    ///     Walks upward from the test assembly's base directory to find the repository's
    ///     <c>test/SysMLModels/OMG</c> directory (mirroring the same idiom used by
    ///     <c>OmgModelsTests.FindOmgModelsRoot</c>), so the OMG corpus fixture can be located
    ///     regardless of the test runner's working directory.
    /// </summary>
    private static string FindOmgModelsRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "test", "SysMLModels", "OMG")))
        {
            dir = dir.Parent;
        }

        if (dir == null)
        {
            throw new DirectoryNotFoundException("Cannot locate test/SysMLModels/OMG from test assembly location.");
        }

        return Path.Combine(dir.FullName, "test", "SysMLModels", "OMG");
    }

    /// <summary>
    ///     'render --help' now prints render-specific usage (a regression-proofing test for the
    ///     command-aware help dispatch added alongside the 'help' command).
    /// </summary>
    [Fact]
    public async Task RenderSubsystem_Help_PrintsRenderSpecificUsage()
    {
        // Arrange
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);

            // Act
            using var context = Context.Create(["render", "--help"]);
            await Program.RunAsync(context);

            // Assert: render-specific usage/options, not the generic top-level command list
            var output = outWriter.ToString();
            Assert.Contains("Usage: sysml2tools render [options] <files...>", output);
            Assert.Contains("--output", output);
            Assert.Contains("--auto", output);
            Assert.DoesNotContain("Commands:", output);
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Regression test for the glob-expansion bug fix: a glob pattern such as '*.sysml'
    ///     (previously treated as a literal, never-matching file name) now resolves to every
    ///     matching file in the target directory via the shared GlobFileCollector, and the
    ///     workspace loads and renders successfully from all of them.
    /// </summary>
    [Fact]
    public async Task RenderSubsystem_GlobPattern_ResolvesMultipleFiles()
    {
        // Arrange: a temp directory containing two SysML files, each with a view definition
        var tempDir = Path.Combine(Path.GetTempPath(), $"render_glob_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var outputDir = Path.Combine(tempDir, "out");
        await File.WriteAllTextAsync(
            Path.Combine(tempDir, "a.sysml"),
            """
            package A {
                part def BlockA {}
                view def ViewA {}
            }
            """,
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(tempDir, "b.sysml"),
            """
            package B {
                part def BlockB {}
                view def ViewB {}
            }
            """,
            TestContext.Current.CancellationToken);

        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);

            // Act: render with a glob pattern matching both files
            var pattern = Path.Combine(tempDir, "*.sysml");
            using var context = Context.Create(["render", pattern, "--output", outputDir]);
            await Program.RunAsync(context);

            // Assert: both files were resolved from the single pattern, and both views rendered
            Assert.Contains("Resolved 2 file(s) from 1 pattern(s)", outWriter.ToString());
            Assert.Equal(0, context.ExitCode);
            Assert.Equal(2, Directory.GetFiles(outputDir, "*.svg").Length);
        }
        finally
        {
            Console.SetOut(originalOut);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    ///     RenderCommand reports a distinct error when one or more file patterns are supplied
    ///     but none of them match any file on disk.
    /// </summary>
    [Fact]
    public async Task RenderSubsystem_PatternMatchesNoFiles_ReportsError()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"render_nomatch_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);

            // Act: render with a pattern that matches no files
            var pattern = Path.Combine(tempDir, "*.sysml");
            using var context = Context.Create(["render", pattern]);
            await Program.RunAsync(context);

            // Assert: distinct "no files matched" diagnostic and failing exit code
            Assert.Contains("no files matched", errWriter.ToString());
            Assert.Equal(1, context.ExitCode);
        }
        finally
        {
            Console.SetError(originalError);
            Directory.Delete(tempDir, recursive: true);
        }
    }
}

