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

using DemaConsulting.HelpCards.Catalog;
using DemaConsulting.HelpCards.Integration;
using DemaConsulting.HelpCards.Testing;
using DemaConsulting.SysML2Tools.Cli;
using DemaConsulting.SysML2Tools.Query;

namespace DemaConsulting.SysML2Tools.Tests.Cli;

/// <summary>
///     Unit tests for the HelpCommand class.
/// </summary>
public class HelpCommandTests
{
    /// <summary>
    ///     The widest line a printed help card may have.
    /// </summary>
    private const int MaxLineWidth = 120;

    /// <summary>
    ///     The version the width test prints for the <c>{{version}}</c> token: a long prerelease version
    ///     with a commit hash, fixed so that the measured width does not depend on how the build stamps the
    ///     tool (a local build prints <c>0.0.0+hash</c>, a CI build <c>0.0.0-run.N+hash</c>).
    /// </summary>
    private const string RepresentativeVersion = "10.0.0-preview.123+0123456789abcdef0123456789abcdef01234567";

    /// <summary>
    ///     Test that the help verb alone prints the topic index and exits with code 0.
    /// </summary>
    [Fact]
    public void HelpCommand_TryRun_HelpVerb_PrintsTopicIndexAndReturnsZero()
    {
        // Arrange: a help command that writes to memory
        using var output = new StringWriter();
        using var error = new StringWriter();
        var command = HelpCommand.Create(output, error);

        // Act: run the help verb
        var answered = command.TryRun(["help"], out var exitCode);

        // Assert: the command answered, succeeded, and listed every root topic with its command
        Assert.True(answered);
        Assert.Equal(0, exitCode);
        foreach (var topic in HelpCommand.RootCardIds)
        {
            Assert.Contains($"{HelpCommand.ToolName} help {topic}", output.ToString());
        }
    }

    /// <summary>
    ///     Test that every command of the tool has a help card, printed by <c>help &lt;command&gt;</c> and by
    ///     <c>help commands &lt;command&gt;</c>.
    /// </summary>
    [Fact]
    public void HelpCommand_TryRun_EveryRegisteredCommand_PrintsCommandCard()
    {
        // Arrange: the names of the commands the tool accepts, asked for with and without the commands topic
        var requests = HelpCommand.CommandNames
            .SelectMany(name => new[]
            {
                (Name: name, Args: new[] { "help", name }),
                (Name: name, Args: new[] { "help", "commands", name })
            })
            .ToList();

        // Act: ask for the card of every command both ways
        var results = requests.Select(request => Run(request.Name, request.Args)).ToList();

        // Assert: every command has a card, which names the command, and both requests print it
        Assert.NotEmpty(results);
        Assert.All(results, result =>
        {
            Assert.True(result.Answered, $"'{result.Command}' was not answered.");
            Assert.Equal(0, result.ExitCode);
            Assert.Contains($"# {result.Name} command", result.Output);
        });
    }

    /// <summary>
    ///     Test that every query verb has a help card, printed by <c>help query &lt;verb&gt;</c>.
    /// </summary>
    [Fact]
    public void HelpCommand_TryRun_EveryQueryVerb_PrintsVerbCard()
    {
        // Arrange: the verbs the query command accepts
        var verbs = QueryVerbParsing.AllTokens;

        // Act: ask for the card of every verb
        var results = verbs.Select(verb => Run(verb, ["help", "query", verb])).ToList();

        // Assert: every verb has a card, which names the verb
        Assert.NotEmpty(results);
        Assert.All(results, result =>
        {
            Assert.True(result.Answered, $"'{result.Command}' was not answered.");
            Assert.Equal(0, result.ExitCode);
            Assert.Contains($"# query {result.Name}", result.Output);
        });
    }

    /// <summary>
    ///     Test that a command name after a help option with a value still prints the card of that command.
    /// </summary>
    [Fact]
    public void HelpCommand_TryRun_CommandNameAfterValueOption_PrintsCommandCard()
    {
        // Arrange: a help command that writes to memory
        using var output = new StringWriter();
        using var error = new StringWriter();
        var command = HelpCommand.Create(output, error);

        // Act: ask for the card of lint after the --width option and its value
        var answered = command.TryRun(["help", "--width", "100", "lint"], out var exitCode);

        // Assert: the command answered with the lint card, not the commands overview
        Assert.True(answered);
        Assert.Equal(0, exitCode);
        Assert.Contains("# lint command", output.ToString());
        Assert.DoesNotContain("# Commands", output.ToString());
    }

    /// <summary>
    ///     Test that the shipped catalog passes validation with warnings treated as errors; the
    ///     validation covers the expected topics, the purpose tags, and the command examples.
    /// </summary>
    [Fact]
    public void HelpCommand_CreateOptions_ShippedCatalog_PassesValidation()
    {
        // Arrange: validate exactly the composition the tool ships, with warnings as errors
        var configuration = HelpCommand.CreateOptions().GetCatalogConfiguration();
        var options = new HelpValidationOptions { WarningsAreErrors = true };

        // Act: validate the catalog; the validator throws an exception that lists every defect
        var exception = Record.Exception(() => HelpCardValidator.AssertValid(configuration, options));

        // Assert: no defect was found
        Assert.Null(exception);
    }

    /// <summary>
    ///     Test that every card prints in lines no wider than the maximum line width.
    /// </summary>
    [Fact]
    public void HelpCommand_TryRun_EveryCard_FitsMaximumLineWidth()
    {
        // Arrange: the cards the tool ships
        var catalog = HelpCatalog.Create(HelpCommand.CreateOptions().GetCatalogConfiguration());

        // Act: print every card with the shipped options but a fixed version, with its links and tokens
        // expanded, and measure its widest line
        var widths = catalog.Cards.Select(card =>
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            var shipped = HelpCommand.CreateOptions(output, error);
            var cli = HelpCli.Create(new HelpCliOptions
            {
                ToolName = shipped.ToolName,
                Version = RepresentativeVersion,
                ConfigureCatalog = shipped.ConfigureCatalog,
                CommandTemplate = shipped.CommandTemplate,
                DefaultWidth = shipped.DefaultWidth,
                Output = output,
                Error = error
            });
            var answered = cli.TryRun(["help", card.Id], out var exitCode);
            var width = output.ToString().Split('\n').Max(line => line.TrimEnd('\r').Length);
            return (card.Id, Answered: answered, ExitCode: exitCode, Width: width);
        }).ToList();

        // Assert: every card was answered with exit code 0, and none has a line wider than the maximum
        Assert.NotEmpty(widths);
        Assert.All(widths, width =>
        {
            Assert.True(width.Answered, $"Card '{width.Id}' was not answered.");
            Assert.Equal(0, width.ExitCode);
            Assert.True(
                width.Width <= MaxLineWidth,
                $"Card '{width.Id}' has a line of {width.Width} characters; the maximum is {MaxLineWidth}.");
        });
    }

    /// <summary>
    ///     Runs a help request with a help command that writes to memory.
    /// </summary>
    /// <param name="name">The command or verb that the request asks for.</param>
    /// <param name="args">The command-line arguments of the request.</param>
    /// <returns>The name, the command line, whether it was answered, the exit code, and the output.</returns>
    private static (string Name, string Command, bool Answered, int ExitCode, string Output) Run(
        string name,
        string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var answered = HelpCommand.Create(output, error).TryRun(args, out var exitCode);
        return (name, string.Join(' ', args), answered, exitCode, output.ToString());
    }
}
