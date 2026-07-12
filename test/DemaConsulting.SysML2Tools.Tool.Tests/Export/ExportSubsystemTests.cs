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
using DemaConsulting.SysML2Tools.Export;

namespace DemaConsulting.SysML2Tools.Tests.Export;

/// <summary>
///     Subsystem tests for the Export command: argument parsing (flags/rejection), happy-path
///     dispatch for both formats, stdlib include/exclude, <c>--output</c> file vs. stdout, and the
///     missing-files / invalid-format error paths.
/// </summary>
[Collection("Sequential")]
public class ExportSubsystemTests
{
    private const string Fixture = """
        package Model {
            part def Root;
            part def Mid specializes Root;
            part def Wheel;
            part def Car {
                part w : Wheel;
            }
        }
        """;

    private const string TargetFixture = """
        package Model {
            metadata def Critical;

            part def Vehicle {
                part engine : Engine;
            }

            part def Engine {
                @Critical;
            }

            part def Accessory;
        }
        """;

    // ---- ExportArgumentParser ----

    /// <summary>
    ///     Parsing with no flags produces defaults: null Format/Output, IncludeStdlib false, and
    ///     positional files captured.
    /// </summary>
    [Fact]
    public void ExportArgumentParser_NoFlags_ProducesDefaults()
    {
        var options = ExportArgumentParser.Parse(["a.sysml", "b.sysml"]);

        Assert.Null(options.Format);
        Assert.Null(options.Output);
        Assert.False(options.IncludeStdlib);
        Assert.Null(options.Target);
        Assert.Null(options.FilterExpression);
        Assert.Equal(["a.sysml", "b.sysml"], options.Files);
    }

    /// <summary>
    ///     --format captures its raw value without validating it (validation happens later in
    ///     ExportCommand.RunAsync).
    /// </summary>
    [Fact]
    public void ExportArgumentParser_FormatFlag_CapturesRawValue()
    {
        var options = ExportArgumentParser.Parse(["--format", "jsonl", "a.sysml"]);

        Assert.Equal("jsonl", options.Format);
        Assert.Equal(["a.sysml"], options.Files);
    }

    /// <summary>
    ///     --output captures the file path value.
    /// </summary>
    [Fact]
    public void ExportArgumentParser_OutputFlag_CapturesValue()
    {
        var options = ExportArgumentParser.Parse(["--output", "out.json", "a.sysml"]);

        Assert.Equal("out.json", options.Output);
    }

    /// <summary>
    ///     --include-stdlib sets the boolean flag.
    /// </summary>
    [Fact]
    public void ExportArgumentParser_IncludeStdlibFlag_SetsTrue()
    {
        var options = ExportArgumentParser.Parse(["--include-stdlib", "a.sysml"]);

        Assert.True(options.IncludeStdlib);
    }

