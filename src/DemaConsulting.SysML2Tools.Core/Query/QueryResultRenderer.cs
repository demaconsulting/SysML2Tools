// <copyright file="QueryResultRenderer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Text.Json;
using DemaConsulting.SysML2Tools.Utilities;

namespace DemaConsulting.SysML2Tools.Query;

/// <summary>
///     Shared, non-duplicated rendering layer for <see cref="QueryResult"/>: two pure methods
///     converting the uniform result shape into Markdown lines or a JSON string. Every caller
///     (e.g. the Tool project's <c>query</c> CLI command) renders through this type instead of
///     formatting its own output, guaranteeing consistent, deterministically-ordered output
///     across all 12 verbs.
/// </summary>
public static class QueryResultRenderer
{
    /// <summary>
    ///     Renders a <see cref="QueryResult"/> as Markdown lines: a heading, an optional summary
    ///     bullet list, then a compact table of entries sorted by
    ///     <see cref="QueryResultEntry.QualifiedName"/> (ordinal). The <c>dependencies</c> verb
    ///     is the sole exception: after the heading, its entries are rendered as prose bullets
    ///     (see <see cref="RenderDependenciesBody"/>) rather than a table.
    /// </summary>
    /// <param name="result">The result to render.</param>
    /// <param name="depth">
    ///     The Markdown heading depth (number of leading <c>#</c> characters), typically sourced
    ///     from a caller's own global heading-depth option (e.g. the Tool project's <c>--depth</c>
    ///     flag). Defaults to 1 (a top-level <c>#</c> heading); expected to be pre-validated to
    ///     the range 1-6 by the caller.
    /// </param>
    /// <param name="heading">
    ///     A custom heading text, supplied via <c>query</c>'s own <c>--heading</c> flag, replacing
    ///     the auto-generated <c>"query {verb}[: {element}]"</c> text. Defaults to
    ///     <see langword="null"/>, which uses the auto-generated text.
    /// </param>
    /// <returns>The Markdown output, one line per list entry.</returns>
    public static IReadOnlyList<string> RenderMarkdown(
        QueryResult result, int depth = 1, string? heading = null)
    {
        var headingPrefix = new string('#', depth);
        var headingText = heading ?? (result.Element is not null
            ? $"query {result.Verb}: {result.Element}"
            : $"query {result.Verb}");

        var lines = new List<string>
        {
            $"{headingPrefix} {headingText}",
            ""
        };

        if (result.Summary.Count > 0)
        {
            foreach (var line in result.Summary)
            {
                lines.Add($"- {line}");
            }

            lines.Add("");
        }

        if (result.Verb == "dependencies")
        {
            var sortedEntries = SortEntries(result.Entries);
            var element = result.Element ?? "";

            // Shorten every name in this result together - the subject element plus both
            // directions' entries - so the subject sentence and every bullet agree on the same
            // stripped prefix instead of each being shortened independently
            var pool = new List<string> { element };
            pool.AddRange(sortedEntries.Select(e => e.QualifiedName));
            var shortened = QualifiedNameShortener.Shorten(pool);

            lines.AddRange(RenderDependenciesBody(sortedEntries, element, shortened));
            return lines;
        }

        var sorted = SortEntries(result.Entries);
        if (sorted.Count == 0)
        {
            lines.Add("_No entries._");
            return lines;
        }

        lines.Add("| Qualified Name | Kind | Detail |");
        lines.Add("| --- | --- | --- |");
        foreach (var entry in sorted)
        {
            lines.Add($"| {entry.QualifiedName} | {entry.Kind ?? ""} | {FormatDetail(entry)} |");
        }

        return lines;
    }

    /// <summary>
    ///     Renders a <see cref="QueryResult"/> as an indented JSON document via the
    ///     <see cref="QueryResultSerializerContext"/> source-generated serializer, with entries
    ///     sorted by <see cref="QueryResultEntry.QualifiedName"/> (ordinal) exactly as
    ///     <see cref="RenderMarkdown"/> orders its table, so the two formats always agree on
    ///     entry order.
    /// </summary>
    /// <param name="result">The result to render.</param>
    /// <returns>The JSON document text.</returns>
    public static string RenderJson(QueryResult result)
    {
        var sorted = result with { Entries = SortEntries(result.Entries) };
        return JsonSerializer.Serialize(sorted, QueryResultSerializerContext.Default.QueryResult);
    }

