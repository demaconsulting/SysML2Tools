// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

namespace DemaConsulting.SysML2Tools.Semantic.Model;

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
///     Inherited memberships also follow the <em>implicit</em> specializations KerML and SysML
///     give every kind of element (a part definition specializes <c>Parts::Part</c>, a part usage
///     subsets <c>Parts::parts</c>, a state definition specializes <c>States::StateAction</c>, a
///     variant its variation, a usage typed by a conjugated port definition that port definition,
///     and so on): they are appended after the declared general types, so library members such as
///     <c>self</c>, <c>start</c> or <c>shape</c> resolve without an explicit supertype, and an
///     explicit supertype wins a name conflict. Implicit specializations are lookup input only —
///     never <see cref="SysmlEdge"/>s and not part of the public <see cref="GeneralTypes"/> — so
///     renderers, queries and exports see exactly the declared relationships. Features a type's
///     own named features redefine are not inherited (KerML <c>removeRedefinedFeatures</c>),
///     except when the target of a redefinition is being resolved.
///     </para>
///     <para>
///     <see cref="ReferenceResolver"/> uses an internal <em>resolving</em> mode while the
///     workspace's edges are still being attached: general types are then derived only from each
///     type's raw references (never from <see cref="SysmlNode.ResolvedEdges"/>), which makes
///     every answer independent of the order in which nodes are visited and keeps the cache valid
///     as edges appear. In that mode the imports written at the root of a source file (outside
///     any package) apply to the declarations of that file, as KerML scopes them.
///     </para>
/// </remarks>
public sealed class NameResolver
{
    /// <summary>
    ///     The outcome of resolving a (possibly dotted) feature-chain reference.
    /// </summary>
    /// <param name="Element">The qualified name of the declared element the last segment resolves to.</param>
    /// <param name="InstancePath">
    ///     The qualified name of the last feature as seen through the chain (see
    ///     <see cref="ResolveFeatureChain"/>); equal to <paramref name="Element"/> without a chain.
    /// </param>
    internal readonly record struct FeatureChainResolution(string Element, string InstancePath);

    /// <summary>Which memberships of a namespace a lookup considers.</summary>
    private enum Scope
    {
        /// <summary>Local resolution: all owned and imported memberships, inherited non-private ones.</summary>
        Local,

        /// <summary>Memberships a specialization inherits: everything except private.</summary>
        Inheritable,

        /// <summary>
        ///     As <see cref="Inheritable"/>, for resolving the target of a redefinition: members
        ///     the general types' own features redefine are still found.
        /// </summary>
        RedefinitionTarget,

        /// <summary>Visible resolution: public memberships only.</summary>
        Visible,
    }

    private readonly IReadOnlyDictionary<string, SysmlNode> _declarations;
    private readonly Func<SysmlImportNode, string, bool>? _importFilter;
    private readonly bool _declaredReferencesOnly;
    private readonly Dictionary<(string Name, string Namespace, Scope Scope), string?> _cache = new();
    private readonly Dictionary<(string Name, string Namespace, Scope Scope), int> _inProgress = new();
    private readonly Dictionary<string, IReadOnlyList<string>> _declaredGeneralTypes = new(StringComparer.Ordinal);
    private readonly HashSet<string> _declaredGeneralTypesInProgress = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _referenceSubsettingTargets = new(StringComparer.Ordinal);
    private readonly HashSet<string> _referenceSubsettingInProgress = new(StringComparer.Ordinal);
    private int _generalTypeReentries;
    private int _lowestCutDepth = int.MaxValue;
    private readonly Dictionary<(string Name, string Namespace, Scope Scope), int> _provisionalMisses = new();
    private readonly List<((string Name, string Namespace, Scope Scope) Key, int CutDepth)> _provisionalOrder = new();
    private readonly Dictionary<string, HashSet<string>> _redefinedMembers = new(StringComparer.Ordinal);
    private readonly HashSet<string> _redefinedMembersInProgress = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<SysmlImportNode>> _rootImportsByDeclaration = new(StringComparer.Ordinal);
    private readonly List<(SysmlAliasNode Alias, IReadOnlyList<SysmlImportNode> RootImports)> _rootAliases = new();
    private readonly HashSet<SysmlAliasNode> _rootAliasesInProgress = new();

    // Implicit specialization tables (KerML 7.3.4 / SysML v2 default general types).
    // Source: the OMG Pilot Implementation's ImplicitGeneralizationMap
    // (org.omg.sysml.logic/.../util/ImplicitGeneralizationMap.java) and the "Default general
    // types" clauses of KerML 9.2 / SysML 8.3 for each kind, restricted to the targets the
    // vendored standard library declares. The keys are the DefinitionKeyword/FeatureKeyword
    // strings AstBuilder emits; a usage keyword with no row falls back to UnmappedFeatureType.

    /// <summary>The library type every view (definition or usage) implicitly specializes.</summary>
    private const string ViewImplicitType = "Views::View";

    /// <summary>The library type every viewpoint definition implicitly specializes.</summary>
    private const string ViewpointImplicitType = "Views::ViewpointCheck";

