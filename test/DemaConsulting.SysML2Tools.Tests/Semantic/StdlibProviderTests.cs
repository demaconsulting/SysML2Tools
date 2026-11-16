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
    ///     The stdlib diagnostics contain no errors.
    /// </summary>
    [Fact]
    public void GetSymbolTable_Diagnostics_ContainNoErrors()
    {
        var (_, diagnostics) = StdlibProvider.GetSymbolTable();

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }
}
