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

using System.Resources;

namespace DemaConsulting.SysML2Tools.Render;

/// <summary>
///     Hand-written, culture-aware accessor for the strings embedded in
///     <c>Render/RenderStrings.resx</c>. See <see cref="ProgramStrings"/> for the rationale
///     behind hand-writing this class instead of relying on the Visual Studio
///     "ResXFileCodeGenerator" custom tool.
/// </summary>
/// <remarks>
///     Adding a future locale requires zero code changes: place a
///     <c>Render/RenderStrings.{culture}.resx</c> file alongside this file with the same key
///     names.
/// </remarks>
internal static class RenderStrings
{
    private static readonly ResourceManager ResourceManager =
        new("DemaConsulting.SysML2Tools.Render.RenderStrings", typeof(RenderStrings).Assembly);

    /// <summary>Gets the 'render' command usage line.</summary>
    public static string Render_Usage => ResourceManager.GetString(nameof(Render_Usage))!;

    /// <summary>Gets the 'render' command description line.</summary>
    public static string Render_Description => ResourceManager.GetString(nameof(Render_Description))!;

    /// <summary>Gets the "Options:" header line.</summary>
    public static string Render_OptionsHeader => ResourceManager.GetString(nameof(Render_OptionsHeader))!;

    /// <summary>Gets the --output option line.</summary>
    public static string Render_OptionOutput => ResourceManager.GetString(nameof(Render_OptionOutput))!;

    /// <summary>Gets the --format option line.</summary>
    public static string Render_OptionFormat => ResourceManager.GetString(nameof(Render_OptionFormat))!;

    /// <summary>Gets the --view option line.</summary>
    public static string Render_OptionView => ResourceManager.GetString(nameof(Render_OptionView))!;

    /// <summary>Gets the --auto option line.</summary>
    public static string Render_OptionAuto => ResourceManager.GetString(nameof(Render_OptionAuto))!;

    /// <summary>Gets the --view-type option line.</summary>
    public static string Render_OptionViewType => ResourceManager.GetString(nameof(Render_OptionViewType))!;

    /// <summary>Gets the --view-target option line.</summary>
    public static string Render_OptionViewTarget => ResourceManager.GetString(nameof(Render_OptionViewTarget))!;

    /// <summary>Gets the --filter option line.</summary>
    public static string Render_OptionFilter => ResourceManager.GetString(nameof(Render_OptionFilter))!;

    /// <summary>Gets the first line of the --depth cross-reference note.</summary>
    public static string Render_DepthNote1 => ResourceManager.GetString(nameof(Render_DepthNote1))!;

    /// <summary>Gets the second line of the --depth cross-reference note.</summary>
    public static string Render_DepthNote2 => ResourceManager.GetString(nameof(Render_DepthNote2))!;
}
