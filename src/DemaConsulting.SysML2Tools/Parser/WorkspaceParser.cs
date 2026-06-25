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
using DemaConsulting.SysML2Tools.Parser.Antlr;
using DemaConsulting.SysML2Tools.Parser.Internal;

namespace DemaConsulting.SysML2Tools.Parser;

/// <summary>
///     Severity level of a SysML diagnostic message.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>Informational message — not a problem.</summary>
    Info,

    /// <summary>Warning — parseable but suspicious.</summary>
    Warning,

    /// <summary>Error — the file cannot be parsed or resolved correctly.</summary>
    Error
}

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
    ///     Stdlib parse result — computed once on first call, shared across all concurrent callers.
    /// </summary>
    private static readonly Lazy<Task<(IReadOnlyList<string> Files, IReadOnlyList<SysmlDiagnostic> Diagnostics)>> _stdlibTask =
        new(() => Task.Run(ParseStdlibInternal));

    /// <summary>
    ///     Parses the stdlib plus every file in <paramref name="filePaths"/> asynchronously.
    ///     User files are parsed in parallel across the thread pool.
    /// </summary>
    /// <param name="filePaths">
    ///     Absolute or relative paths to <c>.sysml</c> or <c>.kerml</c> files to include.
    ///     The OMG stdlib is always implicitly included.
    /// </param>
    /// <returns>
    ///     A <see cref="WorkspaceParseResult"/> containing all files parsed and all
    ///     diagnostics collected.
    /// </returns>
    public static async Task<WorkspaceParseResult> ParseAsync(IEnumerable<string> filePaths)
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        // Await stdlib — starts on first call; returns cached result on all subsequent calls
        var (stdlibFiles, stdlibDiagnostics) = await _stdlibTask.Value.ConfigureAwait(false);

        // Parse user files in parallel across the thread pool
        var userResults = await Task.WhenAll(
            filePaths.Select(path => Task.Run(() =>
            {
                var content = File.ReadAllText(path);
                var diagnostics = new List<SysmlDiagnostic>();
                ParseSource(path, content, diagnostics);
                return (Path: path, Diagnostics: (IReadOnlyList<SysmlDiagnostic>)diagnostics);
            }))).ConfigureAwait(false);

        var allFiles = stdlibFiles.Concat(userResults.Select(r => r.Path)).ToList();
        var allDiagnostics = stdlibDiagnostics
            .Concat(userResults.SelectMany(r => r.Diagnostics))
            .ToList();

        return new WorkspaceParseResult(allFiles, allDiagnostics);
    }

    /// <summary>
    ///     Parses SysML v2 source text from a string (used in tests and for single-file checks).
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
    ///     Parses the stdlib and returns the aggregate files and diagnostics.
    /// </summary>
    /// <remarks>
    ///     KerML files are included in the file count but any parse errors they produce are downgraded
    ///     to Warnings, since the SysML v2 grammar does not fully cover KerML-specific syntax.
    ///     KerML semantic support is handled at the semantic layer.
    /// </remarks>
    private static (IReadOnlyList<string> Files, IReadOnlyList<SysmlDiagnostic> Diagnostics) ParseStdlibInternal()
    {
        var files = new List<string>();
        var diagnostics = new List<SysmlDiagnostic>();
        foreach (var (virtualPath, content) in StdlibLoader.LoadAll())
        {
            files.Add(virtualPath);
            var fileDiagnostics = new List<SysmlDiagnostic>();
            ParseSource(virtualPath, content, fileDiagnostics);

            // KerML files may produce parse errors with the SysML v2 grammar — downgrade to Warning
            if (virtualPath.EndsWith(".kerml", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var d in fileDiagnostics)
                {
                    diagnostics.Add(d.Severity == DiagnosticSeverity.Error
                        ? d with { Severity = DiagnosticSeverity.Warning }
                        : d);
                }
            }
            else
            {
                diagnostics.AddRange(fileDiagnostics);
            }
        }

        return (files, diagnostics);
    }

    /// <summary>
    ///     Parses SysML v2 source text, appends diagnostics, and returns the CST root.
    /// </summary>
    /// <param name="filePath">Virtual file path used in diagnostics.</param>
    /// <param name="content">SysML v2 source text.</param>
    /// <param name="diagnostics">Mutable list to append parse diagnostics to.</param>
    /// <returns>The CST root <see cref="SysMLv2Parser.RootNamespaceContext"/>.</returns>
    internal static SysMLv2Parser.RootNamespaceContext ParseSourceToCst(
        string filePath, string content, List<SysmlDiagnostic> diagnostics)
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

        return parser.rootNamespace();
    }

    /// <summary>
    ///     Parses SysML v2 source text and appends any diagnostics to <paramref name="diagnostics"/>.
    /// </summary>
    private static void ParseSource(string filePath, string content, List<SysmlDiagnostic> diagnostics)
    {
        // Invoke full parse; discard the CST — only diagnostics are retained
        _ = ParseSourceToCst(filePath, content, diagnostics);
    }
}
