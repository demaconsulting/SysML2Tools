// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

namespace DemaConsulting.SysML2Tools.Semantic.Model;

/// <summary>
///     The outcome of <see cref="NameResolver.ResolveWithFallback"/>: the resolved element's
///     qualified name (or null), and whether it was found only by the tool's non-standard bare
///     simple-name fallback rather than by KerML name resolution.
/// </summary>
/// <param name="QualifiedName">The resolved element's qualified name, or null when unresolved.</param>
/// <param name="IsSimpleNameFallback">
///     <see langword="true"/> when KerML name resolution failed and the element was chosen by
///     matching only the reference's last segment against every declaration in the workspace.
/// </param>
/// <param name="Candidates">
///     For a simple-name fallback, every declaration whose last segment matched (more than one
///     means the choice was ambiguous); empty otherwise.
/// </param>
public sealed record NameResolution(string? QualifiedName, bool IsSimpleNameFallback, IReadOnlyList<string> Candidates);

/// <summary>
///     Resolves qualified names against a loaded workspace following KerML 8.2.3.5 name
///     resolution: local resolution (owned, imported, and — for types — inherited memberships,
///     matching names, short names, and aliases), full resolution outward through owning
///     namespaces to the global namespace, and visible resolution for the later segments of a
///     qualified name.
/// </summary>
/// <remarks>
///     Owning namespaces are derived from qualified names (the prefix before the last <c>::</c>).
///     The global namespace is the set of top-level declarations of every loaded root namespace,
///     including the standard library. A type's general types come from its resolved
///     <see cref="SysmlEdgeKind.Supertype"/>/<see cref="SysmlEdgeKind.Typing"/>/
///     <see cref="SysmlEdgeKind.Redefinition"/> edges when present, otherwise from resolving its
///     raw supertype/typing references. Filtered imports are honored through the optional
///     <c>importFilter</c> predicate; without one, an import's filter conditions are ignored.
///     Results are cached per instance, so one instance should be used per loaded workspace.
///     <para>
///     <see cref="ReferenceResolver"/> uses an internal <em>resolving</em> mode while the
///     workspace's edges are still being attached: general types are then derived only from each
///     type's raw references (never from <see cref="SysmlNode.ResolvedEdges"/>), which makes
///     every answer independent of the order in which nodes are visited and keeps the cache valid
///     as edges appear. In that mode the imports written at the root of a source file (outside
///     any package) apply to that file's top-level declarations, as KerML scopes them.
///     </para>
/// </remarks>
public sealed class NameResolver
{
    /// <summary>Which memberships of a namespace a lookup considers.</summary>
    private enum Scope
    {
        /// <summary>Local resolution: all owned and imported memberships, inherited non-private ones.</summary>
        Local,

        /// <summary>Memberships a specialization inherits: everything except private.</summary>
        Inheritable,

        /// <summary>Visible resolution: public memberships only.</summary>
        Visible,
    }

    private readonly IReadOnlyDictionary<string, SysmlNode> _declarations;
    private readonly Func<SysmlImportNode, string, bool>? _importFilter;
    private readonly bool _declaredReferencesOnly;
    private readonly Dictionary<(string Name, string Namespace, Scope Scope), string?> _cache = new();
    private readonly HashSet<(string Name, string Namespace, Scope Scope)> _inProgress = new();
    private readonly Dictionary<string, IReadOnlyList<string>> _declaredGeneralTypes = new(StringComparer.Ordinal);
    private readonly HashSet<string> _declaredGeneralTypesInProgress = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<SysmlImportNode>> _rootImportsByTopLevel = new(StringComparer.Ordinal);
    private Dictionary<string, List<string>>? _bySimpleName;

    /// <summary>
    ///     Initializes a new resolver over the given declarations.
    /// </summary>
    /// <param name="declarations">The workspace declarations keyed by qualified name.</param>
    /// <param name="importFilter">
    ///     Optional predicate deciding whether a candidate element (by qualified name) passes a
    ///     filtered import's conditions; called only for imports with a bracket filter.
    /// </param>
    public NameResolver(
        IReadOnlyDictionary<string, SysmlNode> declarations,
        Func<SysmlImportNode, string, bool>? importFilter = null)
        : this(declarations, importFilter, declaredReferencesOnly: false)
    {
    }

