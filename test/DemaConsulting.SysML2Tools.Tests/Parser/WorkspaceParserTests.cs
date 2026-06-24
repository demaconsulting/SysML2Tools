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

    /// <summary>
    ///     The OMG stdlib (94 embedded files) should load and parse without any errors.
    /// </summary>
    [Fact]
    public void Parse_StdlibOnly_NoErrors()
    {
        var result = WorkspaceParser.Parse([]);
        Assert.False(result.HasErrors,
            $"Stdlib parse errors:{Environment.NewLine}" +
            string.Join(Environment.NewLine, result.Diagnostics.Select(d => $"  {d.FilePath}({d.Line},{d.Column}): {d.Message}")));
    }

    /// <summary>
    ///     Stdlib is always loaded — even when no user files are passed.
    ///     Phase 1 loads the 58 SysML stdlib files; KerML files are embedded but parsed in Phase 2.
    /// </summary>
    [Fact]
    public void Parse_FilesCount_IncludesStdlib()
    {
        var result = WorkspaceParser.Parse([]);
        Assert.True(result.Files.Count >= 58,
            $"Expected at least 58 stdlib files, got {result.Files.Count}");
    }
}
