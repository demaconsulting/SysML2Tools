// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

namespace DemaConsulting.SysML2Tools.Semantic.Model;

/// <summary>
///     The ambiguity-aware half of <see cref="NameResolver"/>: the same name resolution, but every
///     step reports every distinct element a name can denote instead of the first one found.
/// </summary>
/// <remarks>
///     Model resolution (<see cref="Resolve(string, string?)"/> and the lookups beneath it) takes the
///     first match at each step, which is a deliberate and unchanged choice: KerML leaves the winner
///     of a name conflict unspecified. A caller that must not guess — the Query element lookup —
///     uses <see cref="ResolveCandidates"/>, which mirrors that resolution step for step and keeps
///     every alternative, so no first-match shortcut remains on any path:
///     <list type="bullet">
///         <item>the first segment: owned memberships of each owning namespace outward, then the
///             file-root imports, then the global namespace (declared names, root aliases by name,
///             short names, and root aliases by short name, all of them);</item>
///         <item>owned memberships by name or short name, with every alias target resolved the same
///             way;</item>
///         <item>imported memberships: membership imports, plain and recursive namespace imports
///             (every match at every depth, not the least-nested one), with a filtered import's
///             conditions applied to each candidate, and the import's own target resolved the same
///             way;</item>
///         <item>inherited memberships of every general type.</item>
///     </list>
///     The precedence between those layers (owned before imported before inherited, local before
///     outer before global) is the model's and is kept; ambiguity is reported within a layer.
/// </remarks>
public sealed partial class NameResolver
{
    /// <summary>
    ///     The most evaluation rounds one <see cref="ResolveCandidates"/> call runs while chasing a
    ///     fixed point through circular imports; a safety net, as real models settle in two or three.
    /// </summary>
    private const int MaxCandidateRounds = 32;

    /// <summary>
    ///     Identifies one memoized candidate lookup of a single enumeration round: a namespace's
    ///     visible candidates for a name (<paramref name="AliasIndex"/> is -1), or the targets of
    ///     one root alias (<paramref name="AliasIndex"/> indexes the registered root aliases).
    /// </summary>
    private readonly record struct CandidateKey(int AliasIndex, string Name, string Namespace, Scope Scope);

    /// <summary>
    ///     The lookups the current round is inside, so circular imports, aliases, and
    ///     specializations are cut on re-entry instead of recursing forever (the spec's
    ///     <c>excluded</c> set, KerML 8.3.2.4.2 and 8.3.2.4.5).
    /// </summary>
    private readonly HashSet<CandidateKey> _candidatesInProgress = new();

    /// <summary>The lookups the current round has finished, each computed exactly once per round.</summary>
    private readonly Dictionary<CandidateKey, List<string>> _candidateResults = new();

    /// <summary>
    ///     What the previous round found for each lookup; a cut on a lookup still in progress
    ///     answers with this, so a cycle's content flows around it until the answers stop changing.
    /// </summary>
    private Dictionary<CandidateKey, List<string>> _candidateApproximations = new();

    /// <summary>The top-level short-name scan of <see cref="TopLevelShortNameMatches"/>, by name.</summary>
    private readonly Dictionary<string, List<string>> _topLevelShortNameMatches = new(StringComparer.Ordinal);

    /// <summary>Whether the current round cut a lookup that was still in progress.</summary>
    private bool _candidateCycleCut;

    /// <summary>
    ///     Resolves the qualified name <paramref name="reference"/> in the global namespace like
    ///     <see cref="Resolve(string, string?)"/> does, but reports every distinct element it can
    ///     denote rather than the first one found, so a caller can reject an ambiguous name. A name
    ///     reached through two imports of one namespace that bring in different elements of the same
    ///     name is ambiguous, as is a name two root aliases or short names share; the same element
    ///     reached twice is not. Model resolution is unchanged and keeps taking the first match.
    /// </summary>
    /// <param name="reference">
    ///     The (possibly qualified or <c>$::</c>-prefixed) name to resolve. A dotted feature chain is
    ///     not analyzed for ambiguity and yields what <see cref="Resolve(string, string?)"/> finds.
    /// </param>
    /// <returns>
    ///     The distinct resolved elements' qualified names: none when the name does not resolve, one
    ///     when it is unambiguous, several when it is ambiguous.
    /// </returns>
    public IReadOnlyList<string> ResolveCandidates(string reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        // Every lookup is computed once per round and remembered, so shared sub-graphs of imports
        // (a diamond of facades) cost one evaluation instead of one per path. A lookup cut short
        // because it is still in progress (a cycle) answers with the previous round's result, and
        // the rounds repeat until nothing changes, so a cycle contributes its full content
        // regardless of where the walk entered it. An acyclic model settles in one round.
        _candidateApproximations = new Dictionary<CandidateKey, List<string>>();
        try
        {
            var round = 0;
            while (true)
            {
                round++;
                _candidateResults.Clear();
                _candidatesInProgress.Clear();
                _candidateCycleCut = false;
                var result = ReferenceCandidates(reference, null, null);
                if (!_candidateCycleCut || round >= MaxCandidateRounds || CandidateRoundSettled())
                {
                    return result.ToList();
                }

                _candidateApproximations = new Dictionary<CandidateKey, List<string>>(_candidateResults);
            }
        }
        finally
        {
            _candidateResults.Clear();
            _candidatesInProgress.Clear();
            _candidateApproximations = new Dictionary<CandidateKey, List<string>>();
        }
    }

