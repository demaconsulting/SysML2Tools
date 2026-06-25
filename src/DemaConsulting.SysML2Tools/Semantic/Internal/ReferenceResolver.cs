// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using DemaConsulting.SysML2Tools.Parser;

namespace DemaConsulting.SysML2Tools.Semantic.Internal;

/// <summary>
///     Resolves qualified name references and import chains across all loaded files.
/// </summary>
internal sealed class ReferenceResolver
{
    private readonly SymbolTable _symbolTable;
    private readonly List<SysmlDiagnostic> _diagnostics;

    public ReferenceResolver(SymbolTable symbolTable, List<SysmlDiagnostic> diagnostics)
    {
        _symbolTable = symbolTable;
        _diagnostics = diagnostics;
    }

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

    private void DetectCircularImports(Dictionary<string, HashSet<string>> importGraph)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var inStack = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in importGraph.Keys.Where(n => !visited.Contains(n)))
        {
            DetectCycles(node, importGraph, visited, inStack);
        }
    }

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