    /// <summary>The library feature every transition usage implicitly subsets (it owns <c>accepter</c> and <c>effect</c>).</summary>
    private const string TransitionImplicitType = "Actions::transitionActions";

    /// <summary>The library feature a usage with no keyword row implicitly subsets.</summary>
    private const string UnmappedFeatureType = "Base::things";

    /// <summary>The library types a definition implicitly specializes, by definition keyword.</summary>
    private static readonly Dictionary<string, string[]> DefinitionImplicitTypes = new(StringComparer.Ordinal)
    {
        ["part def"] = ["Parts::Part"],
        ["item def"] = ["Items::Item"],
        ["port def"] = ["Ports::Port"],
        ["attribute def"] = ["Base::DataValue"],
        ["enum def"] = ["Base::DataValue"],
        ["action def"] = ["Actions::Action"],
        ["state def"] = ["States::StateAction"],
        ["calc def"] = ["Calculations::Calculation"],
        ["constraint def"] = ["Constraints::ConstraintCheck"],
        ["requirement def"] = ["Requirements::RequirementCheck"],
        ["concern def"] = ["Requirements::ConcernCheck"],
        ["case def"] = ["Cases::Case"],
        ["analysis def"] = ["AnalysisCases::AnalysisCase"],
        ["verification def"] = ["VerificationCases::VerificationCase"],
        ["use case def"] = ["UseCases::UseCase"],
        ["connection def"] = ["Connections::Connection"],
        ["interface def"] = ["Interfaces::Interface"],
        ["allocation def"] = ["Allocations::Allocation"],
        ["flow def"] = ["Flows::MessageAction"],
        ["occurrence def"] = ["Occurrences::Occurrence"],
        ["individual def"] = ["Occurrences::Occurrence"],
        ["metadata def"] = ["Metadata::MetadataItem"],
        ["rendering def"] = ["Views::Rendering"],
        ["class"] = ["Occurrences::Occurrence"],
        ["struct"] = ["Objects::Object"],
        ["assoc struct"] = ["Objects::LinkObject"],
        ["assoc"] = ["Links::Link"],
        ["datatype"] = ["Base::DataValue"],
        ["classifier"] = ["Base::Anything"],
        ["type"] = ["Base::Anything"],
        ["metaclass"] = ["Metaobjects::Metaobject"],
        ["behavior"] = ["Performances::Performance"],
        ["function"] = ["Performances::Evaluation"],
        ["predicate"] = ["Performances::BooleanEvaluation"],
        ["interaction"] = ["Links::Link", "Performances::Performance"],
    };

    /// <summary>The library features a usage implicitly subsets, by usage keyword.</summary>
    private static readonly Dictionary<string, string[]> FeatureImplicitTypes = new(StringComparer.Ordinal)
    {
        ["part"] = ["Parts::parts"],
        ["actor"] = ["Parts::parts"],
        ["stakeholder"] = ["Parts::parts"],
        ["port"] = ["Ports::ports"],
        ["item"] = ["Items::items"],
        ["attribute"] = ["Base::dataValues"],
        ["enum"] = ["Base::dataValues"],
        ["enum value"] = ["Base::dataValues"],
        ["action"] = ["Actions::actions"],
        ["entry"] = ["Actions::actions"],
        ["do"] = ["Actions::actions"],
        ["exit"] = ["Actions::actions"],
        ["merge"] = ["Actions::actions"],
        ["decide"] = ["Actions::actions"],
        ["join"] = ["Actions::actions"],
        ["fork"] = ["Actions::actions"],
        ["accept"] = ["Actions::acceptActions"],
        ["send"] = ["Actions::sendActions"],
        ["terminate"] = ["Actions::terminateActions"],
        ["state"] = ["States::stateActions"],
        ["exhibit"] = ["States::stateActions"],
        ["perform"] = ["Actions::actions"],
        ["include"] = ["UseCases::useCases"],
        ["assert constraint"] = ["Constraints::constraintChecks"],
        ["timeslice"] = ["Occurrences::Occurrence::timeSlices"],
        ["snapshot"] = ["Occurrences::Occurrence::snapshots"],
        ["calc"] = ["Calculations::calculations"],
        ["constraint"] = ["Constraints::constraintChecks"],
        ["assume constraint"] = ["Constraints::constraintChecks"],
        ["require constraint"] = ["Constraints::constraintChecks"],
        ["requirement"] = ["Requirements::requirementChecks"],
        ["concern"] = ["Requirements::concernChecks"],
        ["case"] = ["Cases::cases"],
        ["analysis"] = ["AnalysisCases::analysisCases"],
        ["verification"] = ["VerificationCases::verificationCases"],
        ["use case"] = ["UseCases::useCases"],
        ["rendering"] = ["Views::renderings"],
        ["occurrence"] = ["Occurrences::occurrences"],
        ["event occurrence"] = ["Occurrences::occurrences"],
        ["step"] = ["Performances::performances"],
        ["expr"] = ["Performances::evaluations"],
        ["bool"] = ["Performances::booleanEvaluations"],
        ["inv"] = ["Performances::trueEvaluations"],
        ["connector"] = ["Links::links"],
        ["binding"] = ["Links::selfLinks"],
        ["succession"] = ["Occurrences::happensBeforeLinks"],
        ["flow"] = ["Transfers::flowTransfers", "Transfers::transfers"],
        ["succession flow"] =
        [
            "Transfers::flowTransfersBefore", "Transfers::flowTransfers", "Transfers::transfers",
            "Occurrences::happensBeforeLinks",
        ],
    };

