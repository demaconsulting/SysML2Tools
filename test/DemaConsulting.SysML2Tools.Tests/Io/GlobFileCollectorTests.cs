// <copyright file="GlobFileCollectorTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Io;

namespace DemaConsulting.SysML2Tools.Tests.Io;

/// <summary>
///     Tests for <see cref="GlobFileCollector"/> file glob pattern resolution.
/// </summary>
public sealed class GlobFileCollectorTests : IDisposable
{
    private readonly string _root;

    /// <summary>
    ///     Creates a fresh temporary root directory for this test instance.
    /// </summary>
    public GlobFileCollectorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"glob_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    /// <summary>
    ///     Deletes the temporary root directory created for this test instance.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string WriteFile(string relativePath, string content = "package P {}")
    {
        var fullPath = Path.Combine(_root, relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    /// <summary>
    ///     A literal (non-glob) file path resolves directly without directory traversal.
    /// </summary>
    [Fact]
    public void GlobFileCollector_Collect_LiteralPath_ReturnsSingleFile()
    {
        // Arrange
        var file = WriteFile("model.sysml");

        // Act
        var result = GlobFileCollector.Collect([file], [".sysml", ".kerml"], _root);

        // Assert
        Assert.Single(result);
        Assert.Equal(Path.GetFullPath(file), result[0]);
    }

    /// <summary>
    ///     A relative glob pattern (no directory prefix) resolves against the supplied working
    ///     directory, confirming <see cref="GlobFileCollector.Collect"/> supports relative
    ///     patterns and not just absolute ones.
    /// </summary>
    [Fact]
    public void GlobFileCollector_Collect_RelativeGlob_ResolvesAgainstWorkingDirectory()
    {
        // Arrange
        WriteFile("a.sysml");
        WriteFile("b.sysml");
        WriteFile("c.txt");

        // Act: a bare relative pattern with no directory prefix, resolved against _root
        var result = GlobFileCollector.Collect(["*.sysml"], [".sysml", ".kerml"], _root);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, f => Assert.EndsWith(".sysml", f, StringComparison.Ordinal));
    }

    /// <summary>
    ///     A relative literal path (no glob metacharacters, no directory prefix) resolves
    ///     against the supplied working directory — unlike an absolute literal path, this does
    ///     not take the fast literal-existence-check path; it still resolves correctly via
    ///     <c>Matcher</c>.
    /// </summary>
    [Fact]
    public void GlobFileCollector_Collect_RelativeLiteralPath_ResolvesAgainstWorkingDirectory()
    {
        // Arrange
        var file = WriteFile("model.sysml");

        // Act: a bare relative literal filename, resolved against _root
        var result = GlobFileCollector.Collect(["model.sysml"], [".sysml", ".kerml"], _root);

        // Assert
        Assert.Single(result);
        Assert.Equal(Path.GetFullPath(file), result[0]);
    }

    /// <summary>
    ///     A basic single-segment glob pattern resolves every matching file in the directory.
    /// </summary>
    [Fact]
    public void GlobFileCollector_Collect_BasicGlob_ReturnsMatchingFiles()
    {
        // Arrange
        WriteFile("a.sysml");
        WriteFile("b.sysml");
        WriteFile("c.txt");

        // Act
        var result = GlobFileCollector.Collect(
            [Path.Combine(_root, "*.sysml")], [".sysml", ".kerml"], _root);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, f => Assert.EndsWith(".sysml", f, StringComparison.Ordinal));
    }

    /// <summary>
    ///     A recursive '**' glob pattern resolves matching files at every nesting depth.
    /// </summary>
    [Fact]
    public void GlobFileCollector_Collect_RecursiveGlob_ReturnsNestedFiles()
    {
        // Arrange
        WriteFile("top.sysml");
        WriteFile(Path.Combine("nested", "child.sysml"));
        WriteFile(Path.Combine("nested", "deeper", "grandchild.sysml"));

        // Act
        var result = GlobFileCollector.Collect(
            [Path.Combine(_root, "**", "*.sysml")], [".sysml", ".kerml"], _root);

        // Assert
        Assert.Equal(3, result.Count);
    }

