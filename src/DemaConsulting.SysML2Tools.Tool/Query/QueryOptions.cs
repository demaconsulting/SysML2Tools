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

namespace DemaConsulting.SysML2Tools.Query;

/// <summary>
///     Immutable set of options parsed for one <c>query</c> command invocation.
/// </summary>
/// <remarks>
///     Every field is shared across all 12 <see cref="QueryVerb"/> values so that a single
///     <see cref="Cli.Context"/> parsing pass can build one instance regardless of which verb
///     was supplied; not every field is meaningful for every verb (see the per-field remarks
///     below and the verb-grammar table in
///     <c>docs/design/sysml2-tools-tool/query.md</c>).
/// </remarks>
internal sealed record QueryOptions
{
    /// <summary>
    ///     Gets the verb selecting which model-analysis operation to perform.
    /// </summary>
    public required QueryVerb Verb { get; init; }

    /// <summary>
    ///     Gets the qualified name of the target element, supplied via <c>--element</c>/<c>-e</c>.
    /// </summary>
    /// <remarks>
    ///     Required for every verb except <see cref="QueryVerb.List"/> and
    ///     <see cref="QueryVerb.Find"/>; <see langword="null"/> when not supplied.
    /// </remarks>
    public string? Element { get; init; }

    /// <summary>
    ///     Gets the output format, supplied via <c>--format</c>.
    /// </summary>
    /// <remarks>
    ///     Accepted values are <c>"markdown"</c> (default when <see langword="null"/>) and
    ///     <c>"json"</c>. This reuses the same <c>--format</c> flag as the <c>render</c> command,
    ///     which instead accepts <c>"svg"</c>/<c>"png"</c>; the two commands interpret the raw
    ///     string independently.
    /// </remarks>
    public string? Format { get; init; }

    /// <summary>
    ///     Gets the maximum impact-walk traversal depth, supplied via <c>--walk-depth</c>.
    /// </summary>
    /// <remarks>
    ///     Only meaningful for <see cref="QueryVerb.Impact"/>, where it bounds the transitive
    ///     impact walk; <see langword="null"/> means unlimited. Command-scoped (parsed locally by
    ///     <see cref="QueryArgumentParser"/>); unrelated to the <c>render</c> command's own
    ///     <c>--walk-depth</c> flag.
    /// </remarks>
    public int? WalkDepth { get; init; }

    /// <summary>
    ///     Gets the custom Markdown heading text, supplied via <c>--heading</c>.
    /// </summary>
    /// <remarks>
    ///     Markdown output only; has no effect on <c>--format json</c>. <see langword="null"/>
    ///     means the auto-generated <c>"query {verb}[: {element}]"</c> text is used instead.
    /// </remarks>
    public string? Heading { get; init; }

    /// <summary>
    ///     Gets the traversal direction, supplied via <c>--direction</c>.
    /// </summary>
    /// <remarks>
    ///     Only meaningful for <see cref="QueryVerb.Hierarchy"/>. Accepted values are
    ///     <c>"up"</c>, <c>"down"</c>, and <c>"both"</c>; <see langword="null"/> when not
    ///     supplied.
    /// </remarks>
    public string? Direction { get; init; }

    /// <summary>
    ///     Gets the element-kind filter, supplied via <c>--kind</c>.
    /// </summary>
    /// <remarks>
    ///     Only meaningful for <see cref="QueryVerb.List"/> and <see cref="QueryVerb.Find"/>;
    ///     <see langword="null"/> means no kind filtering.
    /// </remarks>
    public string? Kind { get; init; }

    /// <summary>
    ///     Gets the name substring filter, supplied via <c>--name</c>.
    /// </summary>
    /// <remarks>
    ///     Only meaningful for <see cref="QueryVerb.List"/> and <see cref="QueryVerb.Find"/>;
    ///     <see langword="null"/> means no name filtering.
    /// </remarks>
    public string? NameFilter { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the OMG standard library should be included in
    ///     results, supplied via <c>--include-stdlib</c>.
    /// </summary>
    /// <remarks>
    ///     Applies to every verb; defaults to <see langword="false"/> (stdlib elements are
    ///     excluded from results unless explicitly requested).
    /// </remarks>
    public bool IncludeStdlib { get; init; }

    /// <summary>
    ///     Gets the file glob patterns supplied as positional arguments after the verb token.
    /// </summary>
    /// <remarks>
    ///     Kept separate from <see cref="Cli.Context.Lint"/>/<see cref="Cli.Context.Render"/>'s
    ///     file lists so that query-specific file handling cannot interfere with the other
    ///     commands.
    /// </remarks>
    public IReadOnlyList<string> Files { get; init; } = [];
}