    /// <summary>
    ///     Returns whether the round just finished found, for every lookup it made, the same
    ///     elements as the approximation it was given, so another round cannot change anything.
    /// </summary>
    private bool CandidateRoundSettled() =>
        _candidateResults.All(entry =>
            _candidateApproximations.TryGetValue(entry.Key, out var previous)
                ? entry.Value.Count == previous.Count && entry.Value.ToHashSet(StringComparer.Ordinal).SetEquals(previous)
                : entry.Value.Count == 0);

    /// <summary>
    ///     Computes <paramref name="compute"/> once per round for <paramref name="key"/>. A lookup
    ///     re-entered while it is still being computed (a circular import, alias, or
    ///     specialization) is cut and answers with the previous round's approximation.
    /// </summary>
    /// <returns>The candidates; shared between callers, so the caller must not modify the list.</returns>
    private List<string> Memoized(CandidateKey key, Func<List<string>> compute)
    {
        if (_candidateResults.TryGetValue(key, out var done))
        {
            return done;
        }

        if (!_candidatesInProgress.Add(key))
        {
            _candidateCycleCut = true;
            return _candidateApproximations.GetValueOrDefault(key) ?? [];
        }

        try
        {
            var computed = compute();
            _candidateResults[key] = computed;
            return computed;
        }
        finally
        {
            _candidatesInProgress.Remove(key);
        }
    }

    /// <summary>
    ///     Resolves <paramref name="reference"/> relative to <paramref name="localNamespace"/> like
    ///     <see cref="Resolve(string, string?, IReadOnlyList{SysmlImportNode}?)"/>, keeping every
    ///     distinct candidate. A dotted feature chain falls back to the single answer of
    ///     <see cref="Resolve(string, string?, IReadOnlyList{SysmlImportNode}?)"/>.
    /// </summary>
    private List<string> ReferenceCandidates(
        string reference, string? localNamespace, IReadOnlyList<SysmlImportNode>? fileRootImports)
    {
        if (SplitChain(reference).Count > 1)
        {
            return Resolve(reference, localNamespace, fileRootImports) is { } single ? [single] : [];
        }

        var segments = SplitSegments(reference);
        if (segments.Count == 0)
        {
            return [];
        }

        // The first segment is resolved by full (or, after $::, global) resolution; every later one
        // by visible resolution in each element the previous segment may denote.
        List<string> current;
        int next;
        if (segments[0] == "$")
        {
            if (segments.Count < 2)
            {
                return [];
            }

            current = GlobalCandidates(segments[1]);
            next = 2;
        }
        else
        {
            current = FullCandidates(segments[0], localNamespace, fileRootImports);
            next = 1;
        }

        for (var i = next; current.Count > 0 && i < segments.Count; i++)
        {
            var segment = segments[i];
            current = current.SelectMany(ns => VisibleCandidates(segment, ns, Scope.Visible)).Distinct().ToList();
        }

        return current;
    }

    /// <summary>
    ///     Full resolution (KerML 8.2.3.5.4) keeping every candidate of the first layer that has
    ///     any: the namespace, each owning namespace outward, the file's root imports, then the
    ///     global namespace.
    /// </summary>
    private List<string> FullCandidates(
        string name, string? localNamespace, IReadOnlyList<SysmlImportNode>? fileRootImports)
    {
        for (var scope = localNamespace; !string.IsNullOrEmpty(scope); scope = OwnerOf(scope))
        {
            var found = VisibleCandidates(name, scope, Scope.Local);
            if (found.Count > 0)
            {
                return found;
            }
        }

        var rootImports = fileRootImports ??
            (string.IsNullOrEmpty(localNamespace) ? null : _rootImportsByDeclaration.GetValueOrDefault(localNamespace));
        if (rootImports is not null)
        {
            var imported = rootImports
                .SelectMany(import => ImportedCandidates(name, string.Empty, import))
                .Distinct()
                .ToList();
            if (imported.Count > 0)
            {
                return imported;
            }
        }

        return GlobalCandidates(name);
    }

