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

using DemaConsulting.SysML2Tools.Export;
using DemaConsulting.SysML2Tools.Help;
using DemaConsulting.SysML2Tools.Lint;
using DemaConsulting.SysML2Tools.Query;
using DemaConsulting.SysML2Tools.Render;

namespace DemaConsulting.SysML2Tools.Cli;

/// <summary>
///     Context class that handles command-line arguments and program output.
/// </summary>
/// <remarks>
///     Argument parsing is split into a <see cref="GlobalArgumentParser"/> pass (cross-cutting
///     options that apply regardless of command) followed by exactly one per-command parser
///     dispatch (<see cref="LintArgumentParser"/>, <see cref="RenderArgumentParser"/>,
///     <see cref="Query.QueryCliArgumentParser"/>, or <see cref="HelpArgumentParser"/>), so that
///     each command rejects flags outside its own grammar instead of sharing one mega-switch. See
///     <c>docs/design/sysml2-tools-tool/cli/context.md</c> for the full architecture.
/// </remarks>
internal sealed class Context : IDisposable
{
    /// <summary>
    ///     Log file stream writer (if logging is enabled).
    /// </summary>
    private StreamWriter? _logWriter;

    /// <summary>
    ///     Indicates whether errors have been reported.
    /// </summary>
    private bool _hasErrors;

    /// <summary>
    ///     Gets a value indicating whether the version flag was specified.
    /// </summary>
    public bool Version { get; private init; }

    /// <summary>
    ///     Gets a value indicating whether the help flag was specified.
    /// </summary>
    public bool Help { get; private init; }

    /// <summary>
    ///     Gets a value indicating whether the silent flag was specified.
    /// </summary>
    public bool Silent { get; private init; }

    /// <summary>
    ///     Gets a value indicating whether the validate flag was specified.
    /// </summary>
    public bool Validate { get; private init; }

    /// <summary>
    ///     Gets the validation results file path.
    /// </summary>
    public string? ResultsFile { get; private init; }

    /// <summary>
    ///     Gets the top-level command to execute.
    /// </summary>
    public SysmlCommand Command { get; private init; }

    /// <summary>
    ///     Gets the heading depth for markdown output; valid range 1–6, default 1;
    ///     supplied via <c>--depth</c>.
    /// </summary>
    public int HeadingDepth { get; private init; } = 1;

    /// <summary>
    ///     Gets the parsed options for the <c>lint</c> command; <see langword="null"/> unless
    ///     <see cref="Command"/> is <see cref="SysmlCommand.Lint"/>.
    /// </summary>
    public LintOptions? Lint { get; private init; }

    /// <summary>
    ///     Gets the parsed options for the <c>render</c> command; <see langword="null"/> unless
    ///     <see cref="Command"/> is <see cref="SysmlCommand.Render"/>.
    /// </summary>
    public RenderCommandOptions? Render { get; private init; }

    /// <summary>
    ///     Gets the parsed options for the <c>query</c> command; <see langword="null"/> unless
    ///     <see cref="Command"/> is <see cref="SysmlCommand.Query"/> and a recognized verb was
    ///     supplied.
    /// </summary>
    public QueryOptions? Query { get; private init; }

    /// <summary>
    ///     Gets the file glob patterns supplied as positional arguments to the <c>query</c>
    ///     command; empty unless <see cref="Command"/> is <see cref="SysmlCommand.Query"/>.
    /// </summary>
    /// <remarks>
    ///     Kept separate from <see cref="Query"/> because Core's public <c>QueryOptions</c> no
    ///     longer carries a file-glob-pattern property (a CLI-only concern) — see
    ///     <see cref="Query.QueryCliArgumentParser"/>.
    /// </remarks>
    public IReadOnlyList<string> QueryFiles { get; private init; } = [];

    /// <summary>
    ///     Gets the <c>query</c> command's <c>--output</c> file path; <see langword="null"/>
    ///     means write to stdout via <see cref="WriteLine"/>.
    /// </summary>
    /// <remarks>
    ///     Mirrors <see cref="Export.ExportOptions.Output"/>'s single-output-FILE convention
    ///     (not a directory, unlike <see cref="Render.RenderCommandOptions.OutputDirectory"/>).
    ///     Kept separate from <see cref="Query"/> because Core's public <c>QueryOptions</c> has no
    ///     CLI-I/O concept of its own — see <see cref="Query.QueryCliArgumentParser"/>.
    /// </remarks>
    public string? QueryOutput { get; private init; }

    /// <summary>
    ///     Gets the parsed options for the <c>export</c> command; <see langword="null"/> unless
    ///     <see cref="Command"/> is <see cref="SysmlCommand.Export"/>.
    /// </summary>
    public ExportOptions? Export { get; private init; }

    /// <summary>
    ///     Gets the parsed options for the <c>help</c> command; <see langword="null"/> unless
    ///     <see cref="Command"/> is <see cref="SysmlCommand.Help"/>.
    /// </summary>
    /// <remarks>
    ///     Named <c>HelpCommand</c> (rather than <c>Help</c>) to avoid colliding with the existing
    ///     <see cref="Help"/> flag property, which reflects the global <c>-h</c>/<c>-?</c>/
    ///     <c>--help</c> flag independently of whether <see cref="SysmlCommand.Help"/> was
    ///     selected as the command.
    /// </remarks>
    public HelpOptions? HelpCommand { get; private init; }

