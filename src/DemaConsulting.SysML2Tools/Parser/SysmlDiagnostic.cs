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
///     A single diagnostic message produced while parsing a SysML v2 file.
/// </summary>
/// <param name="FilePath">
///     The source file path (or a <c>[stdlib]…</c> virtual path for embedded library files).
/// </param>
/// <param name="Line">One-based line number within <paramref name="FilePath"/>.</param>
/// <param name="Column">Zero-based column offset within the line.</param>
/// <param name="Severity">Severity of the diagnostic.</param>
/// <param name="Message">Human-readable description of the problem.</param>
public sealed record SysmlDiagnostic(
    string FilePath,
    int Line,
    int Column,
    DiagnosticSeverity Severity,
    string Message);
