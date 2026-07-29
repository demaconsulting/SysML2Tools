// <copyright file="QueryOptions.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Query;

/// <summary>
///     Immutable set of options for one <see cref="QueryEngine"/> invocation.
/// </summary>
/// <remarks>
///     Every field is shared across all 12 <see cref="QueryVerb"/> values so that a single
///     parsing/construction pass can build one instance regardless of which verb was supplied;
///     not every field is meaningful for every verb (see the per-field remarks below and the
///     verb-grammar table in <c>docs/design/sysml2-tools-core/query.md</c>). Does not carry any
///     file-glob or CLI-I/O concept (e.g., input file patterns, an output file path) — those are
///     entirely the caller's concern (for the Tool project's <c>query</c> CLI command, see
///     <c>Query.QueryCliArgumentParser</c> in the Tool project).
/// </remarks>
public sealed record QueryOptions
{
    /// <summary>
    ///     Gets the verb selecting which model-analysis operation to perform.
    /// </summary>
    public required QueryVerb Verb { get; init; }

    /// <summary>
    ///     Gets the qualified name of the target element.
    /// </summary>
    /// <remarks>
    ///     Required for every verb except <see cref="QueryVerb.List"/> and
    ///     <see cref="QueryVerb.Find"/>; <see langword="null"/> when not supplied.
    /// </remarks>
    public string? Element { get; init; }

    /// <summary>
    ///     Gets the output format.
    /// </summary>
    /// <remarks>
    ///     Accepted values are <c>"markdown"</c> (default when <see langword="null"/>) and
    ///     <c>"json"</c>; interpreted by the caller (e.g., the Tool project's <c>query</c> CLI
    ///     command), not by <see cref="QueryEngine"/> itself.
    /// </remarks>
    public string? Format { get; init; }

    /// <summary>
    ///     Gets the maximum impact-walk traversal depth.
    /// </summary>
    /// <remarks>
    ///     Only meaningful for <see cref="QueryVerb.Impact"/>, where it bounds the transitive
    ///     impact walk; <see langword="null"/> means unlimited. When
    ///     <see cref="IncludeConnections"/> is also set, this value additionally bounds the
    ///     number of connector hops taken along any single traversal path, and
    ///     <see langword="null"/> then means "one connector hop" rather than "unlimited" (see
    ///     <see cref="IncludeConnections"/>).
    /// </remarks>
    public int? WalkDepth { get; init; }

    /// <summary>
    ///     Gets the custom Markdown heading text.
    /// </summary>
    /// <remarks>
    ///     Markdown output only; has no effect on JSON output.
    ///     <see langword="null"/> means the auto-generated <c>"query {verb}[: {element}]"</c>
    ///     text is used instead by <see cref="QueryResultRenderer.RenderMarkdown"/>.
    /// </remarks>
    public string? Heading { get; init; }

    /// <summary>
    ///     Gets the traversal direction.
    /// </summary>
    /// <remarks>
    ///     Only meaningful for <see cref="QueryVerb.Hierarchy"/>. Accepted values are
    ///     <c>"up"</c>, <c>"down"</c>, and <c>"both"</c>; <see langword="null"/> when not
    ///     supplied.
    /// </remarks>
    public string? Direction { get; init; }

    /// <summary>
    ///     Gets the element-kind filter.
    /// </summary>
    /// <remarks>
    ///     Only meaningful for <see cref="QueryVerb.List"/> and <see cref="QueryVerb.Find"/>;
    ///     <see langword="null"/> means no kind filtering.
    /// </remarks>
    public string? Kind { get; init; }

    /// <summary>
    ///     Gets the name substring filter.
    /// </summary>
    /// <remarks>
    ///     Only meaningful for <see cref="QueryVerb.List"/> and <see cref="QueryVerb.Find"/>;
    ///     <see langword="null"/> means no name filtering.
    /// </remarks>
    public string? NameFilter { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the OMG standard library should be included in
    ///     results.
    /// </summary>
    /// <remarks>
    ///     Applies to every verb; defaults to <see langword="false"/> (stdlib elements are
    ///     excluded from results unless explicitly requested).
    /// </remarks>
    public bool IncludeStdlib { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the impact walk should also follow connector
    ///     (<c>connect</c>/<c>bind</c>) relationships in addition to resolved reference edges.
    /// </summary>
    /// <remarks>
    ///     Only meaningful for <see cref="QueryVerb.Impact"/>; defaults to
    ///     <see langword="false"/>, so the default <c>impact</c> semantics (a reverse-only
    ///     closure over resolved reference edges) are unchanged. When set, <c>Connect</c> and
    ///     <c>Binding</c> semantic edges are traversed <em>undirected</em> (a connector's two
    ///     ends carry no semantic
    ///     source-causes-target direction), and endpoints are rolled up through containment in
    ///     both directions so a <c>part</c> subject matches a connector attached to one of its
    ///     nested ports and a far-side port is attributed to its nearest owning declaration.
    ///     Because connector graphs are dense meshes, connector hops along a single traversal
    ///     path are bounded by <see cref="WalkDepth"/> when supplied, and by one hop when
    ///     <see cref="WalkDepth"/> is <see langword="null"/>.
    /// </remarks>
    public bool IncludeConnections { get; init; }
}
