// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using DemaConsulting.SysML2Tools.Parser;

namespace DemaConsulting.SysML2Tools.Tests.Parser;

/// <summary>
///     Tests for <see cref="WorkspaceParser"/>.
/// </summary>
public sealed class WorkspaceParserTests
{
    /// <summary>
    ///     An empty SysML file (just EOF) should produce no diagnostics.
    /// </summary>
    [Fact]
    public void ParseSource_EmptyFile_NoErrors()
    {
        var diagnostics = WorkspaceParser.ParseSource("<test>", string.Empty);
        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     A minimal valid package declaration should produce no diagnostics.
    /// </summary>
    [Fact]
    public void ParseSource_MinimalPackage_NoErrors()
    {
        const string source = "package MyPackage {}";
        var diagnostics = WorkspaceParser.ParseSource("<test>", source);
        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     A minimal valid part definition should produce no diagnostics.
    /// </summary>
    [Fact]
    public void ParseSource_PartDef_NoErrors()
    {
        const string source = """
            package Example {
                part def Wheel {
                    attribute radius : Real;
                }
            }
            """;
        var diagnostics = WorkspaceParser.ParseSource("<test>", source);
        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     Garbled text that is not valid SysML should produce at least one error.
    /// </summary>
    [Fact]
    public void ParseSource_InvalidSyntax_ReportsError()
    {
        const string source = "@@@ NOT VALID SYSML @@@";
        var diagnostics = WorkspaceParser.ParseSource("<test>", source);
        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    ///     Diagnostic file path is propagated from the caller.
    /// </summary>
    [Fact]
    public void ParseSource_ErrorPath_MatchesSuppliedPath()
    {
        const string path = "my-model.sysml";
        var diagnostics = WorkspaceParser.ParseSource(path, "@@@");
        Assert.All(diagnostics, d => Assert.Equal(path, d.FilePath));
    }
}
