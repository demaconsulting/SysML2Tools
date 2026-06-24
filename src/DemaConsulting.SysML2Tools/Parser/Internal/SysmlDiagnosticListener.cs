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

using Antlr4.Runtime;
using DemaConsulting.SysML2Tools.Parser.Generated;

namespace DemaConsulting.SysML2Tools.Parser.Internal;

/// <summary>
///     ANTLR4 error listener that captures syntax errors into a shared diagnostic list.
/// </summary>
internal sealed class SysmlDiagnosticListener :
    IAntlrErrorListener<IToken>,
    IAntlrErrorListener<int>
{
    private readonly string _filePath;
    private readonly List<SysmlDiagnostic> _diagnostics;

    /// <summary>
    ///     Initializes a new listener that appends errors to <paramref name="diagnostics"/>.
    /// </summary>
    internal SysmlDiagnosticListener(string filePath, List<SysmlDiagnostic> diagnostics)
    {
        _filePath = filePath;
        _diagnostics = diagnostics;
    }

    // Parser error handler
    void IAntlrErrorListener<IToken>.SyntaxError(
        System.IO.TextWriter output,
        IRecognizer recognizer,
        IToken offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e) =>
        Append(line, charPositionInLine, msg);

    // Lexer error handler
    void IAntlrErrorListener<int>.SyntaxError(
        System.IO.TextWriter output,
        IRecognizer recognizer,
        int offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e) =>
        Append(line, charPositionInLine, msg);

    private void Append(int line, int column, string msg) =>
        _diagnostics.Add(new SysmlDiagnostic(_filePath, line, column, DiagnosticSeverity.Error, msg));
}
