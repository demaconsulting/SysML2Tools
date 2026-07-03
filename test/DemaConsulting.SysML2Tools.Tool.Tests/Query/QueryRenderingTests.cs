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
using DemaConsulting.SysML2Tools.Query;

namespace DemaConsulting.SysML2Tools.Tests.Query;

/// <summary>
///     Format-parity suite: verifies that Markdown and JSON output agree on entry content and
///     ordering, and unit-tests <see cref="QueryResultRenderer"/> directly against hand-built
///     <see cref="QueryResult"/> instances for edge cases not worth loading a whole workspace for.
/// </summary>
[Collection("Sequential")]
public class QueryRenderingTests
{
    /// <summary>
    ///     RenderMarkdown on a result with no entries reports "No entries" rather than an empty
    ///     table.
    /// </summary>
    [Fact]
    public void RenderMarkdown_NoEntries_ReportsNoEntries()
    {
        var result = new QueryResult { Verb = "uses", Element = "Model::Foo", Summary = ["0 outgoing reference(s)."] };

        var lines = QueryResultRenderer.RenderMarkdown(result);

        Assert.Contains(lines, l => l.Contains("No entries"));
    }

    /// <summary>
    ///     RenderMarkdown sorts entries by qualified name (ordinal), regardless of input order.
    /// </summary>
    [Fact]
    public void RenderMarkdown_UnorderedEntries_SortsByQualifiedNameOrdinal()
    {
        var result = new QueryResult
        {
            Verb = "list",
            Entries =
            [
                new QueryResultEntry { QualifiedName = "Model::Zebra" },
                new QueryResultEntry { QualifiedName = "Model::Apple" },
                new QueryResultEntry { QualifiedName = "Model::Mango" }
            ]
        };

        var lines = QueryResultRenderer.RenderMarkdown(result);
        var tableRows = lines.Where(l => l.StartsWith("| Model::")).ToList();

        Assert.Equal(3, tableRows.Count);
        Assert.StartsWith("| Model::Apple", tableRows[0]);
        Assert.StartsWith("| Model::Mango", tableRows[1]);
        Assert.StartsWith("| Model::Zebra", tableRows[2]);
    }

    /// <summary>
    ///     RenderMarkdown combines an entry's Detail and Notes into a single table cell.
    /// </summary>
    [Fact]
    public void RenderMarkdown_EntryWithDetailAndNotes_CombinesIntoOneCell()
    {
        var result = new QueryResult
        {
            Verb = "describe",
            Entries =
            [
                new QueryResultEntry
                {
                    QualifiedName = "Model::Foo",
                    Kind = "part",
                    Detail = "depth 1",
                    Notes = ["extra note"]
                }
            ]
        };

        var lines = QueryResultRenderer.RenderMarkdown(result);

        Assert.Contains(lines, l => l.Contains("depth 1; extra note"));
    }

    /// <summary>
    ///     RenderJson round-trips through System.Text.Json and preserves the verb, element, and
    ///     entries.
    /// </summary>
    [Fact]
    public void RenderJson_RoundTrips_PreservesShape()
    {
        var result = new QueryResult
        {
            Verb = "uses",
            Element = "Model::Foo",
            Summary = ["1 outgoing reference(s)."],
            Entries = [new QueryResultEntry { QualifiedName = "Model::Bar", Kind = "supertype" }]
        };

        var json = QueryResultRenderer.RenderJson(result);
        var deserialized = JsonSerializer.Deserialize(json, QueryResultSerializerContext.Default.QueryResult);

        Assert.NotNull(deserialized);
        Assert.Equal("uses", deserialized!.Verb);
        Assert.Equal("Model::Foo", deserialized.Element);
        Assert.Single(deserialized.Entries);
        Assert.Equal("Model::Bar", deserialized.Entries[0].QualifiedName);
    }

    /// <summary>
    ///     RenderJson sorts entries identically to RenderMarkdown, so both formats report the
    ///     same qualified-name order.
    /// </summary>
    [Fact]
    public void RenderJson_UnorderedEntries_SortsByQualifiedNameOrdinal()
    {
        var result = new QueryResult
        {
            Verb = "list",
            Entries =
            [
                new QueryResultEntry { QualifiedName = "Model::Zebra" },
                new QueryResultEntry { QualifiedName = "Model::Apple" }
            ]
        };

        var json = QueryResultRenderer.RenderJson(result);
        var deserialized = JsonSerializer.Deserialize(json, QueryResultSerializerContext.Default.QueryResult);

        Assert.NotNull(deserialized);
        Assert.Equal("Model::Apple", deserialized!.Entries[0].QualifiedName);
        Assert.Equal("Model::Zebra", deserialized.Entries[1].QualifiedName);
    }

    /// <summary>
    ///     End-to-end: '--format markdown' and '--format json' for the same 'uses' query report
    ///     the same qualified names, in the same order.
    /// </summary>
    [Fact]
    public async Task Query_MarkdownAndJsonFormats_AgreeOnEntryContentAndOrder()
    {
        const string sysml = """
            package Model {
                part def Alpha;
                part def Zeta specializes Alpha;
                part def Beta specializes Alpha;
            }
            """;

        var (markdown, markdownExit) = await QueryTestFixtures.RunQueryAsync(
            sysml, "used-by", "--element", "Model::Alpha", "--format", "markdown");
        var (json, jsonExit) = await QueryTestFixtures.RunQueryAsync(
            sysml, "used-by", "--element", "Model::Alpha", "--format", "json");

        Assert.Equal(0, markdownExit);
        Assert.Equal(0, jsonExit);

        // The JSON document is the last chunk written; extract it (from the first '{') and parse
        var deserialized = JsonSerializer.Deserialize(
            json[json.IndexOf('{')..], QueryResultSerializerContext.Default.QueryResult);

        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized!.Entries.Count);
        Assert.Equal("Model::Beta", deserialized.Entries[0].QualifiedName);
        Assert.Equal("Model::Zeta", deserialized.Entries[1].QualifiedName);

        // Markdown reports the same two qualified names, Beta appearing before Zeta
        var betaIndex = markdown.IndexOf("Model::Beta", StringComparison.Ordinal);
        var zetaIndex = markdown.IndexOf("Model::Zeta", StringComparison.Ordinal);
        Assert.True(betaIndex >= 0 && zetaIndex >= 0 && betaIndex < zetaIndex);
    }
}
