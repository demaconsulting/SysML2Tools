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
///     Thin Tool-only wrapper around Core's <see cref="QueryArgumentParser"/>: extracts the
///     Tool-only <c>--output &lt;file&gt;</c> and <c>--strict-unresolved</c> flags from
///     <c>commandArgs</c> before delegating the remaining tokens to Core's parser.
/// </summary>
/// <remarks>
///     Core's public <see cref="QueryArgumentParser"/> is intentionally unaware of
///     <c>--output</c> (a CLI-I/O-only concept with no meaning for a library caller) and of
///     <c>--strict-unresolved</c> (a workspace-loading option the Core query engine does not see),
///     so this type pre-scans for them (using the Tool project's own
///     <see cref="CliArgumentHelpers"/>, exactly as every other Tool-only command parser does)
///     and removes the tokens (the flags and the <c>--output</c> value) before calling
///     <see cref="QueryArgumentParser.Parse"/> with what remains.
///     This duplicates a small amount of token-scanning logic already present in Core's parser
///     (the loop structure, the <c>-</c>-prefix rejection convention) rather than unifying into
///     one parser — teaching Core's public parser about a Tool-only I/O flag would be
///     architecturally worse than this small, isolated, test-covered duplication.
/// </remarks>
internal static class QueryCliArgumentParser
{
    /// <summary>
    ///     Parses the <c>query</c> command's arguments, extracting the Tool-only
    ///     <c>--output</c> and <c>--strict-unresolved</c> flags before delegating the remainder to
    ///     Core's <see cref="QueryArgumentParser.Parse"/>.
    /// </summary>
    /// <param name="commandArgs">
    ///     The arguments remaining after the global parser has stripped cross-cutting flags and
    ///     the <c>query</c> command token.
    /// </param>
    /// <param name="helpRequested">
    ///     <see langword="true"/> when the global <c>--help</c>/<c>-h</c>/<c>-?</c> flag was
    ///     supplied; suppresses the "verb is required" error when no verb token is present.
    /// </param>
    /// <returns>
    ///     The parsed <see cref="QueryOptions"/> (or <see langword="null"/> when no verb token was
    ///     supplied and <paramref name="helpRequested"/> is <see langword="true"/>), the file glob
    ///     patterns supplied as positional arguments, the <c>--output</c> file path (or
    ///     <see langword="null"/> when not supplied), and whether <c>--strict-unresolved</c> was
    ///     supplied.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when <c>--output</c> is supplied with no value, or Core's parser rejects the
    ///     remaining tokens (see <see cref="QueryArgumentParser.Parse"/>'s own exceptions).
    /// </exception>
    public static (QueryOptions? Options, IReadOnlyList<string> Files, string? Output, bool StrictUnresolved) Parse(
        IReadOnlyList<string> commandArgs, bool helpRequested)
    {
        string? output = null;
        var strictUnresolved = false;
        var remaining = new List<string>(commandArgs.Count);

        var index = 0;
        while (index < commandArgs.Count)
        {
            var arg = commandArgs[index++];
            if (arg == "--output")
            {
                output = CliArgumentHelpers.GetRequiredStringArgument(
                    arg, commandArgs, ref index, "an output file path argument");
                continue;
            }

            if (arg == "--strict-unresolved")
            {
                strictUnresolved = true;
                continue;
            }

            remaining.Add(arg);
        }

        var (options, files) = QueryArgumentParser.Parse(remaining, helpRequested);
        return (options, files, output, strictUnresolved);
    }
}
