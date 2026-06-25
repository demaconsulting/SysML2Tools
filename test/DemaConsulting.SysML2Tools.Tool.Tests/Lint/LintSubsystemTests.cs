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
}
