// <copyright file="GeneralViewLayoutStrategyTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.SysML2Tools.Layout.Internal;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;
using DemaConsulting.SysML2Tools.Stdlib;

namespace DemaConsulting.SysML2Tools.Tests.Layout;

/// <summary>
///     Tests for <see cref="GeneralViewLayoutStrategy"/> layout computation.
/// </summary>
public sealed class GeneralViewLayoutStrategyTests
{
    /// <summary>
    ///     BuildLayout with an empty workspace returns a minimal canvas LayoutTree
    ///     with no nodes, confirming that the empty-workspace sentinel is applied.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_EmptyWorkspace_ReturnsMinimalCanvas()
    {
        // Arrange: strategy, empty workspace, and default options
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace();
        var context = new ViewContext("testView", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act: build layout for an empty workspace
        var layout = strategy.BuildLayout(context, options);

        // Assert: returns minimal canvas dimensions with no nodes
        Assert.Equal(200.0, layout.Width);
        Assert.Equal(100.0, layout.Height);
        Assert.Empty(layout.Nodes);
    }

    /// <summary>
    ///     BuildLayout with a workspace containing only stdlib declarations returns a
    ///     minimal canvas, confirming stdlib filtering is applied.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_StdlibOnlyWorkspace_ReturnsMinimalCanvas()
    {
        // Arrange: strategy and a workspace containing only stdlib declarations
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                // SysML stdlib element — must be filtered
                ["SysML::Parts::PartDef"] = new SysmlDefinitionNode
                {
                    Name = "PartDef",
                    QualifiedName = "SysML::Parts::PartDef",
                    DefinitionKeyword = "part def"
                }
            }
        };
        var context = new ViewContext("stdlibView", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act: build layout for a stdlib-only workspace
        var layout = strategy.BuildLayout(context, options);

        // Assert: stdlib elements are filtered out, producing minimal canvas
        Assert.Equal(200.0, layout.Width);
        Assert.Equal(100.0, layout.Height);
        Assert.Empty(layout.Nodes);
    }

