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
///     Subsystem tests for the Query command covering verb dispatch to real
///     <see cref="DemaConsulting.SysML2Tools.Query.QueryEngine"/> logic, --element validation,
///     unknown-verb errors, and query help rendering.
/// </summary>
[Collection("Sequential")]
public class QuerySubsystemTests
{
    /// <summary>
    ///     A minimal fixture providing at least one element usable by every verb: a specialized
    ///     part def (uses/used-by/impact/hierarchy), a requirement satisfaction
    ///     (requirements), a port (interface), a connection (connections), and a state machine
    ///     (states).
    /// </summary>
    private const string Fixture = """
        package Model {
            part def Root;
            part def Mid specializes Root;

            part def Port;
            part def Node {
                port p : Port;
            }
            part def Assembly {
                part a : Node;
                part b : Node;
                connection link connect a.p to b.p;
            }

            requirement def R;
            requirement req : R;
            part def Q {}
            part subj : Q;
            satisfy req by subj;

            state def Light {
                state stop;
                state go;
                transition first stop if t then go;
            }
        }
        """;

    /// <summary>
    ///     The full ordered list of query verb tokens, whether each requires --element, and the
    ///     qualified name to target when it does.
    /// </summary>
    public static TheoryData<string, string?> VerbTokens => new()
    {
        { "uses", "Model::Mid" },
        { "used-by", "Model::Root" },
        { "impact", "Model::Root" },
        { "describe", "Model::Mid" },
        { "hierarchy", "Model::Mid" },
        { "requirements", "Model::req" },
        { "interface", "Model::Node" },
        { "connections", "Model::Assembly::a" },
        { "states", "Model::Light" },
        { "list", null },
        { "find", null }
    };

    /// <summary>
    ///     Each of the 11 verbs, given a valid element (where required) and a loadable workspace,
    ///     dispatches to real <c>QueryEngine</c> logic and produces exit code 0.
    /// </summary>
    [Theory]
    [MemberData(nameof(VerbTokens))]
    public async Task QuerySubsystem_AnyVerb_WithValidInput_DispatchesToRealLogic(string verbToken, string? element)
    {
        string[] args;
        if (element is not null)
        {
            args = [verbToken, "--element", element];
        }
        else if (verbToken == "find")
        {
            args = [verbToken, "--kind", "part"];
        }
        else
        {
            args = [verbToken];
        }

        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(Fixture, args);

        Assert.Equal(0, exitCode);
        Assert.Contains($"query {verbToken}", output);
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
    ///     'list' succeeds without --element, but reports a "no input files" error when no files
    ///     are supplied (dispatch validation happens before file loading).
    /// </summary>
    [Fact]
    public async Task QuerySubsystem_ListVerb_NoElementNoFiles_ReportsNoInputFilesError()
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
            Assert.Contains("no input files", errWriter.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     'find' succeeds without --element (dispatch validation happens before file loading, and
    ///     'find' does not require --element).
    /// </summary>
    [Fact]
    public async Task QuerySubsystem_FindVerb_NoElementNoFiles_ReportsNoInputFilesError()
    {
        // Arrange
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);

            // Act
            using var context = Context.Create(["query", "find", "--kind", "part"]);
            await QueryCommand.RunAsync(context);

            // Assert
            Assert.Equal(1, context.ExitCode);
            Assert.Contains("find", errWriter.ToString());
            Assert.Contains("no input files", errWriter.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     --format markdown parses and dispatches without a parsing error.
    /// </summary>
    [Fact]
    public async Task QuerySubsystem_FormatMarkdown_DispatchesWithoutError()
    {
        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(Fixture, "list", "--format", "markdown");

        Assert.Equal(0, exitCode);
        Assert.Contains("# query list", output);
    }

    /// <summary>
    ///     --format json parses and dispatches without a parsing error.
    /// </summary>
    [Fact]
    public async Task QuerySubsystem_FormatJson_DispatchesWithoutError()
    {
        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(Fixture, "list", "--format", "json");

        Assert.Equal(0, exitCode);
        Assert.Contains("\"Verb\": \"list\"", output);
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
