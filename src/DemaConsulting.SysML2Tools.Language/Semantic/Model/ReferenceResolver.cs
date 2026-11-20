// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using DemaConsulting.SysML2Tools.Filtering;
using DemaConsulting.SysML2Tools.Parser;

namespace DemaConsulting.SysML2Tools.Semantic.Model;

/// <summary>
///     Resolves qualified name references and import chains across all loaded files.
/// </summary>
/// <remarks>
///     Every reference is resolved by <see cref="NameResolver"/> in its resolving mode, following
///     KerML 8.2.3.5 name resolution: the local namespace is the nearest enclosing
///     package/definition/usage, imports apply only to the namespace that declares them (and the
///     namespaces nested in it), qualified names are resolved through public memberships only,
///     and filtered imports admit only the elements their filter accepts. The imports written at
///     the root of a source file apply to everything in that file.
/// </remarks>
internal sealed class ReferenceResolver
{
    /// <summary>
    ///     The symbol table whose declarations references are resolved against.
    /// </summary>
    private readonly SymbolTable _symbolTable;

    /// <summary>
    ///     The shared diagnostics list to which Warning entries are appended.
    /// </summary>
    private readonly List<SysmlDiagnostic> _diagnostics;

    /// <summary>
    ///     The qualified names of the standard library declarations, used to evaluate filtered
    ///     imports' metaclass classification tests.
    /// </summary>
    private readonly IReadOnlySet<string> _stdlibNames;

    /// <summary>
    ///     The severity of every "Unresolved reference" diagnostic: Warning by default
    ///     (<c>WorkspaceLoader.DefaultUserUnresolvedSeverity</c>), Error when the caller opts in to
    ///     strict mode, and always Warning when the standard library itself is being compiled.
    /// </summary>
    private readonly DiagnosticSeverity _unresolvedSeverity;

