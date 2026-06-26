// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

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
///     Parses one or more SysML v2 source files.
/// </summary>
/// <remarks>
///     Phase 1 performs syntax-only parsing (CST construction). No semantic model,
///     symbol table, or reference resolution is done at this stage.
/// </remarks>
public static class WorkspaceParser
{
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
