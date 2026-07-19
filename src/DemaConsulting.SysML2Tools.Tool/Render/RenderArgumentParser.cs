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

namespace DemaConsulting.SysML2Tools.Render;

/// <summary>
///     Parses the arguments remaining after the <c>render</c> command token into a
///     <see cref="RenderCommandOptions"/> instance.
/// </summary>
/// <remarks>
///     Recognizes only <c>--output</c>, <c>--format</c>, <c>--view</c>, <c>--auto</c>,
///     <c>--view-type</c>, <c>--view-target</c>, <c>--filter</c>, and <c>--walk-depth</c>, plus
///     positional file glob patterns. Any other <c>-</c>-prefixed token is rejected so that flags
///     belonging to other commands (e.g., <c>--kind</c>, <c>--element</c>) are never silently
///     accepted. <c>--format</c>'s value is captured raw and validated later by
///     <see cref="RenderCommand.RunAsync"/>, matching the <c>query</c> command's validation style.
///     <c>--view-type</c>, <c>--view-target</c>, and <c>--filter</c> are likewise captured raw
///     here — mutual-exclusion and value validation happen entirely in
///     <see cref="RenderCommand.RunAsync"/>. <c>--walk-depth</c> (diagram nesting depth limit) is
///     a command-scoped flag, unrelated to the global <c>--depth</c> flag (Markdown heading depth,
///     used by <c>--validate</c> and <c>query</c>).
/// </remarks>
internal static class RenderArgumentParser
{
    /// <summary>
    ///     Parses the <c>render</c> command's arguments.
    /// </summary>
    /// <param name="commandArgs">
    ///     The arguments remaining after the global parser has stripped cross-cutting flags and
    ///     the <c>render</c> command token.
    /// </param>
    /// <returns>The parsed <see cref="RenderCommandOptions"/>.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when an unrecognized flag is supplied, or a recognized flag requiring a value is
    ///     missing one.
    /// </exception>
    public static RenderCommandOptions Parse(IReadOnlyList<string> commandArgs)
    {
        string? outputDirectory = null;
        string? format = null;
        string? viewName = null;
        var autoView = false;
        string? viewType = null;
        string? viewTarget = null;
        string? filterExpression = null;
        int? walkDepth = null;
        var files = new List<string>();

        var index = 0;
        while (index < commandArgs.Count)
        {
            var arg = commandArgs[index++];
            switch (arg)
            {
                case "--output":
                    outputDirectory = CliArgumentHelpers.GetRequiredStringArgument(
                        arg, commandArgs, ref index, "an output directory argument");
                    break;

                case "--format":
                    format = CliArgumentHelpers.GetRequiredStringArgument(
                        arg, commandArgs, ref index, "a format argument (svg or png)");
                    break;

                case "--view":
                    viewName = CliArgumentHelpers.GetRequiredStringArgument(
                        arg, commandArgs, ref index, "a view name argument");
                    break;

                case "--auto":
                    autoView = true;
                    break;

                case "--view-type":
                    viewType = CliArgumentHelpers.GetRequiredStringArgument(
                        arg, commandArgs, ref index, "a view type argument (e.g. general, interconnection, state, action, sequence, grid, browser)");
                    break;

                case "--view-target":
                    viewTarget = CliArgumentHelpers.GetRequiredStringArgument(
                        arg, commandArgs, ref index, "a view target qualified-name argument");
                    break;

                case "--filter":
                    filterExpression = CliArgumentHelpers.GetRequiredStringArgument(
                        arg, commandArgs, ref index, "a filter expression argument");
                    break;

                case "--walk-depth":
                    walkDepth = CliArgumentHelpers.GetRequiredIntArgument(
                        arg, commandArgs, ref index, "a diagram nesting depth argument", 1);
                    break;

                default:
                    if (arg.StartsWith("-", StringComparison.Ordinal))
                    {
                        throw new ArgumentException(
                            $"Unsupported argument '{arg}' for the 'render' command.", nameof(commandArgs));
                    }

                    files.Add(arg);
                    break;
            }
        }

        return new RenderCommandOptions
        {
            OutputDirectory = outputDirectory,
            Format = format,
            ViewName = viewName,
            AutoView = autoView,
            ViewType = viewType,
            ViewTarget = viewTarget,
            FilterExpression = filterExpression,
            WalkDepth = walkDepth,
            Files = files
        };
    }
}