    /// <summary>
    ///     Every distinct element a simple name denotes among the global namespace's memberships:
    ///     a top-level declaration written with or without quotes, a root alias by name, a
    ///     top-level declaration by short name, and a root alias by short name.
    /// </summary>
    private List<string> GlobalCandidates(string name)
    {
        var candidates = new List<string>();
        if (_declarations.ContainsKey(name))
        {
            candidates.Add(name);
        }

        // A top-level element may be written with or without enclosing quotes.
        var otherForm = name.Length >= 2 && name[0] == '\'' && name[^1] == '\''
            ? name[1..^1]
            : "'" + name + "'";
        if (_declarations.ContainsKey(otherForm))
        {
            candidates.Add(otherForm);
        }

        candidates.AddRange(RootAliasCandidates(name, byShortName: false));
        candidates.AddRange(TopLevelShortNameMatches(name));
        candidates.AddRange(RootAliasCandidates(name, byShortName: true));
        return candidates.Distinct().ToList();
    }

    /// <summary>
    ///     The top-level declarations whose short name is <paramref name="name"/>. The scan reads
    ///     every declaration of the workspace, standard library included, and the answer depends on
    ///     nothing but the name, so each name is scanned once for the resolver's lifetime rather
    ///     than once per import of every namespace the enumeration visits.
    /// </summary>
    private List<string> TopLevelShortNameMatches(string name)
    {
        if (!_topLevelShortNameMatches.TryGetValue(name, out var matches))
        {
            matches = _declarations
                .Where(kvp => !kvp.Key.Contains("::", StringComparison.Ordinal) && NamesEqual(kvp.Value.ShortName, name))
                .Select(kvp => kvp.Key)
                .ToList();
            _topLevelShortNameMatches[name] = matches;
        }

        return matches;
    }

    /// <summary>
    ///     The targets of every root-level alias named <paramref name="name"/> (by declared name or
    ///     by short name), each resolved from the root of its own file. An alias already being
    ///     followed contributes nothing, so a cycle of aliases ends unresolved.
    /// </summary>
    private List<string> RootAliasCandidates(string name, bool byShortName)
    {
        var candidates = new List<string>();
        for (var index = 0; index < _rootAliases.Count; index++)
        {
            var (alias, rootImports) = _rootAliases[index];
            if (!NamesEqual(byShortName ? alias.ShortName : alias.Name, name))
            {
                continue;
            }

            // Each alias's targets are resolved once per round; an alias already being followed
            // is cut (a cycle of aliases adds only what the previous round found).
            candidates.AddRange(Memoized(
                new CandidateKey(index, string.Empty, string.Empty, Scope.Visible),
                () => ReferenceCandidates(alias.TargetName, null, rootImports)));
        }

        return candidates;
    }

    /// <summary>
    ///     Every distinct element <paramref name="name"/> denotes among the memberships of
    ///     <paramref name="namespaceName"/> that <paramref name="scope"/> admits, taking the first
    ///     layer that has any: the owned memberships, else the memberships the admitted imports
    ///     bring in, else the memberships inherited from every general type.
    /// </summary>
    private List<string> VisibleCandidates(string name, string namespaceName, Scope scope) =>
        Memoized(
            new CandidateKey(-1, name, namespaceName, scope),
            () => VisibleCandidatesUncached(name, namespaceName, scope));

    /// <summary>The uncached body of <see cref="VisibleCandidates"/>: owned, else imported, else inherited.</summary>
    private List<string> VisibleCandidatesUncached(string name, string namespaceName, Scope scope)
    {
        var owned = OwnedCandidates(name, namespaceName, scope);
        if (owned.Count > 0 || !_declarations.TryGetValue(namespaceName, out var ns))
        {
            return owned;
        }

        var imported = ns.Children
            .OfType<SysmlImportNode>()
            .Where(import => Admits(import.Visibility, scope))
            .SelectMany(import => ImportedCandidates(name, namespaceName, import))
            .Distinct()
            .ToList();
        if (imported.Count > 0)
        {
            return imported;
        }

        // Inherited memberships: every general type's, except the features the type's own
        // features redefine (the targets of a redefinition are resolved without that exclusion).
        var inheritedScope = InheritedScopeOf(scope);
        return LookupGeneralTypes(namespaceName)
            .SelectMany(general => VisibleCandidates(name, general, inheritedScope))
            .Where(inherited => scope == Scope.RedefinitionTarget ||
                                !IsRedefinedByOwnedFeature(ns, namespaceName, inherited))
            .Distinct()
            .ToList();
    }

