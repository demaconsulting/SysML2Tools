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

using DemaConsulting.HelpCards.Integration;
using DemaConsulting.HelpCards.Loading;

namespace DemaConsulting.SysML2Tools.Cli;

/// <summary>
///     Answers the <c>sysml2tools help</c> command from the help cards embedded in the tool assembly.
/// </summary>
/// <remarks>
///     The only unit that references HelpCards. Creating an instance performs no I/O, and the catalog is
///     built only when the first argument is <c>help</c>. The exit codes and the bypass of <see cref="Context"/>
///     are described in the HelpCommand design. Instances are immutable and safe to use from several threads.
/// </remarks>
internal sealed class HelpCommand
{
    /// <summary>
    ///     The name of the tool as the user types it; the usage lines and the <c>{{tool}}</c> token use it.
    /// </summary>
    internal const string ToolName = "sysml2tools";

    /// <summary>
    ///     The id of the topic whose child cards describe the commands of the tool.
    /// </summary>
    private const string CommandsTopic = "commands";

    /// <summary>
    ///     Ids of the root cards: the topics that <c>sysml2tools help</c> lists. A missing or extra root card
    ///     fails the catalog.
    /// </summary>
    internal static readonly string[] RootCardIds =
    [
        "getting-started",
        "command-line",
        CommandsTopic,
        "workspaces",
        "sysml-models",
        "views",
        "model-queries",
        "self-validation",
        "troubleshooting"
    ];

    /// <summary>
    ///     The names of the commands of the tool, as the user types them: every <see cref="SysmlCommand"/>
    ///     except <see cref="SysmlCommand.None"/>, in lower case. Deriving them from the command enumeration
    ///     keeps the routing of <c>help &lt;command&gt;</c> in step with the commands the parser accepts.
    /// </summary>
    internal static readonly string[] CommandNames =
    [
        .. Enum.GetValues<SysmlCommand>()
            .Where(command => command != SysmlCommand.None)
            .Select(command => command.ToString().ToLowerInvariant())
    ];

    /// <summary>
    ///     The closed tag vocabulary: every card carries one of these purpose tags, and no other tag.
    /// </summary>
    private static readonly string[] PurposeTags = ["tutorial", "reference", "troubleshooting"];

    /// <summary>
    ///     The help options of HelpCards that take a value, so that the value is not taken for a path word.
    /// </summary>
    private static readonly string[] ValueOptions = ["--format", "--example", "--search", "--tag", "--source", "--width"];

    /// <summary>
    ///     The cards embedded in the tool assembly; declared before <see cref="Default"/> so that it is
    ///     initialized first. Creating the source reads nothing.
    /// </summary>
    private static readonly HelpSource Cards = HelpSource.FromAssembly(typeof(HelpCommand).Assembly);

    /// <summary>
    ///     The underlying command-line helper.
    /// </summary>
    private readonly HelpCli _cli;

    /// <summary>
    ///     Initializes a new instance of the <see cref="HelpCommand"/> class.
    /// </summary>
    /// <param name="output">The writer for output, or <see langword="null"/> for the console output.</param>
    /// <param name="error">The writer for errors, or <see langword="null"/> for the console error.</param>
    private HelpCommand(TextWriter? output, TextWriter? error) =>
        _cli = HelpCli.Create(CreateOptions(output, error));

    /// <summary>
    ///     Gets the instance used by the tool, which writes to the console.
    /// </summary>
    internal static HelpCommand Default { get; } = new(null, null);

    /// <summary>
    ///     Gets the sentence that ends the usage text and points to <c>sysml2tools help</c>.
    /// </summary>
    public string Footer => _cli.Footer;

