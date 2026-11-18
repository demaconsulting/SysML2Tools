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

namespace DemaConsulting.SysML2Tools.Tests.Lint;

/// <summary>
///     Subsystem tests for the Lint command covering pattern resolution, parsing, diagnostic
///     reporting, and exit-code behavior.
/// </summary>
[Collection("Sequential")]
public class LintSubsystemTests
{
    /// <summary>
    ///     LintCommand accepts a glob pattern as a positional argument and resolves it to files.
    /// </summary>
    [Fact]
    public async Task LintSubsystem_Patterns_AcceptsGlobPatterns_ResolvesFiles()
    {
        // Arrange: a temp directory containing one valid SysML file
        var tempDir = Path.Combine(Path.GetTempPath(), $"lint_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var sysmlFile = Path.Combine(tempDir, "model.sysml");
        await File.WriteAllTextAsync(sysmlFile, "package TestPackage {}", TestContext.Current.CancellationToken);

        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);

            // Act: lint with a glob pattern; all files matching *.sysml should be resolved
            var pattern = Path.Combine(tempDir, "*.sysml");
            using var context = Context.Create(["lint", pattern]);
            await Program.RunAsync(context);

            // Assert: one file was resolved from the pattern and processed
            Assert.Contains("Linting 1 file(s)", outWriter.ToString());
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    ///     LintCommand invokes WorkspaceParser.ParseAsync — a valid SysML file produces no errors.
    /// </summary>
    [Fact]
    public async Task LintSubsystem_Parse_ValidFile_ParsesWithoutErrors()
    {
        // Arrange: a temp file containing a minimal valid SysML package
        var tempFile = Path.Combine(Path.GetTempPath(), $"lint_test_{Guid.NewGuid():N}.sysml");
        await File.WriteAllTextAsync(tempFile, "package ValidPackage {}", TestContext.Current.CancellationToken);

        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);

            // Act: lint the valid file by passing its path as a positional argument
            using var context = Context.Create(["lint", tempFile]);
            await Program.RunAsync(context);

            // Assert: file is parsed via WorkspaceParser.ParseAsync and no errors are found
            Assert.Contains("no errors found", outWriter.ToString());
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    /// <summary>
    ///     LintCommand writes each diagnostic in the format: path(line,col): severity: message.
    /// </summary>
    [Fact]
    public async Task LintSubsystem_Report_InvalidFile_WritesDiagnosticInExpectedFormat()
    {
        // Arrange: a temp file containing invalid SysML syntax that will produce error diagnostics
        var tempFile = Path.Combine(Path.GetTempPath(), $"lint_test_{Guid.NewGuid():N}.sysml");
        await File.WriteAllTextAsync(tempFile, "@@@ NOT VALID SYSML @@@", TestContext.Current.CancellationToken);

        var originalOut = Console.Out;
        var originalError = Console.Error;
        try
        {
            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();
            Console.SetOut(outWriter);
            Console.SetError(errWriter);

            // Act: lint the invalid file
            using var context = Context.Create(["lint", tempFile]);
            await Program.RunAsync(context);

            // Assert: at least one diagnostic in path(line,col): severity: message format appears on stderr
            var errorOutput = errWriter.ToString();
            Assert.Matches(@".+\(\d+,\d+\): error: .+", errorOutput);
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
    ///     LintCommand reports an unresolved reference at the position of the reference text, not
    ///     at (0,0).
    /// </summary>
    [Fact]
    public async Task LintSubsystem_Report_UnresolvedReference_ReportsReferencePosition()
    {
        // Arrange: a model whose supertype reference (line 2, column 19) does not resolve
        var tempFile = Path.Combine(Path.GetTempPath(), $"lint_test_{Guid.NewGuid():N}.sysml");
        await File.WriteAllTextAsync(
            tempFile, "package P {\n    part def X :> DoesNotExist;\n}\n", TestContext.Current.CancellationToken);

        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);

            // Act: lint the file
            using var context = Context.Create(["lint", tempFile]);
            await Program.RunAsync(context);

            // Assert: the warning is located at the reference text
            Assert.Contains(
                $"{tempFile}(2,19): warning: Unresolved reference: 'DoesNotExist'",
                outWriter.ToString().Split('\n').Select(line => line.TrimEnd('\r')));
        }
        finally
        {
            Console.SetOut(originalOut);
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     LintCommand reports an undeclared name inside a transition guard, a trigger payload and a
    ///     <c>render</c> target as warnings at the token's position, while a declared name, an
    ///     operator, and a library-function call are not reported — the exit code stays 0 because the
    ///     severity of an unresolved reference is Warning.
    /// </summary>
    [Fact]
    public async Task LintSubsystem_Report_UnresolvedExpressionReferences_ReportsTokenPositionsAsWarnings()
    {
        // Arrange: undeclared guard `atFloor` (line 5, col 31), trigger payload `Arrived` (line 6,
        // col 35), and render target `asTreeDiagram` (line 8, col 25); `ready` is declared.
        var tempFile = Path.Combine(Path.GetTempPath(), $"lint_test_{Guid.NewGuid():N}.sysml");
        await File.WriteAllTextAsync(
            tempFile,
            "package P {\n    state def S {\n        attribute ready : ScalarValues::Boolean;\n        state a;\n" +
            "        transition first a if atFloor and ready and not ready->isEmpty() then a;\n" +
            "        transition first a accept Arrived then a;\n    }\n    view def V { render asTreeDiagram; }\n}\n",
            TestContext.Current.CancellationToken);

        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);

            // Act: lint the file
            using var context = Context.Create(["lint", tempFile]);
            await Program.RunAsync(context);

            // Assert: each undeclared name is a warning at its token; nothing else is reported
            var lines = outWriter.ToString().Split('\n').Select(line => line.TrimEnd('\r')).ToList();
            Assert.Contains($"{tempFile}(5,31): warning: Unresolved reference: 'atFloor'", lines);
            Assert.Contains($"{tempFile}(6,35): warning: Unresolved reference: 'Arrived'", lines);
            Assert.Contains($"{tempFile}(8,25): warning: Unresolved reference: 'asTreeDiagram'", lines);
            Assert.Equal(3, lines.Count(line => line.Contains("Unresolved reference", StringComparison.Ordinal)));
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     By default LintCommand reports an unresolved reference as a warning, prints the warning
    ///     count and exits 0.
    /// </summary>
    [Fact]
    public async Task LintSubsystem_UnresolvedReference_ReportsWarningAndExitsZero()
    {
        // Arrange / Act
        var (output, error, exitCode) = await RunLintAsync(
            "package P {\n    part def X :> DoesNotExist;\n}\n");

        // Assert
        Assert.Contains("(2,19): warning: Unresolved reference: 'DoesNotExist'", output);
        Assert.DoesNotContain("error", error);
        Assert.Equal(0, exitCode);
    }

    /// <summary>
    ///     With <c>--strict-unresolved</c> LintCommand reports an unresolved reference as an error
    ///     at the reference text and exits 1.
    /// </summary>
    [Fact]
    public async Task LintSubsystem_StrictUnresolved_UnresolvedReference_ReportsErrorAndExitsOne()
    {
        // Arrange / Act
        var (output, error, exitCode) = await RunLintAsync(
            "package P {\n    part def X :> DoesNotExist;\n}\n", "--strict-unresolved");

        // Assert
        Assert.Contains("(2,19): error: Unresolved reference: 'DoesNotExist'", error);
        Assert.Contains("lint: 1 error(s) found.", error);
        Assert.DoesNotContain("warning", output);
        Assert.Equal(1, exitCode);
    }

    /// <summary>
    ///     <c>--strict-unresolved</c> promotes only unresolved references: a circular import stays a
    ///     warning, so a model with only that diagnostic still exits 0.
    /// </summary>
    [Fact]
    public async Task LintSubsystem_StrictUnresolved_CircularImport_RemainsWarning()
    {
        // Arrange / Act
        var (output, error, exitCode) = await RunLintAsync(
            "package A { import B::*; }\npackage B { import A::*; }\n", "--strict-unresolved");

        // Assert
        Assert.Contains("warning: Circular import detected", output);
        Assert.DoesNotContain("error", error);
        Assert.Equal(0, exitCode);
    }

    /// <summary>
    ///     A reference that resolves into the bundled standard library produces no diagnostic even
    ///     with <c>--strict-unresolved</c>.
    /// </summary>
    [Fact]
    public async Task LintSubsystem_StrictUnresolved_StandardLibraryReference_NoDiagnostics()
    {
        // Arrange / Act
        var (output, error, exitCode) = await RunLintAsync(
            "package P {\n    attribute def A { attribute r : ScalarValues::Real; }\n}\n", "--strict-unresolved");

        // Assert
        Assert.DoesNotMatch(@"\(\d+,\d+\): (error|warning)", output + error);
        Assert.Contains("no errors found", output);
        Assert.Equal(0, exitCode);
    }

    /// <summary>
    ///     LintCommand warns, at the token and with exit code 0, that a SysML2Tools extension render
    ///     selector is not standard SysML v2, naming the standard view definition to use.
    /// </summary>
    [Fact]
    public async Task LintSubsystem_Report_ToolRenderSelector_WritesPortabilityWarningAtToken()
    {
        // Arrange / Act: `asGridDiagram` is at line 2, column 21
        var (output, _, exitCode) = await RunLintAsync("package P {\n    view v { render asGridDiagram; }\n}\n");

        // Assert
        var lines = output.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        var line = Assert.Single(lines, l => l.Contains(": warning: ", StringComparison.Ordinal));
        Assert.Contains("(2,21): warning: 'asGridDiagram' is a SysML2Tools extension", line);
        Assert.Contains("StandardViewDefinitions::GridView", line);
        Assert.Equal(0, exitCode);
    }

    /// <summary>
    ///     The portability warning for a SysML2Tools extension render selector is a warning in
    ///     strict mode too (exit code 0): <c>--strict-unresolved</c> does not promote it.
    /// </summary>
    [Fact]
    public async Task LintSubsystem_StrictUnresolved_ToolRenderSelector_RemainsWarning()
    {
        // Arrange / Act
        var (output, error, exitCode) = await RunLintAsync(
            "package P {\n    view v { render asGridDiagram; }\n}\n", "--strict-unresolved");

        // Assert
        Assert.Contains("(2,21): warning: 'asGridDiagram' is a SysML2Tools extension", output);
        Assert.DoesNotContain("error", error);
        Assert.Equal(0, exitCode);
    }

    /// <summary>
    ///     LintCommand reports nothing for the standard <c>Views::asTreeDiagram</c> and
    ///     <c>Views::asInterconnectionDiagram</c> render selectors.
    /// </summary>
    [Fact]
    public async Task LintSubsystem_Report_StandardRenderSelectors_NoDiagnostics()
    {
        // Arrange / Act
        var (output, error, exitCode) = await RunLintAsync(
            "package P {\n    view a { render Views::asTreeDiagram; }\n    view b { render Views::asInterconnectionDiagram; }\n}\n");

        // Assert
        Assert.DoesNotMatch(@"\(\d+,\d+\): (error|warning)", output + error);
        Assert.Contains("no errors found", output);
        Assert.Equal(0, exitCode);
    }

    /// <summary>
    ///     Every gallery model lints on its own with no diagnostics and exit code 0.
    /// </summary>
    [Fact]
    public async Task LintSubsystem_Gallery_AllModels_NoDiagnosticsExitZero()
    {
        await AssertGalleryModelsCleanAsync();
    }

    /// <summary>
    ///     Every gallery model also lints with no diagnostics and exit code 0 under
    ///     <c>--strict-unresolved</c>.
    /// </summary>
    [Fact]
    public async Task LintSubsystem_Gallery_StrictUnresolved_AllModels_NoDiagnosticsExitZero()
    {
        await AssertGalleryModelsCleanAsync("--strict-unresolved");
    }

    /// <summary>
    ///     Lints each gallery model (with the given extra arguments) and asserts no diagnostics
    ///     and exit code 0.
    /// </summary>
    private static async Task AssertGalleryModelsCleanAsync(params string[] extraArgs)
    {
        // Arrange: locate docs/gallery/models above the test assembly
        var models = Directory.GetFiles(FindGalleryModels(), "*.sysml");
        Assert.NotEmpty(models);

        var originalOut = Console.Out;
        var originalError = Console.Error;
        try
        {
            foreach (var model in models)
            {
                using var outWriter = new StringWriter();
                using var errWriter = new StringWriter();
                Console.SetOut(outWriter);
                Console.SetError(errWriter);

                // Act
                using var context = Context.Create(["lint", .. extraArgs, model]);
                await Program.RunAsync(context);

                // Assert: no diagnostic line (path(line,col): severity: message) on either stream
                Assert.DoesNotMatch(@"\(\d+,\d+\): (error|warning)", outWriter.ToString() + errWriter.ToString());
                Assert.True(context.ExitCode == 0, $"{model} exited {context.ExitCode}");
            }
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Finds the <c>docs/gallery/models</c> directory above the test assembly.
    /// </summary>
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
    ///     Lints the given model text with the given extra arguments and returns the captured
    ///     standard output, standard error and exit code.
    /// </summary>
    private static async Task<(string Output, string Error, int ExitCode)> RunLintAsync(
        string model, params string[] extraArgs)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"lint_test_{Guid.NewGuid():N}.sysml");
        await File.WriteAllTextAsync(tempFile, model, TestContext.Current.CancellationToken);

        var originalOut = Console.Out;
        var originalError = Console.Error;
        try
        {
            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();
            Console.SetOut(outWriter);
            Console.SetError(errWriter);

            using var context = Context.Create(["lint", .. extraArgs, tempFile]);
            await Program.RunAsync(context);

            return (outWriter.ToString(), errWriter.ToString(), context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     LintCommand returns exit code 1 when error-severity diagnostics are present.
    /// </summary>
    [Fact]
    public async Task LintSubsystem_ExitCode_InvalidFile_ReturnsOne()
    {
        // Arrange: a temp file containing invalid SysML syntax that will produce error diagnostics
        var tempFile = Path.Combine(Path.GetTempPath(), $"lint_test_{Guid.NewGuid():N}.sysml");
        await File.WriteAllTextAsync(tempFile, "@@@ NOT VALID SYSML @@@", TestContext.Current.CancellationToken);

        var originalOut = Console.Out;
        var originalError = Console.Error;
        try
        {
            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();
            Console.SetOut(outWriter);
            Console.SetError(errWriter);

            // Act: lint the invalid file
            using var context = Context.Create(["lint", tempFile]);
            await Program.RunAsync(context);

            // Assert: exit code is 1 when error-severity diagnostics are present
            Assert.Equal(1, context.ExitCode);
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
    ///     LintCommand reports a distinct error when one or more file patterns are supplied
    ///     but none of them match any file on disk, rather than the "no input files" message.
    /// </summary>
    [Fact]
    public async Task LintSubsystem_PatternMatchesNoFiles_ReportsError()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"lint_nomatch_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);

            // Act: lint with a pattern that matches no files
            var pattern = Path.Combine(tempDir, "*.sysml");
            using var context = Context.Create(["lint", pattern]);
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

    /// <summary>
    ///     'lint --help' now prints lint-specific usage (a regression-proofing test for the
    ///     command-aware help dispatch added alongside the 'help' command).
    /// </summary>
    [Fact]
    public async Task LintSubsystem_Help_PrintsLintSpecificUsage()
    {
        // Arrange
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);

            // Act
            using var context = Context.Create(["lint", "--help"]);
            await Program.RunAsync(context);

            // Assert: lint-specific usage line, not the generic top-level command list
            var output = outWriter.ToString();
            Assert.Contains("Usage: sysml2tools lint [--strict-unresolved] <files...>", output);
            Assert.Contains("--strict-unresolved", output);
            Assert.DoesNotContain("Commands:", output);
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     LintCommand resolves a recursive '**' glob pattern into files nested in subdirectories,
    ///     confirming delegation to the shared GlobFileCollector (the prior hand-rolled resolver
    ///     only supported a single directory segment).
    /// </summary>
    [Fact]
    public async Task LintSubsystem_Patterns_RecursiveGlob_ResolvesNestedFiles()
    {
        // Arrange: a temp directory tree with SysML files nested two levels deep
        var tempDir = Path.Combine(Path.GetTempPath(), $"lint_test_{Guid.NewGuid():N}");
        var nestedDir = Path.Combine(tempDir, "nested", "deeper");
        Directory.CreateDirectory(nestedDir);
        await File.WriteAllTextAsync(
            Path.Combine(tempDir, "top.sysml"), "package Top {}", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(nestedDir, "child.sysml"), "package Child {}", TestContext.Current.CancellationToken);

        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);

            // Act: lint with a recursive glob pattern
            var pattern = Path.Combine(tempDir, "**", "*.sysml");
            using var context = Context.Create(["lint", pattern]);
            await Program.RunAsync(context);

            // Assert: both the top-level and the nested file were resolved and linted
            Assert.Contains("Linting 2 file(s)", outWriter.ToString());
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    ///     LintCommand supports '!'-prefixed exclusion patterns, excluding a previously-matched
    ///     file from the resolved file set.
    /// </summary>
    [Fact]
    public async Task LintSubsystem_Patterns_ExclusionPattern_ExcludesMatchedFile()
    {
        // Arrange: a temp directory containing two SysML files
        var tempDir = Path.Combine(Path.GetTempPath(), $"lint_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var keepFile = Path.Combine(tempDir, "keep.sysml");
        var excludeFile = Path.Combine(tempDir, "exclude.sysml");
        await File.WriteAllTextAsync(keepFile, "package Keep {}", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(excludeFile, "package Exclude {}", TestContext.Current.CancellationToken);

        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);

            // Act: lint with an inclusion pattern followed by a '!'-prefixed exclusion pattern
            var includePattern = Path.Combine(tempDir, "*.sysml");
            var excludePattern = $"!{excludeFile}";
            using var context = Context.Create(["lint", includePattern, excludePattern]);
            await Program.RunAsync(context);

            // Assert: only the non-excluded file was resolved and linted
            Assert.Contains("Linting 1 file(s)", outWriter.ToString());
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
            Directory.Delete(tempDir, recursive: true);
        }
    }
}


