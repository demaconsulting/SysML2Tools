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
}
