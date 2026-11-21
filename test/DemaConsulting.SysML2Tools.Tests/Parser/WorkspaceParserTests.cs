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
    ///     A classification test is not a valid bare operand of a unary operator (KerML 8.2.5.8.1):
    ///     <c>filter not @X;</c>, <c>... and not @X</c>, and <c>-@X</c> are reported as syntax errors
    ///     on the <c>@</c> token that follows the operator.
    /// </summary>
    [Theory]
    [InlineData("filter not @SysML::Documentation;", 3, 20)]
    [InlineData("filter @SysML::PartDefinition and not @SysML::Documentation;", 3, 47)]
    [InlineData("filter -@SysML::Documentation;", 3, 17)]
    [InlineData("filter +@SysML::Documentation;", 3, 17)]
    [InlineData("filter ~@SysML::Documentation;", 3, 17)]
    public void ParseSource_UnaryOperatorWithBareClassificationTest_ReportsSyntaxErrorAtAtSign(
        string statement, int line, int column)
    {
        var source = $"package P {{\n    view V {{\n        {statement}\n    }}\n}}\n";

        var diagnostics = WorkspaceParser.ParseSource("<test>", source);

        var error = Assert.Single(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Equal((line, column), (error.Line, error.Column));
        Assert.Contains("parenthesize", error.Message);
    }

    /// <summary>The expose-filter bracket form <c>[not @X]</c> is rejected the same way.</summary>
    [Fact]
    public void ParseSource_ExposeFilterWithNotBareClassificationTest_ReportsSyntaxError()
    {
        const string source = "package P {\n    view V {\n        expose P::**[not @SysML::Documentation];\n    }\n}\n";

        var diagnostics = WorkspaceParser.ParseSource("<test>", source);

        // ANTLR's recovery after the rejected operand reports a follow-on error at the closing
        // bracket, so only the first (primary) error is pinned to the `@` token.
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.NotEmpty(errors);
        Assert.Equal((3, 26), (errors[0].Line, errors[0].Column));
        Assert.Contains("parenthesize", errors[0].Message);
    }

    /// <summary>Parenthesized classification tests (and plain classification-test operands) stay valid.</summary>
    [Theory]
    [InlineData("filter not (@SysML::Documentation);")]
    [InlineData("filter @SysML::PartDefinition and not (@SysML::Documentation);")]
    [InlineData("filter @SysML::PartDefinition and @SysML::PartUsage;")]
    [InlineData("expose P::**[not (@SysML::Documentation)];")]
    public void ParseSource_UnaryOperatorWithParenthesizedClassificationTest_NoErrors(string statement)
    {
        var source = $"package P {{\n    view V {{\n        {statement}\n    }}\n}}\n";

        var diagnostics = WorkspaceParser.ParseSource("<test>", source);

        Assert.True(
            diagnostics.Count == 0,
            string.Join("\n", diagnostics.Select(d => $"({d.Line},{d.Column}) {d.Message}")));
    }
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
    ///     Every KerML namespace member production (KerML 1.0 <c>NamespaceMember</c>:
    ///     <c>NonFeatureMember</c> | <c>NamespaceFeatureMember</c>) is accepted directly in a
    ///     package body — including the declaration-less forms of <c>step</c>, <c>expr</c>,
    ///     <c>bool</c>, and <c>inv</c> — with no diagnostics.
    /// </summary>
    /// <param name="member">The KerML member declaration placed in a package body.</param>
    [Theory]
    [InlineData("abstract classifier Anything;")]
    [InlineData("classifier Thing :> Anything { feature inner : Anything; }")]
    [InlineData("abstract metaclass SemanticMetadata :> Anything;")]
    [InlineData("behavior Beh;")]
    [InlineData("interaction Inter;")]
    [InlineData("type T :> Anything;")]
    [InlineData("multiplicity exactlyOne [1..1];")]
    [InlineData("namespace Inner { feature f : Anything; }")]
    [InlineData("subclassifier Thing :> Anything;")]
    [InlineData("specialization s1 subtype Thing :> Anything;")]
    [InlineData("conjugate Thing ~ Anything;")]
    [InlineData("disjoint Thing from Anything;")]
    [InlineData("inverse f of g;")]
    [InlineData("typing f : Thing;")]
    [InlineData("subset f :> g;")]
    [InlineData("redefinition f :>> g;")]
    [InlineData("featuring f by Thing;")]
    [InlineData("feature pi : Real = 3.14;")]
    [InlineData("var feature v : Real;")]
    [InlineData("derived var feature d : Real = 1;")]
    [InlineData("composite feature c : Thing[0..*] ordered;")]
    [InlineData("feature :>> g;")]
    [InlineData("step st : Beh;")]
    [InlineData("step;")]
    [InlineData("expr ex : Fn { 1 }")]
    [InlineData("expr;")]
    [InlineData("bool bl : Pr { true }")]
    [InlineData("bool;")]
    [InlineData("inv iv { true }")]
    [InlineData("inv { true }")]
    [InlineData("inv false iv2 { false }")]
    [InlineData("connector cn from a to b;")]
    [InlineData("binding bd of a = b;")]
    [InlineData("succession sc first a then b;")]
    [InlineData("flow fl from a to b;")]
    [InlineData("succession flow sf from a to b;")]
    [InlineData("private classifier Hidden;")]
    [InlineData("protected feature <sn> shortNamed : Anything;")]
    [InlineData("classifier C { step entry[1]; step do[1] subsets entry; step exit[1]; step accept : Beh; }")]
    [InlineData("classifier C { in frame : Anything default frame.clock; succession [1] entry then [*] accept; }")]
    [InlineData("function F { in x : Anything[0..1]; return r : Anything; true }")]
    [InlineData("predicate Q { in x : Anything; x == x }")]
    public void ParseSource_KermlNamespaceMemberAtPackageLevel_NoErrors(string member)
    {
        var source = $"library package K {{\n{member}\n}}\n";

        var diagnostics = WorkspaceParser.ParseSource("<test>", source);

        Assert.True(
            diagnostics.Count == 0,
            string.Join("\n", diagnostics.Select(d => $"({d.Line},{d.Column}) {d.Message}")));
    }

    /// <summary>
    ///     KerML library content at the root of a file (no enclosing package) parses, because the
    ///     root namespace shares the package body element rule.
    /// </summary>
    [Fact]
    public void ParseSource_KermlMembersAtRootNamespace_NoErrors()
    {
        const string source = """
            abstract classifier Anything;
            feature pi : Anything;
            inv { true }
            """;

        var diagnostics = WorkspaceParser.ParseSource("<test>", source);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     The words <c>entry</c>, <c>do</c>, <c>exit</c>, <c>accept</c>, and <c>frame</c> are valid
    ///     names in KerML libraries (Local Patch 4c-3) but keep their SysML meaning in state, action,
    ///     and requirement bodies.
    /// </summary>
    [Fact]
    public void ParseSource_SysmlKeywordsAlsoUsedAsKermlNames_KeepSysmlMeaning()
    {
        const string source = """
            package Example {
                state def S {
                    entry;
                    do;
                    exit;
                    entry action a;
                    do action b;
                    exit action c;
                }
                action def A {
                    accept sig : Signal;
                }
                requirement def R {
                    frame concern c;
                }
                attribute def Signal;
                concern def Cn;
            }
            """;

        var diagnostics = WorkspaceParser.ParseSource("<test>", source);

        Assert.True(
            diagnostics.Count == 0,
            string.Join("\n", diagnostics.Select(d => $"({d.Line},{d.Column}) {d.Message}")));
    }

    /// <summary>
    ///     SysML usages whose names are KerML keywords (<c>feature</c>, <c>type</c>,
    ///     <c>behavior</c>, <c>step</c>, <c>metaclass</c>, <c>interaction</c>) keep parsing as SysML
    ///     usages: the KerML package-member alternatives never take precedence over the SysML ones.
    /// </summary>
    [Fact]
    public void ParseSource_SysmlUsagesNamedLikeKermlKeywords_NoErrors()
    {
        const string source = """
            package Example {
                part def Holder {
                    attribute feature : ScalarValues::Real;
                    attribute type : ScalarValues::Real;
                    part behavior : Holder;
                    part step : Holder;
                    ref metaclass : Holder;
                    ref interaction : Holder;
                }
                attribute type : ScalarValues::Real;
                part step : Holder;
                part behavior : Holder;
            }
            """;

        var diagnostics = WorkspaceParser.ParseSource("<test>", source);

        Assert.Empty(diagnostics);
    }
}
