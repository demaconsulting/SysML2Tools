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

namespace DemaConsulting.SysML2Tools.Tests.Help;

/// <summary>
///     End-to-end subsystem tests for the <c>help</c> command, exercised via
///     <see cref="Context.Create"/> + <see cref="Program.RunAsync"/>. Verifies bare <c>help</c>,
///     per-command help, per-verb help for all 11 <c>query</c> verbs, unknown-command/verb
///     handling, parity between <c>help &lt;command&gt;</c> and <c>&lt;command&gt; --help</c>, and
///     the <c>--silent</c> interaction.
/// </summary>
[Collection("Sequential")]
public class HelpSubsystemTests
{
    /// <summary>
    ///     The full ordered list of query verb tokens, used to verify 'help query &lt;verb&gt;'
    ///     parity with 'query &lt;verb&gt; --help' for every verb.
    /// </summary>
    public static TheoryData<string> QueryVerbTokens =>
    [
        "uses",
        "used-by",
        "impact",
        "describe",
        "hierarchy",
        "requirements",
        "interface",
        "connections",
        "states",
        "list",
        "find"
    ];

    private static readonly string[] AllQueryVerbTokens =
    [
        "uses",
        "used-by",
        "impact",
        "describe",
        "hierarchy",
        "requirements",
        "interface",
        "connections",
        "states",
        "list",
        "find"
    ];

    /// <summary>
    ///     Runs the given arguments through Context.Create + Program.RunAsync and returns the
    ///     captured stdout and exit code.
    /// </summary>
    private static async Task<(string Output, int ExitCode)> RunAsync(params string[] args)
    {
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);

            using var context = Context.Create(args);
            await Program.RunAsync(context);

