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

namespace DemaConsulting.SysML2Tools.Parser;

/// <summary>
///     The aggregate result of parsing a SysML v2 workspace (stdlib + user files).
/// </summary>
public sealed class WorkspaceParseResult
{
    /// <summary>
    ///     Initializes a new instance of <see cref="WorkspaceParseResult"/>.
    /// </summary>
    /// <param name="files">All files that were parsed (stdlib + user files).</param>
    /// <param name="diagnostics">All diagnostics collected across all files.</param>
    internal WorkspaceParseResult(IReadOnlyList<string> files, IReadOnlyList<SysmlDiagnostic> diagnostics)
    {
        Files = files;
        Diagnostics = diagnostics;
    }

    /// <summary>
    ///     Gets all file paths that were parsed, including stdlib virtual paths.
    /// </summary>
    public IReadOnlyList<string> Files { get; }

    /// <summary>
    ///     Gets all diagnostics collected across every parsed file.
    /// </summary>
    public IReadOnlyList<SysmlDiagnostic> Diagnostics { get; }

    /// <summary>
    ///     Gets a value indicating whether any error-level diagnostics exist.
    /// </summary>
    public bool HasErrors => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
}
