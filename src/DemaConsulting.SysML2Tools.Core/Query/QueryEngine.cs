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
    ///     specified (unlimited otherwise).
    /// </summary>
    /// <param name="workspace">The loaded workspace.</param>
    /// <param name="element">The target element.</param>
    /// <param name="options">The parsed query options.</param>
    /// <returns>The query result.</returns>
    public static QueryResult Impact(SysmlWorkspace workspace, SysmlNode element, QueryOptions options)
    {
        var qualifiedName = QualifiedNameOf(element, options);
        var visited = new HashSet<string>(StringComparer.Ordinal) { qualifiedName };
        var entries = new List<QueryResultEntry>();
        var frontier = new List<string> { qualifiedName };
        var depth = 0;

        while (frontier.Count > 0 && (options.WalkDepth is not { } maxDepth || depth < maxDepth))
        {
            depth++;
            var next = new List<string>();

            foreach (var current in frontier)
            {
                foreach (var edge in workspace.Index.GetIncomingEdges(current))
                {
                    if (edge.SourceQualifiedName is not { Length: > 0 } source || !visited.Add(source))
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
                        Detail = $"depth {depth}"
                    });
                }
            }

            frontier = next;
        }

        var depthSuffix = options.WalkDepth is { } d ? $" (depth <= {d})" : string.Empty;
        return new QueryResult
        {
            Verb = "impact",
            Element = qualifiedName,
            Summary = [$"{entries.Count} element(s) transitively impacted by a change to '{qualifiedName}'{depthSuffix}."],
            Entries = entries
        };
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
        var prefix = qualifiedName + "::";
        var connectEdges = CollectConnectEdges(workspace);
        var entries = new List<QueryResultEntry>();

        bool Matches(string? name) => name is not null && (name == qualifiedName || name.StartsWith(prefix, StringComparison.Ordinal));

        foreach (var (source, target, keyword) in connectEdges)
        {
            var sourceMatches = Matches(source);
            var targetMatches = Matches(target);
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
                entries.Add(new QueryResultEntry { QualifiedName = next, Kind = label, Detail = $"depth {depth}" });
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
    ///     Collects every resolved <see cref="SysmlEdgeKind.Connect"/> edge in the workspace,
    ///     together with its originating connector's keyword (<c>connect</c>, <c>connection</c>,
    ///     or <c>message</c>), by walking every node reachable from
    ///     <see cref="SysmlWorkspace.Declarations"/> and reading each connector node's own
    ///     <see cref="SysmlNode.ResolvedEdges"/> (populated in-place by <c>ReferenceResolver</c>
    ///     regardless of whether the connector node itself is named). Connect edges are not
    ///     exposed via <see cref="SemanticIndex.AllEdges"/>, so this walk is required.
    /// </summary>
    /// <param name="workspace">The loaded workspace.</param>
    /// <returns>The list of resolved connect edges with their originating keyword.</returns>
    private static List<(string Source, string Target, string Keyword)> CollectConnectEdges(SysmlWorkspace workspace)
    {
        var results = new List<(string, string, string)>();
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
                if (edge.Kind == SysmlEdgeKind.Connect && edge.SourceQualifiedName is { Length: > 0 } source)
                {
                    results.Add((source, edge.TargetQualifiedName, keyword));
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
