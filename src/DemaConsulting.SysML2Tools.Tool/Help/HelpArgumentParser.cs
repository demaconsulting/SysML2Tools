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

namespace DemaConsulting.SysML2Tools.Help;

/// <summary>
///     Parses the arguments remaining after the <c>help</c> command token into a
///     <see cref="HelpOptions"/> instance.
/// </summary>
/// <remarks>
///     The <c>help</c> grammar is purely positional: an optional first token naming the target
///     command (<c>lint</c>, <c>render</c>, or <c>query</c>), followed — only when the target is
///     <c>query</c> — by an optional second token naming the verb. Unlike the other three
///     per-command parsers, <c>help</c> defines no flags of its own; any <c>-</c>-prefixed token is
///     rejected, matching the rejection convention shared by
///     <see cref="Lint.LintArgumentParser"/>/<see cref="Render.RenderArgumentParser"/>/
///     <see cref="Query.QueryArgumentParser"/>. The verb vocabulary is not duplicated here — it is
///     validated by delegating to <see cref="QueryVerbParsing.Parse"/>, reusing that method's
///     existing error message and valid-token list.
/// </remarks>
internal static class HelpArgumentParser
{
    /// <summary>
    ///     The set of recognized <c>help &lt;command&gt;</c> targets.
    /// </summary>
    private static readonly string[] ValidCommands = ["lint", "render", "query"];

    /// <summary>
    ///     Parses the <c>help</c> command's arguments.
    /// </summary>
    /// <param name="commandArgs">
    ///     The arguments remaining after the global parser has stripped cross-cutting flags and
    ///     the <c>help</c> command token.
    /// </param>
    /// <returns>The parsed <see cref="HelpOptions"/>.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when the first token is not one of <c>lint</c>/<c>render</c>/<c>query</c>; when
    ///     the target is <c>query</c> and the second token is not a recognized verb; or when an
    ///     extra or <c>-</c>-prefixed token is supplied.
    /// </exception>
    public static HelpOptions Parse(IReadOnlyList<string> commandArgs)
    {
        // Bare 'help' — top-level help, same as bare '--help'.
        if (commandArgs.Count == 0)
        {
            return new HelpOptions();
        }

        var index = 0;
        var targetCommand = commandArgs[index++];
        if (targetCommand.StartsWith("-", StringComparison.Ordinal) ||
            !ValidCommands.Contains(targetCommand))
        {
            throw new ArgumentException(
                $"help: unrecognized command '{targetCommand}'. Valid commands are: " +
                $"{string.Join(", ", ValidCommands)}.",
                nameof(commandArgs));
        }

        string? targetVerb = null;
        if (targetCommand == "query" && index < commandArgs.Count)
        {
            var verbToken = commandArgs[index++];

            // Reused validation — no duplicate verb vocabulary maintained here.
            QueryVerbParsing.Parse(verbToken);
            targetVerb = verbToken;
        }

        if (index < commandArgs.Count)
        {
            throw new ArgumentException(
                $"Unsupported argument '{commandArgs[index]}' for the 'help' command.", nameof(commandArgs));
        }

        return new HelpOptions
        {
            TargetCommand = targetCommand,
            TargetVerb = targetVerb
        };
    }
}
