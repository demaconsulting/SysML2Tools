// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using DemaConsulting.SysML2Tools.Parser;
using DemaConsulting.SysML2Tools.Semantic.Internal;

namespace DemaConsulting.SysML2Tools.Semantic;

/// <summary>
///     Loads SysML/KerML files into a semantic workspace with symbol registration and reference resolution.
/// </summary>
public static class WorkspaceLoader
{
    /// <summary>
    ///     Loads the given SysML/KerML source files into a semantic workspace.
    /// </summary>
    /// <param name="filePaths">
    ///     Paths to the SysML/KerML source files to load. May be empty, in which case only
    ///     stdlib declarations are available.
    /// </param>
    /// <returns>
    ///     A <see cref="SysmlLoadResult"/> containing the workspace and all diagnostics.
    /// </returns>
    public static async Task<SysmlLoadResult> LoadAsync(IEnumerable<string> filePaths)
    {
        var allDiagnostics = new List<SysmlDiagnostic>();
        var symbolTable = new SymbolTable();
        var loadedFiles = new List<string>();

        // Load and register stdlib (async, cached)
        var stdlibResult = await GetStdlibAstAsync().ConfigureAwait(false);
        allDiagnostics.AddRange(stdlibResult.Diagnostics);
        foreach (var (virtualPath, root) in stdlibResult.AstRoots)
        {
            loadedFiles.Add(virtualPath);
            symbolTable.RegisterAll(root);
        }

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

        // Run reference resolution and supertype walking
        var allAstRoots = stdlibResult.AstRoots
            .Select(r => (r.VirtualPath, r.Root))
            .Concat(userAstRoots.Select(r => (r.Path, r.Root)))
            .ToList();

        var resolver = new ReferenceResolver(symbolTable, allDiagnostics);
        resolver.ResolveAll(allAstRoots);

        var supertypeWalker = new SupertypeWalker(symbolTable, allDiagnostics);
        supertypeWalker.WalkAll();

        var workspace = new SysmlWorkspace
        {
            Files = loadedFiles,
            Declarations = symbolTable.Symbols.ToDictionary(
                kvp => kvp.Key,
                kvp => (object)kvp.Value,
                StringComparer.Ordinal),
        };

        return new SysmlLoadResult(workspace, allDiagnostics);
    }

    /// <summary>
    ///     Cached stdlib parse and AST build task, executed at most once per process lifetime.
    /// </summary>
    private static readonly Lazy<Task<StdlibSemanticResult>> StdlibSemanticTask =
        new(() => Task.Run(BuildStdlibSemanticAsync));

    /// <summary>
    ///     Returns the shared stdlib semantic task, starting it on first access.
    /// </summary>
    private static Task<StdlibSemanticResult> GetStdlibAstAsync() => StdlibSemanticTask.Value;

    /// <summary>
    ///     Enumerates all embedded stdlib resources, parses each into a CST, builds a typed
    ///     AST, and returns all roots and diagnostics. KerML parse errors are downgraded to
    ///     Warnings because the SysML v2 grammar does not fully cover KerML-specific syntax.
    /// </summary>
    private static async Task<StdlibSemanticResult> BuildStdlibSemanticAsync()
    {
        // Read all resource content up-front (embedded streams are not thread-safe to read in parallel)
        var assembly = typeof(WorkspaceParser).Assembly;
        var resourcePrefix = "DemaConsulting.SysML2Tools.Stdlib.";
        var resources = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(resourcePrefix, StringComparison.Ordinal))
            .Where(n => n.EndsWith(".sysml", StringComparison.OrdinalIgnoreCase) ||
                        n.EndsWith(".kerml", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var contents = new List<(string Resource, string Content)>(resources.Count);
        foreach (var resource in resources)
        {
            using var stream = assembly.GetManifestResourceStream(resource);
            if (stream is null)
            {
                continue;
            }

            using var reader = new StreamReader(stream);
            contents.Add((resource, await reader.ReadToEndAsync().ConfigureAwait(false)));
        }

        // Parse and build ASTs in parallel — each file gets its own AstBuilder (not thread-safe to share)
        var tasks = contents.Select(item => Task.Run(() =>
        {
            var fileDiagnostics = new List<SysmlDiagnostic>();
            var cst = WorkspaceParser.ParseSourceToCst(item.Resource, item.Content, fileDiagnostics);
            var root = new AstBuilder().Build(cst);

            // KerML files may produce parse errors with the SysML v2 grammar — downgrade to Warning
            if (item.Resource.EndsWith(".kerml", StringComparison.OrdinalIgnoreCase))
            {
                fileDiagnostics = fileDiagnostics
                    .Select(d => d.Severity == DiagnosticSeverity.Error ? d with { Severity = DiagnosticSeverity.Warning } : d)
                    .ToList();
            }

            return (item.Resource, Root: root, Diagnostics: fileDiagnostics);
        }));

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        var astRoots = results.Select(r => ((string VirtualPath, SysmlNode? Root))(r.Resource, r.Root)).ToList();
        var diagnostics = results.SelectMany(r => r.Diagnostics).ToList();

        return new StdlibSemanticResult(astRoots, diagnostics);
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

    /// <summary>
    ///     Internal result record holding all stdlib AST roots and collected diagnostics.
    /// </summary>
    private sealed record StdlibSemanticResult(
        IReadOnlyList<(string VirtualPath, SysmlNode? Root)> AstRoots,
        IReadOnlyList<SysmlDiagnostic> Diagnostics);
}
