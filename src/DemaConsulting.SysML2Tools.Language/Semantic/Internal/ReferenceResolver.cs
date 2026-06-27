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

        // Resolve references in each file using the per-file import context
        foreach (var (filePath, root) in fileRootsList.Where(r => r.Root is not null))
        {
            var imports = CollectImportNodes(root!);
            ResolveNode(root!, filePath, new HashSet<string>(), new List<string>(), imports);
        }
    }

    /// <summary>
    ///     Builds an import graph mapping each top-level namespace name to the set of namespace
    ///     names it imports. The file-level root is a nameless container; we key on the
    ///     names of its top-level package/definition children so the DFS can follow
    ///     import edges by namespace name.
    /// </summary>
    private static Dictionary<string, HashSet<string>> BuildImportGraph(
        IEnumerable<(string FilePath, SysmlNode? Root)> fileRoots)
    {
        var graph = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var (_, root) in fileRoots.Where(r => r.Root is not null))
        {
            // The root is a nameless file-level container; top-level namespaces are its children.
            foreach (var topLevel in root!.Children
                         .Where(c => c is SysmlPackageNode or SysmlDefinitionNode)
                         .Where(c => c.QualifiedName is not null || c.Name is not null))
            {
                var key = topLevel.QualifiedName ?? topLevel.Name!;
                var imports = new HashSet<string>(StringComparer.Ordinal);
                CollectImports(topLevel, imports);
                imports.Remove(key); // avoid self-loops

                if (!graph.TryGetValue(key, out var existing))
                {
                    graph[key] = imports;
                }
                else
                {
                    foreach (var imp in imports)
                    {
                        existing.Add(imp);
                    }
                }
            }
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
    ///     Recursively collects all <see cref="SysmlImportNode"/> instances from an AST root and
    ///     its descendants, providing the per-file import context for reference resolution.
    /// </summary>
    /// <param name="root">The AST root to traverse.</param>
    /// <returns>All import nodes found anywhere in the file's AST.</returns>
    private static List<SysmlImportNode> CollectImportNodes(SysmlNode root)
    {
        var imports = new List<SysmlImportNode>();
        CollectImportNodesRecursive(root, imports);
        return imports;
    }

    /// <summary>
    ///     Recursive helper that accumulates <see cref="SysmlImportNode"/> instances into the
    ///     given list.
    /// </summary>
    private static void CollectImportNodesRecursive(SysmlNode node, List<SysmlImportNode> imports)
    {
        if (node is SysmlImportNode importNode)
        {
            imports.Add(importNode);
        }

        foreach (var child in node.Children)
        {
            CollectImportNodesRecursive(child, imports);
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
    ///     Attempts to resolve a name using a four-step lookup strategy so that unqualified
    ///     names referenced in source code match their fully-qualified counterparts in the
    ///     symbol table.
    /// </summary>
    /// <remarks>
    ///     The four steps, tried in order, are:
    ///     <list type="number">
    ///         <item>Direct lookup — handles already-qualified names such as <c>Pkg::Bar</c>.</item>
    ///         <item>
    ///             Enclosing namespace scopes — for a reference inside <c>A::B</c>, tries
    ///             <c>A::B::name</c>, then <c>A::name</c>, so same-package references resolve
    ///             without qualification.
    ///         </item>
    ///         <item>
    ///             Wildcard imports — for each <c>import X::*</c> in the file, tries
    ///             <c>X::name</c>, matching star-imported members by short name.
    ///         </item>
    ///         <item>
    ///             Explicit named imports — for each <c>import X::Y</c> where <c>Y == name</c>
    ///             and <c>X::Y</c> is in the symbol table, accepts the reference.
    ///         </item>
    ///     </list>
    /// </remarks>
    /// <param name="name">The name to resolve — may be unqualified or partially qualified.</param>
    /// <param name="namespaceStack">
    ///     Simple name segments of the current enclosing namespace path, outermost first
    ///     (e.g., <c>["A", "B"]</c> for a symbol nested inside <c>A::B</c>).
    /// </param>
    /// <param name="imports">All import nodes collected from the current file.</param>
    /// <returns>
    ///     <see langword="true"/> if the name resolves to a known symbol;
    ///     <see langword="false"/> otherwise.
    /// </returns>
    private bool TryResolve(
        string name,
        IReadOnlyList<string> namespaceStack,
        IReadOnlyList<SysmlImportNode> imports)
    {
        // Step 1: Direct lookup — handles already-qualified names
        if (_symbolTable.Contains(name))
        {
            return true;
        }

        // Step 2: Enclosing namespace scopes — try progressively shorter prefixes so that
        // an unqualified "Bar" inside A::B matches A::B::Bar, then A::Bar
        for (var i = namespaceStack.Count; i > 0; i--)
        {
            var prefix = string.Join("::", namespaceStack.Take(i));
            if (_symbolTable.Contains($"{prefix}::{name}"))
            {
                return true;
            }
        }

        // Step 3: Wildcard imports — for each `import X::*` in the file, try X::name
        if (imports.Any(i => i.IsWildcard && _symbolTable.Contains($"{i.ImportedNamespace}::{name}")))
        {
            return true;
        }

        // Step 4: Explicit named imports — for each `import X::Y` where Y == name,
        // accept the reference if X::Y is a known symbol
        foreach (var ns in imports.Where(i => !i.IsWildcard).Select(i => i.ImportedNamespace))
        {
            var lastSep = ns.LastIndexOf("::", StringComparison.Ordinal);
            var lastName = lastSep >= 0 ? ns[(lastSep + 2)..] : ns;
            if (lastName == name && _symbolTable.Contains(ns))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Resolves supertype names in the given AST node and its descendants, emitting a Warning
    ///     diagnostic for each name that cannot be resolved through the four-step lookup.
    /// </summary>
    /// <param name="node">The AST node to process.</param>
    /// <param name="filePath">Source file path used when constructing diagnostics.</param>
    /// <param name="resolvedInFile">
    ///     Set of unresolved names already warned about in this file, preventing duplicate
    ///     warnings for the same unresolved name within one file.
    /// </param>
    /// <param name="namespaceStack">
    ///     Mutable stack of simple name segments for the current enclosing namespace path,
    ///     maintained by this method as it recurses. Must be caller-owned; this method pushes
    ///     and pops entries but does not allocate the list.
    /// </param>
    /// <param name="imports">All import nodes collected from the current file.</param>
    private void ResolveNode(
        SysmlNode node,
        string filePath,
        HashSet<string> resolvedInFile,
        List<string> namespaceStack,
        IReadOnlyList<SysmlImportNode> imports)
    {
        // Resolve each supertype name using the current namespace context and file imports
        foreach (var supertypeName in node.SupertypeNames.Where(
                     n => !resolvedInFile.Contains(n) && !TryResolve(n, namespaceStack, imports)))
        {
            resolvedInFile.Add(supertypeName);
            _diagnostics.Add(new SysmlDiagnostic(
                filePath,
                0, 0,
                DiagnosticSeverity.Warning,
                $"Unresolved reference: '{supertypeName}'"));
        }

        // Push this node's name onto the namespace stack before recursing into its children,
        // mirroring the scope that was in effect when AstBuilder computed qualified names
        var pushed = (node is SysmlPackageNode or SysmlDefinitionNode) && node.Name is not null;
        if (pushed)
        {
            namespaceStack.Add(node.Name!);
        }

        foreach (var child in node.Children)
        {
            ResolveNode(child, filePath, resolvedInFile, namespaceStack, imports);
        }

        if (pushed)
        {
            namespaceStack.RemoveAt(namespaceStack.Count - 1);
        }
    }
}
