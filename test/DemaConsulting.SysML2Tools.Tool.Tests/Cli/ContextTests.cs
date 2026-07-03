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
using DemaConsulting.SysML2Tools.Query;

namespace DemaConsulting.SysML2Tools.Tests;

/// <summary>
///     Unit tests for the Context class.
/// </summary>
[Collection("Sequential")]
public class ContextTests
{
    /// <summary>
    ///     Test creating a context with no arguments.
    /// </summary>
    [Fact]
    public void Context_Create_NoArguments_ReturnsDefaultContext()
    {
        // Act: execute the operation being tested
        using var context = Context.Create([]);

        // Assert: verify expected behavior
        Assert.False(context.Version);
        Assert.False(context.Help);
        Assert.False(context.Silent);
        Assert.False(context.Validate);
        Assert.Null(context.ResultsFile);
        Assert.Equal(1, context.HeadingDepth);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with the version flag.
    /// </summary>
    [Fact]
    public void Context_Create_VersionFlag_SetsVersionTrue()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["--version"]);

        // Assert: verify expected behavior
        Assert.True(context.Version);
        Assert.False(context.Help);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with the short version flag.
    /// </summary>
    [Fact]
    public void Context_Create_ShortVersionFlag_SetsVersionTrue()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["-v"]);

        // Assert: verify expected behavior
        Assert.True(context.Version);
        Assert.False(context.Help);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with the help flag.
    /// </summary>
    [Fact]
    public void Context_Create_HelpFlag_SetsHelpTrue()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["--help"]);

        // Assert: verify expected behavior
        Assert.False(context.Version);
        Assert.True(context.Help);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with the short help flag -h.
    /// </summary>
    [Fact]
    public void Context_Create_ShortHelpFlag_H_SetsHelpTrue()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["-h"]);

        // Assert: verify expected behavior
        Assert.False(context.Version);
        Assert.True(context.Help);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with the short help flag -?.
    /// </summary>
    [Fact]
    public void Context_Create_ShortHelpFlag_Question_SetsHelpTrue()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["-?"]);

        // Assert: verify expected behavior
        Assert.False(context.Version);
        Assert.True(context.Help);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with the silent flag.
    /// </summary>
    [Fact]
    public void Context_Create_SilentFlag_SetsSilentTrue()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["--silent"]);

        // Assert: verify expected behavior
        Assert.True(context.Silent);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with the validate flag.
    /// </summary>
    [Fact]
    public void Context_Create_ValidateFlag_SetsValidateTrue()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["--validate"]);

        // Assert: verify expected behavior
        Assert.True(context.Validate);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with the results flag.
    /// </summary>
    [Fact]
    public void Context_Create_ResultsFlag_SetsResultsFile()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["--results", "test.trx"]);

        // Assert: verify expected behavior
        Assert.Equal("test.trx", context.ResultsFile);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with the log flag.
    /// </summary>
    [Fact]
    public void Context_Create_LogFlag_OpensLogFile()
    {
        // Arrange: setup test conditions
        var logFile = Path.GetTempFileName();
        try
        {
            // Act: execute the operation being tested
            using (var context = Context.Create(["--log", logFile]))
            {
                context.WriteLine("Test message");
                Assert.Equal(0, context.ExitCode);
            }

            // Assert: verify expected behavior
            // Verify log file was written
            Assert.True(File.Exists(logFile));
            var logContent = File.ReadAllText(logFile);
            Assert.Contains("Test message", logContent);
        }
        finally
        {
            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }
        }
    }

    /// <summary>
    ///     Test creating a context with an unknown argument throws exception.
    /// </summary>
    [Fact]
    public void Context_Create_UnknownArgument_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--unknown"]));
        Assert.Contains("Unsupported argument", exception.Message);
    }

    /// <summary>
    ///     Test creating a context with --log flag but no value throws exception.
    /// </summary>
    [Fact]
    public void Context_Create_LogFlag_WithoutValue_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--log"]));
        Assert.Contains("--log", exception.Message);
    }

    /// <summary>
    ///     Test creating a context with --results flag but no value throws exception.
    /// </summary>
    [Fact]
    public void Context_Create_ResultsFlag_WithoutValue_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--results"]));
        Assert.Contains("--results", exception.Message);
    }

    /// <summary>
    ///     Test creating a context with the --result alias flag (legacy alias for --results).
    /// </summary>
    [Fact]
    public void Context_Create_ResultAliasFlag_SetsResultsFile()
    {
        // Act: execute the operation using the legacy --result alias
        using var context = Context.Create(["--result", "test.trx"]);

        // Assert: verify --result sets ResultsFile identically to --results
        Assert.Equal("test.trx", context.ResultsFile);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with --result flag but no value throws exception.
    /// </summary>
    [Fact]
    public void Context_Create_ResultAliasFlag_WithoutValue_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--result"]));
        Assert.Contains("--result", exception.Message);
    }

    /// <summary>
    ///     Test creating a context with the depth flag.
    /// </summary>
    [Fact]
    public void Context_Create_DepthFlag_SetsHeadingDepth()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["--depth", "3"]);

        // Assert: verify expected behavior
        Assert.Equal(3, context.HeadingDepth);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with no depth flag returns default heading depth of 1.
    /// </summary>
    [Fact]
    public void Context_Create_NoDepthFlag_ReturnsDefaultHeadingDepth()
    {
        // Act: execute the operation being tested
        using var context = Context.Create([]);

        // Assert: verify default depth is 1
        Assert.Equal(1, context.HeadingDepth);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with --depth flag but no value throws exception.
    /// </summary>
    [Fact]
    public void Context_Create_DepthFlag_WithoutValue_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--depth"]));
        Assert.Contains("--depth", exception.Message);
    }

    /// <summary>
    ///     Test creating a context with --depth flag and non-integer value throws exception.
    /// </summary>
    [Fact]
    public void Context_Create_DepthFlag_NonIntegerValue_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--depth", "abc"]));
        Assert.Contains("--depth", exception.Message);
    }

    /// <summary>
    ///     Test creating a context with --depth flag and zero value throws exception.
    /// </summary>
    [Fact]
    public void Context_Create_DepthFlag_ZeroValue_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--depth", "0"]));
        Assert.Contains("--depth", exception.Message);
    }

    /// <summary>
    ///     Test creating a context with --depth flag and value exceeding maximum (6) does not throw;
    ///     it sets MaxRenderDepth to the raw value and clamps HeadingDepth to 6.
    /// </summary>
    [Fact]
    public void Context_Create_DepthFlag_ExceedsMaxValue_SetsMaxRenderDepth()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["--depth", "7"]);

        // Assert: HeadingDepth is clamped to 6; MaxRenderDepth preserves the raw value
        Assert.Equal(6, context.HeadingDepth);
        Assert.Equal(7, context.MaxRenderDepth);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with --depth 3 sets both HeadingDepth and MaxRenderDepth.
    /// </summary>
    [Fact]
    public void Context_Create_DepthFlag_SetsMaxRenderDepth()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["--depth", "3"]);

        // Assert: both properties reflect the supplied depth
        Assert.Equal(3, context.HeadingDepth);
        Assert.Equal(3, context.MaxRenderDepth);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with the --view flag sets the ViewName property.
    /// </summary>
    [Fact]
    public void Context_Create_ViewFlag_SetsViewName()
    {
        // Act: execute the operation being tested — --view is scoped to the render command
        using var context = Context.Create(["render", "--view", "MyView"]);

        // Assert: verify expected behavior
        Assert.Equal("MyView", context.Render!.ViewName);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test WriteLine writes to console output when not silent.
    /// </summary>
    [Fact]
    public void Context_WriteLine_NotSilent_WritesToConsole()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create([]);

            // Act: execute the operation being tested
            context.WriteLine("Test message");

            // Assert: verify expected behavior
            var output = outWriter.ToString();
            Assert.Contains("Test message", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test WriteLine does not write to console when silent.
    /// </summary>
    [Fact]
    public void Context_WriteLine_Silent_DoesNotWriteToConsole()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["--silent"]);

            // Act: execute the operation being tested
            context.WriteLine("Test message");

            // Assert: verify expected behavior
            var output = outWriter.ToString();
            Assert.DoesNotContain("Test message", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test WriteError does not write to console when silent.
    /// </summary>
    [Fact]
    public void Context_WriteError_Silent_DoesNotWriteToConsole()
    {
        // Arrange: setup test conditions
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);
            using var context = Context.Create(["--silent"]);

            // Act: execute the operation being tested
            context.WriteError("Test error message");

            // Assert - error output should be suppressed in silent mode
            var output = errWriter.ToString();
            Assert.DoesNotContain("Test error message", output);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test WriteError sets exit code to 1.
    /// </summary>
    [Fact]
    public void Context_WriteError_SetsErrorExitCode()
    {
        // Arrange: setup test conditions
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);
            using var context = Context.Create([]);

            // Act: execute the operation being tested
            context.WriteError("Test error message");

            // Assert: verify expected behavior
            Assert.Equal(1, context.ExitCode);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test WriteError writes message to console when not silent.
    /// </summary>
    [Fact]
    public void Context_WriteError_NotSilent_WritesToConsole()
    {
        // Arrange: setup test conditions
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);
            using var context = Context.Create([]);

            // Act: execute the operation being tested
            context.WriteError("Test error message");

            // Assert: verify expected behavior
            var output = errWriter.ToString();
            Assert.Contains("Test error message", output);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test WriteError writes message to log file when logging is enabled.
    /// </summary>
    [Fact]
    public void Context_WriteError_WritesToLogFile()
    {
        // Arrange: setup test conditions
        var logFile = Path.GetTempFileName();
        try
        {
            // Act - use silent to avoid console output; verify the error still goes to the log
            using (var context = Context.Create(["--silent", "--log", logFile]))
            {
                context.WriteError("Test error in log");
                Assert.Equal(1, context.ExitCode);
            }

            // Assert - log file should contain the error message
            Assert.True(File.Exists(logFile));
            var logContent = File.ReadAllText(logFile);
            Assert.Contains("Test error in log", logContent);
        }
        finally
        {
            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }
        }
    }

    /// <summary>
    ///     Test creating a context with --log flag pointing to an invalid path throws InvalidOperationException.
    /// </summary>
    [Fact]
    public void Context_Create_LogFlag_InvalidPath_ThrowsInvalidOperationException()
    {
        // Arrange: a path that cannot be opened as a file (directory or invalid characters)
        // Use a directory path so it cannot be opened as a file
        var invalidLogPath = Path.GetTempPath(); // temp directory itself, not a file

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => Context.Create(["--log", invalidLogPath]));
    }

    /// <summary>
    ///     Test creating a context with the render command sets Command to SysmlCommand.Render.
    /// </summary>
    [Fact]
    public void Context_Create_RenderCommand_SetsCommandRender()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["render"]);

        // Assert: verify expected behavior
        Assert.Equal(SysmlCommand.Render, context.Command);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with render command and --format svg sets Render.Format to "svg".
    /// </summary>
    [Fact]
    public void Context_Create_RenderCommand_WithFormat_SetsSvgFormat()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["render", "--format", "svg"]);

        // Assert: verify expected behavior
        Assert.Equal("svg", context.Render!.Format);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with render command and --format png sets Render.Format to "png".
    /// </summary>
    [Fact]
    public void Context_Create_RenderCommand_WithPngFormat_SetsPngFormat()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["render", "--format", "png"]);

        // Assert: verify expected behavior
        Assert.Equal("png", context.Render!.Format);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with render command and --output sets OutputDirectory.
    /// </summary>
    [Fact]
    public void Context_Create_RenderCommand_WithOutput_SetsOutputDirectory()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["render", "--output", "output/path"]);

        // Assert: verify expected behavior
        Assert.Equal("output/path", context.Render!.OutputDirectory);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with render command and a file pattern sets Files.
    /// </summary>
    [Fact]
    public void Context_Create_RenderCommand_WithFiles_SetsFiles()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["render", "*.sysml"]);

        // Assert: verify expected behavior
        Assert.Single(context.Render!.Files);
        Assert.Equal("*.sysml", context.Render.Files[0]);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with render --format but no value throws ArgumentException.
    /// </summary>
    [Fact]
    public void Context_Create_RenderCommand_FormatWithoutValue_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["render", "--format"]));
        Assert.Contains("--format", exception.Message);
    }

    /// <summary>
    ///     Test creating a context with render --output but no value throws ArgumentException.
    /// </summary>
    [Fact]
    public void Context_Create_RenderCommand_OutputWithoutValue_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["render", "--output"]));
        Assert.Contains("--output", exception.Message);
    }

    /// <summary>
    ///     Test creating a context with the query command and each of the 11 verb tokens sets
    ///     Command to SysmlCommand.Query and Query.Verb to the matching enum value.
    /// </summary>
    [Theory]
    [InlineData("uses")]
    [InlineData("used-by")]
    [InlineData("impact")]
    [InlineData("describe")]
    [InlineData("hierarchy")]
    [InlineData("requirements")]
    [InlineData("interface")]
    [InlineData("connections")]
    [InlineData("states")]
    [InlineData("list")]
    [InlineData("find")]
    public void Context_Create_QueryCommand_WithVerbToken_SetsQueryVerb(string token)
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["query", token, "--element", "Pkg::Foo"]);

        // Assert: verify expected behavior
        Assert.Equal(SysmlCommand.Query, context.Command);
        Assert.NotNull(context.Query);
        Assert.Equal(token, QueryVerbParsing.ToToken(context.Query.Verb));
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with the query command and an unknown verb throws
    ///     ArgumentException naming the bad token.
    /// </summary>
    [Fact]
    public void Context_Create_QueryCommand_UnknownVerb_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["query", "bogus"]));
        Assert.Contains("bogus", exception.Message);
    }

    /// <summary>
    ///     Test creating a context with the query command, no verb, and --help leaves Query null.
    /// </summary>
    [Fact]
    public void Context_Create_QueryCommand_NoVerbWithHelp_LeavesQueryNull()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["query", "--help"]);

        // Assert: verify expected behavior
        Assert.Equal(SysmlCommand.Query, context.Command);
        Assert.True(context.Help);
        Assert.Null(context.Query);
    }

    /// <summary>
    ///     Test creating a context with the query command and --element sets Query.Element.
    /// </summary>
    [Fact]
    public void Context_Create_QueryCommand_WithElementFlag_SetsElement()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["query", "uses", "--element", "Pkg::Foo"]);

        // Assert: verify expected behavior
        Assert.NotNull(context.Query);
        Assert.Equal("Pkg::Foo", context.Query.Element);
    }

    /// <summary>
    ///     Test creating a context with the query command and -e (short flag) sets Query.Element.
    /// </summary>
    [Fact]
    public void Context_Create_QueryCommand_WithShortElementFlag_SetsElement()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["query", "uses", "-e", "Pkg::Foo"]);

        // Assert: verify expected behavior
        Assert.NotNull(context.Query);
        Assert.Equal("Pkg::Foo", context.Query.Element);
    }

    /// <summary>
    ///     Test creating a context with the query command and --direction sets Query.Direction.
    /// </summary>
    [Fact]
    public void Context_Create_QueryCommand_WithDirectionFlag_SetsDirection()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["query", "hierarchy", "--element", "Pkg::Foo", "--direction", "up"]);

        // Assert: verify expected behavior
        Assert.NotNull(context.Query);
        Assert.Equal("up", context.Query.Direction);
    }

    /// <summary>
    ///     Test creating a context with the query command and --kind sets Query.Kind.
    /// </summary>
    [Fact]
    public void Context_Create_QueryCommand_WithKindFlag_SetsKind()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["query", "list", "--kind", "part"]);

        // Assert: verify expected behavior
        Assert.NotNull(context.Query);
        Assert.Equal("part", context.Query.Kind);
    }

    /// <summary>
    ///     Test creating a context with the query command and --name sets Query.NameFilter.
    /// </summary>
    [Fact]
    public void Context_Create_QueryCommand_WithNameFlag_SetsNameFilter()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["query", "find", "--name", "Engine"]);

        // Assert: verify expected behavior
        Assert.NotNull(context.Query);
        Assert.Equal("Engine", context.Query.NameFilter);
    }

    /// <summary>
    ///     Test creating a context with the query command and --include-stdlib sets
    ///     Query.IncludeStdlib to true.
    /// </summary>
    [Fact]
    public void Context_Create_QueryCommand_WithIncludeStdlibFlag_SetsIncludeStdlibTrue()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["query", "list", "--include-stdlib"]);

        // Assert: verify expected behavior
        Assert.NotNull(context.Query);
        Assert.True(context.Query.IncludeStdlib);
    }

    /// <summary>
    ///     Test creating a context with the query command and --format markdown sets
    ///     Query.Format independently of the render command's --format (context.Render is null
    ///     for a query invocation, so there is no shared field to disturb).
    /// </summary>
    [Fact]
    public void Context_Create_QueryCommand_WithFormatMarkdown_SetsQueryFormat()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["query", "list", "--format", "markdown"]);

        // Assert: verify expected behavior
        Assert.NotNull(context.Query);
        Assert.Equal("markdown", context.Query.Format);
        Assert.Null(context.Render);
    }

    /// <summary>
    ///     Test creating a context with the query command and --format json sets Query.Format.
    /// </summary>
    [Fact]
    public void Context_Create_QueryCommand_WithFormatJson_SetsQueryFormat()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["query", "list", "--format", "json"]);

        // Assert: verify expected behavior
        Assert.NotNull(context.Query);
        Assert.Equal("json", context.Query.Format);
    }

    /// <summary>
    ///     Test creating a context with the query command and --depth sets Query.Depth without
    ///     disturbing MaxRenderDepth's meaning for render.
    /// </summary>
    [Fact]
    public void Context_Create_QueryCommand_WithDepthFlag_SetsQueryDepth()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["query", "impact", "--element", "Pkg::Foo", "--depth", "3"]);

        // Assert: verify expected behavior
        Assert.NotNull(context.Query);
        Assert.Equal(3, context.Query.Depth);
        Assert.Equal(3, context.MaxRenderDepth);
    }

    /// <summary>
    ///     Test creating a context with the query command and file globs after the verb sets
    ///     Query.Files, leaving the lint/render option objects null.
    /// </summary>
    [Fact]
    public void Context_Create_QueryCommand_WithFiles_SetsQueryFilesNotTopLevelFiles()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["query", "list", "*.sysml"]);

        // Assert: verify expected behavior
        Assert.NotNull(context.Query);
        Assert.Single(context.Query.Files);
        Assert.Equal("*.sysml", context.Query.Files[0]);
        Assert.Null(context.Lint);
        Assert.Null(context.Render);
    }

    /// <summary>
    ///     Test creating a context with the lint command and an out-of-scope flag (--auto,
    ///     belonging to render) throws ArgumentException naming the flag and the 'lint' command.
    /// </summary>
    [Fact]
    public void Context_Create_LintCommand_OutOfScopeAutoFlag_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => Context.Create(["lint", "--auto", "file.sysml"]));
        Assert.Contains("--auto", exception.Message);
        Assert.Contains("lint", exception.Message);
    }

    /// <summary>
    ///     Test creating a context with the lint command and an out-of-scope flag (--kind,
    ///     belonging to query) throws ArgumentException naming the flag and the 'lint' command.
    /// </summary>
    [Fact]
    public void Context_Create_LintCommand_OutOfScopeKindFlag_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => Context.Create(["lint", "--kind", "part", "file.sysml"]));
        Assert.Contains("--kind", exception.Message);
        Assert.Contains("lint", exception.Message);
    }

    /// <summary>
    ///     Test creating a context with the render command and an out-of-scope flag (--kind,
    ///     belonging to query) throws ArgumentException naming the flag and the 'render' command.
    /// </summary>
    [Fact]
    public void Context_Create_RenderCommand_OutOfScopeKindFlag_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => Context.Create(["render", "--kind", "foo", "file.sysml"]));
        Assert.Contains("--kind", exception.Message);
        Assert.Contains("render", exception.Message);
    }

    /// <summary>
    ///     Test creating a context with the render command and an out-of-scope flag (--element,
    ///     belonging to query) throws ArgumentException naming the flag and the 'render' command.
    /// </summary>
    [Fact]
    public void Context_Create_RenderCommand_OutOfScopeElementFlag_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => Context.Create(["render", "--element", "Pkg::Foo", "file.sysml"]));
        Assert.Contains("--element", exception.Message);
        Assert.Contains("render", exception.Message);
    }

    /// <summary>
    ///     Test creating a context with the query command and an out-of-scope flag (--auto,
    ///     belonging to render) throws ArgumentException naming the flag and the 'query' command.
    /// </summary>
    [Fact]
    public void Context_Create_QueryCommand_OutOfScopeAutoFlag_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => Context.Create(["query", "describe", "--auto", "file.sysml"]));
        Assert.Contains("--auto", exception.Message);
        Assert.Contains("query", exception.Message);
    }

    /// <summary>
    ///     Test creating a context with the query command and no verb token (and no --help)
    ///     throws a clear ArgumentException rather than silently leaving Query null.
    /// </summary>
    [Fact]
    public void Context_Create_QueryCommand_NoVerbNoHelp_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["query"]));
        Assert.Contains("verb", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Test creating a context with the render command and --format but an unsupported value
    ///     does not throw at parse time; validation is deferred to RenderCommand.RunAsync.
    /// </summary>
    [Fact]
    public void Context_Create_RenderCommand_WithUnsupportedFormatValue_DoesNotThrowAtParseTime()
    {
        // Act: execute the operation being tested — value validation happens later, in RunAsync
        using var context = Context.Create(["render", "--format", "xml", "file.sysml"]);

        // Assert: the raw value is captured without eager validation
        Assert.Equal("xml", context.Render!.Format);
    }

    /// <summary>
    ///     Test creating a context with the 'help' command token (no further args) sets Command to
    ///     SysmlCommand.Help and populates HelpCommand with both fields null.
    /// </summary>
    [Fact]
    public void Context_Create_HelpCommand_NoArgs_PopulatesEmptyHelpOptions()
    {
        // Act
        using var context = Context.Create(["help"]);

        // Assert
        Assert.Equal(SysmlCommand.Help, context.Command);
        Assert.NotNull(context.HelpCommand);
        Assert.Null(context.HelpCommand.TargetCommand);
        Assert.Null(context.HelpCommand.TargetVerb);
        Assert.Null(context.Lint);
        Assert.Null(context.Render);
        Assert.Null(context.Query);
    }

    /// <summary>
    ///     Test creating a context with 'help lint' sets HelpCommand.TargetCommand to "lint".
    /// </summary>
    [Fact]
    public void Context_Create_HelpCommand_WithLintTarget_SetsTargetCommand()
    {
        // Act
        using var context = Context.Create(["help", "lint"]);

        // Assert
        Assert.Equal(SysmlCommand.Help, context.Command);
        Assert.NotNull(context.HelpCommand);
        Assert.Equal("lint", context.HelpCommand.TargetCommand);
        Assert.Null(context.HelpCommand.TargetVerb);
    }

    /// <summary>
    ///     Test creating a context with 'help render' sets HelpCommand.TargetCommand to "render".
    /// </summary>
    [Fact]
    public void Context_Create_HelpCommand_WithRenderTarget_SetsTargetCommand()
    {
        // Act
        using var context = Context.Create(["help", "render"]);

        // Assert
        Assert.Equal(SysmlCommand.Help, context.Command);
        Assert.NotNull(context.HelpCommand);
        Assert.Equal("render", context.HelpCommand.TargetCommand);
        Assert.Null(context.HelpCommand.TargetVerb);
    }

    /// <summary>
    ///     Test creating a context with 'help query uses' sets both TargetCommand and TargetVerb.
    /// </summary>
    [Fact]
    public void Context_Create_HelpCommand_WithQueryVerbTarget_SetsTargetCommandAndVerb()
    {
        // Act
        using var context = Context.Create(["help", "query", "uses"]);

        // Assert
        Assert.Equal(SysmlCommand.Help, context.Command);
        Assert.NotNull(context.HelpCommand);
        Assert.Equal("query", context.HelpCommand.TargetCommand);
        Assert.Equal("uses", context.HelpCommand.TargetVerb);
    }

    /// <summary>
    ///     Test creating a context with 'help bogus-command' throws ArgumentException naming the
    ///     unrecognized target.
    /// </summary>
    [Fact]
    public void Context_Create_HelpCommand_UnknownTarget_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["help", "bogus-command"]));
        Assert.Contains("bogus-command", exception.Message);
    }

    /// <summary>
    ///     Test creating a context with 'help query bogus-verb' throws ArgumentException naming
    ///     the unrecognized verb.
    /// </summary>
    [Fact]
    public void Context_Create_HelpCommand_QueryUnknownVerb_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["help", "query", "bogus-verb"]));
        Assert.Contains("bogus-verb", exception.Message);
    }
}


