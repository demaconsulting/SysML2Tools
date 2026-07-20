// <copyright file="QueryResultExporterTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Query;

namespace DemaConsulting.SysML2Tools.Tests.Query;

/// <summary>
///     Tests for <see cref="QueryResultExporter"/>, verifying that its file output matches
///     <see cref="QueryResultRenderer"/>'s direct rendering output exactly.
/// </summary>
public sealed class QueryResultExporterTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"query_export_{Guid.NewGuid():N}.tmp");

    /// <summary>
    ///     Deletes the temporary file created for this test instance, if it exists.
    /// </summary>
    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    private static QueryResult SampleResult()
    {
        return new QueryResult
        {
            Verb = "uses",
            Element = "Model::Foo",
            Summary = ["1 outgoing reference(s)."],
            Entries = [new QueryResultEntry { QualifiedName = "Model::Bar", Kind = "supertype" }]
        };
    }

    /// <summary>
    ///     WriteMarkdown writes the exact same content as joining
    ///     <see cref="QueryResultRenderer.RenderMarkdown"/>'s lines with "\n".
    /// </summary>
    [Fact]
    public void WriteMarkdown_HappyPath_MatchesRendererOutput()
    {
        // Arrange
        var result = SampleResult();
        var expected = string.Join("\n", QueryResultRenderer.RenderMarkdown(result));

        // Act
        QueryResultExporter.WriteMarkdown(result, _path);

        // Assert
        Assert.Equal(expected, File.ReadAllText(_path));
    }

    /// <summary>
    ///     WriteMarkdown honors the depth and heading overrides, matching
    ///     <see cref="QueryResultRenderer.RenderMarkdown"/>'s behavior for the same arguments.
    /// </summary>
    [Fact]
    public void WriteMarkdown_WithDepthAndHeading_MatchesRendererOutput()
    {
        // Arrange
        var result = SampleResult();
        var expected = string.Join("\n", QueryResultRenderer.RenderMarkdown(result, depth: 3, heading: "Custom"));

        // Act
        QueryResultExporter.WriteMarkdown(result, _path, depth: 3, heading: "Custom");

        // Assert
        Assert.Equal(expected, File.ReadAllText(_path));
    }

    /// <summary>
    ///     WriteMarkdownAsync writes the exact same content as its synchronous counterpart.
    /// </summary>
    [Fact]
    public async Task WriteMarkdownAsync_HappyPath_MatchesRendererOutput()
    {
        // Arrange
        var result = SampleResult();
        var expected = string.Join("\n", QueryResultRenderer.RenderMarkdown(result));

        // Act
        await QueryResultExporter.WriteMarkdownAsync(result, _path, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, await File.ReadAllTextAsync(_path, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     WriteJson writes the exact same content as <see cref="QueryResultRenderer.RenderJson"/>.
    /// </summary>
    [Fact]
    public void WriteJson_HappyPath_MatchesRendererOutput()
    {
        // Arrange
        var result = SampleResult();
        var expected = QueryResultRenderer.RenderJson(result);

        // Act
        QueryResultExporter.WriteJson(result, _path);

        // Assert
        Assert.Equal(expected, File.ReadAllText(_path));
    }

    /// <summary>
    ///     WriteJsonAsync writes the exact same content as its synchronous counterpart.
    /// </summary>
    [Fact]
    public async Task WriteJsonAsync_HappyPath_MatchesRendererOutput()
    {
        // Arrange
        var result = SampleResult();
        var expected = QueryResultRenderer.RenderJson(result);

        // Act
        await QueryResultExporter.WriteJsonAsync(result, _path, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, await File.ReadAllTextAsync(_path, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     WriteMarkdown propagates <see cref="DirectoryNotFoundException"/> (an
    ///     <see cref="IOException"/> subtype) uncaught when the parent directory does not exist,
    ///     rather than creating it or swallowing the failure.
    /// </summary>
    [Fact]
    public void WriteMarkdown_MissingParentDirectory_PropagatesIoException()
    {
        // Arrange
        var missingDirPath = Path.Combine(_path + "_missing_dir", "output.md");

        // Act & Assert
        Assert.Throws<DirectoryNotFoundException>(() => QueryResultExporter.WriteMarkdown(SampleResult(), missingDirPath));
    }

    /// <summary>
    ///     WriteJson propagates <see cref="DirectoryNotFoundException"/> (an
    ///     <see cref="IOException"/> subtype) uncaught when the parent directory does not exist.
    /// </summary>
    [Fact]
    public void WriteJson_MissingParentDirectory_PropagatesIoException()
    {
        // Arrange
        var missingDirPath = Path.Combine(_path + "_missing_dir", "output.json");

        // Act & Assert
        Assert.Throws<DirectoryNotFoundException>(() => QueryResultExporter.WriteJson(SampleResult(), missingDirPath));
    }
}