    /// <summary>
    ///     A bare '*' pattern is filtered to the caller-supplied extensions.
    /// </summary>
    [Fact]
    public void GlobFileCollector_Collect_BareStarPattern_FiltersToSuppliedExtensions()
    {
        // Arrange
        WriteFile("a.sysml");
        WriteFile("b.kerml");
        WriteFile("c.txt");

        // Act
        var result = GlobFileCollector.Collect(
            [Path.Combine(_root, "*")], [".sysml", ".kerml"], _root);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, f => f.EndsWith(".txt", StringComparison.Ordinal));
    }

    /// <summary>
    ///     A pattern with an explicit extension is not filtered further by the extension set.
    /// </summary>
    [Fact]
    public void GlobFileCollector_Collect_ExplicitExtensionPattern_ReturnsAllMatches()
    {
        // Arrange
        WriteFile("a.sysml");
        WriteFile("b.sysml");

        // Act: supply an extension set that does NOT include .sysml — explicit-extension
        // patterns must still resolve all matches, ignoring the extension set entirely.
        var result = GlobFileCollector.Collect(
            [Path.Combine(_root, "*.sysml")], [".kerml"], _root);

        // Assert
        Assert.Equal(2, result.Count);
    }

    /// <summary>
    ///     A '!'-prefixed exclusion pattern removes a previously-included file.
    /// </summary>
    [Fact]
    public void GlobFileCollector_Collect_ExclusionPattern_RemovesPreviouslyIncludedFile()
    {
        // Arrange
        WriteFile("a.sysml");
        var excluded = WriteFile("b.sysml");

        // Act
        var result = GlobFileCollector.Collect(
            [Path.Combine(_root, "*.sysml"), $"!{excluded}"], [".sysml", ".kerml"], _root);

        // Assert
        Assert.Single(result);
        Assert.DoesNotContain(Path.GetFullPath(excluded), result);
    }

    /// <summary>
    ///     A later inclusion pattern re-adds a file previously removed by an earlier exclusion
    ///     pattern, confirming patterns are processed strictly in supplied order — accumulating
    ///     into a single mutable result set rather than each pattern being evaluated against an
    ///     independent snapshot.
    /// </summary>
    [Fact]
    public void GlobFileCollector_Collect_LaterInclusionAfterExclusion_ReAddsFile()
    {
        // Arrange
        var file = WriteFile("foo.sysml");

        // Act: include, then exclude, then include again — the final inclusion should win
        var result = GlobFileCollector.Collect(
            [Path.Combine(_root, "*.sysml"), $"!{file}", file], [".sysml", ".kerml"], _root);

        // Assert: the file is present in the final result, confirming re-add semantics
        Assert.Single(result);
        Assert.Equal(Path.GetFullPath(file), result[0]);
    }

    /// <summary>
    ///     Two patterns referring to the same physical file via different casing collapse
    ///     to a single entry, confirming on-disk casing normalization before ordinal dedup.
    /// </summary>
    /// <remarks>
    ///     Dynamically skipped (via <c>Assert.Skip</c>, not a silent early-return) on
    ///     case-sensitive filesystems (e.g. Linux CI) where a differently-cased literal path
    ///     genuinely does not refer to the same on-disk file, so there is nothing to normalize.
    ///     A dynamic skip surfaces as "Skipped" in test reporting rather than a false pass with
    ///     no assertions executed.
    /// </remarks>
    [Fact]
    public void GlobFileCollector_Collect_CaseInsensitiveFilesystem_DeduplicatesSameFile()
    {
        // Arrange
        var file = WriteFile("Model.sysml");
        var upperCased = Path.Combine(_root, "MODEL.SYSML");

        // Dynamically skip on case-sensitive filesystems where the differently-cased literal
        // path would not actually resolve to the same on-disk file — this requirement only
        // applies to case-insensitive filesystems (Windows, macOS).
        if (!File.Exists(upperCased))
        {
            Assert.Skip(
                "Filesystem is case-sensitive; casing normalization is not applicable here.");
        }

        // Act: one pattern in original casing, one in a different casing
        var result = GlobFileCollector.Collect(
            [file, upperCased], [".sysml", ".kerml"], _root);

        // Assert: both resolve to the same on-disk entry, collapsed to one result
        Assert.Single(result);
    }

    /// <summary>
    ///     A pattern whose root directory does not exist resolves to an empty result,
    ///     without throwing.
    /// </summary>
    [Fact]
    public void GlobFileCollector_Collect_NonExistentDirectory_SilentlySkipped()
    {
        // Arrange
        var missingDir = Path.Combine(_root, "does-not-exist");

        // Act
        var result = GlobFileCollector.Collect(
            [Path.Combine(missingDir, "*.sysml")], [".sysml", ".kerml"], _root);

        // Assert
        Assert.Empty(result);
    }

    /// <summary>
    ///     A literal path that does not exist on disk resolves to an empty result,
    ///     without throwing.
    /// </summary>
    [Fact]
    public void GlobFileCollector_Collect_NonExistentLiteralFile_SilentlySkipped()
    {
        // Arrange
        var missingFile = Path.Combine(_root, "missing.sysml");

        // Act
        var result = GlobFileCollector.Collect([missingFile], [".sysml", ".kerml"], _root);

        // Assert
        Assert.Empty(result);
    }

    /// <summary>
    ///     Multiple overlapping inclusion patterns resolve to a stable, ordinally sorted,
    ///     deduplicated list of file paths.
    /// </summary>
    [Fact]
    public void GlobFileCollector_Collect_MultiplePatterns_ReturnsStableSortedOrder()
    {
        // Arrange
        var a = WriteFile("a.sysml");
        var b = WriteFile("b.sysml");

        // Act: two overlapping patterns that both match both files
        var result = GlobFileCollector.Collect(
            [Path.Combine(_root, "*.sysml"), a, b], [".sysml", ".kerml"], _root);

        // Assert: deduplicated to two entries, sorted ordinally
        Assert.Equal(2, result.Count);
        var sorted = result.OrderBy(f => f, StringComparer.Ordinal).ToList();
        Assert.Equal(sorted, result);
    }

    /// <summary>
    ///     An empty pattern list resolves to an empty result.
    /// </summary>
    [Fact]
    public void GlobFileCollector_Collect_EmptyPatternList_ReturnsEmptyResult()
    {
        // Act
        var result = GlobFileCollector.Collect([], [".sysml", ".kerml"], _root);

        // Assert
        Assert.Empty(result);
    }
}
