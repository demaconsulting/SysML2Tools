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
///     Recognizes only <c>--output</c>, <c>--format</c>, <c>--view</c>, and <c>--auto</c>, plus
///     positional file glob patterns. Any other <c>-</c>-prefixed token is rejected so that flags
///     belonging to other commands (e.g., <c>--kind</c>, <c>--element</c>) are never silently
///     accepted. <c>--format</c>'s value is captured raw and validated later by
///     <see cref="RenderCommand.RunAsync"/>, matching the <c>query</c> command's validation style.
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
            Files = files
        };
    }
}
