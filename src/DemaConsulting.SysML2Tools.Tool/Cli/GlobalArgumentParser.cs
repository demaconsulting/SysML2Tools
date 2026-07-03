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

namespace DemaConsulting.SysML2Tools.Cli;

/// <summary>
///     Parses the truly cross-cutting command-line options — the ones that apply regardless of
///     which command (or no command) is selected — and identifies the selected command, leaving
///     every other token for that command's dedicated parser to interpret.
/// </summary>
internal static class GlobalArgumentParser
{
    /// <summary>
    ///     Parses <paramref name="args"/> into a <see cref="GlobalArguments"/> instance.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>The parsed global arguments, including the leftover per-command arguments.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when a recognized global flag requiring a value is missing one, or has an
    ///     invalid value (e.g., <c>--depth</c> with a non-integer or out-of-range argument).
    /// </exception>
    public static GlobalArguments Parse(string[] args)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(args);

        var version = false;
        var help = false;
        var silent = false;
        var validate = false;
        string? resultsFile = null;
        string? logFile = null;
        var headingDepth = 1;
        int? maxRenderDepth = null;
        var command = SysmlCommand.None;
        var commandArgs = new List<string>();

        var index = 0;
        while (index < args.Length)
        {
            var arg = args[index++];
            switch (arg)
            {
                case "-v":
                case "--version":
                    version = true;
                    break;

                case "-?":
                case "-h":
                case "--help":
                    help = true;
                    break;

                case "--silent":
                    silent = true;
                    break;

                case "--validate":
                    validate = true;
                    break;

                case "--log":
                    logFile = CliArgumentHelpers.GetRequiredStringArgument(arg, args, ref index, "a filename argument");
                    break;

                case "--results":
                case "--result":
                    resultsFile = CliArgumentHelpers.GetRequiredStringArgument(arg, args, ref index, "a results filename argument");
                    break;

                case "--depth":
                    var depth = CliArgumentHelpers.GetRequiredIntArgument(arg, args, ref index, "a heading depth argument", 1);
                    headingDepth = Math.Clamp(depth, 1, 6);
                    maxRenderDepth = depth;
                    break;

                // The command token is only recognized on its first occurrence; once a command
                // has been selected, a later literal "lint"/"render"/"query" token is treated as
                // an ordinary per-command argument (e.g., a file named "query").
                case "lint" when command == SysmlCommand.None:
                    command = SysmlCommand.Lint;
                    break;

                case "render" when command == SysmlCommand.None:
                    command = SysmlCommand.Render;
                    break;

                case "query" when command == SysmlCommand.None:
                    command = SysmlCommand.Query;
                    break;

                case "help" when command == SysmlCommand.None:
                    command = SysmlCommand.Help;
                    break;

                default:
                    // Not a recognized global flag or command token — leave it for the
                    // command-specific parser to interpret.
                    commandArgs.Add(arg);
                    break;
            }
        }

        return new GlobalArguments
        {
            Version = version,
            Help = help,
            Silent = silent,
            Validate = validate,
            ResultsFile = resultsFile,
            LogFile = logFile,
            HeadingDepth = headingDepth,
            MaxRenderDepth = maxRenderDepth,
            Command = command,
            CommandArgs = commandArgs
        };
    }
}
