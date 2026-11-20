// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;
using DemaConsulting.SysML2Tools.Stdlib;

namespace DemaConsulting.SysML2Tools.Tests.Semantic;

/// <summary>
///     Tests that every named KerML declaration kind a KerML library file can contain at package
///     level (and nested in classifiers and functions) is indexed with its keyword, qualified name,
///     membership details, and supertypes/typing, and that such declarations resolve from SysML
///     models against the shipped standard library. Exercised through the public
///     <see cref="WorkspaceLoader"/> entry point (<c>AstBuilder</c> is internal).
/// </summary>
public sealed class KermlLibraryIndexingTests
{
    /// <summary>
    ///     Declarations every kind test builds on, so that supertype and typing references resolve
    ///     within the test package itself (no standard library involved).
    /// </summary>
    private const string Preamble = """
        datatype Real;
        classifier Base;
        behavior Beh;
        function Fn;
        predicate Pr;
        feature baseFeature : Base;
        """;

    /// <summary>
    ///     Loads <paramref name="body"/> wrapped in <c>package P { Preamble ... }</c> from a temporary
    ///     <c>.kerml</c> file, without the standard library.
    /// </summary>
    /// <param name="body">The declarations to place after the shared preamble.</param>
    /// <param name="strict">Whether strict unresolved-reference mode is enabled.</param>
    /// <returns>The load result.</returns>
    private static async Task<SysmlLoadResult> LoadPackageAsync(string body, bool strict = false)
    {
        var source = "package P {\n" + Preamble + "\n" + body + "\n}\n";
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".kerml");
        try
        {
            await File.WriteAllTextAsync(tempFile, source, TestContext.Current.CancellationToken);
            return await WorkspaceLoader.LoadAsync([tempFile], null, strict);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Loads a SysML model from a temporary file against the shipped standard library.
    /// </summary>
    /// <param name="source">The SysML source text.</param>
    /// <param name="strict">Whether strict unresolved-reference mode is enabled.</param>
    /// <returns>The load result.</returns>
    private static async Task<SysmlLoadResult> LoadAgainstStdlibAsync(string source, bool strict)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, source, TestContext.Current.CancellationToken);
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            return await WorkspaceLoader.LoadAsync([tempFile], stdlibTable, strict);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Formats a result's diagnostics for assertion failure messages.
    /// </summary>
    private static string Describe(SysmlLoadResult result) =>
        string.Join("\n", result.Diagnostics.Select(d => $"  ({d.Line},{d.Column}) {d.Severity}: {d.Message}"));

    /// <summary>
    ///     Every KerML classifier kind declared at package level is indexed as a definition with
    ///     its keyword, qualified name, and supertypes.
    /// </summary>
    /// <param name="declaration">The declaration to place in the package.</param>
    /// <param name="qualifiedName">The expected qualified name.</param>
    /// <param name="keyword">The expected definition keyword.</param>
    /// <param name="supertype">The expected single supertype.</param>
    [Theory]
    [InlineData("abstract classifier Thing :> Base;", "P::Thing", "classifier", "Base")]
    [InlineData("datatype D :> Base;", "P::D", "datatype", "Base")]
    [InlineData("class C :> Base;", "P::C", "class", "Base")]
    [InlineData("struct S :> Base;", "P::S", "struct", "Base")]
    [InlineData("assoc A :> Base;", "P::A", "assoc", "Base")]
    [InlineData("assoc struct AS :> Base;", "P::AS", "assoc struct", "Base")]
    [InlineData("abstract metaclass MC :> Base;", "P::MC", "metaclass", "Base")]
    [InlineData("behavior B2 :> Beh;", "P::B2", "behavior", "Beh")]
    [InlineData("interaction I :> Beh;", "P::I", "interaction", "Beh")]
    [InlineData("type T :> Base;", "P::T", "type", "Base")]
    [InlineData("function F2 :> Fn { in x : Base; }", "P::F2", "function", "Fn")]
    [InlineData("predicate P2 :> Pr { in x : Base; }", "P::P2", "predicate", "Pr")]
    public async Task KermlClassifierKind_AtPackageLevel_IsIndexed(
        string declaration, string qualifiedName, string keyword, string supertype)
    {
        var result = await LoadPackageAsync(declaration, strict: true);

        Assert.True(result.Diagnostics.Count == 0, Describe(result));
        Assert.NotNull(result.Workspace);
        Assert.True(result.Workspace.Declarations.TryGetValue(qualifiedName, out var node));
        var definition = Assert.IsType<SysmlDefinitionNode>(node);
        Assert.Equal(keyword, definition.DefinitionKeyword);
        Assert.Equal(qualifiedName, definition.QualifiedName);
        Assert.Equal([supertype], definition.SupertypeNames);
    }

