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
///     Immutable result of parsing the truly cross-cutting command-line options — the ones that
///     apply regardless of which command (or no command) is selected — plus the raw leftover
///     arguments for the selected command's dedicated parser to interpret.
/// </summary>
/// <remarks>
///     <c>--depth</c> is deliberately included here (not scoped to <c>render</c>'s parser alone)
///     because it must remain usable with no command at all (feeding <see cref="Context.HeadingDepth"/>
///     during self-validation), in addition to feeding <c>render</c>'s diagram depth and <c>query</c>'s
///     <c>impact</c>-walk depth. See <c>docs/design/sysml2-tools-tool/cli/context.md</c> for the
///     full rationale.
/// </remarks>
internal sealed record GlobalArguments
{
    /// <summary>
    ///     Gets a value indicating whether the version flag was specified.
    /// </summary>
    public bool Version { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the help flag was specified.
    /// </summary>
    public bool Help { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the silent flag was specified.
    /// </summary>
    public bool Silent { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the validate flag was specified.
    /// </summary>
    public bool Validate { get; init; }

    /// <summary>
    ///     Gets the validation results file path.
    /// </summary>
    public string? ResultsFile { get; init; }

    /// <summary>
    ///     Gets the log file path.
    /// </summary>
    public string? LogFile { get; init; }

    /// <summary>
    ///     Gets the heading depth for markdown output; valid range 1–6, default 1.
    /// </summary>
    public int HeadingDepth { get; init; } = 1;

    /// <summary>
    ///     Gets the maximum diagram/impact-walk render depth; <see langword="null"/> means unlimited.
    /// </summary>
    public int? MaxRenderDepth { get; init; }

    /// <summary>
    ///     Gets the top-level command selected by the user.
    /// </summary>
    public SysmlCommand Command { get; init; }

    /// <summary>
    ///     Gets the remaining arguments — everything left over after stripping the recognized
    ///     global flags and the command token — in original relative order, for the selected
    ///     command's dedicated parser to interpret.
    /// </summary>
    public IReadOnlyList<string> CommandArgs { get; init; } = [];
}
