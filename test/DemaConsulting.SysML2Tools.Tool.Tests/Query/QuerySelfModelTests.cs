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
///     Integration suite running <c>query describe</c> over this repository's own SysML2
///     architecture model (<c>docs/sysml2</c>), whose system, subsystem, and unit packages are
///     chained by <c>public import</c> facades: a real model whose element lookup must complete
///     and report the right element or a clear not-found error.
/// </summary>
[Collection("Sequential")]
public class QuerySelfModelTests
{
    /// <summary>
    ///     A unit named through its system facade (<c>SysML2ToolsCore::DiagramRenderer</c>) is
    ///     found and reported under its owning package, with the facade name as the lookup name.
    /// </summary>
    [Fact]
    public async Task QuerySelfModel_Describe_CoreUnitThroughFacade_ReportsOwningElement()
    {
        // Arrange: the repository's own model
        var files = FindSelfModelFiles();

        // Act: describe a Core unit through the Core system facade
        var (output, exitCode) = await RunDescribeAsync("SysML2ToolsCore::DiagramRenderer", files);

        // Assert: the unit is found under its owning package
        Assert.Equal(0, exitCode);
        Assert.Contains("# query describe: SysML2ToolsCore_DiagramRenderer::DiagramRenderer", output);
        Assert.Contains("- Looked up as: SysML2ToolsCore::DiagramRenderer", output);
        Assert.Contains("- Kind: part def", output);
    }

    /// <summary>
    ///     A unit named through the Tool system facade (<c>SysML2ToolsTool::RenderCommand</c>) is
    ///     found and reported under its owning package, with the facade name as the lookup name.
    /// </summary>
    [Fact]
    public async Task QuerySelfModel_Describe_ToolUnitThroughFacade_ReportsOwningElement()
    {
        // Arrange: the repository's own model
        var files = FindSelfModelFiles();

        // Act: describe a Tool unit through the Tool system facade
        var (output, exitCode) = await RunDescribeAsync("SysML2ToolsTool::RenderCommand", files);

        // Assert: the unit is found under its owning package
        Assert.Equal(0, exitCode);
        Assert.Contains("# query describe: SysML2ToolsTool_RenderCommand::RenderCommand", output);
        Assert.Contains("- Looked up as: SysML2ToolsTool::RenderCommand", output);
        Assert.Contains("- Kind: part def", output);
    }

    /// <summary>
    ///     A name no facade of the model exports is reported as not found with a non-zero exit
    ///     code, rather than the lookup running on without end.
    /// </summary>
    [Fact]
    public async Task QuerySelfModel_Describe_NonexistentNameThroughFacade_ReportsNotFound()
    {
        // Arrange: the repository's own model
        var files = FindSelfModelFiles();

        // Act: describe a name the Core system facade does not export
        var (output, exitCode) = await RunDescribeAsync("SysML2ToolsCore::NoSuchThing", files);

        // Assert: a clear not-found error and a failing exit code
        Assert.Equal(1, exitCode);
        Assert.Contains("element 'SysML2ToolsCore::NoSuchThing' not found in the workspace", output);
    }

    /// <summary>
    ///     Runs <c>query describe --element</c> for <paramref name="element"/> over
    ///     <paramref name="files"/> and returns the captured output and exit code.
    /// </summary>
    /// <param name="element">The element name to describe.</param>
    /// <param name="files">The model files to load.</param>
    /// <returns>The captured standard output and error, and the exit code.</returns>
    private static async Task<(string Output, int ExitCode)> RunDescribeAsync(string element, IReadOnlyList<string> files)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        try
        {
            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();
            Console.SetOut(outWriter);
            Console.SetError(errWriter);

            using var context = Context.Create(["query", "describe", "--element", element, .. files]);
            await Program.RunAsync(context);

            return (outWriter.ToString() + errWriter, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Finds the repository's <c>docs/sysml2</c> folder above the test assembly and returns
    ///     every <c>.sysml</c> file below it.
    /// </summary>
    /// <returns>The self-model's files.</returns>
    private static string[] FindSelfModelFiles()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "docs", "sysml2");
            if (Directory.Exists(candidate))
            {
                var files = Directory.GetFiles(candidate, "*.sysml", SearchOption.AllDirectories);
                Assert.NotEmpty(files);
                return files;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new DirectoryNotFoundException("docs/sysml2 was not found above the test assembly.");
    }
}
