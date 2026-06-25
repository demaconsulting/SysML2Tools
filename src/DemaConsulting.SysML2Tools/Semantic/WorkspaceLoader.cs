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

    // Stdlib AST cache
    private static readonly Lazy<Task<StdlibSemanticResult>> StdlibSemanticTask =
        new(() => Task.Run(BuildStdlibSemanticAsync));

    private static Task<StdlibSemanticResult> GetStdlibAstAsync() => StdlibSemanticTask.Value;

    private static async Task<StdlibSemanticResult> BuildStdlibSemanticAsync()
    {
        var diagnostics = new List<SysmlDiagnostic>();
        var astRoots = new List<(string VirtualPath, SysmlNode? Root)>();

        // Access the stdlib embedded resources
        var assembly = typeof(WorkspaceParser).Assembly;
        var resourcePrefix = "DemaConsulting.SysML2Tools.Stdlib.";
        var resources = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(resourcePrefix, StringComparison.Ordinal))
            .Where(n => n.EndsWith(".sysml", StringComparison.OrdinalIgnoreCase) ||
                        n.EndsWith(".kerml", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var builder = new AstBuilder();
        foreach (var resource in resources)
        {
            using var stream = assembly.GetManifestResourceStream(resource);
            if (stream is null)
            {
                continue;
            }

            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync().ConfigureAwait(false);

            var fileDiagnostics = new List<SysmlDiagnostic>();
            var cst = WorkspaceParser.ParseSourceToCst(resource, content, fileDiagnostics);

            // KerML files may produce parse errors with the SysML v2 grammar — downgrade to Warning
            if (resource.EndsWith(".kerml", StringComparison.OrdinalIgnoreCase))
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

            var root = builder.Build(cst);
            astRoots.Add((resource, root));
        }

        return new StdlibSemanticResult(astRoots, diagnostics);
    }

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

    private sealed record StdlibSemanticResult(
        IReadOnlyList<(string VirtualPath, SysmlNode? Root)> AstRoots,
        IReadOnlyList<SysmlDiagnostic> Diagnostics);
}
