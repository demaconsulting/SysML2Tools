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

namespace DemaConsulting.SysML2Tools.Lint;

/// <summary>
///     Hand-written, culture-aware accessor for the strings embedded in
///     <c>Lint/LintStrings.resx</c>. See <see cref="ProgramStrings"/> for the rationale behind
///     hand-writing this class instead of relying on the Visual Studio
///     "ResXFileCodeGenerator" custom tool.
/// </summary>
/// <remarks>
///     Adding a future locale requires zero code changes: place a
///     <c>Lint/LintStrings.{culture}.resx</c> file alongside this file with the same key
///     names.
/// </remarks>
internal static class LintStrings
{
    private static readonly ResourceManager ResourceManager =
        new("DemaConsulting.SysML2Tools.Lint.LintStrings", typeof(LintStrings).Assembly);

    /// <summary>Gets the 'lint' command usage line.</summary>
    public static string Lint_Usage => ResourceManager.GetString(nameof(Lint_Usage))!;

    /// <summary>Gets the first line of the 'lint' command description.</summary>
    public static string Lint_Description1 => ResourceManager.GetString(nameof(Lint_Description1))!;

    /// <summary>Gets the second line of the 'lint' command description.</summary>
    public static string Lint_Description2 => ResourceManager.GetString(nameof(Lint_Description2))!;

    /// <summary>Gets the third line of the 'lint' command description.</summary>
    public static string Lint_Description3 => ResourceManager.GetString(nameof(Lint_Description3))!;
}
