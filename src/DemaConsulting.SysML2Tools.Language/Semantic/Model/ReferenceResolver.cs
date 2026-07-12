// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using DemaConsulting.SysML2Tools.Parser;

namespace DemaConsulting.SysML2Tools.Semantic.Model;

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
    /// <remarks>
    ///     Resolution runs in three passes over <paramref name="fileRoots"/>:
    ///     <list type="number">
    ///         <item>
    ///             <b>Pass 1</b> (<see cref="ResolveNode"/>) resolves every ordinary
    ///             namespace/import-scoped reference (supertype, typing, redefinition, import,
    ///             verify, satisfy, allocate, expose) for every file root. Any bare-name
    ///             <c>redefines</c>/<c>:&gt;&gt;</c> target or usage-level <c>subsets</c>/<c>:&gt;</c>
    ///             supertype name that the plain namespace/import lookup cannot resolve is
    ///             <em>not</em> warned about yet — it is instead recorded as a pending candidate,
    ///             because resolving it requires walking an ancestor's
    ///             <see cref="SysmlNode.ResolvedEdges"/>, which may not yet be populated if that
    ///             ancestor has not been visited yet by this same single top-to-bottom,
    ///             document-order DFS (declared later in the same file, or in a file processed
    ///             later in <paramref name="fileRoots"/>).
    ///         </item>
    ///         <item>
    ///             <b>Pass 2</b> (<see cref="ResolveBareRedefinitions"/>) resolves every pending
    ///             candidate collected by pass 1, once, after pass 1 has completed for
    ///             <em>every</em> file root — so every ancestor's <c>Supertype</c>/
    ///             <c>Redefinition</c> edges from ordinary pass 1 resolution are already attached,
    ///             regardless of declaration order. This mirrors the existing feature-chain
    ///             precedent below exactly: a single extra pass after pass 1 is sufficient because
    ///             the only edges this walk depends on are the ones pass 1 always attaches via
    ///             plain <see cref="TryResolve"/>. Diagnostics for names that still cannot be
    ///             resolved are emitted here instead.
    ///         </item>
    ///         <item>
    ///             <b>Pass 3</b> (<see cref="ResolveFeatureChains"/>) resolves dotted feature
    ///             chains (connection/message endpoints, transition source/target) into
    ///             Connect/Transition edges — see that method's remarks for why it, too, must run
    ///             after pass 1 completes for all file roots. Passes 2 and 3 resolve disjoint edge
    ///             kinds and neither depends on the other's output, so their relative order does
    ///             not matter.
    ///         </item>
    ///     </list>
    /// </remarks>
    public SemanticIndex ResolveAll(IEnumerable<(string FilePath, SysmlNode? Root)> fileRoots)
    {
        // Build import graph first
        var fileRootsList = fileRoots.ToList();
        var importGraph = BuildImportGraph(fileRootsList);

        // Detect circular imports
        DetectCircularImports(importGraph);

        // Shared per-file "already warned about this name" deduplication sets, keyed by file
        // path. Promoted out of the pass-1 loop below so pass 2 (ResolveBareRedefinitions)
        // reuses the *same* set for a given file, avoiding duplicate warnings for a name that
        // pass 1 could not resolve immediately and pass 2 also fails to resolve.
        var resolvedInFileByPath = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        // Pass 1: resolve every ordinary namespace/import-scoped reference for all file roots,
        // accumulating edges. Bare-name redefinition/usage-subsetting candidates that cannot be
        // resolved immediately are collected into pendingBareRedefinitions instead of being
        // warned about right away (see the ResolveAll remarks above).
        var edges = new List<SysmlEdge>();
        var pendingBareRedefinitions = new List<PendingBareRedefinition>();
        foreach (var (filePath, root) in fileRootsList.Where(r => r.Root is not null))
        {
            var imports = CollectImportNodes(root!);
            var resolvedInFile = GetOrAddResolvedInFileSet(resolvedInFileByPath, filePath);
            ResolveNode(root!, filePath, resolvedInFile, new List<string>(), imports, edges, pendingBareRedefinitions);
        }

        // Pass 2: resolve the bare-name redefinition/usage-subsetting candidates collected by
        // pass 1, now that every file root's ordinary Supertype/Redefinition edges are attached
        // (see the ResolveAll remarks above for the full rationale).
        ResolveBareRedefinitions(pendingBareRedefinitions, resolvedInFileByPath, edges);

        // Pass 3: resolve dotted feature chains (connection/message endpoints, transition
        // source/target) into Connect/Transition edges. This must run after pass 1 has resolved
        // Typing/Supertype edges for ALL file roots, because a chain walk may depend on a
        // Typing/Supertype edge belonging to a node visited later in document order within the
        // same file, or to a node in a different file entirely (see "Feature-Chain Resolution" in
        // the design doc for the full rationale).
        foreach (var (filePath, root) in fileRootsList.Where(r => r.Root is not null))
        {
            var imports = CollectImportNodes(root!);
            ResolveFeatureChains(root!, filePath, new HashSet<string>(), new List<string>(), imports, edges);
        }

        return new SemanticIndex(edges);
    }

    /// <summary>
    ///     Gets the existing per-file "already warned about this name" deduplication set for
    ///     <paramref name="filePath"/>, or creates and registers a new empty one if none exists
    ///     yet. Shared by <see cref="ResolveNode"/> (pass 1) and
    ///     <see cref="ResolveBareRedefinitions"/> (pass 2) so a name that pass 1 could not resolve
    ///     immediately, and that pass 2 also fails to resolve, is warned about exactly once.
    /// </summary>
    private static HashSet<string> GetOrAddResolvedInFileSet(
        Dictionary<string, HashSet<string>> resolvedInFileByPath,
        string filePath)
    {
        if (!resolvedInFileByPath.TryGetValue(filePath, out var resolvedInFile))
        {
            resolvedInFile = new HashSet<string>(StringComparer.Ordinal);
            resolvedInFileByPath[filePath] = resolvedInFile;
        }

        return resolvedInFile;
    }

    /// <summary>
    ///     A bare-name <c>redefines</c>/<c>:&gt;&gt;</c> target, or usage-level
    ///     <c>subsets</c>/<c>:&gt;</c> supertype name, that pass 1's plain namespace/import-scoped
    ///     <see cref="TryResolve"/> lookup could not resolve. Recorded by <see cref="ResolveNode"/>
    ///     and resolved later by <see cref="ResolveBareRedefinitions"/>, once every file root's
    ///     ordinary edges have been attached.
    /// </summary>
    /// <param name="OwnerQualifiedName">
    ///     The qualified name of the node that owns/declares the redefining or subsetting
    ///     feature — i.e. the enclosing namespace scope in effect when the candidate was
    ///     recorded — whose ancestor chain must be walked to find <paramref name="Name"/>.
    /// </param>
    /// <param name="Name">The bare (unqualified) name to resolve.</param>
    /// <param name="Kind">The edge kind (<see cref="SysmlEdgeKind.Supertype"/> for usage-level
    ///     subsetting, <see cref="SysmlEdgeKind.Redefinition"/> for <c>redefines</c>) to record on
    ///     success.</param>
    /// <param name="SubjectQualifiedName">
    ///     The qualified name of the redefining/subsetting feature itself, used as the edge's
    ///     subject on success.
    /// </param>
    /// <param name="FilePath">Source file path used when constructing a diagnostic on failure.</param>
    private sealed record PendingBareRedefinition(
        string OwnerQualifiedName,
        string Name,
        SysmlEdgeKind Kind,
        string SubjectQualifiedName,
        string FilePath);

    /// <summary>
    ///     Resolves every bare-name redefinition/usage-subsetting candidate collected by pass 1
    ///     (<see cref="ResolveNode"/>) via the ancestor-chain walk in
    ///     <see cref="FindMemberInAncestorChain"/>. Must run once, after pass 1 has completed for
    ///     every file root, so every ancestor's own <see cref="SysmlEdgeKind.Supertype"/>/
    ///     <see cref="SysmlEdgeKind.Redefinition"/> edges — attached by pass 1's ordinary
    ///     <see cref="TryResolve"/> resolution — are populated regardless of the declaration order
    ///     of the ancestor relative to the descendant doing the walking (see the
    ///     <see cref="ResolveAll"/> remarks for the full rationale). A candidate that still cannot
    ///     be resolved here produces the "Unresolved reference" Warning diagnostic that pass 1
    ///     deferred.
    /// </summary>
    /// <param name="pending">The candidates collected by pass 1.</param>
    /// <param name="resolvedInFileByPath">
    ///     Shared per-file "already warned about this name" deduplication sets, keyed by file
    ///     path (see <see cref="GetOrAddResolvedInFileSet"/>).
    /// </param>
    /// <param name="edges">
    ///     Aggregate list of all edges resolved so far across the whole file-root traversal;
    ///     appended to by this method on success.
    /// </param>
    private void ResolveBareRedefinitions(
        List<PendingBareRedefinition> pending,
        Dictionary<string, HashSet<string>> resolvedInFileByPath,
        List<SysmlEdge> edges)
    {
        foreach (var candidate in pending)
        {
            if (TryResolveBareRedefinition(candidate.OwnerQualifiedName, candidate.Name, out var resolvedName))
            {
                var edge = new SysmlEdge(candidate.SubjectQualifiedName, resolvedName, candidate.Kind);
                edges.Add(edge);

                var subjectNode = _symbolTable.Lookup(candidate.SubjectQualifiedName);
                if (subjectNode is not null)
                {
                    subjectNode.ResolvedEdges = subjectNode.ResolvedEdges.Count > 0
                        ? [.. subjectNode.ResolvedEdges, edge]
                        : [edge];
                }

                continue;
            }

            var resolvedInFile = GetOrAddResolvedInFileSet(resolvedInFileByPath, candidate.FilePath);
            if (resolvedInFile.Add(candidate.Name))
            {
                _diagnostics.Add(new SysmlDiagnostic(
                    candidate.FilePath,
                    0, 0,
                    DiagnosticSeverity.Warning,
                    $"Unresolved reference: '{candidate.Name}'"));
            }
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
    ///     that does resolve. A bare-name <c>redefines</c>/<c>:&gt;&gt;</c> target or usage-level
    ///     <c>subsets</c>/<c>:&gt;</c> supertype name that this method's plain
    ///     namespace/import-scoped lookup cannot resolve is neither warned about nor dropped here
    ///     — it is instead appended to <paramref name="pendingBareRedefinitions"/> for
    ///     <see cref="ResolveBareRedefinitions"/> to resolve in a later pass, once every file
    ///     root's ordinary edges are attached (see the <see cref="ResolveAll"/> remarks for the
    ///     full rationale).
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
    /// <param name="pendingBareRedefinitions">
    ///     Aggregate list of bare-name redefinition/usage-subsetting candidates that could not be
    ///     resolved immediately; appended to by this method, resolved later by
    ///     <see cref="ResolveBareRedefinitions"/>.
    /// </param>
    private void ResolveNode(
        SysmlNode node,
        string filePath,
        HashSet<string> resolvedInFile,
        List<string> namespaceStack,
        IReadOnlyList<SysmlImportNode> imports,
        List<SysmlEdge> edges,
        List<PendingBareRedefinition> pendingBareRedefinitions)
    {
        var nodeEdges = new List<SysmlEdge>();

        // Resolve each supertype name using the current namespace context and file imports. For
        // a usage/feature node (SysmlFeatureNode), a SupertypeNames entry may come from a usage-
        // level `subsets`/`:>` clause — which, like a bare `redefines` reference, commonly names
        // a member the owner *inherits* (declared on a supertype/redefinition ancestor) rather
        // than one declared/imported into the same lexical scope — so such a name is deferred to
        // the bare-redefinition ancestor-chain fallback (see ResolveBareRedefinitions) rather than
        // warned about immediately here. Definition-level SupertypeNames (from `part def X :> Y`)
        // name the supertype itself directly, not an inherited member, so no fallback applies and
        // an immediate warning is correct for non-feature nodes.
        foreach (var supertypeName in node.SupertypeNames)
        {
            if (TryResolve(supertypeName, namespaceStack, imports, out var resolvedSupertype))
            {
                nodeEdges.Add(new SysmlEdge(node.QualifiedName, resolvedSupertype, SysmlEdgeKind.Supertype));
            }
            else if (node is SysmlFeatureNode)
            {
                pendingBareRedefinitions.Add(new PendingBareRedefinition(
                    string.Join("::", namespaceStack),
                    supertypeName,
                    SysmlEdgeKind.Supertype,
                    node.QualifiedName ?? string.Empty,
                    filePath));
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

        // Feature redefinition — the target referenced after 'redefines'/':>>' on a usage/feature
        // element. The standard namespace/import-scoped TryResolve is tried first (handles
        // qualified forms like "Vehicle::eng" and same-scope bare names); since the dominant
        // real-world shape for `redefines` is a bare name referencing a member the owner
        // *inherits* rather than declares/imports itself, a failure here is deferred to the
        // bare-redefinition ancestor-chain fallback (see ResolveBareRedefinitions) rather than
        // warned about immediately.
        if (node is SysmlFeatureNode { RedefinedFeatureName: { } redefined })
        {
            if (TryResolve(redefined, namespaceStack, imports, out var resolvedRedefined))
            {
                nodeEdges.Add(new SysmlEdge(node.QualifiedName, resolvedRedefined, SysmlEdgeKind.Redefinition));
            }
            else
            {
                pendingBareRedefinitions.Add(new PendingBareRedefinition(
                    string.Join("::", namespaceStack),
                    redefined,
                    SysmlEdgeKind.Redefinition,
                    node.QualifiedName ?? string.Empty,
                    filePath));
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

        // Standalone dependency statements (SysmlDependencyNode) resolve every "from" name against
        // every "to" name independently (a cross product, per the grammar's list-of-lists shape),
        // mirroring Satisfy/Allocate's single-segment TryResolve resolution. An unresolved name
        // produces one Warning diagnostic per distinct unresolved name per file (via
        // resolvedInFile, the same de-duplication used by every other block in this method); a
        // pair is only emitted as an edge when both its from-name and to-name resolve.
        if (node is SysmlDependencyNode dependency)
        {
            var resolvedFrom = new List<string>();
            foreach (var fromName in dependency.FromNames)
            {
                if (TryResolve(fromName, namespaceStack, imports, out var resolved))
                {
                    resolvedFrom.Add(resolved);
                }
                else if (resolvedInFile.Add(fromName))
                {
                    _diagnostics.Add(new SysmlDiagnostic(
                        filePath,
                        0, 0,
                        DiagnosticSeverity.Warning,
                        $"Unresolved reference: '{fromName}'"));
                }
            }

            var resolvedTo = new List<string>();
            foreach (var toName in dependency.ToNames)
            {
                if (TryResolve(toName, namespaceStack, imports, out var resolved))
                {
                    resolvedTo.Add(resolved);
                }
                else if (resolvedInFile.Add(toName))
                {
                    _diagnostics.Add(new SysmlDiagnostic(
                        filePath,
                        0, 0,
                        DiagnosticSeverity.Warning,
                        $"Unresolved reference: '{toName}'"));
                }
            }

            foreach (var from in resolvedFrom)
            {
                foreach (var to in resolvedTo)
                {
                    nodeEdges.Add(new SysmlEdge(from, to, SysmlEdgeKind.Dependency));
                }
            }
        }

        // Views resolve only GetExposedNames() (a view's expose members) into Expose edges; each
        // entry is resolved independently, and an unresolved entry produces the Warning diagnostic
        // below with no edge. RenderTargetName (a view's render member) names a rendering style or
        // format, not a content-scoping subject -- per the SysML v2 grammar it never refers to
        // model content, so ReferenceResolver never inspects it: no edge is produced and no
        // diagnostic is emitted for it, mirroring how FilterExpressionText (raw source text, not a
        // reference) is also intentionally never touched here.
        if (node is SysmlViewNode view)
        {
            foreach (var exposedName in view.GetExposedNames())
            {
                if (TryResolve(exposedName, namespaceStack, imports, out var resolvedExposed))
                {
                    nodeEdges.Add(new SysmlEdge(node.QualifiedName, resolvedExposed, SysmlEdgeKind.Expose));
                }
                else if (resolvedInFile.Add(exposedName))
                {
                    _diagnostics.Add(new SysmlDiagnostic(
                        filePath,
                        0, 0,
                        DiagnosticSeverity.Warning,
                        $"Unresolved reference: '{exposedName}'"));
                }
            }
        }

        // Metadata annotations (SysmlMetadataNode, a Children entry of the element it annotates)
        // resolve their annotating type reference (e.g. "Safety" / "Pkg::Safety") the same way
        // feature typing does. The resulting edge's source is this metadata node's own
        // (usually-null) QualifiedName — callers (Core.Filtering's FilterExpressionEvaluator)
        // read the resolved target off node.ResolvedEdges directly rather than by source-name
        // lookup, since a metadata annotation is always addressed by its owning element, not by
        // its own identity.
        if (node is SysmlMetadataNode { TypeReference.Length: > 0 } metadata)
        {
            if (TryResolve(metadata.TypeReference, namespaceStack, imports, out var resolvedType))
            {
                nodeEdges.Add(new SysmlEdge(node.QualifiedName, resolvedType, SysmlEdgeKind.MetadataType));
            }
            else if (resolvedInFile.Add(metadata.TypeReference))
            {
                _diagnostics.Add(new SysmlDiagnostic(
                    filePath,
                    0, 0,
                    DiagnosticSeverity.Warning,
                    $"Unresolved reference: '{metadata.TypeReference}'"));
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
            ResolveNode(child, filePath, resolvedInFile, namespaceStack, imports, edges, pendingBareRedefinitions);
        }

        if (pushed)
        {
            namespaceStack.RemoveAt(namespaceStack.Count - 1);
        }
    }

    /// <summary>
    ///     Second-pass traversal that resolves dotted feature chains (e.g. <c>engine.fuelPort</c>)
    ///     referenced by <see cref="SysmlConnectionNode"/> (<c>"connection"</c>/<c>"message"</c>
    ///     keyword variants only) and <see cref="SysmlTransitionNode"/> <c>Source</c>/<c>Target</c>,
    ///     emitting a <see cref="SysmlEdgeKind.Connect"/>/<see cref="SysmlEdgeKind.Transition"/>
    ///     edge only when both sides resolve. Must run after <see cref="ResolveNode"/> has
    ///     completed for all file roots, since chain walking depends on the Typing/Supertype
    ///     edges pass 1 attaches to <see cref="SysmlNode.ResolvedEdges"/>.
    /// </summary>
    /// <param name="node">The AST node to process.</param>
    /// <param name="filePath">Source file path used when constructing diagnostics.</param>
    /// <param name="resolvedInFile">
    ///     Set of unresolved names already warned about in this file, preventing duplicate
    ///     warnings for the same unresolved name within one file.
    /// </param>
    /// <param name="namespaceStack">
    ///     Mutable stack of simple name segments for the current enclosing namespace path,
    ///     maintained by this method as it recurses, mirroring <see cref="ResolveNode"/>'s own
    ///     push/pop condition exactly so segment-0 resolution scope cannot silently diverge
    ///     between the two passes.
    /// </param>
    /// <param name="imports">All import nodes collected from the current file.</param>
    /// <param name="edges">
    ///     Aggregate list of all edges resolved so far across the whole file-root traversal;
    ///     appended to by this method.
    /// </param>
    private void ResolveFeatureChains(
        SysmlNode node,
        string filePath,
        HashSet<string> resolvedInFile,
        List<string> namespaceStack,
        IReadOnlyList<SysmlImportNode> imports,
        List<SysmlEdge> edges)
    {
        var nodeEdges = new List<SysmlEdge>();

        // Connection/message endpoints (allocation endpoints are intentionally excluded — they
        // remain single-segment-only per unit-3's existing, locked-in Allocate behavior).
        if (node is SysmlConnectionNode { ConnectionKeyword: "connection" or "message" } connection)
        {
            var resolvedA = ResolveFeatureChainSide(
                connection.EndpointA, filePath, resolvedInFile, namespaceStack, imports);
            var resolvedB = ResolveFeatureChainSide(
                connection.EndpointB, filePath, resolvedInFile, namespaceStack, imports);

            if (resolvedA is not null && resolvedB is not null)
            {
                nodeEdges.Add(new SysmlEdge(resolvedA, resolvedB, SysmlEdgeKind.Connect));
            }
        }

        // Binding connector endpoints ("bind A = B;", the bindingConnectorAsUsage shape only)
        // resolve via the same dotted feature-chain walk as connection/message, for parity per
        // the corpus's frequent dotted-chain bind sides (e.g. "bind engine.fuelCmdPort=fuelCmdPort;").
        if (node is SysmlConnectionNode { ConnectionKeyword: "binding" } binding)
        {
            var resolvedA = ResolveFeatureChainSide(
                binding.EndpointA, filePath, resolvedInFile, namespaceStack, imports);
            var resolvedB = ResolveFeatureChainSide(
                binding.EndpointB, filePath, resolvedInFile, namespaceStack, imports);

            if (resolvedA is not null && resolvedB is not null)
            {
                nodeEdges.Add(new SysmlEdge(resolvedA, resolvedB, SysmlEdgeKind.Binding));
            }
        }

        // Transition source/target (an implied/omitted Source produces no edge — documented
        // limitation, since there is nothing to walk a chain from).
        if (node is SysmlTransitionNode transition)
        {
            var resolvedSource = ResolveFeatureChainSide(
                transition.Source, filePath, resolvedInFile, namespaceStack, imports);
            var resolvedTarget = ResolveFeatureChainSide(
                transition.Target, filePath, resolvedInFile, namespaceStack, imports);

            if (resolvedSource is not null && resolvedTarget is not null)
            {
                nodeEdges.Add(new SysmlEdge(resolvedSource, resolvedTarget, SysmlEdgeKind.Transition));
            }
        }

        if (nodeEdges.Count > 0)
        {
            node.ResolvedEdges = node.ResolvedEdges.Count > 0
                ? [.. node.ResolvedEdges, .. nodeEdges]
                : nodeEdges;
            edges.AddRange(nodeEdges);
        }

        // Push/pop condition mirrors ResolveNode's exactly (see the design doc's "Feature-Chain
        // Resolution" section for the ordering rationale that requires this exact match).
        var pushed = (node is SysmlPackageNode or SysmlDefinitionNode or SysmlFeatureNode) && node.Name is not null;
        if (pushed)
        {
            namespaceStack.Add(node.Name!);
        }

        foreach (var child in node.Children)
        {
            ResolveFeatureChains(child, filePath, resolvedInFile, namespaceStack, imports, edges);
        }

        if (pushed)
        {
            namespaceStack.RemoveAt(namespaceStack.Count - 1);
        }
    }

    /// <summary>
    ///     Resolves one side (endpoint/source/target) of a feature-chain reference, emitting a
    ///     Warning diagnostic (deduplicated per file) when the side is present but does not
    ///     resolve. A <see langword="null"/>/empty side (e.g. an implied transition
    ///     <c>Source</c>) is silently skipped — it is not an error, just absent.
    /// </summary>
    private string? ResolveFeatureChainSide(
        string? side,
        string filePath,
        HashSet<string> resolvedInFile,
        IReadOnlyList<string> namespaceStack,
        IReadOnlyList<SysmlImportNode> imports)
    {
        if (side is not { Length: > 0 })
        {
            return null;
        }

        if (TryResolveFeatureChain(side, namespaceStack, imports, out var resolved))
        {
            return resolved;
        }

        if (resolvedInFile.Add(side))
        {
            _diagnostics.Add(new SysmlDiagnostic(
                filePath,
                0, 0,
                DiagnosticSeverity.Warning,
                $"Unresolved reference: '{side}'"));
        }

        return null;
    }

    /// <summary>
    ///     Resolves a dotted feature chain (e.g. <c>engine.fuelPort</c>,
    ///     <c>rearAxle.leftHalfAxle.axleToWheelPort</c>) to the qualified name of its final
    ///     segment. Segment 0 is resolved via the existing <see cref="TryResolve"/> four-step
    ///     lookup (so it participates in the same scope/import resolution as any other single-name
    ///     reference); each subsequent segment is resolved relative to the previous segment's
    ///     node via <see cref="FindFeatureMember"/>. A single-segment "chain" (no <c>.</c>) is
    ///     handled by the loop simply never executing, so this method also serves as the
    ///     single-segment resolver used elsewhere.
    /// </summary>
    /// <param name="chain">The raw, possibly dotted, reference text.</param>
    /// <param name="namespaceStack">
    ///     Simple name segments of the current enclosing namespace path, used to resolve segment 0.
    /// </param>
    /// <param name="imports">All import nodes collected from the current file.</param>
    /// <param name="resolvedName">
    ///     When this method returns <see langword="true"/>, the qualified name of the chain's
    ///     final segment. When this method returns <see langword="false"/>, set to
    ///     <see cref="string.Empty"/>.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> if every segment of the chain resolves; <see langword="false"/>
    ///     otherwise.
    /// </returns>
    private bool TryResolveFeatureChain(
        string chain,
        IReadOnlyList<string> namespaceStack,
        IReadOnlyList<SysmlImportNode> imports,
        out string resolvedName)
    {
        var segments = chain.Split('.');

        if (!TryResolve(segments[0], namespaceStack, imports, out var current))
        {
            resolvedName = string.Empty;
            return false;
        }

        for (var i = 1; i < segments.Length; i++)
        {
            var currentNode = _symbolTable.Lookup(current);
            if (currentNode is null)
            {
                resolvedName = string.Empty;
                return false;
            }

            var member = FindFeatureMember(currentNode, segments[i]);
            if (member?.QualifiedName is not { Length: > 0 } memberQualifiedName)
            {
                resolvedName = string.Empty;
                return false;
            }

            current = memberQualifiedName;
        }

        resolvedName = current;
        return true;
    }

    /// <summary>
    ///     Finds a member named <paramref name="name"/> reachable from <paramref name="node"/>,
    ///     trying <paramref name="node"/>'s own direct children first (an inline nested usage or
    ///     redefinition shadows a same-named definition-level member — see
    ///     <c>2c-PartsInterconnection-MultipleDecompositions.sysml</c>'s <c>port :&gt;&gt; pe =
    ///     c1.pb</c> pattern), then falling back to the member's <see cref="SysmlEdgeKind.Typing"/>
    ///     target's own hierarchy (direct children and supertype chain) when no direct child
    ///     matches.
    /// </summary>
    private SysmlNode? FindFeatureMember(SysmlNode node, string name)
    {
        var direct = node.Children.FirstOrDefault(c => c.Name == name);
        if (direct is not null)
        {
            return direct;
        }

        var typingEdge = node.ResolvedEdges.FirstOrDefault(e => e.Kind == SysmlEdgeKind.Typing);
        if (typingEdge is null)
        {
            return null;
        }

        var typeNode = _symbolTable.Lookup(typingEdge.TargetQualifiedName);
        return typeNode is null ? null : FindMemberInTypeHierarchy(typeNode, name, new HashSet<string>());
    }

    /// <summary>
    ///     Finds a member named <paramref name="name"/> in <paramref name="typeNode"/>'s own
    ///     direct children, or — recursively — in any of its <see cref="SysmlEdgeKind.Supertype"/>
    ///     ancestors' direct children, walking the supertype chain until a match is found or the
    ///     chain is exhausted. <paramref name="visited"/> guards against supertype cycles
    ///     (e.g. a malformed/adversarial <c>A :&gt; B :&gt; A</c> model), keyed on qualified type
    ///     name, so this method always terminates.
    /// </summary>
    private SysmlNode? FindMemberInTypeHierarchy(SysmlNode typeNode, string name, HashSet<string> visited)
    {
        if (typeNode.QualifiedName is { Length: > 0 } qualifiedName && !visited.Add(qualifiedName))
        {
            return null;
        }

        var direct = typeNode.Children.FirstOrDefault(c => c.Name == name);
        if (direct is not null)
        {
            return direct;
        }

        foreach (var supertypeEdge in typeNode.ResolvedEdges.Where(e => e.Kind == SysmlEdgeKind.Supertype))
        {
            var supertypeNode = _symbolTable.Lookup(supertypeEdge.TargetQualifiedName);
            if (supertypeNode is null)
            {
                continue;
            }

            var found = FindMemberInTypeHierarchy(supertypeNode, name, visited);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    ///     Attempts to resolve a bare-name <c>redefines</c>/<c>:&gt;&gt;</c> target (or
    ///     usage-level <c>subsets</c>/<c>:&gt;</c> supertype name) that <see cref="TryResolve"/>
    ///     could not resolve because the referenced member is inherited from an ancestor
    ///     (declared on a supertype, or reachable only by following the owner's own
    ///     already-resolved <see cref="SysmlEdgeKind.Redefinition"/> edge) rather than declared
    ///     or imported into the same lexical scope as the redefining/subsetting feature. This is
    ///     the dominant real-world shape for <c>redefines</c> per the SysML v2 spec — its entire
    ///     purpose is referencing an inherited member — so this fallback is tried by
    ///     <see cref="ResolveBareRedefinitions"/> whenever the standard namespace/import-scoped
    ///     <see cref="TryResolve"/> lookup fails in pass 1. Called only from pass 2, after every
    ///     file root's ordinary <see cref="SysmlEdgeKind.Supertype"/>/
    ///     <see cref="SysmlEdgeKind.Redefinition"/> edges have been attached, so the ancestor
    ///     chain walk below sees a fully-populated <see cref="SysmlNode.ResolvedEdges"/> for
    ///     every ancestor regardless of that ancestor's declaration order relative to
    ///     <paramref name="ownerQualifiedName"/>.
    /// </summary>
    /// <param name="ownerQualifiedName">
    ///     The fully-qualified name of the node that owns/declares the redefining or subsetting
    ///     feature, whose ancestor chain is walked to find <paramref name="name"/>.
    /// </param>
    /// <param name="name">The bare (unqualified) name to resolve.</param>
    /// <param name="resolvedName">
    ///     When this method returns <see langword="true"/>, the fully-qualified name of the
    ///     matched ancestor member. When it returns <see langword="false"/>, set to
    ///     <see cref="string.Empty"/>.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> if an ancestor member named <paramref name="name"/> was found;
    ///     <see langword="false"/> otherwise.
    /// </returns>
    private bool TryResolveBareRedefinition(
        string ownerQualifiedName,
        string name,
        out string resolvedName)
    {
        var ownerNode = _symbolTable.Lookup(ownerQualifiedName);
        var found = ownerNode is null ? null : FindMemberInAncestorChain(ownerNode, name, new HashSet<string>());

        if (found?.QualifiedName is not { Length: > 0 } foundQualifiedName)
        {
            resolvedName = string.Empty;
            return false;
        }

        resolvedName = foundQualifiedName;
        return true;
    }

    /// <summary>
    ///     Finds a member named <paramref name="name"/> in <paramref name="node"/>'s own direct
    ///     children, or — recursively — in any ancestor reachable by following its
    ///     <see cref="SysmlEdgeKind.Supertype"/> <em>and</em> <see cref="SysmlEdgeKind.Redefinition"/>
    ///     resolved edges. Both edge kinds must be followed (not supertype-only) because a nested
    ///     redefining feature's owner sometimes has no <c>SupertypeNames</c> of its own at all —
    ///     the only path to the inherited member is via the owner's own already-resolved
    ///     <c>Redefinition</c> edge (see <c>1c-PartsTreeRedefinition.sysml</c>'s nested
    ///     <c>frontAxle_c1 redefines frontAxle</c>, whose owner <c>frontAxleAssembly_c1</c> only
    ///     redefines <c>frontAxleAssembly</c> and has no supertype of its own).
    ///     <paramref name="visited"/> guards against cycles, keyed on qualified name, mirroring
    ///     <see cref="FindMemberInTypeHierarchy"/>'s guard pattern.
    /// </summary>
    private SysmlNode? FindMemberInAncestorChain(SysmlNode node, string name, HashSet<string> visited)
    {
        if (node.QualifiedName is { Length: > 0 } qualifiedName && !visited.Add(qualifiedName))
        {
            return null;
        }

        var direct = node.Children.FirstOrDefault(c => c.Name == name);
        if (direct is not null)
        {
            return direct;
        }

        foreach (var edge in node.ResolvedEdges.Where(
                     e => e.Kind is SysmlEdgeKind.Supertype or SysmlEdgeKind.Redefinition))
        {
            var ancestorNode = _symbolTable.Lookup(edge.TargetQualifiedName);
            if (ancestorNode is null)
            {
                continue;
            }

            var found = FindMemberInAncestorChain(ancestorNode, name, visited);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}
