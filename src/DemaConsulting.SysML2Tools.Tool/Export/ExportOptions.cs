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

namespace DemaConsulting.SysML2Tools.Export;

/// <summary>
///     Immutable set of options parsed for one <c>export</c> command invocation.
/// </summary>
/// <remarks>
///     Mirrors the shape of <see cref="Render.RenderCommandOptions"/>/<see cref="Query.QueryOptions"/>:
///     a flags-plus-positional-files record populated by <see cref="ExportArgumentParser"/>.
/// </remarks>
internal sealed record ExportOptions
{
    /// <summary>
    ///     Gets the output format, supplied via <c>--format</c>.
    /// </summary>
    /// <remarks>
    ///     Accepted values are <c>"json"</c> (default when <see langword="null"/>) and
    ///     <c>"jsonl"</c>. Captured raw here and validated later by
    ///     <see cref="ExportCommand.RunAsync"/>, matching the <c>query</c>/<c>render</c> commands'
    ///     <c>--format</c> validation style.
    /// </remarks>
    public string? Format { get; init; }

    /// <summary>
    ///     Gets the output file path, supplied via <c>--output</c>.
    /// </summary>
    /// <remarks>
    ///     <b>Important:</b> unlike <see cref="Render.RenderCommandOptions.OutputDirectory"/>
    ///     (which names an output <i>directory</i> that <c>render</c> writes one file per view
    ///     into), <c>export</c>'s <c>--output</c> names a single output <i>file</i> that the
    ///     entire JSON/JSONL document is written to. The two commands intentionally reuse the
    ///     same flag name for a related-but-different meaning (both are "where does the primary
    ///     output go"); this distinction is called out in the <c>export --help</c> text and the
    ///     user guide to avoid confusion. <see langword="null"/> means write to stdout via
    ///     <see cref="Cli.Context.WriteLine"/>.
    /// </remarks>
    public string? Output { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the OMG standard library should be included in the
    ///     exported declarations/edges, supplied via <c>--include-stdlib</c>.
    /// </summary>
    /// <remarks>
    ///     Defaults to <see langword="false"/> (stdlib elements are excluded), mirroring the
    ///     <c>query</c> command's exact convention. Diagnostics are never stdlib-filtered — see
    ///     <see cref="ExportCommand"/>'s remarks.
    /// </remarks>
    public bool IncludeStdlib { get; init; }

    /// <summary>
    ///     Gets the qualified name of the element to restrict the export to, supplied via
    ///     <c>--target</c>.
    /// </summary>
    /// <remarks>
    ///     Captured raw here and resolved/validated later by <see cref="ExportCommand.RunAsync"/>
    ///     (which narrows <see cref="ExportResult.Declarations"/>/<see cref="ExportResult.Edges"/>
    ///     to the target's containment subtree, reporting a clean error when the name does not
    ///     resolve to a visible declaration). <see langword="null"/> means no target scoping is
    ///     applied — the whole (stdlib-filtered) workspace is exported, matching the pre-existing
    ///     behavior. Applied before <see cref="FilterExpression"/> — see
    ///     <see cref="ExportCommand"/>'s remarks for the composition order.
    /// </remarks>
    public string? Target { get; init; }

    /// <summary>
    ///     Gets the Phase 1 filter expression narrowing the exported declarations/edges, supplied
    ///     via <c>--filter</c>.
    /// </summary>
    /// <remarks>
    ///     Mirrors <see cref="Render.RenderCommandOptions.FilterExpression"/>'s style: captured
    ///     raw text here, passed through unchanged to
    ///     <see cref="Filtering.FilterExpressionParser"/>'s <c>Parse</c> method by
    ///     <see cref="ExportCommand.RunAsync"/> — no expression validation happens during parsing
    ///     of the command line. Unlike <c>render</c>'s <c>--filter</c> (which requires
    ///     <c>--view-type</c>/<c>--view-target</c>), this is independent of <see cref="Target"/>:
    ///     with no <see cref="Target"/>, it narrows the whole (stdlib-filtered) workspace; with a
    ///     <see cref="Target"/>, it narrows the target's already-scoped subtree further. A
    ///     parse/evaluation failure does not abort the export — it falls back to the unfiltered
    ///     (but still target-scoped, if applicable) result, with a diagnostic and console warning
    ///     — see <see cref="ExportCommand"/>'s remarks.
    /// </remarks>
    public string? FilterExpression { get; init; }

    /// <summary>
    ///     Gets the file glob patterns supplied as positional arguments.
    /// </summary>
    public IReadOnlyList<string> Files { get; init; } = [];
}
