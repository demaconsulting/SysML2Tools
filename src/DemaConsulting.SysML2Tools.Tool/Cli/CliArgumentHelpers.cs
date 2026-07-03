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
///     Shared value-extraction primitives used by <see cref="GlobalArgumentParser"/> and every
///     per-command argument parser. Kept intentionally minimal: option-value extraction only, not
///     command scoping or dispatch (each command owns its own recognized-flag switch).
/// </summary>
internal static class CliArgumentHelpers
{
    /// <summary>
    ///     Gets a required string argument value, advancing <paramref name="index"/> past it.
    /// </summary>
    /// <param name="arg">The flag token that requires the value (used in error messages).</param>
    /// <param name="args">All arguments in the current parsing scope.</param>
    /// <param name="index">
    ///     The index of the value to read; advanced by one on success.
    /// </param>
    /// <param name="description">Description of what's required, used in error messages.</param>
    /// <returns>The argument value.</returns>
    /// <exception cref="ArgumentException">Thrown when no value is available at <paramref name="index"/>.</exception>
    public static string GetRequiredStringArgument(
        string arg,
        IReadOnlyList<string> args,
        ref int index,
        string description)
    {
        if (index >= args.Count)
        {
            throw new ArgumentException($"{arg} requires {description}", nameof(args));
        }

        return args[index++];
    }

    /// <summary>
    ///     Gets a required integer argument value in the range [<paramref name="min"/>, <paramref name="max"/>],
    ///     advancing <paramref name="index"/> past it.
    /// </summary>
    /// <param name="arg">The flag token that requires the value (used in error messages).</param>
    /// <param name="args">All arguments in the current parsing scope.</param>
    /// <param name="index">
    ///     The index of the value to read; advanced by one on success.
    /// </param>
    /// <param name="description">Description of what's required, used in error messages.</param>
    /// <param name="min">Minimum valid value (inclusive).</param>
    /// <param name="max">Maximum valid value (inclusive).</param>
    /// <returns>The argument value as an integer in [<paramref name="min"/>, <paramref name="max"/>].</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when no value is available at <paramref name="index"/>, or the value is not an
    ///     integer within range.
    /// </exception>
    public static int GetRequiredIntArgument(
        string arg,
        IReadOnlyList<string> args,
        ref int index,
        string description,
        int min = 1,
        int max = int.MaxValue)
    {
        var value = GetRequiredStringArgument(arg, args, ref index, description);
        if (!int.TryParse(value, out var result) || result < min || result > max)
        {
            throw new ArgumentException($"{arg} requires an integer between {min} and {max} for {description}", nameof(args));
        }

        return result;
    }
}