    /// <summary>
    ///     Every KerML feature kind declared at package level is indexed as a feature with its
    ///     keyword, qualified name, and typing.
    /// </summary>
    /// <param name="declaration">The declaration to place in the package.</param>
    /// <param name="qualifiedName">The expected qualified name.</param>
    /// <param name="keyword">The expected feature keyword.</param>
    /// <param name="typing">The expected feature typing.</param>
    [Theory]
    [InlineData("feature f1 : Base;", "P::f1", "feature", "Base")]
    [InlineData("step st1 : Beh;", "P::st1", "step", "Beh")]
    [InlineData("expr ex1 : Fn;", "P::ex1", "expr", "Fn")]
    [InlineData("bool bl1 : Pr;", "P::bl1", "bool", "Pr")]
    [InlineData("inv iv1 : Pr { true }", "P::iv1", "inv", "Pr")]
    [InlineData("connector cn1 : Base;", "P::cn1", "connector", "Base")]
    [InlineData("binding bd1 : Base;", "P::bd1", "binding", "Base")]
    [InlineData("succession sc1 : Base;", "P::sc1", "succession", "Base")]
    public async Task KermlFeatureKind_AtPackageLevel_IsIndexed(
        string declaration, string qualifiedName, string keyword, string typing)
    {
        var result = await LoadPackageAsync(declaration, strict: true);

        Assert.True(result.Diagnostics.Count == 0, Describe(result));
        Assert.NotNull(result.Workspace);
        Assert.True(result.Workspace.Declarations.TryGetValue(qualifiedName, out var node));
        var feature = Assert.IsType<SysmlFeatureNode>(node);
        Assert.Equal(keyword, feature.FeatureKeyword);
        Assert.Equal(qualifiedName, feature.QualifiedName);
        Assert.Equal(typing, feature.FeatureTyping);
    }

    /// <summary>
    ///     The KerML <c>flow</c> and <c>succession flow</c> features declared in a classifier body
    ///     (where no SysML usage alternative exists) are indexed as features. At package level the
    ///     SysML flow usages take precedence, so these forms are only checked for parsing there.
    /// </summary>
    /// <param name="declaration">The declaration to place in the classifier body.</param>
    /// <param name="qualifiedName">The expected qualified name.</param>
    /// <param name="keyword">The expected feature keyword.</param>
    [Theory]
    [InlineData("flow fl1 : Base;", "P::Holder::fl1", "flow")]
    [InlineData("succession flow sf1 : Base;", "P::Holder::sf1", "succession flow")]
    public async Task KermlFlowKind_InClassifierBody_IsIndexed(string declaration, string qualifiedName, string keyword)
    {
        var result = await LoadPackageAsync("classifier Holder { " + declaration + " }", strict: true);

        Assert.True(result.Diagnostics.Count == 0, Describe(result));
        Assert.NotNull(result.Workspace);
        Assert.True(result.Workspace.Declarations.TryGetValue(qualifiedName, out var node));
        var feature = Assert.IsType<SysmlFeatureNode>(node);
        Assert.Equal(keyword, feature.FeatureKeyword);
        Assert.Equal("Base", feature.FeatureTyping);
    }

    /// <summary>
    ///     A KerML classifier keeps its member visibility, short name, and abstract modifier.
    /// </summary>
    [Fact]
    public async Task KermlClassifier_WithVisibilityShortNameAndAbstract_KeepsMembershipDetails()
    {
        var result = await LoadPackageAsync("private abstract classifier <sn> Named :> Base;");

        Assert.True(result.Diagnostics.Count == 0, Describe(result));
        Assert.NotNull(result.Workspace);
        var definition = Assert.IsType<SysmlDefinitionNode>(result.Workspace.Declarations["P::Named"]);
        Assert.Equal(SysmlVisibility.Private, definition.Visibility);
        Assert.Equal("sn", definition.ShortName);
        Assert.True(definition.IsAbstract);
    }

    /// <summary>
    ///     A KerML feature keeps its member visibility, short name, typing, and multiplicity.
    /// </summary>
    [Fact]
    public async Task KermlFeature_WithVisibilityShortNameAndMultiplicity_KeepsMembershipDetails()
    {
        var result = await LoadPackageAsync("protected feature <fsn> f1 : Base[0..1];");

        Assert.True(result.Diagnostics.Count == 0, Describe(result));
        Assert.NotNull(result.Workspace);
        var feature = Assert.IsType<SysmlFeatureNode>(result.Workspace.Declarations["P::f1"]);
        Assert.Equal(SysmlVisibility.Protected, feature.Visibility);
        Assert.Equal("fsn", feature.ShortName);
        Assert.Equal("Base", feature.FeatureTyping);
        Assert.Equal("[0..1]", feature.Multiplicity);
    }

    /// <summary>
    ///     A KerML feature records its subsetting and redefinition targets.
    /// </summary>
    [Fact]
    public async Task KermlFeature_WithSubsettingAndRedefinition_RecordsTargets()
    {
        var result = await LoadPackageAsync(
            """
            feature sub :> baseFeature;
            classifier Owner { feature own : Base; }
            classifier Derived :> Owner { feature own2 :>> own; }
            """,
            strict: true);

        Assert.True(result.Diagnostics.Count == 0, Describe(result));
        Assert.NotNull(result.Workspace);
        var sub = Assert.IsType<SysmlFeatureNode>(result.Workspace.Declarations["P::sub"]);
        Assert.Equal(["baseFeature"], sub.SupertypeNames);
        var own2 = Assert.IsType<SysmlFeatureNode>(result.Workspace.Declarations["P::Derived::own2"]);
        Assert.Equal("own", own2.RedefinedFeatureName);
    }

