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
    ///     Runs import-graph cycle detection and supertype/typing/import reference resolution
    ///     over all file roots, building a reverse-lookup index over the resolved edges.
    /// </summary>
    /// <param name="fileRoots">The parsed file roots to resolve references within.</param>
    /// <returns>
    ///     A <see cref="SemanticIndex"/> over all resolved edges discovered while walking
    ///     <paramref name="fileRoots"/>.
    /// </returns>
    public SemanticIndex ResolveAll(IEnumerable<(string FilePath, SysmlNode? Root)> fileRoots)
    {
        // Build import graph first
        var fileRootsList = fileRoots.ToList();
        var importGraph = BuildImportGraph(fileRootsList);

        // Detect circular imports
        DetectCircularImports(importGraph);

        // Resolve references in each file using the per-file import context, accumulating edges
        var edges = new List<SysmlEdge>();
        foreach (var (filePath, root) in fileRootsList.Where(r => r.Root is not null))
        {
            var imports = CollectImportNodes(root!);
            ResolveNode(root!, filePath, new HashSet<string>(), new List<string>(), imports, edges);
        }

        return new SemanticIndex(edges);
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
    /// <param name="resolvedName">
    ///     When this method returns <see langword="true"/>, the fully-qualified name that
    ///     matched (the exact key registered in the symbol table). When this method returns
    ///     <see langword="false"/>, set to <see cref="string.Empty"/>.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> if the name resolves to a known symbol;
    ///     <see langword="false"/> otherwise.
    /// </returns>
    private bool TryResolve(
        string name,
        IReadOnlyList<string> namespaceStack,
        IReadOnlyList<SysmlImportNode> imports,
        out string resolvedName)
    {
        // Step 1: Direct lookup — handles already-qualified names
        if (_symbolTable.Contains(name))
        {
            resolvedName = name;
            return true;
        }

        // Step 2: Enclosing namespace scopes — try progressively shorter prefixes so that
        // an unqualified "Bar" inside A::B matches A::B::Bar, then A::Bar
        for (var i = namespaceStack.Count; i > 0; i--)
        {
            var prefix = string.Join("::", namespaceStack.Take(i));
            var candidate = $"{prefix}::{name}";
            if (_symbolTable.Contains(candidate))
            {
                resolvedName = candidate;
                return true;
            }
        }

        // Step 3: Wildcard imports — for each `import X::*` in the file, try X::name. X itself is
        // resolved first (direct or via enclosing scope) so that wildcard imports of a *nested*
        // namespace (e.g. `import Inner::*;` inside `package Outer { package Inner {...} }`)
        // match members of the fully-qualified "Outer::Inner", not just a top-level "Inner".
        foreach (var wildcard in imports.Where(i => i.IsWildcard))
        {
            var resolvedNamespace = ResolveNamespaceName(wildcard.ImportedNamespace, namespaceStack);
            var candidate = $"{resolvedNamespace}::{name}";
            if (_symbolTable.Contains(candidate))
            {
                resolvedName = candidate;
                return true;
            }
        }

        // Step 4: Explicit named imports — for each `import X::Y` where Y == name,
        // accept the reference if X::Y is a known symbol. X is resolved the same way as for
        // wildcard imports, so a nested-namespace explicit import also matches.
        foreach (var ns in imports.Where(i => !i.IsWildcard).Select(i => i.ImportedNamespace))
        {
            var lastSep = ns.LastIndexOf("::", StringComparison.Ordinal);
            var nsPrefix = lastSep >= 0 ? ns[..lastSep] : string.Empty;
            var lastName = lastSep >= 0 ? ns[(lastSep + 2)..] : ns;
            if (lastName != name)
            {
                continue;
            }

            var resolvedPrefix = nsPrefix.Length > 0 ? ResolveNamespaceName(nsPrefix, namespaceStack) : null;
            var candidate = resolvedPrefix is not null ? $"{resolvedPrefix}::{lastName}" : ns;
            if (_symbolTable.Contains(candidate))
            {
                resolvedName = candidate;
                return true;
            }
        }

        resolvedName = string.Empty;
        return false;
    }

    /// <summary>
    ///     Resolves a raw (possibly nested-relative) namespace name referenced by an <c>import</c>
    ///     statement to its fully-qualified form, trying a direct symbol-table lookup first and
    ///     then progressively shorter enclosing-scope prefixes (mirroring <see cref="TryResolve"/>'s
    ///     own Step 2). Falls back to the raw name unchanged when no match is found, preserving the
    ///     previous behavior for namespaces that are already fully qualified or do not resolve.
    /// </summary>
    private string ResolveNamespaceName(string ns, IReadOnlyList<string> namespaceStack)
    {
        if (_symbolTable.Contains(ns))
        {
            return ns;
        }

        for (var i = namespaceStack.Count; i > 0; i--)
        {
            var prefix = string.Join("::", namespaceStack.Take(i));
            var candidate = $"{prefix}::{ns}";
            if (_symbolTable.Contains(candidate))
            {
                return candidate;
            }
        }

        return ns;
    }

    /// <summary>
    ///     Resolves supertype, feature-typing, and import references in the given AST node and
    ///     its descendants, emitting a Warning diagnostic for each name that cannot be resolved
    ///     through the four-step lookup and recording a <see cref="SysmlEdge"/> for each name
    ///     that does resolve.
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
    /// <param name="edges">
    ///     Aggregate list of all edges resolved so far across the whole file-root traversal;
    ///     appended to by this method, mirroring <paramref name="namespaceStack"/> and
    ///     <paramref name="resolvedInFile"/>.
    /// </param>
    private void ResolveNode(
        SysmlNode node,
        string filePath,
        HashSet<string> resolvedInFile,
        List<string> namespaceStack,
        IReadOnlyList<SysmlImportNode> imports,
        List<SysmlEdge> edges)
    {
        var nodeEdges = new List<SysmlEdge>();

        // Resolve each supertype name using the current namespace context and file imports
        foreach (var supertypeName in node.SupertypeNames)
        {
            if (TryResolve(supertypeName, namespaceStack, imports, out var resolvedSupertype))
            {
                nodeEdges.Add(new SysmlEdge(node.QualifiedName, resolvedSupertype, SysmlEdgeKind.Supertype));
            }
            else if (resolvedInFile.Add(supertypeName))
            {
                _diagnostics.Add(new SysmlDiagnostic(
                    filePath,
                    0, 0,
                    DiagnosticSeverity.Warning,
                    $"Unresolved reference: '{supertypeName}'"));
            }
        }

        // Feature typing — the type referenced after ':' on a usage/feature element
        if (node is SysmlFeatureNode { FeatureTyping: { } typing })
        {
            if (TryResolve(typing, namespaceStack, imports, out var resolvedTyping))
            {
                nodeEdges.Add(new SysmlEdge(node.QualifiedName, resolvedTyping, SysmlEdgeKind.Typing));
            }
            else if (resolvedInFile.Add(typing))
            {
                _diagnostics.Add(new SysmlDiagnostic(
                    filePath,
                    0, 0,
                    DiagnosticSeverity.Warning,
                    $"Unresolved reference: '{typing}'"));
            }
        }

        // Imports — uniform with SupertypeNames, now that ImportedNames is populated by AstBuilder
        foreach (var importedName in node.ImportedNames)
        {
            if (TryResolve(importedName, namespaceStack, imports, out var resolvedImport))
            {
                nodeEdges.Add(new SysmlEdge(node.QualifiedName, resolvedImport, SysmlEdgeKind.Import));
            }
            else if (resolvedInFile.Add(importedName))
            {
                _diagnostics.Add(new SysmlDiagnostic(
                    filePath,
                    0, 0,
                    DiagnosticSeverity.Warning,
                    $"Unresolved reference: '{importedName}'"));
            }
        }

        // Verified requirement names — uniform with SupertypeNames/ImportedNames, sourced from
        // the owning def/usage node's own qualified name (the verifying case/requirement).
        foreach (var verifiedName in node.VerifiedRequirementNames)
        {
            if (TryResolve(verifiedName, namespaceStack, imports, out var resolvedVerified))
            {
                nodeEdges.Add(new SysmlEdge(node.QualifiedName, resolvedVerified, SysmlEdgeKind.Verify));
            }
            else if (resolvedInFile.Add(verifiedName))
            {
                _diagnostics.Add(new SysmlDiagnostic(
                    filePath,
                    0, 0,
                    DiagnosticSeverity.Warning,
                    $"Unresolved reference: '{verifiedName}'"));
            }
        }

        // Satisfy usages resolve two independent sides (subject and requirement); no bespoke
        // resolution logic is needed beyond TryResolve, but — unlike the uniform loops above — an
        // edge is only emitted when BOTH sides resolve (graceful degradation: partial/misleading
        // edges are never produced). Dotted feature-chain subjects (e.g. "a.b") are out of scope
        // for this unit and simply fail to resolve as a single symbol name, which is graceful.
        if (node is SysmlSatisfyNode satisfy)
        {
            string? resolvedSubject = null;
            string? resolvedRequirement = null;

            if (satisfy.SubjectName is { Length: > 0 } subjectName)
            {
                if (TryResolve(subjectName, namespaceStack, imports, out var subj))
                {
                    resolvedSubject = subj;
                }
                else if (resolvedInFile.Add(subjectName))
                {
                    _diagnostics.Add(new SysmlDiagnostic(
                        filePath,
                        0, 0,
                        DiagnosticSeverity.Warning,
                        $"Unresolved reference: '{subjectName}'"));
                }
            }

            if (satisfy.RequirementName is { Length: > 0 } requirementName)
            {
                if (TryResolve(requirementName, namespaceStack, imports, out var req))
                {
                    resolvedRequirement = req;
                }
                else if (resolvedInFile.Add(requirementName))
                {
                    _diagnostics.Add(new SysmlDiagnostic(
                        filePath,
                        0, 0,
                        DiagnosticSeverity.Warning,
                        $"Unresolved reference: '{requirementName}'"));
                }
            }

            if (resolvedSubject is not null && resolvedRequirement is not null)
            {
                nodeEdges.Add(new SysmlEdge(resolvedSubject, resolvedRequirement, SysmlEdgeKind.Satisfy));
            }
        }

        // Allocation usages (SysmlConnectionNode reused with ConnectionKeyword == "allocation")
        // resolve both endpoints independently, same graceful-degradation contract as satisfy.
        // Regular "connection"/"message" endpoints remain intentionally unresolved (out of scope).
        if (node is SysmlConnectionNode { ConnectionKeyword: "allocation" } allocation)
        {
            string? resolvedA = null;
            string? resolvedB = null;

            if (allocation.EndpointA is { Length: > 0 } endpointA)
            {
                if (TryResolve(endpointA, namespaceStack, imports, out var a))
                {
                    resolvedA = a;
                }
                else if (resolvedInFile.Add(endpointA))
                {
                    _diagnostics.Add(new SysmlDiagnostic(
                        filePath,
                        0, 0,
                        DiagnosticSeverity.Warning,
                        $"Unresolved reference: '{endpointA}'"));
                }
            }

            if (allocation.EndpointB is { Length: > 0 } endpointB)
            {
                if (TryResolve(endpointB, namespaceStack, imports, out var b))
                {
                    resolvedB = b;
                }
                else if (resolvedInFile.Add(endpointB))
                {
                    _diagnostics.Add(new SysmlDiagnostic(
                        filePath,
                        0, 0,
                        DiagnosticSeverity.Warning,
                        $"Unresolved reference: '{endpointB}'"));
                }
            }

            if (resolvedA is not null && resolvedB is not null)
            {
                nodeEdges.Add(new SysmlEdge(resolvedA, resolvedB, SysmlEdgeKind.Allocate));
            }
        }

        if (nodeEdges.Count > 0)
        {
            node.ResolvedEdges = nodeEdges;
            edges.AddRange(nodeEdges);
        }

        // Push this node's name onto the namespace stack before recursing into its children,
        // mirroring the scope that was in effect when AstBuilder computed qualified names.
        // Feature nodes (e.g. a named `part` usage) are included alongside Package/Definition
        // because AstBuilder's own QualifyName scoping (BuildUsageNode) pushes named usages onto
        // its namespace stack the same way, and real-world satisfy/verify targets are frequently
        // nested two or more Feature levels deep (e.g. a `satisfy`/named `requirement` usage
        // inside a named `part { ... }` body, as in the OMG "8-Requirements.sysml" fixture) —
        // without this, such nested references could never resolve via the enclosing-scope step.
        var pushed = (node is SysmlPackageNode or SysmlDefinitionNode or SysmlFeatureNode) && node.Name is not null;
        if (pushed)
        {
            namespaceStack.Add(node.Name!);
        }

        foreach (var child in node.Children)
        {
            ResolveNode(child, filePath, resolvedInFile, namespaceStack, imports, edges);
        }

        if (pushed)
        {
            namespaceStack.RemoveAt(namespaceStack.Count - 1);
        }
    }
}
