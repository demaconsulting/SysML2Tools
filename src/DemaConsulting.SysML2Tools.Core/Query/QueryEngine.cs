// <copyright file="QueryEngine.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Globalization;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;

namespace DemaConsulting.SysML2Tools.Query;

/// <summary>
///     Implements the real analysis logic for all 12 <see cref="QueryVerb"/> operations, each as
///     a static method taking the loaded <see cref="SysmlWorkspace"/>, the resolved target
///     <see cref="SysmlNode"/> (<see langword="null"/> only for <see cref="List(SysmlWorkspace,QueryOptions)"/>/
///     <see cref="Find"/>), and the parsed <see cref="QueryOptions"/>, and returning a uniform
///     <see cref="QueryResult"/> for <see cref="QueryResultRenderer"/> to render.
/// </summary>
public static class QueryEngine
{
    /// <summary>
    ///     The <see cref="SysmlEdgeKind"/> values considered "requirement relationships" by the
    ///     <c>requirements</c> verb.
    /// </summary>
    private static readonly SysmlEdgeKind[] RequirementEdgeKinds =
    [
        SysmlEdgeKind.Satisfy,
        SysmlEdgeKind.Verify,
        SysmlEdgeKind.Allocate
    ];

    /// <summary>
    ///     The connector edge kinds reported by the <c>connections</c> verb.
    /// </summary>
    private static readonly SysmlEdgeKind[] ConnectionsVerbEdgeKinds = [SysmlEdgeKind.Connect];

    /// <summary>
    ///     The connector edge kinds traversed by the <c>impact</c> verb when
    ///     <see cref="QueryOptions.IncludeConnections"/> is set. Both kinds join two endpoints
    ///     that carry no semantic source-causes-target direction, so both are traversed
    ///     undirected.
    /// </summary>
    /// <remarks>
    ///     These kinds are also <b>excluded</b> from the <c>impact</c> verb's reference-edge
    ///     closure. They are present in <see cref="SemanticIndex.AllEdges"/> alongside ordinary
    ///     reference edges, so without that exclusion every connector would be followed a second
    ///     time as a plain incoming reference — directed, unrolled, and unattributed. Excluding
    ///     them makes <see cref="CollectImpactConnections"/> the single attribution path for
    ///     connector relationships, so each connector is reported exactly once and only under
    ///     <see cref="QueryOptions.IncludeConnections"/>.
    /// </remarks>
    private static readonly SysmlEdgeKind[] ImpactConnectorEdgeKinds =
    [
        SysmlEdgeKind.Connect,
        SysmlEdgeKind.Binding
    ];

    /// <summary>
    ///     The <see cref="SysmlFeatureNode.FeatureKeyword"/> value identifying a port usage.
    ///     Named here rather than repeated inline so the endpoint-only classification in
    ///     <see cref="IsEndpointOnlyDeclaration"/> reads as a structural model test rather than a
    ///     bare string comparison.
    /// </summary>
    private const string PortFeatureKeyword = "port";

    /// <summary>
    ///     The <see cref="SysmlDefinitionNode.DefinitionKeyword"/> value identifying a port
    ///     definition, the definition-side counterpart of <see cref="PortFeatureKeyword"/>.
    /// </summary>
    private const string PortDefinitionKeyword = "port def";

    /// <summary>
    ///     Dispatches to the verb method selected by <see cref="QueryOptions.Verb"/>, the single
    ///     entry point library callers can use instead of writing their own 12-arm switch (this is
    ///     the same dispatch previously inlined in the Tool project's <c>QueryCommand.RunAsync</c>
    ///     before <see cref="QueryEngine"/> became part of this project's public API).
    /// </summary>
    /// <param name="workspace">The loaded workspace.</param>
    /// <param name="options">The parsed query options, supplying <see cref="QueryOptions.Verb"/>.</param>
    /// <param name="element">
    ///     The resolved target element. Required (non-<see langword="null"/>) for every verb
    ///     except <see cref="QueryVerb.List"/> and <see cref="QueryVerb.Find"/> (see
    ///     <see cref="QueryVerbParsing.RequiresElement"/>); callers that already guarantee this
    ///     (e.g., because they resolved <paramref name="element"/> themselves before calling this
    ///     method) incur only the cost of one enum check.
    /// </param>
    /// <returns>The query result produced by the dispatched verb.</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="workspace"/> or <paramref name="options"/> is
    ///     <see langword="null"/>, or when <paramref name="element"/> is <see langword="null"/>
    ///     for a verb that <see cref="QueryVerbParsing.RequiresElement"/> reports as requiring one.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <see cref="QueryOptions.Verb"/> is not a recognized <see cref="QueryVerb"/>
    ///     value.
    /// </exception>
    public static QueryResult Execute(SysmlWorkspace workspace, QueryOptions options, SysmlNode? element)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(options);

        if (QueryVerbParsing.RequiresElement(options.Verb) && element is null)
        {
            throw new ArgumentNullException(
                nameof(element),
                $"The '{QueryVerbParsing.ToToken(options.Verb)}' verb requires a resolved target element.");
        }

