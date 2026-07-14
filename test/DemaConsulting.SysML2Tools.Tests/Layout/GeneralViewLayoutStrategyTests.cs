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
    ///     Builds a workspace where <c>Sys::OperatorConsole</c> owns two nested definitions,
    ///     <c>Sys::OperatorConsole::DisplayPanel</c> and <c>Sys::OperatorConsole::CommsHandset</c>,
    ///     all inside package <c>Sys</c> — the fixture reproducing the real mission-control
    ///     gallery model's duplicate-box defect (Defect A).
    /// </summary>
    private static SysmlWorkspace BuildNestedDefinitionWorkspace() => new()
    {
        Declarations = new Dictionary<string, SysmlNode>
        {
            ["Sys::OperatorConsole"] = new SysmlDefinitionNode
            {
                Name = "OperatorConsole",
                QualifiedName = "Sys::OperatorConsole",
                DefinitionKeyword = "part def"
            },
            ["Sys::OperatorConsole::DisplayPanel"] = new SysmlDefinitionNode
            {
                Name = "DisplayPanel",
                QualifiedName = "Sys::OperatorConsole::DisplayPanel",
                DefinitionKeyword = "part def"
            },
            ["Sys::OperatorConsole::CommsHandset"] = new SysmlDefinitionNode
            {
                Name = "CommsHandset",
                QualifiedName = "Sys::OperatorConsole::CommsHandset",
                DefinitionKeyword = "part def"
            }
        }
    };

    /// <summary>
    ///     Unscoped: a definition that owns nested definitions renders exactly one box for
    ///     itself (regression guard against the sibling-duplicate-folder defect) with its nested
    ///     definitions placed as children of that single box, nested inside the still-present
    ///     package folder — not as a duplicate sibling folder.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_DefinitionOwningNestedDefinitions_RendersOneContainerBoxUnscoped()
    {
        // Arrange: OperatorConsole owns DisplayPanel and CommsHandset, all inside package Sys, unscoped
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = BuildNestedDefinitionWorkspace();
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: exactly one OperatorConsole box exists
        var allBoxes = CollectBoxes(layout.Nodes);
        var consoleBoxes = allBoxes.Where(b => b.Label == "OperatorConsole").ToList();
        Assert.Single(consoleBoxes);

        // Assert: the single OperatorConsole box's own children include DisplayPanel and CommsHandset
        var consoleChildLabels = CollectBoxes(consoleBoxes[0].Children).Select(b => b.Label).ToList();
        Assert.Contains("DisplayPanel", consoleChildLabels);
        Assert.Contains("CommsHandset", consoleChildLabels);

        // Assert: the Sys package folder still exists (unscoped preserves package folders), and its
        // own direct children include the single OperatorConsole box, not DisplayPanel/CommsHandset
        // directly.
        var folder = allBoxes.First(b => b.Shape == BoxShape.Folder && b.Label == "Sys");
        var folderChildLabels = folder.Children.OfType<LayoutBox>().Select(b => b.Label).ToList();
        Assert.Contains("OperatorConsole", folderChildLabels);
        Assert.DoesNotContain("DisplayPanel", folderChildLabels);
        Assert.DoesNotContain("CommsHandset", folderChildLabels);
    }

    /// <summary>
    ///     Scoped: the same nested-definition-owning fixture, but with a view exposing
    ///     <c>Sys::OperatorConsole::**</c> (whole-subtree recursion). Still renders exactly one
    ///     <c>OperatorConsole</c> box (no duplicate), its children still nested correctly, and —
    ///     combined with Defect B's bare-package folder suppression — no <c>Sys</c> folder appears
    ///     at all (its only admitted content's immediate parent, <c>OperatorConsole</c>, is itself
    ///     an admitted definition, so <c>OperatorConsole</c> is promoted directly to root).
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_DefinitionOwningNestedDefinitions_RendersOneContainerBoxScoped()
    {
        // Arrange: same workspace as the unscoped test, but exposing Sys::OperatorConsole::**
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = BuildNestedDefinitionWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Sys::V",
            ExposeMembers = [new ExposeMember("OperatorConsole", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("Sys::V", "Sys::OperatorConsole", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: exactly one OperatorConsole box exists, with DisplayPanel/CommsHandset nested
        // as its own children.
        var allBoxes = CollectBoxes(layout.Nodes);
        var consoleBoxes = allBoxes.Where(b => b.Label == "OperatorConsole").ToList();
        Assert.Single(consoleBoxes);
        var consoleChildLabels = CollectBoxes(consoleBoxes[0].Children).Select(b => b.Label).ToList();
        Assert.Contains("DisplayPanel", consoleChildLabels);
        Assert.Contains("CommsHandset", consoleChildLabels);

        // Assert: no Sys folder appears — OperatorConsole is promoted directly to root instead of
        // being wrapped in a folder for its bare-package ancestor.
        Assert.DoesNotContain(allBoxes, b => b.Shape == BoxShape.Folder);
    }

    /// <summary>
    ///     Scoped, isolating Defect B alone (no nested-definition-owning case involved): a view
    ///     exposing <c>Sys::*</c> (direct-children recursion) over a bare package <c>Sys</c>
    ///     containing plain sibling definitions renders no <see cref="BoxShape.Folder"/> box
    ///     anywhere, while the exposed definitions' boxes are present directly at the root.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_ExposedNamespaceChildren_BarePackageAncestor_NoFolderRendered()
    {
        // Arrange: bare package Sys with two sibling definitions, exposing Sys::* (direct children)
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["Sys::A"] = new SysmlDefinitionNode { Name = "A", QualifiedName = "Sys::A", DefinitionKeyword = "part def" },
                ["Sys::B"] = new SysmlDefinitionNode { Name = "B", QualifiedName = "Sys::B", DefinitionKeyword = "part def" }
            }
        };
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Sys::V",
            ExposeMembers = [new ExposeMember("Sys", null, ExposeRecursionKind.NamespaceDirectChildren)],
            ResolvedEdges = [new SysmlEdge("Sys::V", "Sys", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: no folder-shaped box exists anywhere in the result
        var allBoxes = CollectBoxes(layout.Nodes);
        Assert.DoesNotContain(allBoxes, b => b.Shape == BoxShape.Folder);

        // Assert: the exposed definitions' boxes are present directly under the root node list
        var rootLabels = CollectBoxes(layout.Nodes).Select(b => b.Label).ToList();
        Assert.Contains("A", rootLabels);
        Assert.Contains("B", rootLabels);
    }

    /// <summary>
    ///     Explicit regression guard: with no <c>expose</c>/no <see cref="SysmlViewNode"/>, an
    ///     ordinary bare-package case still renders a <see cref="BoxShape.Folder"/> box with the
    ///     expected package label and expected non-definition-container children, confirming
    ///     unscoped behavior is provably unchanged.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_Unscoped_StillRendersFullPackageFolderStructure()
    {
        // Arrange: two sibling definitions inside package Sys, unscoped
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

        // Assert: a Sys folder box still exists, with A and B nested directly as its own children
        var allBoxes = CollectBoxes(layout.Nodes);
        var folder = allBoxes.First(b => b.Shape == BoxShape.Folder);
        Assert.Equal("package", folder.Keyword);
        Assert.Equal("Sys", folder.Label);
        var folderChildLabels = CollectBoxes(folder.Children).Select(b => b.Label).ToList();
        Assert.Contains("A", folderChildLabels);
        Assert.Contains("B", folderChildLabels);
    }

    /// <summary>
    ///     Mirrors the real <c>BatterySubsystemView</c> gallery scenario directly: a single
    ///     <c>part def Battery</c> inside bare package <c>QuadcopterDrone</c>, exposed via
    ///     <c>expose Battery;</c> (exact match, single-target expose). No <see cref="BoxShape.Folder"/>
    ///     box exists in the result, and the <c>Battery</c> box is present directly at the root
    ///     level — the primary Defect B correctness guard.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_ExposedDefinitionInsideBarePackage_NoAncestorFolderRendered()
    {
        // Arrange: Battery inside bare package QuadcopterDrone, exposing Battery exactly
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["QuadcopterDrone::Battery"] = new SysmlDefinitionNode
                {
                    Name = "Battery",
                    QualifiedName = "QuadcopterDrone::Battery",
                    DefinitionKeyword = "part def"
                }
            }
        };
        var viewNode = new SysmlViewNode
        {
            Name = "BatterySubsystemView",
            QualifiedName = "QuadcopterDrone::BatterySubsystemView",
            ExposeMembers = [new ExposeMember("Battery", null, ExposeRecursionKind.MembershipExact)],
            ResolvedEdges = [new SysmlEdge("QuadcopterDrone::BatterySubsystemView", "QuadcopterDrone::Battery", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: no folder-shaped box exists, and Battery is present directly at the root level
        var allBoxes = CollectBoxes(layout.Nodes);
        Assert.DoesNotContain(allBoxes, b => b.Shape == BoxShape.Folder);
        var rootLabels = layout.Nodes.OfType<LayoutBox>().Select(b => b.Label).ToList();
        Assert.Contains("Battery", rootLabels);
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
    ///     A part def that owns a <c>ref</c>-typed feature emits a Dependency-shaped edge (dashed
    ///     line, open chevron) from the owning definition to the referenced type box — the obsolete
    ///     hollow-diamond membership notation was removed in favor of current OMG SysML v2 notation
    ///     for reference usages, sharing the same rendering as the public Dependency edge kind.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_ReferenceMembership_ProducesDependencyEdge()
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

        // Assert: a dashed open-chevron edge is emitted for a ref feature, and no hollow-diamond
        // edge is emitted anywhere (the obsolete notation is fully retired).
        var lines = CollectLines(layout.Nodes);
        var dependencyEdge = lines.FirstOrDefault(l => l.TargetEnd == EndMarkerStyle.OpenChevron && l.LineStyle == LineStyle.Dashed);
        Assert.NotNull(dependencyEdge);
        Assert.DoesNotContain(lines, l => l.TargetEnd == EndMarkerStyle.HollowDiamond);
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
    ///     anywhere in the supertype chain) produces no redefinition edge and does not throw.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_UnresolvableRedefinition_ProducesNoEdge()
    {
        // Arrange: Vehicle redefines a bare name that does not exist anywhere in scope.
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
    ///     A bare-name redefinition whose declaring ancestor is two supertype hops away
    ///     (<c>Mid :> Parent :> GrandParent</c>, with <c>GrandParent</c> declaring the redefined
    ///     member) produces a hollow-triangle-crossbar edge targeting the actual declaring
    ///     ancestor (<c>GrandParent</c>), not the immediate supertype (<c>Parent</c>) — proving the
    ///     bare-name walk is transitive, not limited to a single hop.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_TransitiveBareNameRedefinition_ProducesHollowTriangleCrossbarEdgeToDeclaringAncestor()
    {
        // Arrange: GrandParent declares "feat"; Parent specializes GrandParent with no members of
        // its own; Mid specializes Parent and redefines "feat" by bare name.
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::GrandParent"] = new SysmlDefinitionNode
                {
                    Name = "GrandParent",
                    QualifiedName = "P::GrandParent",
                    DefinitionKeyword = "part def",
                    Children =
                    [
                        new SysmlFeatureNode { Name = "feat", QualifiedName = "P::GrandParent::feat", FeatureKeyword = "attribute", FeatureTyping = "Real" }
                    ]
                },
                ["P::Parent"] = new SysmlDefinitionNode
                {
                    Name = "Parent",
                    QualifiedName = "P::Parent",
                    DefinitionKeyword = "part def",
                    SupertypeNames = ["GrandParent"],
                },
                ["P::Mid"] = new SysmlDefinitionNode
                {
                    Name = "Mid",
                    QualifiedName = "P::Mid",
                    DefinitionKeyword = "part def",
                    SupertypeNames = ["Parent"],
                    Children =
                    [
                        new SysmlFeatureNode { Name = "subFeat", QualifiedName = "P::Mid::subFeat", FeatureKeyword = "attribute", FeatureTyping = "Real", RedefinedFeatureName = "feat" }
                    ]
                }
            }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: the crossbar edge targets GrandParent (the actual declaring ancestor), and there
        // is exactly one such edge (not one for each hop of the chain).
        var redefinitionEdges = CollectLines(layout.Nodes)
            .Where(l => l.TargetEnd == EndMarkerStyle.HollowTriangleCrossbar)
            .ToList();
        Assert.Single(redefinitionEdges);
        Assert.Equal(LineStyle.Solid, redefinitionEdges[0].LineStyle);
    }

    /// <summary>
    ///     A genuinely self-referential redefinition — a definition whose own supertype chain
    ///     cycles back to itself, such that the bare-name walk resolves the redefined member's
    ///     owner back to the very definition doing the redefining — produces no redefinition edge
    ///     and does not throw. This is distinct from
    ///     <see cref="GeneralViewLayoutStrategy_BuildLayout_UnresolvableRedefinition_ProducesNoEdge"/>,
    ///     which covers a name that cannot be found anywhere, not a name that resolves back to the
    ///     definition itself.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_SelfReferentialRedefinition_ProducesNoEdge()
    {
        // Arrange: Standalone lists itself as its own supertype (a self-cycle), and redefines its
        // own "otherFeat" member by bare name — so the walk resolves the owner back to Standalone.
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Standalone"] = new SysmlDefinitionNode
                {
                    Name = "Standalone",
                    QualifiedName = "P::Standalone",
                    DefinitionKeyword = "part def",
                    SupertypeNames = ["Standalone"],
                    Children =
                    [
                        new SysmlFeatureNode { Name = "selfFeat", QualifiedName = "P::Standalone::selfFeat", FeatureKeyword = "attribute", FeatureTyping = "Real", RedefinedFeatureName = "otherFeat" },
                        new SysmlFeatureNode { Name = "otherFeat", QualifiedName = "P::Standalone::otherFeat", FeatureKeyword = "attribute", FeatureTyping = "Real" }
                    ]
                }
            }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act: laying out must not throw despite the self-referential supertype cycle.
        var layout = strategy.BuildLayout(context, options);

        // Assert: no hollow-triangle-crossbar edge is produced, since the resolved owner is
        // Standalone itself.
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
            ExposeMembers = [new ExposeMember("A", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("Root::V", "Root::A", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
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
            ExposeMembers = [new ExposeMember("myVehicle", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("Root::V", "Root::myVehicle", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
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
    ///     A view whose <c>FilterExpressionText</c> uses a construct outside the Phase 1 subset
    ///     emits a "could not be evaluated" diagnostic through <see cref="LayoutTree.Warnings"/>,
    ///     while still rendering the (unfiltered) resolved scope — the documented fallback
    ///     behavior for a filter expression that fails to parse/evaluate (see ROADMAP.md).
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_FilterExpressionPresent_EmitsNotYetEvaluatedWarning()
    {
        // Arrange: a view declaring a filter expression outside the Phase 1 construct subset
        // (arithmetic addition, which has no corresponding FilterExpression node).
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = BuildScopingWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            FilterExpressionText = "1 + 2"
        };
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: a warning about the unevaluated filter is present, and the resolved (unfiltered)
        // scope — here, the full workspace, since no expose statement was declared — still renders.
        Assert.Contains(layout.Warnings, w => w.Contains("filter expression") && w.Contains("could not be evaluated"));
        var labels = CollectBoxes(layout.Nodes).Select(b => b.Label).ToList();
        Assert.Contains("A", labels);
        Assert.Contains("B", labels);
    }

    /// <summary>
    ///     A view whose <c>FilterExpressionText</c> is a Phase 1 classification-test expression
    ///     that matches no candidate's metadata annotations narrows the rendered scope to nothing,
    ///     confirming standalone <c>filter</c> evaluation actually applies (as opposed to the
    ///     legacy "parsed but not evaluated" behavior).
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_FilterExpressionMatchesNothing_RendersEmpty()
    {
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = BuildScopingWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            FilterExpressionText = "@NoSuchMetadataType"
        };
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        Assert.Empty(layout.Warnings);
        Assert.Empty(CollectBoxes(layout.Nodes));
    }

    /// <summary>
    ///     A workspace of mixed usage kinds (part/requirement/other), reproducing the shape of the
    ///     OMG's <c>42.Views/ViewsExample.sysml</c> corpus example (a <c>filter</c> statement
    ///     dominated by usage-level <c>@SysML::PartUsage</c> classification tests over a model
    ///     with no <c>part def</c> declarations at all).
    /// </summary>
    private static SysmlWorkspace BuildMixedUsageKindWorkspace() => new()
    {
        Declarations = new Dictionary<string, SysmlNode>
        {
            ["Root::myPart"] = new SysmlFeatureNode { Name = "myPart", QualifiedName = "Root::myPart", FeatureKeyword = "part" },
            ["Root::myRequirement"] = new SysmlFeatureNode { Name = "myRequirement", QualifiedName = "Root::myRequirement", FeatureKeyword = "requirement" },
            ["Root::myAttribute"] = new SysmlFeatureNode { Name = "myAttribute", QualifiedName = "Root::myAttribute", FeatureKeyword = "attribute" }
        }
    };

    /// <summary>
    ///     Regression test for the OMG's own canonical <c>42.Views/ViewsExample.sysml</c> corpus
    ///     pattern (see <c>ROADMAP.md</c>'s Phase 2d visual gate): a standalone
    ///     <c>filter @SysML::PartUsage;</c> statement, with no <c>expose</c>, against a model with
    ///     mixed part/requirement/attribute usages and no <c>part def</c> declarations at all,
    ///     renders only the <c>PartUsage</c> element(s) — not empty, as it did before Phase 2d.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_QualifiedPartUsageFilter_RendersOnlyPartUsages()
    {
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = BuildMixedUsageKindWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            FilterExpressionText = "@SysML::PartUsage"
        };
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        Assert.Empty(layout.Warnings);
        var labels = CollectBoxes(layout.Nodes).Select(b => b.Label).ToList();
        Assert.Contains("myPart", labels);
        Assert.DoesNotContain("myRequirement", labels);
        Assert.DoesNotContain("myAttribute", labels);
    }

    /// <summary>
    ///     The bare-spelling variant of the above (<c>filter @PartUsage;</c>) renders identically,
    ///     confirming both the bare and <c>SysML::</c>-qualified metaclass-name spellings work.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_BarePartUsageFilter_RendersOnlyPartUsages()
    {
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = BuildMixedUsageKindWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            FilterExpressionText = "@PartUsage"
        };
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        Assert.Empty(layout.Warnings);
        var labels = CollectBoxes(layout.Nodes).Select(b => b.Label).ToList();
        Assert.Contains("myPart", labels);
        Assert.DoesNotContain("myRequirement", labels);
        Assert.DoesNotContain("myAttribute", labels);
    }

    /// <summary>
    ///     A view with no <c>filter</c>/<c>expose</c> at all now renders usage-level candidates too
    ///     (Phase 2d's <c>CollectDefinitions</c> widening), alongside pre-existing definitions —
    ///     confirming the widened candidate set is also the default (unfiltered) render behavior,
    ///     not merely a filter-narrowing behavior.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_NoFilter_RendersUsageLevelCandidatesToo()
    {
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = BuildMixedUsageKindWorkspace();
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var labels = CollectBoxes(layout.Nodes).Select(b => b.Label).ToList();
        Assert.Contains("myPart", labels);
        Assert.Contains("myRequirement", labels);
        Assert.Contains("myAttribute", labels);
    }

    /// <summary>
    ///     Retry-1 regression fix: a usage nested directly inside an independently-rendered
    ///     definition must not also render as its own standalone box — that would duplicate the
    ///     compartment row the usage already occupies inside the definition's box. This is the
    ///     direct, minimal reproduction of the quality-reported 21 → 47 box-count regression on
    ///     <c>docs/gallery/models/01-drone-general.sysml</c>'s <c>DroneGeneralView</c> (see the
    ///     dedicated gallery-corpus regression guard test below for the full-scale case).
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_NoFilter_ExcludesUsageNestedInsideRenderedDefinition()
    {
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["Root::Drone"] = new SysmlDefinitionNode
                {
                    Name = "Drone",
                    QualifiedName = "Root::Drone",
                    DefinitionKeyword = "part def",
                    Children = [new SysmlFeatureNode { Name = "airframe", QualifiedName = "Root::Drone::airframe", FeatureKeyword = "part", FeatureTyping = "Frame" }]
                },
                // A real workspace (built by WorkspaceLoader) registers every named nested
                // declaration under its own qualified-name key too, not merely as a Children entry
                // of its owner — reproducing that shape here is what actually exercises
                // CollectDefinitions's usage-level widening for this nested usage.
                ["Root::Drone::airframe"] = new SysmlFeatureNode { Name = "airframe", QualifiedName = "Root::Drone::airframe", FeatureKeyword = "part", FeatureTyping = "Frame" }
            }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var rectangles = CollectBoxes(layout.Nodes).Where(b => b.Shape == BoxShape.Rectangle).ToList();
        Assert.Single(rectangles);
        Assert.Equal("Drone", rectangles[0].Label);
    }

    /// <summary>
    ///     Retry-1 regression fix: a nested usage whose immediate parent is excluded from the final
    ///     rendered set by a metaclass filter (rather than by scope) must still render as its own
    ///     standalone box — proving <c>RemoveRedundantNestedUsages</c> runs after (not before)
    ///     standalone filter narrowing, so it only removes usages whose parent survived the filter.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_MetaclassFilter_KeepsNestedUsageWhenParentExcluded()
    {
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["Root::Container"] = new SysmlFeatureNode
                {
                    Name = "Container",
                    QualifiedName = "Root::Container",
                    FeatureKeyword = "requirement",
                    Children = [new SysmlFeatureNode { Name = "child", QualifiedName = "Root::Container::child", FeatureKeyword = "part" }]
                },
                ["Root::Container::child"] = new SysmlFeatureNode { Name = "child", QualifiedName = "Root::Container::child", FeatureKeyword = "part" }
            }
        };
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            FilterExpressionText = "@SysML::PartUsage"
        };
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        // "Container" may still appear as a package-folder label — GroupByPackage treats any
        // qualified-name prefix as a folder path regardless of whether that prefix belongs to a
        // package or an excluded definition/usage — but it must not appear as its own rendered
        // rectangle box (which would mean the excluded metaclass-filtered-out usage was rendered
        // after all). "child" must render as its own rectangle box.
        var rectangleLabels = CollectBoxes(layout.Nodes)
            .Where(b => b.Shape == BoxShape.Rectangle)
            .Select(b => b.Label)
            .ToList();
        Assert.DoesNotContain("Container", rectangleLabels);
        Assert.Contains("child", rectangleLabels);
    }

    /// <summary>
    ///     Retry-2 regression fix: a usage nested two or more levels deep (e.g.
    ///     <c>part def A { part b { part c; } }</c>) must not be silently dropped when its
    ///     immediate parent is itself excluded as a redundant nested usage. Since <c>b</c> is
    ///     excluded (its parent <c>A</c> is rendered), <c>b</c> no longer renders anywhere and its
    ///     compartment can no longer show <c>c</c> — so <c>c</c> must survive as its own standalone
    ///     box rather than vanishing entirely (the single-pass, pre-dedup-snapshot bug the prior
    ///     quality re-validation found).
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_NoFilter_RendersDeeplyNestedGrandchildUsageWhenIntermediateParentExcluded()
    {
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["Root::A"] = new SysmlDefinitionNode
                {
                    Name = "A",
                    QualifiedName = "Root::A",
                    DefinitionKeyword = "part def",
                    Children = [new SysmlFeatureNode { Name = "b", QualifiedName = "Root::A::b", FeatureKeyword = "part" }]
                },
                // A real workspace (built by WorkspaceLoader) registers every named nested
                // declaration under its own qualified-name key too, not merely as a Children entry
                // of its owner — reproducing that shape here is what actually exercises
                // CollectDefinitions's usage-level widening for both nested usages.
                ["Root::A::b"] = new SysmlFeatureNode
                {
                    Name = "b",
                    QualifiedName = "Root::A::b",
                    FeatureKeyword = "part",
                    Children = [new SysmlFeatureNode { Name = "c", QualifiedName = "Root::A::b::c", FeatureKeyword = "part" }]
                },
                ["Root::A::b::c"] = new SysmlFeatureNode { Name = "c", QualifiedName = "Root::A::b::c", FeatureKeyword = "part" }
            }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var rectangles = CollectBoxes(layout.Nodes).Where(b => b.Shape == BoxShape.Rectangle).ToList();
        var rectangleLabels = rectangles.Select(b => b.Label).ToList();
        Assert.Equal(2, rectangles.Count);
        Assert.Contains("A", rectangleLabels);
        Assert.DoesNotContain("b", rectangleLabels);
        Assert.Contains("c", rectangleLabels);
    }

    /// <summary>
    ///     Real-corpus regression guard for the quality-reported 21 → 47 box-count regression: the
    ///     project's own checked-in gallery example <c>docs/gallery/models/01-drone-general.sysml</c>
    ///     must render exactly the 21 rectangle-shaped definition boxes matching the currently
    ///     checked-in <c>docs/gallery/svg/DroneGeneralView.svg</c>'s <c>&lt;rect&gt;</c> count
    ///     (independently re-counted via <c>Select-String -Pattern '&lt;rect'</c>) when laid out
    ///     through the actual <see cref="GeneralViewLayoutStrategy"/>. Only <see cref="BoxShape.Rectangle"/>
    ///     boxes are counted (excluding the single package folder and the Battery definition's
    ///     documentation-annotation note box, neither of which is drawn as a checked-in <c>&lt;rect&gt;</c>
    ///     element), matching the checked-in SVG's own count basis. This converts the quality
    ///     agent's one-off manual <c>git stash</c> empirical check into a standing, automated test.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_DroneGalleryModel_RendersExactly21BoxesMatchingCheckedInSvg()
    {
        var galleryModelsRoot = FindGalleryModelsRoot();
        if (galleryModelsRoot is null)
        {
            return;
        }

        var modelPath = Path.Combine(galleryModelsRoot, "01-drone-general.sysml");
        if (!File.Exists(modelPath))
        {
            return;
        }

        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([modelPath], stdlibTable);
        Assert.NotNull(result.Workspace);
        var workspace = result.Workspace!;

        const string viewQualifiedName = "QuadcopterDrone::DroneGeneralView";
        var viewNode = Assert.IsType<SysmlViewNode>(workspace.Declarations[viewQualifiedName]);

        var strategy = new GeneralViewLayoutStrategy();
        var options = new RenderOptions(Themes.Light);
        var layout = strategy.BuildLayout(new ViewContext("v", workspace, viewNode), options);

        var boxes = CollectBoxes(layout.Nodes).Where(b => b.Shape == BoxShape.Rectangle).ToList();
        Assert.Equal(21, boxes.Count);
    }

    /// <summary>
    ///     Finds the repository's <c>docs/gallery/models</c> directory relative to the test
    ///     assembly, mirroring <see cref="FindSysMLModelsRoot"/>'s upward path-walk convention.
    /// </summary>
    private static string? FindGalleryModelsRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "docs", "gallery", "models");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }

        return null;
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
    ///     subtree. Since Phase 2d (see <c>ROADMAP.md</c>'s "View <c>filter [&lt;expr&gt;];</c>
    ///     expression evaluation" section), <see cref="GeneralViewLayoutStrategy"/>'s internal
    ///     candidate collection admits usage-level candidates too, so the `vehicle` subtree's part
    ///     *usages* (this
    ///     fixture declares no `part def`) render as boxes: the bracket filter
    ///     <c>@Safety and (as Safety).isMandatory</c> narrows those usages to exactly the ones
    ///     carrying a mandatory <c>@Safety</c> annotation (<c>seatBelt</c>, <c>bumper</c>), while
    ///     the unrelated <c>AnnotationDefinitions::Safety</c>/<c>Security</c> metadata definitions
    ///     — outside the <c>vehicle</c> subtree entirely — remain excluded from both renderings'
    ///     scoped result. Before the Phase 2d fix, the correctly-scoped rendering dropped to zero
    ///     boxes (usages were not renderable candidates at all).
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
        Assert.NotEmpty(viewNode.GetExposedNames());
        Assert.Contains(workspace.Index.AllEdges,
            e => e.Kind == SysmlEdgeKind.Expose && e.SourceQualifiedName == viewQualifiedName);

        var strategy = new GeneralViewLayoutStrategy();
        var options = new RenderOptions(Themes.Light);

        // Act
        var scoped = strategy.BuildLayout(new ViewContext("scoped", workspace, viewNode), options);
        var full = strategy.BuildLayout(new ViewContext("full", workspace), options);

        // Assert: the scoped view renders a non-empty subset of the vehicle's mandatory-safety
        // part usages, strictly fewer than the full workspace, and excludes the unrelated
        // AnnotationDefinitions metadata definitions from both the scoped subset and (by name)
        // from being conflated with the vehicle's own usages.
        var scopedBoxes = CollectBoxes(scoped.Nodes);
        var fullBoxes = CollectBoxes(full.Nodes);
        Assert.NotEmpty(scopedBoxes);
        Assert.True(scopedBoxes.Count < fullBoxes.Count,
            $"expected scoped box count ({scopedBoxes.Count}) < full box count ({fullBoxes.Count})");
        var fullLabels = fullBoxes.Select(b => b.Label).ToList();
        Assert.Contains("Safety", fullLabels);
        Assert.Contains("Security", fullLabels);
        var scopedLabels = scopedBoxes.Select(b => b.Label).ToList();
        Assert.DoesNotContain("Safety", scopedLabels);
        Assert.DoesNotContain("Security", scopedLabels);
        Assert.Contains("seatBelt", scopedLabels);
        Assert.Contains("bumper", scopedLabels);
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

    /// <summary>
    ///     A resolved <see cref="SysmlEdgeKind.Connect"/> edge between two sibling ports/parts of
    ///     different types (each nested inside the same enclosing definition, the dominant
    ///     real-world shape) emits a solid, unmarked line between the two distinct owning boxes —
    ///     confirming <c>ResolveOwningBox</c>'s shortest-typed-feature-prefix walk correctly maps
    ///     each dotted-chain endpoint to a different box rather than self-looping back to the
    ///     shared enclosing definition.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_Connect_DifferentOwningTypes_ProducesUnmarkedSolidEdge()
    {
        // Arrange: Drone owns "controller" (typed FlightController) and "battery" (typed Battery),
        // each with a nested feature ("power"/"output"); a Connect edge links the two nested chains.
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::FlightController"] = new SysmlDefinitionNode { Name = "FlightController", QualifiedName = "P::FlightController", DefinitionKeyword = "part def" },
                ["P::Battery"] = new SysmlDefinitionNode { Name = "Battery", QualifiedName = "P::Battery", DefinitionKeyword = "part def" },
                ["P::Drone"] = new SysmlDefinitionNode
                {
                    Name = "Drone",
                    QualifiedName = "P::Drone",
                    DefinitionKeyword = "part def",
                    Children =
                    [
                        new SysmlFeatureNode { Name = "controller", QualifiedName = "P::Drone::controller", FeatureKeyword = "part", FeatureTyping = "FlightController" },
                        new SysmlFeatureNode { Name = "battery", QualifiedName = "P::Drone::battery", FeatureKeyword = "part", FeatureTyping = "Battery" }
                    ]
                },
                ["P::Drone::controller"] = new SysmlFeatureNode
                {
                    Name = "controller",
                    QualifiedName = "P::Drone::controller",
                    FeatureKeyword = "part",
                    FeatureTyping = "FlightController",
                    ResolvedEdges = [new SysmlEdge("P::Drone::controller", "P::FlightController", SysmlEdgeKind.Typing)]
                },
                ["P::Drone::battery"] = new SysmlFeatureNode
                {
                    Name = "battery",
                    QualifiedName = "P::Drone::battery",
                    FeatureKeyword = "part",
                    FeatureTyping = "Battery",
                    ResolvedEdges = [new SysmlEdge("P::Drone::battery", "P::Battery", SysmlEdgeKind.Typing)]
                }
            },
            Index = new SemanticIndex(
            [
                new SysmlEdge("P::Drone::controller::power", "P::Drone::battery::output", SysmlEdgeKind.Connect)
            ])
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: a solid, unmarked line exists linking the FlightController and Battery boxes.
        var lines = CollectLines(layout.Nodes);
        var connectEdge = lines.FirstOrDefault(l => l.TargetEnd == EndMarkerStyle.None && l.LineStyle == LineStyle.Solid && l.MidpointLabel is null);
        Assert.NotNull(connectEdge);
    }

    /// <summary>
    ///     A <c>connect</c> edge whose two dotted-chain endpoints resolve to the <em>same</em>
    ///     owning box (e.g. two features of the same enclosing definition) produces no edge — the
    ///     self-loop guard that every other edge kind in this unit already applies.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_Connect_SameOwningType_ProducesNoEdge()
    {
        // Arrange: Vehicle owns two features both typed as Port (the same owning box).
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Port"] = new SysmlDefinitionNode { Name = "Port", QualifiedName = "P::Port", DefinitionKeyword = "port def" },
                ["P::Vehicle"] = new SysmlDefinitionNode
                {
                    Name = "Vehicle",
                    QualifiedName = "P::Vehicle",
                    DefinitionKeyword = "part def"
                },
                ["P::Vehicle::portA"] = new SysmlFeatureNode
                {
                    Name = "portA",
                    QualifiedName = "P::Vehicle::portA",
                    FeatureKeyword = "port",
                    FeatureTyping = "Port",
                    ResolvedEdges = [new SysmlEdge("P::Vehicle::portA", "P::Port", SysmlEdgeKind.Typing)]
                },
                ["P::Vehicle::portB"] = new SysmlFeatureNode
                {
                    Name = "portB",
                    QualifiedName = "P::Vehicle::portB",
                    FeatureKeyword = "port",
                    FeatureTyping = "Port",
                    ResolvedEdges = [new SysmlEdge("P::Vehicle::portB", "P::Port", SysmlEdgeKind.Typing)]
                }
            },
            Index = new SemanticIndex(
            [
                new SysmlEdge("P::Vehicle::portA::sig", "P::Vehicle::portB::sig", SysmlEdgeKind.Connect)
            ])
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: no Connect-shaped (unmarked solid) edge is emitted between Vehicle and itself.
        var lines = CollectLines(layout.Nodes);
        Assert.DoesNotContain(lines, l => l.TargetEnd == EndMarkerStyle.None && l.LineStyle == LineStyle.Solid);

        // Assert: the drop is surfaced as a visible warning (defense-in-depth diagnostic) rather
        // than silently discarded.
        Assert.Contains(layout.Warnings, w => w.Contains("Connect", StringComparison.Ordinal) &&
            w.Contains("P::Vehicle::portA::sig", StringComparison.Ordinal) &&
            w.Contains("P::Vehicle::portB::sig", StringComparison.Ordinal));
    }

    /// <summary>
    ///     End-to-end regression guard for the dominant real-world <c>connect</c> shape: two
    ///     sibling features (ports) declared directly in their owning <c>part def</c>s, referenced
    ///     from an enclosing part via bare <c>part</c> usages with no per-instance nested
    ///     redeclaration. This test runs the real <see cref="WorkspaceLoader"/> (not a synthetic
    ///     <see cref="SysmlWorkspace"/>) and feeds the resulting workspace into the real
    ///     <see cref="GeneralViewLayoutStrategy.BuildLayout"/>, proving the fix to
    ///     <c>ReferenceResolver.TryResolveFeatureChain</c>'s instance-path preservation actually
    ///     reaches the rendering pipeline end to end for this shape — not just via hand-built
    ///     fixtures that assume already-correct resolver output.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ConnectDominantShape_RealWorkspaceLoader_ProducesDistinctBoxes()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    part def PowerPort;
                    part def FlightController {
                        port power : PowerPort;
                    }
                    part def Battery {
                        port output : PowerPort;
                    }
                    part def Drone {
                        part controller : FlightController;
                        part battery : Battery;
                        connect controller.power to battery.output;
                    }
                }
                """, TestContext.Current.CancellationToken);

            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);
            Assert.NotNull(result.Workspace);
            var workspace = result.Workspace!;

            var strategy = new GeneralViewLayoutStrategy();
            var options = new RenderOptions(Themes.Light);
            var context = new ViewContext("v", workspace);

            // Act
            var layout = strategy.BuildLayout(context, options);

            // Assert: a solid, unmarked Connect edge exists between the FlightController and
            // Battery boxes, and no dropped-edge warning is emitted for it.
            var lines = CollectLines(layout.Nodes);
            var connectEdge = lines.FirstOrDefault(l =>
                l.TargetEnd == EndMarkerStyle.None && l.LineStyle == LineStyle.Solid && l.MidpointLabel is null);
            Assert.NotNull(connectEdge);

            var boxes = CollectBoxes(layout.Nodes);
            var boxLabels = boxes.Select(b => b.Label).ToList();
            Assert.Contains("FlightController", boxLabels);
            Assert.Contains("Battery", boxLabels);
            Assert.DoesNotContain(layout.Warnings, w => w.Contains("Connect", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A resolved <see cref="SysmlEdgeKind.Allocate"/> edge between two definitions emits a
    ///     dashed, open-chevron edge carrying the <c>«allocate»</c> midpoint label.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_Allocate_ProducesDashedChevronEdgeWithLabel()
    {
        // Arrange: an Allocate edge from FlightTimeRequirement to Battery.
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::FlightTimeRequirement"] = new SysmlDefinitionNode { Name = "FlightTimeRequirement", QualifiedName = "P::FlightTimeRequirement", DefinitionKeyword = "requirement def" },
                ["P::Battery"] = new SysmlDefinitionNode { Name = "Battery", QualifiedName = "P::Battery", DefinitionKeyword = "part def" }
            },
            Index = new SemanticIndex(
            [
                new SysmlEdge("P::FlightTimeRequirement", "P::Battery", SysmlEdgeKind.Allocate)
            ])
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: a dashed open-chevron edge with the "«allocate»" label is emitted.
        var lines = CollectLines(layout.Nodes);
        var allocateEdge = lines.FirstOrDefault(l => l.TargetEnd == EndMarkerStyle.OpenChevron && l.LineStyle == LineStyle.Dashed && l.MidpointLabel == "«allocate»");
        Assert.NotNull(allocateEdge);
    }

    /// <summary>
    ///     A resolved standalone <see cref="SysmlEdgeKind.Dependency"/> edge between two
    ///     definitions produces the same dashed, open-chevron rendering as the <c>ref</c>-keyword
    ///     fix (<see cref="GeneralViewLayoutStrategy_BuildLayout_ReferenceMembership_ProducesDependencyEdge"/>),
    ///     confirming both sources share one visual identity.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_Dependency_ProducesDashedChevronEdge()
    {
        // Arrange: a standalone dependency from FlightController to Battery.
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::FlightController"] = new SysmlDefinitionNode { Name = "FlightController", QualifiedName = "P::FlightController", DefinitionKeyword = "part def" },
                ["P::Battery"] = new SysmlDefinitionNode { Name = "Battery", QualifiedName = "P::Battery", DefinitionKeyword = "part def" }
            },
            Index = new SemanticIndex(
            [
                new SysmlEdge("P::FlightController", "P::Battery", SysmlEdgeKind.Dependency)
            ])
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: a dashed open-chevron edge (no label) is emitted.
        var lines = CollectLines(layout.Nodes);
        var dependencyEdge = lines.FirstOrDefault(l => l.TargetEnd == EndMarkerStyle.OpenChevron && l.LineStyle == LineStyle.Dashed && l.MidpointLabel is null);
        Assert.NotNull(dependencyEdge);
    }

    /// <summary>
    ///     A resolved <see cref="SysmlEdgeKind.Binding"/> edge between two dotted feature chains
    ///     resolves through <c>ResolveOwningBox</c> the same way <c>Connect</c> does, and emits a
    ///     solid, unmarked edge carrying the <c>=</c> midpoint label distinguishing it from Connect.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_Binding_ProducesSolidEdgeWithEqualsLabel()
    {
        // Arrange: Vehicle owns "engine" (typed Engine) and "gauge" (typed Gauge); a Binding links
        // a feature nested under each.
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Engine"] = new SysmlDefinitionNode { Name = "Engine", QualifiedName = "P::Engine", DefinitionKeyword = "part def" },
                ["P::Gauge"] = new SysmlDefinitionNode { Name = "Gauge", QualifiedName = "P::Gauge", DefinitionKeyword = "part def" },
                ["P::Vehicle"] = new SysmlDefinitionNode { Name = "Vehicle", QualifiedName = "P::Vehicle", DefinitionKeyword = "part def" },
                ["P::Vehicle::engine"] = new SysmlFeatureNode
                {
                    Name = "engine",
                    QualifiedName = "P::Vehicle::engine",
                    FeatureKeyword = "part",
                    FeatureTyping = "Engine",
                    ResolvedEdges = [new SysmlEdge("P::Vehicle::engine", "P::Engine", SysmlEdgeKind.Typing)]
                },
                ["P::Vehicle::gauge"] = new SysmlFeatureNode
                {
                    Name = "gauge",
                    QualifiedName = "P::Vehicle::gauge",
                    FeatureKeyword = "part",
                    FeatureTyping = "Gauge",
                    ResolvedEdges = [new SysmlEdge("P::Vehicle::gauge", "P::Gauge", SysmlEdgeKind.Typing)]
                }
            },
            Index = new SemanticIndex(
            [
                new SysmlEdge("P::Vehicle::engine::rpm", "P::Vehicle::gauge::value", SysmlEdgeKind.Binding)
            ])
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: a solid, unmarked edge with the "=" label is emitted.
        var lines = CollectLines(layout.Nodes);
        var bindingEdge = lines.FirstOrDefault(l => l.TargetEnd == EndMarkerStyle.None && l.LineStyle == LineStyle.Solid && l.MidpointLabel == "=");
        Assert.NotNull(bindingEdge);
    }

    /// <summary>
    ///     A subtype narrowing an inherited feature via <c>subsets</c> (e.g.
    ///     <c>part frontMotors : Motor[2] subsets motors;</c> where <c>motors</c> is declared on
    ///     the supertype) emits a dashed hollow-triangle edge from the subtype to the ancestor
    ///     definition that declares the subsetted feature — reusing the same
    ///     <c>ResolveRedefinitionOwner</c> ancestor-chain walk as Redefinition, but rendered dashed
    ///     to distinguish it from solid Specialization/Redefinition edges.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_Subsetting_CrossesSpecializationBoundary_ProducesDashedHollowTriangleEdge()
    {
        // Arrange: Drone declares "motors"; RacingDrone specializes Drone and subsets "motors" via
        // a narrower "frontMotors" feature.
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Motor"] = new SysmlDefinitionNode { Name = "Motor", QualifiedName = "P::Motor", DefinitionKeyword = "part def" },
                ["P::Drone"] = new SysmlDefinitionNode
                {
                    Name = "Drone",
                    QualifiedName = "P::Drone",
                    DefinitionKeyword = "part def",
                    Children =
                    [
                        new SysmlFeatureNode { Name = "motors", QualifiedName = "P::Drone::motors", FeatureKeyword = "part", FeatureTyping = "Motor", Multiplicity = "[4]" }
                    ]
                },
                ["P::RacingDrone"] = new SysmlDefinitionNode
                {
                    Name = "RacingDrone",
                    QualifiedName = "P::RacingDrone",
                    DefinitionKeyword = "part def",
                    SupertypeNames = ["Drone"],
                    Children =
                    [
                        new SysmlFeatureNode { Name = "frontMotors", QualifiedName = "P::RacingDrone::frontMotors", FeatureKeyword = "part", FeatureTyping = "Motor", Multiplicity = "[2]", SupertypeNames = ["motors"] }
                    ]
                }
            }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: a dashed hollow-triangle edge from RacingDrone to Drone is emitted (distinct
        // from the solid hollow-triangle Specialization edge also present between the same boxes).
        var lines = CollectLines(layout.Nodes);
        var subsettingEdge = lines.FirstOrDefault(l => l.TargetEnd == EndMarkerStyle.HollowTriangle && l.LineStyle == LineStyle.Dashed);
        Assert.NotNull(subsettingEdge);
        Assert.Contains(lines, l => l.TargetEnd == EndMarkerStyle.HollowTriangle && l.LineStyle == LineStyle.Solid);
    }

    /// <summary>
    ///     A <c>subsets</c> reference to a sibling feature declared on the very same definition
    ///     (no specialization boundary crossed) produces no edge — a known limitation mirroring
    ///     <see cref="GeneralViewLayoutStrategy_BuildLayout_SelfReferentialRedefinition_ProducesNoEdge"/>'s
    ///     documented self-reference behavior for Redefinition.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_SelfReferentialSubsetting_ProducesNoEdge()
    {
        // Arrange: Vehicle declares both "wheels" and a same-definition sibling "frontWheels" that
        // subsets "wheels" with no supertype to walk.
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
                        new SysmlFeatureNode { Name = "wheels", QualifiedName = "P::Vehicle::wheels", FeatureKeyword = "part", FeatureTyping = "Wheel", Multiplicity = "[4]" },
                        new SysmlFeatureNode { Name = "frontWheels", QualifiedName = "P::Vehicle::frontWheels", FeatureKeyword = "part", FeatureTyping = "Wheel", Multiplicity = "[2]", SupertypeNames = ["wheels"] }
                    ]
                }
            }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: no dashed hollow-triangle (Subsetting) edge is emitted.
        var lines = CollectLines(layout.Nodes);
        Assert.DoesNotContain(lines, l => l.TargetEnd == EndMarkerStyle.HollowTriangle && l.LineStyle == LineStyle.Dashed);
    }

    /// <summary>
    ///     BuildLayout emits a companion <see cref="BoxShape.Note"/> box (plus a connecting plain
    ///     line) for a definition that carries a <c>doc</c>/<c>comment</c> annotation.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_AnnotatedDefinition_EmitsNoteBox()
    {
        // Arrange: a part def with one Documentation annotation
        var strategy = new GeneralViewLayoutStrategy();
        var battery = new SysmlDefinitionNode
        {
            Name = "Battery",
            QualifiedName = "P::Battery",
            DefinitionKeyword = "part def",
            Annotations = [new SysmlAnnotation(SysmlAnnotationKind.Documentation, "Nominal battery capacity is 5000mAh.")],
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["P::Battery"] = battery }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: a Note-shaped box is present, plus a plain (no-arrowhead, solid) connecting line
        var boxes = CollectBoxes(layout.Nodes);
        var note = Assert.Single(boxes, b => b.Shape == BoxShape.Note);
        Assert.Contains(note.Compartments, c => c.Rows.Any(r => r.Contains("Nominal battery capacity")));

        var lines = CollectLines(layout.Nodes);
        Assert.Contains(lines, l => l.TargetEnd == EndMarkerStyle.None && l.LineStyle == LineStyle.Solid);
    }

    /// <summary>
    ///     BuildLayout does not emit a Note box for a definition with no annotations (regression
    ///     guard against always-on note emission).
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_UnannotatedDefinition_EmitsNoNoteBox()
    {
        // Arrange: a part def with no annotations
        var strategy = new GeneralViewLayoutStrategy();
        var battery = new SysmlDefinitionNode
        {
            Name = "Battery",
            QualifiedName = "P::Battery",
            DefinitionKeyword = "part def",
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["P::Battery"] = battery }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: no Note-shaped box is emitted
        var boxes = CollectBoxes(layout.Nodes);
        Assert.DoesNotContain(boxes, b => b.Shape == BoxShape.Note);
    }

    /// <summary>
    ///     BuildLayout combines multiple annotations on one element into a single Note box (not
    ///     one box per annotation), per this strategy's documented aggregation choice.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_MultipleAnnotations_ProduceOneNoteBox()
    {
        // Arrange: a part def with two annotations (a doc and a comment)
        var strategy = new GeneralViewLayoutStrategy();
        var battery = new SysmlDefinitionNode
        {
            Name = "Battery",
            QualifiedName = "P::Battery",
            DefinitionKeyword = "part def",
            Annotations =
            [
                new SysmlAnnotation(SysmlAnnotationKind.Documentation, "Primary power source."),
                new SysmlAnnotation(SysmlAnnotationKind.Comment, "TODO: revisit capacity."),
            ],
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["P::Battery"] = battery }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: exactly one Note box, containing both annotations' text
        var boxes = CollectBoxes(layout.Nodes);
        var note = Assert.Single(boxes, b => b.Shape == BoxShape.Note);
        Assert.Contains(note.Compartments, c => c.Rows.Any(r => r.Contains("Primary power source")));
        Assert.Contains(note.Compartments, c => c.Rows.Any(r => r.Contains("revisit capacity")));
    }

    /// <summary>
    ///     BuildLayout renders a requirement definition's <c>subject</c> feature under a
    ///     stereotype-style <c>«subject»</c> compartment title.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_RequirementSubject_UsesGuillemetTitle()
    {
        // Arrange: a requirement def owning a subject feature
        var strategy = new GeneralViewLayoutStrategy();
        var requirement = new SysmlDefinitionNode
        {
            Name = "MassLimitationRequirement",
            QualifiedName = "P::MassLimitationRequirement",
            DefinitionKeyword = "requirement def",
            Children =
            [
                new SysmlFeatureNode { Name = "vehicle", QualifiedName = "P::MassLimitationRequirement::vehicle", FeatureKeyword = "subject", FeatureTyping = "Vehicle" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["P::MassLimitationRequirement"] = requirement }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert
        var box = CollectBoxes(layout.Nodes).First(b => b.Label == "MassLimitationRequirement");
        Assert.Contains(box.Compartments, c => c.Title == "«subject»" && c.Rows.Contains("vehicle : Vehicle"));
    }

    /// <summary>
    ///     BuildLayout renders <c>assume constraint</c>/<c>require constraint</c>/<c>constraint</c>
    ///     features under stereotype-style compartment titles, showing the raw expression text
    ///     instead of a <c>name : Type</c> row.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_ConstraintFeatures_ShowExpressionText()
    {
        // Arrange: a requirement def owning assume/require constraint features
        var strategy = new GeneralViewLayoutStrategy();
        var requirement = new SysmlDefinitionNode
        {
            Name = "MassLimitationRequirement",
            QualifiedName = "P::MassLimitationRequirement",
            DefinitionKeyword = "requirement def",
            Children =
            [
                new SysmlFeatureNode { FeatureKeyword = "assume constraint", ExpressionText = "{fuelMass > 0}" },
                new SysmlFeatureNode { FeatureKeyword = "require constraint", ExpressionText = "{massActual <= massReqd}" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["P::MassLimitationRequirement"] = requirement }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert
        var box = CollectBoxes(layout.Nodes).First(b => b.Label == "MassLimitationRequirement");
        Assert.Contains(box.Compartments, c => c.Title == "«assume constraint»" && c.Rows.Contains("{fuelMass > 0}"));
        Assert.Contains(box.Compartments, c => c.Title == "«require constraint»" && c.Rows.Contains("{massActual <= massReqd}"));
    }

    /// <summary>
    ///     BuildLayout renders an <c>enum def</c>'s literal values under an "enum values"
    ///     compartment title (the plain default pluralization, not a stereotype).
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_EnumDefLiteralValues_ProducesEnumValuesCompartment()
    {
        // Arrange: an enum def owning two enum-value features
        var strategy = new GeneralViewLayoutStrategy();
        var flightMode = new SysmlDefinitionNode
        {
            Name = "FlightMode",
            QualifiedName = "P::FlightMode",
            DefinitionKeyword = "enum def",
            Children =
            [
                new SysmlFeatureNode { Name = "manual", QualifiedName = "P::FlightMode::manual", FeatureKeyword = "enum value" },
                new SysmlFeatureNode { Name = "auto", QualifiedName = "P::FlightMode::auto", FeatureKeyword = "enum value" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["P::FlightMode"] = flightMode }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert
        var box = CollectBoxes(layout.Nodes).First(b => b.Label == "FlightMode");
        Assert.Contains(box.Compartments, c => c.Title == "enum values" && c.Rows.Contains("manual") && c.Rows.Contains("auto"));
    }

    /// <summary>
    ///     Builds a workspace where package <c>Sys</c> contains <c>Sys::Outer</c>, which owns one
    ///     nested definition <c>Sys::Outer::Inner</c> — a 2-level-deep nested-definition-containment
    ///     structure (folder contents at depth 1, <c>Outer</c>'s own nested child at depth 2), used
    ///     by the <see cref="RenderOptions.DepthLimit"/> nested-definition-containment truncation
    ///     regression tests.
    /// </summary>
    private static SysmlWorkspace BuildTwoLevelNestedDefinitionWorkspace() => new()
    {
        Declarations = new Dictionary<string, SysmlNode>
        {
            ["Sys::Outer"] = new SysmlDefinitionNode
            {
                Name = "Outer",
                QualifiedName = "Sys::Outer",
                DefinitionKeyword = "part def"
            },
            ["Sys::Outer::Inner"] = new SysmlDefinitionNode
            {
                Name = "Inner",
                QualifiedName = "Sys::Outer::Inner",
                DefinitionKeyword = "part def"
            }
        }
    };

    /// <summary>
    ///     Regression test for the reviewer-confirmed bug: previously <see cref="RenderOptions.DepthLimit"/>
    ///     only capped package-folder-contents nesting (<c>truncateFolderContents</c>), never
    ///     nested-definition containment introduced by <c>PlaceDef</c>'s own recursion — so a
    ///     <c>DepthLimit</c> that should cap all nesting had no effect once a definition owned a
    ///     nested definition. With the fix, a <c>DepthLimit</c> of 2 over the 2-level-deep
    ///     <c>Outer</c>/<c>Inner</c> nested-definition-containment fixture truncates the innermost
    ///     level (<c>Inner</c>, <c>Outer</c>'s own nested child, at depth 2): <c>Outer</c>'s own box
    ///     is still rendered normally, but in place of an <c>Inner</c> box its own
    ///     <see cref="LayoutBox.Children"/> carries a single "+1 more…" ellipsis
    ///     <see cref="LayoutLabel"/>, mirroring how a depth-truncated package folder's contents are
    ///     already reported.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_NestedDefinitionContainment_DepthLimitTruncatesInnerLevel()
    {
        // Arrange: Sys::Outer owns Sys::Outer::Inner, DepthLimit = 2
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = BuildTwoLevelNestedDefinitionWorkspace();
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light, DepthLimit: 2);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: Outer's own box still renders, but Inner does not appear as a box anywhere
        var allBoxes = CollectBoxes(layout.Nodes);
        var outerBox = allBoxes.Single(b => b.Label == "Outer");
        Assert.DoesNotContain(allBoxes, b => b.Label == "Inner");

        // Assert: Outer's own Children carries a single placeholder box standing in for the
        // truncated Inner definition, itself decorated with a "+1 more…" ellipsis label — mirroring
        // how a depth-truncated package folder's own placed box is decorated, one nesting level
        // deeper.
        var placeholder = Assert.IsType<LayoutBox>(Assert.Single(outerBox.Children));
        var indicator = Assert.IsType<LayoutLabel>(Assert.Single(placeholder.Children));
        Assert.Equal("+1 more\u2026", indicator.Text);
    }

    /// <summary>
    ///     Regression guard: <see cref="RenderOptions.DepthLimit"/> == 0 (unlimited) still renders
    ///     full nested-definition-containment depth unchanged — the fix must not truncate anything
    ///     when no depth limit is requested.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_NestedDefinitionContainment_DepthLimitZero_RendersFullDepth()
    {
        // Arrange: same Outer/Inner fixture, but with the default unlimited DepthLimit
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = BuildTwoLevelNestedDefinitionWorkspace();
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: both Outer and Inner render as full boxes, Inner nested as Outer's own child, and
        // no ellipsis placeholder label appears anywhere.
        var allBoxes = CollectBoxes(layout.Nodes);
        var outerBox = allBoxes.Single(b => b.Label == "Outer");
        var innerBox = Assert.IsType<LayoutBox>(Assert.Single(outerBox.Children));
        Assert.Equal("Inner", innerBox.Label);
    }

    /// <summary>
    ///     Regression guard: unrelated to nested-definition containment, a <see cref="RenderOptions.DepthLimit"/>
    ///     of 1 still truncates a plain package folder's own contents exactly as before the fix
    ///     (<c>truncateFolderContents</c> in <c>BuildGraph</c>), proving the nested-definition-containment
    ///     fix did not regress the pre-existing package-folder-contents truncation path.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_PackageFolderContents_DepthLimitOne_StillTruncatesFolder()
    {
        // Arrange: package Sys with two sibling definitions (no definition-containment nesting),
        // DepthLimit = 1
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
        var options = new RenderOptions(Themes.Light, DepthLimit: 1);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: the folder is present but neither A nor B renders as an individual box
        var allBoxes = CollectBoxes(layout.Nodes);
        var folder = allBoxes.Single(b => b.Shape == BoxShape.Folder);
        Assert.Equal("Sys", folder.Label);
        Assert.DoesNotContain(allBoxes, b => b.Label is "A" or "B");

        // Assert: the folder carries a "+2 more…" ellipsis label reporting its two hidden definitions
        var indicator = Assert.IsType<LayoutLabel>(Assert.Single(folder.Children));
        Assert.Equal("+2 more\u2026", indicator.Text);
    }
}