            return (outWriter.ToString(), context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     'help' (no args) produces the same top-level output as bare '--help'.
    /// </summary>
    [Fact]
    public async Task HelpSubsystem_BareHelp_MatchesTopLevelHelpFlag()
    {
        var (helpOutput, helpExitCode) = await RunAsync("help");
        var (flagOutput, flagExitCode) = await RunAsync("--help");

        Assert.Equal(flagOutput, helpOutput);
        Assert.Equal(flagExitCode, helpExitCode);
    }

    /// <summary>
    ///     'help' (no args) does not fall through to "No command specified" — it prints the
    ///     top-level help and returns before RunToolLogicAsync is ever reached.
    /// </summary>
    [Fact]
    public async Task HelpSubsystem_BareHelp_DoesNotFallThroughToNoCommandSpecified()
    {
        var (output, exitCode) = await RunAsync("help");

        Assert.Contains("Usage:", output);
        Assert.DoesNotContain("No command specified", output);
        Assert.Equal(0, exitCode);
    }

    /// <summary>
    ///     'help lint' produces lint-distinguishing content and matches 'lint --help'.
    /// </summary>
    [Fact]
    public async Task HelpSubsystem_HelpLint_MatchesLintHelpFlag()
    {
        var (helpOutput, _) = await RunAsync("help", "lint");
        var (flagOutput, _) = await RunAsync("lint", "--help");

        Assert.Contains("sysml2tools lint <files...>", helpOutput);
        Assert.Equal(flagOutput, helpOutput);
    }

    /// <summary>
    ///     'help render' produces render-distinguishing content and matches 'render --help'.
    /// </summary>
    [Fact]
    public async Task HelpSubsystem_HelpRender_MatchesRenderHelpFlag()
    {
        var (helpOutput, _) = await RunAsync("help", "render");
        var (flagOutput, _) = await RunAsync("render", "--help");

        Assert.Contains("sysml2tools render [options] <files...>", helpOutput);
        Assert.Contains("--auto", helpOutput);
        Assert.Equal(flagOutput, helpOutput);
    }

    /// <summary>
    ///     'help query' (no verb) produces query-distinguishing content — an overview of all 11
    ///     verbs — and matches 'query --help'.
    /// </summary>
    [Fact]
    public async Task HelpSubsystem_HelpQuery_MatchesQueryHelpFlag()
    {
        var (helpOutput, _) = await RunAsync("help", "query");
        var (flagOutput, _) = await RunAsync("query", "--help");

        foreach (var verb in AllQueryVerbTokens)
        {
            Assert.Contains(verb, helpOutput);
        }

        Assert.Equal(flagOutput, helpOutput);
    }

    /// <summary>
    ///     'help query &lt;verb&gt;' matches 'query &lt;verb&gt; --help' for every one of the 11
    ///     verbs, and mentions that verb's real flags where applicable.
    /// </summary>
    [Theory]
    [MemberData(nameof(QueryVerbTokens))]
    public async Task HelpSubsystem_HelpQueryVerb_MatchesQueryVerbHelpFlag(string verb)
    {
        var (helpOutput, _) = await RunAsync("help", "query", verb);
        var (flagOutput, _) = await RunAsync("query", verb, "--help");

        Assert.Equal(flagOutput, helpOutput);
        Assert.Contains($"sysml2tools query {verb}", helpOutput);

        // Verb-specific real flags, verified against QueryCommand.PrintVerbHelp.
        switch (verb)
        {
            case "impact":
                Assert.Contains("--depth", helpOutput);
                break;

            case "hierarchy":
                Assert.Contains("--direction", helpOutput);
                break;

            case "list":
            case "find":
                Assert.Contains("--kind", helpOutput);
                Assert.Contains("--name", helpOutput);
                break;

            default:
                Assert.Contains("--element", helpOutput);
                break;
        }
    }

    /// <summary>
    ///     'help bogus-command' throws a graceful ArgumentException (not a crash), consistent with
    ///     the existing ArgumentException/InvalidOperationException handling pattern in
    ///     Program.Main.
    /// </summary>
    [Fact]
    public void HelpSubsystem_HelpUnknownCommand_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["help", "bogus-command"]));
        Assert.Contains("bogus-command", exception.Message);
    }

    /// <summary>
    ///     'help query bogus-verb' throws a graceful ArgumentException (not a crash).
    /// </summary>
    [Fact]
    public void HelpSubsystem_HelpQueryUnknownVerb_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["help", "query", "bogus-verb"]));
        Assert.Contains("bogus-verb", exception.Message);
    }

    /// <summary>
    ///     'sysml2tools help bogus-command' surfaces as a clean exit-code-1 error (via Main's
    ///     ArgumentException handling), not an unhandled crash.
    /// </summary>
    [Fact]
    public async Task HelpSubsystem_HelpUnknownCommand_ViaMain_ReturnsNonZeroExitCodeWithoutCrashing()
    {
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);

            var result = await Program.Main(["help", "bogus-command"]);

            Assert.Equal(1, result);
            Assert.Contains("bogus-command", errWriter.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     '--silent help query' — silent suppresses the help output, consistent with
    ///     <see cref="Context.WriteLine"/>'s existing Silent semantics, under which stdout output
    ///     is unconditionally suppressed regardless of the reason for the write (this already
    ///     applies to e.g. '--silent --version', which also suppresses its own explicitly
    ///     requested output). The 'help' command intentionally does not special-case Silent to
    ///     bypass this, so behavior remains consistent across every command rather than carving
    ///     out an exception only for 'help'.
    /// </summary>
    [Fact]
    public async Task HelpSubsystem_SilentHelpQuery_SuppressesOutputConsistentlyWithOtherCommands()
    {
        var (output, exitCode) = await RunAsync("--silent", "help", "query");

        Assert.Equal(string.Empty, output);
        Assert.Equal(0, exitCode);
    }

    /// <summary>
    ///     'help --silent lint' (silent supplied after the command token) has the same suppressing
    ///     effect, since --silent is a global flag recognized regardless of position.
    /// </summary>
    [Fact]
    public async Task HelpSubsystem_HelpLintSilent_SuppressesOutput()
    {
        var (output, exitCode) = await RunAsync("help", "lint", "--silent");

        Assert.Equal(string.Empty, output);
        Assert.Equal(0, exitCode);
    }
}
