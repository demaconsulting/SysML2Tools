// <copyright file="QueryResult.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Text.Json.Serialization;

namespace DemaConsulting.SysML2Tools.Query;

/// <summary>
///     Uniform result shape produced by every <see cref="QueryEngine"/> verb method, consumed
///     by <see cref="QueryResultRenderer"/> to render either Markdown or JSON output.
/// </summary>
/// <remarks>
///     A single shared shape (rather than one result type per verb) allows one non-duplicated
///     rendering layer to serve all 12 verbs: <see cref="Summary"/> carries free-form narrative
///     lines (e.g. <c>describe</c>'s kind/supertypes/annotations), and <see cref="Entries"/>
///     carries the tabular list of related elements every verb ultimately reports.
/// </remarks>
public sealed record QueryResult
{
    /// <summary>
    ///     Gets the kebab-case verb token that produced this result (e.g. <c>"used-by"</c>).
    /// </summary>
    public required string Verb { get; init; }

    /// <summary>
    ///     Gets the qualified name of the target element, or <see langword="null"/> for the
    ///     workspace-wide <c>list</c>/<c>find</c> verbs.
    /// </summary>
    public string? Element { get; init; }

    /// <summary>
    ///     Gets free-form narrative summary lines (e.g. counts, kind/supertype/annotation text
    ///     for <c>describe</c>). Rendered as a bullet list preceding the entries table.
    /// </summary>
    public IReadOnlyList<string> Summary { get; init; } = [];

    /// <summary>
    ///     Gets the list of result entries (related elements, transitions, etc.). Sorted by
    ///     <see cref="QueryResultEntry.QualifiedName"/> (ordinal) once, by
    ///     <see cref="QueryResultRenderer"/>, guaranteeing deterministic output regardless of
    ///     which verb produced the (potentially unordered) list.
    /// </summary>
    public IReadOnlyList<QueryResultEntry> Entries { get; init; } = [];
}

/// <summary>
///     One row of a <see cref="QueryResult"/>: a related element (or transition/state) and its
///     relationship to the queried element.
/// </summary>
public sealed record QueryResultEntry
{
    /// <summary>
    ///     Gets the qualified name of the related element.
    /// </summary>
    public required string QualifiedName { get; init; }

    /// <summary>
    ///     Gets a short kind/relationship label (e.g. <c>"supertype"</c>, <c>"typing"</c>,
    ///     <c>"part def"</c>, <c>"state"</c>). <see langword="null"/> when not meaningful.
    /// </summary>
    public string? Kind { get; init; }

    /// <summary>
    ///     Gets a short free-form detail string (e.g. a traversal depth, a role label, a
    ///     transition's source/guard text). <see langword="null"/> when not meaningful.
    /// </summary>
    public string? Detail { get; init; }

    /// <summary>
    ///     Gets additional free-form note lines for this entry. Usually empty; reserved for
    ///     verbs that need to attach more than one piece of extra context to a single entry.
    /// </summary>
    public IReadOnlyList<string> Notes { get; init; } = [];

    /// <summary>
    ///     Gets the traversal direction of this entry relative to the queried element. Only
    ///     populated by the <c>dependencies</c> verb (which combines <c>uses</c>/<c>used-by</c>
    ///     results); <see langword="null"/> for every other verb. Omitted from JSON output
    ///     entirely when <see langword="null"/>, so this addition does not change the JSON
    ///     shape of any other verb.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public QueryEntryDirection? Direction { get; init; }
}

/// <summary>
///     The traversal direction of a <see cref="QueryResultEntry"/> relative to the queried
///     element, populated only by the <c>dependencies</c> verb.
/// </summary>
public enum QueryEntryDirection
{
    /// <summary>The entry is an element the queried element depends on (an outgoing reference).</summary>
    Outgoing,

    /// <summary>The entry is an element that depends on the queried element (an incoming reference).</summary>
    Incoming
}
