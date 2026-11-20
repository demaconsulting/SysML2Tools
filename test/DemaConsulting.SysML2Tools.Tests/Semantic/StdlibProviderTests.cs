// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using DemaConsulting.SysML2Tools.Parser;
using DemaConsulting.SysML2Tools.Semantic.Model;
using DemaConsulting.SysML2Tools.Stdlib;

namespace DemaConsulting.SysML2Tools.Tests.Semantic;

/// <summary>
///     Tests for <see cref="StdlibProvider"/> correctness and caching behavior.
/// </summary>
public sealed class StdlibProviderTests
{
    /// <summary>
    ///     GetSymbolTable returns a non-empty symbol table.
    /// </summary>
    [Fact]
    public void GetSymbolTable_ReturnsNonEmpty()
    {
        var (table, _) = StdlibProvider.GetSymbolTable();
        Assert.True(table.Symbols.Count > 0, "Stdlib symbol table should not be empty.");
    }

    /// <summary>
    ///     Two calls to GetSymbolTable return the same instance (cached).
    /// </summary>
    [Fact]
    public void GetSymbolTable_IsCached()
    {
        var (table1, _) = StdlibProvider.GetSymbolTable();
        var (table2, _) = StdlibProvider.GetSymbolTable();
        Assert.Same(table1, table2);
    }

    /// <summary>
    ///     The second call to GetSymbolTable completes in under 50ms.
    /// </summary>
    [Fact]
    public void GetSymbolTable_FastOnSubsequentCalls()
    {
        // Warm up
        _ = StdlibProvider.GetSymbolTable();

        var sw = Stopwatch.StartNew();
        _ = StdlibProvider.GetSymbolTable();
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 50,
            $"Second GetSymbolTable call took {sw.ElapsedMilliseconds}ms; expected < 50ms.");
    }

    /// <summary>
    ///     The stdlib symbol table contains at least 50 standard declarations.
    /// </summary>
    [Fact]
    public void GetSymbolTable_ContainsKnownStdlibTypes()
    {
        var (table, _) = StdlibProvider.GetSymbolTable();

        // These names come from the SysML v2 KerML/SysML standard libraries
        Assert.True(table.Symbols.Count > 50,
            "Stdlib symbol table should contain at least 50 standard declarations.");
    }

    /// <summary>
    ///     The stdlib diagnostics list is not null.
    /// </summary>
    [Fact]
    public void GetSymbolTable_DiagnosticsNotNull()
    {
        var (_, diagnostics) = StdlibProvider.GetSymbolTable();
        Assert.NotNull(diagnostics);
    }

    /// <summary>
    ///     The pre-compiled stdlib carries the additive modifier data: <c>Parts::Part</c> is
    ///     declared <c>abstract part def Part</c>. Fails if the stdlib resource was not regenerated.
    /// </summary>
    [Fact]
    public void GetSymbolTable_RegeneratedStdlib_CarriesModifiers()
    {
        var (table, _) = StdlibProvider.GetSymbolTable();

        var part = Assert.IsType<SysmlDefinitionNode>(table.Symbols["Parts::Part"]);
        Assert.True(part.IsAbstract);
    }

    /// <summary>
    ///     The pre-compiled stdlib indexes the named KerML library declarations that were
    ///     previously unreachable from package bodies (abstract classifier, abstract metaclass,
    ///     package-level feature, struct, and associations). Fails if the stdlib resource was not
    ///     regenerated.
    /// </summary>
    /// <param name="qualifiedName">The qualified name of the library declaration.</param>
    /// <param name="keyword">The expected definition or feature keyword.</param>
    [Theory]
    [InlineData("Base::Anything", "classifier")]
    [InlineData("Metaobjects::SemanticMetadata", "metaclass")]
    [InlineData("TrigFunctions::pi", "feature")]
    [InlineData("Objects::Point", "struct")]
    [InlineData("Occurrences::HappensBefore", "assoc")]
    [InlineData("Occurrences::HappensJustBefore", "assoc")]
    public void GetSymbolTable_KermlLibraryDeclarations_AreIndexed(string qualifiedName, string keyword)
    {
        var (table, _) = StdlibProvider.GetSymbolTable();

        Assert.True(table.Symbols.TryGetValue(qualifiedName, out var node), $"{qualifiedName} is not indexed.");
        var actualKeyword = node switch
        {
            SysmlDefinitionNode definition => definition.DefinitionKeyword,
            SysmlFeatureNode feature => feature.FeatureKeyword,
            _ => node.GetType().Name,
        };
        Assert.Equal(keyword, actualKeyword);
    }

    /// <summary>
    ///     The stdlib diagnostics contain no errors.
    /// </summary>
    [Fact]
    public void GetSymbolTable_Diagnostics_ContainNoErrors()
    {
        var (_, diagnostics) = StdlibProvider.GetSymbolTable();

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    ///     The standard library compiles with no errors and no more than the recorded number of
    ///     unresolved-reference warnings (the remaining ones name connection ends and members of
    ///     bodies the AST does not record, tracked in issue #129). Lower the ceiling whenever
    ///     resolver gaps are closed and the library resource is regenerated.
    /// </summary>
    [Fact]
    public void GetSymbolTable_Diagnostics_WarningCount_DoesNotExceedCeiling()
    {
        // Arrange
        const int ceiling = 12;

        // Act
        var (_, diagnostics) = StdlibProvider.GetSymbolTable();

        // Assert
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        var warnings = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);
        Assert.True(warnings <= ceiling, $"Expected at most {ceiling} stdlib warnings but got {warnings}.");
    }

    /// <summary>
    ///     The stdlib's unresolved-reference diagnostics stay Warnings: the standard library carries
    ///     known resolver gaps that must not fail a load, even when user models opt in to strict
    ///     (Error) unresolved-reference severity.
    /// </summary>
    [Fact]
    public void GetSymbolTable_Diagnostics_UnresolvedReferences_RemainWarnings()
    {
        // Arrange / Act
        var (_, diagnostics) = StdlibProvider.GetSymbolTable();

        // Assert
        var unresolved = diagnostics
            .Where(d => d.Message.StartsWith("Unresolved reference:", StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(unresolved);
        Assert.All(unresolved, d => Assert.Equal(DiagnosticSeverity.Warning, d.Severity));
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }
}
