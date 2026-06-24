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
using DemaConsulting.SysML2Tools.Parser.Internal;

namespace DemaConsulting.SysML2Tools.Parser;

/// <summary>
///     Parses one or more SysML v2 source files together with the OMG stdlib.
/// </summary>
/// <remarks>
///     Phase 1 performs syntax-only parsing (CST construction). No semantic model,
///     symbol table, or reference resolution is done at this stage.
/// </remarks>
public static class WorkspaceParser
{
    /// <summary>
    ///     Parses the stdlib plus every file matched by <paramref name="filePaths"/>.
    /// </summary>
    /// <param name="filePaths">
    ///     Absolute or relative paths to <c>.sysml</c> or <c>.kerml</c> files to include.
    ///     The OMG stdlib is always implicitly included.
    /// </param>
    /// <returns>
    ///     A <see cref="WorkspaceParseResult"/> containing all files parsed and all
    ///     diagnostics collected.
    /// </returns>
    public static WorkspaceParseResult Parse(IEnumerable<string> filePaths)
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        var allFiles = new List<string>();
        var allDiagnostics = new List<SysmlDiagnostic>();

        // Parse each stdlib file (silently — no user-visible output on success)
        foreach (var (virtualPath, content) in StdlibLoader.LoadAll())
        {
            allFiles.Add(virtualPath);
            ParseSource(virtualPath, content, allDiagnostics);
        }

        // Parse user-supplied files
        foreach (var path in filePaths)
        {
            allFiles.Add(path);
            var content = File.ReadAllText(path);
            ParseSource(path, content, allDiagnostics);
        }

        return new WorkspaceParseResult(allFiles, allDiagnostics);
    }

    /// <summary>
    ///     Parses SysML v2 source text from a string (used in tests and for stdlib loading).
    /// </summary>
    /// <param name="filePath">Virtual or real path used in diagnostic messages.</param>
    /// <param name="content">Source text to parse.</param>
    /// <returns>Diagnostics produced during parsing.</returns>
    public static IReadOnlyList<SysmlDiagnostic> ParseSource(string filePath, string content)
    {
        var diagnostics = new List<SysmlDiagnostic>();
        ParseSource(filePath, content, diagnostics);
        return diagnostics;
    }

    /// <summary>
    ///     Parses SysML v2 source text and appends any diagnostics to <paramref name="diagnostics"/>.
    /// </summary>
    private static void ParseSource(string filePath, string content, List<SysmlDiagnostic> diagnostics)
    {
        var listener = new SysmlDiagnosticListener(filePath, diagnostics);

        var inputStream = new AntlrInputStream(content);

        var lexer = new SysMLv2Lexer(inputStream);
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(listener);

        var tokenStream = new CommonTokenStream(lexer);

        var parser = new SysMLv2Parser(tokenStream);
        parser.RemoveErrorListeners();
        parser.AddErrorListener(listener);

        // Trigger parse; the CST root is discarded in Phase 1
        _ = parser.rootNamespace();
    }
}
