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

using DemaConsulting.SysML2Tools.Parser;

namespace DemaConsulting.SysML2Tools.Utilities;

/// <summary>
///     Formats a <see cref="SysmlDiagnostic"/> as a single console line, shared by every command
///     that prints diagnostics (<c>lint</c>, <c>render</c>, and <c>query</c>) so they all report
///     identically.
/// </summary>
internal static class DiagnosticFormatter
{
    /// <summary>
    ///     Formats <paramref name="diagnostic"/> as <c>path(line,col): severity: message</c>, the
    ///     compiler-style shape recognized by IDEs, CI problem matchers, and annotation tools. The
    ///     severity is its lowercase name (<c>info</c>, <c>warning</c>, or <c>error</c>). When the
    ///     diagnostic is not tied to a file (empty <see cref="SysmlDiagnostic.FilePath"/>), the
    ///     <c>path(line,col): </c> location is omitted and the result is
    ///     <c>severity: message</c>.
    /// </summary>
    /// <param name="diagnostic">The diagnostic to format.</param>
    /// <returns>The single-line text, without a trailing newline or indentation.</returns>
    /// <remarks>
    ///     Stateless and thread-safe. The line is one-based and the column is zero-based, exactly
    ///     as carried by the diagnostic.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="diagnostic"/> is <see langword="null"/>.</exception>
    internal static string Format(SysmlDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        var severity = diagnostic.Severity.ToString().ToLowerInvariant();
        return diagnostic.FilePath.Length > 0
            ? $"{diagnostic.FilePath}({diagnostic.Line},{diagnostic.Column}): {severity}: {diagnostic.Message}"
            : $"{severity}: {diagnostic.Message}";
    }
}
