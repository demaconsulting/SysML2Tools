// <copyright file="QueryRenderingTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

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
    ///     RenderMarkdown for the 'dependencies' verb renders its entries as prose bullets
    ///     (never a table), sorted by qualified name (ordinal) within each direction
    ///     regardless of input order, and shortens every name (subject + entries) by the
    ///     longest shared leading "::"-segment prefix across the whole pool.
    /// </summary>
    [Fact]
    public void RenderMarkdown_DependenciesVerb_RendersBulletProseNotTable()
    {
        var result = new QueryResult
        {
            Verb = "dependencies",
            Element = "Model::Car",
            Entries =
            [
                new QueryResultEntry { QualifiedName = "Model::Zebra", Kind = "supertype", Direction = QueryEntryDirection.Outgoing },
                new QueryResultEntry { QualifiedName = "Model::Apple", Kind = "supertype", Direction = QueryEntryDirection.Outgoing },
                new QueryResultEntry { QualifiedName = "Model::Truck", Kind = "supertype", Direction = QueryEntryDirection.Incoming }
            ]
        };

        var lines = QueryResultRenderer.RenderMarkdown(result);

        Assert.DoesNotContain(lines, l => l.Contains("| Qualified Name | Kind | Detail |"));
        Assert.Contains("Car references the following elements:", lines);
        Assert.Contains("- Depends on **Apple** (supertype)", lines);
        Assert.Contains("- Depends on **Zebra** (supertype)", lines);
        Assert.Contains("The following elements reference Car:", lines);
        Assert.Contains("- Used by **Truck** (supertype)", lines);

        // The outgoing bullets must appear in ordinal order (Apple before Zebra)
        var lineList = lines.ToList();
        var appleIndex = lineList.IndexOf("- Depends on **Apple** (supertype)");
        var zebraIndex = lineList.IndexOf("- Depends on **Zebra** (supertype)");
        Assert.True(appleIndex >= 0 && zebraIndex >= 0 && appleIndex < zebraIndex);
    }

    /// <summary>
    ///     RenderMarkdown for the 'dependencies' verb leaves names fully-qualified when the
    ///     subject and its entries share no common leading segment.
    /// </summary>
    [Fact]
    public void RenderMarkdown_DependenciesVerb_NoCommonPrefix_LeavesNamesFullyQualified()
    {
        var result = new QueryResult
        {
            Verb = "dependencies",
            Element = "PkgA::Car",
            Entries =
            [
                new QueryResultEntry { QualifiedName = "PkgB::Vehicle", Kind = "supertype", Direction = QueryEntryDirection.Outgoing }
            ]
        };

        var lines = QueryResultRenderer.RenderMarkdown(result);

        Assert.Contains("PkgA::Car references the following elements:", lines);
        Assert.Contains("- Depends on **PkgB::Vehicle** (supertype)", lines);
    }

    /// <summary>
    ///     RenderMarkdown for the 'dependencies' verb reports both "has no outgoing
    ///     references"/"No elements reference" prose lines (and no bullets or "_No entries._"
    ///     fallback) when neither direction has any entries.
    /// </summary>
    [Fact]
    public void RenderMarkdown_DependenciesVerb_EmptyOutgoingAndIncoming_ReportsBothProseLines()
    {
        var result = new QueryResult { Verb = "dependencies", Element = "Model::Car" };

        var lines = QueryResultRenderer.RenderMarkdown(result);

        Assert.Contains("Model::Car has no outgoing references.", lines);
        Assert.Contains("No elements reference Model::Car.", lines);
        Assert.DoesNotContain(lines, l => l.Contains("Depends on"));
        Assert.DoesNotContain(lines, l => l.Contains("Used by"));
        Assert.DoesNotContain(lines, l => l.Contains("_No entries._"));
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
    ///     RenderMarkdown with default arguments (no depth/heading override) produces the
    ///     same single '#' heading as before this feature was added.
    /// </summary>
    [Fact]
    public void RenderMarkdown_DefaultArguments_ProducesUnchangedTopLevelHeading()
    {
        var result = new QueryResult { Verb = "uses", Element = "Model::Foo" };

        var lines = QueryResultRenderer.RenderMarkdown(result);

        Assert.Equal("# query uses: Model::Foo", lines[0]);
    }

    /// <summary>
    ///     RenderMarkdown with a custom depth uses that many '#' characters for the
    ///     heading, keeping the auto-generated heading text.
    /// </summary>
    [Fact]
    public void RenderMarkdown_CustomDepth_UsesThatManyHeadingHashes()
    {
        var result = new QueryResult { Verb = "uses", Element = "Model::Foo" };

        var lines = QueryResultRenderer.RenderMarkdown(result, depth: 3);

        Assert.Equal("### query uses: Model::Foo", lines[0]);
    }

    /// <summary>
    ///     RenderMarkdown with a custom heading replaces the auto-generated heading text
    ///     entirely, with no merging of verb/element information.
    /// </summary>
    [Fact]
    public void RenderMarkdown_CustomHeading_ReplacesAutoGeneratedText()
    {
        var result = new QueryResult { Verb = "uses", Element = "Model::Foo" };

        var lines = QueryResultRenderer.RenderMarkdown(result, heading: "Custom Heading");

        Assert.Equal("# Custom Heading", lines[0]);
    }

    /// <summary>
    ///     RenderMarkdown with both a custom depth and heading combines both
    ///     overrides: the requested number of '#' characters and the custom heading text.
    /// </summary>
    [Fact]
    public void RenderMarkdown_CustomDepthAndHeading_CombinesBothOverrides()
    {
        var result = new QueryResult { Verb = "uses", Element = "Model::Foo" };

        var lines = QueryResultRenderer.RenderMarkdown(result, depth: 5, heading: "Custom Heading");

        Assert.Equal("##### Custom Heading", lines[0]);
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
    ///     RenderJson includes a populated 'Direction' field for each entry of a 'dependencies'
    ///     result, round-tripping 'Outgoing'/'Incoming' correctly.
    /// </summary>
    [Fact]
    public void RenderJson_DependenciesVerb_IncludesDirectionField()
    {
        var result = new QueryResult
        {
            Verb = "dependencies",
            Element = "Model::Car",
            Entries =
            [
                new QueryResultEntry { QualifiedName = "Model::Vehicle", Kind = "supertype", Direction = QueryEntryDirection.Outgoing },
                new QueryResultEntry { QualifiedName = "Model::Truck", Kind = "supertype", Direction = QueryEntryDirection.Incoming }
            ]
        };

        var json = QueryResultRenderer.RenderJson(result);
        var deserialized = JsonSerializer.Deserialize(json, QueryResultSerializerContext.Default.QueryResult);

        Assert.NotNull(deserialized);
        Assert.Contains("\"Direction\"", json);
        Assert.Equal(QueryEntryDirection.Incoming, deserialized!.Entries[0].Direction);
        Assert.Equal(QueryEntryDirection.Outgoing, deserialized.Entries[1].Direction);
    }

    /// <summary>
    ///     Regression test: 'dependencies' JSON output remains fully-qualified (unaffected by
    ///     the Markdown-only <see cref="DemaConsulting.SysML2Tools.Utilities.QualifiedNameShortener"/> shortening), even
    ///     when the subject and entries share a common leading segment that Markdown would
    ///     shorten.
    /// </summary>
    [Fact]
    public void RenderJson_DependenciesVerb_NamesRemainFullyQualified()
    {
        var result = new QueryResult
        {
            Verb = "dependencies",
            Element = "Model::Car",
            Entries =
            [
                new QueryResultEntry { QualifiedName = "Model::Vehicle", Kind = "supertype", Direction = QueryEntryDirection.Outgoing },
                new QueryResultEntry { QualifiedName = "Model::Truck", Kind = "supertype", Direction = QueryEntryDirection.Incoming }
            ]
        };

        // Markdown shortens the shared "Model" prefix away...
        var markdown = QueryResultRenderer.RenderMarkdown(result);
        Assert.Contains("Car references the following elements:", markdown);
        Assert.DoesNotContain(markdown, l => l.Contains("Model::Vehicle"));

        // ...but JSON output for the very same result stays fully-qualified throughout
        var json = QueryResultRenderer.RenderJson(result);
        var deserialized = JsonSerializer.Deserialize(json, QueryResultSerializerContext.Default.QueryResult);

        Assert.NotNull(deserialized);
        Assert.Equal("Model::Car", deserialized!.Element);
        Assert.Contains(deserialized.Entries, e => e.QualifiedName == "Model::Vehicle");
        Assert.Contains(deserialized.Entries, e => e.QualifiedName == "Model::Truck");
        Assert.Contains("Model::Vehicle", json);
        Assert.Contains("Model::Truck", json);
        Assert.Contains("Model::Car", json);
    }

    /// <summary>
    ///     Regression test: for every verb other than 'dependencies', the 'Direction' field is
    ///     omitted from JSON output entirely (not serialized as 'null'), so adding it does not
    ///     change any other verb's JSON output shape.
    /// </summary>
    [Fact]
    public void RenderJson_NonDependenciesVerb_DirectionFieldOmittedFromOutput()
    {
        var result = new QueryResult
        {
            Verb = "uses",
            Element = "Model::Foo",
            Entries = [new QueryResultEntry { QualifiedName = "Model::Bar", Kind = "supertype" }]
        };

        var json = QueryResultRenderer.RenderJson(result);

        Assert.DoesNotContain("Direction", json);
    }
}