    /// <summary>
    ///     Initializes a new resolver, optionally in the internal resolving mode
    ///     <see cref="ReferenceResolver"/> uses (see the class remarks).
    /// </summary>
    /// <param name="declarations">The workspace declarations keyed by qualified name.</param>
    /// <param name="importFilter">Optional filtered-import predicate.</param>
    /// <param name="declaredReferencesOnly">
    ///     <see langword="true"/> to derive general types only from raw references, ignoring
    ///     resolved edges.
    /// </param>
    internal NameResolver(
        IReadOnlyDictionary<string, SysmlNode> declarations,
        Func<SysmlImportNode, string, bool>? importFilter,
        bool declaredReferencesOnly)
    {
        ArgumentNullException.ThrowIfNull(declarations);
        _declarations = declarations;
        _importFilter = importFilter;
        _declaredReferencesOnly = declaredReferencesOnly;
    }

    /// <summary>
    ///     Registers the imports written at the root of a source file (outside any package) as
    ///     applying to that file's top-level declarations, which are then consulted after the
    ///     owning namespaces and before the global namespace.
    /// </summary>
    /// <param name="topLevelNames">The simple names of the file's top-level declarations.</param>
    /// <param name="rootImports">The file-root import nodes.</param>
    internal void RegisterRootImports(IEnumerable<string> topLevelNames, IReadOnlyList<SysmlImportNode> rootImports)
    {
        if (rootImports.Count == 0)
        {
            return;
        }

        foreach (var name in topLevelNames)
        {
            _rootImportsByTopLevel.TryAdd(name, rootImports);
        }
    }

    /// <summary>
    ///     Resolves <paramref name="reference"/> relative to <paramref name="localNamespace"/>
    ///     per KerML 8.2.3.5.1: a single segment by full resolution, a <c>$::</c>-prefixed name in
    ///     the global namespace, otherwise the qualification part by this same process and the
    ///     last segment by visible resolution in the namespace it resolves to.
    /// </summary>
    /// <param name="reference">The (possibly qualified) name to resolve.</param>
    /// <param name="localNamespace">
    ///     The qualified name of the local namespace, or null/empty for a root namespace.
    /// </param>
    /// <returns>The resolved element's qualified name, or null when it does not resolve.</returns>
    public string? Resolve(string reference, string? localNamespace) =>
        Resolve(reference, localNamespace, null);

    /// <summary>
    ///     Resolves <paramref name="reference"/> as <see cref="Resolve(string, string?)"/> does,
    ///     additionally consulting <paramref name="fileRootImports"/> — the imports written at the
    ///     root of the referencing file — for a reference whose local namespace is the file root
    ///     itself (a reference inside a top-level declaration finds them through
    ///     <see cref="RegisterRootImports"/>).
    /// </summary>
    internal string? Resolve(string reference, string? localNamespace, IReadOnlyList<SysmlImportNode>? fileRootImports)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var segments = SplitSegments(reference);
        if (segments.Count == 0)
        {
            return null;
        }

        if (segments[0] == "$")
        {
            return segments.Count == 2
                ? ResolveGlobal(segments[1])
                : ResolveSegments(segments.Skip(1).ToList(), null, true, null);
        }

