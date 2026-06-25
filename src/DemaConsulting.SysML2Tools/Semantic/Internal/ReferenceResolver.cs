// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using DemaConsulting.SysML2Tools.Parser;

namespace DemaConsulting.SysML2Tools.Semantic.Internal;

/// <summary>
///     Resolves qualified name references and import chains across all loaded files.
/// </summary>
internal sealed class ReferenceResolver
{
    /// <summary>
    ///     The symbol table used to check whether supertype names are registered.
    /// </summary>
    private readonly SymbolTable _symbolTable;

    /// <summary>
    ///     The shared diagnostics list to which Warning entries are appended.
    /// </summary>
    private readonly List<SysmlDiagnostic> _diagnostics;

    /// <summary>
    ///     Initializes a new instance of <see cref="ReferenceResolver"/> with the given symbol
    ///     table and diagnostics list.
    /// </summary>
    public ReferenceResolver(SymbolTable symbolTable, List<SysmlDiagnostic> diagnostics)
    {
        _symbolTable = symbolTable;
        _diagnostics = diagnostics;
    }

    /// <summary>
    ///     Runs import-graph cycle detection and supertype reference resolution over all file roots.
    /// </summary>
    public void ResolveAll(IEnumerable<(string FilePath, SysmlNode? Root)> fileRoots)
    {
        // Build import graph first
        var fileRootsList = fileRoots.ToList();
        var importGraph = BuildImportGraph(fileRootsList);

        // Detect circular imports
        DetectCircularImports(importGraph);

        // Resolve references in each file
        foreach (var (filePath, root) in fileRootsList.Where(r => r.Root is not null))
        {
            ResolveNode(root!, filePath, new HashSet<string>());
        }
    }

    /// <summary>
    ///     Builds an import graph mapping each file path to the set of namespace names it imports.
    /// </summary>
    private static Dictionary<string, HashSet<string>> BuildImportGraph(
        IEnumerable<(string FilePath, SysmlNode? Root)> fileRoots)
    {
        var graph = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var (filePath, root) in fileRoots.Where(r => r.Root is not null))
        {
            var imports = new HashSet<string>(StringComparer.Ordinal);
            CollectImports(root!, imports);
            graph[filePath] = imports;
        }

        return graph;
    }

    /// <summary>
    ///     Recursively collects all imported namespace names from an AST node and its descendants.
    /// </summary>
    private static void CollectImports(SysmlNode node, HashSet<string> imports)
    {
        if (node is SysmlImportNode importNode)
        {
            imports.Add(importNode.ImportedNamespace);
        }

        foreach (var child in node.Children)
        {
            CollectImports(child, imports);
        }
    }

    /// <summary>
    ///     Performs a DFS over the import graph to detect and report circular import chains.
    /// </summary>
    private void DetectCircularImports(Dictionary<string, HashSet<string>> importGraph)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var inStack = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in importGraph.Keys.Where(n => !visited.Contains(n)))
        {
            DetectCycles(node, importGraph, visited, inStack);
        }
    }

    /// <summary>
    ///     Recursive DFS helper that detects back-edges in the import graph and emits Warning
    ///     diagnostics for any cycle found.
    /// </summary>
    private void DetectCycles(
        string current,
        Dictionary<string, HashSet<string>> graph,
        HashSet<string> visited,
        HashSet<string> inStack)
    {
        visited.Add(current);
        inStack.Add(current);

        if (graph.TryGetValue(current, out var neighbors))
        {
            foreach (var neighbor in neighbors)
            {
                if (!visited.Contains(neighbor))
                {
                    DetectCycles(neighbor, graph, visited, inStack);
                }
                else if (inStack.Contains(neighbor))
                {
                    _diagnostics.Add(new SysmlDiagnostic(
                        current,
                        0, 0,
                        DiagnosticSeverity.Warning,
                        $"Circular import detected: '{current}' imports '{neighbor}'"));
                }
            }
        }

        inStack.Remove(current);
    }

    /// <summary>
    ///     Resolves supertype names in the given AST node and its descendants, emitting a Warning
    ///     for each name not found in the symbol table.
    /// </summary>
    private void ResolveNode(SysmlNode node, string filePath, HashSet<string> resolvedInFile)
    {
        // Resolve supertype names
        foreach (var supertypeName in node.SupertypeNames.Where(
                     n => !_symbolTable.Contains(n) && !resolvedInFile.Contains(n)))
        {
            resolvedInFile.Add(supertypeName);
            _diagnostics.Add(new SysmlDiagnostic(
                filePath,
                0, 0,
                DiagnosticSeverity.Warning,
                $"Unresolved reference: '{supertypeName}'"));
        }

        foreach (var child in node.Children)
        {
            ResolveNode(child, filePath, resolvedInFile);
        }
    }
}
