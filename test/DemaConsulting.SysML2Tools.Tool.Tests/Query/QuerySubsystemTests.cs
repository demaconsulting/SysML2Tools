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

namespace DemaConsulting.SysML2Tools.Tests.Query;

/// <summary>
///     Subsystem tests for the Query command covering verb dispatch to "not yet implemented"
///     stubs, --element validation, unknown-verb errors, and query help rendering.
/// </summary>
[Collection("Sequential")]
public class QuerySubsystemTests
{
    /// <summary>
    ///     The full ordered list of query verb tokens and whether each requires --element.
    /// </summary>
    public static TheoryData<string, bool> VerbTokens => new()
    {
        { "uses", true },
        { "used-by", true },
        { "impact", true },
        { "describe", true },
        { "hierarchy", true },
        { "requirements", true },
        { "interface", true },
        { "connections", true },
        { "states", true },
        { "list", false },
        { "find", false }
    };

    /// <summary>
    ///     Each of the 11 verbs, when supplied with --element (where required), reports the
    ///     "not yet implemented" diagnostic and produces exit code 1.
    /// </summary>
    [Theory]
    [MemberData(nameof(VerbTokens))]
    public async Task QuerySubsystem_AnyVerb_WithElement_ReportsNotImplementedStub(string verbToken, bool requiresElement)
    {
        // Arrange
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);

            var args = requiresElement
                ? new[] { "query", verbToken, "--element", "Pkg::Foo", "file.sysml" }
                : new[] { "query", verbToken, "file.sysml" };

            // Act
            using var context = Context.Create(args);
            await QueryCommand.RunAsync(context);

            // Assert: stub message written and exit code indicates failure
            Assert.Equal(1, context.ExitCode);
            Assert.Contains(verbToken, errWriter.ToString());
            Assert.Contains("not yet implemented", errWriter.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Missing --element for a verb that requires it throws ArgumentException from
    ///     QueryCommand.RunAsync.
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
    public async Task QuerySubsystem_ElementRequiredVerb_MissingElement_ThrowsArgumentException(string verbToken)
    {
        // Arrange
        using var context = Context.Create(["query", verbToken]);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => QueryCommand.RunAsync(context));
        Assert.Contains("--element", exception.Message);
    }

    /// <summary>
    ///     'list' succeeds without --element (dispatches to its stub instead of throwing).
    /// </summary>
    [Fact]
    public async Task QuerySubsystem_ListVerb_NoElement_DispatchesToStub()
    {
        // Arrange
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);

            // Act
            using var context = Context.Create(["query", "list"]);
            await QueryCommand.RunAsync(context);

            // Assert
            Assert.Equal(1, context.ExitCode);
            Assert.Contains("list", errWriter.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     'find' succeeds without --element (dispatches to its stub instead of throwing).
    /// </summary>
    [Fact]
    public async Task QuerySubsystem_FindVerb_NoElement_DispatchesToStub()
    {
        // Arrange
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);

            // Act
            using var context = Context.Create(["query", "find"]);
            await QueryCommand.RunAsync(context);

            // Assert
            Assert.Equal(1, context.ExitCode);
            Assert.Contains("find", errWriter.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     --format markdown parses and dispatches without error.
    /// </summary>
    [Fact]
    public async Task QuerySubsystem_FormatMarkdown_DispatchesWithoutError()
    {
        // Arrange
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);

            // Act
            using var context = Context.Create(["query", "list", "--format", "markdown"]);
            await QueryCommand.RunAsync(context);

            // Assert: reaches the stub (exit code 1 from the stub, not from a parse error)
            Assert.Equal(1, context.ExitCode);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     --format json parses and dispatches without error.
    /// </summary>
    [Fact]
    public async Task QuerySubsystem_FormatJson_DispatchesWithoutError()
    {
        // Arrange
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);

            // Act
            using var context = Context.Create(["query", "list", "--format", "json"]);
            await QueryCommand.RunAsync(context);

            // Assert
            Assert.Equal(1, context.ExitCode);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     An unrecognized verb token throws ArgumentException naming the bad token, when
    ///     parsed via Context.Create.
    /// </summary>
    [Fact]
    public void QuerySubsystem_UnknownVerb_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["query", "bogus"]));
        Assert.Contains("bogus", exception.Message);
    }

    /// <summary>
    ///     'query --help' (no verb) prints general help without throwing and without requiring
    ///     --element.
    /// </summary>
    [Fact]
    public async Task QuerySubsystem_QueryHelp_NoVerb_PrintsGeneralHelpWithoutThrowing()
    {
        // Arrange
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);

            // Act
            using var context = Context.Create(["query", "--help"]);
            await Program.RunAsync(context);

            // Assert
            Assert.Contains("query", outWriter.ToString());
            Assert.Contains("uses", outWriter.ToString());
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     'query uses --help' (verb + help) prints verb-specific help without throwing and
    ///     without requiring --element.
    /// </summary>
    [Fact]
    public async Task QuerySubsystem_QueryVerbHelp_WithVerb_PrintsVerbHelpWithoutThrowing()
    {
        // Arrange
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);

            // Act
            using var context = Context.Create(["query", "uses", "--help"]);
            await Program.RunAsync(context);

            // Assert
            Assert.Contains("uses", outWriter.ToString());
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
