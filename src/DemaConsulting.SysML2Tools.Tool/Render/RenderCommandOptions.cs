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

namespace DemaConsulting.SysML2Tools.Render;

/// <summary>
///     Immutable set of options parsed for one <c>render</c> command invocation.
/// </summary>
/// <remarks>
///     Named <c>RenderCommandOptions</c> rather than <c>RenderOptions</c> to avoid colliding with
///     <see cref="DemaConsulting.Rendering.Abstractions.RenderOptions"/>, the off-the-shelf
///     rendering-library type already used by <see cref="RenderCommand"/>.
/// </remarks>
internal sealed record RenderCommandOptions
{
    /// <summary>
    ///     Gets the output directory path for rendered diagram files, supplied via
    ///     <c>--output</c>; <see langword="null"/> means the current working directory.
    /// </summary>
    public string? OutputDirectory { get; init; }

    /// <summary>
    ///     Gets the renderer format identifier, supplied via <c>--format</c>.
    /// </summary>
    /// <remarks>
    ///     Accepted values are <c>"svg"</c> (default when <see langword="null"/>) and <c>"png"</c>;
    ///     validated by <see cref="RenderCommand.RunAsync"/> (not at parse time), matching the
    ///     <c>query</c> command's <c>--format</c> validation style. This reuses the same
    ///     <c>--format</c> flag as the <c>query</c> command, which instead accepts
    ///     <c>"markdown"</c>/<c>"json"</c>; the two commands interpret the raw string independently.
    /// </remarks>
    public string? Format { get; init; }

    /// <summary>
    ///     Gets the view name filter, supplied via <c>--view</c>; <see langword="null"/> means
    ///     render all views.
    /// </summary>
    public string? ViewName { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the <c>--auto</c> flag was specified.
    /// </summary>
    /// <remarks>
    ///     When <see langword="true"/> and the workspace has no user-defined view declarations,
    ///     the render command synthesizes a GeneralView targeting the most representative
    ///     top-level element. The flag is silently ignored when views already exist.
    /// </remarks>
    public bool AutoView { get; init; }

    /// <summary>
    ///     Gets the file glob patterns supplied as positional arguments.
    /// </summary>
    public IReadOnlyList<string> Files { get; init; } = [];
}
