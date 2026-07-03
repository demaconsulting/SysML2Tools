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

namespace DemaConsulting.SysML2Tools.Query;

/// <summary>
///     Parses the arguments remaining after the <c>query</c> command token into a
///     <see cref="QueryOptions"/> instance.
/// </summary>
/// <remarks>
///     The <c>query</c> grammar is structural: the first token following the <c>query</c> command
///     token must be a recognized verb (see <see cref="QueryVerbParsing"/>) — this is validated
///     eagerly here, not lazily inferred from a shared default case. When no verb is present and
///     <c>--help</c> was requested, parsing returns <see langword="null"/> (general help is shown
///     instead); when no verb is present and <c>--help</c> was not requested, a clear
///     <see cref="ArgumentException"/> is thrown rather than leaving the command in a silent
///     null/None state. Remaining tokens recognize <c>--element</c>/<c>-e</c>, <c>--direction</c>,
///     <c>--kind</c>, <c>--name</c>, <c>--include-stdlib</c>, and <c>--format</c>, plus positional
///     file glob patterns; any other <c>-</c>-prefixed token is rejected so that flags belonging to
///     other commands (e.g., <c>--auto</c>, <c>--output</c>) are never silently accepted.
///     <c>--format</c>'s value is captured raw and validated later by
///     <see cref="QueryCommand.RunAsync"/>, exactly as it already was before this refactor.
/// </remarks>
internal static class QueryArgumentParser
{
    /// <summary>
    ///     Parses the <c>query</c> command's arguments.
    /// </summary>
    /// <param name="commandArgs">
    ///     The arguments remaining after the global parser has stripped cross-cutting flags and
    ///     the <c>query</c> command token.
    /// </param>
    /// <param name="helpRequested">
    ///     <see langword="true"/> when the global <c>--help</c>/<c>-h</c>/<c>-?</c> flag was
    ///     supplied; suppresses the "verb is required" error when no verb token is present.
    /// </param>
    /// <param name="depth">
    ///     The global <c>--depth</c> value (shared with <c>render</c>'s diagram depth), threaded
    ///     through into <see cref="QueryOptions.Depth"/>.
    /// </param>
    /// <returns>
    ///     The parsed <see cref="QueryOptions"/>, or <see langword="null"/> when no verb token was
    ///     supplied and <paramref name="helpRequested"/> is <see langword="true"/> (e.g.,
    ///     <c>query --help</c>).
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when no verb token is present and <paramref name="helpRequested"/> is
    ///     <see langword="false"/>; when the first token is not a recognized verb; or when an
    ///     unrecognized flag is supplied.
    /// </exception>
    public static QueryOptions? Parse(IReadOnlyList<string> commandArgs, bool helpRequested, int? depth)
    {
        // The verb is a required structural first argument, validated strictly here rather than
        // lazily inferred by a shared default case.
        if (commandArgs.Count == 0)
        {
            if (helpRequested)
            {
                return null;
            }

            throw new ArgumentException(
                $"query: a verb is required. Valid verbs are: {string.Join(", ", QueryVerbParsing.AllTokens)}.");
        }

        var index = 0;
        var verbToken = commandArgs[index++];
        if (verbToken.StartsWith("-", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"query: expected a verb as the first argument, but found '{verbToken}'. " +
                $"Valid verbs are: {string.Join(", ", QueryVerbParsing.AllTokens)}.");
        }

        var verb = QueryVerbParsing.Parse(verbToken);

        string? element = null;
        string? direction = null;
        string? kind = null;
        string? nameFilter = null;
        string? format = null;
        var includeStdlib = false;
        var files = new List<string>();

        while (index < commandArgs.Count)
        {
            var arg = commandArgs[index++];
            switch (arg)
            {
                case "--element":
                case "-e":
                    element = CliArgumentHelpers.GetRequiredStringArgument(
                        arg, commandArgs, ref index, "an element qualified-name argument");
                    break;

                case "--direction":
                    direction = CliArgumentHelpers.GetRequiredStringArgument(
                        arg, commandArgs, ref index, "a direction argument (up, down, or both)");
                    break;

                case "--kind":
                    kind = CliArgumentHelpers.GetRequiredStringArgument(
                        arg, commandArgs, ref index, "a kind filter argument");
                    break;

                case "--name":
                    nameFilter = CliArgumentHelpers.GetRequiredStringArgument(
                        arg, commandArgs, ref index, "a name filter argument");
                    break;

                case "--format":
                    format = CliArgumentHelpers.GetRequiredStringArgument(
                        arg, commandArgs, ref index, "a format argument (markdown or json)");
                    break;

                case "--include-stdlib":
                    includeStdlib = true;
                    break;

                default:
                    if (arg.StartsWith("-", StringComparison.Ordinal))
                    {
                        throw new ArgumentException(
                            $"Unsupported argument '{arg}' for the 'query' command.", nameof(commandArgs));
                    }

                    files.Add(arg);
                    break;
            }
        }

        return new QueryOptions
        {
            Verb = verb,
            Element = element,
            Format = format,
            Depth = depth,
            Direction = direction,
            Kind = kind,
            NameFilter = nameFilter,
            IncludeStdlib = includeStdlib,
            Files = files
        };
    }
}