        // Each verb gets its own switch arm (rather than a lookup/loop) so a future release can
        // change one verb's logic without touching the others; mirrors the switch previously
        // inlined in the Tool project's QueryCommand.RunAsync.
        return options.Verb switch
        {
            QueryVerb.Uses => Uses(workspace, element!, options),
            QueryVerb.UsedBy => UsedBy(workspace, element!, options),
            QueryVerb.Dependencies => Dependencies(workspace, element!, options),
            QueryVerb.Impact => Impact(workspace, element!, options),
            QueryVerb.Describe => Describe(workspace, element!, options),
            QueryVerb.Hierarchy => Hierarchy(workspace, element!, options),
            QueryVerb.Requirements => Requirements(workspace, element!, options),
            QueryVerb.Interface => Interface(workspace, element!, options),
            QueryVerb.Connections => Connections(workspace, element!, options),
            QueryVerb.States => States(workspace, element!, options),
            QueryVerb.List => List(workspace, options),
            QueryVerb.Find => Find(workspace, options),
            _ => throw new ArgumentOutOfRangeException(nameof(options), options.Verb, "Unrecognized query verb.")
        };
    }

    /// <summary>
    ///     Lists the elements a given element uses (its resolved outgoing supertype, typing, and
    ///     import edges).
    /// </summary>
    /// <param name="workspace">The loaded workspace.</param>
    /// <param name="element">The target element.</param>
    /// <param name="options">The parsed query options.</param>
    /// <returns>The query result.</returns>
    public static QueryResult Uses(SysmlWorkspace workspace, SysmlNode element, QueryOptions options)
    {
        var qualifiedName = QualifiedNameOf(element, options);
        var entries = new List<QueryResultEntry>();

        foreach (var edge in workspace.Index.GetOutgoingEdges(qualifiedName))
        {
            if (!IsVisible(edge.TargetQualifiedName, workspace, options.IncludeStdlib))
            {
                continue;
            }

            workspace.Declarations.TryGetValue(edge.TargetQualifiedName, out var target);
            entries.Add(new QueryResultEntry
            {
                QualifiedName = edge.TargetQualifiedName,
                Kind = EdgeKindLabel(edge.Kind),
                Detail = target is not null ? DescribeKind(target) : null
            });
        }

        return new QueryResult
        {
            Verb = "uses",
            Element = qualifiedName,
            Summary = [$"{entries.Count} outgoing reference(s) from '{qualifiedName}'."],
            Entries = entries
        };
    }

    /// <summary>
    ///     Lists the elements that use a given element (the reverse of <see cref="Uses"/>, via
    ///     <see cref="SemanticIndex.GetIncomingEdges"/>).
    /// </summary>
    /// <param name="workspace">The loaded workspace.</param>
    /// <param name="element">The target element.</param>
    /// <param name="options">The parsed query options.</param>
    /// <returns>The query result.</returns>
    public static QueryResult UsedBy(SysmlWorkspace workspace, SysmlNode element, QueryOptions options)
    {
        var qualifiedName = QualifiedNameOf(element, options);
        var entries = new List<QueryResultEntry>();

        foreach (var edge in workspace.Index.GetIncomingEdges(qualifiedName))
        {
            if (edge.SourceQualifiedName is not { Length: > 0 } source)
            {
                continue;
            }

            if (!IsVisible(source, workspace, options.IncludeStdlib))
            {
                continue;
            }

            workspace.Declarations.TryGetValue(source, out var sourceNode);
            entries.Add(new QueryResultEntry
            {
                QualifiedName = source,
                Kind = EdgeKindLabel(edge.Kind),
                Detail = sourceNode is not null ? DescribeKind(sourceNode) : null
            });
        }

        return new QueryResult
        {
            Verb = "used-by",
            Element = qualifiedName,
            Summary = [$"{entries.Count} incoming reference(s) to '{qualifiedName}'."],
            Entries = entries
        };
    }

    /// <summary>
    ///     Combines <see cref="Uses"/> (outgoing) and <see cref="UsedBy"/> (incoming) for a given
    ///     element into one result, tagging each entry's <see cref="QueryResultEntry.Direction"/>
    ///     accordingly, so the caller doesn't need to run two separate queries or duplicate the
    ///     underlying edge-traversal logic.
    /// </summary>
    /// <param name="workspace">The loaded workspace.</param>
    /// <param name="element">The target element.</param>
    /// <param name="options">The parsed query options.</param>
    /// <returns>The query result.</returns>
    public static QueryResult Dependencies(SysmlWorkspace workspace, SysmlNode element, QueryOptions options)
    {
        var qualifiedName = QualifiedNameOf(element, options);

        var usesResult = Uses(workspace, element, options);
        var usedByResult = UsedBy(workspace, element, options);

        var entries = new List<QueryResultEntry>();
        entries.AddRange(usesResult.Entries.Select(e => e with { Direction = QueryEntryDirection.Outgoing }));
        entries.AddRange(usedByResult.Entries.Select(e => e with { Direction = QueryEntryDirection.Incoming }));

        return new QueryResult
        {
            Verb = "dependencies",
            Element = qualifiedName,
            Entries = entries
        };
    }

    /// <summary>
    ///     Reports the transitive "blast radius" of a change to a given element: the transitive
    ///     closure of <see cref="UsedBy"/>, bounded by <see cref="QueryOptions.WalkDepth"/> when
    ///     specified (unlimited otherwise), optionally extended with undirected connector
    ///     (<c>connect</c>/<c>bind</c>) traversal when
    ///     <see cref="QueryOptions.IncludeConnections"/> is set. The bound applies uniformly to
    ///     reference and connector relationships alike — one relationship is one unit of depth
    ///     regardless of its kind.
    /// </summary>
    /// <param name="workspace">The loaded workspace.</param>
    /// <param name="element">The target element.</param>
    /// <param name="options">The parsed query options.</param>
    /// <returns>The query result.</returns>
    /// <remarks>
    ///     A single depth budget is applied, because users reason about proximity to the subject
    ///     rather than about per-relationship-class allowances; the class of each relationship is
    ///     reported on the entry (<see cref="QueryResultEntry.Relation"/>) for clients to group or
    ///     filter on after the fact. <see cref="QueryOptions.IncludeConnections"/> therefore
    ///     selects <em>which edges exist in the graph</em>, never how far the walk goes.
    ///     <para>
    ///     Connector edge kinds (<see cref="ImpactConnectorEdgeKinds"/>) are excluded from the
    ///     reference closure, so a connector is attributed exactly once — by
    ///     <see cref="CollectImpactConnections"/>, rolled up to its owning declaration — and
    ///     never additionally as a raw incoming reference edge.
    ///     </para>
    ///     <para>
    ///     Because every traversed relationship costs exactly one breadth-first level (both
    ///     collectors append only to the next level's frontier, and neither can reach an element
    ///     without advancing a level), this is a uniform-cost level-order search. First arrival is
    ///     therefore provably the shortest relationship distance, so a plain membership set
    ///     suffices as the cycle guard and <see cref="QueryResultEntry.Depth"/> is exactly that
    ///     shortest distance. Termination on cyclic and densely connected graphs follows from
    ///     each element being admitted to the guard at most once.
    ///     </para>
    /// </remarks>
    public static QueryResult Impact(SysmlWorkspace workspace, SysmlNode element, QueryOptions options)
    {
        var qualifiedName = QualifiedNameOf(element, options);
        var visited = new HashSet<string>(StringComparer.Ordinal) { qualifiedName };
        var entries = new List<QueryResultEntry>();

        // Collected once per call, and only when requested, rather than once per frontier item.
        List<(string Source, string Target, string Keyword, SysmlEdgeKind Kind)> connectorEdges =
            options.IncludeConnections
                ? CollectConnectorEdges(workspace, ImpactConnectorEdgeKinds)
                : [];

        var frontier = new List<string> { qualifiedName };
        var depth = 0;

        while (frontier.Count > 0 && (options.WalkDepth is not { } maxDepth || depth < maxDepth))
        {
            depth++;
            var next = new List<string>();

            foreach (var current in frontier)
            {
                CollectImpactReferences(workspace, options, current, depth, visited, entries, next);

                if (options.IncludeConnections)
                {
                    CollectImpactConnections(
                        workspace, options, current, depth, connectorEdges, visited, entries, next);
                }
            }

            frontier = next;
        }

        var depthSuffix = options.WalkDepth is { } d ? $" (depth <= {d})" : string.Empty;
        var connectionSuffix = options.IncludeConnections ? ", including connections" : string.Empty;
        return new QueryResult
        {
            Verb = "impact",
            Element = qualifiedName,
            Summary =
            [
                $"{entries.Count} element(s) transitively impacted by a change to " +
                $"'{qualifiedName}'{depthSuffix}{connectionSuffix}."
            ],
            Entries = entries
        };
    }

    /// <summary>
    ///     Expands one impact frontier item over its incoming reference edges — the original,
    ///     always-on reverse closure — appending newly-reached names to
    ///     <paramref name="next"/>, the frontier for the following depth level.
    /// </summary>
    /// <remarks>
    ///     Edges whose kind is in <see cref="ImpactConnectorEdgeKinds"/> are skipped. Connector
    ///     edges are published into <see cref="SemanticIndex.AllEdges"/> alongside ordinary
    ///     reference edges, so following them here as well would report every connector a second
    ///     time — directed instead of undirected, as the raw endpoint instead of its owning
    ///     declaration, and without <see cref="QueryResultEntry.ViaQualifiedName"/> attribution.
    ///     <see cref="CollectImpactConnections"/> is therefore the single attribution path for
    ///     connector relationships.
    /// </remarks>
    /// <param name="workspace">The loaded workspace.</param>
    /// <param name="options">The parsed query options.</param>
    /// <param name="current">The frontier item's qualified name.</param>
    /// <param name="depth">The 1-based traversal depth of the names being reached.</param>
    /// <param name="visited">
    ///     The shared cycle guard, holding every qualified name already reached (seeded with the
    ///     subject so it never reports itself). Updated in place.
    /// </param>
    /// <param name="entries">The result entries accumulated so far.</param>
    /// <param name="next">The next frontier being built.</param>
    private static void CollectImpactReferences(
        SysmlWorkspace workspace,
        QueryOptions options,
        string current,
        int depth,
        HashSet<string> visited,
        List<QueryResultEntry> entries,
        List<string> next)
    {
        foreach (var edge in workspace.Index.GetIncomingEdges(current))
        {
            if (IsImpactConnectorKind(edge.Kind) ||
                edge.SourceQualifiedName is not { Length: > 0 } source ||
                !visited.Add(source))
            {
                continue;
            }

            next.Add(source);

            if (!IsVisible(source, workspace, options.IncludeStdlib))
            {
                continue;
            }

            workspace.Declarations.TryGetValue(source, out var sourceNode);
            entries.Add(new QueryResultEntry
            {
                QualifiedName = source,
                Kind = sourceNode is not null ? DescribeKind(sourceNode) : EdgeKindLabel(edge.Kind),
                Detail = $"depth {depth}",
                Depth = depth,
                Relation = edge.Kind
            });
        }
    }

    /// <summary>
    ///     Expands one impact frontier item over connector edges, undirected: a connector is
    ///     followed whenever exactly one of its two endpoints is the frontier item itself or a
    ///     feature nested inside it, and the other endpoint is rolled up to its nearest owning
    ///     declaration. Costs one depth level per reached element, exactly like a reference edge.
    /// </summary>
    /// <param name="workspace">The loaded workspace.</param>
    /// <param name="options">The parsed query options.</param>
    /// <param name="current">The frontier item's qualified name.</param>
    /// <param name="depth">The 1-based traversal depth of the names being reached.</param>
    /// <param name="connectorEdges">The connector edges collected once for this invocation.</param>
    /// <param name="visited">
    ///     The shared cycle guard, holding every qualified name already reached (seeded with the
    ///     subject so it never reports itself). Updated in place.
    /// </param>
    /// <param name="entries">The result entries accumulated so far.</param>
    /// <param name="next">The next frontier being built.</param>
    private static void CollectImpactConnections(
        SysmlWorkspace workspace,
        QueryOptions options,
        string current,
        int depth,
        List<(string Source, string Target, string Keyword, SysmlEdgeKind Kind)> connectorEdges,
        HashSet<string> visited,
        List<QueryResultEntry> entries,
        List<string> next)
    {
        foreach (var (source, target, keyword, kind) in connectorEdges)
        {
            var nearIsSource = IsSelfOrNestedUnder(source, current);
            var nearIsTarget = IsSelfOrNestedUnder(target, current);

            // Rejects both "neither end belongs to the subject" and the self-loop case where
            // both ends are nested under it (which would otherwise report the subject itself).
            if (nearIsSource == nearIsTarget)
            {
                continue;
            }

            var near = nearIsSource ? source : target;
            var far = nearIsSource ? target : source;
            var owner = RollUpToNearestDeclaration(workspace, far);
            if (!visited.Add(owner))
            {
                continue;
            }

            next.Add(owner);

            if (!IsVisible(owner, workspace, options.IncludeStdlib))
            {
                continue;
            }

            workspace.Declarations.TryGetValue(owner, out var ownerNode);
            entries.Add(new QueryResultEntry
            {
                QualifiedName = owner,
                Kind = ownerNode is not null ? DescribeKind(ownerNode) : keyword,
                Detail = $"depth {depth}",
                Notes = [$"connected via {keyword}: {near} -> {far}"],
                Depth = depth,
                Relation = kind,
                ViaQualifiedName = string.Equals(owner, far, StringComparison.Ordinal) ? null : far
            });
        }
    }

    /// <summary>
    ///     Describes a single element in detail: kind, qualified name, supertypes, typing,
    ///     annotations (comments/documentation), applied metadata annotations (type and attribute
    ///     values), and a list of its direct children.
    /// </summary>
    /// <param name="workspace">The loaded workspace.</param>
    /// <param name="element">The target element.</param>
    /// <param name="options">The parsed query options.</param>
    /// <returns>The query result.</returns>
    public static QueryResult Describe(SysmlWorkspace workspace, SysmlNode element, QueryOptions options)
    {
        var qualifiedName = QualifiedNameOf(element, options);
        var summary = new List<string>
        {
            $"Kind: {DescribeKind(element)}",
            $"Qualified name: {qualifiedName}"
        };

        var resolvedSupertypes = workspace.Index
            .GetOutgoingEdges(qualifiedName)
            .Where(e => e.Kind == SysmlEdgeKind.Supertype)
            .Select(e => e.TargetQualifiedName)
            .ToList();
        if (resolvedSupertypes.Count > 0)
        {
            summary.Add($"Supertypes: {string.Join(", ", resolvedSupertypes)}");
        }
        else if (element.SupertypeNames.Count > 0)
        {
            summary.Add($"Supertypes: {string.Join(", ", element.SupertypeNames)}");
        }

        if (element is SysmlFeatureNode { FeatureTyping: { } typing } feature)
        {
            summary.Add($"Typing: {typing}{(feature.Multiplicity is { } m ? $" {m}" : string.Empty)}");
        }

        foreach (var annotation in element.Annotations)
        {
            summary.Add($"{annotation.Kind}: {NormalizeAnnotationText(annotation.Text)}");
        }

        foreach (var metadata in element.Children.OfType<SysmlMetadataNode>())
        {
            if (metadata.Attributes.Count == 0)
            {
                summary.Add($"Metadata {metadata.TypeReference}");
                continue;
            }

            foreach (var attribute in metadata.Attributes)
            {
                summary.Add($"Metadata {metadata.TypeReference}.{attribute.Name}: {FormatMetadataAttributeValue(attribute)}");
            }
        }

        var entries = element.Children
            .Where(c => c.QualifiedName is not null && IsVisible(c.QualifiedName, workspace, options.IncludeStdlib))
            .Select(c => new QueryResultEntry { QualifiedName = c.QualifiedName!, Kind = DescribeKind(c) })
            .ToList();

        summary.Add($"Children: {entries.Count}");

        return new QueryResult
        {
            Verb = "describe",
            Element = qualifiedName,
            Summary = summary,
            Entries = entries
        };
    }

    /// <summary>
    ///     Normalizes a raw <see cref="SysmlAnnotation.Text"/> value for use as a single summary
    ///     line. Annotation text is captured verbatim from the source comment/documentation body
    ///     (including embedded newlines, tabs, and <c>*</c> continuation markers from multi-line
    ///     <c>/* ... */</c> blocks), which is correct for round-tripping but unsuitable for direct
    ///     use in a one-fact-per-line summary/bullet. This collapses the text to a single line by
    ///     splitting on newlines, trimming surrounding whitespace and leading <c>*</c> continuation
    ///     markers from each line, dropping empty lines, and re-joining with single spaces.
    /// </summary>
    /// <param name="text">The raw annotation text.</param>
    /// <returns>The normalized, single-line text.</returns>
    private static string NormalizeAnnotationText(string text)
    {
        var lines = text
            .Split('\n')
            .Select(line => line.Trim().TrimStart('*').Trim())
            .Where(line => line.Length > 0);
        return string.Join(" ", lines);
    }

    /// <summary>
    ///     Formats a single applied <see cref="MetadataAttributeValue"/> for display on a
    ///     <c>"Metadata {Type}.{Attribute}: {value}"</c> summary line. Boolean values render as
    ///     <c>"true"</c>/<c>"false"</c>, numbers via invariant-culture formatting, and strings
    ///     unquoted; any non-scalar (<see cref="MetadataAttributeValueKind.Unsupported"/>) value
    ///     falls back to its verbatim <see cref="MetadataAttributeValue.RawText"/> so the value is
    ///     never silently dropped.
    /// </summary>
    /// <param name="attribute">The attribute value to format.</param>
    /// <returns>The formatted value text.</returns>
    private static string FormatMetadataAttributeValue(MetadataAttributeValue attribute) => attribute.Kind switch
    {
        MetadataAttributeValueKind.Boolean => attribute.BooleanValue!.Value ? "true" : "false",
        MetadataAttributeValueKind.Number => attribute.NumberValue!.Value.ToString(CultureInfo.InvariantCulture),
        MetadataAttributeValueKind.String => attribute.StringValue!,
        _ => attribute.RawText
    };

    /// <summary>
    ///     Reports the specialization/generalization hierarchy of a given element, walking
    ///     resolved <see cref="SysmlEdgeKind.Supertype"/> edges up (toward supertypes), down
    ///     (toward subtypes), or both, per <see cref="QueryOptions.Direction"/>.
    /// </summary>
    /// <param name="workspace">The loaded workspace.</param>
    /// <param name="element">The target element.</param>
    /// <param name="options">The parsed query options.</param>
    /// <returns>The query result.</returns>
    public static QueryResult Hierarchy(SysmlWorkspace workspace, SysmlNode element, QueryOptions options)
    {
        var qualifiedName = QualifiedNameOf(element, options);
        var direction = (options.Direction ?? "both").ToLowerInvariant();
        var entries = new List<QueryResultEntry>();

        if (direction is "up" or "both")
        {
            WalkHierarchy(
                workspace, qualifiedName, options, entries,
                new HashSet<string>(StringComparer.Ordinal) { qualifiedName },
                1, "supertype", edgeSource => workspace.Index.GetOutgoingEdges(edgeSource),
                edge => edge.TargetQualifiedName);
        }

        if (direction is "down" or "both")
        {
            WalkHierarchy(
                workspace, qualifiedName, options, entries,
                new HashSet<string>(StringComparer.Ordinal) { qualifiedName },
                1, "subtype", edgeSource => workspace.Index.GetIncomingEdges(edgeSource),
                edge => edge.SourceQualifiedName);
        }

        return new QueryResult
        {
            Verb = "hierarchy",
            Element = qualifiedName,
            Summary = [$"{entries.Count} related type(s) in the hierarchy of '{qualifiedName}' (direction: {direction})."],
            Entries = entries
        };
    }

    /// <summary>
    ///     Lists the requirements satisfied, verified, or allocated to/from a given element (in
    ///     either direction).
    /// </summary>
    /// <param name="workspace">The loaded workspace.</param>
    /// <param name="element">The target element.</param>
    /// <param name="options">The parsed query options.</param>
    /// <returns>The query result.</returns>
    public static QueryResult Requirements(SysmlWorkspace workspace, SysmlNode element, QueryOptions options)
    {
        var qualifiedName = QualifiedNameOf(element, options);
        var entries = new List<QueryResultEntry>();

        foreach (var edge in workspace.Index.GetOutgoingEdges(qualifiedName))
        {
            if (!RequirementEdgeKinds.Contains(edge.Kind) ||
                !IsVisible(edge.TargetQualifiedName, workspace, options.IncludeStdlib))
            {
                continue;
            }

            entries.Add(new QueryResultEntry
            {
                QualifiedName = edge.TargetQualifiedName,
                Kind = EdgeKindLabel(edge.Kind),
                Detail = OutgoingRequirementLabel(edge.Kind)
            });
        }

        foreach (var edge in workspace.Index.GetIncomingEdges(qualifiedName))
        {
            if (!RequirementEdgeKinds.Contains(edge.Kind) ||
                edge.SourceQualifiedName is not { Length: > 0 } source ||
                !IsVisible(source, workspace, options.IncludeStdlib))
            {
                continue;
            }

            entries.Add(new QueryResultEntry
            {
                QualifiedName = source,
                Kind = EdgeKindLabel(edge.Kind),
                Detail = IncomingRequirementLabel(edge.Kind)
            });
        }

        return new QueryResult
        {
            Verb = "requirements",
            Element = qualifiedName,
            Summary = [$"{entries.Count} requirement relationship(s) for '{qualifiedName}'."],
            Entries = entries
        };
    }

    /// <summary>
    ///     Describes the ports and typed features exposed by a given definition: direct child
    ///     features whose keyword is <c>"port"</c>, or which carry a non-null feature typing.
    /// </summary>
    /// <param name="workspace">The loaded workspace.</param>
    /// <param name="element">The target element.</param>
    /// <param name="options">The parsed query options.</param>
    /// <returns>The query result.</returns>
    public static QueryResult Interface(SysmlWorkspace workspace, SysmlNode element, QueryOptions options)
    {
        var qualifiedName = QualifiedNameOf(element, options);

        var entries = element.Children
            .OfType<SysmlFeatureNode>()
            .Where(f => f.QualifiedName is not null &&
                        (f.FeatureKeyword == "port" || f.FeatureTyping is not null) &&
                        IsVisible(f.QualifiedName, workspace, options.IncludeStdlib))
            .Select(f => new QueryResultEntry
            {
                QualifiedName = f.QualifiedName!,
                Kind = f.FeatureKeyword,
                Detail = FormatTypingDetail(f)
            })
            .ToList();

        return new QueryResult
        {
            Verb = "interface",
            Element = qualifiedName,
            Summary = [$"{entries.Count} port/typed feature(s) exposed by '{qualifiedName}'."],
            Entries = entries
        };
    }

    /// <summary>
    ///     Lists the resolved connection/message endpoints attached to a given element, including
    ///     endpoints reached via a dotted feature chain rooted at, or nested inside, the element.
    /// </summary>
    /// <param name="workspace">The loaded workspace.</param>
    /// <param name="element">The target element.</param>
    /// <param name="options">The parsed query options.</param>
    /// <returns>The query result.</returns>
    public static QueryResult Connections(SysmlWorkspace workspace, SysmlNode element, QueryOptions options)
    {
        var qualifiedName = QualifiedNameOf(element, options);
        var connectEdges = CollectConnectorEdges(workspace, ConnectionsVerbEdgeKinds);
        var entries = new List<QueryResultEntry>();

        foreach (var (source, target, keyword, _) in connectEdges)
        {
            var sourceMatches = IsSelfOrNestedUnder(source, qualifiedName);
            var targetMatches = IsSelfOrNestedUnder(target, qualifiedName);
            if (!sourceMatches && !targetMatches)
            {
                continue;
            }

            var other = sourceMatches ? target : source;
            if (!IsVisible(other, workspace, options.IncludeStdlib))
            {
                continue;
            }

            entries.Add(new QueryResultEntry
            {
                QualifiedName = other,
                Kind = keyword,
                Detail = sourceMatches ? "A" : "B"
            });
        }

        return new QueryResult
        {
            Verb = "connections",
            Element = qualifiedName,
            Summary = [$"{entries.Count} connection endpoint(s) involving '{qualifiedName}'."],
            Entries = entries
        };
    }

    /// <summary>
    ///     Lists the states and guarded transitions nested (directly or transitively) within a
    ///     given state-machine element.
    /// </summary>
    /// <param name="workspace">The loaded workspace.</param>
    /// <param name="element">The target element.</param>
    /// <param name="options">The parsed query options.</param>
    /// <returns>The query result.</returns>
    public static QueryResult States(SysmlWorkspace workspace, SysmlNode element, QueryOptions options)
    {
        var qualifiedName = QualifiedNameOf(element, options);
        var entries = new List<QueryResultEntry>();
        CollectStates(workspace, element, options, entries);

        return new QueryResult
        {
            Verb = "states",
            Element = qualifiedName,
            Summary = [$"{entries.Count} state/transition entry(ies) under '{qualifiedName}'."],
            Entries = entries
        };
    }

    /// <summary>
    ///     Enumerates elements in the workspace, optionally filtered by
    ///     <see cref="QueryOptions.Kind"/> (substring match against the element's display kind)
    ///     and/or <see cref="QueryOptions.NameFilter"/> (substring match against name or
    ///     qualified name).
    /// </summary>
    /// <param name="workspace">The loaded workspace.</param>
    /// <param name="options">The parsed query options.</param>
    /// <returns>The query result.</returns>
    public static QueryResult List(SysmlWorkspace workspace, QueryOptions options)
    {
        var entries = workspace.Declarations
            .Where(kv => IsVisible(kv.Key, workspace, options.IncludeStdlib) && MatchesFilters(kv.Value, kv.Key, options))
            .Select(kv => new QueryResultEntry { QualifiedName = kv.Key, Kind = DescribeKind(kv.Value) })
            .ToList();

        return new QueryResult
        {
            Verb = "list",
            Element = null,
            Summary = [$"{entries.Count} element(s) match the filter."],
            Entries = entries
        };
    }

    /// <summary>
    ///     Searches the workspace for elements matching a kind and/or name filter, using the same
    ///     matching logic as <see cref="List"/>. Requires at least one of
    ///     <see cref="QueryOptions.Kind"/>/<see cref="QueryOptions.NameFilter"/> to avoid an
    ///     unbounded, effectively unfiltered search.
    /// </summary>
    /// <param name="workspace">The loaded workspace.</param>
    /// <param name="options">The parsed query options.</param>
    /// <returns>The query result.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when neither <see cref="QueryOptions.Kind"/> nor
    ///     <see cref="QueryOptions.NameFilter"/> was supplied.
    /// </exception>
    public static QueryResult Find(SysmlWorkspace workspace, QueryOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Kind) && string.IsNullOrWhiteSpace(options.NameFilter))
        {
            throw new ArgumentException(
                "query find: at least one of --kind or --name is required.", nameof(options));
        }

        return List(workspace, options) with { Verb = "find" };
    }

    /// <summary>
    ///     Maps a resolved node to a short, human-readable display kind (definition/feature
    ///     keyword, or a fixed label for structural node types).
    /// </summary>
    /// <param name="node">The node to describe.</param>
    /// <returns>The display kind string.</returns>
    private static string DescribeKind(SysmlNode node) => node switch
    {
        SysmlDefinitionNode def => def.DefinitionKeyword,
        SysmlFeatureNode feature => feature.FeatureKeyword,
        SysmlConnectionNode connection => connection.ConnectionKeyword,
        SysmlImportNode => "import",
        SysmlPackageNode => "package",
        SysmlViewNode => "view",
        SysmlViewpointNode => "viewpoint",
        SysmlTransitionNode => "transition",
        SysmlSatisfyNode => "satisfy",
        _ => node.GetType().Name
    };

    /// <summary>
    ///     Formats a feature's typing and multiplicity as a single detail string (e.g.
    ///     <c>"FuelPort [0..1]"</c>), or <see langword="null"/> when the feature is untyped.
    /// </summary>
    /// <param name="feature">The feature to format.</param>
    /// <returns>The formatted detail string, or <see langword="null"/>.</returns>
    private static string? FormatTypingDetail(SysmlFeatureNode feature)
    {
        if (feature.FeatureTyping is not { } typing)
        {
            return null;
        }

        var multiplicity = feature.Multiplicity is { } m ? $" {m}" : string.Empty;
        return $"{typing}{multiplicity}";
    }

    /// <summary>
    ///     Determines whether a qualified name should be included in results, applying the
    ///     <c>--include-stdlib</c> filter via <see cref="SysmlWorkspace.StdlibNames"/>.
    /// </summary>
    /// <param name="qualifiedName">The qualified name to check.</param>
    /// <param name="workspace">The loaded workspace.</param>
    /// <param name="includeStdlib">Whether <c>--include-stdlib</c> was specified.</param>
    /// <returns><see langword="true"/> when the element should be included.</returns>
    private static bool IsVisible(string qualifiedName, SysmlWorkspace workspace, bool includeStdlib) =>
        includeStdlib || !workspace.StdlibNames.Contains(qualifiedName);

    /// <summary>
    ///     Resolves the effective qualified name for a target element, preferring the node's own
    ///     <see cref="SysmlNode.QualifiedName"/> and falling back to the raw
    ///     <see cref="QueryOptions.Element"/> string used by the caller to look it up.
    /// </summary>
    /// <param name="element">The resolved target element.</param>
    /// <param name="options">The parsed query options.</param>
    /// <returns>The qualified name to report in the result.</returns>
    private static string QualifiedNameOf(SysmlNode element, QueryOptions options) =>
        element.QualifiedName ?? options.Element!;

    /// <summary>
    ///     Converts a <see cref="SysmlEdgeKind"/> to its lowercase display label.
    /// </summary>
    /// <param name="kind">The edge kind.</param>
    /// <returns>The lowercase label.</returns>
    private static string EdgeKindLabel(SysmlEdgeKind kind) => kind.ToString().ToLowerInvariant();

    /// <summary>
    ///     Gets the direction label for an outgoing requirement-relationship edge (the element is
    ///     the source).
    /// </summary>
    /// <param name="kind">The edge kind.</param>
    /// <returns>The direction label.</returns>
    private static string OutgoingRequirementLabel(SysmlEdgeKind kind) => kind switch
    {
        SysmlEdgeKind.Satisfy => "satisfies",
        SysmlEdgeKind.Verify => "verifies",
        SysmlEdgeKind.Allocate => "allocates-to",
        _ => EdgeKindLabel(kind)
    };

    /// <summary>
    ///     Gets the direction label for an incoming requirement-relationship edge (the element is
    ///     the target).
    /// </summary>
    /// <param name="kind">The edge kind.</param>
    /// <returns>The direction label.</returns>
    private static string IncomingRequirementLabel(SysmlEdgeKind kind) => kind switch
    {
        SysmlEdgeKind.Satisfy => "satisfied-by",
        SysmlEdgeKind.Verify => "verified-by",
        SysmlEdgeKind.Allocate => "allocated-from",
        _ => EdgeKindLabel(kind)
    };

    /// <summary>
    ///     Recursively walks resolved <see cref="SysmlEdgeKind.Supertype"/> edges in one direction
    ///     (up toward supertypes, or down toward subtypes), cycle-guarded by
    ///     <paramref name="visited"/>, appending one entry per newly-reached qualified name.
    /// </summary>
    private static void WalkHierarchy(
        SysmlWorkspace workspace,
        string qualifiedName,
        QueryOptions options,
        List<QueryResultEntry> entries,
        HashSet<string> visited,
        int depth,
        string label,
        Func<string, IReadOnlyList<SysmlEdge>> getEdges,
        Func<SysmlEdge, string?> selectNext)
    {
        foreach (var edge in getEdges(qualifiedName))
        {
            if (edge.Kind != SysmlEdgeKind.Supertype)
            {
                continue;
            }

            var next = selectNext(edge);
            if (next is null || !visited.Add(next))
            {
                continue;
            }

            if (IsVisible(next, workspace, options.IncludeStdlib))
            {
                entries.Add(new QueryResultEntry
                {
                    QualifiedName = next,
                    Kind = label,
                    Detail = $"depth {depth}",
                    Depth = depth
                });
            }

            WalkHierarchy(workspace, next, options, entries, visited, depth + 1, label, getEdges, selectNext);
        }
    }

    /// <summary>
    ///     Determines whether a declaration matches the <see cref="QueryOptions.Kind"/> and
    ///     <see cref="QueryOptions.NameFilter"/> substring filters used by <see cref="List"/>/
    ///     <see cref="Find"/>.
    /// </summary>
    private static bool MatchesFilters(SysmlNode node, string qualifiedName, QueryOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Kind) &&
            !DescribeKind(node).Contains(options.Kind, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(options.NameFilter) &&
            !(node.Name?.Contains(options.NameFilter, StringComparison.OrdinalIgnoreCase) ?? false) &&
            !qualifiedName.Contains(options.NameFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Collects every resolved connector edge in the workspace whose kind is in
    ///     <paramref name="kinds"/>, together with its originating connector's keyword
    ///     (<c>connect</c>, <c>connection</c>, <c>message</c>, or <c>bind</c>), by walking every
    ///     node reachable from <see cref="SysmlWorkspace.Declarations"/> and reading each
    ///     connector node's own <see cref="SysmlNode.ResolvedEdges"/> (populated in-place by
    ///     <c>ReferenceResolver</c> regardless of whether the connector node itself is named).
    /// </summary>
    /// <param name="workspace">The loaded workspace.</param>
    /// <param name="kinds">The connector edge kinds to collect.</param>
    /// <returns>The list of resolved connector edges with their originating keyword and kind.</returns>
    /// <remarks>
    ///     The connector edges themselves <em>are</em> present in
    ///     <see cref="SemanticIndex.AllEdges"/> (<c>ReferenceResolver</c>'s feature-chain
    ///     resolution pass appends them to the aggregate edge list that builds the index). The
    ///     node walk is nevertheless required because a <see cref="SysmlEdge"/> carries only
    ///     <c>(Source, Target, Kind)</c> and not the originating connector's keyword, which the
    ///     <c>connections</c> verb reports as each entry's <c>Kind</c> and which the
    ///     <c>impact</c> verb reports in each connection entry's notes. Sharing this one
    ///     collector between both verbs also guarantees they can never disagree about the
    ///     workspace's connection topology.
    /// </remarks>
    private static List<(string Source, string Target, string Keyword, SysmlEdgeKind Kind)> CollectConnectorEdges(
        SysmlWorkspace workspace,
        IReadOnlyList<SysmlEdgeKind> kinds)
    {
        var results = new List<(string, string, string, SysmlEdgeKind)>();
        var visited = new HashSet<SysmlNode>();

        void Walk(SysmlNode node)
        {
            if (!visited.Add(node))
            {
                return;
            }

            var keyword = node is SysmlConnectionNode connection ? connection.ConnectionKeyword : "connect";
            foreach (var edge in node.ResolvedEdges)
            {
                if (kinds.Contains(edge.Kind) && edge.SourceQualifiedName is { Length: > 0 } source)
                {
                    results.Add((source, edge.TargetQualifiedName, keyword, edge.Kind));
                }
            }

            foreach (var child in node.Children)
            {
                Walk(child);
            }
        }

        foreach (var declaration in workspace.Declarations.Values)
        {
            Walk(declaration);
        }

        return results;
    }

    /// <summary>
    ///     Determines whether <paramref name="candidate"/> is the subject element itself or a
    ///     feature nested (at any depth) inside it, using the qualified-name containment prefix
    ///     rule. Shared by <see cref="Connections"/> and <see cref="Impact"/> so both verbs
    ///     agree on what "belongs to this element" means.
    /// </summary>
    /// <param name="candidate">The candidate qualified name (typically a connector endpoint).</param>
    /// <param name="subjectQualifiedName">The subject element's qualified name.</param>
    /// <returns><see langword="true"/> when the candidate is the subject or nested under it.</returns>
    private static bool IsSelfOrNestedUnder(string? candidate, string subjectQualifiedName) =>
        candidate is not null &&
        (string.Equals(candidate, subjectQualifiedName, StringComparison.Ordinal) ||
         candidate.StartsWith(subjectQualifiedName + "::", StringComparison.Ordinal));

    /// <summary>
    ///     Determines whether an edge kind is one of the connector kinds the <c>impact</c> verb
    ///     handles through its dedicated, rolled-up connector pass.
    /// </summary>
    /// <param name="kind">The edge kind to test.</param>
    /// <returns>
    ///     <see langword="true"/> when <paramref name="kind"/> is in
    ///     <see cref="ImpactConnectorEdgeKinds"/> and must therefore be excluded from the
    ///     reference-edge closure.
    /// </returns>
    private static bool IsImpactConnectorKind(SysmlEdgeKind kind) =>
        Array.IndexOf(ImpactConnectorEdgeKinds, kind) >= 0;

    /// <summary>
    ///     Rolls a connector endpoint up to the nearest owning declaration that is a meaningful
    ///     impact subject. The endpoint itself is probed first and returned unchanged when it is
    ///     present in <see cref="SysmlWorkspace.Declarations"/> and is not an endpoint-only
    ///     construct (for example a directly connected sibling part usage). Endpoints that are
    ///     absent from <see cref="SysmlWorkspace.Declarations"/> — frequently ports inherited
    ///     through a typed usage — and endpoints that are declared but endpoint-only, per
    ///     <see cref="IsEndpointOnlyDeclaration"/>, have trailing <c>::</c> segments stripped
    ///     until such a declaration is found.
    /// </summary>
    /// <remarks>
    ///     Skipping declared endpoint-only constructs matters because whether a port endpoint is
    ///     itself a key in <see cref="SysmlWorkspace.Declarations"/> is an artifact of modeling
    ///     style, not of meaning. A port declared on a definition (<c>part def Hub { port J1; }</c>)
    ///     reached through a typed usage yields the path <c>System::hub::J1</c>, which is not a
    ///     declaration key, whereas the same port declared inline on the usage
    ///     (<c>part hub { port J1; }</c>) yields a path that <i>is</i> a declaration key. Both
    ///     forms are legal and equivalent, so stopping at the first declared ancestor would make
    ///     the answer depend on the model's spelling: it would report a port — never an
    ///     actionable answer to "what parts are impacted" — and, because the reported name is
    ///     also the name enqueued onto the traversal frontier, it would dead-end the walk at the
    ///     port instead of continuing through the owning part.
    /// </remarks>
    /// <param name="workspace">The loaded workspace.</param>
    /// <param name="qualifiedName">The connector endpoint's qualified name.</param>
    /// <returns>
    ///     <paramref name="qualifiedName"/> itself when it is declared and is not endpoint-only,
    ///     otherwise the nearest declared, non-endpoint-only owning qualified name, or
    ///     <paramref name="qualifiedName"/> unchanged when no such ancestor exists, so a
    ///     connection is never silently dropped.
    /// </returns>
    private static string RollUpToNearestDeclaration(SysmlWorkspace workspace, string qualifiedName)
    {
        var probe = qualifiedName;
        while (true)
        {
            // Accept the probe only when it names a declaration that a caller could act on and
            // usefully continue the impact walk from - a declared port is neither.
            if (workspace.Declarations.TryGetValue(probe, out var declaration) &&
                !IsEndpointOnlyDeclaration(declaration))
            {
                return probe;
            }

            // Strip one containment segment and retry; exhausting the path yields the original
            // endpoint rather than nothing, so the connection is still reported.
            var index = probe.LastIndexOf("::", StringComparison.Ordinal);
            if (index < 0)
            {
                return qualifiedName;
            }

            probe = probe[..index];
        }
    }

    /// <summary>
    ///     Determines whether a declaration is an endpoint-only construct: an element that exists
    ///     solely to be named as a connector endpoint and is therefore never a valid subject of an
    ///     impact answer.
    /// </summary>
    /// <remarks>
    ///     Ports are the only such construct in the modeled node hierarchy. A port has no
    ///     behavior or state of its own to be impacted - it is the attachment point through which
    ///     its owning part is impacted - so an impact row naming a port is a name the caller can
    ///     neither act on nor feed back into another query. Classified structurally, from
    ///     <see cref="SysmlFeatureNode.FeatureKeyword"/> and
    ///     <see cref="SysmlDefinitionNode.DefinitionKeyword"/>, so the decision follows the parsed
    ///     model rather than any naming convention in the source text.
    /// </remarks>
    /// <param name="declaration">The resolved declaration to classify. Must not be null.</param>
    /// <returns>
    ///     <see langword="true"/> when <paramref name="declaration"/> is a port usage or port
    ///     definition; otherwise <see langword="false"/>.
    /// </returns>
    private static bool IsEndpointOnlyDeclaration(SysmlNode declaration) => declaration switch
    {
        SysmlFeatureNode feature =>
            string.Equals(feature.FeatureKeyword, PortFeatureKeyword, StringComparison.Ordinal),
        SysmlDefinitionNode definition =>
            string.Equals(definition.DefinitionKeyword, PortDefinitionKeyword, StringComparison.Ordinal),
        _ => false
    };

    /// <summary>
    ///     Recursively collects state entries (child <see cref="SysmlFeatureNode"/> with
    ///     <c>FeatureKeyword == "state"</c>) and transition entries (child
    ///     <see cref="SysmlTransitionNode"/>, preferring its resolved
    ///     <see cref="SysmlEdgeKind.Transition"/> edge over its raw <c>Source</c>/<c>Target</c>
    ///     text) nested anywhere under <paramref name="node"/>.
    /// </summary>
    private static void CollectStates(
        SysmlWorkspace workspace, SysmlNode node, QueryOptions options, List<QueryResultEntry> entries)
    {
        foreach (var child in node.Children)
        {
            switch (child)
            {
                case SysmlFeatureNode { FeatureKeyword: "state" } state when state.QualifiedName is { } stateName:
                    if (IsVisible(stateName, workspace, options.IncludeStdlib))
                    {
                        entries.Add(new QueryResultEntry { QualifiedName = stateName, Kind = "state" });
                    }

                    break;

                case SysmlTransitionNode transition:
                    {
                        var resolved = transition.ResolvedEdges.FirstOrDefault(e => e.Kind == SysmlEdgeKind.Transition);
                        var target = resolved?.TargetQualifiedName ?? transition.Target;
                        var source = resolved?.SourceQualifiedName ?? transition.Source;

                        if (target is not null && IsVisible(target, workspace, options.IncludeStdlib))
                        {
                            var detail = source is not null ? $"{source} -> {target}" : $"-> {target}";
                            if (transition.Guard is { } guard)
                            {
                                detail += $" if {guard}";
                            }

                            entries.Add(new QueryResultEntry { QualifiedName = target, Kind = "transition", Detail = detail });
                        }

                        break;
                    }
            }

            CollectStates(workspace, child, options, entries);
        }
    }
}
