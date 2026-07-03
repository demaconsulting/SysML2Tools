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

using DemaConsulting.SysML2Tools.Query;

namespace DemaConsulting.SysML2Tools.Cli;

/// <summary>
///     Top-level command selected by the user.
/// </summary>
internal enum SysmlCommand
{
    /// <summary>No command specified — prints banner and help hint.</summary>
    None,

    /// <summary>Parse a workspace and report syntax diagnostics.</summary>
    Lint,

    /// <summary>Render view diagrams to SVG or PNG files.</summary>
    Render,

    /// <summary>Run a model-analysis query verb.</summary>
    Query
}

/// <summary>
///     Context class that handles command-line arguments and program output.
/// </summary>
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
    ///     Gets the file glob patterns supplied as positional arguments.
    /// </summary>
    public IReadOnlyList<string> Files { get; private init; } = Array.Empty<string>();


    /// <summary>
    ///     Gets the heading depth for markdown output; valid range 1–6, default 1;
    ///     supplied via <c>--depth</c>.
    /// </summary>
    public int HeadingDepth { get; private init; } = 1;

    /// <summary>
    ///     Gets the maximum diagram render depth; <see langword="null"/> means unlimited.
    ///     Supplied via <c>--depth</c> and passed through directly (not clamped to 6).
    /// </summary>
    public int? MaxRenderDepth { get; private init; }

    /// <summary>
    ///     Gets the view name filter for the render command; <see langword="null"/> means
    ///     render all views. Supplied via <c>--view</c>.
    /// </summary>
    public string? ViewName { get; private init; }

    /// <summary>
    ///     Gets the output directory path for rendered diagram files.
    /// </summary>
    public string? OutputDirectory { get; private init; }

    /// <summary>
    ///     Gets the renderer format identifier (e.g., <c>"svg"</c> or <c>"png"</c>).
    ///     Defaults to <see langword="null"/>, which the render command interprets as SVG.
    /// </summary>
    public string? RendererFormat { get; private init; }

    /// <summary>
    ///     Gets a value indicating whether the <c>--auto</c> flag was specified.
    /// </summary>
    /// <remarks>
    ///     When <see langword="true"/> and the workspace has no user-defined view declarations,
    ///     the render command synthesizes a GeneralView targeting the most representative
    ///     top-level element. The flag is silently ignored when views already exist.
    /// </remarks>
    public bool AutoView { get; private init; }

    /// <summary>
    ///     Gets the parsed options for the <c>query</c> command; <see langword="null"/> unless
    ///     <see cref="Command"/> is <see cref="SysmlCommand.Query"/> and a recognized verb was
    ///     supplied.
    /// </summary>
    public QueryOptions? Query { get; private init; }

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

        var parser = new ArgumentParser();
        parser.ParseArguments(args);

        var result = new Context
        {
            Version = parser.Version,
            Help = parser.Help,
            Silent = parser.Silent,
            Validate = parser.Validate,
            ResultsFile = parser.ResultsFile,
            HeadingDepth = parser.HeadingDepth,
            MaxRenderDepth = parser.MaxRenderDepth,
            ViewName = parser.ViewName,
            Command = parser.Command,
            Files = parser.Files,
            OutputDirectory = parser.OutputDirectory,
            RendererFormat = parser.RendererFormat,
            AutoView = parser.AutoView,
            Query = BuildQueryOptions(parser)
        };

        // Open log file if specified
        if (parser.LogFile != null)
        {
            result.OpenLogFile(parser.LogFile);
        }

        return result;
    }

    /// <summary>
    ///     Builds a <see cref="QueryOptions"/> instance from the parser's fields when the
    ///     <c>query</c> command and a verb were both supplied.
    /// </summary>
    /// <param name="parser">The argument parser holding the parsed field values.</param>
    /// <returns>
    ///     A new <see cref="QueryOptions"/>, or <see langword="null"/> when <see cref="ArgumentParser.Command"/>
    ///     is not <see cref="SysmlCommand.Query"/> or no verb was captured (e.g., <c>query --help</c>).
    /// </returns>
    private static QueryOptions? BuildQueryOptions(ArgumentParser parser)
    {
        if (parser.Command != SysmlCommand.Query || parser.QueryVerb is not { } verb)
        {
            return null;
        }

        return new QueryOptions
        {
            Verb = verb,
            Element = parser.Element,
            Format = parser.RendererFormat,
            Depth = parser.MaxRenderDepth,
            Direction = parser.Direction,
            Kind = parser.Kind,
            NameFilter = parser.NameFilter,
            IncludeStdlib = parser.IncludeStdlib,
            Files = parser.QueryFiles
        };
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
    ///     Helper class for parsing command-line arguments
    /// </summary>
    private sealed class ArgumentParser
    {
        /// <summary>
        ///     Gets a value indicating whether the version flag was specified.
        /// </summary>
        public bool Version { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the help flag was specified.
        /// </summary>
        public bool Help { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the silent flag was specified.
        /// </summary>
        public bool Silent { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the validate flag was specified.
        /// </summary>
        public bool Validate { get; private set; }

        /// <summary>
        ///     Gets the log file path.
        /// </summary>
        public string? LogFile { get; private set; }

        /// <summary>
        ///     Gets the validation results file path.
        /// </summary>
        public string? ResultsFile { get; private set; }

        /// <summary>
        ///     Gets the top-level command.
        /// </summary>
        public SysmlCommand Command { get; private set; }

        /// <summary>
        ///     File glob patterns collected from positional arguments.
        /// </summary>
        private readonly List<string> _files = [];

        /// <summary>
        ///     Gets the file glob patterns supplied as positional arguments.
        /// </summary>
        public IReadOnlyList<string> Files => _files;

        /// <summary>
        ///     File glob patterns collected from positional arguments after a <c>query</c> verb.
        /// </summary>
        /// <remarks>
        ///     Kept separate from <see cref="_files"/> so that <c>query</c>'s positional file
        ///     handling cannot interfere with the top-level <see cref="Files"/> read by
        ///     <c>lint</c>/<c>render</c>.
        /// </remarks>
        private readonly List<string> _queryFiles = [];

        /// <summary>
        ///     Gets the file glob patterns supplied as positional arguments after a query verb.
        /// </summary>
        public IReadOnlyList<string> QueryFiles => _queryFiles;

        /// <summary>
        ///     Gets the recognized query verb captured from the first bare word following the
        ///     <c>query</c> command token; <see langword="null"/> when no verb has been captured.
        /// </summary>
        public QueryVerb? QueryVerb { get; private set; }

        /// <summary>
        ///     Gets the target element qualified name supplied via <c>--element</c>/<c>-e</c>.
        /// </summary>
        public string? Element { get; private set; }

        /// <summary>
        ///     Gets the traversal direction supplied via <c>--direction</c> (query <c>hierarchy</c> verb).
        /// </summary>
        public string? Direction { get; private set; }

        /// <summary>
        ///     Gets the element-kind filter supplied via <c>--kind</c> (query <c>list</c>/<c>find</c> verbs).
        /// </summary>
        public string? Kind { get; private set; }

        /// <summary>
        ///     Gets the name substring filter supplied via <c>--name</c> (query <c>list</c>/<c>find</c> verbs).
        /// </summary>
        public string? NameFilter { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the <c>--include-stdlib</c> flag was specified.
        /// </summary>
        public bool IncludeStdlib { get; private set; }

        /// <summary>
        ///     Gets the heading depth for markdown output.
        /// </summary>
        public int HeadingDepth { get; private set; } = 1;

        /// <summary>
        ///     Gets the maximum diagram render depth; <see langword="null"/> means unlimited.
        /// </summary>
        public int? MaxRenderDepth { get; private set; }

        /// <summary>
        ///     Gets the view name filter for the render command.
        /// </summary>
        public string? ViewName { get; private set; }

        /// <summary>
        ///     Gets the output directory path for rendered diagram files.
        /// </summary>
        public string? OutputDirectory { get; private set; }

        /// <summary>
        ///     Gets the renderer format identifier supplied via <c>--format</c>.
        /// </summary>
        public string? RendererFormat { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the <c>--auto</c> flag was specified.
        /// </summary>
        public bool AutoView { get; private set; }

        /// <summary>
        ///     Parses command-line arguments
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        public void ParseArguments(string[] args)
        {
            // Validate input
            ArgumentNullException.ThrowIfNull(args);

            int i = 0;
            while (i < args.Length)
            {
                var arg = args[i++];
                i = ParseArgument(arg, args, i);
            }
        }

        /// <summary>
        ///     Parses a single argument
        /// </summary>
        /// <param name="arg">Argument to parse</param>
        /// <param name="args">All arguments</param>
        /// <param name="index">Current index</param>
        /// <returns>Updated index</returns>
        private int ParseArgument(string arg, string[] args, int index)
        {
            switch (arg)
            {
                case "-v":
                case "--version":
                    Version = true;
                    return index;

                case "-?":
                case "-h":
                case "--help":
                    Help = true;
                    return index;

                case "--silent":
                    Silent = true;
                    return index;

                case "--validate":
                    Validate = true;
                    return index;

                case "--log":
                    LogFile = GetRequiredStringArgument(arg, args, index, "a filename argument");
                    return index + 1;

                case "--results":
                case "--result":
                    ResultsFile = GetRequiredStringArgument(arg, args, index, "a results filename argument");
                    return index + 1;

                case "--depth":
                    var depth = GetRequiredIntArgument(arg, args, index, "a heading depth argument", 1);
                    HeadingDepth = Math.Clamp(depth, 1, 6);
                    MaxRenderDepth = depth;
                    return index + 1;

                case "--output":
                    OutputDirectory = GetRequiredStringArgument(arg, args, index, "an output directory argument");
                    return index + 1;

                case "--format":
                    RendererFormat = GetRequiredStringArgument(arg, args, index, "a format argument (svg or png)");
                    return index + 1;

                case "--view":
                    ViewName = GetRequiredStringArgument(arg, args, index, "a view name argument");
                    return index + 1;

                case "--auto":
                    AutoView = true;
                    return index;

                case "--element":
                case "-e":
                    Element = GetRequiredStringArgument(arg, args, index, "an element qualified-name argument");
                    return index + 1;

                case "--direction":
                    Direction = GetRequiredStringArgument(arg, args, index, "a direction argument (up, down, or both)");
                    return index + 1;

                case "--kind":
                    Kind = GetRequiredStringArgument(arg, args, index, "a kind filter argument");
                    return index + 1;

                case "--name":
                    NameFilter = GetRequiredStringArgument(arg, args, index, "a name filter argument");
                    return index + 1;

                case "--include-stdlib":
                    IncludeStdlib = true;
                    return index;

                case "lint":
                    Command = SysmlCommand.Lint;
                    return index;

                case "render":
                    Command = SysmlCommand.Render;
                    return index;

                case "query":
                    Command = SysmlCommand.Query;
                    return index;

                default:
                    if (!arg.StartsWith("-", StringComparison.Ordinal))
                    {
                        // When the 'query' command has been seen and no verb captured yet, the
                        // first bare word is the verb token, not a file glob pattern. This guard
                        // only activates for Command == Query, a value no lint/render invocation
                        // can produce, so lint/render positional-file behavior is unaffected.
                        if (Command == SysmlCommand.Query && QueryVerb is null)
                        {
                            QueryVerb = QueryVerbParsing.Parse(arg);
                            return index;
                        }

                        // Positional argument — treat as a file glob pattern
                        if (Command == SysmlCommand.Query)
                        {
                            _queryFiles.Add(arg);
                        }
                        else
                        {
                            _files.Add(arg);
                        }

                        return index;
                    }

                    throw new ArgumentException($"Unsupported argument '{arg}'", nameof(args));
            }
        }

        /// <summary>
        ///     Gets a required string argument value
        /// </summary>
        /// <param name="arg">Argument name</param>
        /// <param name="args">All arguments</param>
        /// <param name="index">Current index</param>
        /// <param name="description">Description of what's required</param>
        /// <returns>Argument value</returns>
        private static string GetRequiredStringArgument(string arg, string[] args, int index, string description)
        {
            if (index >= args.Length)
            {
                throw new ArgumentException($"{arg} requires {description}", nameof(args));
            }

            return args[index];
        }

        /// <summary>
        ///     Gets a required integer argument value
        /// </summary>
        /// <param name="arg">Argument name</param>
        /// <param name="args">All arguments</param>
        /// <param name="index">Current index</param>
        /// <param name="description">Description of what's required</param>
        /// <param name="min">Minimum valid value (inclusive)</param>
        /// <param name="max">Maximum valid value (inclusive)</param>
        /// <returns>Argument value as an integer in [min, max]</returns>
        private static int GetRequiredIntArgument(string arg, string[] args, int index, string description, int min = 1, int max = int.MaxValue)
        {
            var value = GetRequiredStringArgument(arg, args, index, description);
            if (!int.TryParse(value, out var result) || result < min || result > max)
            {
                throw new ArgumentException($"{arg} requires an integer between {min} and {max} for {description}", nameof(args));
            }

            return result;
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
