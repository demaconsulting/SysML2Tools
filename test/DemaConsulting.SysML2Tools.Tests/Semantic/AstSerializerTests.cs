// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using DemaConsulting.SysML2Tools.Parser;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;

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
    ///     All eight concrete node types survive a round-trip, including the connection and
    ///     transition nodes and their type-specific properties.
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
        table.RegisterAll(new SysmlConnectionNode
        {
            Name = "conn",
            QualifiedName = "conn",
            ConnectionKeyword = "connection",
            EndpointA = "engine.fuelPort",
            EndpointB = "transmission.input",
        });
        table.RegisterAll(new SysmlTransitionNode
        {
            Name = "trans",
            QualifiedName = "trans",
            Source = "Idle",
            Target = "Running",
            Guard = "start",
        });

        var bytes = AstSerializer.Serialize(table, []);
        var (result, _) = AstDeserializer.Deserialize(bytes);

        Assert.Equal(8, result.Symbols.Count);
        Assert.IsType<SysmlPackageNode>(result.Symbols["pkg"]);
        Assert.IsType<SysmlDefinitionNode>(result.Symbols["def"]);
        Assert.IsType<SysmlFeatureNode>(result.Symbols["feat"]);
        Assert.IsType<SysmlImportNode>(result.Symbols["imp"]);
        Assert.IsType<SysmlViewNode>(result.Symbols["view"]);
        Assert.IsType<SysmlViewpointNode>(result.Symbols["vp"]);

        var conn = Assert.IsType<SysmlConnectionNode>(result.Symbols["conn"]);
        Assert.Equal("connection", conn.ConnectionKeyword);
        Assert.Equal("engine.fuelPort", conn.EndpointA);
        Assert.Equal("transmission.input", conn.EndpointB);

        var trans = Assert.IsType<SysmlTransitionNode>(result.Symbols["trans"]);
        Assert.Equal("Idle", trans.Source);
        Assert.Equal("Running", trans.Target);
        Assert.Equal("start", trans.Guard);
    }

    /// <summary>
    ///     Annotations are preserved through a round-trip, including kind and text.
    /// </summary>
    [Fact]
    public void Serialize_Annotations_Preserved()
    {
        var table = new SymbolTable();
        var node = new SysmlPackageNode
        {
            Name = "Pkg",
            QualifiedName = "Pkg",
            Annotations =
            [
                new SysmlAnnotation(SysmlAnnotationKind.Comment, " a comment "),
                new SysmlAnnotation(SysmlAnnotationKind.Documentation, " some\nmulti-line\ndoc "),
            ],
        };
        table.RegisterAll(node);

        var bytes = AstSerializer.Serialize(table, []);
        var (result, _) = AstDeserializer.Deserialize(bytes);

        var rt = result.Symbols["Pkg"];
        Assert.Equal(2, rt.Annotations.Count);
        Assert.Equal(SysmlAnnotationKind.Comment, rt.Annotations[0].Kind);
        Assert.Equal(" a comment ", rt.Annotations[0].Text);
        Assert.Equal(SysmlAnnotationKind.Documentation, rt.Annotations[1].Kind);
        Assert.Equal(" some\nmulti-line\ndoc ", rt.Annotations[1].Text);
    }

    /// <summary>
    ///     A node with no annotations round-trips with an empty (never null) Annotations list.
    /// </summary>
    [Fact]
    public void Serialize_NoAnnotations_RoundTripsEmptyNotNull()
    {
        var table = new SymbolTable();
        var node = new SysmlPackageNode { Name = "Pkg", QualifiedName = "Pkg" };
        table.RegisterAll(node);

        var bytes = AstSerializer.Serialize(table, []);
        var (result, _) = AstDeserializer.Deserialize(bytes);

        var rt = result.Symbols["Pkg"];
        Assert.NotNull(rt.Annotations);
        Assert.Empty(rt.Annotations);
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

    /// <summary>
    ///     A <see cref="SysmlEdge.RawReference"/> round-trips through the polymorphic node
    ///     serializer (resolved edges are serialized inline on their node), and a
    ///     <see langword="null"/> one is omitted from the JSON text.
    /// </summary>
    [Fact]
    public void Serialize_EdgeRawReference_RoundTripsAndNullIsOmitted()
    {
        var table = new SymbolTable();
        table.RegisterAll(new SysmlDefinitionNode
        {
            Name = "Sub",
            QualifiedName = "Q::Sub",
            DefinitionKeyword = "part def",
            ResolvedEdges =
            [
                new SysmlEdge("Q::Sub", "P::X", SysmlEdgeKind.Supertype) { RawReference = "A" },
                new SysmlEdge("Q::Sub", "P::Y", SysmlEdgeKind.Supertype),
            ],
        });

        var bytes = AstSerializer.Serialize(table, []);
        var (result, _) = AstDeserializer.Deserialize(bytes);

        var text = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Count(text, "RawReference"));
        var rt = Assert.IsType<SysmlDefinitionNode>(result.Symbols["Q::Sub"]);
        Assert.Equal(2, rt.ResolvedEdges.Count);
        Assert.Equal("A", rt.ResolvedEdges[0].RawReference);
        Assert.Null(rt.ResolvedEdges[1].RawReference);
    }

    /// <summary>
    ///     A dependency edge's <see cref="SysmlEdge.DeclaredName"/> round-trips through the node
    ///     serializer, and a <see langword="null"/> one is omitted from the JSON text.
    /// </summary>
    [Fact]
    public void Serialize_EdgeDeclaredName_RoundTripsAndNullIsOmitted()
    {
        var table = new SymbolTable();
        table.RegisterAll(new SysmlDefinitionNode
        {
            Name = "Sub",
            QualifiedName = "Q::Sub",
            DefinitionKeyword = "part def",
            ResolvedEdges =
            [
                new SysmlEdge("Q::A", "Q::B", SysmlEdgeKind.Dependency) { DeclaredName = "link" },
                new SysmlEdge("Q::A", "Q::B", SysmlEdgeKind.Dependency),
            ],
        });

        var bytes = AstSerializer.Serialize(table, []);
        var (result, _) = AstDeserializer.Deserialize(bytes);

        var text = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Count(text, "DeclaredName"));
        var rt = Assert.IsType<SysmlDefinitionNode>(result.Symbols["Q::Sub"]);
        Assert.Equal("link", rt.ResolvedEdges[0].DeclaredName);
        Assert.Null(rt.ResolvedEdges[1].DeclaredName);
    }

    /// <summary>
    ///     <see cref="SysmlImportNode.RawReferenceText"/> and <see cref="ExposeMember.RawReferenceText"/>
    ///     round-trip through the node serializer, and a <see langword="null"/> one is omitted from
    ///     the JSON text (so legacy data without the property still deserializes, with
    ///     <see langword="null"/>).
    /// </summary>
    [Fact]
    public void Serialize_ImportAndExposeRawReferenceText_RoundTripsAndNullIsOmitted()
    {
        var table = new SymbolTable();
        table.RegisterAll(new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Q::V",
            ExposeMembers =
            [
                new ExposeMember("P", null, ExposeRecursionKind.NamespaceDirectChildren, "P::*"),
                new ExposeMember("P", null),
            ],
            Children =
            [
                new SysmlImportNode { ImportedNamespace = "P", ImportedNames = ["P"], RawReferenceText = "P::*" },
                new SysmlImportNode { ImportedNamespace = "P", ImportedNames = ["P"] },
            ],
        });

        var bytes = AstSerializer.Serialize(table, []);
        var (result, _) = AstDeserializer.Deserialize(bytes);

        var text = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Count(text, "RawReferenceText"));
        var rt = Assert.IsType<SysmlViewNode>(result.Symbols["Q::V"]);
        Assert.Equal("P::*", rt.ExposeMembers[0].RawReferenceText);
        Assert.Null(rt.ExposeMembers[1].RawReferenceText);
        var imports = rt.Children.OfType<SysmlImportNode>().ToList();
        Assert.Equal("P::*", imports[0].RawReferenceText);
        Assert.Null(imports[1].RawReferenceText);
    }

    /// <summary>
    ///     The new <see cref="SysmlSatisfyNode"/> node type, the <c>"allocation"</c>
    ///     <see cref="SysmlConnectionNode.ConnectionKeyword"/> variant, and
    ///     <see cref="SysmlNode.VerifiedRequirementNames"/> all round-trip through the polymorphic
    ///     <c>$type</c> discriminator without loss.
    /// </summary>
    [Fact]
    public void Serialize_SatisfyAndAllocationNodes_RoundTrip()
    {
        var table = new SymbolTable();
        table.RegisterAll(new SysmlSatisfyNode
        {
            Name = "sat",
            QualifiedName = "sat",
            RequirementName = "Spec",
            SubjectName = "vehicle_design",
        });
        table.RegisterAll(new SysmlConnectionNode
        {
            Name = "alloc",
            QualifiedName = "alloc",
            ConnectionKeyword = "allocation",
            EndpointA = "torqueGenerator",
            EndpointB = "powerTrain",
        });
        table.RegisterAll(new SysmlDefinitionNode
        {
            Name = "verifyingCase",
            QualifiedName = "verifyingCase",
            DefinitionKeyword = "verification def",
            VerifiedRequirementNames = ["MassRequirement"],
        });

        var bytes = AstSerializer.Serialize(table, []);
        var (result, _) = AstDeserializer.Deserialize(bytes);

        var sat = Assert.IsType<SysmlSatisfyNode>(result.Symbols["sat"]);
        Assert.Equal("Spec", sat.RequirementName);
        Assert.Equal("vehicle_design", sat.SubjectName);

        var alloc = Assert.IsType<SysmlConnectionNode>(result.Symbols["alloc"]);
        Assert.Equal("allocation", alloc.ConnectionKeyword);
        Assert.Equal("torqueGenerator", alloc.EndpointA);
        Assert.Equal("powerTrain", alloc.EndpointB);

        var verifyingCase = Assert.IsType<SysmlDefinitionNode>(result.Symbols["verifyingCase"]);
        Assert.Equivalent(new[] { "MassRequirement" }, verifyingCase.VerifiedRequirementNames);
    }

    /// <summary>
    ///     The connector end names, end multiplicities, and payload text of a
    ///     <see cref="SysmlConnectionNode"/> round-trip through the serializer, and a node without them
    ///     omits the properties from the JSON.
    /// </summary>
    [Fact]
    public void Serialize_ConnectionEndLabelsAndPayload_RoundTripAndNullIsOmitted()
    {
        // Arrange
        var table = new SymbolTable();
        table.RegisterAll(new SysmlConnectionNode
        {
            Name = "feed",
            QualifiedName = "feed",
            ConnectionKeyword = "flow",
            EndpointA = "a.out1",
            EndpointB = "b.in1",
            EndpointAName = "supply",
            EndpointAMultiplicity = "[2]",
            EndpointBName = "drain",
            EndpointBMultiplicity = "[1..*]",
            PayloadText = "Torque",
        });
        table.RegisterAll(new SysmlConnectionNode
        {
            Name = "bare",
            QualifiedName = "bare",
            ConnectionKeyword = "interface",
            EndpointA = "a.p",
            EndpointB = "b.q",
        });

        // Act
        var bytes = AstSerializer.Serialize(table, []);
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        var (result, _) = AstDeserializer.Deserialize(bytes);

        // Assert
        var feed = Assert.IsType<SysmlConnectionNode>(result.Symbols["feed"]);
        Assert.Equal("supply", feed.EndpointAName);
        Assert.Equal("[2]", feed.EndpointAMultiplicity);
        Assert.Equal("drain", feed.EndpointBName);
        Assert.Equal("[1..*]", feed.EndpointBMultiplicity);
        Assert.Equal("Torque", feed.PayloadText);
        var bare = Assert.IsType<SysmlConnectionNode>(result.Symbols["bare"]);
        Assert.Equal("interface", bare.ConnectionKeyword);
        Assert.Null(bare.EndpointAName);
        Assert.Null(bare.PayloadText);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(json, "\"PayloadText\""));
    }

    /// <summary>
    ///     Every additive modifier/notation member of definitions, features and transitions —
    ///     including the <see cref="SysmlFeatureDirection"/> enum — round-trips.
    /// </summary>
    [Fact]
    public void Serialize_NewModifierProperties_RoundTrip()
    {
        // Arrange
        var table = new SymbolTable();
        table.RegisterAll(new SysmlDefinitionNode
        {
            Name = "def",
            QualifiedName = "def",
            DefinitionKeyword = "state def",
            IsAbstract = true,
            IsVariation = true,
            IsIndividual = true,
            IsParallel = true,
        });
        table.RegisterAll(new SysmlFeatureNode
        {
            Name = "feat",
            QualifiedName = "feat",
            FeatureKeyword = "part",
            IsAbstract = true,
            IsVariation = true,
            IsIndividual = true,
            IsVariant = true,
            IsReference = true,
            Direction = SysmlFeatureDirection.InOut,
            IsOrdered = true,
            IsNonunique = true,
            ValueText = "= 5 [kg]",
            ReferenceSubsettingName = "owner",
            ConjugatedFeatureTyping = "PortT",
            ActionText = "action initial",
            IsParallel = true,
        });
        table.RegisterAll(new SysmlTransitionNode
        {
            Name = "tr",
            QualifiedName = "tr",
            Source = "a",
            Target = "b",
            Trigger = "sig via pp",
            Effect = "act",
            IsElse = true,
        });

        // Act
        var bytes = AstSerializer.Serialize(table, []);
        var (result, _) = AstDeserializer.Deserialize(bytes);

        // Assert
        var def = Assert.IsType<SysmlDefinitionNode>(result.Symbols["def"]);
        Assert.True(def.IsAbstract);
        Assert.True(def.IsVariation);
        Assert.True(def.IsIndividual);
        Assert.True(def.IsParallel);
        var feat = Assert.IsType<SysmlFeatureNode>(result.Symbols["feat"]);
        Assert.True(feat.IsAbstract);
        Assert.True(feat.IsVariation);
        Assert.True(feat.IsIndividual);
        Assert.True(feat.IsVariant);
        Assert.True(feat.IsReference);
        Assert.Equal(SysmlFeatureDirection.InOut, feat.Direction);
        Assert.True(feat.IsOrdered);
        Assert.True(feat.IsNonunique);
        Assert.Equal("= 5 [kg]", feat.ValueText);
        Assert.Equal("owner", feat.ReferenceSubsettingName);
        Assert.Equal("PortT", feat.ConjugatedFeatureTyping);
        Assert.True(feat.IsConjugated);
        Assert.Equal("action initial", feat.ActionText);
        Assert.True(feat.IsParallel);
        var tr = Assert.IsType<SysmlTransitionNode>(result.Symbols["tr"]);
        Assert.Equal("sig via pp", tr.Trigger);
        Assert.Equal("act", tr.Effect);
        Assert.True(tr.IsElse);
    }

    /// <summary>
    ///     Nodes that leave the new members at their defaults serialize without any of the new
    ///     property names, so existing output and the pre-compiled stdlib stay unchanged in size.
    /// </summary>
    [Fact]
    public void Serialize_DefaultModifierProperties_AreOmittedFromJson()
    {
        // Arrange
        var table = new SymbolTable();
        table.RegisterAll(new SysmlDefinitionNode { Name = "def", QualifiedName = "def", DefinitionKeyword = "part def" });
        table.RegisterAll(new SysmlFeatureNode { Name = "feat", QualifiedName = "feat", FeatureKeyword = "part" });
        table.RegisterAll(new SysmlTransitionNode { Name = "tr", QualifiedName = "tr" });

        // Act
        var json = System.Text.Encoding.UTF8.GetString(AstSerializer.Serialize(table, []));

        // Assert
        foreach (var name in new[]
                 {
                     "IsAbstract", "IsVariation", "IsIndividual", "IsParallel", "IsVariant", "IsReference",
                     "Direction", "IsOrdered", "IsNonunique", "ValueText", "ReferenceSubsettingName",
                     "ConjugatedFeatureTyping", "IsConjugated", "ActionText", "Trigger", "Effect", "IsElse",
                 })
        {
            Assert.DoesNotContain($"\"{name}\"", json);
        }
    }

    /// <summary>
    ///     Source positions (declaration file/line/column and per-reference positions) are only
    ///     needed while a workspace's own files are resolved, so they are not written to the
    ///     serialized format and deserialize to their defaults.
    /// </summary>
    [Fact]
    public void Serialize_SourcePositions_AreNotPersisted()
    {
        // Arrange
        var table = new SymbolTable();
        table.RegisterAll(new SysmlDefinitionNode
        {
            Name = "def",
            QualifiedName = "def",
            DefinitionKeyword = "part def",
            SourceFilePath = "model.sysml",
            SourceLine = 7,
            SourceColumn = 4,
            ReferencePositions = new Dictionary<string, SourcePosition> { ["Base"] = new(7, 18) },
        });

        // Act
        var bytes = AstSerializer.Serialize(table, []);
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        var (result, _) = AstDeserializer.Deserialize(bytes);

        // Assert: nothing about positions is written, and the round trip restores the defaults
        foreach (var name in new[] { "SourceFilePath", "SourceLine", "SourceColumn", "ReferencePositions", "model.sysml" })
        {
            Assert.DoesNotContain(name, json);
        }

        var roundTripped = result.Symbols["def"];
        Assert.Null(roundTripped.SourceFilePath);
        Assert.Equal(0, roundTripped.SourceLine);
        Assert.Equal(0, roundTripped.SourceColumn);
        Assert.Empty(roundTripped.ReferencePositions);
    }

    /// <summary>
    ///     A view node's source location is serialized and restored exactly as before the
    ///     properties moved to the base class (other node kinds do not persist theirs).
    /// </summary>
    [Fact]
    public void Serialize_ViewNodeSourceLocation_RoundTrips()
    {
        // Arrange
        var table = new SymbolTable();
        table.RegisterAll(new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "V",
            SourceFilePath = "views.sysml",
            SourceLine = 3,
            SourceColumn = 5,
        });

        // Act
        var bytes = AstSerializer.Serialize(table, []);
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        var (result, _) = AstDeserializer.Deserialize(bytes);

        // Assert
        Assert.Contains("\"SourceFilePath\":\"views.sysml\"", json);
        Assert.Contains("\"SourceLine\":3", json);
        Assert.Contains("\"SourceColumn\":5", json);
        var view = Assert.IsType<SysmlViewNode>(result.Symbols["V"]);
        Assert.Equal("views.sysml", view.SourceFilePath);
        Assert.Equal(3, view.SourceLine);
        Assert.Equal(5, view.SourceColumn);
    }

    /// <summary>
    ///     <see cref="SysmlFeatureDirection"/> is written as a readable string, and only when set.
    /// </summary>
    [Fact]
    public void Serialize_Direction_WrittenAsString()
    {
        // Arrange
        var table = new SymbolTable();
        table.RegisterAll(new SysmlFeatureNode
        {
            Name = "feat",
            QualifiedName = "feat",
            Direction = SysmlFeatureDirection.InOut,
        });

        // Act
        var json = System.Text.Encoding.UTF8.GetString(AstSerializer.Serialize(table, []));

        // Assert
        Assert.Contains("\"Direction\":\"InOut\"", json.Replace(" ", string.Empty));
    }

    /// <summary>
    ///     Data written before the new members existed (no new properties) loads with all of them
    ///     at their defaults.
    /// </summary>
    [Fact]
    public void Deserialize_LegacyJsonWithoutNewProperties_UsesDefaults()
    {
        // Arrange: hand-written JSON in the shape written before the new members existed
        const string legacyJson =
            """
            {"Symbols":{"feat":{"$type":"feature","FeatureKeyword":"part","FeatureTyping":null,"Name":"feat","QualifiedName":"feat"}},"Diagnostics":[]}
            """;
        var legacyBytes = System.Text.Encoding.UTF8.GetBytes(legacyJson);

        // Act
        var (result, _) = AstDeserializer.Deserialize(legacyBytes);

        // Assert
        var feat = Assert.IsType<SysmlFeatureNode>(result.Symbols["feat"]);
        Assert.False(feat.IsAbstract);
        Assert.False(feat.IsReference);
        Assert.Null(feat.Direction);
        Assert.False(feat.IsOrdered);
        Assert.Null(feat.ValueText);
        Assert.False(feat.IsConjugated);
        Assert.Null(feat.ActionText);
    }

    /// <summary>
    ///     The additional redefinition targets and the diagram-exclusion flag of a feature survive a
    ///     serialization round trip, and are absent (default) on a feature that sets neither.
    /// </summary>
    [Fact]
    public void Serialize_AdditionalRedefinitionsAndExcludedFlag_RoundTrip()
    {
        var table = new SymbolTable();
        var package = new SysmlPackageNode
        {
            Name = "P",
            QualifiedName = "P",
            Children =
            [
                new SysmlFeatureNode
                {
                    Name = "f",
                    QualifiedName = "P::f",
                    FeatureKeyword = "ref",
                    RedefinedFeatureName = "a",
                    AdditionalRedefinedFeatureNames = ["b", "c"],
                    IsExcludedFromDiagrams = true,
                    Direction = SysmlFeatureDirection.Out,
                },
                new SysmlFeatureNode { Name = "g", QualifiedName = "P::g", FeatureKeyword = "part" },
            ],
        };
        table.RegisterAll(package);

        var bytes = AstSerializer.Serialize(table, []);
        var (result, _) = AstDeserializer.Deserialize(bytes);

        var f = Assert.IsType<SysmlFeatureNode>(result.Symbols["P::f"]);
        Assert.Equal(["b", "c"], f.AdditionalRedefinedFeatureNames);
        Assert.True(f.IsExcludedFromDiagrams);
        var g = Assert.IsType<SysmlFeatureNode>(result.Symbols["P::g"]);
        Assert.Null(g.AdditionalRedefinedFeatureNames);
        Assert.False(g.IsExcludedFromDiagrams);
    }
}
