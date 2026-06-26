// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using DemaConsulting.SysML2Tools.Parser;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Internal;

namespace DemaConsulting.SysML2Tools.Tests.Semantic;

/// <summary>
///     Tests for <see cref="AstSerializer"/> and <see cref="AstDeserializer"/> round-trip fidelity.
/// </summary>
public sealed class AstSerializerTests
{
    /// <summary>
    ///     An empty symbol table serializes and deserializes to an empty table.
    /// </summary>
    [Fact]
    public void Serialize_EmptyTable_RoundTrips()
    {
        var table = new SymbolTable();
        var diagnostics = Array.Empty<SysmlDiagnostic>();

        var bytes = AstSerializer.Serialize(table, diagnostics);
        var (result, resultDiags) = AstDeserializer.Deserialize(bytes);

        Assert.Empty(result.Symbols);
        Assert.Empty(resultDiags);
    }

    /// <summary>
    ///     A symbol table with a package node round-trips correctly.
    /// </summary>
    [Fact]
    public void Serialize_PackageNode_RoundTrips()
    {
        var table = new SymbolTable();
        var node = new SysmlPackageNode { Name = "MyPackage", QualifiedName = "MyPackage" };
        table.RegisterAll(node);

        var bytes = AstSerializer.Serialize(table, []);
        var (result, _) = AstDeserializer.Deserialize(bytes);

        Assert.True(result.Symbols.ContainsKey("MyPackage"));
        var roundTripped = result.Symbols["MyPackage"];
        Assert.IsType<SysmlPackageNode>(roundTripped);
        Assert.Equal("MyPackage", roundTripped.Name);
        Assert.Equal("MyPackage", roundTripped.QualifiedName);
    }

    /// <summary>
    ///     A symbol table with a definition node round-trips with keyword preserved.
    /// </summary>
    [Fact]
    public void Serialize_DefinitionNode_RoundTrips()
    {
        var table = new SymbolTable();
        var node = new SysmlDefinitionNode
        {
            Name = "MyDef",
            QualifiedName = "MyDef",
            DefinitionKeyword = "part def"
        };
        table.RegisterAll(node);

        var bytes = AstSerializer.Serialize(table, []);
        var (result, _) = AstDeserializer.Deserialize(bytes);

        Assert.True(result.Symbols.ContainsKey("MyDef"));
        var roundTripped = result.Symbols["MyDef"] as SysmlDefinitionNode;
        Assert.NotNull(roundTripped);
        Assert.Equal("part def", roundTripped.DefinitionKeyword);
    }

    /// <summary>
    ///     All six node types survive a round-trip.
    /// </summary>
    [Fact]
    public void Serialize_AllNodeTypes_RoundTrip()
    {
        var table = new SymbolTable();
        table.RegisterAll(new SysmlPackageNode { Name = "pkg", QualifiedName = "pkg" });
        table.RegisterAll(new SysmlDefinitionNode { Name = "def", QualifiedName = "def", DefinitionKeyword = "part def" });
        table.RegisterAll(new SysmlFeatureNode { Name = "feat", QualifiedName = "feat" });
        table.RegisterAll(new SysmlImportNode { Name = "imp", QualifiedName = "imp", ImportedNamespace = "Other", IsWildcard = true });
        table.RegisterAll(new SysmlViewNode { Name = "view", QualifiedName = "view" });
        table.RegisterAll(new SysmlViewpointNode { Name = "vp", QualifiedName = "vp" });

        var bytes = AstSerializer.Serialize(table, []);
        var (result, _) = AstDeserializer.Deserialize(bytes);

        Assert.Equal(6, result.Symbols.Count);
        Assert.IsType<SysmlPackageNode>(result.Symbols["pkg"]);
        Assert.IsType<SysmlDefinitionNode>(result.Symbols["def"]);
        Assert.IsType<SysmlFeatureNode>(result.Symbols["feat"]);
        Assert.IsType<SysmlImportNode>(result.Symbols["imp"]);
        Assert.IsType<SysmlViewNode>(result.Symbols["view"]);
        Assert.IsType<SysmlViewpointNode>(result.Symbols["vp"]);
    }

    /// <summary>
    ///     Diagnostics are preserved through a round-trip.
    /// </summary>
    [Fact]
    public void Serialize_Diagnostics_RoundTrip()
    {
        var table = new SymbolTable();
        var diags = new List<SysmlDiagnostic>
        {
            new("file.sysml", 1, 0, DiagnosticSeverity.Warning, "test warning"),
            new("file.sysml", 2, 5, DiagnosticSeverity.Error, "test error"),
        };

        var bytes = AstSerializer.Serialize(table, diags);
        var (_, resultDiags) = AstDeserializer.Deserialize(bytes);

        Assert.Equal(2, resultDiags.Count);
        Assert.Equal(DiagnosticSeverity.Warning, resultDiags[0].Severity);
        Assert.Equal("test warning", resultDiags[0].Message);
        Assert.Equal(DiagnosticSeverity.Error, resultDiags[1].Severity);
    }

    /// <summary>
    ///     SupertypeNames and ImportedNames are preserved through a round-trip.
    /// </summary>
    [Fact]
    public void Serialize_SupertypeAndImportedNames_Preserved()
    {
        var table = new SymbolTable();
        var node = new SysmlDefinitionNode
        {
            Name = "Child",
            QualifiedName = "Child",
            DefinitionKeyword = "part def",
            SupertypeNames = ["Base1", "Base2"],
            ImportedNames = ["NS1", "NS2"],
        };
        table.RegisterAll(node);

        var bytes = AstSerializer.Serialize(table, []);
        var (result, _) = AstDeserializer.Deserialize(bytes);

        var rt = result.Symbols["Child"] as SysmlDefinitionNode;
        Assert.NotNull(rt);
        Assert.Equivalent(new[] { "Base1", "Base2" }, rt.SupertypeNames);
        Assert.Equivalent(new[] { "NS1", "NS2" }, rt.ImportedNames);
    }
}
