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
    ///     The name resolver used by the current pass (replaced between pass 0 and pass 1 so the
    ///     filter-enabled instance starts with a fresh cache).
    /// </summary>
    private NameResolver _resolver;

    /// <summary>
    ///     Initializes a new instance of <see cref="ReferenceResolver"/> with the given symbol
    ///     table and diagnostics list.
    /// </summary>
    /// <param name="symbolTable">The symbol table to resolve references against.</param>
    /// <param name="diagnostics">The shared diagnostics list to append Warning entries to.</param>
    /// <param name="stdlibNames">
    ///     The qualified names of the standard library declarations (the seed symbol table's
    ///     keys), or <see langword="null"/> when there is no standard library.
    /// </param>
    public ReferenceResolver(
        SymbolTable symbolTable,
        List<SysmlDiagnostic> diagnostics,
        IReadOnlySet<string>? stdlibNames = null)
    {
        _symbolTable = symbolTable;
        _diagnostics = diagnostics;
        _stdlibNames = stdlibNames ?? new HashSet<string>(StringComparer.Ordinal);
        _resolver = new NameResolver(symbolTable.Symbols, null, declaredReferencesOnly: true);
    }

    /// <summary>
    ///     A parsed source file together with the imports written at its root (outside any
    ///     package), which apply to every declaration in the file.
    /// </summary>
    private sealed record LoadedFile(string FilePath, SysmlNode Root, IReadOnlyList<SysmlImportNode> RootImports);

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
    ///             types' raw references rather than from edges that may not be attached yet.
    ///         </item>
    ///         <item>
    ///             <b>Pass 2</b> (<see cref="ResolveFeatureChains"/>) resolves dotted feature
    ///             chains (connection/message endpoints, transition source/target) into
    ///             Connect/Transition edges — see that method's remarks for why it must run after
    ///             pass 1 completes for all file roots.
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

        // Pass 2: resolve dotted feature chains (connection/message endpoints, transition
        // source/target) into Connect/Transition edges. This must run after pass 1 has resolved
        // Typing/Supertype edges for ALL file roots, because a chain walk may depend on a
        // Typing/Supertype edge belonging to a node visited later in document order within the
        // same file, or to a node in a different file entirely (see "Feature-Chain Resolution" in
        // the design doc for the full rationale).
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
    ///     Returns the local namespace in effect for the children of <paramref name="node"/>: the
    ///     node's own qualified name when it is a named package, definition, or usage, otherwise
    ///     the namespace <paramref name="scope"/> that already applied to the node itself.
    /// </summary>
    private static string? ChildScope(SysmlNode node, string? scope)
    {
        if (node is not (SysmlPackageNode or SysmlDefinitionNode or SysmlFeatureNode) || node.Name is null)
        {
            return scope;
        }

        if (node.QualifiedName is { Length: > 0 } qualified)
        {
            return qualified;
        }

        return scope is null ? node.Name : $"{scope}::{node.Name}";
    }

    /// <summary>
    ///     Attempts to resolve a name relative to <paramref name="scope"/> by KerML name
    ///     resolution (see <see cref="NameResolver"/>).
    /// </summary>
    /// <param name="name">The name to resolve — may be unqualified or qualified.</param>
    /// <param name="scope">The qualified name of the local namespace, or null at the file root.</param>
    /// <param name="rootImports">The imports written at the root of the referencing file.</param>
    /// <param name="resolvedName">
    ///     When this method returns <see langword="true"/>, the resolved element's fully-qualified
    ///     name; otherwise <see cref="string.Empty"/>.
    /// </param>
    private bool TryResolve(
        string name,
        string? scope,
        IReadOnlyList<SysmlImportNode> rootImports,
        out string resolvedName)
    {
        resolvedName = _resolver.Resolve(name, scope, rootImports) ?? string.Empty;
        return resolvedName.Length > 0;
    }

    /// <summary>
    ///     Resolves <paramref name="name"/> like <see cref="TryResolve"/>, emitting the
    ///     "Unresolved reference" Warning diagnostic (once per name per file) when it does not
    ///     resolve.
    /// </summary>
    /// <returns>The resolved qualified name, or null when the name did not resolve.</returns>
    private string? ResolveOrWarn(
        string name,
        string? scope,
        LoadedFile file,
        HashSet<string> resolvedInFile)
    {
        if (TryResolve(name, scope, file.RootImports, out var resolved))
        {
            return resolved;
        }

        ReportUnresolved(name, file.FilePath, resolvedInFile);
        return null;
    }

    /// <summary>
    ///     Emits the "Unresolved reference" Warning diagnostic for <paramref name="name"/> unless
    ///     it was already reported for this file.
    /// </summary>
    private void ReportUnresolved(string name, string filePath, HashSet<string> resolvedInFile)
    {
        if (resolvedInFile.Add(name))
        {
            _diagnostics.Add(new SysmlDiagnostic(
                filePath,
                0, 0,
                DiagnosticSeverity.Warning,
                $"Unresolved reference: '{name}'"));
        }
    }

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
            ResolveOrWarn(metadata.TypeReference, scope, file, resolvedInFile) is { } resolvedType)
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
            if (ResolveOrWarn(supertypeName, scope, file, resolvedInFile) is { } resolvedSupertype)
            {
                nodeEdges.Add(new SysmlEdge(node.QualifiedName, resolvedSupertype, SysmlEdgeKind.Supertype) { RawReference = supertypeName });
            }
        }

        // Feature typing — the type referenced after ':' on a usage/feature element
        if (node is SysmlFeatureNode { FeatureTyping: { } typing } &&
            ResolveOrWarn(typing, scope, file, resolvedInFile) is { } resolvedTyping)
        {
            nodeEdges.Add(new SysmlEdge(node.QualifiedName, resolvedTyping, SysmlEdgeKind.Typing) { RawReference = typing });
        }

        // Feature redefinition — the target referenced after 'redefines'/':>>' on a usage/feature
        // element, resolved against the general types of the owning type (KerML 8.2.3.5.1), with
        // a lenient fallback to ordinary resolution (see NameResolver.ResolveRedefinition).
        if (node is SysmlFeatureNode { RedefinedFeatureName: { } redefined })
        {
            var target = _resolver.ResolveRedefinition(redefined, scope ?? string.Empty, node.QualifiedName, file.RootImports);
            if (target is not null)
            {
                nodeEdges.Add(new SysmlEdge(node.QualifiedName, target, SysmlEdgeKind.Redefinition) { RawReference = redefined });
            }
            else
            {
                ReportUnresolved(redefined, file.FilePath, resolvedInFile);
            }
        }

        // Imports — resolved in the namespace that declares the import
        foreach (var importedName in node.ImportedNames)
        {
            if (ResolveOrWarn(importedName, scope, file, resolvedInFile) is { } resolvedImport)
            {
                nodeEdges.Add(new SysmlEdge(node.QualifiedName, resolvedImport, SysmlEdgeKind.Import) { RawReference = importedName });
            }
        }

        // Verified requirement names — sourced from the owning def/usage node's own qualified
        // name (the verifying case/requirement).
        foreach (var verifiedName in node.VerifiedRequirementNames)
        {
            if (ResolveOrWarn(verifiedName, scope, file, resolvedInFile) is { } resolvedVerified)
            {
                nodeEdges.Add(new SysmlEdge(node.QualifiedName, resolvedVerified, SysmlEdgeKind.Verify) { RawReference = verifiedName });
            }
        }

        // Satisfy usages resolve two independent sides (subject and requirement); an edge is only
        // emitted when BOTH sides resolve (graceful degradation: partial/misleading edges are
        // never produced). Dotted feature-chain subjects (e.g. "a.b") are out of scope for this
        // unit and simply fail to resolve as a single symbol name, which is graceful.
        if (node is SysmlSatisfyNode satisfy)
        {
            var resolvedSubject = satisfy.SubjectName is { Length: > 0 } subjectName
                ? ResolveOrWarn(subjectName, scope, file, resolvedInFile)
                : null;
            var resolvedRequirement = satisfy.RequirementName is { Length: > 0 } requirementName
                ? ResolveOrWarn(requirementName, scope, file, resolvedInFile)
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
                ? ResolveOrWarn(endpointA, scope, file, resolvedInFile)
                : null;
            var resolvedB = allocation.EndpointB is { Length: > 0 } endpointB
                ? ResolveOrWarn(endpointB, scope, file, resolvedInFile)
                : null;

            if (resolvedA is not null && resolvedB is not null)
            {
                nodeEdges.Add(new SysmlEdge(resolvedA, resolvedB, SysmlEdgeKind.Allocate));
            }
        }

        // Standalone dependency statements (SysmlDependencyNode) resolve every "from" name against
        // every "to" name independently (a cross product, per the grammar's list-of-lists shape).
        // An unresolved name produces one Warning diagnostic per distinct unresolved name per
        // file; a pair is only emitted as an edge when both its from-name and to-name resolve.
        if (node is SysmlDependencyNode dependency)
        {
            var resolvedFrom = dependency.FromNames
                .Select(name => ResolveOrWarn(name, scope, file, resolvedInFile))
                .OfType<string>()
                .ToList();
            var resolvedTo = dependency.ToNames
                .Select(name => ResolveOrWarn(name, scope, file, resolvedInFile))
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
        // subject -- per the SysML v2 grammar it never refers to model content, so
        // ReferenceResolver never inspects it, mirroring how FilterExpressionText (raw source
        // text, not a reference) is also intentionally never touched here.
        if (node is SysmlViewNode view)
        {
            var resolvedExposeMembers = new List<(ExposeMember Member, string ResolvedQualifiedName)>();
            foreach (var member in view.ExposeMembers)
            {
                if (ResolveOrWarn(member.QualifiedName, scope, file, resolvedInFile) is { } resolvedExposed)
                {
                    nodeEdges.Add(new SysmlEdge(node.QualifiedName, resolvedExposed, SysmlEdgeKind.Expose) { RawReference = member.QualifiedName });
                    resolvedExposeMembers.Add((member, resolvedExposed));
                }
            }

            view.ResolvedExposeMembers = resolvedExposeMembers;

            // A view usage's view definition (`view v : Def;`) resolves like feature typing; the
            // usage inherits the definition's view conditions and rendering (SysML 7.26.2).
            if (view.ViewDefinitionName is { Length: > 0 } viewDefinitionName &&
                ResolveOrWarn(viewDefinitionName, scope, file, resolvedInFile) is { } resolvedDefinition)
            {
                nodeEdges.Add(new SysmlEdge(node.QualifiedName, resolvedDefinition, SysmlEdgeKind.Typing) { RawReference = viewDefinitionName });
            }
        }

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
    ///     Second-pass traversal that resolves dotted feature chains (e.g. <c>engine.fuelPort</c>)
    ///     referenced by <see cref="SysmlConnectionNode"/> (<c>"connection"</c>/<c>"message"</c>
    ///     keyword variants only) and <see cref="SysmlTransitionNode"/> <c>Source</c>/<c>Target</c>,
    ///     emitting a <see cref="SysmlEdgeKind.Connect"/>/<see cref="SysmlEdgeKind.Transition"/>
    ///     edge only when both sides resolve. Must run after <see cref="ResolveNode"/> has
    ///     completed for all file roots, since chain walking depends on the Typing/Supertype
    ///     edges pass 1 attaches to <see cref="SysmlNode.ResolvedEdges"/>.
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
            var resolvedA = ResolveFeatureChainSide(connection.EndpointA, file, resolvedInFile, scope);
            var resolvedB = ResolveFeatureChainSide(connection.EndpointB, file, resolvedInFile, scope);

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
            var resolvedA = ResolveFeatureChainSide(binding.EndpointA, file, resolvedInFile, scope);
            var resolvedB = ResolveFeatureChainSide(binding.EndpointB, file, resolvedInFile, scope);

            if (resolvedA is not null && resolvedB is not null)
            {
                nodeEdges.Add(new SysmlEdge(resolvedA, resolvedB, SysmlEdgeKind.Binding));
            }
        }

        // Transition source/target (an implied/omitted Source produces no edge — documented
        // limitation, since there is nothing to walk a chain from). Source additionally falls
        // back to TryResolveInheritedActionMember (see its doc comment) when the ordinary
        // dotted-chain walk fails — this is the only reference kind allowed that fallback, since
        // it exists specifically to resolve inherited pseudostate-like features (`start`/`done`)
        // used as a transition's implicit initial source (e.g. `first start then off;`).
        if (node is SysmlTransitionNode transition)
        {
            var resolvedSource = ResolveTransitionSource(transition.Source, file, resolvedInFile, scope);
            var resolvedTarget = ResolveFeatureChainSide(transition.Target, file, resolvedInFile, scope);

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
        string? scope)
    {
        if (side is not { Length: > 0 })
        {
            return null;
        }

        if (TryResolveFeatureChain(side, scope, file.RootImports, out var resolved))
        {
            return resolved;
        }

        ReportUnresolved(side, file.FilePath, resolvedInFile);
        return null;
    }

    /// <summary>
    ///     Resolves a <see cref="SysmlTransitionNode"/>'s <c>Source</c> side. Behaves exactly like
    ///     <see cref="ResolveFeatureChainSide"/> (same diagnostic/deduplication behavior) except
    ///     that, when the ordinary dotted-chain walk fails, it also tries
    ///     <see cref="TryResolveInheritedActionMember"/> before giving up — the narrow fallback
    ///     that lets an implied initial-pseudostate transition (e.g. <c>first start then off;</c>)
    ///     resolve <c>start</c>/<c>done</c>, inherited members that ordinary scope/import
    ///     resolution can never see since they are not declared or imported anywhere in the
    ///     referencing file. This fallback is deliberately scoped to Transition <c>Source</c> only
    ///     — not <c>Target</c>, and not connection/binding endpoints — per ROADMAP.md's explicit
    ///     instruction.
    /// </summary>
    private string? ResolveTransitionSource(
        string? source,
        LoadedFile file,
        HashSet<string> resolvedInFile,
        string? scope)
    {
        if (source is not { Length: > 0 })
        {
            return null;
        }

        if (TryResolveFeatureChain(source, scope, file.RootImports, out var resolved))
        {
            return resolved;
        }

        if (TryResolveInheritedActionMember(scope, source, out var inherited))
        {
            return inherited;
        }

        ReportUnresolved(source, file.FilePath, resolvedInFile);
        return null;
    }

    /// <summary>
    ///     Narrow fallback for resolving a Transition <c>Source</c> that names a member inherited
    ///     from the enclosing state/action's type or ancestor chain, rather than declared/imported
    ///     directly — the shape needed for real SysML v2 initial-pseudostate/entry-action idioms
    ///     like <c>first start then off;</c>, where <c>start</c> is a real feature every state
    ///     definition/usage inherits from <c>Actions::Action</c>
    ///     (<c>action start: Action :&gt;&gt; startShot</c>), not a special keyword.
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
    ///     exists because this codebase implements no general implicit-generalization/default-
    ///     supertype inference (a bare <c>state def VehicleStates;</c> with no explicit <c>:&gt;</c>
    ///     clause never gets an automatic <c>Supertype</c> edge to <c>States::StateAction</c>/
    ///     <c>Actions::Action</c> the way the real SysML v2 spec implies) — it is a deliberate,
    ///     explicitly-scoped simplification, not a general inherited-member resolver, and must not
    ///     be reused for any other reference kind or name.
    /// </summary>
    private bool TryResolveInheritedActionMember(
        string? scope,
        string name,
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

        var isStateOrActionKeyword = enclosingNode switch
        {
            SysmlFeatureNode { FeatureKeyword: "state" or "action" } => true,
            SysmlDefinitionNode { DefinitionKeyword: "state def" or "action def" } => true,
            _ => false,
        };

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

    /// <summary>
    ///     Resolves a dotted feature chain (e.g. <c>engine.fuelPort</c>,
    ///     <c>rearAxle.leftHalfAxle.axleToWheelPort</c>) to an instance-relative qualified path for
    ///     its final segment. Segment 0 is resolved via <see cref="TryResolve"/> (so it
    ///     participates in the same scope/import resolution as any other single-name reference);
    ///     each subsequent segment is resolved relative to the previous
    ///     segment's node via <see cref="FindFeatureMember"/>. A single-segment "chain" (no
    ///     <c>.</c>) is handled by the loop simply never executing, so this method also serves as
    ///     the single-segment resolver used elsewhere.
    ///     A parallel <c>instancePath</c> accumulator is tracked alongside the real declared node's
    ///     qualified name (<c>current</c>, which continues to drive the next segment's
    ///     <c>_symbolTable.Lookup</c>): while every segment so far has resolved via
    ///     <see cref="FindFeatureMember"/>'s direct-child branch, <c>instancePath</c> is simply the
    ///     member's own (already instance-relative) qualified name. Once any segment resolves via
    ///     the type-hierarchy fallback branch, <c>current</c> stops referring to a real instance
    ///     node (it becomes the type's own declared path), so every remaining segment — even one
    ///     that is itself a "direct child" of that type-declared node — is also only reachable
    ///     relative to the type, not the instance; the accumulator therefore latches into
    ///     instance-relative-append mode (<c>{previous instancePath}::{segment}</c>) as soon as the
    ///     first fallback hop occurs, and stays in that mode for every subsequent segment,
    ///     preserving the instance-relative path rather than collapsing back to a type-declared path
    ///     on a later direct-child hop. This is what <paramref name="resolvedName"/> ultimately
    ///     returns.
    /// </summary>
    /// <param name="chain">The raw, possibly dotted, reference text.</param>
    /// <param name="scope">The local namespace, used to resolve segment 0.</param>
    /// <param name="rootImports">The imports written at the root of the referencing file.</param>
    /// <param name="resolvedName">
    ///     When this method returns <see langword="true"/>, the instance-relative qualified path of
    ///     the chain's final segment. When this method returns <see langword="false"/>, set to
    ///     <see cref="string.Empty"/>.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> if every segment of the chain resolves; <see langword="false"/>
    ///     otherwise.
    /// </returns>
    private bool TryResolveFeatureChain(
        string chain,
        string? scope,
        IReadOnlyList<SysmlImportNode> rootImports,
        out string resolvedName)
    {
        var segments = chain.Split('.');

        if (!TryResolve(segments[0], scope, rootImports, out var current))
        {
            resolvedName = string.Empty;
            return false;
        }

        var instancePath = current;

        // Once any segment resolves via the type-hierarchy fallback branch, `current` no longer
        // refers to a real instance node — it is the type's own declared path. Every subsequent
        // segment is therefore looked up *within that type's declared structure*, so even a
        // "direct child" match at that point yields a type-declared (not instance-relative)
        // QualifiedName. `inTypeContext` latches once fallback is hit and stays set, so the
        // instance-relative accumulation keeps applying to every later segment too — fixing a bug
        // where a direct-child hop occurring *after* an earlier fallback hop would incorrectly
        // reset `instancePath` back to the type's own declared path.
        var inTypeContext = false;

        for (var i = 1; i < segments.Length; i++)
        {
            var currentNode = _symbolTable.Lookup(current);
            if (currentNode is null)
            {
                resolvedName = string.Empty;
                return false;
            }

            var member = FindFeatureMember(currentNode, segments[i], out var viaTypeFallback);
            if (member?.QualifiedName is not { Length: > 0 } memberQualifiedName)
            {
                resolvedName = string.Empty;
                return false;
            }

            current = memberQualifiedName;
            inTypeContext = inTypeContext || viaTypeFallback;
            instancePath = inTypeContext ? $"{instancePath}::{segments[i]}" : memberQualifiedName;
        }

        resolvedName = instancePath;
        return true;
    }

    /// <summary>
    ///     Finds a member named <paramref name="name"/> reachable from <paramref name="node"/>,
    ///     trying <paramref name="node"/>'s own direct children first (an inline nested usage or
    ///     redefinition shadows a same-named definition-level member — see
    ///     <c>2c-PartsInterconnection-MultipleDecompositions.sysml</c>'s <c>port :&gt;&gt; pe =
    ///     c1.pb</c> pattern), then falling back to the member's <see cref="SysmlEdgeKind.Typing"/>
    ///     target's own hierarchy (direct children and supertype chain) when no direct child
    ///     matches. Only public members are found: a feature-chain segment after the first is
    ///     resolved by visible resolution in the previous feature (KerML 8.2.3.5), so private and
    ///     protected members, declared or inherited, never match.
    /// </summary>
    /// <param name="node">The node whose members are searched.</param>
    /// <param name="name">The member name being looked up.</param>
    /// <param name="viaTypeFallback">
    ///     Set to <see langword="false"/> when the returned node was found among
    ///     <paramref name="node"/>'s own direct children (the result's <c>QualifiedName</c> is
    ///     already instance-relative). Set to <see langword="true"/> when the returned node was
    ///     found via the <see cref="SysmlEdgeKind.Typing"/> target's own hierarchy (the result's
    ///     <c>QualifiedName</c> is the type's own declared path, not instance-relative — callers
    ///     that need an instance-relative path must reconstruct it themselves).
    /// </param>
    private SysmlNode? FindFeatureMember(SysmlNode node, string name, out bool viaTypeFallback)
    {
        var direct = node.Children.FirstOrDefault(c => c.Name == name && Admits(c.Visibility, MemberAccess.Public));
        if (direct is not null)
        {
            viaTypeFallback = false;
            return direct;
        }

        viaTypeFallback = true;

        var typingEdge = node.ResolvedEdges.FirstOrDefault(e => e.Kind == SysmlEdgeKind.Typing);
        if (typingEdge is null)
        {
            return null;
        }

        var typeNode = _symbolTable.Lookup(typingEdge.TargetQualifiedName);
        return typeNode is null
            ? null
            : FindMemberInTypeHierarchy(typeNode, name, MemberAccess.Public, new HashSet<string>());
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