        return ResolveSegments(segments, localNamespace, false, fileRootImports);
    }

    /// <summary>
    ///     Resolves a redefined-feature reference per KerML 8.2.3.5.1: the basic resolution is
    ///     repeated with each general type of <paramref name="owningType"/> as the local
    ///     namespace, in turn, until one resolves.
    /// </summary>
    /// <param name="reference">The redefined feature's (possibly qualified) name.</param>
    /// <param name="owningType">The qualified name of the redefining feature's owning type.</param>
    /// <returns>The redefined feature's qualified name, or null when it does not resolve.</returns>
    public string? ResolveRedefinedFeature(string reference, string owningType)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(owningType);
        foreach (var general in GeneralTypes(owningType))
        {
            if (Resolve(reference, general) is { } resolved)
            {
                return resolved;
            }
        }

        return null;
    }

    /// <summary>
    ///     Resolves the target of a <c>redefines</c>/<c>:&gt;&gt;</c> reference: per KerML
    ///     (<see cref="ResolveRedefinedFeature"/>), and — a deliberate lenient deviation, so a
    ///     qualified target such as <c>:&gt;&gt; ISQ::mass</c> still resolves in a type with no
    ///     resolvable general types — otherwise by ordinary resolution from the owning type. The
    ///     redefining feature itself (which the owning type owns under the same name for the
    ///     dominant bare <c>:&gt;&gt; name</c> shape) is never an acceptable target.
    /// </summary>
    /// <param name="reference">The redefined feature's (possibly qualified) name.</param>
    /// <param name="owningType">The redefining feature's owning type's qualified name, or empty at the file root.</param>
    /// <param name="redefiningFeature">The redefining feature's own qualified name, excluded as a target.</param>
    /// <param name="fileRootImports">The imports at the root of the referencing file, if any.</param>
    /// <returns>The redefined feature's qualified name, or null when it does not resolve.</returns>
    internal string? ResolveRedefinition(
        string reference, string owningType, string? redefiningFeature, IReadOnlyList<SysmlImportNode>? fileRootImports)
    {
        if (ResolveRedefinedFeature(reference, owningType) is { } redefined && redefined != redefiningFeature)
        {
            return redefined;
        }

        return Resolve(reference, owningType, fileRootImports) is { } fallback && fallback != redefiningFeature
            ? fallback
            : null;
    }

    /// <summary>
    ///     Resolves <paramref name="reference"/> per <see cref="Resolve(string, string?)"/>; when that fails, falls
    ///     back to matching the reference's last segment against every declaration's simple name
    ///     (choosing the first in declaration order), flagging the result as a fallback so callers
    ///     can surface a warning instead of silently guessing.
    /// </summary>
    /// <param name="reference">The (possibly qualified) name to resolve.</param>
    /// <param name="localNamespace">The local namespace's qualified name, or null.</param>
    /// <returns>The resolution outcome.</returns>
    public NameResolution ResolveWithFallback(string reference, string? localNamespace)
    {
        if (Resolve(reference, localNamespace) is { } resolved)
        {
            return new NameResolution(resolved, false, []);
        }

        var segments = SplitSegments(reference);
        if (segments.Count == 0)
        {
            return new NameResolution(null, false, []);
        }

        _bySimpleName ??= BuildSimpleNameIndex();
        return _bySimpleName.TryGetValue(segments[^1], out var candidates)
            ? new NameResolution(candidates[0], true, candidates)
            : new NameResolution(null, false, []);
    }

    /// <summary>
    ///     Returns the qualified names of the general types of the type <paramref name="typeName"/>
    ///     (its supertypes, and for a feature its types and redefined features), or an empty list
    ///     when it is not a type.
    /// </summary>
    /// <param name="typeName">The type's qualified name.</param>
    /// <returns>The general types' qualified names, without duplicates.</returns>
    public IReadOnlyList<string> GeneralTypes(string typeName)
    {
        if (!_declarations.TryGetValue(typeName, out var node) ||
            node is not (SysmlDefinitionNode or SysmlFeatureNode or SysmlViewNode or SysmlViewpointNode))
        {
            return [];
        }

        if (_declaredReferencesOnly)
        {
            return DeclaredGeneralTypes(typeName, node);
        }

        var resolved = node.ResolvedEdges
            .Where(e => e.Kind is SysmlEdgeKind.Supertype or SysmlEdgeKind.Typing or SysmlEdgeKind.Redefinition &&
                e.SourceQualifiedName == typeName)
            .Select(e => e.TargetQualifiedName)
            .Where(target => target != typeName)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // References without a resolved edge (e.g. standard library types, or a supertype the
        // semantic pass did not resolve): resolve the raw references, which per KerML 8.2.3.5.2
        // are resolved relative to the owning namespace of the specializing type, and merge them
        // with the resolved targets.
        var raw = RawGeneralReferences(node);
        if (resolved.Count >= raw.Count)
        {
            return resolved;
        }

        var owner = OwnerOf(typeName);
        var rootImports = RootImportsOf(typeName, owner);
        return resolved
            .Concat(raw.Select(reference => Resolve(reference, owner, rootImports)).OfType<string>())
            .Where(target => target != typeName)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    ///     The file-root imports that apply to <paramref name="typeName"/>'s own references when it
    ///     is itself a top-level declaration (<paramref name="owner"/> is null: no owning namespace
    ///     carries them); null for a nested type, whose outermost owning namespace carries them.
    /// </summary>
    private IReadOnlyList<SysmlImportNode>? RootImportsOf(string typeName, string? owner) =>
        owner is null ? _rootImportsByTopLevel.GetValueOrDefault(typeName) : null;

    /// <summary>The raw supertype, typing, and view-definition references that name a type's general types.</summary>
    private static List<string> RawGeneralReferences(SysmlNode node)
    {
        var raw = new List<string>(node.SupertypeNames);
        if (node is SysmlFeatureNode { FeatureTyping: { Length: > 0 } typing })
        {
            raw.Add(typing);
        }
        else if (node is SysmlViewNode { ViewDefinitionName: { Length: > 0 } viewDefinition })
        {
            raw.Add(viewDefinition);
        }

        return raw;
    }

    /// <summary>
    ///     General types derived purely from the type's raw references (supertypes, typing, view
    ///     definition, and redefined feature), resolved relative to its owning namespace per KerML
    ///     8.2.3.5.2 — independent of <see cref="SysmlNode.ResolvedEdges"/> and so of resolution
    ///     order. A type reached again while its own general types are being computed (a
    ///     specialization cycle) contributes nothing.
    /// </summary>
    private IReadOnlyList<string> DeclaredGeneralTypes(string typeName, SysmlNode node)
    {
        if (_declaredGeneralTypes.TryGetValue(typeName, out var cached))
        {
            return cached;
        }

        if (!_declaredGeneralTypesInProgress.Add(typeName))
        {
            return [];
        }

        try
        {
            var owner = OwnerOf(typeName);
            var rootImports = RootImportsOf(typeName, owner);
            var general = RawGeneralReferences(node)
                .Select(reference => Resolve(reference, owner, rootImports))
                .OfType<string>()
                .ToList();
            if (node is SysmlFeatureNode { RedefinedFeatureName: { Length: > 0 } redefined } &&
                ResolveRedefinition(redefined, owner ?? string.Empty, typeName, rootImports) is { } redefinedFeature)
            {
                general.Add(redefinedFeature);
            }

            var result = general
                .Where(target => target != typeName)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            _declaredGeneralTypes[typeName] = result;
            return result;
        }
        finally
        {
            _declaredGeneralTypesInProgress.Remove(typeName);
        }
    }

    /// <summary>Resolves a multi-segment name: the first by full (or global) resolution, the rest by visible resolution.</summary>
    private string? ResolveSegments(
        IReadOnlyList<string> segments, string? localNamespace, bool global, IReadOnlyList<SysmlImportNode>? fileRootImports)
    {
        var current = global
            ? ResolveGlobal(segments[0])
            : FullResolve(segments[0], localNamespace, fileRootImports);
        for (var i = 1; current is not null && i < segments.Count; i++)
        {
            current = Lookup(segments[i], current, Scope.Visible);
        }

        return current;
    }

    /// <summary>
    ///     Full resolution (KerML 8.2.3.5.4): local resolution in the namespace, then each owning
    ///     namespace outward, then the imports at the root of the file (when known), then the
    ///     global namespace.
    /// </summary>
    private string? FullResolve(string name, string? localNamespace, IReadOnlyList<SysmlImportNode>? fileRootImports)
    {
        string? topLevel = null;
        for (var scope = localNamespace; !string.IsNullOrEmpty(scope); scope = OwnerOf(scope))
        {
            topLevel = scope;
            if (Lookup(name, scope, Scope.Local) is { } found)
            {
                return found;
            }
        }

        var rootImports = topLevel is null ? fileRootImports : _rootImportsByTopLevel.GetValueOrDefault(topLevel);
        if (rootImports is not null)
        {
            foreach (var import in rootImports)
            {
                if (LookupImported(name, string.Empty, import) is { } imported)
                {
                    return imported;
                }
            }
        }

        return ResolveGlobal(name);
    }

    /// <summary>Resolves a simple name among the top-level elements of all root namespaces.</summary>
    private string? ResolveGlobal(string name)
    {
        if (_declarations.ContainsKey(name))
        {
            return name;
        }

        // A top-level element may also be found by its short name.
        return _declarations
            .Where(kvp => !kvp.Key.Contains("::", StringComparison.Ordinal) && kvp.Value.ShortName == name)
            .Select(kvp => kvp.Key)
            .FirstOrDefault();
    }

    /// <summary>
    ///     Finds the membership named <paramref name="name"/> (by name or short name) in
    ///     <paramref name="namespaceName"/> among the memberships <paramref name="scope"/> admits,
    ///     returning its member element's qualified name.
    /// </summary>
    private string? Lookup(string name, string namespaceName, Scope scope)
    {
        var key = (name, namespaceName, scope);
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        // Imports and specializations may be circular (KerML 8.2.3.5.1): a lookup already in
        // progress contributes nothing rather than recursing forever.
        if (!_inProgress.Add(key))
        {
            return null;
        }

        try
        {
            var result = LookupUncached(name, namespaceName, scope);
            _cache[key] = result;
            return result;
        }
        finally
        {
            _inProgress.Remove(key);
        }
    }

    /// <summary>Owned, then imported, then inherited memberships, as <see cref="Lookup"/> describes.</summary>
    private string? LookupUncached(string name, string namespaceName, Scope scope)
    {
        // An element whose qualified name is "<namespace>::<name>" is owned by that namespace by
        // construction, even when the namespace node itself is absent or does not list it.
        if (_declarations.TryGetValue(namespaceName + "::" + name, out var direct) &&
            direct is not (SysmlImportNode or SysmlAliasNode) && Admits(direct.Visibility, scope))
        {
            return namespaceName + "::" + name;
        }

        if (!_declarations.TryGetValue(namespaceName, out var ns))
        {
            return null;
        }

        // Owned memberships (including aliases).
        foreach (var child in ns.Children)
        {
            if (child is SysmlImportNode || !Admits(child.Visibility, scope) ||
                (child.Name != name && child.ShortName != name))
            {
                continue;
            }

            var target = child is SysmlAliasNode alias
                ? Resolve(alias.TargetName, namespaceName)
                : child.QualifiedName;
            if (target is not null)
            {
                return target;
            }
        }

        // Imported memberships.
        foreach (var import in ns.Children.OfType<SysmlImportNode>())
        {
            if (Admits(import.Visibility, scope) &&
                LookupImported(name, namespaceName, import) is { } imported)
            {
                return imported;
            }
        }

        // Inherited memberships of a type: the non-private memberships of its general types
        // (only the public ones for visible resolution).
        var inheritedScope = scope == Scope.Visible ? Scope.Visible : Scope.Inheritable;
        foreach (var general in GeneralTypes(namespaceName))
        {
            if (Lookup(name, general, inheritedScope) is { } inherited)
            {
                return inherited;
            }
        }

        return null;
    }

    /// <summary>
    ///     Finds <paramref name="name"/> among the memberships one import of
    ///     <paramref name="importingNamespace"/> brings in (KerML 8.3.2.4.4/8.3.2.4.6): a membership
    ///     import brings in the named membership (and, if recursive, its namespace's members
    ///     recursively); a namespace import brings in the namespace's members (recursively, if
    ///     recursive). Only visible memberships are imported unless the import is <c>import all</c>.
    /// </summary>
    private string? LookupImported(string name, string importingNamespace, SysmlImportNode import)
    {
        var memberScope = import.IsImportAll ? Scope.Local : Scope.Visible;
        string? found = null;
        if (import.IsMembershipImport)
        {
            var target = Resolve(import.ImportedNamespace, importingNamespace);
            if (target is null)
            {
                return null;
            }

            var lastSegment = SplitSegments(import.ImportedNamespace)[^1];
            if (lastSegment == name ||
                (_declarations.TryGetValue(target, out var targetNode) && targetNode.ShortName == name) ||
                ImportedAliasShortName(import, importingNamespace) == name)
            {
                found = target;
            }
            else if (import.IsRecursive)
            {
                return LookupRecursive(name, target, memberScope, candidate => PassesImportFilter(import, candidate));
            }
        }
        else
        {
            var namespaceTarget = Resolve(import.ImportedNamespace, importingNamespace);
            if (namespaceTarget is null)
            {
                return null;
            }

            if (import.IsRecursive)
            {
                return LookupRecursive(name, namespaceTarget, memberScope, candidate => PassesImportFilter(import, candidate));
            }

            found = Lookup(name, namespaceTarget, memberScope);
        }

        return found is not null && PassesImportFilter(import, found) ? found : null;
    }

    /// <summary>
    ///     Returns the short name of the alias membership a membership import names (the alias
    ///     itself, not its target), or null when the imported membership is not a short-named alias.
    /// </summary>
    private string? ImportedAliasShortName(SysmlImportNode import, string importingNamespace)
    {
        var segments = SplitSegments(import.ImportedNamespace);
        var owner = segments.Count > 1
            ? Resolve(string.Join("::", segments.Take(segments.Count - 1)), importingNamespace)
            : null;
        if (owner is null || !_declarations.TryGetValue(owner, out var ownerNode))
        {
            return null;
        }

        return ownerNode.Children
            .OfType<SysmlAliasNode>()
            .FirstOrDefault(alias => alias.Name == segments[^1])?.ShortName;
    }

    /// <summary>Returns whether <paramref name="candidate"/> satisfies a filtered import's conditions (always true for an unfiltered import).</summary>
    private bool PassesImportFilter(SysmlImportNode import, string candidate) =>
        import.BracketFilterExpressionText is null || _importFilter is null || _importFilter(import, candidate);

    /// <summary>
    ///     Breadth-first lookup of <paramref name="name"/> in <paramref name="namespaceName"/> and,
    ///     recursively, in each of its owned member namespaces, so the least-nested match wins. A
    ///     match that <paramref name="accept"/> rejects (a filtered import's conditions) is skipped
    ///     and the search continues.
    /// </summary>
    private string? LookupRecursive(string name, string namespaceName, Scope scope, Func<string, bool> accept)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(namespaceName);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current))
            {
                continue;
            }

            if (Lookup(name, current, scope) is { } found && accept(found))
            {
                return found;
            }

            if (_declarations.TryGetValue(current, out var node))
            {
                foreach (var child in node.Children)
                {
                    if (child.QualifiedName is { Length: > 0 } childName && Admits(child.Visibility, scope))
                    {
                        queue.Enqueue(childName);
                    }
                }
            }
        }

        return null;
    }

    /// <summary>Returns whether a membership with <paramref name="visibility"/> is admitted by <paramref name="scope"/>.</summary>
    private static bool Admits(SysmlVisibility visibility, Scope scope) => scope switch
    {
        Scope.Local => true,
        Scope.Inheritable => visibility != SysmlVisibility.Private,
        _ => visibility == SysmlVisibility.Public,
    };

    /// <summary>Returns the owning namespace's qualified name (the prefix before the last <c>::</c>), or null at the root.</summary>
    private static string? OwnerOf(string qualifiedName)
    {
        var sep = qualifiedName.LastIndexOf("::", StringComparison.Ordinal);
        return sep >= 0 ? qualifiedName[..sep] : null;
    }

    /// <summary>Splits a qualified name into its segments, keeping <c>::</c> inside quoted names intact.</summary>
    private static List<string> SplitSegments(string reference)
    {
        var segments = new List<string>();
        var start = 0;
        var quoted = false;
        var i = 0;
        while (i < reference.Length)
        {
            if (reference[i] == '\'')
            {
                quoted = !quoted;
            }
            else if (!quoted && i + 1 < reference.Length && reference[i] == ':' && reference[i + 1] == ':')
            {
                segments.Add(reference[start..i]);
                start = i + 2;
                i += 2;
                continue;
            }

            i++;
        }

        segments.Add(reference[start..]);
        return segments.Where(s => s.Length > 0).ToList();
    }

    /// <summary>Indexes every declaration by its last name segment, in declaration order.</summary>
    private Dictionary<string, List<string>> BuildSimpleNameIndex()
    {
        var index = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var qualifiedName in _declarations.Keys)
        {
            var simple = SplitSegments(qualifiedName)[^1];
            if (!index.TryGetValue(simple, out var list))
            {
                list = [];
                index[simple] = list;
            }

            list.Add(qualifiedName);
        }

        return index;
    }
}