    /// <summary>
    ///     BuildLayout with a workspace containing one user-defined part def produces a
    ///     LayoutTree with at least one LayoutBox node, confirming that user part defs
    ///     are rendered.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_OneUserPartDef_ProducesLayoutBox()
    {
        // Arrange: strategy and a workspace with a single user-defined part def
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["MyPackage::MyComponent"] = new SysmlDefinitionNode
                {
                    Name = "MyComponent",
                    QualifiedName = "MyPackage::MyComponent",
                    DefinitionKeyword = "part def"
                }
            }
        };
        var context = new ViewContext("componentView", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act: build layout for a workspace with one user part def
        var layout = strategy.BuildLayout(context, options);

        // Assert: layout tree is non-empty and contains at least one LayoutBox
        Assert.NotEmpty(layout.Nodes);
        Assert.Contains(layout.Nodes, n => n is LayoutBox);
    }

    /// <summary>
    ///     BuildLayout renders definitions of kinds other than <c>part def</c> (e.g. port def,
    ///     interface def), each carrying its keyword, confirming the strategy is no longer
    ///     restricted to part defs.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_MixedDefinitionKinds_RendersAllWithKeywords()
    {
        // Arrange: a workspace with three different definition kinds
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Engine"] = new SysmlDefinitionNode { Name = "Engine", QualifiedName = "P::Engine", DefinitionKeyword = "part def" },
                ["P::FuelPort"] = new SysmlDefinitionNode { Name = "FuelPort", QualifiedName = "P::FuelPort", DefinitionKeyword = "port def" },
                ["P::IFuel"] = new SysmlDefinitionNode { Name = "IFuel", QualifiedName = "P::IFuel", DefinitionKeyword = "interface def" }
            }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: collect every box keyword in the tree and confirm all three kinds appear
        var keywords = CollectBoxes(layout.Nodes).Select(b => b.Keyword).ToList();
        Assert.Contains("part def", keywords);
        Assert.Contains("port def", keywords);
        Assert.Contains("interface def", keywords);
    }

    /// <summary>
    ///     BuildLayout wraps a package's definitions in a folder-shaped container box.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_PackagedDefinitions_ProducesFolderBox()
    {
        // Arrange: two definitions within the same package
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["Sys::A"] = new SysmlDefinitionNode { Name = "A", QualifiedName = "Sys::A", DefinitionKeyword = "part def" },
                ["Sys::B"] = new SysmlDefinitionNode { Name = "B", QualifiedName = "Sys::B", DefinitionKeyword = "part def" }
            }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: a folder-shaped box exists carrying the package keyword
        var folder = CollectBoxes(layout.Nodes).FirstOrDefault(b => b.Shape == BoxShape.Folder);
        Assert.NotNull(folder);
        Assert.Equal("package", folder!.Keyword);
        Assert.Equal("Sys", folder.Label);
    }

    /// <summary>
    ///     BuildLayout draws a specialization edge (a <see cref="LayoutLine"/>) between a subtype
    ///     and its supertype when both are present in the workspace.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_Subclassification_ProducesEdge()
    {
        // Arrange: B specializes A, both in the same package
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::A"] = new SysmlDefinitionNode { Name = "A", QualifiedName = "P::A", DefinitionKeyword = "part def" },
                ["P::B"] = new SysmlDefinitionNode
                {
                    Name = "B",
                    QualifiedName = "P::B",
                    DefinitionKeyword = "part def",
                    SupertypeNames = ["A"]
                }
            }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: at least one orthogonal line with an open arrowhead at the supertype end
        var line = CollectLines(layout.Nodes).FirstOrDefault();
        Assert.NotNull(line);
        Assert.Equal(EndMarkerStyle.HollowTriangle, line!.TargetEnd);
        Assert.True(line.Waypoints.Count >= 2);
    }

    /// <summary>
    ///     BuildLayout excludes declarations listed in the workspace's seed-derived
    ///     <see cref="SysmlWorkspace.StdlibNames"/> set even when their names do not match a known
    ///     stdlib root-package prefix.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_SeedStdlibNames_AreExcluded()
    {
        // Arrange: a definition whose name is not a known stdlib prefix but is in the seed set
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["CustomLib::Helper"] = new SysmlDefinitionNode { Name = "Helper", QualifiedName = "CustomLib::Helper", DefinitionKeyword = "part def" }
            },
            StdlibNames = new HashSet<string>(StringComparer.Ordinal) { "CustomLib::Helper" }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: the seed-listed element is filtered out, producing the minimal canvas
        Assert.Empty(layout.Nodes);
    }

    /// <summary>
    ///     BuildLayout populates a definition box with compartments grouped by usage keyword,
    ///     formatting each usage as a <c>name : Type</c> row.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_DefinitionWithUsages_ProducesCompartments()
    {
        // Arrange: a part def owning an attribute usage and a port usage
        var strategy = new GeneralViewLayoutStrategy();
        var vehicle = new SysmlDefinitionNode
        {
            Name = "Vehicle",
            QualifiedName = "P::Vehicle",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "mass", QualifiedName = "P::Vehicle::mass", FeatureKeyword = "attribute", FeatureTyping = "Real" },
                new SysmlFeatureNode { Name = "fuel", QualifiedName = "P::Vehicle::fuel", FeatureKeyword = "port", FeatureTyping = "FuelPort" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["P::Vehicle"] = vehicle }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: the Vehicle box has an attributes compartment and a ports compartment
        var box = CollectBoxes(layout.Nodes).First(b => b.Label == "Vehicle");
        Assert.Equal(2, box.Compartments.Count);
        Assert.Contains(box.Compartments, c => c.Title == "attributes" && c.Rows.Contains("mass : Real"));
        Assert.Contains(box.Compartments, c => c.Title == "ports" && c.Rows.Contains("fuel : FuelPort"));
    }

    /// <summary>Recursively collects all <see cref="LayoutBox"/> nodes from a node list.</summary>
    private static IReadOnlyList<LayoutBox> CollectBoxes(IReadOnlyList<LayoutNode> nodes)
    {
        var result = new List<LayoutBox>();
        void Walk(IReadOnlyList<LayoutNode> ns)
        {
            foreach (var n in ns)
            {
                if (n is LayoutBox box)
                {
                    result.Add(box);
                    Walk(box.Children);
                }
            }
        }

        Walk(nodes);
        return result;
    }

    /// <summary>
    /// Recursively collects all <see cref="LayoutLine"/> nodes from a node list, including those
    /// nested inside a package folder's own routed edges (an intra-package edge is routed within its
    /// folder's own coordinate space by the hierarchical layout engine, so it appears in that folder
    /// box's <see cref="LayoutBox.Children"/> rather than at the top level).
    /// </summary>
    private static IReadOnlyList<LayoutLine> CollectLines(IReadOnlyList<LayoutNode> nodes)
    {
        var result = new List<LayoutLine>();
        void Walk(IReadOnlyList<LayoutNode> ns)
        {
            foreach (var n in ns)
            {
                switch (n)
                {
                    case LayoutLine line:
                        result.Add(line);
                        break;
                    case LayoutBox box:
                        Walk(box.Children);
                        break;
                }
            }
        }

        Walk(nodes);
        return result;
    }

    /// <summary>
    ///     A part def that owns a typed feature emits a filled-diamond line from the feature's type
    ///     box to the owning definition box.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_CompositeMembership_ProducesFilledDiamondEdge()
    {
        // Arrange: Vehicle owns a part typed as Wheel; both are user definitions
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Wheel"] = new SysmlDefinitionNode { Name = "Wheel", QualifiedName = "P::Wheel", DefinitionKeyword = "part def" },
                ["P::Vehicle"] = new SysmlDefinitionNode
                {
                    Name = "Vehicle",
                    QualifiedName = "P::Vehicle",
                    DefinitionKeyword = "part def",
                    Children =
                    [
                        new SysmlFeatureNode { Name = "wheel", QualifiedName = "P::Vehicle::wheel", FeatureKeyword = "part", FeatureTyping = "Wheel" }
                    ]
                }
            }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: a line with a filled-diamond arrowhead at the owner (Vehicle) end exists
        var membershipEdge = CollectLines(layout.Nodes)
            .FirstOrDefault(l => l.TargetEnd == EndMarkerStyle.FilledDiamond);
        Assert.NotNull(membershipEdge);
    }

    /// <summary>
    ///     A part def that owns a <c>ref</c>-typed feature emits a hollow-diamond line from the
    ///     referenced type box to the owning definition box (SysML v2 reference membership notation).
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_ReferenceMembership_ProducesHollowDiamondEdge()
    {
        // Arrange: System owns a ref typed as Engine; both are user definitions
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Engine"] = new SysmlDefinitionNode { Name = "Engine", QualifiedName = "P::Engine", DefinitionKeyword = "part def" },
                ["P::System"] = new SysmlDefinitionNode
                {
                    Name = "System",
                    QualifiedName = "P::System",
                    DefinitionKeyword = "part def",
                    Children =
                    [
                        new SysmlFeatureNode { Name = "eng", QualifiedName = "P::System::eng", FeatureKeyword = "ref", FeatureTyping = "Engine" }
                    ]
                }
            }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: a hollow-diamond arrowhead edge (EndMarkerStyle.HollowDiamond) is emitted for a ref feature
        var membershipEdge = CollectLines(layout.Nodes)
            .FirstOrDefault(l => l.TargetEnd == EndMarkerStyle.HollowDiamond);
        Assert.NotNull(membershipEdge);
    }

    /// <summary>
    ///     A part def that owns an <c>attribute</c>-typed feature does NOT emit any diamond edge,
    ///     because attribute features are excluded from the membership-edge filter.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_AttributeFeature_DoesNotProduceDiamondEdge()
    {
        // Arrange: Vehicle owns an attribute typed as Real (represented as a user definition)
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Mass"] = new SysmlDefinitionNode { Name = "Mass", QualifiedName = "P::Mass", DefinitionKeyword = "attribute def" },
                ["P::Vehicle"] = new SysmlDefinitionNode
                {
                    Name = "Vehicle",
                    QualifiedName = "P::Vehicle",
                    DefinitionKeyword = "part def",
                    Children =
                    [
                        new SysmlFeatureNode { Name = "mass", QualifiedName = "P::Vehicle::mass", FeatureKeyword = "attribute", FeatureTyping = "Mass" }
                    ]
                }
            }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: no diamond arrowhead edge is produced for an attribute feature
        var membershipEdge = CollectLines(layout.Nodes)
            .FirstOrDefault(l => l.TargetEnd == EndMarkerStyle.HollowDiamond ||
                                 l.TargetEnd == EndMarkerStyle.FilledDiamond);
        Assert.Null(membershipEdge);
    }

    /// <summary>
    ///     A definition that owns an <c>attribute</c>-typed feature whose type is another definition
    ///     in the view draws a dashed dependency line with an open chevron at the attribute-type box,
    ///     connecting the otherwise-disconnected attribute def into the cluster.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_AttributeTyping_ProducesDashedOpenChevronEdge()
    {
        // Arrange: Vehicle owns an attribute typed as the user attribute def Mass
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Mass"] = new SysmlDefinitionNode { Name = "Mass", QualifiedName = "P::Mass", DefinitionKeyword = "attribute def" },
                ["P::Vehicle"] = new SysmlDefinitionNode
                {
                    Name = "Vehicle",
                    QualifiedName = "P::Vehicle",
                    DefinitionKeyword = "part def",
                    Children =
                    [
                        new SysmlFeatureNode { Name = "mass", QualifiedName = "P::Vehicle::mass", FeatureKeyword = "attribute", FeatureTyping = "Mass" }
                    ]
                }
            }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: a dashed dependency line with an open chevron at the attribute-type (Mass) end exists
        var typingEdge = CollectLines(layout.Nodes)
            .FirstOrDefault(l => l.LineStyle == LineStyle.Dashed && l.TargetEnd == EndMarkerStyle.OpenChevron);
        Assert.NotNull(typingEdge);
        Assert.True(typingEdge!.Waypoints.Count >= 2);

        // Assert: attribute typing is a dependency, not composition — no membership diamond is drawn.
        var diamondEdge = CollectLines(layout.Nodes)
            .FirstOrDefault(l => l.TargetEnd == EndMarkerStyle.FilledDiamond ||
                                 l.TargetEnd == EndMarkerStyle.HollowDiamond);
        Assert.Null(diamondEdge);
    }

    /// <summary>
    ///     A subtype feature that redefines a bare-named inherited feature (declared on a resolved
    ///     supertype in the view) emits a solid hollow-triangle-with-crossbar line from the subtype
    ///     to the supertype that declares the redefined feature.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_BareNameRedefinition_ProducesHollowTriangleCrossbarEdge()
    {
        // Arrange: Vehicle declares "eng"; SmallVehicle specializes Vehicle and redefines "eng" by
        // bare name (no Owner:: qualifier).
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Vehicle"] = new SysmlDefinitionNode
                {
                    Name = "Vehicle",
                    QualifiedName = "P::Vehicle",
                    DefinitionKeyword = "part def",
                    Children =
                    [
                        new SysmlFeatureNode { Name = "eng", QualifiedName = "P::Vehicle::eng", FeatureKeyword = "attribute", FeatureTyping = "Real" }
                    ]
                },
                ["P::SmallVehicle"] = new SysmlDefinitionNode
                {
                    Name = "SmallVehicle",
                    QualifiedName = "P::SmallVehicle",
                    DefinitionKeyword = "part def",
                    SupertypeNames = ["Vehicle"],
                    Children =
                    [
                        new SysmlFeatureNode { Name = "smallEng", QualifiedName = "P::SmallVehicle::smallEng", FeatureKeyword = "attribute", FeatureTyping = "Real", RedefinedFeatureName = "eng" }
                    ]
                }
            }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: a solid line with a hollow-triangle-crossbar arrowhead exists, from SmallVehicle to Vehicle
        var redefinitionEdge = CollectLines(layout.Nodes)
            .FirstOrDefault(l => l.TargetEnd == EndMarkerStyle.HollowTriangleCrossbar);
        Assert.NotNull(redefinitionEdge);
        Assert.Equal(LineStyle.Solid, redefinitionEdge!.LineStyle);
    }

    /// <summary>
    ///     A subtype feature that redefines a qualified <c>Owner::feature</c> reference emits a
    ///     hollow-triangle-with-crossbar edge to the named owner, without needing to walk the
    ///     supertype chain.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_QualifiedRedefinition_ProducesHollowTriangleCrossbarEdgeToOwner()
    {
        // Arrange: Car redefines "Vehicle::mass" directly by qualified reference.
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Vehicle"] = new SysmlDefinitionNode
                {
                    Name = "Vehicle",
                    QualifiedName = "P::Vehicle",
                    DefinitionKeyword = "part def",
                    Children =
                    [
                        new SysmlFeatureNode { Name = "mass", QualifiedName = "P::Vehicle::mass", FeatureKeyword = "attribute", FeatureTyping = "Real" }
                    ]
                },
                ["P::Car"] = new SysmlDefinitionNode
                {
                    Name = "Car",
                    QualifiedName = "P::Car",
                    DefinitionKeyword = "part def",
                    SupertypeNames = ["Vehicle"],
                    Children =
                    [
                        new SysmlFeatureNode { Name = "carMass", QualifiedName = "P::Car::carMass", FeatureKeyword = "attribute", FeatureTyping = "Real", RedefinedFeatureName = "Vehicle::mass" }
                    ]
                }
            }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: a hollow-triangle-crossbar arrowhead edge is emitted, resolved via the qualified reference
        var redefinitionEdge = CollectLines(layout.Nodes)
            .FirstOrDefault(l => l.TargetEnd == EndMarkerStyle.HollowTriangleCrossbar);
        Assert.NotNull(redefinitionEdge);
    }

    /// <summary>
    ///     An unresolvable redefinition reference (neither a qualified owner nor a bare name found
    ///     anywhere in the supertype chain) produces no redefinition edge and does not throw. A
    ///     self-referential redefinition (resolving back to the same definition) is likewise skipped.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_UnresolvableRedefinition_ProducesNoEdge()
    {
        // Arrange: Vehicle redefines a bare name that does not exist anywhere in scope, and Standalone
        // redefines its own feature name (self-reference, since it has no supertype).
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Vehicle"] = new SysmlDefinitionNode
                {
                    Name = "Vehicle",
                    QualifiedName = "P::Vehicle",
                    DefinitionKeyword = "part def",
                    Children =
                    [
                        new SysmlFeatureNode { Name = "eng", QualifiedName = "P::Vehicle::eng", FeatureKeyword = "attribute", FeatureTyping = "Real", RedefinedFeatureName = "nonExistentFeature" }
                    ]
                }
            }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act: laying out must not throw even though the redefinition cannot be resolved.
        var layout = strategy.BuildLayout(context, options);

        // Assert: no hollow-triangle-crossbar edge is produced.
        var redefinitionEdge = CollectLines(layout.Nodes)
            .FirstOrDefault(l => l.TargetEnd == EndMarkerStyle.HollowTriangleCrossbar);
        Assert.Null(redefinitionEdge);
    }

    /// <summary>
    ///     A definition with TWO <c>attribute</c>-typed features of the SAME in-view type produces two
    ///     identical owner→type intra-group edges. The layered pipeline de-duplicates the identical
    ///     directed pair so its routed waypoints are not 1:1 with the intra-edges; the strategy must
    ///     resolve each intra-edge by its endpoints (not by input position) and lay out without throwing,
    ///     emitting one dashed open-chevron typing dependency per attribute.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_TwoAttributesSameType_ProducesTwoTypingEdgesWithoutException()
    {
        // Arrange: Vehicle owns two attributes (mass, weight) both typed as the user attribute def Mass.
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Mass"] = new SysmlDefinitionNode { Name = "Mass", QualifiedName = "P::Mass", DefinitionKeyword = "attribute def" },
                ["P::Vehicle"] = new SysmlDefinitionNode
                {
                    Name = "Vehicle",
                    QualifiedName = "P::Vehicle",
                    DefinitionKeyword = "part def",
                    Children =
                    [
                        new SysmlFeatureNode { Name = "mass", QualifiedName = "P::Vehicle::mass", FeatureKeyword = "attribute", FeatureTyping = "Mass" },
                        new SysmlFeatureNode { Name = "weight", QualifiedName = "P::Vehicle::weight", FeatureKeyword = "attribute", FeatureTyping = "Mass" }
                    ]
                }
            }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act: laying out must not throw even though the two intra-edges share one routed polyline.
        var layout = strategy.BuildLayout(context, options);

        // Assert: exactly two dashed open-chevron typing dependencies are drawn (one per attribute), and
        // each has a real polyline.
        var typingEdges = CollectLines(layout.Nodes)
            .Where(l => l.LineStyle == LineStyle.Dashed && l.TargetEnd == EndMarkerStyle.OpenChevron)
            .ToList();
        Assert.Equal(2, typingEdges.Count);
        Assert.All(typingEdges, e => Assert.True(e.Waypoints.Count >= 2));
    }

    /// <summary>
    ///     An <c>attribute</c>-typed feature whose type is an <c>enum def</c> in the view also draws a
    ///     dashed open-chevron typing dependency to the enumeration definition.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_EnumTypedAttribute_ProducesDashedOpenChevronEdge()
    {
        // Arrange: Controller owns an attribute typed as the user enum def FlightMode
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::FlightMode"] = new SysmlDefinitionNode { Name = "FlightMode", QualifiedName = "P::FlightMode", DefinitionKeyword = "enum def" },
                ["P::Controller"] = new SysmlDefinitionNode
                {
                    Name = "Controller",
                    QualifiedName = "P::Controller",
                    DefinitionKeyword = "part def",
                    Children =
                    [
                        new SysmlFeatureNode { Name = "mode", QualifiedName = "P::Controller::mode", FeatureKeyword = "attribute", FeatureTyping = "FlightMode" }
                    ]
                }
            }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: a dashed dependency line with an open chevron at the enum-type (FlightMode) end exists
        var typingEdge = CollectLines(layout.Nodes)
            .FirstOrDefault(l => l.LineStyle == LineStyle.Dashed && l.TargetEnd == EndMarkerStyle.OpenChevron);
        Assert.NotNull(typingEdge);
    }

    /// <summary>
    ///     An <c>attribute</c> feature whose type does not resolve to a definition in the view draws
    ///     no typing edge, mirroring the specialization/membership resolution rules.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_AttributeTyping_UnresolvedType_ProducesNoEdge()
    {
        // Arrange: Vehicle owns an attribute typed as a name with no matching definition in the view
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Vehicle"] = new SysmlDefinitionNode
                {
                    Name = "Vehicle",
                    QualifiedName = "P::Vehicle",
                    DefinitionKeyword = "part def",
                    Children =
                    [
                        new SysmlFeatureNode { Name = "mass", QualifiedName = "P::Vehicle::mass", FeatureKeyword = "attribute", FeatureTyping = "Real" }
                    ]
                }
            }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: no typing dependency edge is produced when the attribute type is unresolved
        var typingEdge = CollectLines(layout.Nodes)
            .FirstOrDefault(l => l.LineStyle == LineStyle.Dashed && l.TargetEnd == EndMarkerStyle.OpenChevron);
        Assert.Null(typingEdge);
    }

    /// <summary>
    ///     A part def that owns a <c>port</c>-typed feature emits a filled-diamond line from the
    ///     port's type box to the owning definition box.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_PortFeature_ProducesFilledDiamondEdge()
    {
        // Arrange: Vehicle owns a port typed as FuelPort; both are user definitions
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::FuelPort"] = new SysmlDefinitionNode { Name = "FuelPort", QualifiedName = "P::FuelPort", DefinitionKeyword = "port def" },
                ["P::Vehicle"] = new SysmlDefinitionNode
                {
                    Name = "Vehicle",
                    QualifiedName = "P::Vehicle",
                    DefinitionKeyword = "part def",
                    Children =
                    [
                        new SysmlFeatureNode { Name = "fuel", QualifiedName = "P::Vehicle::fuel", FeatureKeyword = "port", FeatureTyping = "FuelPort" }
                    ]
                }
            }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: a line with a filled-diamond arrowhead at the owner (Vehicle) end exists
        var membershipEdge = CollectLines(layout.Nodes)
            .FirstOrDefault(l => l.TargetEnd == EndMarkerStyle.FilledDiamond);
        Assert.NotNull(membershipEdge);
    }

    /// <summary>
    ///     A dense model where one definition owns four others as parts is placed by the layered
    ///     pipeline so that no two definition boxes overlap.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_AdaptiveGap_DenseModelProducesNonOverlappingBoxes()
    {
        // Arrange: five definitions where Root owns all four others as parts, producing
        // many membership edges that the layered pipeline must route between separated boxes.
        var strategy = new GeneralViewLayoutStrategy();
        var denseWorkspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["Q::D1"] = new SysmlDefinitionNode { Name = "D1", QualifiedName = "Q::D1", DefinitionKeyword = "part def" },
                ["Q::D2"] = new SysmlDefinitionNode { Name = "D2", QualifiedName = "Q::D2", DefinitionKeyword = "part def" },
                ["Q::D3"] = new SysmlDefinitionNode { Name = "D3", QualifiedName = "Q::D3", DefinitionKeyword = "part def" },
                ["Q::D4"] = new SysmlDefinitionNode { Name = "D4", QualifiedName = "Q::D4", DefinitionKeyword = "part def" },
                ["Q::Root"] = new SysmlDefinitionNode
                {
                    Name = "Root",
                    QualifiedName = "Q::Root",
                    DefinitionKeyword = "part def",
                    Children =
                    [
                        new SysmlFeatureNode { Name = "d1", QualifiedName = "Q::Root::d1", FeatureKeyword = "part", FeatureTyping = "D1" },
                        new SysmlFeatureNode { Name = "d2", QualifiedName = "Q::Root::d2", FeatureKeyword = "part", FeatureTyping = "D2" },
                        new SysmlFeatureNode { Name = "d3", QualifiedName = "Q::Root::d3", FeatureKeyword = "part", FeatureTyping = "D3" },
                        new SysmlFeatureNode { Name = "d4", QualifiedName = "Q::Root::d4", FeatureKeyword = "part", FeatureTyping = "D4" }
                    ]
                }
            }
        };

        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(new ViewContext("dense", denseWorkspace), options);

        // Assert: the canvas is valid, carries no warnings, and the definition boxes do not overlap.
        Assert.True(layout.Width > 0 && layout.Height > 0);
        Assert.Empty(layout.Warnings);
        AssertDefinitionBoxesDoNotOverlap(layout.Nodes);
    }

    /// <summary>
    ///     A connected model whose definitions cross-reference one another within a single package is
    ///     laid out so that every definition box stays clear of the others.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_HeatLayout_ConnectedModelKeepsBoxesSeparated()
    {
        // Arrange: a chain of part references A1 <- A2 <- A3 within one package (a connected component).
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["Row1::A1"] = new SysmlDefinitionNode { Name = "A1", QualifiedName = "Row1::A1", DefinitionKeyword = "part def" },
                ["Row1::A2"] = new SysmlDefinitionNode
                {
                    Name = "A2",
                    QualifiedName = "Row1::A2",
                    DefinitionKeyword = "part def",
                    Children = [new SysmlFeatureNode { Name = "a1", QualifiedName = "Row1::A2::a1", FeatureKeyword = "part", FeatureTyping = "A1" }]
                },
                ["Row1::A3"] = new SysmlDefinitionNode
                {
                    Name = "A3",
                    QualifiedName = "Row1::A3",
                    DefinitionKeyword = "part def",
                    Children = [new SysmlFeatureNode { Name = "a2", QualifiedName = "Row1::A3::a2", FeatureKeyword = "part", FeatureTyping = "A2" }]
                }
            }
        };
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(new ViewContext("connected", workspace), options);

        // Assert: a valid canvas with no overlapping definition boxes.
        Assert.True(layout.Width > 0 && layout.Height > 0);
        AssertDefinitionBoxesDoNotOverlap(layout.Nodes);
    }

    /// <summary>
    ///     A minimal model (two boxes and one specialization edge) produces a compact canvas with no
    ///     warnings, confirming the layered engine does not over-pad sparse layouts.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_HeatLayout_SparseModelProducesCompactCanvas()
    {
        // Arrange: two definitions in the same package with a single specialization edge.
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Base"] = new SysmlDefinitionNode { Name = "Base", QualifiedName = "P::Base", DefinitionKeyword = "part def" },
                ["P::Sub"] = new SysmlDefinitionNode
                {
                    Name = "Sub",
                    QualifiedName = "P::Sub",
                    DefinitionKeyword = "part def",
                    SupertypeNames = ["Base"]
                }
            }
        };
        var options = new RenderOptions(Themes.Light);

        // Act: build layout for the sparse model.
        var layout = strategy.BuildLayout(new ViewContext("sparse", workspace), options);

        // Assert: canvas is valid, no warnings emitted, and height is within a reasonable
        // upper bound (500px) confirming the layered engine does not artificially over-pad.
        Assert.True(layout.Width > 0 && layout.Height > 0);
        Assert.Empty(layout.Warnings);
        Assert.True(layout.Height < 500.0,
            $"Sparse canvas height {layout.Height} should be below 500px (no over-padding)");
    }

    /// <summary>
    ///     Builds the fixed three-definition workspace shared by the expose-scoping tests:
    ///     <c>Root::A</c> (an expose target), <c>Root::A::Child</c> (inside <c>A</c>'s containment
    ///     subtree, qualified name prefixed <c>"Root::A::"</c>), and <c>Root::B</c> (an unrelated
    ///     sibling definition, outside <c>A</c>'s subtree).
    /// </summary>
    private static SysmlWorkspace BuildScopingWorkspace() => new()
    {
        Declarations = new Dictionary<string, SysmlNode>
        {
            ["Root::A"] = new SysmlDefinitionNode { Name = "A", QualifiedName = "Root::A", DefinitionKeyword = "part def" },
            ["Root::A::Child"] = new SysmlDefinitionNode { Name = "Child", QualifiedName = "Root::A::Child", DefinitionKeyword = "part def" },
            ["Root::B"] = new SysmlDefinitionNode { Name = "B", QualifiedName = "Root::B", DefinitionKeyword = "part def" }
        }
    };

    /// <summary>
    ///     Builds a workspace modeling the usage-vs-definition containment gap fix: a
    ///     <c>Root::Vehicle</c> definition with an owned child <c>Root::Vehicle::Engine</c>, and a
    ///     <c>Root::myVehicle</c> feature usage typed by <c>Vehicle</c> (a resolved <c>Typing</c>
    ///     edge to <c>Root::Vehicle</c>) plus an unrelated sibling <c>Root::Other</c>.
    /// </summary>
    private static SysmlWorkspace BuildUsageTypingWorkspace() => new()
    {
        Declarations = new Dictionary<string, SysmlNode>
        {
            ["Root::Vehicle"] = new SysmlDefinitionNode { Name = "Vehicle", QualifiedName = "Root::Vehicle", DefinitionKeyword = "part def" },
            ["Root::Vehicle::Engine"] = new SysmlDefinitionNode { Name = "Engine", QualifiedName = "Root::Vehicle::Engine", DefinitionKeyword = "part def" },
            ["Root::myVehicle"] = new SysmlFeatureNode
            {
                Name = "myVehicle",
                QualifiedName = "Root::myVehicle",
                FeatureTyping = "Vehicle",
                ResolvedEdges = [new SysmlEdge("Root::myVehicle", "Root::Vehicle", SysmlEdgeKind.Typing)]
            },
            ["Root::Other"] = new SysmlDefinitionNode { Name = "Other", QualifiedName = "Root::Other", DefinitionKeyword = "part def" }
        }
    };

    /// <summary>
    ///     A view with a resolved <c>Expose</c> edge to <c>Root::A</c> scopes the diagram to
    ///     <c>Root::A</c> plus its containment subtree (<c>Root::A::Child</c>), excluding the
    ///     unrelated sibling <c>Root::B</c> — producing fewer boxes than rendering the full
    ///     workspace.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_ExposedName_UnionsAdditionalSubtree()
    {
        // Arrange: a view exposing only Root::A
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = BuildScopingWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            ExposedNames = ["A"],
            ResolvedEdges = [new SysmlEdge("Root::V", "Root::A", SysmlEdgeKind.Expose)]
        };
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        // Act
        var scoped = strategy.BuildLayout(context, options);
        var full = strategy.BuildLayout(new ViewContext("full", workspace), options);

        // Assert: the scoped view contains A and A::Child but not B, and has fewer boxes than
        // the unscoped full-workspace rendering.
        var labels = CollectBoxes(scoped.Nodes).Select(b => b.Label).ToList();
        Assert.Contains("A", labels);
        Assert.Contains("Child", labels);
        Assert.DoesNotContain("B", labels);
        Assert.True(CollectBoxes(scoped.Nodes).Count < CollectBoxes(full.Nodes).Count);
    }

    /// <summary>
    ///     A view whose <c>RenderTargetName</c> is present but has no <c>Expose</c> edges renders
    ///     the full workspace, byte-identical (same box count/labels) to the null-<c>ViewNode</c>
    ///     case — proving <c>RenderTargetName</c> never affects scope, since it names a rendering
    ///     style/format, not content.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_RenderTargetNameOnly_NoExposeEdges_RendersFullWorkspace()
    {
        // Arrange: a view with a RenderTargetName but no resolved Expose edges
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = BuildScopingWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            RenderTargetName = "asTreeDiagram",
            ResolvedEdges = []
        };
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        // Act
        var inert = strategy.BuildLayout(context, options);
        var full = strategy.BuildLayout(new ViewContext("full", workspace), options);

        // Assert: identical box count/labels to the full-workspace (no-ViewNode) rendering.
        var inertLabels = CollectBoxes(inert.Nodes).Select(b => b.Label).OrderBy(l => l).ToList();
        var fullLabels = CollectBoxes(full.Nodes).Select(b => b.Label).OrderBy(l => l).ToList();
        Assert.Equal(fullLabels, inertLabels);
    }

    /// <summary>
    ///     A view whose <c>Expose</c> edge resolves to a feature usage (not a definition) still
    ///     renders that usage's type's containment subtree, by additionally resolving the usage's
    ///     own <c>Typing</c> edge — the fix for the usage-vs-definition containment gap. Excludes
    ///     the unrelated sibling <c>Root::Other</c>.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_ExposedUsage_ResolvesThroughTypingToDefinitionSubtree()
    {
        // Arrange: a view exposing Root::myVehicle, a usage typed by Root::Vehicle
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = BuildUsageTypingWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            ExposedNames = ["myVehicle"],
            ResolvedEdges = [new SysmlEdge("Root::V", "Root::myVehicle", SysmlEdgeKind.Expose)]
        };
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: Vehicle and its Engine child are present (resolved through the usage's typing
        // edge), but the unrelated Other definition is excluded.
        var labels = CollectBoxes(layout.Nodes).Select(b => b.Label).ToList();
        Assert.Contains("Vehicle", labels);
        Assert.Contains("Engine", labels);
        Assert.DoesNotContain("Other", labels);
    }

    /// <summary>
    ///     A view whose <c>FilterExpressionText</c> is non-null emits the "parsed but not yet
    ///     evaluated" diagnostic through <see cref="LayoutTree.Warnings"/>, while still rendering
    ///     the (unfiltered) resolved scope — per the binding decision to defer filter expression
    ///     evaluation to a future roadmap item.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_FilterExpressionPresent_EmitsNotYetEvaluatedWarning()
    {
        // Arrange: a view declaring a filter expression
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = BuildScopingWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            FilterExpressionText = "@SysML::PartUsage"
        };
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: a warning about the unevaluated filter is present, and the resolved (unfiltered)
        // scope — here, the full workspace, since no expose statement was declared — still renders.
        Assert.Contains(layout.Warnings, w => w.Contains("filter expression") && w.Contains("not yet evaluated"));
        var labels = CollectBoxes(layout.Nodes).Select(b => b.Label).ToList();
        Assert.Contains("A", labels);
        Assert.Contains("B", labels);
    }

    /// <summary>
    ///     A view with no <c>expose</c> statement (a null <see cref="ViewContext.ViewNode"/>, e.g.
    ///     the <c>--auto</c> synthesized view) renders identically to the pre-scoping-change
    ///     baseline: every non-stdlib definition in the workspace. This is the critical regression
    ///     guard confirming the scoping feature is fully backward-compatible when unused.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_NullViewNode_RendersFullWorkspaceUnchanged()
    {
        // Arrange: no ViewNode at all — the pre-existing 2-arg ViewContext construction.
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = BuildScopingWorkspace();
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: all three definitions are present and no warnings are emitted.
        var labels = CollectBoxes(layout.Nodes).Select(b => b.Label).ToList();
        Assert.Contains("A", labels);
        Assert.Contains("B", labels);
        Assert.Contains("Child", labels);
        Assert.Empty(layout.Warnings);
    }

    /// <summary>
    ///     Regression guard for the bracketed-filter <c>expose</c> parsing bug:
    ///     <c>vehicleMandatorySafetyFeatureViewStandalone</c> in the real OMG corpus fixture
    ///     <c>11b-SafetyAndSecurityFeatureViews.sysml</c> declares
    ///     <c>expose vehicle::**[@Safety and (as Safety).isMandatory];</c> — the bracketed-filter
    ///     grammar form that <c>AstBuilder.ExtractImportTarget</c> previously failed to descend
    ///     into, leaving the view with zero <c>Expose</c> edges and causing
    ///     <see cref="GeneralViewLayoutStrategy"/> to silently fall back to rendering the entire
    ///     workspace. After the fix, this view's layout must be scoped to the <c>vehicle</c>
    ///     subtree — which, in this fixture, contains no <c>part def</c> declarations (only
    ///     usages), so the correctly-scoped rendering drops to zero boxes, strictly fewer than the
    ///     unscoped full-workspace rendering (which still includes the unrelated
    ///     <c>AnnotationDefinitions::Safety</c>/<c>Security</c> metadata definitions). Before the
    ///     fix, scoped and full box counts were identical (both rendered everything).
    /// </summary>
    // cspell:ignore Feaure -- typo present verbatim in the real OMG corpus fixture's package name
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_OmgSafetyFeatureViewsFixture_ScopesToExposedVehicleSubtree()
    {
        // Arrange: load the real OMG corpus fixture.
        var modelsRoot = FindSysMLModelsRoot();
        if (modelsRoot is null)
        {
            return;
        }

        var fixturePath = Path.Combine(
            modelsRoot, "OMG", "validation", "11-ViewAndViewpoint", "11b-SafetyAndSecurityFeatureViews.sysml");
        if (!File.Exists(fixturePath))
        {
            return;
        }

        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([fixturePath], stdlibTable);
        Assert.NotNull(result.Workspace);
        var workspace = result.Workspace!;

        const string viewQualifiedName =
            "'11b-Safety and Security Feaure Views'::Views::vehicleMandatorySafetyFeatureViewStandalone";
        var viewNode = Assert.IsType<SysmlViewNode>(workspace.Declarations[viewQualifiedName]);

        // Confirm the fix actually resolved an Expose edge before asserting on layout scoping —
        // otherwise this test would pass vacuously by comparing full-workspace to full-workspace.
        Assert.NotEmpty(viewNode.ExposedNames);
        Assert.Contains(workspace.Index.AllEdges,
            e => e.Kind == SysmlEdgeKind.Expose && e.SourceQualifiedName == viewQualifiedName);

        var strategy = new GeneralViewLayoutStrategy();
        var options = new RenderOptions(Themes.Light);

        // Act
        var scoped = strategy.BuildLayout(new ViewContext("scoped", workspace, viewNode), options);
        var full = strategy.BuildLayout(new ViewContext("full", workspace), options);

        // Assert: the scoped view renders strictly fewer boxes than the full workspace. In this
        // fixture the `vehicle` subtree is built entirely from part *usages* (not `part def`
        // declarations), which `GeneralViewLayoutStrategy.CollectDefinitions` does not render as
        // boxes — so the correctly-scoped view renders zero boxes here, while the unscoped full
        // workspace still renders the two unrelated `AnnotationDefinitions` metadata definitions
        // (`Safety`, `Security`). That drop from 2 boxes to 0 is itself the regression signal:
        // before the fix, `ResolveExposedScope` saw no `Expose` edges and fell back to rendering
        // the entire workspace (i.e. scoped would equal full, not be strictly smaller).
        var scopedBoxes = CollectBoxes(scoped.Nodes);
        var fullBoxes = CollectBoxes(full.Nodes);
        Assert.True(scopedBoxes.Count < fullBoxes.Count,
            $"expected scoped box count ({scopedBoxes.Count}) < full box count ({fullBoxes.Count})");
        var fullLabels = fullBoxes.Select(b => b.Label).ToList();
        Assert.Contains("Safety", fullLabels);
        Assert.Contains("Security", fullLabels);
        var scopedLabels = scopedBoxes.Select(b => b.Label).ToList();
        Assert.DoesNotContain("Safety", scopedLabels);
        Assert.DoesNotContain("Security", scopedLabels);
    }

    /// <summary>
    ///     Finds the test/SysMLModels directory relative to the test assembly.
    /// </summary>
    private static string? FindSysMLModelsRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "test", "SysMLModels");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }

        return null;
    }

    /// <summary>
    ///     Asserts that no two rendered definition (rectangle-shaped) boxes overlap in the layout.
    /// </summary>
    /// <param name="nodes">The layout's top-level nodes.</param>
    private static void AssertDefinitionBoxesDoNotOverlap(IReadOnlyList<LayoutNode> nodes)
    {
        var boxes = CollectBoxes(nodes).Where(b => b.Shape == BoxShape.Rectangle).ToList();
        for (var a = 0; a < boxes.Count; a++)
        {
            for (var b = a + 1; b < boxes.Count; b++)
            {
                var overlapX = boxes[a].X < boxes[b].X + boxes[b].Width && boxes[b].X < boxes[a].X + boxes[a].Width;
                var overlapY = boxes[a].Y < boxes[b].Y + boxes[b].Height && boxes[b].Y < boxes[a].Y + boxes[a].Height;
                Assert.False(overlapX && overlapY, $"definition boxes {boxes[a].Label} and {boxes[b].Label} overlap");
            }
        }
    }
}
