// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using DemaConsulting.SysML2Tools.Parser;
using DemaConsulting.SysML2Tools.Semantic.Model;

namespace DemaConsulting.SysML2Tools.Semantic;

/// <summary>
///     Loads SysML/KerML files into a semantic workspace with symbol registration and reference resolution.
/// </summary>
/// <remarks>
///     <see cref="LoadAsync(IEnumerable{string}, SymbolTable?)"/> is the primary entry point for consuming this library: it parses
///     all supplied files in parallel, registers every declaration in a symbol table, resolves
///     supertype/typing/import references, and walks specialization chains — returning a fully
///     resolved <see cref="SysmlWorkspace"/> plus the aggregated diagnostics from every phase.
///     <para>
///     Every root-level name may be declared once per workspace: a later declaration of a name that an
///     earlier one (in canonical file order, then source order) already uses is ignored as a whole
///     and reported as a Warning, never merged into the earlier one.
///     </para>
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
    ///     The severity of an unresolved reference in a user file when the caller does not opt in
    ///     to strict mode. This is the single place the default is chosen: flipping the default
    ///     to Error means changing this constant (and replacing the command-line opt-in).
    ///     The default is Warning while resolver gaps remain (#93, #129).
    ///     The standard library compiler (StdlibGen) never reads it and always passes Warning.
    /// </summary>
    internal const DiagnosticSeverity DefaultUserUnresolvedSeverity = DiagnosticSeverity.Warning;

    /// <summary>
    ///     Loads the given SysML/KerML source files into a semantic workspace, reporting every
    ///     unresolved reference in the user files as a Warning.
    /// </summary>
    /// <remarks>
    ///     This is the original public signature, kept so that consumers compiled against earlier
    ///     releases remain binary compatible. It is equivalent to calling
    ///     <see cref="LoadAsync(IEnumerable{string}, SymbolTable?, bool)"/> with
    ///     <c>strictUnresolved: false</c>.
    /// </remarks>
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
    public static Task<SysmlLoadResult> LoadAsync(
        IEnumerable<string> filePaths,
        SymbolTable? seedSymbolTable = null) =>
        LoadAsync(filePaths, seedSymbolTable, strictUnresolved: false);

    /// <summary>
    ///     Loads the given SysML/KerML source files into a semantic workspace, choosing whether
    ///     unresolved references in the user files are reported as Warnings or Errors.
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
    /// <param name="strictUnresolved">
    ///     When <see langword="false"/> every unresolved reference in the user files is reported
    ///     as a Warning diagnostic; when <see langword="true"/> it is reported as an Error instead
    ///     (strict mode). Circular-import and cyclic-specialization diagnostics are always
    ///     Warnings. This parameter has no default so the overload never overlaps the
    ///     two-parameter overload.
    /// </param>
    /// <returns>
    ///     A <see cref="SysmlLoadResult"/> containing the workspace and all diagnostics.
    /// </returns>
    /// <example>
    /// <code>
    /// var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
    /// var result = await WorkspaceLoader.LoadAsync(["model.sysml"], stdlibTable, strictUnresolved: true);
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
        SymbolTable? seedSymbolTable,
        bool strictUnresolved)
    {
        var allDiagnostics = new List<SysmlDiagnostic>();
        var symbolTable = seedSymbolTable is not null
            ? new SymbolTable(seedSymbolTable.Symbols)
            : new SymbolTable();
        var loadedFiles = new List<string>();

        // Parse user files in parallel
        var paths = filePaths.ToList();
        var parseTasks = paths.Select(ParseUserFileAsync).ToList();
        var unorderedResults = await Task.WhenAll(parseTasks).ConfigureAwait(false);

        // Process the files in a canonical order, independent of the caller's order and of the
        // platform: when several declarations share a root-level name, the first one in this
        // order is the one the model keeps (KerML leaves which duplicate wins unspecified), so
        // the order must be stable. Within one file the source order decides. A path supplied
        // more than once is one file and is processed once.
        var parseResults = unorderedResults
            .GroupBy(result => CanonicalPathKey(result.Path), StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();

        // Register user file ASTs, leaving out every root-level declaration whose name an earlier
        // declaration already uses: it is reported and ignored as a whole, never merged.
        var userAstRoots = new List<(string Path, SysmlNode? Root)>();
        var claimedNames = new Dictionary<string, (string Path, SourcePosition Position)>(StringComparer.Ordinal);
        foreach (var (path, parsedRoot, diagnostics) in parseResults)
        {
            loadedFiles.Add(path);
            allDiagnostics.AddRange(diagnostics);
            var root = ExcludeDuplicateRootDeclarations(path, parsedRoot, claimedNames, allDiagnostics);
            symbolTable.RegisterAll(root);
            userAstRoots.Add((path, root));
        }

        // Run reference resolution and supertype walking on user files
        var stdlibNames = seedSymbolTable is not null
            ? new HashSet<string>(seedSymbolTable.Symbols.Keys, StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        var resolver = new ReferenceResolver(
            symbolTable,
            allDiagnostics,
            strictUnresolved ? DiagnosticSeverity.Error : DefaultUserUnresolvedSeverity,
            stdlibNames);
        var index = resolver.ResolveAll(userAstRoots);

        var supertypeWalker = new SupertypeWalker(symbolTable, allDiagnostics);
        supertypeWalker.WalkAll();

        var workspace = new SysmlWorkspace
        {
            Files = loadedFiles,
            Declarations = symbolTable.Symbols,
            StdlibNames = stdlibNames,
            Index = index,
        };

        return new SysmlLoadResult(workspace, allDiagnostics);
    }

    /// <summary>
    ///     Leaves out of a file's root namespace every declaration whose name an earlier
    ///     declaration already uses, reporting each as a Warning at its name.
    /// </summary>
    /// <remarks>
    ///     Every named root-level member of every user file shares the one global namespace, so a
    ///     second member with an already-used name (quotes ignored: <c>'P'</c> is <c>P</c>) is a
    ///     duplicate: a package, a definition, a usage, or an alias (by its declared name, not its
    ///     short name). A <c>namespace</c> or a named <c>comment</c> is not detected. The earlier
    ///     declaration in canonical order is used; the later one is ignored as a whole subtree — it is not merged into the earlier one, is not
    ///     registered, and nothing inside it is resolved or reported. Imports are not members and
    ///     never collide. Declarations of the standard library are not part of
    ///     <paramref name="claimed"/>, so a user declaration named like a library package is not
    ///     a duplicate.
    /// </remarks>
    /// <param name="path">The path of the file, as it appears in diagnostics.</param>
    /// <param name="root">The file's root namespace, or <see langword="null"/> when it could not be read.</param>
    /// <param name="claimed">The root names already declared by earlier files and declarations.</param>
    /// <param name="diagnostics">Receives one Warning per ignored declaration.</param>
    /// <returns>
    ///     <paramref name="root"/> itself when nothing is ignored; otherwise a root namespace holding
    ///     only the declarations that are kept.
    /// </returns>
    private static SysmlNode? ExcludeDuplicateRootDeclarations(
        string path,
        SysmlNode? root,
        Dictionary<string, (string Path, SourcePosition Position)> claimed,
        List<SysmlDiagnostic> diagnostics)
    {
        if (root is null)
        {
            return root;
        }

        var kept = new List<SysmlNode>(root.Children.Count);
        foreach (var child in root.Children)
        {
            var memberName = RootMemberName(child);
            if (child is SysmlImportNode || string.IsNullOrEmpty(memberName))
            {
                kept.Add(child);
                continue;
            }

            var key = UnquotedName(memberName);
            var position = DeclarationNamePosition(child);
            if (claimed.TryGetValue(key, out var first))
            {
                diagnostics.Add(new SysmlDiagnostic(
                    path,
                    position.Line,
                    position.Column,
                    DiagnosticSeverity.Warning,
                    $"Duplicate of other owned member name: '{key}' is also declared in " +
                    $"{first.Path}({first.Position.Line},{first.Position.Column}); " +
                    "that declaration is used and this one is ignored."));
                continue;
            }

            claimed.Add(key, (path, position));
            kept.Add(child);
        }

        return kept.Count == root.Children.Count
            ? root
            : new SysmlPackageNode
            {
                Children = kept,
                Annotations = root.Annotations,
                SourceFilePath = root.SourceFilePath,
                SourceLine = root.SourceLine,
                SourceColumn = root.SourceColumn,
            };
    }

    /// <summary>
    ///     Returns the name a root-level member declares in the global namespace, or null when it
    ///     declares none. An alias has no qualified name, so its declared name is used.
    /// </summary>
    private static string? RootMemberName(SysmlNode node) =>
        node is SysmlAliasNode ? node.Name : node.QualifiedName;

    /// <summary>
    ///     Returns where a declaration's name is written: its name token, or the start of the
    ///     declaration when the name token is unknown.
    /// </summary>
    private static SourcePosition DeclarationNamePosition(SysmlNode node) =>
        node.NamePosition != default
            ? node.NamePosition
            : new SourcePosition(node.SourceLine, node.SourceColumn);

    /// <summary>
    ///     Returns <paramref name="name"/> without one pair of enclosing single quotes, so that
    ///     <c>'P'</c> and <c>P</c> compare as the same name (KerML 8.2.2.2.1).
    /// </summary>
    private static string UnquotedName(string name) =>
        name.Length >= 2 && name[0] == '\'' && name[^1] == '\''
            ? name[1..^1]
            : name;

    /// <summary>
    ///     Returns the key that orders a user file canonically: its full path with directory
    ///     separators normalized to '/', compared ordinally, so the order is the same on every
    ///     platform. A path that cannot be made absolute is used as written.
    /// </summary>
    private static string CanonicalPathKey(string path)
    {
        try
        {
            return Path.GetFullPath(path).Replace('\\', '/');
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path.Replace('\\', '/');
        }
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
        var builder = new AstBuilder(filePath);
        var root = builder.Build(cst);
        return (filePath, root, diagnostics);
    }
}