    /// <summary>
    ///     The SysML2Tools extension <c>render</c> selectors (accepted by the diagram type router
    ///     but declared by no standard library), each mapped to the standard view definition (in
    ///     <c>StandardViewDefinitions</c>) a portable model should use instead. This is the single
    ///     copy of the list; the router in the Core library, which this library cannot reference,
    ///     recognizes the same five tokens.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> ToolRenderSelectors =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["asGeneralDiagram"] = "GeneralView",
            ["asStateTransitionDiagram"] = "StateTransitionView",
            ["asActionFlowDiagram"] = "ActionFlowView",
            ["asSequenceDiagram"] = "SequenceView",
            ["asGridDiagram"] = "GridView",
        };

    /// <summary>
    ///     The name resolver used by the current pass (replaced between pass 0 and pass 1 so the
    ///     filter-enabled instance starts with a fresh cache).
    /// </summary>
    private NameResolver _resolver;

    /// <summary>
    ///     Initializes a new instance of <see cref="ReferenceResolver"/> with the given symbol
    ///     table and diagnostics list.
    /// </summary>
    /// <param name="symbolTable">The symbol table to resolve references against.</param>
    /// <param name="diagnostics">The shared diagnostics list to append diagnostics to.</param>
    /// <param name="unresolvedSeverity">
    ///     The severity of every "Unresolved reference" diagnostic.
    /// </param>
    /// <param name="stdlibNames">
    ///     The qualified names of the standard library declarations (the seed symbol table's
    ///     keys), or <see langword="null"/> when there is no standard library.
    /// </param>
    public ReferenceResolver(
        SymbolTable symbolTable,
        List<SysmlDiagnostic> diagnostics,
        DiagnosticSeverity unresolvedSeverity,
        IReadOnlySet<string>? stdlibNames = null)
    {
        _symbolTable = symbolTable;
        _diagnostics = diagnostics;
        _unresolvedSeverity = unresolvedSeverity;
        _stdlibNames = stdlibNames ?? new HashSet<string>(StringComparer.Ordinal);
        _resolver = new NameResolver(symbolTable.Symbols, null, declaredReferencesOnly: true);
    }

    /// <summary>
    ///     A parsed source file together with the imports written at its root (outside any
    ///     package), which apply to every declaration in the file.
    /// </summary>
    private sealed record LoadedFile(string FilePath, SysmlNode Root, IReadOnlyList<SysmlImportNode> RootImports);

    /// <summary>
    ///     Where an import statement is written: its file and the position of the imported name.
    /// </summary>
    private sealed record ImportSite(string FilePath, int Line, int Column);

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
    ///             <b>Pass 0</b> (<see cref="ResolveMetadataNodes"/>) resolves every metadata
    ///             annotation's type reference first, so a filtered import's classification test
    ///             (<c>import P::*[@X];</c>) can read the resolved
    ///             <see cref="SysmlEdgeKind.MetadataType"/> edge of any candidate regardless of
    ///             where the candidate is declared. These references are resolved ignoring import
    ///             filters (a metadata type reachable only through a filtered import is resolved
    ///             as if the filter admitted it), which avoids a circular dependency between a
    ///             filter and the metadata it tests.
    ///         </item>
    ///         <item>
    ///             <b>Pass 1</b> (<see cref="ResolveNode"/>) resolves every other namespace-scoped
    ///             reference (supertype, typing, redefinition, import, verify, satisfy, allocate,
    ///             dependency, expose) for every file root, with filtered imports honored. A
    ///             redefined or subsetted member the owner inherits resolves in this single pass,
    ///             wherever its ancestor is declared, because general types are derived from the
    ///             types' raw references rather than from edges that may not be attached yet. Any
    ///             of these references may be a dotted feature chain (<c>a.b.c</c>), which
    ///             <see cref="NameResolver"/> resolves segment by segment.
    ///         </item>
    ///         <item>
    ///             <b>Pass 2</b> (<see cref="ResolveFeatureChains"/>) resolves the ends of
    ///             connection/message/binding connectors and of transitions and successions into
    ///             Connect/Binding/Transition edges. It is a separate traversal only to keep each
    ///             node's edges after its pass-1 edges — see that method's remarks.
    ///         </item>
    ///     </list>
    /// </remarks>
    public SemanticIndex ResolveAll(IEnumerable<(string FilePath, SysmlNode? Root)> fileRoots)
    {
        // Build import graph first
        var fileRootsList = fileRoots.ToList();
        var importGraph = BuildImportGraph(fileRootsList, out var importSites);

        // Detect circular imports
        DetectCircularImports(importGraph, importSites);

        var files = fileRootsList
            .Where(r => r.Root is not null)
            .Select(r => new LoadedFile(r.FilePath, r.Root!, r.Root!.Children.OfType<SysmlImportNode>().ToList()))
            .ToList();

        // Per-file "already warned about this name" deduplication sets, keyed by file path and
        // shared by every pass so a name that cannot be resolved is warned about once per file.
        var resolvedInFileByPath = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var edges = new List<SysmlEdge>();

        // Pass 0: metadata annotation types, resolved with an unfiltered resolver (see remarks).
        _resolver = CreateResolver(files, importFilter: null);
        foreach (var file in files)
        {
            ResolveMetadataNodes(
                file.Root, file, GetOrAddResolvedInFileSet(resolvedInFileByPath, file.FilePath), null, edges);
        }

        // Pass 1: every other reference, now honoring filtered imports. Without any filtered
        // import the pass-0 resolver (and its warm cache) is exactly equivalent, so it is kept.
        if (files.Any(file => ContainsFilteredImport(file.Root)))
        {
            _resolver = CreateResolver(
                files, FilterExpressionEvaluator.CreateImportFilter(_symbolTable.Symbols, _stdlibNames));
        }

        foreach (var file in files)
        {
            ResolveNode(file.Root, file, GetOrAddResolvedInFileSet(resolvedInFileByPath, file.FilePath), null, edges);
        }

        // Pass 2: resolve the ends of connection/message/binding connectors and of transitions and
        // successions into Connect/Binding/Transition edges. Chains themselves are resolved by
        // NameResolver (so they no longer depend on edges attached by pass 1); this is a separate
        // traversal only so each node's edges follow its pass-1 edges in ResolvedEdges and the
        // aggregate list, an order layouts and the recorded golden edge files rely on.
        foreach (var file in files)
        {
            ResolveFeatureChains(file.Root, file, new HashSet<string>(), null, edges);
        }

        return new SemanticIndex(edges);
    }

    /// <summary>
    ///     Creates a resolving-mode <see cref="NameResolver"/> over the symbol table and
    ///     registers each file's root imports against that file's top-level declarations.
    /// </summary>
    private NameResolver CreateResolver(IEnumerable<LoadedFile> files, Func<SysmlImportNode, string, bool>? importFilter)
    {
        var resolver = new NameResolver(_symbolTable.Symbols, importFilter, declaredReferencesOnly: true);
        foreach (var file in files.Where(file => file.RootImports.Count > 0))
        {
            // Key by declaration (not top-level name): a package may be split across files, each
            // with its own root imports. Only nodes that ARE the symbol-table entry for their
            // qualified name belong to this file's registration.
            var declarations = new List<string>();
            CollectDeclarationNames(file.Root, declarations);
            resolver.RegisterRootImports(declarations, file.RootImports);
        }

        return resolver;
    }

    /// <summary>
    ///     Collects the qualified names of the symbol-table declarations in <paramref name="node"/>'s
    ///     subtree (the nodes the symbol table holds for their qualified names).
    /// </summary>
    private void CollectDeclarationNames(SysmlNode node, List<string> names)
    {
        foreach (var child in node.Children)
        {
            if (child.QualifiedName is { Length: > 0 } qualified &&
                _symbolTable.Symbols.TryGetValue(qualified, out var symbol) &&
                ReferenceEquals(symbol, child))
            {
                names.Add(qualified);
            }

            CollectDeclarationNames(child, names);
        }
    }

    /// <summary>Returns whether any import in <paramref name="node"/>'s subtree carries a bracket filter.</summary>
    private static bool ContainsFilteredImport(SysmlNode node) =>
        node is SysmlImportNode { BracketFilterExpressionText: not null } ||
        node.Children.Any(ContainsFilteredImport);

    /// <summary>
    ///     Gets the existing per-file "already warned about this name" deduplication set for
    ///     <paramref name="filePath"/>, or creates and registers a new empty one if none exists
    ///     yet.
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
    ///     Builds an import graph mapping each top-level namespace name to the set of namespace
    ///     names it imports. The file-level root is a nameless container; we key on the
    ///     names of its top-level package/definition children so the DFS can follow
    ///     import edges by namespace name.
    /// </summary>
    private static Dictionary<string, HashSet<string>> BuildImportGraph(
        IEnumerable<(string FilePath, SysmlNode? Root)> fileRoots,
        out Dictionary<(string From, string To), ImportSite> importSites)
    {
        var graph = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        importSites = new Dictionary<(string From, string To), ImportSite>();
        foreach (var (filePath, root) in fileRoots.Where(r => r.Root is not null))
        {
            // The root is a nameless file-level container; top-level namespaces are its children.
            foreach (var topLevel in root!.Children
                         .Where(c => c is SysmlPackageNode or SysmlDefinitionNode)
                         .Where(c => c.QualifiedName is not null || c.Name is not null))
            {
                var key = topLevel.QualifiedName ?? topLevel.Name!;
                var imports = new HashSet<string>(StringComparer.Ordinal);
                var importNodes = new Dictionary<string, SysmlImportNode>(StringComparer.Ordinal);
                CollectImports(topLevel, imports, importNodes);
                imports.Remove(key); // avoid self-loops

                // Remember where each import is written so a cycle can be reported at its import.
                foreach (var (imported, importNode) in importNodes.Where(entry => imports.Contains(entry.Key)))
                {
                    var position = importNode.ReferencePositions.TryGetValue(imported, out var located)
                        ? located
                        : new SourcePosition(importNode.SourceLine, importNode.SourceColumn);
                    importSites.TryAdd((key, imported), new ImportSite(filePath, position.Line, position.Column));
                }

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
    private static void CollectImports(
        SysmlNode node,
        HashSet<string> imports,
        Dictionary<string, SysmlImportNode> importNodes)
    {
        if (node is SysmlImportNode importNode)
        {
            imports.Add(importNode.ImportedNamespace);
            importNodes.TryAdd(importNode.ImportedNamespace, importNode);
        }

        foreach (var child in node.Children)
        {
            CollectImports(child, imports, importNodes);
        }
    }

    /// <summary>
    ///     Performs a DFS over the import graph to detect and report circular import chains.
    /// </summary>
    private void DetectCircularImports(
        Dictionary<string, HashSet<string>> importGraph,
        Dictionary<(string From, string To), ImportSite> importSites)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var inStack = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in importGraph.Keys.Where(n => !visited.Contains(n)))
        {
            DetectCycles(node, importGraph, importSites, visited, inStack);
        }
    }

    /// <summary>
    ///     Recursive DFS helper that detects back-edges in the import graph and emits Warning
    ///     diagnostics for any cycle found.
    /// </summary>
    private void DetectCycles(
        string current,
        Dictionary<string, HashSet<string>> graph,
        Dictionary<(string From, string To), ImportSite> importSites,
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
                    DetectCycles(neighbor, graph, importSites, visited, inStack);
                }
                else if (inStack.Contains(neighbor))
                {
                    // Located at the import statement that closes the cycle, in its own file.
                    var site = importSites.GetValueOrDefault((current, neighbor), new ImportSite(current, 0, 0));
                    _diagnostics.Add(new SysmlDiagnostic(
                        site.FilePath,
                        site.Line,
                        site.Column,
                        DiagnosticSeverity.Warning,
                        $"Circular import detected: '{current}' imports '{neighbor}'"));
                }
            }
        }

        inStack.Remove(current);
    }

    /// <summary>
    ///     Returns the local namespace in effect for the children of <paramref name="node"/>: the
    ///     node's own qualified name when it is a named package, definition, or usage, otherwise
    ///     the namespace <paramref name="scope"/> that already applied to the node itself.
    /// </summary>
    private static string? ChildScope(SysmlNode node, string? scope)
    {
        if (node is not (SysmlPackageNode or SysmlDefinitionNode or SysmlFeatureNode))
        {
            return scope;
        }

        // An anonymous usage that owns members has a synthetic qualified name (see
        // AstBuilder.BuildUsageNode) and is the namespace of those members.
        if (node.Name is null)
        {
            return node is SysmlFeatureNode && node.QualifiedName is { Length: > 0 } anonymousScope ? anonymousScope : scope;
        }

        if (node.QualifiedName is { Length: > 0 } qualified)
        {
            return qualified;
        }

        return scope is null ? node.Name : $"{scope}::{node.Name}";
    }

    /// <summary>
    ///     Attempts to resolve a name relative to <paramref name="scope"/> by KerML name
    ///     resolution (see <see cref="NameResolver"/>), including a dotted feature chain.
    /// </summary>
    /// <param name="name">The name to resolve — may be unqualified, qualified, or a dotted feature chain.</param>
    /// <param name="scope">The qualified name of the local namespace, or null at the file root.</param>
    /// <param name="rootImports">The imports written at the root of the referencing file.</param>
    /// <param name="resolvedName">
    ///     When this method returns <see langword="true"/>, the resolved element's fully-qualified
    ///     name; otherwise <see cref="string.Empty"/>.
    /// </param>
    /// <param name="pathValued">
    ///     <see langword="true"/> to report a feature chain's instance path (the first segment's
    ///     element followed by the simple name of each later segment's element, which is how
    ///     the relationship ends drawn by the general view are keyed) rather than the declared
    ///     element of its last segment. Without a chain both are the same name.
    /// </param>
    private bool TryResolve(
        string name,
        string? scope,
        IReadOnlyList<SysmlImportNode> rootImports,
        out string resolvedName,
        bool pathValued = false)
    {
        var resolution = _resolver.ResolveFeatureChain(name, scope, rootImports);
        resolvedName = (pathValued ? resolution?.InstancePath : resolution?.Element) ?? string.Empty;
        return resolvedName.Length > 0;
    }

    /// <summary>
    ///     Resolves <paramref name="name"/> like <see cref="TryResolve"/>, emitting the
    ///     "Unresolved reference" diagnostic (once per name per file) when it does not
    ///     resolve.
    /// </summary>
    /// <param name="name">The name to resolve.</param>
    /// <param name="scope">The local namespace.</param>
    /// <param name="file">The file being resolved.</param>
    /// <param name="resolvedInFile">The names already reported as unresolved in this file.</param>
    /// <param name="owner">The node that declares the reference (for the diagnostic position).</param>
    /// <param name="pathValued">Whether a feature chain resolves to its instance path (see <see cref="TryResolve"/>).</param>
    /// <returns>The resolved qualified name, or null when the name did not resolve.</returns>
    private string? ResolveOrWarn(
        string name,
        string? scope,
        LoadedFile file,
        HashSet<string> resolvedInFile,
        SysmlNode owner,
        bool pathValued = false)
    {
        if (TryResolve(name, scope, file.RootImports, out var resolved, pathValued))
        {
            return resolved;
        }

        ReportUnresolved(name, file.FilePath, resolvedInFile, owner);
        return null;
    }

    /// <summary>
    ///     Emits the "Unresolved reference" diagnostic (at the resolver's unresolved severity) for <paramref name="name"/> unless
    ///     it was already reported for this file. The diagnostic is located at the reference text
    ///     recorded on <paramref name="owner"/> (<see cref="SysmlNode.ReferencePositions"/>), or at
    ///     the owner's own declaration when the reference text was not located.
    /// </summary>
    private void ReportUnresolved(string name, string filePath, HashSet<string> resolvedInFile, SysmlNode owner)
    {
        if (resolvedInFile.Add(name))
        {
            var (line, column) = LocateReference(owner, name);
            _diagnostics.Add(new SysmlDiagnostic(
                filePath,
                line,
                column,
                _unresolvedSeverity,
                $"Unresolved reference: '{name}'"));
        }
    }

    /// <summary>
    ///     Returns the source position of the reference text <paramref name="name"/> declared by
    ///     <paramref name="owner"/>: the located token position when known, otherwise the owner's
    ///     declaration position, otherwise <c>(0, 0)</c>.
    /// </summary>
    private static (int Line, int Column) LocateReference(SysmlNode owner, string name) =>
        owner.ReferencePositions.TryGetValue(name, out var position)
            ? (position.Line, position.Column)
            : (owner.SourceLine, owner.SourceColumn);

    /// <summary>
    ///     Pass 0 traversal: resolves each metadata annotation's type reference (e.g.
    ///     <c>Safety</c> / <c>Pkg::Safety</c>) into a <see cref="SysmlEdgeKind.MetadataType"/> edge
    ///     the same way feature typing resolves, before any filtered import is evaluated. The
    ///     edge's source is the metadata node's own (usually-null) qualified name — callers
    ///     (the filter evaluator) read the resolved target off the node's
    ///     <see cref="SysmlNode.ResolvedEdges"/> directly, since a metadata annotation is always
    ///     addressed by its owning element, not by its own identity.
    /// </summary>
    private void ResolveMetadataNodes(
        SysmlNode node,
        LoadedFile file,
        HashSet<string> resolvedInFile,
        string? scope,
        List<SysmlEdge> edges)
    {
        if (node is SysmlMetadataNode { TypeReference.Length: > 0 } metadata &&
            ResolveOrWarn(metadata.TypeReference, scope, file, resolvedInFile, node) is { } resolvedType)
        {
            var edge = new SysmlEdge(node.QualifiedName, resolvedType, SysmlEdgeKind.MetadataType)
            {
                RawReference = metadata.TypeReference,
            };
            node.ResolvedEdges = [edge];
            edges.Add(edge);
        }

        var childScope = ChildScope(node, scope);
        foreach (var child in node.Children)
        {
            ResolveMetadataNodes(child, file, resolvedInFile, childScope, edges);
        }
    }

    /// <summary>
    ///     Resolves supertype, feature-typing, redefinition, import, and requirement-trace
    ///     references in the given AST node and its descendants, emitting a Warning diagnostic for
    ///     each name that cannot be resolved and recording a <see cref="SysmlEdge"/> for each name
    ///     that does resolve. Metadata annotations were already handled by pass 0.
    /// </summary>
    /// <param name="node">The AST node to process.</param>
    /// <param name="file">The file being resolved (path for diagnostics, root imports).</param>
    /// <param name="resolvedInFile">
    ///     Set of unresolved names already warned about in this file, preventing duplicate
    ///     warnings for the same unresolved name within one file.
    /// </param>
    /// <param name="scope">
    ///     The qualified name of the local namespace in effect for <paramref name="node"/>'s own
    ///     references (its nearest enclosing named package, definition, or usage), or null at the
    ///     file root.
    /// </param>
    /// <param name="edges">
    ///     Aggregate list of all edges resolved so far across the whole file-root traversal;
    ///     appended to by this method.
    /// </param>
    private void ResolveNode(
        SysmlNode node,
        LoadedFile file,
        HashSet<string> resolvedInFile,
        string? scope,
        List<SysmlEdge> edges)
    {
        var nodeEdges = new List<SysmlEdge>();

        // Supertypes (definition `:>` / usage `subsets`/`:>`): resolved in the owning namespace,
        // which for a usage includes the memberships the owner inherits.
        foreach (var supertypeName in node.SupertypeNames)
        {
            if (ResolveOrWarn(supertypeName, scope, file, resolvedInFile, node) is { } resolvedSupertype)
            {
                nodeEdges.Add(new SysmlEdge(node.QualifiedName, resolvedSupertype, SysmlEdgeKind.Supertype) { RawReference = supertypeName });
            }
        }

        // Feature typing — the type referenced after ':' on a usage/feature element
        if (node is SysmlFeatureNode { FeatureTyping: { } typing } &&
            ResolveOrWarn(typing, scope, file, resolvedInFile, node) is { } resolvedTyping)
        {
            nodeEdges.Add(new SysmlEdge(node.QualifiedName, resolvedTyping, SysmlEdgeKind.Typing) { RawReference = typing });
        }

        // Feature redefinition — the target(s) referenced after 'redefines'/':>>' on a usage/feature
        // element, resolved against the general types of the owning type (KerML 8.2.3.5.1), with
        // a lenient fallback to ordinary resolution (see NameResolver.ResolveRedefinition). Every
        // target gets its own edge (`part :>> a :>> b`), and each unresolved one its own diagnostic.
        if (node is SysmlFeatureNode redefiningFeature)
        {
            foreach (var redefined in NameResolver.AllRedefinedFeatureNames(redefiningFeature))
            {
                var target = _resolver.ResolveRedefinition(redefined, scope ?? string.Empty, node.QualifiedName, file.RootImports);
                if (target is not null)
                {
                    nodeEdges.Add(new SysmlEdge(node.QualifiedName, target, SysmlEdgeKind.Redefinition) { RawReference = redefined });
                }
                else
                {
                    ReportUnresolved(redefined, file.FilePath, resolvedInFile, node);
                }
            }
        }

        // Imports — resolved in the namespace that declares the import
        foreach (var importedName in node.ImportedNames)
        {
            // The edge's raw reference is the reference text as written (wildcard/recursive
            // markers kept, bracket filter excluded); resolution still uses the stripped name.
            var rawReference = (node as SysmlImportNode)?.RawReferenceText ?? importedName;
            if (ResolveOrWarn(importedName, scope, file, resolvedInFile, node) is { } resolvedImport)
            {
                nodeEdges.Add(new SysmlEdge(node.QualifiedName, resolvedImport, SysmlEdgeKind.Import) { RawReference = rawReference });
            }
        }

        // Verified requirement names — sourced from the owning def/usage node's own qualified
        // name (the verifying case/requirement).
        foreach (var verifiedName in node.VerifiedRequirementNames)
        {
            if (ResolveOrWarn(verifiedName, scope, file, resolvedInFile, node, pathValued: true) is { } resolvedVerified)
            {
                nodeEdges.Add(new SysmlEdge(node.QualifiedName, resolvedVerified, SysmlEdgeKind.Verify) { RawReference = verifiedName });
            }
        }

        // Satisfy usages resolve two independent sides (subject and requirement); an edge is only
        // emitted when BOTH sides resolve (graceful degradation: partial/misleading edges are
        // never produced). Either side may be a dotted feature chain (e.g. "a.b"), recorded at its
        // instance path like the ends of connections.
        if (node is SysmlSatisfyNode satisfy)
        {
            var resolvedSubject = satisfy.SubjectName is { Length: > 0 } subjectName
                ? ResolveOrWarn(subjectName, scope, file, resolvedInFile, node, pathValued: true)
                : null;
            var resolvedRequirement = satisfy.RequirementName is { Length: > 0 } requirementName
                ? ResolveOrWarn(requirementName, scope, file, resolvedInFile, node, pathValued: true)
                : null;

            if (resolvedSubject is not null && resolvedRequirement is not null)
            {
                nodeEdges.Add(new SysmlEdge(resolvedSubject, resolvedRequirement, SysmlEdgeKind.Satisfy));
            }
        }

        // Allocation usages (SysmlConnectionNode reused with ConnectionKeyword == "allocation")
        // resolve both endpoints independently, same graceful-degradation contract as satisfy.
        // Regular "connection"/"message" endpoints are resolved as feature chains in pass 2.
        if (node is SysmlConnectionNode { ConnectionKeyword: "allocation" } allocation)
        {
            var resolvedA = allocation.EndpointA is { Length: > 0 } endpointA
                ? ResolveOrWarn(endpointA, scope, file, resolvedInFile, node, pathValued: true)
                : null;
            var resolvedB = allocation.EndpointB is { Length: > 0 } endpointB
                ? ResolveOrWarn(endpointB, scope, file, resolvedInFile, node, pathValued: true)
                : null;

            if (resolvedA is not null && resolvedB is not null)
            {
                nodeEdges.Add(new SysmlEdge(resolvedA, resolvedB, SysmlEdgeKind.Allocate));
            }
        }

        // Standalone dependency statements (SysmlDependencyNode) resolve every "from" name against
        // every "to" name independently (a cross product, per the grammar's list-of-lists shape).
        // An unresolved name produces one diagnostic per distinct unresolved name per
        // file; a pair is only emitted as an edge when both its from-name and to-name resolve.
        if (node is SysmlDependencyNode dependency)
        {
            var resolvedFrom = dependency.FromNames
                .Select(name => ResolveOrWarn(name, scope, file, resolvedInFile, node))
                .OfType<string>()
                .ToList();
            var resolvedTo = dependency.ToNames
                .Select(name => ResolveOrWarn(name, scope, file, resolvedInFile, node))
                .OfType<string>()
                .ToList();

            foreach (var from in resolvedFrom)
            {
                foreach (var to in resolvedTo)
                {
                    nodeEdges.Add(new SysmlEdge(from, to, SysmlEdgeKind.Dependency));
                }
            }
        }

        // Views resolve each ExposeMembers entry independently into an Expose edge (unresolved
        // entries produce the Warning diagnostic with no edge) -- iterating ExposeMembers
        // directly lets each resolved qualified name be paired unambiguously with the specific
        // ExposeMember object it came from, via ResolvedExposeMembers. An exposed member must be
        // visible from the view (a private member of another package is not). RenderTargetName
        // (a view's render member) names a rendering style or format, not a content-scoping
        // subject, so it produces no Expose edge; its reference is resolved separately, as a
        // ReferenceOccurrence (see ResolveReferenceOccurrences). FilterExpressionText is raw
        // source text, not a reference, so it is intentionally never touched here.
        if (node is SysmlViewNode view)
        {
            var resolvedExposeMembers = new List<(ExposeMember Member, string ResolvedQualifiedName)>();
            foreach (var member in view.ExposeMembers)
            {
                if (ResolveOrWarn(member.QualifiedName, scope, file, resolvedInFile, node) is { } resolvedExposed)
                {
                    // Raw reference is the written text (markers kept, bracket filter excluded);
                    // resolution above used the stripped name.
                    nodeEdges.Add(new SysmlEdge(node.QualifiedName, resolvedExposed, SysmlEdgeKind.Expose) { RawReference = member.RawReferenceText ?? member.QualifiedName });
                    resolvedExposeMembers.Add((member, resolvedExposed));
                }
            }

            view.ResolvedExposeMembers = resolvedExposeMembers;

            // A view usage's view definition (`view v : Def;`) resolves like feature typing; the
            // usage inherits the definition's view conditions and rendering (SysML 7.26.2).
            if (view.ViewDefinitionName is { Length: > 0 } viewDefinitionName &&
                ResolveOrWarn(viewDefinitionName, scope, file, resolvedInFile, node) is { } resolvedDefinition)
            {
                nodeEdges.Add(new SysmlEdge(node.QualifiedName, resolvedDefinition, SysmlEdgeKind.Typing) { RawReference = viewDefinitionName });
            }
        }

        // References written inside raw expression/declaration text (transition triggers, guards
        // and effects, accept/send payloads, state action declarations, render targets): resolved
        // like any other reference, but located per occurrence and without an edge.
        ResolveReferenceOccurrences(node, scope, file);

        if (nodeEdges.Count > 0)
        {
            node.ResolvedEdges = nodeEdges;
            edges.AddRange(nodeEdges);
        }

        var childScope = ChildScope(node, scope);
        foreach (var child in node.Children)
        {
            ResolveNode(child, file, resolvedInFile, childScope, edges);
        }
    }

    /// <summary>
    ///     Resolves each <see cref="SysmlNode.ReferenceOccurrences"/> entry of
    ///     <paramref name="node"/> and reports every one that does not resolve as an
    ///     "Unresolved reference" diagnostic (at the resolver's unresolved severity) at its own
    ///     token position.
    /// </summary>
    /// <remarks>
    ///     A <c>render</c> target is resolved in the view's own namespace (so an
    ///     <c>import Views::*;</c> written in the view's body applies); every other occurrence is
    ///     resolved in the namespace that owns the node, like the node's typing and supertypes.
    ///     Each occurrence is reported, not each distinct name: the same undeclared name used in
    ///     two guards is two places to fix. No <see cref="SysmlEdge"/> is recorded because the
    ///     occurrences have no single edge kind yet (the reference is to a feature, a type, or a
    ///     rendering depending on the context), so the resolved/unresolved outcome is the only
    ///     result; the standard library is checked when it is compiled, not when it is loaded.
    ///     <para>
    ///     A view's render target that names a SysML2Tools extension selector
    ///     (<see cref="ToolRenderSelectors"/>) and does not resolve to a declaration is not an
    ///     unresolved reference: it is accepted (rendering is unchanged) but a portability Warning
    ///     is reported at the token, because no other SysML v2 tool knows the selector.
    ///     </para>
    /// </remarks>
    private void ResolveReferenceOccurrences(SysmlNode node, string? scope, LoadedFile file)
    {
        if (node.ReferenceOccurrences.Count == 0)
        {
            return;
        }

        var occurrenceScope = node is SysmlViewNode { QualifiedName: { Length: > 0 } viewName } ? viewName : scope;
        foreach (var occurrence in node.ReferenceOccurrences)
        {
            if (TryResolve(occurrence.Name, occurrenceScope, file.RootImports, out _))
            {
                continue;
            }

            if (node is SysmlViewNode && ToolRenderSelectors.TryGetValue(occurrence.Name, out var definition))
            {
                _diagnostics.Add(new SysmlDiagnostic(
                    file.FilePath,
                    occurrence.Position.Line,
                    occurrence.Position.Column,
                    DiagnosticSeverity.Warning,
                    $"'{occurrence.Name}' is a SysML2Tools extension, not a standard SysML v2 rendering; " +
                    "the model will not validate in other tools. Use a standard view definition instead " +
                    $"(e.g. 'view v : StandardViewDefinitions::{definition}')."));
                continue;
            }

            _diagnostics.Add(new SysmlDiagnostic(
                file.FilePath,
                occurrence.Position.Line,
                occurrence.Position.Column,
                _unresolvedSeverity,
                $"Unresolved reference: '{occurrence.Name}'"));
        }
    }

    /// <summary>
    ///     Second-pass traversal that resolves the ends (possibly dotted feature chains such as
    ///     <c>engine.fuelPort</c>) of <see cref="SysmlConnectionNode"/> (<c>"connection"</c>,
    ///     <c>"message"</c> and <c>"binding"</c> keyword variants only) and
    ///     <see cref="SysmlTransitionNode"/> <c>Source</c>/<c>Target</c>, emitting a
    ///     <see cref="SysmlEdgeKind.Connect"/>/<see cref="SysmlEdgeKind.Binding"/>/
    ///     <see cref="SysmlEdgeKind.Transition"/> edge only when both sides resolve. A chain is
    ///     resolved by <see cref="NameResolver"/> and recorded at its instance path. This is a
    ///     traversal of its own, rather than part of <see cref="ResolveNode"/>, only so that a
    ///     node's chain edges follow its pass-1 edges in <see cref="SysmlNode.ResolvedEdges"/> and
    ///     in the aggregate edge list: layouts and the recorded golden edge files see that order.
    /// </summary>
    /// <param name="node">The AST node to process.</param>
    /// <param name="file">The file being resolved (path for diagnostics, root imports).</param>
    /// <param name="resolvedInFile">
    ///     Set of unresolved names already warned about in this file, preventing duplicate
    ///     warnings for the same unresolved name within one file.
    /// </param>
    /// <param name="scope">
    ///     The local namespace in effect for <paramref name="node"/>, computed exactly as
    ///     <see cref="ResolveNode"/> does so segment-0 resolution scope cannot silently diverge
    ///     between the two passes.
    /// </param>
    /// <param name="edges">
    ///     Aggregate list of all edges resolved so far across the whole file-root traversal;
    ///     appended to by this method.
    /// </param>
    private void ResolveFeatureChains(
        SysmlNode node,
        LoadedFile file,
        HashSet<string> resolvedInFile,
        string? scope,
        List<SysmlEdge> edges)
    {
        var nodeEdges = new List<SysmlEdge>();

        // Connection/message endpoints (allocation endpoints are intentionally excluded — they
        // remain single-segment-only per unit-3's existing, locked-in Allocate behavior).
        if (node is SysmlConnectionNode { ConnectionKeyword: "connection" or "message" } connection)
        {
            var resolvedA = ResolveFeatureChainSide(connection.EndpointA, file, resolvedInFile, scope, node);
            var resolvedB = ResolveFeatureChainSide(connection.EndpointB, file, resolvedInFile, scope, node);

            if (resolvedA is not null && resolvedB is not null)
            {
                nodeEdges.Add(new SysmlEdge(resolvedA, resolvedB, SysmlEdgeKind.Connect));
            }
        }

        // Binding connector endpoints ("bind A = B;", the bindingConnectorAsUsage shape only)
        // resolve via the same feature-chain resolution as connection/message, for parity per
        // the corpus's frequent dotted-chain bind sides (e.g. "bind engine.fuelCmdPort=fuelCmdPort;").
        if (node is SysmlConnectionNode { ConnectionKeyword: "binding" } binding)
        {
            var resolvedA = ResolveFeatureChainSide(binding.EndpointA, file, resolvedInFile, scope, node);
            var resolvedB = ResolveFeatureChainSide(binding.EndpointB, file, resolvedInFile, scope, node);

            if (resolvedA is not null && resolvedB is not null)
            {
                nodeEdges.Add(new SysmlEdge(resolvedA, resolvedB, SysmlEdgeKind.Binding));
            }
        }

        // Transition source/target (an implied/omitted Source produces no edge — documented
        // limitation, since there is nothing to walk a chain from). Both ends additionally fall
        // back to TryResolveInheritedActionMember (see its doc comment) when the ordinary
        // dotted-chain resolution fails — these are the only reference kinds allowed that fallback,
        // since it exists specifically to resolve the inherited `start`/`done` pseudo-features
        // (e.g. `first start then off;` and `then done;`).
        if (node is SysmlTransitionNode transition)
        {
            var resolvedSource = ResolveTransitionEnd(transition.Source, false, file, resolvedInFile, scope, node);
            var resolvedTarget = ResolveTransitionEnd(transition.Target, true, file, resolvedInFile, scope, node);

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

        var childScope = ChildScope(node, scope);
        foreach (var child in node.Children)
        {
            ResolveFeatureChains(child, file, resolvedInFile, childScope, edges);
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
        LoadedFile file,
        HashSet<string> resolvedInFile,
        string? scope,
        SysmlNode owner)
    {
        if (side is not { Length: > 0 })
        {
            return null;
        }

        if (TryResolve(side, scope, file.RootImports, out var resolved, pathValued: true))
        {
            return resolved;
        }

        ReportUnresolved(side, file.FilePath, resolvedInFile, owner);
        return null;
    }

    /// <summary>
    ///     Resolves one end (<c>Source</c> or <c>Target</c>) of a <see cref="SysmlTransitionNode"/>.
    ///     Behaves exactly like <see cref="ResolveFeatureChainSide"/> (same diagnostic/deduplication
    ///     behavior) except that, when the ordinary dotted-chain resolution fails, it also tries
    ///     <see cref="TryResolveInheritedActionMember"/> before giving up — the narrow fallback
    ///     that lets the standard <c>start</c> and <c>done</c> pseudo-features resolve on either end
    ///     of a succession or transition (<c>first start then off;</c> and
    ///     <c>then done;</c> / <c>transition first x accept e then done;</c>). Ordinary lookup now
    ///     finds the library's own <c>start</c>/<c>done</c> through the implicit specialization of
    ///     the enclosing state or action (see <see cref="NameResolver"/>), but exported transition
    ///     edges have always targeted the <c>Actions::Action</c> members, so a written end that is
    ///     exactly <c>start</c>/<c>done</c> and resolves to a library member is canonicalized to
    ///     that same target (compatibility rule; the fallback is kept for ends that still fail).
    ///     This is deliberately scoped to
    ///     transition ends only — not connection/binding endpoints or any other reference kind. On the
    ///     <c>Target</c> side it is further limited to exactly <c>start</c>/<c>done</c> inside a state
    ///     or action body (definition or usage); the <c>Source</c> side keeps the wider inherited
    ///     lookup that implicit initial-pseudostate transitions rely on.
    /// </summary>
    private string? ResolveTransitionEnd(
        string? end,
        bool isTarget,
        LoadedFile file,
        HashSet<string> resolvedInFile,
        string? scope,
        SysmlNode owner)
    {
        if (end is not { Length: > 0 })
        {
            return null;
        }

        if (TryResolve(end, scope, file.RootImports, out var resolved, pathValued: true))
        {
            // Compatibility canonicalization: ordinary lookup now also finds the library's own
            // start/done through the implicit specialization of the enclosing state or action
            // (e.g. States::StateAction::start for a state), but the transition edges this tool
            // has always exported target the Actions::Action members; keep them stable.
            if (end is "start" or "done" &&
                _stdlibNames.Contains(resolved) &&
                TryResolveInheritedActionMember(scope, end, pseudoFeaturesOnly: isTarget, out var canonical))
            {
                return canonical;
            }

            return resolved;
        }

        if (TryResolveInheritedActionMember(scope, end, pseudoFeaturesOnly: isTarget, out var inherited))
        {
            return inherited;
        }

        // An anonymous action node (`fork;`, `decide;`, `terminate;`, ...) is named `$keywordN`
        // by the AST builder — a name no user identifier can have — and is not in the symbol
        // table, so a succession end naming one cannot resolve and draws no edge; it is a
        // reference to a node the enclosing scope owns, not an unresolved name.
        if (end[0] == '$' && IsAnonymousActionNodeOf(scope, end))
        {
            return null;
        }

        ReportUnresolved(end, file.FilePath, resolvedInFile, owner);
        return null;
    }

    /// <summary>
    ///     Returns whether <paramref name="name"/> names an anonymous action node owned by the
    ///     element <paramref name="scope"/> (a child of it whose name is exactly
    ///     <paramref name="name"/>).
    /// </summary>
    private bool IsAnonymousActionNodeOf(string? scope, string name) =>
        !string.IsNullOrEmpty(scope) &&
        _symbolTable.Lookup(scope) is { } scopeNode &&
        scopeNode.Children.Any(child => child.Name == name);

    /// <summary>
    ///     Narrow fallback for resolving a Transition <c>Source</c> or <c>Target</c> that names a
    ///     member inherited from the enclosing state/action's type or ancestor chain, rather than
    ///     declared/imported directly — the shape needed for real SysML v2 initial-pseudostate and
    ///     completion idioms like <c>first start then off;</c> and <c>then done;</c>, where
    ///     <c>start</c>/<c>done</c> are real features every action and state inherits from
    ///     <c>Actions::Action</c> (SysML v2.0 Systems Library, <c>Action::start</c>/<c>Action::done</c>; a
    ///     state is an Action via <c>States::StateAction</c>), not special keywords. When
    ///     <paramref name="pseudoFeaturesOnly"/> is set (the <c>Target</c> side) the lookup applies only
    ///     to the names <c>start</c>/<c>done</c> and only when the enclosing node is a state or action
    ///     (definition or usage); otherwise it returns <see langword="false"/> immediately.
    ///     <para/>
    ///     Resolution order: (1) if the enclosing node (the current <paramref name="scope"/>)
    ///     has a resolved <see cref="SysmlEdgeKind.Typing"/> edge (e.g. a
    ///     <c>state x : SomeStateDef { ... }</c> usage), follow it to the type node and walk its
    ///     <see cref="SysmlEdgeKind.Supertype"/> chain via <see cref="FindMemberInTypeHierarchy"/>;
    ///     (2) failing that, walk the enclosing node's own
    ///     <see cref="SysmlEdgeKind.Supertype"/>/<see cref="SysmlEdgeKind.Redefinition"/> edges
    ///     directly via <see cref="FindMemberInAncestorChain"/> (covers a <c>state def</c> that
    ///     itself declares an explicit <c>:&gt;</c> supertype); (3) only if both fail, and only for
    ///     the two well-known names ROADMAP.md calls out (<c>"start"</c>/<c>"done"</c>), and only
    ///     when the enclosing node itself has a state/action keyword, fall back to a hardcoded,
    ///     narrowly-scoped last resort: look the name up directly among
    ///     <c>Actions::Action</c>'s own direct children in the stdlib. This last-resort branch
    ///     exists to keep the exported transition targets stable: <see cref="NameResolver"/> now
    ///     models implicit specialization for name lookup (a bare <c>state def VehicleStates;</c>
    ///     implicitly specializes <c>States::StateAction</c>, so ordinary lookup finds
    ///     <c>States::StateAction::start</c>), but implicit specializations are never edges, and
    ///     <see cref="ResolveTransitionEnd"/> canonicalizes a library <c>start</c>/<c>done</c> found
    ///     by ordinary lookup to the <c>Actions::Action</c> member this method has always produced.
    ///     It is a deliberate, explicitly-scoped compatibility rule, not a general inherited-member
    ///     resolver, and must not be reused for any other reference kind or name (dropping the
    ///     canonicalization and this last resort is the library-faithful alternative, at the cost
    ///     of changing exported state-transition edges).
    /// </summary>
    private bool TryResolveInheritedActionMember(
        string? scope,
        string name,
        bool pseudoFeaturesOnly,
        out string resolvedName)
    {
        resolvedName = string.Empty;

        if (string.IsNullOrEmpty(scope))
        {
            return false;
        }

        var enclosingNode = _symbolTable.Lookup(scope);
        if (enclosingNode is null)
        {
            return false;
        }

        var isStateOrActionKeyword = enclosingNode switch
        {
            SysmlFeatureNode { FeatureKeyword: "state" or "action" } => true,
            SysmlDefinitionNode { DefinitionKeyword: "state def" or "action def" } => true,
            _ => false,
        };

        // Target-side use is limited to exactly the well-known pseudo-features inside a state or
        // action body: no other inherited member, and no other kind of enclosing node, qualifies.
        if (pseudoFeaturesOnly && (name is not ("start" or "done") || !isStateOrActionKeyword))
        {
            return false;
        }

        var typingEdge = enclosingNode.ResolvedEdges.FirstOrDefault(e => e.Kind == SysmlEdgeKind.Typing);
        if (typingEdge is not null)
        {
            var typeNode = _symbolTable.Lookup(typingEdge.TargetQualifiedName);
            if (typeNode is not null)
            {
                var viaTyping = FindMemberInTypeHierarchy(typeNode, name, MemberAccess.NonPrivate, new HashSet<string>());
                if (viaTyping?.QualifiedName is { Length: > 0 } viaTypingQualifiedName)
                {
                    resolvedName = viaTypingQualifiedName;
                    return true;
                }
            }
        }

        var viaAncestor = FindMemberInAncestorChain(enclosingNode, name, MemberAccess.Any, new HashSet<string>());
        if (viaAncestor?.QualifiedName is { Length: > 0 } viaAncestorQualifiedName)
        {
            resolvedName = viaAncestorQualifiedName;
            return true;
        }

        // Last resort: only for the two well-known inherited pseudostate/action members named in
        // ROADMAP.md, and only when the enclosing node itself has a state/action keyword, so this
        // simplification cannot silently misfire for unrelated names or node kinds.
        if (name is not ("start" or "done"))
        {
            return false;
        }

        if (!isStateOrActionKeyword)
        {
            return false;
        }

        var actionDefinition = _symbolTable.Lookup("Actions::Action");
        var fallback = actionDefinition?.Children.FirstOrDefault(c => c.Name == name);
        if (fallback?.QualifiedName is not { Length: > 0 } fallbackQualifiedName)
        {
            return false;
        }

        resolvedName = fallbackQualifiedName;
        return true;
    }

    /// <summary>Which memberships of a type a member lookup admits.</summary>
    private enum MemberAccess
    {
        /// <summary>Every membership (the type's own scope).</summary>
        Any,

        /// <summary>Everything except private memberships (what a specialization inherits).</summary>
        NonPrivate,

        /// <summary>Public memberships only (visible resolution from outside the type).</summary>
        Public,
    }

    /// <summary>Returns whether a membership with <paramref name="visibility"/> is admitted by <paramref name="access"/>.</summary>
    private static bool Admits(SysmlVisibility visibility, MemberAccess access) => access switch
    {
        MemberAccess.Any => true,
        MemberAccess.NonPrivate => visibility != SysmlVisibility.Private,
        _ => visibility == SysmlVisibility.Public,
    };

    /// <summary>
    ///     Finds a member named <paramref name="name"/> in <paramref name="typeNode"/>'s own
    ///     direct children, or — recursively — in any of its <see cref="SysmlEdgeKind.Supertype"/>
    ///     ancestors' direct children, walking the supertype chain until a match is found or the
    ///     chain is exhausted. <paramref name="access"/> limits the memberships considered at
    ///     every level (the same limit applies to inherited members as to the type's own).
    ///     <paramref name="visited"/> guards against supertype cycles
    ///     (e.g. a malformed/adversarial <c>A :&gt; B :&gt; A</c> model), keyed on qualified type
    ///     name, so this method always terminates.
    /// </summary>
    private SysmlNode? FindMemberInTypeHierarchy(
        SysmlNode typeNode, string name, MemberAccess access, HashSet<string> visited)
    {
        if (typeNode.QualifiedName is { Length: > 0 } qualifiedName && !visited.Add(qualifiedName))
        {
            return null;
        }

        var direct = typeNode.Children.FirstOrDefault(c => c.Name == name && Admits(c.Visibility, access));
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

            var found = FindMemberInTypeHierarchy(supertypeNode, name, access, visited);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    ///     Finds a member named <paramref name="name"/> in <paramref name="node"/>'s own direct
    ///     children (limited by <paramref name="access"/>), or — recursively — in any ancestor
    ///     reachable by following its <see cref="SysmlEdgeKind.Supertype"/> <em>and</em>
    ///     <see cref="SysmlEdgeKind.Redefinition"/> resolved edges; an ancestor's private members
    ///     are never inherited, so the ancestors are searched with at most
    ///     <see cref="MemberAccess.NonPrivate"/>. Both edge kinds must be followed (not
    ///     supertype-only) because a nested
    ///     redefining feature's owner sometimes has no <c>SupertypeNames</c> of its own at all —
    ///     the only path to the inherited member is via the owner's own already-resolved
    ///     <c>Redefinition</c> edge (see <c>1c-PartsTreeRedefinition.sysml</c>'s nested
    ///     <c>frontAxle_c1 redefines frontAxle</c>, whose owner <c>frontAxleAssembly_c1</c> only
    ///     redefines <c>frontAxleAssembly</c> and has no supertype of its own).
    ///     <paramref name="visited"/> guards against cycles, keyed on qualified name, mirroring
    ///     <see cref="FindMemberInTypeHierarchy"/>'s guard pattern.
    /// </summary>
    private SysmlNode? FindMemberInAncestorChain(
        SysmlNode node, string name, MemberAccess access, HashSet<string> visited)
    {
        if (node.QualifiedName is { Length: > 0 } qualifiedName && !visited.Add(qualifiedName))
        {
            return null;
        }

        var direct = node.Children.FirstOrDefault(c => c.Name == name && Admits(c.Visibility, access));
        if (direct is not null)
        {
            return direct;
        }

        var inheritedAccess = access == MemberAccess.Public ? MemberAccess.Public : MemberAccess.NonPrivate;
        foreach (var edge in node.ResolvedEdges.Where(
                     e => e.Kind is SysmlEdgeKind.Supertype or SysmlEdgeKind.Redefinition))
        {
            var ancestorNode = _symbolTable.Lookup(edge.TargetQualifiedName);
            if (ancestorNode is null)
            {
                continue;
            }

            var found = FindMemberInAncestorChain(ancestorNode, name, inheritedAccess, visited);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}