    /// <summary>
    ///     An unrecognized '-'-prefixed token is rejected with an ArgumentException naming it.
    /// </summary>
    [Fact]
    public void ExportArgumentParser_UnrecognizedFlag_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => ExportArgumentParser.Parse(["--bogus"]));
        Assert.Contains("--bogus", exception.Message);
        Assert.Contains("export", exception.Message);
    }

    /// <summary>
    ///     --format with no value throws an ArgumentException.
    /// </summary>
    [Fact]
    public void ExportArgumentParser_FormatFlagMissingValue_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ExportArgumentParser.Parse(["--format"]));
    }

    /// <summary>
    ///     --output with no value throws an ArgumentException.
    /// </summary>
    [Fact]
    public void ExportArgumentParser_OutputFlagMissingValue_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ExportArgumentParser.Parse(["--output"]));
    }

    /// <summary>
    ///     --target captures the qualified-name value.
    /// </summary>
    [Fact]
    public void ExportArgumentParser_TargetFlag_CapturesValue()
    {
        var options = ExportArgumentParser.Parse(["--target", "Model::Vehicle", "a.sysml"]);

        Assert.Equal("Model::Vehicle", options.Target);
        Assert.Equal(["a.sysml"], options.Files);
    }

    /// <summary>
    ///     --filter captures the raw expression text without validating it (validation happens
    ///     later in ExportCommand.RunAsync).
    /// </summary>
    [Fact]
    public void ExportArgumentParser_FilterFlag_CapturesValue()
    {
        var options = ExportArgumentParser.Parse(["--filter", "@Critical", "a.sysml"]);

        Assert.Equal("@Critical", options.FilterExpression);
    }

    /// <summary>
    ///     --target with no value throws an ArgumentException.
    /// </summary>
    [Fact]
    public void ExportArgumentParser_TargetFlagMissingValue_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ExportArgumentParser.Parse(["--target"]));
    }

    /// <summary>
    ///     --filter with no value throws an ArgumentException.
    /// </summary>
    [Fact]
    public void ExportArgumentParser_FilterFlagMissingValue_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ExportArgumentParser.Parse(["--filter"]));
    }

    // ---- ExportCommand happy path ----

    /// <summary>
    ///     'export --format json' against a valid fixture dispatches, prints JSON to stdout, and
    ///     exits 0.
    /// </summary>
    [Fact]
    public async Task ExportSubsystem_FormatJson_DispatchesAndPrintsJson()
    {
        var (output, exitCode) = await ExportTestFixtures.RunExportAsync(Fixture, "--format", "json");

        Assert.Equal(0, exitCode);
        Assert.Contains("\"Declarations\"", output);
        Assert.Contains("\"Edges\"", output);
        Assert.Contains("\"Diagnostics\"", output);
        Assert.Contains("Model::Wheel", output);
    }

    /// <summary>
    ///     'export' with no --format defaults to json.
    /// </summary>
    [Fact]
    public async Task ExportSubsystem_NoFormat_DefaultsToJson()
    {
        var (output, exitCode) = await ExportTestFixtures.RunExportAsync(Fixture);

        Assert.Equal(0, exitCode);
        Assert.Contains("\"Declarations\"", output);
    }

    /// <summary>
    ///     'export --format jsonl' against a valid fixture dispatches and prints JSONL lines (one
    ///     compact JSON object per declaration/edge/diagnostic) to stdout.
    /// </summary>
    [Fact]
    public async Task ExportSubsystem_FormatJsonl_DispatchesAndPrintsJsonLines()
    {
        var (output, exitCode) = await ExportTestFixtures.RunExportAsync(Fixture, "--format", "jsonl");

        Assert.Equal(0, exitCode);
        Assert.Contains("\"kind\":\"declaration\"", output.Replace(" ", "", StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Model::Wheel", output);
    }

    /// <summary>
    ///     Without --include-stdlib, stdlib declarations are excluded from the exported output.
    /// </summary>
    [Fact]
    public async Task ExportSubsystem_NoIncludeStdlib_ExcludesStdlib()
    {
        var (output, _) = await ExportTestFixtures.RunExportAsync(Fixture, "--format", "json");

        // Without stdlib inclusion, no output is produced from files under 'test/SysMLModels', and
        // the default OMG stdlib root packages are excluded.
        Assert.DoesNotContain("ScalarValues", output);
    }

    /// <summary>
    ///     With --include-stdlib, the exported output is substantially larger and includes stdlib
    ///     declarations, confirming the flag has an observable effect.
    /// </summary>
    [Fact]
    public async Task ExportSubsystem_IncludeStdlib_IncludesStdlibAndIncreasesSize()
    {
        var (withoutStdlib, exitCode1) = await ExportTestFixtures.RunExportAsync(Fixture, "--format", "json");
        var (withStdlib, exitCode2) = await ExportTestFixtures.RunExportAsync(Fixture, "--format", "json", "--include-stdlib");

        Assert.Equal(0, exitCode1);
        Assert.Equal(0, exitCode2);
        Assert.True(withStdlib.Length > withoutStdlib.Length);
        Assert.Contains("ScalarValues", withStdlib);
    }

    /// <summary>
    ///     --output writes the export document to the given file path instead of stdout, and
    ///     stdout instead reports a short summary line.
    /// </summary>
    [Fact]
    public async Task ExportSubsystem_OutputFlag_WritesToFileInsteadOfStdout()
    {
        var outputFile = Path.Combine(Path.GetTempPath(), $"export_test_{Guid.NewGuid():N}.json");
        try
        {
            var (output, exitCode) = await ExportTestFixtures.RunExportAsync(
                Fixture, "--format", "json", "--output", outputFile);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputFile));

            var fileContent = await File.ReadAllTextAsync(outputFile, TestContext.Current.CancellationToken);
            Assert.Contains("\"Declarations\"", fileContent);
            Assert.Contains("Model::Wheel", fileContent);

            // stdout should not itself contain the full JSON document (it was written to the file)
            Assert.DoesNotContain("\"Declarations\"", output);
            Assert.Contains("wrote", output);
        }
        finally
        {
            if (File.Exists(outputFile))
            {
                File.Delete(outputFile);
            }
        }
    }

    // ---- --target / --filter ----

    /// <summary>
    ///     --target restricts the exported declarations to the target's containment subtree,
    ///     excluding unrelated top-level declarations.
    /// </summary>
    [Fact]
    public async Task ExportSubsystem_TargetFlag_RestrictsToSubtree()
    {
        var (output, exitCode) = await ExportTestFixtures.RunExportAsync(
            TargetFixture, "--format", "json", "--target", "Model::Vehicle");

        Assert.Equal(0, exitCode);
        Assert.Contains("Model::Vehicle", output);
        Assert.Contains("Model::Vehicle::engine", output);
        Assert.DoesNotContain("Model::Accessory", output);
    }

    /// <summary>
    ///     --target only includes edges whose source and target both lie within the target's
    ///     subtree; using a usage (feature) target exercises the usage-to-type expansion, so the
    ///     usage's own <c>Typing</c> edge to its resolved type survives (both endpoints — the
    ///     usage and its type — are in scope), while an edge to an unrelated declaration would not.
    /// </summary>
    [Fact]
    public async Task ExportSubsystem_TargetFlag_IncludesEdgesWithBothEndpointsInSubtree()
    {
        var (output, exitCode) = await ExportTestFixtures.RunExportAsync(
            TargetFixture, "--format", "jsonl", "--target", "Model::Vehicle::engine");

        Assert.Equal(0, exitCode);

        // The usage's Typing edge (Model::Vehicle::engine -> Model::Engine) survives because
        // usage-to-type expansion added Model::Engine to the target's subtree subjects alongside
        // the usage itself.
        Assert.Contains("Model::Vehicle::engine", output);
        Assert.Contains("Model::Engine", output);
        Assert.DoesNotContain("Model::Accessory", output);
        Assert.Contains("\"kind\":\"edge\"", output.Replace(" ", "", StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     A --target value that does not resolve to any declaration in the workspace reports a
    ///     clean "not found" error and produces no export output.
    /// </summary>
    [Fact]
    public async Task ExportSubsystem_TargetFlag_UnresolvedName_ReportsNotFoundError()
    {
        var (output, exitCode) = await ExportTestFixtures.RunExportAsync(
            TargetFixture, "--format", "json", "--target", "Model::NoSuchElement");

        Assert.Equal(1, exitCode);
        Assert.Contains("--target 'Model::NoSuchElement' was not found in the workspace", output);
    }

    /// <summary>
    ///     A --target value naming a standard-library declaration, without --include-stdlib,
    ///     reports the same "not found" error as a genuinely absent name (an invisible stdlib
    ///     target is indistinguishable from a nonexistent one).
    /// </summary>
    [Fact]
    public async Task ExportSubsystem_TargetFlag_StdlibTargetWithoutIncludeStdlib_ReportsNotFoundError()
    {
        var (output, exitCode) = await ExportTestFixtures.RunExportAsync(
            TargetFixture, "--format", "json", "--target", "ScalarValues::Boolean");

        Assert.Equal(1, exitCode);
        Assert.Contains("--target 'ScalarValues::Boolean' was not found in the workspace", output);
    }

    /// <summary>
    ///     A --target value naming a standard-library declaration succeeds when --include-stdlib
    ///     is also supplied, scoping the export to that stdlib element's subtree.
    /// </summary>
    [Fact]
    public async Task ExportSubsystem_TargetFlag_StdlibTargetWithIncludeStdlib_Succeeds()
    {
        var (output, exitCode) = await ExportTestFixtures.RunExportAsync(
            TargetFixture, "--format", "json", "--target", "ScalarValues::Boolean", "--include-stdlib");

        Assert.Equal(0, exitCode);
        Assert.Contains("ScalarValues::Boolean", output);
        Assert.DoesNotContain("Model::Vehicle", output);
    }

    /// <summary>
    ///     --filter (with no --target) narrows the exported declarations to those matching the
    ///     classification-test expression.
    /// </summary>
    [Fact]
    public async Task ExportSubsystem_FilterFlag_NarrowsDeclarations()
    {
        var (output, exitCode) = await ExportTestFixtures.RunExportAsync(
            TargetFixture, "--format", "json", "--filter", "@Critical");

        Assert.Equal(0, exitCode);
        Assert.Contains("Model::Engine", output);
        Assert.DoesNotContain("Model::Vehicle\"", output);
        Assert.DoesNotContain("Model::Accessory", output);
    }

    /// <summary>
    ///     --filter without --target narrows the whole (stdlib-filtered) workspace, not just a
    ///     subset scoped by some other means.
    /// </summary>
    [Fact]
    public async Task ExportSubsystem_FilterFlag_WithoutTarget_NarrowsWholeWorkspace()
    {
        var (unfiltered, exitCode1) = await ExportTestFixtures.RunExportAsync(TargetFixture, "--format", "json");
        var (filtered, exitCode2) = await ExportTestFixtures.RunExportAsync(
            TargetFixture, "--format", "json", "--filter", "@Critical");

        Assert.Equal(0, exitCode1);
        Assert.Equal(0, exitCode2);
        Assert.True(filtered.Length < unfiltered.Length);
        Assert.Contains("Model::Engine", filtered);
    }

    /// <summary>
    ///     --target and --filter compose in order: --target scopes to the subtree first (here,
    ///     via a usage target whose usage-to-type expansion adds its resolved type to scope), then
    ///     --filter narrows that already-scoped set further, so only elements satisfying both
    ///     survive.
    /// </summary>
    [Fact]
    public async Task ExportSubsystem_TargetAndFilter_ComposeTargetFirstThenFilter()
    {
        var (output, exitCode) = await ExportTestFixtures.RunExportAsync(
            TargetFixture, "--format", "json", "--target", "Model::Vehicle::engine", "--filter", "@Critical");

        Assert.Equal(0, exitCode);

        // --target's usage-to-type expansion brings Model::Engine into scope alongside the usage
        // itself; --filter then narrows to only the elements carrying @Critical: Model::Engine
        // matches (it carries the annotation) but the usage itself does not, proving --filter
        // narrows the already-scoped set rather than either step alone deciding the result.
        Assert.Contains("Model::Engine", output);
        Assert.DoesNotContain("Vehicle::engine", output);
        Assert.DoesNotContain("Model::Accessory", output);
    }

    /// <summary>
    ///     A --filter expression using an unsupported Phase 1 construct does not abort the export:
    ///     it falls back to the unfiltered result, appends a synthetic warning diagnostic to the
    ///     output, and prints a matching console warning.
    /// </summary>
    [Fact]
    public async Task ExportSubsystem_FilterFlag_UnsupportedConstruct_AddsDiagnosticAndWarns()
    {
        var (output, exitCode) = await ExportTestFixtures.RunExportAsync(
            TargetFixture, "--format", "json", "--filter", "1 + 1");

        Assert.Equal(0, exitCode);

        // Console warning channel (plain text, not JSON-escaped).
        Assert.Contains("export: warning: --filter expression '1 + 1' failed to parse/evaluate", output);
        Assert.Contains("Unsupported filter construct", output);

        // Synthetic diagnostic channel: FilePath uses the "<--filter>" virtual-path convention
        // (JSON-encoded as \u003C--filter\u003E by System.Text.Json's default HTML-safe encoder)
        // with Severity 1 (Warning).
        Assert.Contains("\\u003C--filter\\u003E", output);
        Assert.Contains("\"Severity\": 1", output);

        // Falls back to the unfiltered result: every declaration is still present.
        Assert.Contains("Model::Vehicle", output);
        Assert.Contains("Model::Accessory", output);
    }

    // ---- error paths ----

    /// <summary>
    ///     No input files reports a "no input files" error and exit code 1.
    /// </summary>
    [Fact]
    public async Task ExportSubsystem_NoFiles_ReportsNoInputFilesError()
    {
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);

            using var context = Context.Create(["export"]);
            await ExportCommand.RunAsync(context);

            Assert.Equal(1, context.ExitCode);
            Assert.Contains("no input files", errWriter.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     No files matching the given pattern reports a "no files matched" error and exit code 1.
    /// </summary>
    [Fact]
    public async Task ExportSubsystem_NoMatchingFiles_ReportsNoFilesMatchedError()
    {
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);

            using var context = Context.Create(["export", $"nonexistent_{Guid.NewGuid():N}.sysml"]);
            await ExportCommand.RunAsync(context);

            Assert.Equal(1, context.ExitCode);
            Assert.Contains("no files matched", errWriter.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     An unsupported --format value throws ArgumentException up front, before any file
    ///     loading is attempted.
    /// </summary>
    [Fact]
    public async Task ExportSubsystem_InvalidFormat_ThrowsArgumentException()
    {
        using var context = Context.Create(["export", "--format", "yaml", "a.sysml"]);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => ExportCommand.RunAsync(context));
        Assert.Contains("yaml", exception.Message);
        Assert.Contains("--format", exception.Message);
    }

    /// <summary>
    ///     'export --help' prints help without throwing.
    /// </summary>
    [Fact]
    public async Task ExportSubsystem_ExportHelp_PrintsHelpWithoutThrowing()
    {
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);

            using var context = Context.Create(["export", "--help"]);
            await Program.RunAsync(context);

            Assert.Contains("export", outWriter.ToString());
            Assert.Contains("--format", outWriter.ToString());
            Assert.Contains("--include-stdlib", outWriter.ToString());
            Assert.Contains("--target", outWriter.ToString());
            Assert.Contains("--filter", outWriter.ToString());
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