    /// <summary>
    ///     Every distinct element the owned memberships named <paramref name="name"/> denote; an
    ///     alias contributes every candidate of its target.
    /// </summary>
    private List<string> OwnedCandidates(string name, string namespaceName, Scope scope) =>
        EnumerateOwned(name, namespaceName, scope)
            .SelectMany(membership => OwnedTargets(membership, namespaceName))
            .Distinct()
            .ToList();

    /// <summary>
    ///     The elements one owned membership denotes: its own element, or every candidate of an
    ///     alias's target.
    /// </summary>
    private List<string> OwnedTargets(OwnedMembership membership, string namespaceName)
    {
        if (membership.Alias is { } alias)
        {
            return ReferenceCandidates(alias.TargetName, namespaceName, null);
        }

        return membership.QualifiedName is { } qualified ? [qualified] : [];
    }

    /// <summary>
    ///     Every distinct element <paramref name="name"/> denotes among the memberships one import of
    ///     <paramref name="importingNamespace"/> brings in (mirroring <see cref="LookupImported"/>):
    ///     the named membership of a membership import, every match anywhere below the target of a
    ///     recursive import, or every candidate among the target's visible memberships; each
    ///     candidate must pass a filtered import's conditions. The import's own target is resolved
    ///     with candidates too, so an ambiguous target contributes every alternative.
    /// </summary>
    private List<string> ImportedCandidates(string name, string importingNamespace, SysmlImportNode import)
    {
        var memberScope = import.IsImportAll ? Scope.Local : Scope.Visible;
        var candidates = new List<string>();
        foreach (var target in ReferenceCandidates(import.ImportedNamespace, importingNamespace, null))
        {
            if (import.IsMembershipImport && ImportNamesMember(import, importingNamespace, target, name))
            {
                if (PassesImportFilter(import, target))
                {
                    candidates.Add(target);
                }
            }
            else if (import.IsRecursive)
            {
                candidates.AddRange(RecursiveCandidates(
                    name, target, memberScope, candidate => PassesImportFilter(import, candidate)));
            }
            else if (!import.IsMembershipImport)
            {
                candidates.AddRange(
                    VisibleCandidates(name, target, memberScope).Where(candidate => PassesImportFilter(import, candidate)));
            }
        }

        return candidates.Distinct().ToList();
    }

    /// <summary>
    ///     Returns whether the membership import brings in <paramref name="target"/> under
    ///     <paramref name="name"/>: by the imported name's last segment, the target's short name, or
    ///     the short name of the alias the import names.
    /// </summary>
    private bool ImportNamesMember(SysmlImportNode import, string importingNamespace, string target, string name)
    {
        var segments = SplitSegments(import.ImportedNamespace);
        if (segments.Count == 0)
        {
            return false;
        }

        return NamesEqual(segments[^1], name) ||
               (_declarations.TryGetValue(target, out var targetNode) && NamesEqual(targetNode.ShortName, name)) ||
               ImportedAliasShortNames(import, importingNamespace).Any(shortName => NamesEqual(shortName, name));
    }

    /// <summary>
    ///     The short names of the alias memberships a membership import may name (the alias itself,
    ///     not its target), over every candidate of the alias's owner.
    /// </summary>
    private IEnumerable<string?> ImportedAliasShortNames(SysmlImportNode import, string importingNamespace)
    {
        var segments = SplitSegments(import.ImportedNamespace);
        if (segments.Count < 2)
        {
            return [];
        }

        return ReferenceCandidates(string.Join("::", segments.Take(segments.Count - 1)), importingNamespace, null)
            .Select(owner => _declarations.GetValueOrDefault(owner))
            .OfType<SysmlNode>()
            .SelectMany(ownerNode => ownerNode.Children.OfType<SysmlAliasNode>())
            .Where(alias => NamesEqual(alias.Name, segments[^1]))
            .Select(alias => alias.ShortName);
    }

    /// <summary>
    ///     Every distinct match of <paramref name="name"/> in <paramref name="namespaceName"/> and,
    ///     recursively, in each of its owned member namespaces — all depths, not the least-nested
    ///     one — keeping the matches <paramref name="accept"/> admits (a filtered import's conditions).
    /// </summary>
    private List<string> RecursiveCandidates(string name, string namespaceName, Scope scope, Func<string, bool> accept)
    {
        var candidates = new List<string>();
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

            candidates.AddRange(VisibleCandidates(name, current, scope).Where(accept));

            if (_declarations.TryGetValue(current, out var node))
            {
                foreach (var childName in node.Children
                             .Where(child => Admits(child.Visibility, scope))
                             .Select(child => child.QualifiedName)
                             .OfType<string>()
                             .Where(childName => childName.Length > 0))
                {
                    queue.Enqueue(childName);
                }
            }
        }

        return candidates.Distinct().ToList();
    }
}
