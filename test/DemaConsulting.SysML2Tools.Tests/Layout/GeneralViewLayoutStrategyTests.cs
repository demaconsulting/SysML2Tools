// <copyright file="GeneralViewLayoutStrategyTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout;
using DemaConsulting.SysML2Tools.Layout.Internal;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Internal;

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
        var line = layout.Nodes.OfType<LayoutLine>().FirstOrDefault();
        Assert.NotNull(line);
        Assert.Equal(ArrowheadStyle.Open, line!.TargetArrowhead);
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
}