    /// <summary>
    ///     Sorts entries by qualified name (ordinal comparison), the single point where
    ///     deterministic ordering is enforced for every verb and both output formats.
    /// </summary>
    /// <param name="entries">The entries to sort.</param>
    /// <returns>A new, sorted list.</returns>
    private static List<QueryResultEntry> SortEntries(IReadOnlyList<QueryResultEntry> entries) =>
        [.. entries.OrderBy(e => e.QualifiedName, StringComparer.Ordinal)];

    /// <summary>
    ///     Renders the <c>dependencies</c> verb's body as prose bullets rather than a table: an
    ///     intro sentence plus one "Depends on" bullet per outgoing entry (or a single "has no
    ///     outgoing references" line when there are none), then symmetrically an intro sentence
    ///     plus one "Used by" bullet per incoming entry (or a single "No elements reference"
    ///     line when there are none).
    /// </summary>
    /// <param name="sortedEntries">
    ///     The merged <c>uses</c>/<c>used-by</c> entries, already sorted by
    ///     <see cref="QueryResultEntry.QualifiedName"/> (ordinal); filtering this list by
    ///     <see cref="QueryResultEntry.Direction"/> preserves that order within each group.
    /// </param>
    /// <param name="element">The qualified name of the queried element.</param>
    /// <param name="shortened">
    ///     A map from each original qualified name (the queried element plus every entry's
    ///     <see cref="QueryResultEntry.QualifiedName"/>) to its <see cref="QualifiedNameShortener"/>
    ///     -shortened form, applied to the subject sentence and every bullet so the whole body
    ///     agrees on the same stripped common prefix.
    /// </param>
    /// <returns>The Markdown lines for the dependencies body.</returns>
    private static IReadOnlyList<string> RenderDependenciesBody(
        IReadOnlyList<QueryResultEntry> sortedEntries,
        string element,
        IReadOnlyDictionary<string, string> shortened)
    {
        var outgoing = sortedEntries.Where(e => e.Direction == QueryEntryDirection.Outgoing).ToList();
        var incoming = sortedEntries.Where(e => e.Direction == QueryEntryDirection.Incoming).ToList();
        var shortElement = shortened.GetValueOrDefault(element, element);

        var lines = new List<string>();

        if (outgoing.Count == 0)
        {
            lines.Add($"{shortElement} has no outgoing references.");
        }
        else
        {
            lines.Add($"{shortElement} references the following elements:");
            lines.Add("");
            foreach (var entry in outgoing)
            {
                var shortName = shortened.GetValueOrDefault(entry.QualifiedName, entry.QualifiedName);
                lines.Add($"- Depends on **{shortName}** ({entry.Kind})");
            }
        }

        lines.Add("");

        if (incoming.Count == 0)
        {
            lines.Add($"No elements reference {shortElement}.");
        }
        else
        {
            lines.Add($"The following elements reference {shortElement}:");
            lines.Add("");
            foreach (var entry in incoming)
            {
                var shortName = shortened.GetValueOrDefault(entry.QualifiedName, entry.QualifiedName);
                lines.Add($"- Used by **{shortName}** ({entry.Kind})");
            }
        }

        return lines;
    }

    /// <summary>
    ///     Combines an entry's <see cref="QueryResultEntry.Detail"/> and
    ///     <see cref="QueryResultEntry.Notes"/> into a single Markdown table cell.
    /// </summary>
    /// <param name="entry">The entry to format.</param>
    /// <returns>The combined detail text (may be empty).</returns>
    private static string FormatDetail(QueryResultEntry entry)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(entry.Detail))
        {
            parts.Add(entry.Detail);
        }

        parts.AddRange(entry.Notes);
        return string.Join("; ", parts);
    }
}