    /// <summary>
    ///     Creates the options of the command-line helper; tests build the shipped catalog from
    ///     <see cref="HelpCliOptions.GetCatalogConfiguration"/>.
    /// </summary>
    /// <param name="output">The writer for output, or <see langword="null"/> for the console output.</param>
    /// <param name="error">The writer for errors, or <see langword="null"/> for the console error.</param>
    /// <returns>The options.</returns>
    internal static HelpCliOptions CreateOptions(TextWriter? output = null, TextWriter? error = null) =>
        new()
        {
            ToolName = ToolName,
            Version = Program.Version,
            ConfigureCatalog = (builder, _) => builder
                .AddSource(Cards)
                .TagVocabulary(PurposeTags, PurposeTags)
                .ExpectedRoots(RootCardIds),
            UsageLines =
            [
                $"{ToolName} help [topic]",
                $"{ToolName} help <command>",
                $"{ToolName} help query <verb>",
                $"{ToolName} help --search <words>"
            ],
            IndexPreamble = $"Start with '{ToolName} help getting-started'.",
            Output = output,
            Error = error
        };

    /// <summary>
    ///     Creates a help command that writes to the given writers.
    /// </summary>
    /// <param name="output">The writer for output.</param>
    /// <param name="error">The writer for errors.</param>
    /// <returns>The new help command.</returns>
    internal static HelpCommand Create(TextWriter output, TextWriter error) => new(output, error);

    /// <summary>
    ///     Runs the help command when the first argument is <c>help</c>.
    /// </summary>
    /// <param name="args">The command-line arguments of the tool.</param>
    /// <param name="exitCode">Receives the exit code when the command was answered.</param>
    /// <returns>
    ///     <see langword="true"/> when the command was answered; <see langword="false"/> when the tool should
    ///     continue with its own option parser.
    /// </returns>
    /// <remarks>
    ///     A command card is a child of the <c>commands</c> topic, and HelpCards resolves the first word of a
    ///     path only against the topics. So that <c>sysml2tools help &lt;command&gt;</c> and
    ///     <c>sysml2tools help query &lt;verb&gt;</c> keep printing the help of that command or verb, as they
    ///     did before the help cards, a first path word that names a command is prefixed with <c>commands</c>.
    /// </remarks>
    public bool TryRun(string[] args, out int exitCode)
    {
        // Leave anything that is not the help verb to the helper, which answers false without any work
        if (args.Length == 0 || !string.Equals(args[0], "help", StringComparison.OrdinalIgnoreCase))
        {
            return _cli.TryRun(args, out exitCode);
        }

        exitCode = _cli.Run(ExpandCommandName(args[1..]));
        return true;
    }

    /// <summary>
    ///     Inserts <c>commands</c> before the first path word when that word names a command of the tool.
    /// </summary>
    /// <param name="helpArguments">The arguments after the help verb.</param>
    /// <returns>The arguments to pass to the helper.</returns>
    private static List<string> ExpandCommandName(string[] helpArguments)
    {
        var arguments = helpArguments.ToList();
        var index = FindFirstPathWord(arguments);
        if (index >= 0 && CommandNames.Any(name =>
                string.Equals(name, arguments[index], StringComparison.OrdinalIgnoreCase)))
        {
            arguments.Insert(index, CommandsTopic);
        }

        return arguments;
    }

    /// <summary>
    ///     Finds the first argument that is a path word rather than an option or the value of an option.
    /// </summary>
    /// <param name="arguments">The arguments after the help verb.</param>
    /// <returns>The index of the first path word, or -1 when there is none.</returns>
    private static int FindFirstPathWord(List<string> arguments)
    {
        var index = 0;
        while (index < arguments.Count)
        {
            var argument = arguments[index];
            if (argument == "--")
            {
                // Everything after the end of the options is a path word
                return index + 1 < arguments.Count ? index + 1 : -1;
            }

            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                return index;
            }

            // A value option takes the next argument as its value, unless that argument is an option
            var takesValue = ValueOptions.Contains(argument) && index + 1 < arguments.Count &&
                             !arguments[index + 1].StartsWith("--", StringComparison.Ordinal);
            index += takesValue ? 2 : 1;
        }

        return -1;
    }
}