    /// <summary>
    ///     Gets the proposed exit code for the application (0 for success, 1 for errors).
    /// </summary>
    public int ExitCode => _hasErrors ? 1 : 0;

    /// <summary>
    ///     Private constructor - use Create factory method instead.
    /// </summary>
    private Context()
    {
    }

    /// <summary>
    ///     Creates a Context instance from command-line arguments.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>A new Context instance.</returns>
    /// <exception cref="ArgumentException">Thrown when arguments are invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the specified log file cannot be opened.</exception>
    public static Context Create(string[] args)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(args);

        // Parse the cross-cutting global options and identify the selected command, leaving the
        // remaining tokens for that command's dedicated parser.
        var global = GlobalArgumentParser.Parse(args);

        LintOptions? lintOptions = null;
        RenderCommandOptions? renderOptions = null;
        QueryOptions? queryOptions = null;
        IReadOnlyList<string> queryFiles = [];
        string? queryOutput = null;
        ExportOptions? exportOptions = null;
        HelpOptions? helpOptions = null;

        switch (global.Command)
        {
            case SysmlCommand.Lint:
                lintOptions = LintArgumentParser.Parse(global.CommandArgs);
                break;

            case SysmlCommand.Render:
                renderOptions = RenderArgumentParser.Parse(global.CommandArgs);
                break;

            case SysmlCommand.Query:
                (queryOptions, queryFiles, queryOutput) = QueryCliArgumentParser.Parse(global.CommandArgs, global.Help);
                break;

            case SysmlCommand.Export:
                exportOptions = ExportArgumentParser.Parse(global.CommandArgs);
                break;

            case SysmlCommand.Help:
                helpOptions = HelpArgumentParser.Parse(global.CommandArgs);
                break;

            default:
                // No command selected: preserve the historical bare-invocation behavior of
                // rejecting any leftover flag-like token (e.g., "sysml2tools --unknown").
                var badArg = global.CommandArgs.FirstOrDefault(
                    arg => arg.StartsWith("-", StringComparison.Ordinal));
                if (badArg != null)
                {
                    throw new ArgumentException($"Unsupported argument '{badArg}'", nameof(args));
                }

                break;
        }

        var result = new Context
        {
            Version = global.Version,
            Help = global.Help,
            Silent = global.Silent,
            Validate = global.Validate,
            ResultsFile = global.ResultsFile,
            HeadingDepth = global.HeadingDepth,
            Command = global.Command,
            Lint = lintOptions,
            Render = renderOptions,
            Query = queryOptions,
            QueryFiles = queryFiles,
            QueryOutput = queryOutput,
            Export = exportOptions,
            HelpCommand = helpOptions
        };

        // Open log file if specified
        if (global.LogFile != null)
        {
            result.OpenLogFile(global.LogFile);
        }

        return result;
    }

    /// <summary>
    ///     Opens the log file for writing
    /// </summary>
    /// <param name="logFile">Log file path</param>
    private void OpenLogFile(string logFile)
    {
        try
        {
            // Open with AutoFlush enabled so log entries are immediately written to disk
            // even if the application terminates unexpectedly before Dispose is called
            _logWriter = new StreamWriter(logFile, append: false) { AutoFlush = true };
        }
        // Generic catch is justified here to wrap any file system exception with context.
        // Expected exceptions include IOException, UnauthorizedAccessException, ArgumentException,
        // NotSupportedException, and other file system-related exceptions.
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to open log file '{logFile}': {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Writes a line of output to the console and log file (if logging is enabled).
    /// </summary>
    /// <param name="message">The message to write.</param>
    /// <remarks>
    ///     Output is written to stdout. When <see cref="Silent"/> is <c>true</c>, stdout output is
    ///     suppressed, but the message is still written to the log file when one is open.
    /// </remarks>
    public void WriteLine(string message)
    {
        // Write to console unless silent mode is enabled
        if (!Silent)
        {
            Console.WriteLine(message);
        }

        // Write to log file if logging is enabled
        _logWriter?.WriteLine(message);
    }

    /// <summary>
    ///     Writes an error message to the error console and log file (if logging is enabled).
    /// </summary>
    /// <param name="message">The error message to write.</param>
    /// <remarks>
    ///     <c>_hasErrors</c> is set to <c>true</c> unconditionally, so <see cref="ExitCode"/> will
    ///     return 1 regardless of whether <see cref="Silent"/> suppresses the console output.
    ///     Stderr output is suppressed when <see cref="Silent"/> is <c>true</c>, but the message
    ///     is still written to the log file when one is open.
    /// </remarks>
    public void WriteError(string message)
    {
        // Mark that we have encountered errors
        _hasErrors = true;

        // Write to error console unless silent mode is enabled
        if (!Silent)
        {
            var previousColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(message);
            Console.ForegroundColor = previousColor;
        }

        // Write to log file if logging is enabled
        _logWriter?.WriteLine(message);
    }

    /// <summary>
    ///     Disposes resources used by the Context.
    /// </summary>
    public void Dispose()
    {
        // Close and dispose the log file writer if it exists
        _logWriter?.Dispose();
        _logWriter = null;
    }
}
