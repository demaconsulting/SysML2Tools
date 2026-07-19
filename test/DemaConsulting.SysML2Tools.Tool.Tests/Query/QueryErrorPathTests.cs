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

namespace DemaConsulting.SysML2Tools.Tests.Query;

/// <summary>
///     Error-path suite for the real (non-stub) query verbs: element-not-found, 'find' without a
///     filter, unsupported --format values, and best-effort behavior against a file that produces
///     parse diagnostics.
/// </summary>
[Collection("Sequential")]
public class QueryErrorPathTests
{
    /// <summary>
    ///     Querying a nonexistent qualified name reports a clear "not found" error and a non-zero
    ///     exit code, without throwing.
    /// </summary>
    [Fact]
    public async Task Query_ElementNotFound_ReportsErrorAndNonZeroExitCode()
    {
        const string sysml = """
            package Model {
                part def Vehicle;
            }
            """;

        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            sysml, "uses", "--element", "Model::DoesNotExist");

        Assert.Equal(1, exitCode);
        Assert.Contains("not found in the workspace", output);
    }

    /// <summary>
    ///     'find' without --kind or --name throws ArgumentException before any workspace is
    ///     loaded (fails fast, mirroring the existing --element required-value validation style).
    /// </summary>
    [Fact]
    public async Task Find_WithoutFilter_ThrowsArgumentException()
    {
        using var context = Context.Create(["query", "find", "file.sysml"]);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => Program.RunAsync(context));
        Assert.Contains("--kind", exception.Message);
    }

    /// <summary>
    ///     An unsupported --format value throws ArgumentException naming the bad value.
    /// </summary>
    [Fact]
    public async Task Query_UnsupportedFormat_ThrowsArgumentException()
    {
        using var context = Context.Create(["query", "list", "--format", "xml", "file.sysml"]);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => Program.RunAsync(context));
        Assert.Contains("xml", exception.Message);
    }

    /// <summary>
    ///     A non-integer --walk-depth value throws ArgumentException naming the flag, mirroring
    ///     the existing --format validation style.
    /// </summary>
    [Fact]
    public void Query_WalkDepthInvalidValue_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => Context.Create(["query", "impact", "--element", "Pkg::Foo", "--walk-depth", "abc", "file.sysml"]));
        Assert.Contains("--walk-depth", exception.Message);
    }

    /// <summary>
    ///     A file with a syntax error still loads best-effort (matching 'lint'/'render'): parse
    ///     diagnostics are reported, and 'list' still runs against whatever declarations were
    ///     successfully registered.
    /// </summary>
    [Fact]
    public async Task List_FileWithParseErrors_ReportsDiagnosticsAndRunsBestEffort()
    {
        const string invalidSysml = "@@@ NOT VALID SYSML @@@";

        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(invalidSysml, "list");

        // Best-effort: the command completes (possibly with a non-zero exit code from reported
        // diagnostics), producing a query result rather than crashing.
        Assert.True(exitCode is 0 or 1);
        Assert.Contains("query list", output);
    }

    /// <summary>
    ///     Querying with no input files reports a clear error and a non-zero exit code.
    /// </summary>
    [Fact]
    public async Task Query_NoInputFiles_ReportsErrorAndNonZeroExitCode()
    {
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);

            using var context = Context.Create(["query", "list"]);
            await Program.RunAsync(context);

            Assert.Equal(1, context.ExitCode);
            Assert.Contains("no input files", errWriter.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Querying with a file pattern that matches no file on disk reports a distinct "no files
    ///     matched" error and a non-zero exit code.
    /// </summary>
    [Fact]
    public async Task Query_PatternMatchesNoFiles_ReportsErrorAndNonZeroExitCode()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"query_nomatch_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);

            var pattern = Path.Combine(tempDir, "*.sysml");
            using var context = Context.Create(["query", "list", pattern]);
            await Program.RunAsync(context);

            Assert.Equal(1, context.ExitCode);
            Assert.Contains("no files matched", errWriter.ToString());
        }
        finally
        {
            Console.SetError(originalError);
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
