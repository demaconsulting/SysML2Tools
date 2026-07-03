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

namespace DemaConsulting.SysML2Tools.Lint;

/// <summary>
///     Parses the arguments remaining after the <c>lint</c> command token into a
///     <see cref="LintOptions"/> instance.
/// </summary>
/// <remarks>
///     <c>lint</c> recognizes no flags of its own. Any <c>-</c>-prefixed token is rejected so that
///     flags belonging to other commands (e.g., <c>--auto</c>, <c>--kind</c>) are never silently
///     accepted; every other token is treated as a file glob pattern.
/// </remarks>
internal static class LintArgumentParser
{
    /// <summary>
    ///     Parses the <c>lint</c> command's arguments.
    /// </summary>
    /// <param name="commandArgs">
    ///     The arguments remaining after the global parser has stripped cross-cutting flags and
    ///     the <c>lint</c> command token.
    /// </param>
    /// <returns>The parsed <see cref="LintOptions"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when an unrecognized flag is supplied.</exception>
    public static LintOptions Parse(IReadOnlyList<string> commandArgs)
    {
        var files = new List<string>();

        foreach (var arg in commandArgs)
        {
            if (arg.StartsWith("-", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unsupported argument '{arg}' for the 'lint' command.", nameof(commandArgs));
            }

            files.Add(arg);
        }

        return new LintOptions { Files = files };
    }
}
