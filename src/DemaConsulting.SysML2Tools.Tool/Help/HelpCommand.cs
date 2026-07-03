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
using DemaConsulting.SysML2Tools.Lint;
using DemaConsulting.SysML2Tools.Query;
using DemaConsulting.SysML2Tools.Render;

namespace DemaConsulting.SysML2Tools.Help;

/// <summary>
///     Implements the <c>help</c> command: prints help text for the tool itself, or for a
///     specific command (and, for <c>query</c>, a specific verb).
/// </summary>
/// <remarks>
///     This class is pure dispatch — it authors no help text of its own. Every branch delegates
///     to the single source of truth for that command's help text: <see cref="Program.PrintTopLevelHelp"/>,
///     <see cref="LintCommand.PrintHelp"/>, <see cref="RenderCommand.PrintHelp"/>, or
///     <see cref="QueryCommand.PrintGeneralHelp"/>/<see cref="QueryCommand.PrintVerbHelp"/>. Those
///     same methods are also used by the command-aware <c>&lt;command&gt; --help</c> code path in
///     <see cref="Program.RunAsync"/>, so <c>help lint</c> and <c>lint --help</c> always produce
///     identical output.
/// </remarks>
internal static class HelpCommand
{
    /// <summary>
    ///     Runs the <c>help</c> command.
    /// </summary>
    /// <param name="context">
    ///     The CLI context; <see cref="Context.HelpCommand"/> supplies the parsed target
    ///     command/verb (populated only when <see cref="Context.Command"/> is
    ///     <see cref="SysmlCommand.Help"/>).
    /// </param>
    /// <exception cref="ArgumentException">
    ///     Thrown when <see cref="Context.HelpCommand"/> is <see langword="null"/> (should never
    ///     happen when <see cref="Context.Command"/> is <see cref="SysmlCommand.Help"/>, since
    ///     <see cref="Context.Create"/> always populates it for that command).
    /// </exception>
    public static void Run(Context context)
    {
        var options = context.HelpCommand
                      ?? throw new ArgumentException("help: no help options were parsed.", nameof(context));

        switch (options.TargetCommand)
        {
            case null:
                Program.PrintTopLevelHelp(context);
                break;

            case "lint":
                LintCommand.PrintHelp(context);
                break;

            case "render":
                RenderCommand.PrintHelp(context);
                break;

            case "query":
                if (options.TargetVerb is { } verbToken)
                {
                    QueryCommand.PrintVerbHelp(context, QueryVerbParsing.Parse(verbToken));
                }
                else
                {
                    QueryCommand.PrintGeneralHelp(context);
                }

                break;

            default:
                // Defensive: HelpArgumentParser only ever sets TargetCommand to one of the three
                // values handled above.
                throw new ArgumentException(
                    $"help: unrecognized command '{options.TargetCommand}'.", nameof(context));
        }
    }
}
