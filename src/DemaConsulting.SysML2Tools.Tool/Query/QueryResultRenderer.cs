// Copyright (c) DEMA Consulting
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Text.Json;

namespace DemaConsulting.SysML2Tools.Query;

/// <summary>
///     Shared, non-duplicated rendering layer for <see cref="QueryResult"/>: two pure methods
///     converting the uniform result shape into Markdown lines or a JSON string. Every
///     <see cref="QueryCommand"/> verb arm renders through this type instead of formatting its
///     own output, guaranteeing consistent, deterministically-ordered output across all 11
///     verbs.
/// </summary>
internal static class QueryResultRenderer
{
    /// <summary>
    ///     Renders a <see cref="QueryResult"/> as Markdown lines: a heading, an optional summary
    ///     bullet list, then a compact table of entries sorted by
    ///     <see cref="QueryResultEntry.QualifiedName"/> (ordinal).
    /// </summary>
    /// <param name="result">The result to render.</param>
    /// <returns>The Markdown output, one line per list entry.</returns>
    public static IReadOnlyList<string> RenderMarkdown(QueryResult result)
    {
        var lines = new List<string>
        {
            result.Element is not null
                ? $"# query {result.Verb}: {result.Element}"
                : $"# query {result.Verb}",
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
