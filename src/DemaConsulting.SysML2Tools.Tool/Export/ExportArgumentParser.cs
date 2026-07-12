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

namespace DemaConsulting.SysML2Tools.Export;

/// <summary>
///     Parses the arguments remaining after the <c>export</c> command token into an
///     <see cref="ExportOptions"/> instance.
/// </summary>
/// <remarks>
///     Recognizes only <c>--format</c>, <c>--output</c>, <c>--include-stdlib</c>, <c>--target</c>,
///     and <c>--filter</c>, plus positional file glob patterns. Any other <c>-</c>-prefixed token
///     is rejected so that flags belonging to other commands (e.g., <c>--view</c>, <c>--kind</c>)
///     are never silently accepted. <c>--format</c>'s value is captured raw and validated later by
///     <see cref="ExportCommand.RunAsync"/>, matching the <c>query</c>/<c>render</c> commands'
///     validation style; <c>--target</c>'s and <c>--filter</c>'s values are likewise captured raw
///     here and resolved/applied later by <see cref="ExportCommand.RunAsync"/>.
/// </remarks>
internal static class ExportArgumentParser
{
    /// <summary>
    ///     Parses the <c>export</c> command's arguments.
    /// </summary>
    /// <param name="commandArgs">
    ///     The arguments remaining after the global parser has stripped cross-cutting flags and
    ///     the <c>export</c> command token.
    /// </param>
    /// <returns>The parsed <see cref="ExportOptions"/>.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when an unrecognized flag is supplied, or a recognized flag requiring a value is
    ///     missing one.
    /// </exception>
    public static ExportOptions Parse(IReadOnlyList<string> commandArgs)
    {
        string? format = null;
        string? output = null;
        var includeStdlib = false;
        string? target = null;
        string? filterExpression = null;
        var files = new List<string>();

        var index = 0;
        while (index < commandArgs.Count)
        {
            var arg = commandArgs[index++];
            switch (arg)
            {
                case "--format":
                    format = CliArgumentHelpers.GetRequiredStringArgument(
                        arg, commandArgs, ref index, "a format argument (json or jsonl)");
                    break;

                case "--output":
                    output = CliArgumentHelpers.GetRequiredStringArgument(
                        arg, commandArgs, ref index, "an output file path argument");
                    break;

                case "--include-stdlib":
                    includeStdlib = true;
                    break;

                case "--target":
                    target = CliArgumentHelpers.GetRequiredStringArgument(
                        arg, commandArgs, ref index, "a target qualified-name argument");
                    break;

                case "--filter":
                    filterExpression = CliArgumentHelpers.GetRequiredStringArgument(
                        arg, commandArgs, ref index, "a filter expression argument");
                    break;

                default:
                    if (arg.StartsWith("-", StringComparison.Ordinal))
                    {
                        throw new ArgumentException(
                            $"Unsupported argument '{arg}' for the 'export' command.", nameof(commandArgs));
                    }

                    files.Add(arg);
                    break;
            }
        }

        return new ExportOptions
        {
            Format = format,
            Output = output,
            IncludeStdlib = includeStdlib,
            Target = target,
            FilterExpression = filterExpression,
            Files = files
        };
    }
}
