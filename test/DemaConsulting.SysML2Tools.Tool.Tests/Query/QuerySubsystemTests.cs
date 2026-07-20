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

using System.Text.Json;
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
        { "dependencies", "Model::Mid" },
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
    ///     Each of the 12 verbs, given a valid element (where required) and a loadable workspace,
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
    [InlineData("dependencies")]
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
    ///     '--format json' output is byte-identical whether or not '--depth'/'--heading' are
    ///     supplied, since those options only affect Markdown rendering.
    /// </summary>
    [Fact]
    public async Task QuerySubsystem_FormatJson_UnaffectedByDepthOrHeading()
    {
        var (withoutFlags, exitCodeWithout) = await QueryTestFixtures.RunQueryAsync(
            Fixture, "list", "--format", "json");
        var (withFlags, exitCodeWith) = await QueryTestFixtures.RunQueryAsync(
            Fixture, "list", "--format", "json", "--depth", "5", "--heading", "Custom Heading");

        Assert.Equal(0, exitCodeWithout);
        Assert.Equal(0, exitCodeWith);
        Assert.Equal(withoutFlags, withFlags);
    }

    /// <summary>
    ///     Regression test for the glob-expansion bug fix: a glob pattern such as '*.sysml'
    ///     (previously treated as a literal, never-matching file name) now resolves to every
    ///     matching file in the target directory via the shared GlobFileCollector, and 'query'
    ///     dispatches successfully against the resulting multi-file workspace.
    /// </summary>
    [Fact]
    public async Task QuerySubsystem_GlobPattern_ResolvesMultipleFiles()
    {
        // Arrange: a temp directory containing two SysML files
        var tempDir = Path.Combine(Path.GetTempPath(), $"query_glob_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        await File.WriteAllTextAsync(
            Path.Combine(tempDir, "a.sysml"), "package A { part def BlockA {} }",
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(tempDir, "b.sysml"), "package B { part def BlockB {} }",
            TestContext.Current.CancellationToken);

        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);

            // Act: query 'list' with a glob pattern matching both files
            var pattern = Path.Combine(tempDir, "*.sysml");
            using var context = Context.Create(["query", "list", pattern]);
            await Program.RunAsync(context);

            // Assert: both files resolved from the single pattern, and both parts listed
            Assert.Contains("Resolved 2 file(s) from 1 pattern(s)", outWriter.ToString());
            Assert.Equal(0, context.ExitCode);
            Assert.Contains("BlockA", outWriter.ToString());
            Assert.Contains("BlockB", outWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Directory.Delete(tempDir, recursive: true);
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
    ///     'query --help' (no verb) includes the "typical workflow" note directing users to
    ///     'list'/'find' before element-scoped verbs.
    /// </summary>
    [Fact]
    public async Task QuerySubsystem_QueryHelp_NoVerb_MentionsTypicalWorkflow()
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
            var output = outWriter.ToString();
            Assert.Contains("Typical workflow", output);
            Assert.Contains("--element", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     'query &lt;verb&gt; --help' for every verb includes that verb's example invocation and
    ///     the shared Markdown/JSON output-shape schema hints.
    /// </summary>
    [Theory]
    [InlineData("uses", "query uses --element VehicleDefinitions::Vehicle")]
    [InlineData("used-by", "query used-by --element VehicleDefinitions::Wheel")]
    [InlineData("dependencies", "query dependencies --element VehicleDefinitions::Vehicle")]
    [InlineData("impact", "query impact --element VehicleDefinitions::Axle --walk-depth 2")]
    [InlineData("describe", "query describe --element VehicleUsages::vehicle_C1")]
    [InlineData("hierarchy", "query hierarchy --element VehicleUsages::vehicle_C3 --direction up")]
    [InlineData("requirements", "query requirements --element VehicleUsages::vehicle_C1")]
    [InlineData("interface", "query interface --element VehicleDefinitions::Axle")]
    [InlineData("connections", "query connections --element VehicleUsages::vehicle_C2::frontAxleAssembly::leftFrontMount")]
    [InlineData("states", "query states --element VehicleUsages::vehicle_C1::OperatingStates")]
    [InlineData("list", "query list --kind \"part def\"")]
    [InlineData("find", "query find --name Wheel")]
    public async Task QuerySubsystem_QueryVerbHelp_MentionsExampleInvocationAndSchemaHints(
        string verbToken, string expectedExampleSubstring)
    {
        // Arrange
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);

            // Act
            using var context = Context.Create(["query", verbToken, "--help"]);
            await Program.RunAsync(context);

            // Assert
            var output = outWriter.ToString();
            Assert.Contains(expectedExampleSubstring, output);
            if (verbToken == "dependencies")
            {
                Assert.Contains("Depends on", output);
                Assert.Contains("Direction", output);
            }
            else
            {
                Assert.Contains("Qualified Name", output);
                Assert.Contains("QualifiedName", output);
            }
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     'query &lt;verb&gt; --help' (verb + help) prints verb-specific help without throwing and
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

    /// <summary>
    ///     'dependencies' end-to-end: '--depth'/'--heading' apply to its heading line exactly
    ///     like every other verb, while the bullet-prose body below is unaffected.
    /// </summary>
    [Fact]
    public async Task Dependencies_DepthAndHeadingOptions_ApplyToHeadingLikeOtherVerbs()
    {
        const string sysml = """
            package Model {
                part def Vehicle;
                part def Car specializes Vehicle;
            }
            """;

        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            sysml, "dependencies", "--element", "Model::Car", "--depth", "3", "--heading", "Custom");

        Assert.Equal(0, exitCode);
        Assert.Contains("### Custom", output);
        Assert.Contains("Depends on **Vehicle** (supertype)", output);
    }

    /// <summary>
    ///     End-to-end: '--format markdown' and '--format json' for the same 'uses' query report
    ///     the same qualified names, in the same order.
    /// </summary>
    [Fact]
    public async Task Query_MarkdownAndJsonFormats_AgreeOnEntryContentAndOrder()
    {
        const string sysml = """
            package Model {
                part def Alpha;
                part def Zeta specializes Alpha;
                part def Beta specializes Alpha;
            }
            """;

        var (markdown, markdownExit) = await QueryTestFixtures.RunQueryAsync(
            sysml, "used-by", "--element", "Model::Alpha", "--format", "markdown");
        var (json, jsonExit) = await QueryTestFixtures.RunQueryAsync(
            sysml, "used-by", "--element", "Model::Alpha", "--format", "json");

        Assert.Equal(0, markdownExit);
        Assert.Equal(0, jsonExit);

        // The JSON document is the last chunk written; extract it (from the first '{') and parse
        var deserialized = JsonSerializer.Deserialize(
            json[json.IndexOf('{')..], QueryResultSerializerContext.Default.QueryResult);

        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized!.Entries.Count);
        Assert.Equal("Model::Beta", deserialized.Entries[0].QualifiedName);
        Assert.Equal("Model::Zeta", deserialized.Entries[1].QualifiedName);

        // Markdown reports the same two qualified names, Beta appearing before Zeta
        var betaIndex = markdown.IndexOf("Model::Beta", StringComparison.Ordinal);
        var zetaIndex = markdown.IndexOf("Model::Zeta", StringComparison.Ordinal);
        Assert.True(betaIndex >= 0 && zetaIndex >= 0 && betaIndex < zetaIndex);
    }

    /// <summary>
    ///     'query --output &lt;file&gt;' writes the rendered output to the given file instead of
    ///     stdout, mirroring 'export --output' semantics exactly (single output file, not a
    ///     directory).
    /// </summary>
    [Fact]
    public async Task QuerySubsystem_OutputFlag_WritesToFileInsteadOfStdout()
    {
        const string sysml = """
            package Model {
                part def Vehicle;
                part def Car specializes Vehicle;
            }
            """;

        var tempFile = Path.GetTempFileName() + ".sysml";
        var outputFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, sysml, TestContext.Current.CancellationToken);

        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);

            using var context = Context.Create(
                ["query", "uses", "--element", "Model::Car", "--output", outputFile, tempFile]);
            await Program.RunAsync(context);

            Assert.Equal(0, context.ExitCode);
            Assert.DoesNotContain("# query uses", outWriter.ToString());
            Assert.Contains(outputFile, outWriter.ToString());

            var written = await File.ReadAllTextAsync(outputFile, TestContext.Current.CancellationToken);
            Assert.Contains("# query uses: Model::Car", written);
            Assert.Contains("Vehicle", written);
        }
        finally
        {
            Console.SetOut(originalOut);
            File.Delete(tempFile);
            File.Delete(outputFile);
        }
    }
}
