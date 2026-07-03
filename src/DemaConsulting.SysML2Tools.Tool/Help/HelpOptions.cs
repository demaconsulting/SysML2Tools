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

namespace DemaConsulting.SysML2Tools.Help;

/// <summary>
///     Immutable set of options parsed for one <c>help</c> command invocation.
/// </summary>
/// <remarks>
///     Mirrors the flat-immutable-record style of <see cref="Query.QueryOptions"/>. Both fields
///     are raw, already-validated lowercase tokens (not enums) because <c>lint</c>/<c>render</c>
///     have no enum of their own; only <c>query</c> verbs do, and those are re-validated via
///     <see cref="Query.QueryVerbParsing"/> at dispatch time in <see cref="HelpCommand"/> rather
///     than being pre-converted here.
/// </remarks>
internal sealed record HelpOptions
{
    /// <summary>
    ///     Gets the raw command token requested (<c>"lint"</c>, <c>"render"</c>, or
    ///     <c>"query"</c>), or <see langword="null"/> for a bare <c>help</c> invocation (top-level
    ///     help).
    /// </summary>
    public string? TargetCommand { get; init; }

    /// <summary>
    ///     Gets the raw query verb token requested, or <see langword="null"/> when no verb was
    ///     supplied.
    /// </summary>
    /// <remarks>
    ///     Only meaningful when <see cref="TargetCommand"/> is <c>"query"</c>; ignored otherwise.
    /// </remarks>
    public string? TargetVerb { get; init; }
}