    /// <summary>
    ///     A feature nested in a KerML classifier body is indexed under the classifier.
    /// </summary>
    [Fact]
    public async Task KermlClassifier_WithNestedFeature_IndexesTheFeature()
    {
        var result = await LoadPackageAsync("classifier Holder { feature inner : Base; step s : Beh; }", strict: true);

        Assert.True(result.Diagnostics.Count == 0, Describe(result));
        Assert.NotNull(result.Workspace);
        var inner = Assert.IsType<SysmlFeatureNode>(result.Workspace.Declarations["P::Holder::inner"]);
        Assert.Equal("feature", inner.FeatureKeyword);
        Assert.Equal("Base", inner.FeatureTyping);
        var step = Assert.IsType<SysmlFeatureNode>(result.Workspace.Declarations["P::Holder::s"]);
        Assert.Equal("step", step.FeatureKeyword);
    }

    /// <summary>
    ///     The parameters and return feature of a KerML function are indexed under the function.
    /// </summary>
    [Fact]
    public async Task KermlFunction_WithParametersAndReturn_IndexesTheMembers()
    {
        var result = await LoadPackageAsync("function Calc { in x : Base[0..1]; in y : Base; return r : Base; }", strict: true);

        Assert.True(result.Diagnostics.Count == 0, Describe(result));
        Assert.NotNull(result.Workspace);
        var x = Assert.IsType<SysmlFeatureNode>(result.Workspace.Declarations["P::Calc::x"]);
        Assert.Equal(SysmlFeatureDirection.In, x.Direction);
        Assert.Equal("Base", x.FeatureTyping);
        Assert.Equal("[0..1]", x.Multiplicity);
        Assert.IsType<SysmlFeatureNode>(result.Workspace.Declarations["P::Calc::y"]);
        Assert.IsType<SysmlFeatureNode>(result.Workspace.Declarations["P::Calc::r"]);
    }

    /// <summary>
    ///     Unnamed KerML members (an anonymous invariant and boolean expression) parse without
    ///     diagnostics and register no symbol of their own. (A lone <c>step;</c> is not among them:
    ///     the SysML grammar reads <c>step</c> as the name of a bare usage.)
    /// </summary>
    [Fact]
    public async Task KermlUnnamedMembers_AreSkippedFromTheSymbolTable()
    {
        var baseline = await LoadPackageAsync(string.Empty, strict: true);
        var result = await LoadPackageAsync(
            """
            inv { true }
            bool;
            """,
            strict: true);

        Assert.True(result.Diagnostics.Count == 0, Describe(result));
        Assert.NotNull(baseline.Workspace);
        Assert.NotNull(result.Workspace);
        Assert.Equal(
            baseline.Workspace.Declarations.Keys.OrderBy(k => k, StringComparer.Ordinal),
            result.Workspace.Declarations.Keys.OrderBy(k => k, StringComparer.Ordinal));
    }

    /// <summary>
    ///     Relationship statements (<c>subclassifier</c>, <c>subset</c>, <c>redefinition</c>, ...)
    ///     parse and register no symbol of their own.
    /// </summary>
    [Fact]
    public async Task KermlRelationshipStatements_ParseWithoutDiagnostics()
    {
        var result = await LoadPackageAsync(
            """
            classifier Other;
            subclassifier Other :> Base;
            feature fa : Base;
            feature fb : Base;
            subset fa :> fb;
            """,
            strict: true);

        Assert.True(result.Diagnostics.Count == 0, Describe(result));
    }

    /// <summary>
    ///     A SysML model resolves references to the KerML library declarations the standard
    ///     library now indexes — a feature (<c>TrigFunctions::pi</c>), a struct
    ///     (<c>Objects::Point</c>), an abstract metaclass (<c>Metaobjects::SemanticMetadata</c>),
    ///     associations (<c>Occurrences::HappensBefore</c>, <c>HappensJustBefore</c>) and an
    ///     abstract classifier (<c>Base::Anything</c>) — with no diagnostics, by default and in
    ///     strict mode.
    /// </summary>
    /// <param name="strict">Whether strict unresolved-reference mode is enabled.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SysmlModel_ReferencingKermlLibraryDeclarations_ResolvesWithoutDiagnostics(bool strict)
    {
        var result = await LoadAgainstStdlibAsync(
            """
            package M {
                private import TrigFunctions::pi;
                private import Objects::Point;
                private import Metaobjects::SemanticMetadata;
                private import Occurrences::HappensJustBefore;
                attribute a = TrigFunctions::pi;
                part def Q :> Objects::Point;
                metadata def MD :> Metaobjects::SemanticMetadata;
                connection def Causation :> Occurrences::HappensBefore;
                item def Thing :> Base::Anything;
            }
            """,
            strict);

        Assert.True(result.Diagnostics.Count == 0, Describe(result));
    }
}
