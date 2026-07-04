// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using DemaConsulting.SysML2Tools.Parser;
using DemaConsulting.SysML2Tools.Semantic.Model;

namespace DemaConsulting.SysML2Tools.Semantic;

/// <summary>
///     Loads SysML/KerML files into a semantic workspace with symbol registration and reference resolution.
/// </summary>
/// <remarks>
///     <see cref="LoadAsync"/> is the primary entry point for consuming this library: it parses
///     all supplied files in parallel, registers every declaration in a symbol table, resolves
///     supertype/typing/import references, and walks specialization chains — returning a fully
///     resolved <see cref="SysmlWorkspace"/> plus the aggregated diagnostics from every phase.
///     <para>
///     Always seed the <c>seedSymbolTable</c> parameter with the pre-compiled standard library
///     symbol table (<c>StdlibProvider.GetSymbolTable()</c>) unless the caller has a specific
///     reason to load user files against an empty namespace; without the stdlib seed, references
///     to standard-library types (e.g. <c>ScalarValues::Real</c>) resolve to unresolved-reference
///     Warning diagnostics rather than a loaded declaration.
///     </para>
/// </remarks>
public static class WorkspaceLoader
{
    /// <summary>
    ///     Loads the given SysML/KerML source files into a semantic workspace.
    /// </summary>
    /// <param name="filePaths">
    ///     Paths to the SysML/KerML source files to load. May be empty, in which case only
    ///     seed declarations are available.
    /// </param>
    /// <param name="seedSymbolTable">
    ///     An optional pre-populated symbol table (e.g., from a pre-compiled stdlib).
    ///     When provided, the workspace is initialized with these symbols before parsing user files.
    ///     When null, the workspace starts empty.
    /// </param>
    /// <returns>
    ///     A <see cref="SysmlLoadResult"/> containing the workspace and all diagnostics.
    /// </returns>
    /// <example>
    /// <code>
    /// var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
    /// var result = await WorkspaceLoader.LoadAsync(["model.sysml"], stdlibTable);
    /// if (result.HasErrors)
    /// {
    ///     return; // inspect result.Diagnostics for details
    /// }
    ///
    /// Console.WriteLine($"Loaded {result.Workspace!.Declarations.Count} declaration(s).");
    /// </code>
    /// </example>
    public static async Task<SysmlLoadResult> LoadAsync(
        IEnumerable<string> filePaths,
        SymbolTable? seedSymbolTable = null)
    {
        var allDiagnostics = new List<SysmlDiagnostic>();
        var symbolTable = seedSymbolTable is not null
            ? new SymbolTable(seedSymbolTable.Symbols)
            : new SymbolTable();
        var loadedFiles = new List<string>();

        // Parse user files in parallel
        var paths = filePaths.ToList();
        var parseTasks = paths.Select(ParseUserFileAsync).ToList();
        var parseResults = await Task.WhenAll(parseTasks).ConfigureAwait(false);

        // Register user file ASTs
        var userAstRoots = new List<(string Path, SysmlNode? Root)>();
        foreach (var (path, root, diagnostics) in parseResults)
        {
            loadedFiles.Add(path);
            allDiagnostics.AddRange(diagnostics);
            symbolTable.RegisterAll(root);
            userAstRoots.Add((path, root));
        }

        // Run reference resolution and supertype walking on user files
        var resolver = new ReferenceResolver(symbolTable, allDiagnostics);
        var index = resolver.ResolveAll(userAstRoots);

        var supertypeWalker = new SupertypeWalker(symbolTable, allDiagnostics);
        supertypeWalker.WalkAll();

        var workspace = new SysmlWorkspace
        {
            Files = loadedFiles,
            Declarations = symbolTable.Symbols,
            StdlibNames = seedSymbolTable is not null
                ? new HashSet<string>(seedSymbolTable.Symbols.Keys, StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal),
            Index = index,
        };

        return new SysmlLoadResult(workspace, allDiagnostics);
    }

    /// <summary>
    ///     Reads and parses a single user-supplied SysML/KerML file, returning the file path,
    ///     AST root, and all collected diagnostics. File I/O failures are caught and returned
    ///     as an Error-severity diagnostic rather than propagated.
    /// </summary>
    private static async Task<(string Path, SysmlNode? Root, List<SysmlDiagnostic> Diagnostics)> ParseUserFileAsync(
        string filePath)
    {
        string content;
        try
        {
            content = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var diag = new SysmlDiagnostic(filePath, 0, 0, DiagnosticSeverity.Error,
                $"Failed to read file: {ex.Message}");
            return (filePath, null, [diag]);
        }

        var diagnostics = new List<SysmlDiagnostic>();
        var cst = WorkspaceParser.ParseSourceToCst(filePath, content, diagnostics);
        var builder = new AstBuilder();
        var root = builder.Build(cst);
        return (filePath, root, diagnostics);
    }
}