    /// <summary>
    ///     The library features a connection-like usage (<see cref="SysmlConnectionNode"/>)
    ///     implicitly subsets, by connection keyword. Each row starts with the specification's
    ///     default (e.g. <c>Flows::flows</c> for a flow usage) and continues with that feature's
    ///     own subsetted features. The library declares its connection-like features
    ///     (<c>Connections::connections</c>, <c>Flows::flows</c>, <c>Transfers::flowTransfers</c>, ...)
    ///     with connection syntax, which <see cref="SysmlConnectionNode"/> records without any
    ///     supertypes or typing, so the default alone is a lookup dead end; the listed ancestors
    ///     (<c>Parts::parts</c>, <c>Transfers::transfers</c>, <c>Actions::actions</c>, ...) are the
    ///     nearest ones that do carry members.
    /// </summary>
    private static readonly Dictionary<string, string[]> ConnectionImplicitTypes = new(StringComparer.Ordinal)
    {
        ["connection"] = ["Connections::connections", "Parts::parts"],
        ["interface"] = ["Interfaces::interfaces", "Connections::connections", "Parts::parts"],
        ["allocation"] = ["Allocations::allocations", "Connections::connections", "Parts::parts"],
        ["message"] = ["Flows::messages", "Transfers::transfers", "Actions::actions"],
        ["flow"] = ["Flows::flows", "Flows::messages", "Transfers::flowTransfers", "Transfers::transfers", "Actions::actions"],
        ["binding"] = ["Links::selfLinks"],
    };

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
    ///     applying to the declarations of that file, which are then consulted after the owning
    ///     namespaces and before the global namespace.
    /// </summary>
    /// <remarks>
    ///     The imports are keyed by declaration qualified name, never by top-level name alone, so
    ///     each file sees only its own root imports. Two user files never declare the same root
    ///     name (the workspace loader ignores a later declaration of a name that is already used),
    ///     so the keying now matters for a user declaration named like a standard-library one.
    ///     Callers register the qualified names of every declaration the file contributes; a name
    ///     already registered keeps its first registration.
    /// </remarks>
    /// <param name="declarationNames">The qualified names of the file's declarations.</param>
    /// <param name="rootImports">The file-root import nodes.</param>
    internal void RegisterRootImports(IEnumerable<string> declarationNames, IReadOnlyList<SysmlImportNode> rootImports)
    {
        if (rootImports.Count == 0)
        {
            return;
        }

        foreach (var name in declarationNames)
        {
            _rootImportsByDeclaration.TryAdd(name, rootImports);
        }
    }


    /// <summary>
    ///     Registers the aliases written at the root of a source file (outside any package) as
    ///     memberships of the global namespace (KerML 8.2.3.5.2): a global name that matches one
    ///     resolves to the alias's target, which is resolved from the root of the same file.
    /// </summary>
    /// <param name="aliases">The file-root alias nodes the workspace keeps.</param>
    /// <param name="rootImports">The file-root import nodes of the same file.</param>
    internal void RegisterRootAliases(IEnumerable<SysmlAliasNode> aliases, IReadOnlyList<SysmlImportNode> rootImports)
    {
        foreach (var alias in aliases)
        {
            _rootAliases.Add((alias, rootImports));
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
    internal string? Resolve(string reference, string? localNamespace, IReadOnlyList<SysmlImportNode>? fileRootImports) =>
        ResolveFeatureChain(reference, localNamespace, fileRootImports)?.Element;

    /// <summary>
    ///     Resolves <paramref name="reference"/> like <see cref="Resolve(string, string?, IReadOnlyList{SysmlImportNode}?)"/>
    ///     and, for a feature chain (<c>a.b.c</c>, KerML 8.2.3.5.1: each segment after the first is
    ///     resolved by visible resolution in the feature the previous segment resolved to, through
    ///     its own members and everything it inherits), also reports the <em>instance path</em> of
    ///     the chain's last feature.
    /// </summary>
    /// <remarks>
    ///     The instance path is the first segment's element followed by the simple name of each
    ///     later segment's element (<c>P::p1::x::x1</c> for <c>p1.x.x1</c> although <c>x</c> is
    ///     declared by the type of <c>p1</c>): it names the feature as seen through the chain, which
    ///     is what the connectors a chain end draws are keyed by. For a reference without a chain
    ///     it equals the resolved element.
    /// </remarks>
    /// <param name="reference">The (possibly qualified, possibly dotted) name to resolve.</param>
    /// <param name="localNamespace">The local namespace's qualified name, or null for a root namespace.</param>
    /// <param name="fileRootImports">The imports at the root of the referencing file, if any.</param>
    /// <returns>The resolution, or null when any segment does not resolve.</returns>
    internal FeatureChainResolution? ResolveFeatureChain(
        string reference, string? localNamespace, IReadOnlyList<SysmlImportNode>? fileRootImports)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (reference.IndexOf('.') < 0)
        {
            return ResolveQualified(reference, localNamespace, fileRootImports) is { } element
                ? new FeatureChainResolution(element, element)
                : null;
        }

        var parts = SplitChain(reference);
        if (parts.Count == 1)
        {
            return ResolveQualified(reference, localNamespace, fileRootImports) is { } single
                ? new FeatureChainResolution(single, single)
                : null;
        }

        return ResolveChainTail(ResolveQualified(parts[0], localNamespace, fileRootImports), parts);
    }

    /// <summary>
    ///     Resolves a (possibly qualified, never dotted) name: a single segment by full resolution, a
    ///     <c>$::</c>-prefixed name in the global namespace, otherwise the qualification part by this
    ///     same process and the last segment by visible resolution in the namespace it resolves to.
    /// </summary>
    private string? ResolveQualified(
        string reference, string? localNamespace, IReadOnlyList<SysmlImportNode>? fileRootImports)
    {
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
    ///     Resolves each part of a chain after the first by visible resolution in the element the
    ///     previous part resolved to, accumulating the instance path.
    /// </summary>
    /// <param name="first">The first part's resolved element, or null when it did not resolve.</param>
    /// <param name="parts">All the chain's parts (the first one already resolved as <paramref name="first"/>).</param>
    private FeatureChainResolution? ResolveChainTail(string? first, IReadOnlyList<string> parts)
    {
        if (first is null)
        {
            return null;
        }

        var current = first;
        var path = new System.Text.StringBuilder(first);
        for (var i = 1; i < parts.Count; i++)
        {
            var segments = SplitSegments(parts[i]);
            if (segments.Count == 0)
            {
                return null;
            }

            foreach (var segment in segments)
            {
                if (Lookup(segment, current, Scope.Visible) is not { } next)
                {
                    return null;
                }

                current = next;
            }

            path.Append("::").Append(SplitSegments(current)[^1]);
        }

        return new FeatureChainResolution(current, path.ToString());
    }

    /// <summary>
    ///     Resolves a redefined-feature reference per KerML 8.2.3.5.1: the basic resolution is
    ///     repeated with each general type of <paramref name="owningType"/> as the local
    ///     namespace, in turn, until one resolves. A bare name is looked up among each general
    ///     type's inheritable memberships, so a private member of a general type (which a
    ///     specialization does not inherit) cannot be redefined; a protected one can.
    /// </summary>
    /// <param name="reference">The redefined feature's (possibly qualified) name.</param>
    /// <param name="owningType">The qualified name of the redefining feature's owning type.</param>
    /// <returns>The redefined feature's qualified name, or null when it does not resolve.</returns>
    public string? ResolveRedefinedFeature(string reference, string owningType)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(owningType);
        IReadOnlyList<string> parts = reference.IndexOf('.') < 0 ? new[] { reference } : SplitChain(reference);
        var segments = SplitSegments(parts[0]);
        foreach (var general in LookupGeneralTypes(owningType))
        {
            // The redefining feature inherits from the general type, so a bare name is looked up
            // among the general type's inheritable memberships (not its private ones); the
            // enclosing namespaces and the qualified-name segments resolve as usual. For a chain
            // this applies to its first part; the later parts resolve by visible resolution.
            var first = segments is [var bare] && bare != "$"
                ? FullResolve(bare, general, null, Scope.RedefinitionTarget)
                : ResolveQualified(parts[0], general, null);
            var resolved = parts.Count == 1 ? first : ResolveChainTail(first, parts)?.Element;
            if (resolved is not null)
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
    ///     dominant bare <c>:&gt;&gt; name</c> shape) is never an acceptable target; when ordinary
    ///     resolution finds only that feature, the reference is resolved again from the owning
    ///     type's own owner, so an unnamed redefinition of a member of an enclosing or sibling
    ///     feature (<c>part :&gt;&gt; part1;</c> inside a nested usage) reaches that member.
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

        var ordinary = Resolve(reference, owningType, fileRootImports);
        if (ordinary is not null && ordinary != redefiningFeature)
        {
            return ordinary;
        }

        // Only the redefining feature itself was found: the owning type owns it under the
        // redefined name, so look past it in the enclosing namespaces.
        if (ordinary is not null && OwnerOf(owningType) is { } outer &&
            Resolve(reference, outer, fileRootImports) is { } outward && outward != redefiningFeature)
        {
            return outward;
        }

        return null;
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
        var rootImports = RootImportsOf(typeName);
        return resolved
            .Concat(raw.Select(reference => ResolveGeneralReference(reference, typeName, owner, rootImports)).OfType<string>())
            .Where(target => target != typeName)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    ///     The qualified names of every library type or feature an element can implicitly
    ///     specialize (the values of the implicit-specialization tables, plus the shared
    ///     <c>Base::things</c> fallback), for tests that guard the tables against library drift.
    /// </summary>
    internal static IReadOnlyCollection<string> ImplicitSpecializationTargets { get; } =
        DefinitionImplicitTypes.Values
            .Concat(FeatureImplicitTypes.Values)
            .Concat(ConnectionImplicitTypes.Values)
            .SelectMany(targets => targets)
            .Append(UnmappedFeatureType)
            .Append(ViewImplicitType)
            .Append(ViewpointImplicitType)
            .Append(TransitionImplicitType)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    ///     The general types lookup uses for <paramref name="typeName"/>: its declared general
    ///     types (<see cref="GeneralTypes"/>) followed by the library types its kind implicitly
    ///     specializes (KerML 7.3.4/SysML default general types), a variant's owning variation, and
    ///     the type of a conjugated typing. Implicit entries come last so an explicit supertype
    ///     wins a name conflict; one that is the type itself, already declared, or absent from the
    ///     loaded declarations (a workspace without the standard library) is skipped.
    /// </summary>
    /// <remarks>
    ///     Implicit specializations are deliberately not <see cref="SysmlEdge"/>s and not part of
    ///     the public <see cref="GeneralTypes"/>: they only feed name lookup, so renderers, queries
    ///     and exports see exactly the declared relationships.
    /// </remarks>
    /// <param name="typeName">The type's qualified name.</param>
    /// <returns>The lookup general types' qualified names, without duplicates.</returns>
    internal IReadOnlyList<string> LookupGeneralTypes(string typeName)
    {
        var declared = GeneralTypes(typeName);
        if (!_declarations.TryGetValue(typeName, out var node))
        {
            return declared;
        }

        List<string>? result = null;
        if (ReferenceSubsettingLookupType(typeName, node) is { } referenceSubsetting &&
            !declared.Contains(referenceSubsetting))
        {
            result = [.. declared, referenceSubsetting];
        }

        foreach (var candidate in ImplicitGeneralTypes(typeName, node))
        {
            if (candidate == typeName || !_declarations.ContainsKey(candidate) || declared.Contains(candidate) ||
                (result is not null && result.Contains(candidate)))
            {
                continue;
            }

            result ??= [.. declared];
            result.Add(candidate);
        }

        return result ?? declared;
    }

    /// <summary>
    ///     The candidate implicit general types of a node: the library types its definition or
    ///     usage keyword names in the implicit-specialization tables, the owning variation of a
    ///     variant, and the port definition of a conjugated typing (resolved relative to the
    ///     owning namespace like any declared reference). Candidates are not yet filtered for
    ///     existence, self-reference or duplicates.
    /// </summary>
    private IEnumerable<string> ImplicitGeneralTypes(string typeName, SysmlNode node)
    {
        switch (node)
        {
            case SysmlDefinitionNode definition:
                if (DefinitionImplicitTypes.TryGetValue(definition.DefinitionKeyword, out var definitionTypes))
                {
                    return definitionTypes;
                }

                return [];

            case SysmlViewNode:
                return [ViewImplicitType];

            case SysmlViewpointNode:
                return [ViewpointImplicitType];

            case SysmlTransitionNode:
                return [TransitionImplicitType];

            case SysmlFeatureNode feature:
                return ImplicitFeatureGeneralTypes(typeName, feature);

            case SysmlConnectionNode connection:
                return ConnectionImplicitTypes.TryGetValue(connection.ConnectionKeyword, out var connectionTypes)
                    ? connectionTypes
                    : [UnmappedFeatureType];

            default:
                return [];
        }
    }

    /// <summary>The candidate implicit general types of a usage (see <see cref="ImplicitGeneralTypes"/>).</summary>
    private List<string> ImplicitFeatureGeneralTypes(string typeName, SysmlFeatureNode feature)
    {
        var result = new List<string>();
        if (feature.IsVariant && OwnerOf(typeName) is { } variation)
        {
            result.Add(variation);
        }

        if (feature.ConjugatedFeatureTyping is { Length: > 0 } conjugated &&
            Resolve(conjugated, OwnerOf(typeName), RootImportsOf(typeName)) is { } conjugatedType)
        {
            result.Add(conjugatedType);
        }

        if (FeatureImplicitTypes.TryGetValue(feature.FeatureKeyword, out var featureTypes))
        {
            result.AddRange(featureTypes);
        }
        else
        {
            result.Add(UnmappedFeatureType);
        }

        return result;
    }

    /// <summary>
    ///     The file-root imports that apply to <paramref name="typeName"/>'s own references: those
    ///     of the file that declares it (registered against its qualified name), or null when that
    ///     file has none.
    /// </summary>
    private IReadOnlyList<SysmlImportNode>? RootImportsOf(string typeName) =>
        _rootImportsByDeclaration.GetValueOrDefault(typeName);

    /// <summary>
    ///     Returns the raw text of every redefinition target of <paramref name="feature"/>: the
    ///     first one followed by any further ones (<see cref="SysmlFeatureNode.AdditionalRedefinedFeatureNames"/>).
    /// </summary>
    internal static IEnumerable<string> AllRedefinedFeatureNames(SysmlFeatureNode feature)
    {
        if (feature.RedefinedFeatureName is { Length: > 0 } first)
        {
            yield return first;
        }

        foreach (var additional in feature.AdditionalRedefinedFeatureNames ?? [])
        {
            yield return additional;
        }
    }

    /// <summary>
    ///     The raw supertype, typing, and view-definition references that name a type's declared
    ///     general types. A reference subsetting is deliberately not among them: it specializes for
    ///     lookup purposes only (see <see cref="ReferenceSubsettingLookupType"/>), so it is never
    ///     part of the public <see cref="GeneralTypes"/>.
    /// </summary>
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
    ///     The qualified name of the feature a feature references by reference subsetting
    ///     (<c>::&gt;</c>, <c>perform</c>, <c>assert</c>, ...), resolved relative to its owning
    ///     namespace, or null when it has none, it does not resolve, or it resolves to the feature
    ///     itself. It specializes for name lookup only: it adds no edge and no diagnostic and is
    ///     not part of <see cref="GeneralTypes"/>. A feature reached again while its own reference
    ///     is being resolved contributes nothing.
    /// </summary>
    private string? ReferenceSubsettingLookupType(string typeName, SysmlNode node)
    {
        if (node is not SysmlFeatureNode { ReferenceSubsettingName: { Length: > 0 } reference })
        {
            return null;
        }

        if (_referenceSubsettingTargets.TryGetValue(typeName, out var cached))
        {
            return cached;
        }

        if (!_referenceSubsettingInProgress.Add(typeName))
        {
            _generalTypeReentries++;
            return null;
        }

        try
        {
            var target = ResolveGeneralReference(reference, typeName, OwnerOf(typeName), RootImportsOf(typeName));
            var result = target == typeName ? null : target;
            _referenceSubsettingTargets[typeName] = result;
            return result;
        }
        finally
        {
            _referenceSubsettingInProgress.Remove(typeName);
        }
    }

    /// <summary>
    ///     Resolves a general-type reference of the type <paramref name="typeName"/> relative to its
    ///     owning namespace. A reference that resolves to the type itself (a usage named only by the
    ///     feature it references, <c>assert massLimitation { ... }</c>, finds itself under that
    ///     name) is resolved again from the owner's own owner, mirroring
    ///     <see cref="ResolveRedefinition"/>.
    /// </summary>
    private string? ResolveGeneralReference(
        string reference, string typeName, string? owner, IReadOnlyList<SysmlImportNode>? rootImports)
    {
        var resolved = Resolve(reference, owner, rootImports);
        return resolved == typeName && owner is not null
            ? Resolve(reference, OwnerOf(owner), rootImports)
            : resolved;
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
            _generalTypeReentries++;
            return [];
        }

        try
        {
            var owner = OwnerOf(typeName);
            var rootImports = RootImportsOf(typeName);
            var general = RawGeneralReferences(node)
                .Select(reference => ResolveGeneralReference(reference, typeName, owner, rootImports))
                .OfType<string>()
                .ToList();
            if (node is SysmlFeatureNode { RedefinedFeatureName.Length: > 0 } feature)
            {
                // Every redefinition target is a general type: the feature inherits the members of
                // each (`part :>> a :>> b` sees the members of both).
                foreach (var reference in AllRedefinedFeatureNames(feature))
                {
                    if (ResolveRedefinition(reference, owner ?? string.Empty, typeName, rootImports) is { } redefinedFeature)
                    {
                        general.Add(redefinedFeature);
                    }
                }
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
    ///     namespace outward, then the imports at the root of the file, then the global namespace.
    ///     The file is the referencing one when the caller supplies its root imports; otherwise it is
    ///     the file declaring <paramref name="localNamespace"/> (a top-level name alone cannot say
    ///     which file's imports apply). <paramref name="localScope"/> selects which
    ///     memberships of <paramref name="localNamespace"/> itself are considered (the outer
    ///     namespaces always use local resolution).
    /// </summary>
    private string? FullResolve(
        string name,
        string? localNamespace,
        IReadOnlyList<SysmlImportNode>? fileRootImports,
        Scope localScope = Scope.Local)
    {
        for (var scope = localNamespace; !string.IsNullOrEmpty(scope); scope = OwnerOf(scope))
        {
            if (Lookup(name, scope, scope == localNamespace ? localScope : Scope.Local) is { } found)
            {
                return found;
            }
        }

        var rootImports = fileRootImports ??
            (string.IsNullOrEmpty(localNamespace) ? null : _rootImportsByDeclaration.GetValueOrDefault(localNamespace));
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

        // A top-level element may be written with or without enclosing quotes.
        var otherForm = name.Length >= 2 && name[0] == '\'' && name[^1] == '\''
            ? name[1..^1]
            : "'" + name + "'";
        if (_declarations.ContainsKey(otherForm))
        {
            return otherForm;
        }

        // A root-level alias is a membership of the global namespace too: it resolves to its target.
        if (ResolveRootAlias(name, byShortName: false) is { } aliased)
        {
            return aliased;
        }

        // A top-level element may also be found by its short name.
        return _declarations
            .Where(kvp => !kvp.Key.Contains("::", StringComparison.Ordinal) && NamesEqual(kvp.Value.ShortName, name))
            .Select(kvp => kvp.Key)
            .FirstOrDefault()
            ?? ResolveRootAlias(name, byShortName: true);
    }

    /// <summary>
    ///     Resolves <paramref name="name"/> through the root-level aliases registered with
    ///     <see cref="RegisterRootAliases"/>, by declared name or by short name, following the first
    ///     matching alias whose target resolves. An alias already being followed contributes nothing,
    ///     so a cycle of aliases ends unresolved.
    /// </summary>
    private string? ResolveRootAlias(string name, bool byShortName)
    {
        foreach (var (alias, rootImports) in _rootAliases)
        {
            if (!NamesEqual(byShortName ? alias.ShortName : alias.Name, name) ||
                !_rootAliasesInProgress.Add(alias))
            {
                continue;
            }

            try
            {
                if (Resolve(alias.TargetName, null, rootImports) is { } target)
                {
                    return target;
                }
            }
            finally
            {
                _rootAliasesInProgress.Remove(alias);
            }
        }

        return null;
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
        // progress contributes nothing rather than recursing forever. This is the role of the
        // spec's `excluded` set (Namespace::visibleMemberships). The depth of the outermost
        // lookup cut short this way is tracked, so a miss that depended on the cut is not final.
        if (_inProgress.TryGetValue(key, out var activeDepth))
        {
            _lowestCutDepth = Math.Min(_lowestCutDepth, activeDepth);
            return null;
        }

        if (_provisionalMisses.TryGetValue(key, out var provisionalCutDepth))
        {
            _lowestCutDepth = Math.Min(_lowestCutDepth, provisionalCutDepth);
            return null;
        }

        var depth = _inProgress.Count;
        _inProgress[key] = depth;
        var cutDepthOutside = _lowestCutDepth;
        var provisionalStart = _provisionalOrder.Count;
        _lowestCutDepth = int.MaxValue;
        try
        {
            var reentriesBefore = _generalTypeReentries;
            var result = LookupUncached(name, namespaceName, scope);

            // A miss computed while a type's general types were being re-entered (a feature
            // specializing a chain through itself, `ref x : T :> x.y`) saw that type without its
            // declared general types; it is not final, so it is not cached.
            var reentered = _generalTypeReentries != reentriesBefore;
            if (result is not null || !reentered)
            {
                if (result is null && _lowestCutDepth < depth)
                {
                    // A miss cut short by a lookup still in progress further out (a cycle of
                    // imports through this namespace) may be found through that lookup's other
                    // memberships once it finishes. It stands only while that lookup is in
                    // progress (a provisional miss) and is settled when the lookup finishes.
                    _provisionalMisses[key] = _lowestCutDepth;
                    _provisionalOrder.Add((key, _lowestCutDepth));
                }
                else
                {
                    _cache[key] = result;
                }
            }

            // Cuts of this lookup alone are settled: the cyclic region it heads has been explored
            // in full. Its provisional misses become final if it missed too, and are discarded
            // (they may be found through this lookup's result) if it did not.
            if (_lowestCutDepth >= depth)
            {
                SettleProvisionalMisses(provisionalStart, depth, result is null && !reentered);
            }
            else
            {
                // This lookup depends on one further out, and so do the provisional misses that
                // were cut short by it: they now wait for that lookup instead.
                RebaseProvisionalMisses(provisionalStart, depth, _lowestCutDepth);
            }

            return result;
        }
        finally
        {
            _inProgress.Remove(key);
            _lowestCutDepth = Math.Min(cutDepthOutside, _lowestCutDepth < depth ? _lowestCutDepth : int.MaxValue);
        }
    }

    /// <summary>
    ///     Re-targets the provisional misses recorded since <paramref name="start"/> that wait for
    ///     the lookup at <paramref name="depth"/> (or deeper), which is finishing without having
    ///     settled them, to the outer lookup at <paramref name="newCutDepth"/>.
    /// </summary>
    private void RebaseProvisionalMisses(int start, int depth, int newCutDepth)
    {
        for (var i = start; i < _provisionalOrder.Count; i++)
        {
            var (key, cutDepth) = _provisionalOrder[i];
            if (cutDepth >= depth)
            {
                _provisionalOrder[i] = (key, newCutDepth);
                _provisionalMisses[key] = newCutDepth;
            }
        }
    }
    /// <summary>
    ///     Settles the provisional misses recorded since <paramref name="start"/> whose lookup cycle
    ///     was headed by the lookup at <paramref name="depth"/>: each becomes a final cached miss
    ///     when the head missed as well (<paramref name="headMissed"/>), and is dropped otherwise.
    ///     Provisional misses that depend on a lookup further out stay pending.
    /// </summary>
    private void SettleProvisionalMisses(int start, int depth, bool headMissed)
    {
        var kept = start;
        for (var i = start; i < _provisionalOrder.Count; i++)
        {
            var (key, cutDepth) = _provisionalOrder[i];
            if (cutDepth < depth)
            {
                _provisionalOrder[kept++] = (key, cutDepth);
                continue;
            }

            _provisionalMisses.Remove(key);
            if (headMissed)
            {
                _cache[key] = null;
            }
        }

        _provisionalOrder.RemoveRange(kept, _provisionalOrder.Count - kept);
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
                (!NamesEqual(child.Name, name) && !NamesEqual(child.ShortName, name)))
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
        // (only the public ones for visible resolution), explicit and implicit alike, except the
        // features the type's own features redefine (KerML Type::removeRedefinedFeatures). The
        // targets of a redefinition are resolved without that exclusion: a library association
        // routinely redefines a name its general type's own feature already redefined.
        var inheritedScope = scope switch
        {
            Scope.Visible => Scope.Visible,
            Scope.RedefinitionTarget => Scope.RedefinitionTarget,
            _ => Scope.Inheritable,
        };
        foreach (var general in LookupGeneralTypes(namespaceName))
        {
            if (Lookup(name, general, inheritedScope) is { } inherited &&
                (scope == Scope.RedefinitionTarget || !IsRedefinedByOwnedFeature(ns, namespaceName, inherited)))
            {
                return inherited;
            }
        }

        return null;
    }

    /// <summary>
    ///     Returns whether a feature owned by <paramref name="ns"/> redefines the inherited member
    ///     <paramref name="member"/>, which therefore is not among <paramref name="ns"/>'s inherited
    ///     memberships. The redefined targets of the namespace's owned features are resolved once
    ///     per namespace (strictly, against its general types); a namespace re-entered while its own
    ///     targets are being resolved (a specialization cycle) hides nothing.
    /// </summary>
    private bool IsRedefinedByOwnedFeature(SysmlNode ns, string namespaceName, string member)
    {
        if (!_redefinedMembers.TryGetValue(namespaceName, out var redefined))
        {
            if (!_redefinedMembersInProgress.Add(namespaceName))
            {
                return false;
            }

            try
            {
                redefined = new HashSet<string>(StringComparer.Ordinal);
                foreach (var child in ns.Children)
                {
                    if (child is not SysmlFeatureNode { RedefinedFeatureName.Length: > 0, Name.Length: > 0 } redefiningFeature)
                    {
                        continue;
                    }

                    foreach (var reference in AllRedefinedFeatureNames(redefiningFeature))
                    {
                        if (ResolveRedefinedFeature(reference, namespaceName) is { } target)
                        {
                            redefined.Add(target);
                        }
                    }
                }

                _redefinedMembers[namespaceName] = redefined;
            }
            finally
            {
                _redefinedMembersInProgress.Remove(namespaceName);
            }
        }

        return redefined.Count > 0 && redefined.Contains(member);
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
            if (NamesEqual(lastSegment, name) ||
                (_declarations.TryGetValue(target, out var targetNode) && NamesEqual(targetNode.ShortName, name)) ||
                NamesEqual(ImportedAliasShortName(import, importingNamespace), name))
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
            .FirstOrDefault(alias => NamesEqual(alias.Name, segments[^1]))?.ShortName;
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
        Scope.Inheritable or Scope.RedefinitionTarget => visibility != SysmlVisibility.Private,
        _ => visibility == SysmlVisibility.Public,
    };

    /// <summary>Returns the owning namespace's qualified name (the prefix before the last <c>::</c>), or null at the root.</summary>
    private static string? OwnerOf(string qualifiedName)
    {
        var sep = qualifiedName.LastIndexOf("::", StringComparison.Ordinal);
        return sep >= 0 ? qualifiedName[..sep] : null;
    }

    /// <summary>
    ///     Splits a feature chain into its parts on every <c>.</c> outside a quoted name, keeping
    ///     empty parts (so a malformed chain such as <c>a.</c> cannot resolve).
    /// </summary>
    private static List<string> SplitChain(string reference)
    {
        var parts = new List<string>();
        var start = 0;
        var quoted = false;
        for (var i = 0; i < reference.Length; i++)
        {
            if (reference[i] == '\'')
            {
                quoted = !quoted;
            }
            else if (!quoted && reference[i] == '.')
            {
                parts.Add(reference[start..i]);
                start = i + 1;
            }
        }

        parts.Add(reference[start..]);
        return parts;
    }

    /// <summary>
    ///     Returns whether two names are the same name: equal, or equal once one pair of enclosing
    ///     single quotes is ignored on either side (<c>'PowerTrain'</c> and <c>PowerTrain</c> name
    ///     the same element, KerML 8.2.2.2.1).
    /// </summary>
    private static bool NamesEqual(string? left, string? right)
    {
        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        // Two names that differ only by one pair of enclosing quotes differ in length by exactly two.
        return (left.Length - right.Length) switch
        {
            2 => IsQuotedForm(left, right),
            -2 => IsQuotedForm(right, left),
            _ => false,
        };
    }

    /// <summary>Returns whether <paramref name="quoted"/> is <paramref name="name"/> enclosed in single quotes.</summary>
    private static bool IsQuotedForm(string quoted, string name) =>
        quoted[0] == '\'' && quoted[^1] == '\'' && quoted.AsSpan(1, name.Length).SequenceEqual(name);

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
}
