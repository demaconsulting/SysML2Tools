// <copyright file="GeneralViewLayoutStrategyTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Reflection;
using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.Layout;
using DemaConsulting.SysML2Tools.Layout.Internal;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;
using DemaConsulting.SysML2Tools.Stdlib;
using NodePolicy = DemaConsulting.SysML2Tools.Layout.Internal.GeneralViewLayoutStrategy.NodePolicy;

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

        // Assert: a folder-shaped box exists labeled with the package name and no «package» keyword
        var folder = CollectBoxes(layout.Nodes).FirstOrDefault(b => b.Shape == BoxShape.Folder);
        Assert.NotNull(folder);
        Assert.Null(folder.Keyword);
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
        Assert.Null(folder.Keyword);
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
        Assert.Equal(EndMarkerStyle.HollowTriangle, line.TargetEnd);
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
    ///     formatting each usage as a <c>name : Type</c> row (the row-only policy shows every
    ///     usage as a row), with every compartment title centered and italic.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_DefinitionWithUsages_ProducesCompartments()
    {
        // Arrange: a part def owning an attribute usage and a port usage, under the row-only policy
        var strategy = new GeneralViewLayoutStrategy(NodePolicy.RowsOnly);
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
        Assert.All(box.Compartments, c =>
        {
            Assert.Equal(TextAlign.Center, c.TitleAlign);
            Assert.Equal(TextStyle.Italic, c.TitleStyle);
        });
    }

    /// <summary>
    ///     Loads <paramref name="sysml"/>, then lays out the view <paramref name="viewName"/>
    ///     (declared in package <c>Diagrams</c>) with the general view strategy.
    /// </summary>
    private static async Task<LayoutTree> LayOutDeclaredViewAsync(string sysml, string viewName, int depthLimit = 0, NodePolicy? policy = null)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, sysml, TestContext.Current.CancellationToken);
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);
            Assert.NotNull(result.Workspace);
            var view = Assert.IsType<SysmlViewNode>(result.Workspace.Declarations[$"Diagrams::{viewName}"]);
            return new GeneralViewLayoutStrategy(policy ?? NodePolicy.Pilot).BuildLayout(
                new ViewContext(viewName, result.Workspace, view), new RenderOptions(Themes.Light, DepthLimit: depthLimit));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>Model with one package of two definitions and one view per expose form.</summary>
    private const string ExposeFormsSysml = """
        package Pkg { part def Block1; part def Block2; }
        package Diagrams {
            view Bare { expose Pkg; }
            view Members { expose Pkg::*; }
            view Recursive { expose Pkg::**; }
            view BareKeptByFilter { expose Pkg; filter @SysML::Package; }
            view BareExcludedByFilter { expose Pkg; filter @SysML::PartDefinition; }
        }
        """;

    /// <summary>
    ///     <c>expose Pkg;</c> (MembershipExpose, SysML 8.3.26.3) exposes the package element itself:
    ///     the general view draws a single package node with no member boxes.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_BarePackageExpose_DrawsEmptyPackageNode()
    {
        // Act
        var layout = await LayOutDeclaredViewAsync(ExposeFormsSysml, "Bare");

        // Assert
        var box = Assert.Single(CollectBoxes(layout.Nodes));
        Assert.Equal(BoxShape.Folder, box.Shape);
        Assert.Equal("Pkg", box.Label);
        Assert.Empty(box.Children);
    }

    /// <summary>
    ///     View conditions apply to an exposed package node like any exposed element: a condition the
    ///     package satisfies (<c>@SysML::Package</c>) keeps it, one it does not
    ///     (<c>@SysML::PartDefinition</c>) removes it.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ViewConditionOnExposedPackage_KeepsOrRemovesPackageNode()
    {
        // Act
        var kept = await LayOutDeclaredViewAsync(ExposeFormsSysml, "BareKeptByFilter");
        var excluded = await LayOutDeclaredViewAsync(ExposeFormsSysml, "BareExcludedByFilter");

        // Assert
        var box = Assert.Single(CollectBoxes(kept.Nodes));
        Assert.Equal(BoxShape.Folder, box.Shape);
        Assert.Equal("Pkg", box.Label);
        Assert.Empty(CollectBoxes(excluded.Nodes));
    }

    /// <summary>
    ///     With a depth limit of 1, a bare-exposed package is still drawn as an empty package node,
    ///     never decorated with a truncation (<c>…</c>) indicator.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_BarePackageExposeWithDepthLimitOne_HasNoTruncationIndicator()
    {
        // Act
        var layout = await LayOutDeclaredViewAsync(ExposeFormsSysml, "Bare", depthLimit: 1);

        // Assert
        var box = Assert.Single(CollectBoxes(layout.Nodes));
        Assert.Equal(BoxShape.Folder, box.Shape);
        Assert.Empty(box.Children);
    }

    /// <summary>
    ///     <c>expose Pkg::*;</c> (NamespaceExpose, SysML 8.3.26.4) exposes only the package's members:
    ///     the member boxes are drawn without a package node.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_NamespaceExpose_DrawsMembersWithoutPackageNode()
    {
        // Act
        var layout = await LayOutDeclaredViewAsync(ExposeFormsSysml, "Members");

        // Assert
        var boxes = CollectBoxes(layout.Nodes);
        Assert.DoesNotContain(boxes, b => b.Shape == BoxShape.Folder);
        Assert.Contains(boxes, b => b.Label == "Block1");
        Assert.Contains(boxes, b => b.Label == "Block2");
    }

    /// <summary>
    ///     <c>expose Pkg::**;</c> (recursive MembershipExpose) exposes the package and all its members:
    ///     the package node is drawn containing the member boxes.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_RecursivePackageExpose_DrawsPackageNodeWithMembers()
    {
        // Act
        var layout = await LayOutDeclaredViewAsync(ExposeFormsSysml, "Recursive");

        // Assert
        var folder = Assert.Single(CollectBoxes(layout.Nodes), b => b.Shape == BoxShape.Folder);
        Assert.Equal("Pkg", folder.Label);
        Assert.Contains(folder.Children.OfType<LayoutBox>(), b => b.Label == "Block1");
        Assert.Contains(folder.Children.OfType<LayoutBox>(), b => b.Label == "Block2");
    }

    /// <summary>
    ///     Three packages each define a part def with the same simple name (<c>Checker</c>) and use
    ///     it from their own system definition. Each usage must be wired to its own package's
    ///     definition: every package folder contains exactly one composition (filled-diamond) edge
    ///     between its own two boxes, rather than all three edges converging on the first
    ///     same-named definition found workspace-wide.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_SameNamedDefsInSiblingPackages_WiresEachToItsOwn()
    {
        // Arrange: three sibling packages, each with its own Checker and a System using it
        const string sysml = """
            package PkgA { part def Checker; part def SystemA { part checker : Checker; } }
            package PkgB { part def Checker; part def SystemB { part checker : Checker; } }
            package PkgC { part def Checker; part def SystemC { part checker : Checker; } }
            """;
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, sysml, TestContext.Current.CancellationToken);
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);
            Assert.NotNull(result.Workspace);
            var context = new ViewContext("v", result.Workspace);

            // Act
            var layout = new GeneralViewLayoutStrategy().BuildLayout(context, new RenderOptions(Themes.Light));

            // Assert: three distinct Checker boxes, and each package folder holds exactly one
            // filled-diamond membership edge among its own children (Checker -> its own System)
            var boxes = CollectBoxes(layout.Nodes);
            Assert.Equal(3, boxes.Count(b => b.Label == "Checker"));
            foreach (var package in new[] { "PkgA", "PkgB", "PkgC" })
            {
                var folder = Assert.Single(boxes, b => b.Label == package);
                Assert.Single(folder.Children.OfType<LayoutLine>(), l => l.TargetEnd == EndMarkerStyle.FilledDiamond);
            }
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A <c>dependency</c> whose endpoint is a definition outside the exposed scope is not
    ///     re-mapped to a same-named definition that is rendered: endpoints from the semantic index
    ///     are already qualified, so no simple-name matching is applied to them.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_UnrenderedQualifiedEndpoint_IsNotMappedBySimpleName()
    {
        // Arrange: PkgA::X depends on PkgB::Y, and the view exposes only PkgB (which has its own X)
        const string sysml = """
            package PkgA { part def X; }
            package PkgB { part def X; part def Y; }
            package Diagrams { dependency PkgA::X to PkgB::Y; view V { expose PkgB::**; } }
            """;
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, sysml, TestContext.Current.CancellationToken);
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);
            Assert.NotNull(result.Workspace);
            var view = Assert.IsType<SysmlViewNode>(result.Workspace.Declarations["Diagrams::V"]);

            // Act
            var layout = new GeneralViewLayoutStrategy().BuildLayout(
                new ViewContext("V", result.Workspace, view), new RenderOptions(Themes.Light));

            // Assert: no dependency (dashed open-chevron) line between PkgB::X and PkgB::Y
            Assert.DoesNotContain(CollectLines(layout.Nodes), l =>
                l.TargetEnd == EndMarkerStyle.OpenChevron && l.LineStyle == LineStyle.Dashed);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A type reference that does not resolve by SysML v2 name resolution (no import, not in an
    ///     enclosing namespace) is unresolved even though a same-named definition exists in an
    ///     unrelated package: no edge is drawn and the layout reports no warning (the load-time
    ///     <c>Unresolved reference</c> diagnostic is the single report).
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_SameSimpleNameInUnrelatedPackage_DrawsNoEdge()
    {
        // Act: Consumer types its part by TurboEngineX without importing Lib; both packages are exposed
        var layout = await LayOutViewVAsync("""
            package Lib { part def TurboEngineX; }
            package App { part def Consumer { part engine : TurboEngineX; } }
            package Diagrams { view V { expose Lib::*; expose App::*; } }
            """);

        // Assert: both definitions are drawn, with no composition edge and no layout warning
        var boxes = CollectBoxes(layout.Nodes);
        Assert.Contains(boxes, b => b.Label == "TurboEngineX");
        Assert.Contains(boxes, b => b.Label == "Consumer");
        Assert.DoesNotContain(CollectLines(layout.Nodes), l => l.TargetEnd == EndMarkerStyle.FilledDiamond);
        Assert.Empty(layout.Warnings);
    }

    /// <summary>
    ///     Subsetting and redefinition references that do not resolve (only a same-named feature in
    ///     an unrelated package exists) draw no edge and no warning, while the same references
    ///     written in a specialization of the declaring type still draw theirs, from the subsetting
    ///     or redefining usage to the usage it subsets or redefines.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_UnresolvedSubsettingAndRedefinition_DrawNoEdge()
    {
        // Act: Consumer's references are unresolvable; Child's resolve through its supertype Holder
        var layout = await LayOutViewVAsync("""
            package Lib {
                part def Holder { part slotX; part spareX; }
                part def Child :> Holder { part childSub :> slotX; part childRedef redefines spareX; }
            }
            package App { part def Consumer { part consumerSub :> slotX; part consumerRedef redefines spareX; } }
            package Diagrams { view V { expose Lib::**; expose App::**; } }
            """);

        // Assert: exactly one subsetting and one redefinition edge (Child's), no warning
        var lines = CollectLines(layout.Nodes);
        var subsetting = Assert.Single(lines, l => l.TargetEnd == EndMarkerStyle.HollowTriangle && RunsBetween(l, FindBox(layout.Nodes, "childSub"), FindBox(layout.Nodes, "slotX")));
        Assert.Equal(LineStyle.Solid, subsetting.LineStyle);
        var redefinition = Assert.Single(lines, l => l.TargetEnd == EndMarkerStyle.HollowTriangleCrossbar);
        Assert.True(RunsBetween(redefinition, FindBox(layout.Nodes, "childRedef"), FindBox(layout.Nodes, "spareX")));
        Assert.Empty(layout.Warnings);
    }

    /// <summary>
    ///     A subsetting and a redefinition whose targets are reachable only through an import written
    ///     at the root of the file (outside any package) are resolved by the semantic model, whose
    ///     edges the layout prefers: both still draw their edge although the layout's own name
    ///     resolver cannot see file-root imports.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_FileRootImportSubsettingAndRedefinition_DrawsEdges()
    {
        // Act
        var layout = await LayOutViewVAsync("""
            package Lib { part def Holder { part slot; part spare; } }
            import Lib::Holder::*;
            package App { part def Consumer { part consumerSub :> slot; part consumerRedef redefines spare; } }
            package Diagrams { view V { expose Lib::**; expose App::**; } }
            """);

        // Assert
        var lines = CollectLines(layout.Nodes);
        Assert.Single(lines, l => l.TargetEnd == EndMarkerStyle.HollowTriangle && RunsBetween(l, FindBox(layout.Nodes, "consumerSub"), FindBox(layout.Nodes, "slot")));
        var redefinition = Assert.Single(lines, l => l.TargetEnd == EndMarkerStyle.HollowTriangleCrossbar);
        Assert.True(RunsBetween(redefinition, FindBox(layout.Nodes, "consumerRedef"), FindBox(layout.Nodes, "spare")));
        Assert.Empty(layout.Warnings);
    }

    /// <summary>
    ///     A reference to a private member of another package gets no semantic typing edge and is
    ///     not drawn (and not reported by the layout), while an accepted (public) reference is wired
    ///     through its semantic edge.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_PrivateMemberReference_DrawsNoEdge()
    {
        // Act
        var layout = await LayOutViewVAsync("""
            package P { private part def Hidden; part def Shown; }
            package Q { part def Owner { part a : P::Hidden; part b : P::Shown; } }
            package Diagrams { view V { expose P::**; expose Q::**; } }
            """);

        // Assert: only the usage b is typed by Shown; a is not typed by Hidden
        var typing = Assert.Single(CollectLines(layout.Nodes), l => l.TargetEnd == EndMarkerStyle.HollowTriangleTwoDots);
        Assert.True(RunsBetween(typing, FindBox(layout.Nodes, "b"), FindBox(layout.Nodes, "Shown")));
        Assert.Empty(layout.Warnings);
    }

    /// <summary>
    ///     A candidate a filtered import rejects gets no semantic typing edge and is not drawn; the
    ///     candidate the filter accepts is wired.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_FilteredOutImportCandidate_DrawsNoEdge()
    {
        // Act
        var layout = await LayOutViewVAsync("""
            metadata def X { }
            package P { part def Marked { @X; } part def Plain; }
            package Q {
                private import P::*[@X];
                part def Owner { part a : Marked; part b : Plain; }
            }
            package Diagrams { view V { expose P::**; expose Q::**; } }
            """);

        // Assert: only the usage a is typed by Marked
        var typing = Assert.Single(CollectLines(layout.Nodes), l => l.TargetEnd == EndMarkerStyle.HollowTriangleTwoDots);
        Assert.True(RunsBetween(typing, FindBox(layout.Nodes, "a"), FindBox(layout.Nodes, "Marked")));
        Assert.Empty(layout.Warnings);
    }

    /// <summary>
    ///     An import applies only to its own package: a sibling package that merely shares the
    ///     simple name gets no edge, while the importing package does.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ImportOfSiblingPackage_DoesNotResolveForThisPackage()
    {
        // Act: one view exposes the importing package, the other only the sibling package
        var importing = await LayOutViewVAsync("""
            package A { part def Thing; }
            package PkgA { private import A::*; part def OwnerA { part p : Thing; } }
            package PkgB { part def OwnerB { part q : Thing; } }
            package Diagrams { view V { expose A::**; expose PkgA::**; } }
            """);
        var sibling = await LayOutViewVAsync("""
            package A { part def Thing; }
            package PkgA { private import A::*; part def OwnerA { part p : Thing; } }
            package PkgB { part def OwnerB { part q : Thing; } }
            package Diagrams { view V { expose A::**; expose PkgB::**; } }
            """);

        // Assert: PkgA::OwnerA's usage p is typed by A::Thing, PkgB::OwnerB's usage q is not
        var typing = Assert.Single(CollectLines(importing.Nodes), l => l.TargetEnd == EndMarkerStyle.HollowTriangleTwoDots);
        Assert.True(RunsBetween(typing, FindBox(importing.Nodes, "p"), FindBox(importing.Nodes, "Thing")));
        Assert.DoesNotContain(CollectLines(sibling.Nodes), l => l.TargetEnd == EndMarkerStyle.HollowTriangleTwoDots);
        Assert.Empty(importing.Warnings);
        Assert.Empty(sibling.Warnings);
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
    ///     A part def that owns a part usage draws the usage as its own node, joined to the owning
    ///     definition's box by a solid line with a filled diamond at the owner (composite feature
    ///     membership); the usage's rounded node is the line's source.
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

        // Assert: a solid filled-diamond line runs from the wheel usage node to the Vehicle box
        var membershipEdge = Assert.Single(CollectLines(layout.Nodes), l => l.TargetEnd == EndMarkerStyle.FilledDiamond);
        Assert.Equal(LineStyle.Solid, membershipEdge.LineStyle);
        var wheel = FindBox(layout.Nodes, "wheel");
        Assert.Equal(BoxShape.RoundedRectangle, wheel.Shape);
        Assert.True(RunsBetween(membershipEdge, wheel, FindBox(layout.Nodes, "Vehicle")));
    }

    /// <summary>
    ///     A part def that owns a reference part usage (<c>ref part</c>) draws the usage as a node
    ///     with a dashed outline and the keyword <c>ref part</c>, joined to the owner by a solid
    ///     line with a hollow diamond at the owner (noncomposite feature membership).
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_ReferenceMembership_ProducesHollowDiamondEdgeAndDashedNode()
    {
        // Arrange: System owns a reference part typed as Engine; both are user definitions
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
                        new SysmlFeatureNode { Name = "eng", QualifiedName = "P::System::eng", FeatureKeyword = "part", IsReference = true, FeatureTyping = "Engine" }
                    ]
                }
            }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: the usage node is dashed and reads «ref part»; a solid hollow-diamond line joins
        // it to its owner; no filled diamond is drawn
        var eng = FindBox(layout.Nodes, "eng");
        Assert.Equal("ref part", eng.Keyword);
        Assert.Equal(LineStyle.Dashed, eng.Appearance?.OutlineStyle);
        var lines = CollectLines(layout.Nodes);
        var membership = Assert.Single(lines, l => l.TargetEnd == EndMarkerStyle.HollowDiamond);
        Assert.Equal(LineStyle.Solid, membership.LineStyle);
        Assert.True(RunsBetween(membership, eng, FindBox(layout.Nodes, "System")));
        Assert.DoesNotContain(lines, l => l.TargetEnd == EndMarkerStyle.FilledDiamond);
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
    ///     An attribute usage owned by a definition is a compartment row (<c>mass : Mass</c>) under
    ///     the Pilot policy and draws no typing edge, however its type is declared — the type is
    ///     visible in the row, as the Pilot implementation shows it.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_AttributeTyping_RowDrawsNoTypingEdge()
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

        // Assert: the attribute is a row of the Vehicle box and no edge of any kind is drawn
        var vehicle = FindBox(layout.Nodes, "Vehicle");
        Assert.Contains(vehicle.Compartments, c => c.Title == "attributes" && c.Rows.Contains("mass : Mass"));
        Assert.Empty(CollectLines(layout.Nodes));
    }

    /// <summary>
    ///     With the all-usages policy an attribute usage is a node typed by its attribute definition
    ///     (hollow triangle with two dots at the definition) and joined to its owner by a hollow
    ///     diamond (attributes are referential, not composite).
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_AllUsages_AttributeUsage_IsNodeWithTypingAndHollowDiamond()
    {
        // Arrange: Vehicle owns an attribute typed as the user attribute def Mass
        var strategy = new GeneralViewLayoutStrategy(NodePolicy.AllUsages);
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

        // Assert: the usage node exists, is typed by Mass, and is joined to Vehicle by a hollow diamond
        var mass = FindBox(layout.Nodes, "mass");
        var lines = CollectLines(layout.Nodes);
        var typing = Assert.Single(lines, l => l.TargetEnd == EndMarkerStyle.HollowTriangleTwoDots);
        Assert.True(RunsBetween(typing, mass, FindBox(layout.Nodes, "Mass")));
        var membership = Assert.Single(lines, l => l.TargetEnd == EndMarkerStyle.HollowDiamond);
        Assert.True(RunsBetween(membership, mass, FindBox(layout.Nodes, "Vehicle")));
        Assert.DoesNotContain(FindBox(layout.Nodes, "Vehicle").Compartments, c => c.Title == "attributes");
    }

    /// <summary>
    ///     A usage that redefines a bare-named inherited usage (declared on a resolved supertype of
    ///     its owner) draws a solid hollow-triangle-with-crossbar line from the redefining usage
    ///     node to the redefined usage node.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_BareNameRedefinition_ProducesHollowTriangleCrossbarEdge()
    {
        // Arrange: Vehicle declares "eng"; SmallVehicle specializes Vehicle and redefines "eng" by
        // bare name (no Owner:: qualifier). Nested usages are registered in Declarations like a
        // loaded workspace does.
        var strategy = new GeneralViewLayoutStrategy();
        var eng = new SysmlFeatureNode { Name = "eng", QualifiedName = "P::Vehicle::eng", FeatureKeyword = "part" };
        var smallEng = new SysmlFeatureNode { Name = "smallEng", QualifiedName = "P::SmallVehicle::smallEng", FeatureKeyword = "part", RedefinedFeatureName = "eng" };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Vehicle"] = new SysmlDefinitionNode { Name = "Vehicle", QualifiedName = "P::Vehicle", DefinitionKeyword = "part def", Children = [eng] },
                ["P::Vehicle::eng"] = eng,
                ["P::SmallVehicle"] = new SysmlDefinitionNode
                {
                    Name = "SmallVehicle",
                    QualifiedName = "P::SmallVehicle",
                    DefinitionKeyword = "part def",
                    SupertypeNames = ["Vehicle"],
                    Children = [smallEng]
                },
                ["P::SmallVehicle::smallEng"] = smallEng
            }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: a solid crossbar line runs from smallEng to eng
        var redefinitionEdge = Assert.Single(CollectLines(layout.Nodes), l => l.TargetEnd == EndMarkerStyle.HollowTriangleCrossbar);
        Assert.Equal(LineStyle.Solid, redefinitionEdge.LineStyle);
        Assert.True(RunsBetween(redefinitionEdge, FindBox(layout.Nodes, "smallEng"), FindBox(layout.Nodes, "eng")));
    }

    /// <summary>
    ///     A usage that redefines a qualified <c>Owner::feature</c> reference draws a
    ///     hollow-triangle-with-crossbar edge to the named usage, without needing to walk the
    ///     supertype chain.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_QualifiedRedefinition_ProducesHollowTriangleCrossbarEdgeToRedefinedUsage()
    {
        // Arrange: Car redefines "Vehicle::mass" directly by qualified reference.
        var strategy = new GeneralViewLayoutStrategy();
        var mass = new SysmlFeatureNode { Name = "mass", QualifiedName = "P::Vehicle::mass", FeatureKeyword = "part" };
        var carMass = new SysmlFeatureNode { Name = "carMass", QualifiedName = "P::Car::carMass", FeatureKeyword = "part", RedefinedFeatureName = "Vehicle::mass" };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Vehicle"] = new SysmlDefinitionNode { Name = "Vehicle", QualifiedName = "P::Vehicle", DefinitionKeyword = "part def", Children = [mass] },
                ["P::Vehicle::mass"] = mass,
                ["P::Car"] = new SysmlDefinitionNode
                {
                    Name = "Car",
                    QualifiedName = "P::Car",
                    DefinitionKeyword = "part def",
                    SupertypeNames = ["Vehicle"],
                    Children = [carMass]
                },
                ["P::Car::carMass"] = carMass
            }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: a crossbar edge is emitted, resolved via the qualified reference
        var redefinitionEdge = Assert.Single(CollectLines(layout.Nodes), l => l.TargetEnd == EndMarkerStyle.HollowTriangleCrossbar);
        Assert.True(RunsBetween(redefinitionEdge, FindBox(layout.Nodes, "carMass"), FindBox(layout.Nodes, "mass")));
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
        var eng = new SysmlFeatureNode { Name = "eng", QualifiedName = "P::Vehicle::eng", FeatureKeyword = "part", RedefinedFeatureName = "nonExistentFeature" };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Vehicle"] = new SysmlDefinitionNode { Name = "Vehicle", QualifiedName = "P::Vehicle", DefinitionKeyword = "part def", Children = [eng] },
                ["P::Vehicle::eng"] = eng
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
    ///     member) produces a hollow-triangle-crossbar edge to the usage of the actual declaring
    ///     ancestor (<c>GrandParent::feat</c>) — proving the bare-name walk is transitive, not
    ///     limited to a single hop.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_TransitiveBareNameRedefinition_ProducesHollowTriangleCrossbarEdgeToDeclaringAncestorUsage()
    {
        // Arrange: GrandParent declares "feat"; Parent specializes GrandParent with no members of
        // its own; Mid specializes Parent and redefines "feat" by bare name.
        var strategy = new GeneralViewLayoutStrategy();
        var feat = new SysmlFeatureNode { Name = "feat", QualifiedName = "P::GrandParent::feat", FeatureKeyword = "part" };
        var subFeat = new SysmlFeatureNode { Name = "subFeat", QualifiedName = "P::Mid::subFeat", FeatureKeyword = "part", RedefinedFeatureName = "feat" };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::GrandParent"] = new SysmlDefinitionNode { Name = "GrandParent", QualifiedName = "P::GrandParent", DefinitionKeyword = "part def", Children = [feat] },
                ["P::GrandParent::feat"] = feat,
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
                    Children = [subFeat]
                },
                ["P::Mid::subFeat"] = subFeat
            }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: exactly one crossbar edge, from subFeat to GrandParent's feat (not one per hop)
        var redefinitionEdges = CollectLines(layout.Nodes)
            .Where(l => l.TargetEnd == EndMarkerStyle.HollowTriangleCrossbar)
            .ToList();
        var edge = Assert.Single(redefinitionEdges);
        Assert.Equal(LineStyle.Solid, edge.LineStyle);
        Assert.True(RunsBetween(edge, FindBox(layout.Nodes, "subFeat"), FindBox(layout.Nodes, "feat")));
    }

    /// <summary>
    ///     A genuinely self-referential redefinition — a usage that redefines itself — produces no
    ///     redefinition edge and does not throw. This is distinct from
    ///     <see cref="GeneralViewLayoutStrategy_BuildLayout_UnresolvableRedefinition_ProducesNoEdge"/>,
    ///     which covers a name that cannot be found anywhere, not a name that resolves back to the
    ///     usage itself.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_SelfReferentialRedefinition_ProducesNoEdge()
    {
        // Arrange: Standalone lists itself as its own supertype (a self-cycle), and its usage
        // "selfFeat" redefines itself through the semantic redefinition edge.
        var strategy = new GeneralViewLayoutStrategy();
        var selfFeat = new SysmlFeatureNode
        {
            Name = "selfFeat",
            QualifiedName = "P::Standalone::selfFeat",
            FeatureKeyword = "part",
            RedefinedFeatureName = "selfFeat",
            ResolvedEdges = [new SysmlEdge("P::Standalone::selfFeat", "P::Standalone::selfFeat", SysmlEdgeKind.Redefinition)]
        };
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
                    Children = [selfFeat]
                },
                ["P::Standalone::selfFeat"] = selfFeat
            }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act: laying out must not throw despite the self-referential supertype cycle.
        var layout = strategy.BuildLayout(context, options);

        // Assert: no hollow-triangle-crossbar edge is produced
        var redefinitionEdge = CollectLines(layout.Nodes)
            .FirstOrDefault(l => l.TargetEnd == EndMarkerStyle.HollowTriangleCrossbar);
        Assert.Null(redefinitionEdge);
    }

    /// <summary>
    ///     A definition with TWO attribute usages of the SAME in-view type draws two typing edges
    ///     under the all-usages policy (one per usage node). The layered pipeline de-duplicates an
    ///     identical directed pair, so the strategy must resolve each edge by its endpoints (not by
    ///     input position) and lay out without throwing.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_TwoAttributesSameType_ProducesTwoTypingEdgesWithoutException()
    {
        // Arrange: Vehicle owns two attributes (mass, weight) both typed as the user attribute def Mass.
        var strategy = new GeneralViewLayoutStrategy(NodePolicy.AllUsages);
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

        // Act: laying out must not throw even though the two edges share one type endpoint.
        var layout = strategy.BuildLayout(context, options);

        // Assert: exactly two typing edges are drawn (one per attribute), each with a real polyline.
        var typingEdges = CollectLines(layout.Nodes)
            .Where(l => l.TargetEnd == EndMarkerStyle.HollowTriangleTwoDots)
            .ToList();
        Assert.Equal(2, typingEdges.Count);
        Assert.All(typingEdges, e => Assert.True(e.Waypoints.Count >= 2));
    }

    /// <summary>
    ///     With the all-usages policy an attribute usage typed by an <c>enum def</c> in the view is
    ///     a node with a typing edge (hollow triangle with two dots) to the enumeration definition.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_EnumTypedAttribute_ProducesFeatureTypingEdge()
    {
        // Arrange: Controller owns an attribute typed as the user enum def FlightMode
        var strategy = new GeneralViewLayoutStrategy(NodePolicy.AllUsages);
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

        // Assert: a typing edge runs from the mode usage to the enum definition
        var typingEdge = Assert.Single(CollectLines(layout.Nodes), l => l.TargetEnd == EndMarkerStyle.HollowTriangleTwoDots);
        Assert.True(RunsBetween(typingEdge, FindBox(layout.Nodes, "mode"), FindBox(layout.Nodes, "FlightMode")));
    }

    /// <summary>
    ///     A usage whose type does not resolve to a definition in the view draws no typing edge,
    ///     mirroring the specialization/membership resolution rules.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_UsageTyping_UnresolvedType_ProducesNoTypingEdge()
    {
        // Arrange: Vehicle owns a part typed as a name with no matching definition in the view
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
                        new SysmlFeatureNode { Name = "engine", QualifiedName = "P::Vehicle::engine", FeatureKeyword = "part", FeatureTyping = "Unknown" }
                    ]
                }
            }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: the usage node and its owner link exist, but no typing edge is produced
        Assert.DoesNotContain(CollectLines(layout.Nodes), l => l.TargetEnd == EndMarkerStyle.HollowTriangleTwoDots);
        Assert.Single(CollectLines(layout.Nodes), l => l.TargetEnd == EndMarkerStyle.FilledDiamond);
    }

    /// <summary>
    ///     A part def that owns a <c>port</c> usage draws the port as its own node (the Pilot
    ///     default), joined to the owning definition's box by a filled diamond at the owner.
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

        // Assert: the port is a node, joined to the owner by a filled diamond
        var membershipEdge = Assert.Single(CollectLines(layout.Nodes), l => l.TargetEnd == EndMarkerStyle.FilledDiamond);
        Assert.True(RunsBetween(membershipEdge, FindBox(layout.Nodes, "fuel"), FindBox(layout.Nodes, "Vehicle")));
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
        // after all). "child" must render as its own box (a usage, so rounded rather than square).
        var rectangleLabels = CollectBoxes(layout.Nodes)
            .Where(b => b.Shape is BoxShape.Rectangle or BoxShape.RoundedRectangle)
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
    ///     quality re-validation found). Row-only policy: under the Pilot policy <c>b</c> is a node
    ///     and <c>c</c> its own node (see the usage-node tests).
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_NoFilter_RendersDeeplyNestedGrandchildUsageWhenIntermediateParentExcluded()
    {
        var strategy = new GeneralViewLayoutStrategy(NodePolicy.RowsOnly);
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

        var rectangles = CollectBoxes(layout.Nodes).Where(b => b.Shape is BoxShape.Rectangle or BoxShape.RoundedRectangle).ToList();
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
    ///     comment-annotation note box, neither of which is drawn as a checked-in <c>&lt;rect&gt;</c>
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
        var workspace = result.Workspace;

        const string viewQualifiedName = "QuadcopterDrone::DroneGeneralView";
        var viewNode = Assert.IsType<SysmlViewNode>(workspace.Declarations[viewQualifiedName]);

        var strategy = new GeneralViewLayoutStrategy();
        var options = new RenderOptions(Themes.Light);
        var layout = strategy.BuildLayout(new ViewContext("v", workspace, viewNode), options);

        var boxes = CollectBoxes(layout.Nodes).Where(b => b.Shape == BoxShape.Rectangle).ToList();
        Assert.Equal(21, boxes.Count);
    }

    /// <summary>
    ///     The gallery's nested-package example (<c>11-nested-packages-general.sysml</c>) draws its
    ///     <c>Powertrain</c> and <c>Body</c> packages as folders inside the <c>Vehicle</c> folder,
    ///     declares the cross-package relationships in the <c>Vehicle</c> scope (the lowest common
    ///     ancestor of the nested packages and the owner-level <c>Car</c>), and routes each to its
    ///     actual endpoint boxes: the <c>Hybrid</c> to <c>Engine</c> specialization across the two
    ///     sibling packages and the composition edges from both nested packages into <c>Car</c>.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_NestedPackagesGalleryModel_RendersFoldersNestedAndCrossPackageEdges()
    {
        var galleryModelsRoot = FindGalleryModelsRoot();
        if (galleryModelsRoot is null)
        {
            return;
        }

        var modelPath = Path.Combine(galleryModelsRoot, "11-nested-packages-general.sysml");
        if (!File.Exists(modelPath))
        {
            return;
        }

        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([modelPath], stdlibTable);
        Assert.NotNull(result.Workspace);

        // Act
        var layout = new GeneralViewLayoutStrategy().BuildLayout(
            new ViewContext("v", result.Workspace), new RenderOptions(Themes.Light));

        // Assert: one root folder holding both nested package folders and the owner-level Car box
        var vehicle = Assert.Single(layout.Nodes.OfType<LayoutBox>());
        Assert.Equal("Vehicle", vehicle.Label);
        var nestedFolders = vehicle.Children.OfType<LayoutBox>().Where(b => b.Shape == BoxShape.Folder).ToList();
        Assert.Equal(["Body", "Powertrain"], nestedFolders.Select(f => f.Label).Order().ToArray());
        Assert.Contains(vehicle.Children.OfType<LayoutBox>(), b => b.Label == "Car");

        // Assert: the cross-package edges live in the Vehicle scope and end on the nested boxes
        var hybrid = FindBox(layout.Nodes, "Hybrid");
        var engine = FindBox(layout.Nodes, "Engine");
        var car = FindBox(layout.Nodes, "Car");
        var engineUsage = FindBox(layout.Nodes, "engine");
        var chassisUsage = FindBox(layout.Nodes, "chassis");
        var crossSpecialization = Assert.Single(DirectSpecializationLines(vehicle.Children));
        Assert.True(RunsBetween(crossSpecialization, hybrid, engine));
        var compositions = vehicle.Children.OfType<LayoutLine>().Where(l => l.TargetEnd == EndMarkerStyle.FilledDiamond).ToList();
        Assert.Equal(2, compositions.Count);
        Assert.Contains(compositions, l => RunsBetween(l, engineUsage, car));
        Assert.Contains(compositions, l => RunsBetween(l, chassisUsage, car));

        // Assert: the car's usage nodes sit beside it in the Vehicle folder and are typed by the
        // definitions inside the nested package folders
        Assert.Contains(vehicle.Children.OfType<LayoutBox>(), b => b.Label == engineUsage.Label);
        var typing = vehicle.Children.OfType<LayoutLine>().Where(l => l.TargetEnd == EndMarkerStyle.HollowTriangleTwoDots).ToList();
        Assert.Equal(2, typing.Count);
        Assert.Contains(typing, l => RunsBetween(l, engineUsage, engine));
        Assert.Contains(typing, l => RunsBetween(l, chassisUsage, FindBox(layout.Nodes, "Chassis")));
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
        var workspace = result.Workspace;

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
        Assert.Contains(scopedLabels, l => l?.StartsWith("seatBelt", StringComparison.Ordinal) == true);
        Assert.Contains(scopedLabels, l => l?.StartsWith("bumper", StringComparison.Ordinal) == true);
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

        // Assert: a solid, unmarked line links the controller and battery usage nodes (not the
        // enclosing Drone definition, which would be a self-loop).
        var lines = CollectLines(layout.Nodes);
        var connectEdge = Assert.Single(lines, l => l.TargetEnd == EndMarkerStyle.None && l.LineStyle == LineStyle.Solid && l.MidpointLabel is null);
        Assert.True(
            RunsBetween(connectEdge, FindBox(layout.Nodes, "controller"), FindBox(layout.Nodes, "battery")) ||
            RunsBetween(connectEdge, FindBox(layout.Nodes, "battery"), FindBox(layout.Nodes, "controller")));
    }

    /// <summary>
    ///     A <c>connect</c> edge whose two dotted-chain endpoints resolve to the <em>same</em>
    ///     owning box (two features nested in the same usage node) produces no edge — the
    ///     self-loop guard that every other edge kind in this unit already applies.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_Connect_SameOwningType_ProducesNoEdge()
    {
        // Arrange: Vehicle owns the port portA; the connect joins two features nested in portA.
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
                }
            },
            Index = new SemanticIndex(
            [
                new SysmlEdge("P::Vehicle::portA::sig", "P::Vehicle::portA::other", SysmlEdgeKind.Connect)
            ])
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: no Connect-shaped (unmarked solid) edge is emitted between portA and itself.
        var lines = CollectLines(layout.Nodes);
        Assert.DoesNotContain(lines, l => l.TargetEnd == EndMarkerStyle.None && l.LineStyle == LineStyle.Solid);

        // Assert: the drop is surfaced as a visible warning (defense-in-depth diagnostic) rather
        // than silently discarded.
        Assert.Contains(layout.Warnings, w => w.Contains("Connect", StringComparison.Ordinal) &&
            w.Contains("P::Vehicle::portA::sig", StringComparison.Ordinal) &&
            w.Contains("P::Vehicle::portA::other", StringComparison.Ordinal));
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
            var workspace = result.Workspace;

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
    ///     solid, open-chevron edge carrying the <c>«allocate»</c> midpoint label.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_Allocate_ProducesSolidChevronEdgeWithLabel()
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

        // Assert: a solid open-chevron edge with the "«allocate»" label is emitted.
        var lines = CollectLines(layout.Nodes);
        var allocateEdge = lines.FirstOrDefault(l => l.MidpointLabel == "«allocate»");
        Assert.NotNull(allocateEdge);
        Assert.Equal(EndMarkerStyle.OpenChevron, allocateEdge.TargetEnd);
        Assert.Equal(LineStyle.Solid, allocateEdge.LineStyle);
    }

    /// <summary>
    ///     A resolved standalone <see cref="SysmlEdgeKind.Dependency"/> edge between two
    ///     definitions is drawn as a dashed line with an open chevron at the depended-upon box.
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
    ///     A subtype narrowing an inherited usage via <c>subsets</c> (e.g.
    ///     <c>part frontMotors : Motor[2] subsets motors;</c> where <c>motors</c> is declared on
    ///     the supertype) emits a solid hollow-triangle edge from the subsetting usage node to the
    ///     subsetted usage node — the same marker as specialization, between the usages instead of
    ///     between the owning definitions.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_Subsetting_CrossesSpecializationBoundary_ProducesSolidHollowTriangleUsageEdge()
    {
        // Arrange: Drone declares "motors"; RacingDrone specializes Drone and subsets "motors" via
        // a narrower "frontMotors" usage.
        var strategy = new GeneralViewLayoutStrategy();
        var motors = new SysmlFeatureNode { Name = "motors", QualifiedName = "P::Drone::motors", FeatureKeyword = "part", FeatureTyping = "Motor", Multiplicity = "[4]" };
        var frontMotors = new SysmlFeatureNode { Name = "frontMotors", QualifiedName = "P::RacingDrone::frontMotors", FeatureKeyword = "part", FeatureTyping = "Motor", Multiplicity = "[2]", SupertypeNames = ["motors"] };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Motor"] = new SysmlDefinitionNode { Name = "Motor", QualifiedName = "P::Motor", DefinitionKeyword = "part def" },
                ["P::Drone"] = new SysmlDefinitionNode { Name = "Drone", QualifiedName = "P::Drone", DefinitionKeyword = "part def", Children = [motors] },
                ["P::Drone::motors"] = motors,
                ["P::RacingDrone"] = new SysmlDefinitionNode
                {
                    Name = "RacingDrone",
                    QualifiedName = "P::RacingDrone",
                    DefinitionKeyword = "part def",
                    SupertypeNames = ["Drone"],
                    Children = [frontMotors]
                },
                ["P::RacingDrone::frontMotors"] = frontMotors
            }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: a solid hollow-triangle edge runs from the frontMotors usage to the motors usage
        // (usage to usage — no longer from RacingDrone to Drone), distinct from the solid
        // hollow-triangle Specialization edge also present between the two definitions.
        var lines = CollectLines(layout.Nodes);
        var frontMotorsBox = FindBox(layout.Nodes, "frontMotors");
        var motorsBox = FindBox(layout.Nodes, "motors");
        var subsettingEdge = Assert.Single(lines, l => l.TargetEnd == EndMarkerStyle.HollowTriangle && RunsBetween(l, frontMotorsBox, motorsBox));
        Assert.Equal(LineStyle.Solid, subsettingEdge.LineStyle);
        Assert.Single(lines, l => l.TargetEnd == EndMarkerStyle.HollowTriangle && RunsBetween(l, FindBox(layout.Nodes, "RacingDrone"), FindBox(layout.Nodes, "Drone")));
    }

    /// <summary>
    ///     A <c>subsets</c> reference to a sibling usage declared on the very same definition draws
    ///     an edge from the subsetting usage node to the subsetted usage node: the edge joins the
    ///     two usages, so no specialization boundary has to be crossed.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_SameDefinitionSubsetting_ProducesUsageToUsageEdge()
    {
        // Arrange: Vehicle declares both "wheels" and a same-definition sibling "frontWheels" that
        // subsets "wheels" with no supertype to walk.
        var strategy = new GeneralViewLayoutStrategy();
        var wheels = new SysmlFeatureNode { Name = "wheels", QualifiedName = "P::Vehicle::wheels", FeatureKeyword = "part", FeatureTyping = "Wheel", Multiplicity = "[4]" };
        var frontWheels = new SysmlFeatureNode { Name = "frontWheels", QualifiedName = "P::Vehicle::frontWheels", FeatureKeyword = "part", FeatureTyping = "Wheel", Multiplicity = "[2]", SupertypeNames = ["wheels"] };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Wheel"] = new SysmlDefinitionNode { Name = "Wheel", QualifiedName = "P::Wheel", DefinitionKeyword = "part def" },
                ["P::Vehicle"] = new SysmlDefinitionNode { Name = "Vehicle", QualifiedName = "P::Vehicle", DefinitionKeyword = "part def", Children = [wheels, frontWheels] },
                ["P::Vehicle::wheels"] = wheels,
                ["P::Vehicle::frontWheels"] = frontWheels
            }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: a solid hollow-triangle edge runs from frontWheels to wheels
        var subsettingEdge = Assert.Single(CollectLines(layout.Nodes), l => l.TargetEnd == EndMarkerStyle.HollowTriangle);
        Assert.True(RunsBetween(subsettingEdge, FindBox(layout.Nodes, "frontWheels"), FindBox(layout.Nodes, "wheels")));
    }

    /// <summary>
    ///     A usage that subsets itself draws no subsetting edge — the self-reference guard shared
    ///     with every other edge kind — and does not throw.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_SelfReferentialSubsetting_ProducesNoEdge()
    {
        // Arrange: a usage that names itself as its own subsetted feature
        var strategy = new GeneralViewLayoutStrategy();
        var wheels = new SysmlFeatureNode { Name = "wheels", QualifiedName = "P::Vehicle::wheels", FeatureKeyword = "part", SupertypeNames = ["wheels"] };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Vehicle"] = new SysmlDefinitionNode { Name = "Vehicle", QualifiedName = "P::Vehicle", DefinitionKeyword = "part def", Children = [wheels] },
                ["P::Vehicle::wheels"] = wheels
            }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: no hollow-triangle (subsetting) edge is emitted.
        Assert.DoesNotContain(CollectLines(layout.Nodes), l => l.TargetEnd == EndMarkerStyle.HollowTriangle);
    }

    /// <summary>
    ///     BuildLayout emits a companion <see cref="BoxShape.Note"/> box (plus a connecting plain
    ///     line) for a definition that carries an explicit <c>comment</c> annotation.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_AnnotatedDefinition_EmitsNoteBox()
    {
        // Arrange: a part def with one Comment annotation
        var strategy = new GeneralViewLayoutStrategy();
        var battery = new SysmlDefinitionNode
        {
            Name = "Battery",
            QualifiedName = "P::Battery",
            DefinitionKeyword = "part def",
            Annotations = [new SysmlAnnotation(SysmlAnnotationKind.Comment, "Nominal battery capacity is 5000mAh.")],
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["P::Battery"] = battery }
        };
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: a Note-shaped box is present, plus a dashed no-arrowhead connecting line (the
        // specification's annotation link)
        var boxes = CollectBoxes(layout.Nodes);
        var note = Assert.Single(boxes, b => b.Shape == BoxShape.Note);
        Assert.Contains(note.Compartments, c => c.Rows.Any(r => r.Contains("Nominal battery capacity")));

        var lines = CollectLines(layout.Nodes);
        Assert.Contains(lines, l => l.TargetEnd == EndMarkerStyle.None && l.LineStyle == LineStyle.Dashed);
        Assert.DoesNotContain(lines, l => l.TargetEnd == EndMarkerStyle.None && l.LineStyle == LineStyle.Solid);
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
    ///     BuildLayout combines multiple comment annotations on one element into a single Note box
    ///     (not one box per annotation), per this strategy's documented aggregation choice.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_MultipleAnnotations_ProduceOneNoteBox()
    {
        // Arrange: a part def with two comment annotations
        var strategy = new GeneralViewLayoutStrategy();
        var battery = new SysmlDefinitionNode
        {
            Name = "Battery",
            QualifiedName = "P::Battery",
            DefinitionKeyword = "part def",
            Annotations =
            [
                new SysmlAnnotation(SysmlAnnotationKind.Comment, "Primary power source."),
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
    ///     Builds a single-definition workspace whose part def carries one Documentation and one
    ///     Comment annotation, used by the documentation-visibility tests.
    /// </summary>
    private static SysmlWorkspace BuildDocumentedBatteryWorkspace(string docText) =>
        new()
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Battery"] = new SysmlDefinitionNode
                {
                    Name = "Battery",
                    QualifiedName = "P::Battery",
                    DefinitionKeyword = "part def",
                    Annotations =
                    [
                        new SysmlAnnotation(SysmlAnnotationKind.Documentation, docText),
                        new SysmlAnnotation(SysmlAnnotationKind.Comment, "TODO: revisit capacity."),
                    ],
                },
            },
        };

    /// <summary>
    ///     By default, exposed documentation is drawn as a <c>doc</c> compartment inside its owner's
    ///     box (SysML 8.2.3.4 documentation-compartment), word-wrapped into rows no wider than the
    ///     box, and never as a note; an explicit comment is still drawn as an attached note.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_DocumentationByDefault_RendersWrappedDocCompartment()
    {
        // Arrange: a long documentation string and a view with no view conditions
        var strategy = new GeneralViewLayoutStrategy();
        const string docText =
            "The battery is the primary power source for the vehicle and supplies every subsystem " +
            "with regulated power during all operating modes.";
        var workspace = BuildDocumentedBatteryWorkspace(docText);
        var context = new ViewContext("v", workspace, new SysmlViewNode { Name = "v", QualifiedName = "P::v" });
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: the Battery box carries a wrapped doc compartment preserving every word, sized
        // within the box; the comment note is present and does not repeat the documentation
        var boxes = CollectBoxes(layout.Nodes);
        var battery = Assert.Single(boxes, b => b.Label == "Battery");
        var doc = Assert.Single(battery.Compartments, c => c.Title == "doc");
        Assert.True(doc.Rows.Count > 1, "Expected the documentation to wrap onto several rows");
        Assert.Equal(docText, string.Join(" ", doc.Rows));
        var charWidth = options.Theme.FontSizeBody * 0.62;
        Assert.All(doc.Rows, r => Assert.True(r.Length * charWidth <= battery.Width, $"Row too wide: {r}"));
        var note = Assert.Single(boxes, b => b.Shape == BoxShape.Note);
        Assert.Contains(note.Compartments, c => c.Rows.Any(r => r.Contains("revisit capacity")));
        Assert.DoesNotContain(note.Compartments, c => c.Rows.Any(r => r.Contains("primary power source")));
    }

    /// <summary>
    ///     A view condition <c>filter not @SysML::Documentation;</c> (SysML 7.26.2) hides the
    ///     <c>doc</c> compartment, while the element itself and its comment note remain.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_FilterNotDocumentation_HidesDocButKeepsComment()
    {
        // Arrange
        var workspace = BuildDocumentedBatteryWorkspace("Primary power source.");
        var view = new SysmlViewNode { Name = "v", QualifiedName = "P::v", FilterExpressionTexts = ["not @SysML::Documentation"] };

        // Act
        var layout = new GeneralViewLayoutStrategy().BuildLayout(new ViewContext("v", workspace, view), new RenderOptions(Themes.Light));

        // Assert
        var boxes = CollectBoxes(layout.Nodes);
        var battery = Assert.Single(boxes, b => b.Label == "Battery");
        Assert.DoesNotContain(battery.Compartments, c => c.Title == "doc");
        Assert.Contains(boxes, b => b.Shape == BoxShape.Note);
    }

    /// <summary>
    ///     A positive view condition that Documentation does not satisfy
    ///     (<c>filter @SysML::PartDefinition;</c>) keeps the part definition but hides its
    ///     <c>doc</c> compartment and its comment note.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_PositiveFilterExcludingDocumentation_HidesDoc()
    {
        // Arrange
        var workspace = BuildDocumentedBatteryWorkspace("Primary power source.");
        var view = new SysmlViewNode { Name = "v", QualifiedName = "P::v", FilterExpressionTexts = ["@SysML::PartDefinition"] };

        // Act
        var layout = new GeneralViewLayoutStrategy().BuildLayout(new ViewContext("v", workspace, view), new RenderOptions(Themes.Light));

        // Assert
        var boxes = CollectBoxes(layout.Nodes);
        var battery = Assert.Single(boxes, b => b.Label == "Battery");
        Assert.DoesNotContain(battery.Compartments, c => c.Title == "doc");
        Assert.DoesNotContain(boxes, b => b.Shape == BoxShape.Note);
    }

    /// <summary>
    ///     A supertype reference that does not resolve (<c>X</c> with no import) listed before an
    ///     alias of the same target (<c>A</c> for <c>P::X</c>) draws only the alias's edge: the
    ///     unresolved reference must not be paired with the target the alias resolved to, in either
    ///     reference order.
    /// </summary>
    /// <param name="supertypes">The supertype list of <c>Sub</c>.</param>
    [Theory]
    [InlineData("X, A")]
    [InlineData("A, X")]
    public async Task GeneralViewLayoutStrategy_BuildLayout_UnresolvedSupertypeAndAliasOfSameTarget_DrawsOneEdge(
        string supertypes)
    {
        // Act: X is unresolved from Q (no import); A is an alias that resolves to P::X
        var layout = await LayOutViewVAsync($$"""
            package P { part def X; }
            package Q { alias A for P::X; part def Sub :> {{supertypes}}; }
            package Diagrams { view V { expose P::*; expose Q::*; } }
            """);

        // Assert: exactly one specialization edge (the alias's)
        Assert.Single(
            CollectLines(layout.Nodes), l => l.TargetEnd == EndMarkerStyle.HollowTriangle && l.LineStyle == LineStyle.Solid);
    }

    /// <summary>
    ///     A subsetted-feature reference that does not resolve (<c>slotX</c> with no import) listed
    ///     before an alias of the same feature draws only the alias's subsetting edge.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_UnresolvedSubsettingAndAliasOfSameFeature_DrawsOneEdge()
    {
        // Act: slotX is unresolved from Consumer; Alias resolves to Lib::Holder::slotX
        var layout = await LayOutViewVAsync("""
            package Lib { part def Holder { part slotX; } }
            package App {
                alias Alias for Lib::Holder::slotX;
                part def Consumer { part s :> slotX, Alias; }
            }
            package Diagrams { view V { expose Lib::**; expose App::**; } }
            """);

        // Assert: exactly one subsetting edge (the alias's)
        Assert.Single(CollectLines(layout.Nodes), l => l.TargetEnd == EndMarkerStyle.HollowTriangle);
    }

    /// <summary>
    ///     A qualified and a bare supertype reference sharing a simple name each draw their own
    ///     edge: for <c>part def C :&gt; Y::X, X;</c> where the bare <c>X</c> resolves (through an
    ///     import) to <c>P::X</c>, one edge goes to <c>Y::X</c> and one to <c>P::X</c>.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_QualifiedAndBareSupertypesSharingName_DrawsBothEdges()
    {
        // Act
        var layout = await LayOutViewVAsync("""
            package P { part def X; }
            package Y { part def X; }
            package Q { private import P::*; part def C :> Y::X, X; }
            package Diagrams { view V { expose P::*; expose Y::*; expose Q::*; } }
            """);

        // Assert: two distinct specialization edges
        var lines = CollectLines(layout.Nodes)
            .Where(l => l.TargetEnd == EndMarkerStyle.HollowTriangle && l.LineStyle == LineStyle.Solid)
            .ToList();
        Assert.Equal(2, lines.Count);
    }

    /// <summary>
    ///     When the semantic model identified edges only for some references (here only <c>A</c>),
    ///     an exact-text lookup of an unmatched reference is authoritative: it reports "known" with
    ///     no target (so the caller draws nothing) and never answers with another reference's
    ///     target merely because the target's name ends with the reference.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_TryGetResolvedTarget_IdentifiedEdges_MatchesExactTextOnly()
    {
        // Arrange: only the alias "A" resolved (to P::X); "X" did not
        var declaration = new SysmlDefinitionNode
        {
            Name = "Sub",
            QualifiedName = "Q::Sub",
            ResolvedEdges = [new SysmlEdge("Q::Sub", "P::X", SysmlEdgeKind.Supertype) { RawReference = "A" }],
        };

        // Act
        var forUnresolved = GeneralViewLayoutStrategy.TryGetResolvedTarget(declaration, SysmlEdgeKind.Supertype, "X", out var unresolvedTarget);
        var forAlias = GeneralViewLayoutStrategy.TryGetResolvedTarget(declaration, SysmlEdgeKind.Supertype, "A", out var aliasTarget);

        // Assert
        Assert.True(forUnresolved);
        Assert.Null(unresolvedTarget);
        Assert.True(forAlias);
        Assert.Equal("P::X", aliasTarget);
    }

    /// <summary>
    ///     With no declaration, no edges of the kind, or only edges that carry no raw reference
    ///     (hand-built workspaces, older data), the semantic model gave no identity: the helper
    ///     reports "not known" so the caller resolves the reference itself.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_TryGetResolvedTarget_NoIdentifiedEdges_ReportsNotKnown()
    {
        // Arrange
        var noEdges = new SysmlDefinitionNode { Name = "Sub", QualifiedName = "Q::Sub" };
        var unidentified = new SysmlDefinitionNode
        {
            Name = "Sub",
            QualifiedName = "Q::Sub",
            ResolvedEdges = [new SysmlEdge("Q::Sub", "P::X", SysmlEdgeKind.Supertype)],
        };
        var otherKind = new SysmlDefinitionNode
        {
            Name = "Sub",
            QualifiedName = "Q::Sub",
            ResolvedEdges = [new SysmlEdge("Q::Sub", "P::X", SysmlEdgeKind.Typing) { RawReference = "X" }],
        };

        // Act / Assert
        Assert.False(GeneralViewLayoutStrategy.TryGetResolvedTarget(null, SysmlEdgeKind.Supertype, "X", out var nullTarget));
        Assert.False(GeneralViewLayoutStrategy.TryGetResolvedTarget(noEdges, SysmlEdgeKind.Supertype, "X", out _));
        Assert.False(GeneralViewLayoutStrategy.TryGetResolvedTarget(unidentified, SysmlEdgeKind.Supertype, "X", out _));
        Assert.False(GeneralViewLayoutStrategy.TryGetResolvedTarget(otherKind, SysmlEdgeKind.Supertype, "X", out _));
        Assert.Null(nullTarget);
    }

    /// <summary>
    ///     A reference written twice (<c>X, X</c>) resolves to the same target both times: the
    ///     lookup answers the same target for the text (first matching edge), and a qualified text
    ///     is a different key from the bare text even when it names the same symbol.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_TryGetResolvedTarget_DuplicateAndQualifiedText_UsesExactKey()
    {
        // Arrange
        var declaration = new SysmlDefinitionNode
        {
            Name = "Sub",
            QualifiedName = "Q::Sub",
            ResolvedEdges =
            [
                new SysmlEdge("Q::Sub", "P::X", SysmlEdgeKind.Supertype) { RawReference = "X" },
                new SysmlEdge("Q::Sub", "P::X", SysmlEdgeKind.Supertype) { RawReference = "X" },
            ],
        };

        // Act
        var bare = GeneralViewLayoutStrategy.TryGetResolvedTarget(declaration, SysmlEdgeKind.Supertype, "X", out var bareTarget);
        var qualified = GeneralViewLayoutStrategy.TryGetResolvedTarget(declaration, SysmlEdgeKind.Supertype, "P::X", out var qualifiedTarget);

        // Assert
        Assert.True(bare);
        Assert.Equal("P::X", bareTarget);
        Assert.True(qualified);
        Assert.Null(qualifiedTarget);
    }

    /// <summary>
    ///     Loads <paramref name="sysml"/> and lays out the general view <c>Diagrams::V</c>.
    /// </summary>
    private static async Task<LayoutTree> LayOutViewVAsync(string sysml, NodePolicy? policy = null)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, sysml, TestContext.Current.CancellationToken);
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);
            Assert.NotNull(result.Workspace);
            var view = Assert.IsType<SysmlViewNode>(result.Workspace.Declarations["Diagrams::V"]);
            return new GeneralViewLayoutStrategy(policy ?? NodePolicy.Pilot).BuildLayout(
                new ViewContext("V", result.Workspace, view), new RenderOptions(Themes.Light));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>Documented part def used by the expose/inheritance documentation tests.</summary>
    private const string DocumentedPackageSysml = """
        package P {
            part def Battery {
                doc /* Primary power source. */
                comment /* Revisit capacity. */
            }
        }
        """;

    /// <summary>
    ///     A view inherits the view conditions of its view definition (SysML 7.26.2): a view typed
    ///     by a view definition owning <c>filter not @SysML::Documentation;</c> hides documentation.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_InheritedViewCondition_HidesDoc()
    {
        // Act
        var layout = await LayOutViewVAsync(DocumentedPackageSysml + """

            package Diagrams {
                view def NoDocs { filter not @SysML::Documentation; }
                view V : NoDocs { expose P::**; }
            }
            """);

        // Assert
        var battery = Assert.Single(CollectBoxes(layout.Nodes), b => b.Label == "Battery");
        Assert.DoesNotContain(battery.Compartments, c => c.Title == "doc");
    }

    /// <summary>
    ///     A view typed by a standard library view definition (<c>StandardViewDefinitions::GeneralView</c>,
    ///     whose node is deserialized from the bundled standard library) applies its own view
    ///     conditions without failing on the library definition.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ViewTypedByStandardViewDefinition_AppliesOwnConditions()
    {
        // Act
        var layout = await LayOutViewVAsync(DocumentedPackageSysml + """

            package Diagrams {
                view V : StandardViewDefinitions::GeneralView { expose P::**; filter not @SysML::Documentation; }
            }
            """);

        // Assert
        var battery = Assert.Single(CollectBoxes(layout.Nodes), b => b.Label == "Battery");
        Assert.DoesNotContain(battery.Compartments, c => c.Title == "doc");
    }

    /// <summary>
    ///     An expose filter (<c>expose P::**[not @SysML::Documentation];</c>, SysML 7.26.2) excludes
    ///     the exposed documentation while still exposing the definitions and comments.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ExposeFilterNotDocumentation_HidesDoc()
    {
        // Act
        var layout = await LayOutViewVAsync(DocumentedPackageSysml + """

            package Diagrams { view V { expose P::**[not @SysML::Documentation]; } }
            """);

        // Assert
        var boxes = CollectBoxes(layout.Nodes);
        var battery = Assert.Single(boxes, b => b.Label == "Battery");
        Assert.DoesNotContain(battery.Compartments, c => c.Title == "doc");
        Assert.Contains(boxes, b => b.Shape == BoxShape.Note);
    }

    /// <summary>
    ///     A bare <c>expose P::Battery;</c> exposes only the element, not its owned documentation
    ///     (SysML 8.3.26.3), whereas <c>expose P::**;</c> exposes the documentation too.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_MembershipExpose_ExposesDocOnlyWhenRecursive()
    {
        // Act
        var bare = await LayOutViewVAsync(DocumentedPackageSysml + """

            package Diagrams { view V { expose P::Battery; } }
            """);
        var recursive = await LayOutViewVAsync(DocumentedPackageSysml + """

            package Diagrams { view V { expose P::**; } }
            """);

        // Assert
        Assert.DoesNotContain(Assert.Single(CollectBoxes(bare.Nodes), b => b.Label == "Battery").Compartments, c => c.Title == "doc");
        Assert.Contains(Assert.Single(CollectBoxes(recursive.Nodes), b => b.Label == "Battery").Compartments, c => c.Title == "doc");
    }

    /// <summary>
    ///     WrapText hard-breaks a single word longer than the row limit and keeps every row within it.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_WrapText_LongWord_IsHardBroken()
    {
        // Act: wrap a paragraph containing a word longer than the limit
        var rows = GeneralViewLayoutStrategy.WrapText("short 123456789012 end", 5);

        // Assert: every row fits and the content is preserved in order
        Assert.All(rows, r => Assert.True(r.Length <= 5));
        Assert.Equal(["short", "12345", "67890", "12", "end"], rows);
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
    ///     BuildLayout renders an <c>enum def</c>'s literal values under the specification's
    ///     "enums" compartment title (not the naive "enum values" plural).
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_EnumDefLiteralValues_ProducesEnumsCompartment()
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
        Assert.Contains(box.Compartments, c => c.Title == "enums" && c.Rows.Contains("manual") && c.Rows.Contains("auto"));
        Assert.DoesNotContain(box.Compartments, c => c.Title == "enum values");
    }

    /// <summary>
    ///     Compartments are emitted in the canonical order attributes, enums, parts, ports followed
    ///     by every other keyword group in declaration order, regardless of the order the usages are
    ///     declared in; the <c>doc</c> compartment stays first. (Row-only policy: under the Pilot
    ///     policy the item, port and part usages are nodes of their own, not rows.)
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_Compartments_AreInCanonicalOrder()
    {
        // Arrange: a part def declaring an item, port, enum value, part, and attribute (in that
        // order) plus documentation
        var strategy = new GeneralViewLayoutStrategy(NodePolicy.RowsOnly);
        var def = new SysmlDefinitionNode
        {
            Name = "Thing",
            QualifiedName = "P::Thing",
            DefinitionKeyword = "part def",
            Annotations = [new SysmlAnnotation(SysmlAnnotationKind.Documentation, "Documented.")],
            Children =
            [
                new SysmlFeatureNode { Name = "cargo", QualifiedName = "P::Thing::cargo", FeatureKeyword = "item" },
                new SysmlFeatureNode { Name = "p", QualifiedName = "P::Thing::p", FeatureKeyword = "port" },
                new SysmlFeatureNode { Name = "e", QualifiedName = "P::Thing::e", FeatureKeyword = "enum value" },
                new SysmlFeatureNode { Name = "sub", QualifiedName = "P::Thing::sub", FeatureKeyword = "part" },
                new SysmlFeatureNode { Name = "mass", QualifiedName = "P::Thing::mass", FeatureKeyword = "attribute" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["P::Thing"] = def }
        };

        // Act
        var layout = strategy.BuildLayout(new ViewContext("v", workspace), new RenderOptions(Themes.Light));

        // Assert: doc first, then the canonical groups, then the remaining group
        var box = CollectBoxes(layout.Nodes).First(b => b.Label == "Thing");
        Assert.Equal(
            ["doc", "attributes", "enums", "parts", "ports", "items"],
            box.Compartments.Select(c => c.Title).ToList());
    }

    /// <summary>
    ///     A usage-level box (a top-level named usage) is drawn with rounded corners while a
    ///     definition keeps square corners.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_TopLevelUsage_IsRoundedAndDefinitionIsSquare()
    {
        // Arrange: a part def and a top-level part usage typed by it
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["Engine"] = new SysmlDefinitionNode { Name = "Engine", QualifiedName = "Engine", DefinitionKeyword = "part def" },
                ["engine1"] = new SysmlFeatureNode { Name = "engine1", QualifiedName = "engine1", FeatureKeyword = "part", FeatureTyping = "Engine" }
            }
        };

        // Act
        var layout = strategy.BuildLayout(new ViewContext("v", workspace), new RenderOptions(Themes.Light));

        // Assert: the usage is a rounded rectangle, the definition a plain rectangle
        var boxes = CollectBoxes(layout.Nodes);
        Assert.Equal(BoxShape.RoundedRectangle, boxes.Single(b => IsNamed(b, "engine1")).Shape);
        Assert.Equal(BoxShape.Rectangle, boxes.Single(b => b.Label == "Engine").Shape);
    }

    /// <summary>
    ///     A definition's name line shows its specialization and short name in the
    ///     specification's textual form (<c>&lt;short&gt; Name :&gt; Super1, Super2</c>); the box is
    ///     wide enough for the longer label.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_DefinitionNameLine_ShowsShortNameAndSupertypes()
    {
        // Arrange: a plain def, a specializing def, a short-named def, and one with both plus two supers
        var strategy = new GeneralViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Vehicle"] = new SysmlDefinitionNode { Name = "Vehicle", QualifiedName = "P::Vehicle", DefinitionKeyword = "part def" },
                ["P::Car"] = new SysmlDefinitionNode { Name = "Car", QualifiedName = "P::Car", DefinitionKeyword = "part def", SupertypeNames = ["Vehicle"] },
                ["P::PartDef2"] = new SysmlDefinitionNode { Name = "PartDef2", ShortName = "PD2", QualifiedName = "P::PartDef2", DefinitionKeyword = "part def" },
                ["P::Truck"] = new SysmlDefinitionNode
                {
                    Name = "Truck",
                    ShortName = "T",
                    QualifiedName = "P::Truck",
                    DefinitionKeyword = "part def",
                    SupertypeNames = ["Vehicle", "Hauler"]
                }
            }
        };
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(new ViewContext("v", workspace), options);

        // Assert: labels follow the textual notation
        var boxes = CollectBoxes(layout.Nodes);
        Assert.Contains(boxes, b => b.Label == "Vehicle");
        Assert.Contains(boxes, b => b.Label == "Car :> Vehicle");
        Assert.Contains(boxes, b => b.Label == "<PD2> PartDef2");
        var truck = Assert.Single(boxes, b => b.Label == "<T> Truck :> Vehicle, Hauler");

        // Assert: the box is sized to its label
        var minWidth = (truck.Label!.Length * options.Theme.FontSizeTitle * 0.62) + (2.0 * options.Theme.LabelPadding);
        Assert.True(truck.Width >= minWidth - 1e-6, $"box width {truck.Width} must fit its label ({minWidth}).");
    }

    /// <summary>FormatNameLine omits every piece that is null or empty.</summary>
    [Fact]
    public void GeneralViewLayoutStrategy_FormatNameLine_AllOptionalsNull_ReturnsNameOnly()
    {
        // Arrange / Act: only the name is supplied
        var line = GeneralViewLayoutStrategy.FormatNameLine(null, "Car");

        // Assert
        Assert.Equal("Car", line);
    }

    /// <summary>FormatNameLine renders each optional piece on its own in the textual notation.</summary>
    [Fact]
    public void GeneralViewLayoutStrategy_FormatNameLine_EachOptionalAlone_UsesTextualNotation()
    {
        // Arrange / Act / Assert: short name, supertypes, typing, multiplicity individually; empty
        // values behave like null
        Assert.Equal("<c1> Car", GeneralViewLayoutStrategy.FormatNameLine("c1", "Car"));
        Assert.Equal("Car :> A, B", GeneralViewLayoutStrategy.FormatNameLine(null, "Car", supertypes: ["A", "B"]));
        Assert.Equal("car : Car", GeneralViewLayoutStrategy.FormatNameLine(null, "car", typing: "Car"));
        Assert.Equal("car [2]", GeneralViewLayoutStrategy.FormatNameLine(null, "car", multiplicity: "[2]"));
        Assert.Equal("Car", GeneralViewLayoutStrategy.FormatNameLine(string.Empty, "Car", supertypes: [], typing: string.Empty, multiplicity: string.Empty));
    }

    /// <summary>FormatNameLine combines every piece in short name, name, supertypes, typing, multiplicity order.</summary>
    [Fact]
    public void GeneralViewLayoutStrategy_FormatNameLine_AllPieces_CombinesInSpecOrder()
    {
        // Arrange / Act
        var line = GeneralViewLayoutStrategy.FormatNameLine("r", "ram", ["Base"], "Memory", "[1..*]");

        // Assert
        Assert.Equal("<r> ram :> Base : Memory [1..*]", line);
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
    ///     is still rendered normally, but in place of an <c>Inner</c> box it stays a leaf (no
    ///     children) whose last compartment is an untitled <c>…</c> row — the specification's
    ///     truncation marker — mirroring how a depth-truncated package folder's contents are
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

        // Assert: Outer is a leaf (no placeholder child box) ending in an untitled "…" compartment
        Assert.Empty(outerBox.Children);
        var last = outerBox.Compartments[^1];
        Assert.Null(last.Title);
        Assert.Equal(["\u2026"], last.Rows);
    }

    /// <summary>
    ///     A truncated definition that already has compartments keeps them and gains the "…" row as a
    ///     separate trailing compartment; the box is taller than the same definition untruncated.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_TruncatedDefinitionWithCompartments_AppendsEllipsisRow()
    {
        // Arrange: Outer owns an attribute and a nested definition
        var attributeOwner = new SysmlDefinitionNode
        {
            Name = "Outer",
            QualifiedName = "Sys::Outer",
            DefinitionKeyword = "part def",
            Children = [new SysmlFeatureNode { Name = "mass", QualifiedName = "Sys::Outer::mass", FeatureKeyword = "attribute", FeatureTyping = "Real" }]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["Sys::Outer"] = attributeOwner,
                ["Sys::Outer::Inner"] = new SysmlDefinitionNode { Name = "Inner", QualifiedName = "Sys::Outer::Inner", DefinitionKeyword = "part def" }
            }
        };
        var leafWorkspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["Sys::Outer"] = attributeOwner }
        };
        var strategy = new GeneralViewLayoutStrategy();

        // Act: the truncated layout, and a baseline in which Outer has the same attribute but no
        // nested definition (so it is an ordinary leaf)
        var truncated = strategy.BuildLayout(new ViewContext("v", workspace), new RenderOptions(Themes.Light, DepthLimit: 2));
        var baseline = strategy.BuildLayout(new ViewContext("v", leafWorkspace), new RenderOptions(Themes.Light));

        // Assert: attributes compartment kept, "…" row appended as its own untitled compartment
        var outer = CollectBoxes(truncated.Nodes).Single(b => b.Label == "Outer");
        Assert.Equal(["attributes", null], outer.Compartments.Select(c => c.Title).ToList());
        Assert.Equal(["\u2026"], outer.Compartments[^1].Rows);

        // Assert: the extra row makes the box taller than the plain leaf rendering
        var leaf = CollectBoxes(baseline.Nodes).Single(b => b.Label == "Outer");
        Assert.Single(leaf.Compartments);
        Assert.True(outer.Height > leaf.Height, $"truncated height {outer.Height} must exceed leaf height {leaf.Height}.");
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

        // Assert: the folder carries a bare "…" ellipsis label (the hidden count is not shown)
        var indicator = Assert.IsType<LayoutLabel>(Assert.Single(folder.Children));
        Assert.Equal("\u2026", indicator.Text);
    }

    /// <summary>
    ///     Regression test (reflection-based, since the recursive worker is private) for the
    ///     reviewer-confirmed pairing bug in <c>GeneralViewLayoutStrategy</c>'s recursive
    ///     <c>DecorateTruncated</c> worker: it previously matched each placed <see cref="LayoutBox"/>
    ///     against a scope's <see cref="LayoutGraphNode"/> list using a single shared loop index, but
    ///     a scope's placed node list can intersperse <see cref="LayoutLine"/> entries (routed
    ///     intra-scope edges) among its boxes at arbitrary positions — every <see cref="LayoutLine"/>
    ///     encountered before a box permanently shifted the box-to-graph-node pairing by one, so the
    ///     wrong <see cref="LayoutGraphNode"/> (and therefore the wrong truncation/hidden-count
    ///     decoration) got matched to each subsequent box. This test builds a minimal fixture with
    ///     one <see cref="LayoutLine"/> placed before two <see cref="LayoutBox"/> entries, only the
    ///     second of which corresponds to a truncated <see cref="LayoutGraphNode"/>, and invokes the
    ///     private recursive worker directly via reflection — there is no public seam to reach it, and
    ///     reproducing this box/line ordering deterministically through the public
    ///     <c>BuildLayout</c>/<c>DemaConsulting.Rendering</c> layout-algorithm pipeline is not
    ///     practical since box/line placement ordering is an internal detail of that external
    ///     package. With the fix, the leading line no longer perturbs the pairing: the first box
    ///     (matching the non-truncated node) is left unchanged, and only the second box (matching the
    ///     truncated node) is decorated with the ellipsis label.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_DecorateTruncated_LineBeforeBoxes_DoesNotBreakBoxToNodePairing()
    {
        // Arrange: two graph nodes at this scope — nodeA (kept) and nodeB (truncated, hiding 3 items)
        var graph = new LayoutGraph();
        var nodeA = graph.AddNode("A", 100, 60);
        nodeA.Label = "A";
        var nodeB = graph.AddNode("B", 100, 60);
        nodeB.Label = "B";

        // A placed line (an intra-scope routed edge) appears before either box in the placed-node list
        var line = new LayoutLine(
            Waypoints: [],
            SourceEnd: EndMarkerStyle.None,
            TargetEnd: EndMarkerStyle.None,
            LineStyle: LineStyle.Solid,
            MidpointLabel: null);

        var boxA = new LayoutBox(
            X: 0, Y: 0, Width: 100, Height: 60, Label: "A", Depth: 1, Shape: BoxShape.Rectangle,
            Compartments: [], Children: [], Keyword: null, RoundedCornerRadius: null,
            FolderTabWidth: null, FolderTabHeight: null, ContentInsetLeft: 0, ContentInsetRight: 0,
            ContentInsetTop: 0, ContentInsetBottom: 0, CenterTitle: false);
        var boxB = boxA with { Label = "B" };

        var placed = new List<LayoutNode> { line, boxA, boxB };
        var graphNodes = new List<LayoutGraphNode> { nodeA, nodeB };
        var hiddenByNode = new Dictionary<LayoutGraphNode, (int HiddenCount, double TitleOffset)>
        {
            [nodeB] = (3, 0.0)
        };

        var method = typeof(GeneralViewLayoutStrategy)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(m => m.Name == "DecorateTruncated" && m.GetParameters().Length == 4
                         && m.GetParameters()[0].ParameterType == typeof(IReadOnlyList<LayoutNode>));

        // Act
        var result = (List<LayoutNode>)method.Invoke(
            null, [placed, graphNodes, hiddenByNode, Themes.Light])!;

        // Assert: the leading line is preserved untouched
        Assert.Same(line, result[0]);

        // Assert: boxA (paired with the non-truncated nodeA) is unchanged — not decorated
        var resultBoxA = Assert.IsType<LayoutBox>(result[1]);
        Assert.Equal("A", resultBoxA.Label);
        Assert.Empty(resultBoxA.Children);

        // Assert: boxB (paired with the truncated nodeB) carries the bare "…" ellipsis decoration
        var resultBoxB = Assert.IsType<LayoutBox>(result[2]);
        Assert.Equal("B", resultBoxB.Label);
        var indicatorB = Assert.IsType<LayoutLabel>(Assert.Single(resultBoxB.Children));
        Assert.Equal("\u2026", indicatorB.Text);
    }

    /// <summary>
    ///     Loads <paramref name="sysml"/> and lays out an unscoped view (no <c>expose</c>: every
    ///     non-stdlib definition and package is drawn) with the general view strategy.
    /// </summary>
    private static async Task<LayoutTree> LayOutUnscopedAsync(string sysml, int depthLimit = 0, NodePolicy? policy = null)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, sysml, TestContext.Current.CancellationToken);
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);
            Assert.NotNull(result.Workspace);
            return new GeneralViewLayoutStrategy(policy ?? NodePolicy.Pilot).BuildLayout(
                new ViewContext("v", result.Workspace), new RenderOptions(Themes.Light, DepthLimit: depthLimit));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>Returns the single box labeled <paramref name="label"/> (optionally of <paramref name="shape"/>) anywhere in <paramref name="nodes"/>.</summary>
    private static LayoutBox FindBox(IReadOnlyList<LayoutNode> nodes, string label, BoxShape? shape = null) =>
        Assert.Single(CollectBoxes(nodes), b => IsNamed(b, label) && (shape is null || b.Shape == shape));

    /// <summary>
    ///     True when the box's name line is <paramref name="name"/> exactly or <paramref name="name"/>
    ///     followed by a <c>:</c>-introduced clause (a specializing definition's label reads
    ///     <c>Name :&gt; Super</c>, a typed usage's <c>name : Type</c>).
    /// </summary>
    private static bool IsNamed(LayoutBox box, string name) =>
        box.Label == name || (box.Label?.StartsWith(name + " :", StringComparison.Ordinal) ?? false);

    /// <summary>Returns the hollow-triangle (specialization) lines among the direct nodes of a scope.</summary>
    private static IReadOnlyList<LayoutLine> DirectSpecializationLines(IReadOnlyList<LayoutNode> scopeNodes) =>
        [.. scopeNodes.OfType<LayoutLine>().Where(l => l.TargetEnd == EndMarkerStyle.HollowTriangle)];

    /// <summary>
    ///     True when the point lies on the border of <paramref name="box"/> (within a 2-pixel tolerance
    ///     for stroke and marker geometry): inside the box's outline and next to one of its four edges.
    /// </summary>
    private static bool OnBorder(double x, double y, LayoutBox box)
    {
        const double tolerance = 2.0;
        var inside = x >= box.X - tolerance && x <= box.X + box.Width + tolerance &&
                     y >= box.Y - tolerance && y <= box.Y + box.Height + tolerance;
        var nearEdge = Math.Abs(x - box.X) <= tolerance || Math.Abs(x - (box.X + box.Width)) <= tolerance ||
                       Math.Abs(y - box.Y) <= tolerance || Math.Abs(y - (box.Y + box.Height)) <= tolerance;
        return inside && nearEdge;
    }

    /// <summary>True when the line starts on <paramref name="source"/>'s border and ends on <paramref name="target"/>'s.</summary>
    private static bool RunsBetween(LayoutLine line, LayoutBox source, LayoutBox target) =>
        OnBorder(line.Waypoints[0].X, line.Waypoints[0].Y, source) &&
        OnBorder(line.Waypoints[^1].X, line.Waypoints[^1].Y, target);

    /// <summary>
    ///     A package nested in another package is drawn inside its owner's folder (issue #87), not as a
    ///     separate folder beside it.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_NestedPackage_RendersInsideOwnerFolder()
    {
        // Act
        var layout = await LayOutUnscopedAsync("package P { part def A; package Sub { part def B; } }");

        // Assert: one root folder P, holding the A box and the Sub folder (which holds B)
        var root = Assert.Single(layout.Nodes.OfType<LayoutBox>());
        Assert.Equal("P", root.Label);
        Assert.Equal(BoxShape.Folder, root.Shape);
        Assert.Contains(root.Children.OfType<LayoutBox>(), b => b.Label == "A");
        var sub = Assert.Single(root.Children.OfType<LayoutBox>(), b => b.Shape == BoxShape.Folder);
        Assert.Equal("Sub", sub.Label);
        Assert.Contains(sub.Children.OfType<LayoutBox>(), b => b.Label == "B");
        Assert.Single(CollectBoxes(layout.Nodes), b => b.Label == "Sub");
    }

    /// <summary>
    ///     An enclosing package that owns no definitions directly still gets its folder when a nested
    ///     package is drawn, at every nesting level.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_NestedPackage_ParentWithoutDirectItems_CreatesEnclosingFolder()
    {
        // Act
        var layout = await LayOutUnscopedAsync("package Outer { package Inner { package Deep { part def X; } } }");

        // Assert: Outer > Inner > Deep > X, three folders and nothing else at the root
        var outer = Assert.Single(layout.Nodes.OfType<LayoutBox>());
        Assert.Equal("Outer", outer.Label);
        var inner = Assert.Single(outer.Children.OfType<LayoutBox>());
        Assert.Equal("Inner", inner.Label);
        Assert.Equal(BoxShape.Folder, inner.Shape);
        var deep = Assert.Single(inner.Children.OfType<LayoutBox>());
        Assert.Equal("Deep", deep.Label);
        Assert.Equal(BoxShape.Folder, deep.Shape);
        Assert.Equal("X", Assert.Single(deep.Children.OfType<LayoutBox>()).Label);
        Assert.Equal(3, CollectBoxes(layout.Nodes).Count(b => b.Shape == BoxShape.Folder));
    }

    /// <summary>
    ///     With <c>expose P::**;</c> the nested exposed package sits inside P's folder; with
    ///     <c>expose P::Sub::**;</c> only Sub is exposed, so it is drawn at the root with no P folder.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ScopedExpose_NestedExposedPackageInsideOwner()
    {
        // Arrange
        const string sysml = """
            package P { part def A; package Sub { part def B; } }
            package Diagrams {
                view Outer { expose P::**; }
                view InnerOnly { expose P::Sub::**; }
            }
            """;

        // Act
        var outer = await LayOutDeclaredViewAsync(sysml, "Outer");
        var innerOnly = await LayOutDeclaredViewAsync(sysml, "InnerOnly");

        // Assert: nested inside P in the first view
        var p = Assert.Single(outer.Nodes.OfType<LayoutBox>());
        Assert.Equal("P", p.Label);
        Assert.Contains(p.Children.OfType<LayoutBox>(), b => b.Label == "Sub" && b.Shape == BoxShape.Folder);

        // Assert: Sub at the root, P not drawn, in the second
        var sub = Assert.Single(innerOnly.Nodes.OfType<LayoutBox>());
        Assert.Equal("Sub", sub.Label);
        Assert.DoesNotContain(CollectBoxes(innerOnly.Nodes), b => b.Label == "P");
    }

    /// <summary>
    ///     A specialization between definitions of two sibling top-level packages is drawn at the root
    ///     scope (their lowest common ancestor) and runs between the two definition boxes themselves,
    ///     not between the package folder borders.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_CrossPackageEdge_SiblingPackages_DeclaredAtRootAndRoutedToNestedBoxes()
    {
        // Act
        var layout = await LayOutUnscopedAsync("package P1 { part def A; } package P2 { part def B :> P1::A; }");

        // Assert
        var line = Assert.Single(DirectSpecializationLines(layout.Nodes));
        Assert.True(RunsBetween(line, FindBox(layout.Nodes, "B"), FindBox(layout.Nodes, "A")));
    }

    /// <summary>
    ///     An edge between a definition in a nested package and one in the owner package is declared in
    ///     the owner's folder scope (the nested package is below it), and runs between the two boxes.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_CrossPackageEdge_NestedVersusOuter_DeclaredInOuterScope()
    {
        // Act
        var layout = await LayOutUnscopedAsync("package P { part def A; package Sub { part def B :> A; } }");

        // Assert: the line belongs to P's scope - not the root, not Sub's
        var p = FindBox(layout.Nodes, "P");
        var sub = FindBox(layout.Nodes, "Sub");
        Assert.Empty(DirectSpecializationLines(layout.Nodes));
        Assert.Empty(DirectSpecializationLines(sub.Children));
        var line = Assert.Single(DirectSpecializationLines(p.Children));
        Assert.True(RunsBetween(line, FindBox(layout.Nodes, "B"), FindBox(layout.Nodes, "A")));
    }

    /// <summary>An edge between two sibling nested packages is declared in their common parent's scope.</summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_CrossPackageEdge_SiblingNestedPackages_DeclaredInCommonParent()
    {
        // Act
        var layout = await LayOutUnscopedAsync(
            "package P { package S1 { part def A; } package S2 { part def B :> S1::A; } }");

        // Assert
        var p = FindBox(layout.Nodes, "P");
        Assert.Empty(DirectSpecializationLines(layout.Nodes));
        Assert.Empty(DirectSpecializationLines(FindBox(layout.Nodes, "S1").Children));
        Assert.Empty(DirectSpecializationLines(FindBox(layout.Nodes, "S2").Children));
        var line = Assert.Single(DirectSpecializationLines(p.Children));
        Assert.True(RunsBetween(line, FindBox(layout.Nodes, "B"), FindBox(layout.Nodes, "A")));
    }

    /// <summary>
    ///     An edge from a definition three packages deep to a definition in another top-level package
    ///     is declared at the root (their lowest common ancestor) and drawn between the two boxes,
    ///     even beside an unpackaged definition.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_CrossPackageEdge_DeepNestingAcrossTopLevelPackages_DrawsEdge()
    {
        // Act
        var layout = await LayOutUnscopedAsync(
            "package O { package M { package D { part def X :> Q::Y; } } } package Q { part def Y; } part def Loose;");

        // Assert
        var line = Assert.Single(DirectSpecializationLines(layout.Nodes));
        Assert.True(RunsBetween(line, FindBox(layout.Nodes, "X"), FindBox(layout.Nodes, "Y")));
        Assert.Contains(layout.Nodes.OfType<LayoutBox>(), b => b.Label == "Loose");
    }

    /// <summary>
    ///     A definition that contains a nested definition and is related to it (here through the typed
    ///     feature) has the container's own enclosing scope as its lowest common ancestor; the layout
    ///     completes without error and draws both boxes.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_EdgeBetweenContainerAndOwnDescendant_DoesNotThrow()
    {
        // Act
        var layout = await LayOutUnscopedAsync("part def Car { part def Eng; part e : Eng; }");

        // Assert
        var car = FindBox(layout.Nodes, "Car");
        Assert.Contains(car.Children.OfType<LayoutBox>(), b => b.Label == "Eng");
    }

    /// <summary>The model for the nested-package depth-limit tests: P owns A and a Sub package of B and C.</summary>
    private const string DepthLimitedNestedSysml = "package P { part def A; package Sub { part def B; part def C; } }";

    /// <summary>
    ///     Each enclosing package counts as one nesting level, so a depth limit of 1 collapses the top
    ///     folder (counting the nested package's contents too), a limit of 2 keeps the owner's own
    ///     definitions but collapses the nested package, and a limit of 3 (or none) shows everything.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_NestedPackages_DepthLimit_CountsPackageNesting()
    {
        // Act
        var limitOne = await LayOutUnscopedAsync(DepthLimitedNestedSysml, depthLimit: 1);
        var limitTwo = await LayOutUnscopedAsync(DepthLimitedNestedSysml, depthLimit: 2);
        var limitThree = await LayOutUnscopedAsync(DepthLimitedNestedSysml, depthLimit: 3);
        var unlimited = await LayOutUnscopedAsync(DepthLimitedNestedSysml);

        // Assert: limit 1 - P collapses, hiding A, Sub, B and C behind a bare "…" label
        var folderOne = FindBox(limitOne.Nodes, "P");
        Assert.Equal("\u2026", Assert.IsType<LayoutLabel>(Assert.Single(folderOne.Children)).Text);
        Assert.DoesNotContain(CollectBoxes(limitOne.Nodes), b => b.Label is "A" or "Sub" or "B" or "C");

        // Assert: limit 2 - A is drawn, Sub collapses to "…"
        var folderTwo = FindBox(limitTwo.Nodes, "P");
        Assert.Contains(folderTwo.Children.OfType<LayoutBox>(), b => b.Label == "A");
        var subTwo = FindBox(limitTwo.Nodes, "Sub");
        Assert.Equal("\u2026", Assert.IsType<LayoutLabel>(Assert.Single(subTwo.Children)).Text);
        Assert.DoesNotContain(CollectBoxes(limitTwo.Nodes), b => b.Label is "B" or "C");

        // Assert: limit 3 and no limit - everything is drawn with no truncation label
        foreach (var layout in new[] { limitThree, unlimited })
        {
            var sub = FindBox(layout.Nodes, "Sub");
            Assert.Equal(["B", "C"], sub.Children.OfType<LayoutBox>().Select(b => b.Label).Order().ToArray());
            Assert.DoesNotContain(CollectNodes(layout.Nodes), n => n is LayoutLabel { Text: "\u2026" });
        }
    }

    /// <summary>An edge to a definition hidden inside a depth-truncated nested package is dropped without error.</summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_NestedPackages_DepthLimit_EdgeToTruncatedPackageIsDropped()
    {
        // Act
        var layout = await LayOutUnscopedAsync(
            "package P { part def A; package Sub { part def B :> P::A; } }", depthLimit: 2);

        // Assert: A is drawn, Sub is collapsed, and no specialization line exists
        FindBox(layout.Nodes, "A");
        Assert.DoesNotContain(CollectBoxes(layout.Nodes), b => b.Label == "B");
        Assert.Empty(CollectLines(layout.Nodes));
    }

    /// <summary>Recursively collects every node (boxes, lines, labels) from a node list.</summary>
    private static IReadOnlyList<LayoutNode> CollectNodes(IReadOnlyList<LayoutNode> nodes)
    {
        var result = new List<LayoutNode>();
        void Walk(IReadOnlyList<LayoutNode> ns)
        {
            foreach (var n in ns)
            {
                result.Add(n);
                if (n is LayoutBox box)
                {
                    Walk(box.Children);
                }
            }
        }

        Walk(nodes);
        return result;
    }

    /// <summary>Builds a minimal placed box for the <c>DecorateTruncated</c> fixtures.</summary>
    private static LayoutBox MakeFixtureBox(string label, BoxShape shape, string? keyword, params LayoutNode[] children) =>
        new(
            X: 0, Y: 0, Width: 100, Height: 60, Label: label, Depth: 1, Shape: shape,
            Compartments: [], Children: children, Keyword: keyword, RoundedCornerRadius: null,
            FolderTabWidth: null, FolderTabHeight: null, ContentInsetLeft: 0, ContentInsetRight: 0,
            ContentInsetTop: 0, ContentInsetBottom: 0, CenterTitle: false);

    /// <summary>Invokes the private recursive <c>DecorateTruncated</c> worker.</summary>
    private static List<LayoutNode> InvokeDecorateTruncated(
        List<LayoutNode> placed,
        List<LayoutGraphNode> graphNodes,
        Dictionary<LayoutGraphNode, (int HiddenCount, double TitleOffset)> hiddenByNode)
    {
        var method = typeof(GeneralViewLayoutStrategy)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(m => m.Name == "DecorateTruncated" && m.GetParameters().Length == 4
                         && m.GetParameters()[0].ParameterType == typeof(IReadOnlyList<LayoutNode>));
        return (List<LayoutNode>)method.Invoke(null, [placed, graphNodes, hiddenByNode, Themes.Light])!;
    }

    /// <summary>
    ///     The ellipsis decoration is paired with its box by the node's identity (label, keyword,
    ///     shape), not by position: with the boxes placed in a different order than the graph nodes
    ///     (and a line among them), and with a nested container whose children are permuted too, only
    ///     the box standing for the truncated node is decorated.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_DecorateTruncated_BoxOrderDiffersFromNodeOrder_DecoratesMatchingNode()
    {
        // Arrange: root scope nodes A, P (a folder container holding X and truncated Y), B (truncated)
        var graph = new LayoutGraph();
        var nodeA = graph.AddNode("A", 100, 60);
        nodeA.Label = "A";
        nodeA.Keyword = "part def";
        nodeA.Shape = BoxShape.Rectangle;
        var nodeP = graph.AddNode("folder:P", 0, 0);
        nodeP.Label = "P";
        nodeP.Keyword = "package";
        nodeP.Shape = BoxShape.Folder;
        var nodeX = nodeP.Children.AddNode("P::X", 100, 60);
        nodeX.Label = "X";
        nodeX.Keyword = "part def";
        nodeX.Shape = BoxShape.Rectangle;
        var nodeY = nodeP.Children.AddNode("folder:P::Y", 100, 60);
        nodeY.Label = "Y";
        nodeY.Keyword = "package";
        nodeY.Shape = BoxShape.Folder;
        var nodeB = graph.AddNode("folder:B", 100, 60);
        nodeB.Label = "B";
        nodeB.Keyword = "package";
        nodeB.Shape = BoxShape.Folder;

        var line = new LayoutLine(
            Waypoints: [], SourceEnd: EndMarkerStyle.None, TargetEnd: EndMarkerStyle.None,
            LineStyle: LineStyle.Solid, MidpointLabel: null);

        // The engine order is reversed relative to the graph order, at both scopes
        var placedP = MakeFixtureBox(
            "P", BoxShape.Folder, "package",
            MakeFixtureBox("Y", BoxShape.Folder, "package"),
            line,
            MakeFixtureBox("X", BoxShape.Rectangle, "part def"));
        var placed = new List<LayoutNode>
        {
            MakeFixtureBox("B", BoxShape.Folder, "package"),
            line,
            placedP,
            MakeFixtureBox("A", BoxShape.Rectangle, "part def"),
        };
        var hiddenByNode = new Dictionary<LayoutGraphNode, (int HiddenCount, double TitleOffset)>
        {
            [nodeB] = (5, 0.0),
            [nodeY] = (2, 0.0),
        };

        // Act
        var result = InvokeDecorateTruncated(placed, [nodeA, nodeP, nodeB], hiddenByNode);

        // Assert: root scope - only B carries a label
        Assert.Equal("\u2026", Assert.IsType<LayoutLabel>(Assert.Single(Assert.IsType<LayoutBox>(result[0]).Children)).Text);
        Assert.Empty(Assert.IsType<LayoutBox>(result[3]).Children);

        // Assert: nested scope - only Y carries a label
        var resultP = Assert.IsType<LayoutBox>(result[2]);
        var resultY = Assert.IsType<LayoutBox>(resultP.Children[0]);
        Assert.Equal("\u2026", Assert.IsType<LayoutLabel>(Assert.Single(resultY.Children)).Text);
        Assert.Empty(Assert.IsType<LayoutBox>(resultP.Children[2]).Children);
    }

    /// <summary>
    ///     Two lines whose final points share an X within one anchor spacing but that end on different
    ///     boxes are still compared: the crossing between them is counted.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_CountLineCrossings_ArrivalsOnDifferentBoxes_CountsCrossing()
    {
        // Arrange: two stacked boxes whose left faces share X = 100, and two routes that cross at (70, 45)
        var upper = MakeFixtureBox("Upper", BoxShape.Rectangle, "part def") with { X = 100, Y = 0, Width = 60, Height = 50 };
        var lower = MakeFixtureBox("Lower", BoxShape.Rectangle, "part def") with { X = 100, Y = 60, Width = 60, Height = 50 };
        var toUpper = MakeFixtureLine([new Point2D(40, 120), new Point2D(40, 45), new Point2D(100, 45)]);
        var toLower = MakeFixtureLine([new Point2D(70, 0), new Point2D(70, 65), new Point2D(100, 65)]);

        // Act
        var crossings = CountLineCrossings([upper, lower, toUpper, toLower]);

        // Assert
        Assert.Equal(1, crossings);
    }

    /// <summary>
    ///     Two lines that end side by side on the same face of the same box are not compared (the
    ///     engine's anchor order there is a tie the strategy does not control).
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_CountLineCrossings_ArrivalsOnSameFace_AreNotCompared()
    {
        // Arrange: one box and two routes that cross at (70, 65) and end on its left face
        var box = MakeFixtureBox("Box", BoxShape.Rectangle, "part def") with { X = 100, Y = 40, Width = 60, Height = 60 };
        var first = MakeFixtureLine([new Point2D(40, 100), new Point2D(40, 65), new Point2D(100, 65)]);
        var second = MakeFixtureLine([new Point2D(70, 0), new Point2D(70, 80), new Point2D(100, 80)]);

        // Act
        var crossings = CountLineCrossings([box, first, second]);

        // Assert
        Assert.Equal(0, crossings);
    }

    /// <summary>A solid line with the given absolute waypoints.</summary>
    private static LayoutLine MakeFixtureLine(IReadOnlyList<Point2D> waypoints) =>
        new(
            Waypoints: waypoints, SourceEnd: EndMarkerStyle.None, TargetEnd: EndMarkerStyle.OpenChevron,
            LineStyle: LineStyle.Solid, MidpointLabel: null);

    /// <summary>
    ///     Counts the places where two different lines cross each other properly (an interior point of
    ///     a segment of one line lies strictly inside a segment of the other). Lines that merely touch,
    ///     share an endpoint or run along the same line are not counted; every line's waypoints are absolute.
    ///     Two lines that arrive on the same face of the same box (their final waypoints lie on that
    ///     face, share an X and are within one anchor spacing of each other) are not compared: which of
    ///     them takes the upper anchor is a tie the layout engine (0.3.0-beta.3) resolves by the other endpoints' heights rather
    ///     than by the side the route approaches from, so it can interleave two arrivals independently of
    ///     the ordering this strategy controls.
    /// </summary>
    private static int CountLineCrossings(IReadOnlyList<LayoutNode> nodes)
    {
        var all = CollectNodes(nodes);
        var lines = all.OfType<LayoutLine>().ToList();
        var boxes = all.OfType<LayoutBox>().ToList();
        var crossings = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            for (var j = i + 1; j < lines.Count; j++)
            {
                if (ArriveOnSameFace(lines[i], lines[j], boxes))
                {
                    continue;
                }

                crossings += CountSegmentCrossings(lines[i], lines[j]);
            }
        }

        return crossings;
    }

    /// <summary>
    ///     Whether two lines end side by side on one face of one box: both final points lie on the
    ///     same face of the same box, share an X and are within 20 px (one anchor spacing).
    /// </summary>
    private static bool ArriveOnSameFace(LayoutLine first, LayoutLine second, IReadOnlyList<LayoutBox> boxes)
    {
        var (a, b) = (first.Waypoints[^1], second.Waypoints[^1]);
        if (Math.Abs(a.X - b.X) >= 0.5 || Math.Abs(a.Y - b.Y) > 20)
        {
            return false;
        }

        var faceA = FaceOf(a, boxes);
        var faceB = FaceOf(b, boxes);
        return faceA is not null && faceB is not null
            && ReferenceEquals(faceA.Value.Box, faceB.Value.Box) && faceA.Value.Face == faceB.Value.Face;
    }

    /// <summary>
    ///     The box and face (0 left, 1 right, 2 top, 3 bottom) an endpoint lies on, or null when it lies
    ///     on no box's outline. The innermost (smallest) box wins when outlines coincide.
    /// </summary>
    private static (LayoutBox Box, int Face)? FaceOf(Point2D point, IReadOnlyList<LayoutBox> boxes)
    {
        const double Tolerance = 0.5;
        foreach (var box in boxes.OrderBy(b => b.Width * b.Height))
        {
            var withinX = point.X >= box.X - Tolerance && point.X <= box.X + box.Width + Tolerance;
            var withinY = point.Y >= box.Y - Tolerance && point.Y <= box.Y + box.Height + Tolerance;
            if (withinY && Math.Abs(point.X - box.X) <= Tolerance)
            {
                return (box, 0);
            }

            if (withinY && Math.Abs(point.X - (box.X + box.Width)) <= Tolerance)
            {
                return (box, 1);
            }

            if (withinX && Math.Abs(point.Y - box.Y) <= Tolerance)
            {
                return (box, 2);
            }

            if (withinX && Math.Abs(point.Y - (box.Y + box.Height)) <= Tolerance)
            {
                return (box, 3);
            }
        }

        return null;
    }

    /// <summary>Counts the proper crossings between the segments of two lines.</summary>
    private static int CountSegmentCrossings(LayoutLine first, LayoutLine second)
    {
        var count = 0;
        for (var a = 0; a + 1 < first.Waypoints.Count; a++)
        {
            for (var b = 0; b + 1 < second.Waypoints.Count; b++)
            {
                var (a1, a2) = (first.Waypoints[a], first.Waypoints[a + 1]);
                var (b1, b2) = (second.Waypoints[b], second.Waypoints[b + 1]);
                var onOppositeSidesOfB = Orientation(b1.X, b1.Y, b2.X, b2.Y, a1.X, a1.Y) * Orientation(b1.X, b1.Y, b2.X, b2.Y, a2.X, a2.Y) < 0;
                var onOppositeSidesOfA = Orientation(a1.X, a1.Y, a2.X, a2.Y, b1.X, b1.Y) * Orientation(a1.X, a1.Y, a2.X, a2.Y, b2.X, b2.Y) < 0;
                if (onOppositeSidesOfA && onOppositeSidesOfB)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>The signed area of the triangle (p, q, r): positive when r is left of p-q, negative when right, zero when collinear.</summary>
    private static double Orientation(double px, double py, double qx, double qy, double rx, double ry) =>
        ((qx - px) * (ry - py)) - ((qy - py) * (rx - px));

    /// <summary>
    ///     The vehicle model of the nested-package gallery example with the <c>Body</c> package's
    ///     definitions supplied by the caller, so tests can vary their declaration order.
    /// </summary>
    private static string VehicleModel(string bodyDefinitions) => $$"""
        package Vehicle {
            package Powertrain { part def Engine; part def ElectricMotor :> Engine; }
            package Body { {{bodyDefinitions}} }
            part def Car { part engine : Powertrain::Engine; part chassis : Body::Chassis; }
        }
        """;

    /// <summary>
    ///     The declaration order of a package folder's definitions does not change where the
    ///     definitions are drawn: whether <c>Hybrid</c> (leading into the <c>Powertrain</c> folder)
    ///     is declared last or first, the folder places every box at the same position and no two
    ///     definition boxes overlap. (A residual crossing between the cross-folder connectors of
    ///     this model is an engine routing outcome, see the verification document.)
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ExternalEdges_OrderFolderItemsByPartnerPosition()
    {
        // Act: the declaration order that used to force the crossing, and its reverse
        var hybridLast = await LayOutUnscopedAsync(VehicleModel("part def Chassis; part def SportsChassis :> Chassis; part def Hybrid :> Powertrain::Engine;"));
        var hybridFirst = await LayOutUnscopedAsync(VehicleModel("part def Hybrid :> Powertrain::Engine; part def Chassis; part def SportsChassis :> Chassis;"));

        // Assert: both orders draw the same boxes at the same positions, without overlap
        foreach (var name in new[] { "Hybrid", "Chassis", "SportsChassis", "Engine", "Car" })
        {
            var last = FindBox(hybridLast.Nodes, name);
            var first = FindBox(hybridFirst.Nodes, name);
            Assert.Equal(last.X, first.X, 1.0);
            Assert.Equal(last.Y, first.Y, 1.0);
        }

        AssertDefinitionBoxesDoNotOverlap(hybridLast.Nodes);
        AssertDefinitionBoxesDoNotOverlap(hybridFirst.Nodes);
    }

    /// <summary>
    ///     A folder none of whose definitions has a relationship leaving it is laid out in declaration
    ///     order: the refinement leaves it alone (the engine stacks its unconnected definitions by the
    ///     order they were added).
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_NoExternalEdges_KeepsDeclarationOrder()
    {
        // Act: three unconnected definitions and a specialization pair, all inside one package
        var layout = await LayOutUnscopedAsync("package P { part def Zeta; part def Alpha; part def Mid :> Alpha; part def Beta; }");

        // Assert: the unconnected definitions keep their declaration order in reading order (top to
        // bottom, then left to right); nothing is reordered alphabetically or by connectivity
        var p = FindBox(layout.Nodes, "P");
        var readingOrder = p.Children.OfType<LayoutBox>()
            .OrderBy(b => b.Y).ThenBy(b => b.X)
            .Select(b => b.Label)
            .ToList();
        Assert.True(readingOrder.IndexOf("Zeta") < readingOrder.IndexOf("Beta"));
    }

    /// <summary>
    ///     A partner hidden inside a depth-truncated folder has no box to measure, so the definition has
    ///     no ordering key: the layout completes, both of the folder's definitions are drawn and they keep
    ///     their declaration order.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ExternalEdgeToTruncatedPartner_DoesNotThrowAndKeepsOrder()
    {
        // Act: Z lives in Far::Sub, whose contents sit beyond the depth limit of 2
        var layout = await LayOutUnscopedAsync(
            "package Body { part def A; part def B :> Far::Sub::Z; } package Far { part def W; package Sub { part def Z; } }",
            depthLimit: 2);

        // Assert
        var a = FindBox(layout.Nodes, "A");
        var b = FindBox(layout.Nodes, "B");
        Assert.True(a.Y < b.Y || a.X < b.X);
        Assert.DoesNotContain(CollectBoxes(layout.Nodes), box => box.Label == "Z");
        Assert.Equal("\u2026", Assert.IsType<LayoutLabel>(Assert.Single(FindBox(layout.Nodes, "Sub").Children)).Text);
    }

    /// <summary>
    ///     The partner's position is measured at the level where the two meet, however deep the partner
    ///     is nested: definitions of <c>A::B</c> that lead into <c>Lo::In</c> and <c>Hi::In</c> are
    ///     placed in the reading order of the horizontal order of the <c>Lo</c> and <c>Hi</c> folders
    ///     they lead to, whichever way they were declared.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ExternalEdges_DeepNesting_FollowsPartnerContainerOrder()
    {
        // Arrange: the same four packages, with the definitions of A::B declared in both orders
        const string packages = "package Lo { package In { part def L; } } package Hi { package In { part def H; } }";
        var viaLowFirst = packages + " package A { package B { part def ToLo :> Lo::In::L; part def ToHi :> Hi::In::H; } }";
        var viaHighFirst = packages + " package A { package B { part def ToHi :> Hi::In::H; part def ToLo :> Lo::In::L; } }";

        // Act / Assert: the reading order of the two definitions matches the left-to-right order of their partners
        foreach (var sysml in new[] { viaLowFirst, viaHighFirst })
        {
            var layout = await LayOutUnscopedAsync(sysml);
            var lo = FindBox(layout.Nodes, "Lo");
            var hi = FindBox(layout.Nodes, "Hi");
            Assert.NotEqual(lo.X + (lo.Width / 2.0), hi.X + (hi.Width / 2.0));
            Assert.Equal(
                (lo.X + (lo.Width / 2.0)) < (hi.X + (hi.Width / 2.0)),
                ReadsBefore(FindBox(layout.Nodes, "ToLo"), FindBox(layout.Nodes, "ToHi")));
            Assert.Equal(0, CountLineCrossings(layout.Nodes));
        }
    }

    /// <summary>True when <paramref name="first"/> comes before <paramref name="second"/> in reading order (top to bottom, then left to right).</summary>
    private static bool ReadsBefore(LayoutBox first, LayoutBox second)
    {
        var sameRow = Math.Abs(first.Y - second.Y) < 1.0;
        return sameRow ? first.X < second.X : first.Y < second.Y;
    }

    /// <summary>
    ///     A definition that is a direct member of a package and whose partners live in different nested
    ///     packages of that same package is ordered by where those nested packages sit, even though the
    ///     lowest common scope of each relationship is the package's own scope: <c>ToA</c> and
    ///     <c>ToB</c> read in the same order as the <c>A</c> and <c>B</c> folders they lead to,
    ///     whichever way they were declared, and no lines cross.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ExternalEdges_DefinitionToSiblingNestedPackages_OrdersByPartnerFolder()
    {
        // Arrange: the same packages, with the owner's definitions declared in both orders
        const string packages = "package A { part def X; } package B { part def Y; }";
        var toAFirst = "package P { part def ToA :> A::X; part def ToB :> B::Y; " + packages + " }";
        var toBFirst = "package P { part def ToB :> B::Y; part def ToA :> A::X; " + packages + " }";

        // Act / Assert
        foreach (var sysml in new[] { toAFirst, toBFirst })
        {
            var layout = await LayOutUnscopedAsync(sysml);
            var a = FindBox(layout.Nodes, "A");
            var b = FindBox(layout.Nodes, "B");
            Assert.NotEqual(a.X + (a.Width / 2.0), b.X + (b.Width / 2.0));
            Assert.Equal(
                (a.X + (a.Width / 2.0)) < (b.X + (b.Width / 2.0)),
                ReadsBefore(FindBox(layout.Nodes, "ToA"), FindBox(layout.Nodes, "ToB")));
            Assert.Equal(0, CountLineCrossings(layout.Nodes));
        }
    }

    /// <summary>
    ///     Two distinct relationships between the same two definitions inside a nested package (two
    ///     attribute usages of the same type, drawn as nodes under the all-usages policy) are drawn
    ///     as two separate connectors: parallel-edge merging is off in every scope, not only at the root.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ParallelEdgesInNestedPackage_AreNotMerged()
    {
        // Act
        var layout = await LayOutUnscopedAsync(
            "package P { package Sub { attribute def Mass; part def Vehicle { attribute mass : Mass; attribute weight : Mass; } } }",
            policy: NodePolicy.AllUsages);

        // Assert: one typing edge per attribute usage
        var typing = CollectLines(layout.Nodes).Where(l => l.TargetEnd == EndMarkerStyle.HollowTriangleTwoDots).ToList();
        Assert.Equal(2, typing.Count);
    }

    /// <summary>
    ///     Two distinct relationships between the same two definitions inside a definition container
    ///     are likewise drawn as two separate connectors.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ParallelEdgesInDefinitionContainer_AreNotMerged()
    {
        // Act
        var layout = await LayOutUnscopedAsync(
            "part def Outer { attribute def Mass; part def Vehicle { attribute mass : Mass; attribute weight : Mass; } }",
            policy: NodePolicy.AllUsages);

        // Assert: Vehicle and Mass are nested in Outer, and each attribute usage has its own typing edge
        var outer = FindBox(layout.Nodes, "Outer");
        Assert.Contains(outer.Children.OfType<LayoutBox>(), b => b.Label == "Mass");
        var typing = CollectLines(layout.Nodes).Where(l => l.TargetEnd == EndMarkerStyle.HollowTriangleTwoDots).ToList();
        Assert.Equal(2, typing.Count);
    }

    /// <summary>
    ///     Builds the layered-layout graph the General view relies on for its ordering refinement: a
    ///     <c>B</c> folder holding <c>C</c>, <c>S</c> (specializing <c>C</c>) and <c>H</c>, added in the
    ///     given order, beside a <c>P</c> folder holding <c>E</c> and a leaf <c>K</c>, with relationships
    ///     <c>H</c> to <c>E</c>, <c>E</c> to <c>K</c> and <c>C</c> to <c>K</c> declared at the root.
    /// </summary>
    private static LayoutTree LayOutOrderProbe(string bodyOrder)
    {
        static LayoutGraphNode Add(LayoutGraph scope, string name)
        {
            var node = scope.AddNode(name, 130, 50);
            node.Label = name;
            node.Keyword = "part def";
            node.Shape = BoxShape.Rectangle;
            return node;
        }

        static LayoutGraphNode AddFolder(LayoutGraph scope, string name)
        {
            var folder = scope.AddNode("folder:" + name, 0, 0);
            folder.Label = name;
            folder.Shape = BoxShape.Folder;
            folder.Keyword = "package";
            folder.TitleHeight = 56;
            folder.Set(CoreOptions.Algorithm, LayeredLayoutAlgorithm.AlgorithmId);
            return folder;
        }

        var graph = new LayoutGraph();
        var k = Add(graph, "K");
        var p = AddFolder(graph, "P");
        var e = Add(p.Children, "E");
        var b = AddFolder(graph, "B");
        var nodes = bodyOrder.ToDictionary(name => name, name => Add(b.Children, name.ToString()));
        b.Children.AddEdge("s-c", nodes['S'], nodes['C']).TargetEnd = EndMarkerStyle.HollowTriangle;
        graph.AddEdge("h-e", nodes['H'], e).TargetEnd = EndMarkerStyle.HollowTriangle;
        graph.AddEdge("e-k", e, k).TargetEnd = EndMarkerStyle.FilledDiamond;
        graph.AddEdge("c-k", nodes['C'], k).TargetEnd = EndMarkerStyle.FilledDiamond;
        graph.Set(CoreOptions.Algorithm, AutoLayoutAlgorithm.AlgorithmId);
        return LayoutEngine.Layout(graph);
    }

    /// <summary>
    ///     OTS evidence for the contract the General view's ordering refinement builds on: the layered
    ///     algorithm stacks the unconnected groups of a container's contents in the order the nodes were
    ///     added, so adding <c>H</c> first places it above <c>C</c> and adding it last places it below.
    /// </summary>
    [Fact]
    public void LayeredLayoutAlgorithm_Layout_ContainerContents_StackUnconnectedGroupsByInsertionOrder()
    {
        // Act
        var hybridLast = LayOutOrderProbe("CSH");
        var hybridFirst = LayOutOrderProbe("HCS");

        // Assert
        Assert.True(FindBox(hybridLast.Nodes, "H").Y > FindBox(hybridLast.Nodes, "C").Y);
        Assert.True(FindBox(hybridFirst.Nodes, "H").Y < FindBox(hybridFirst.Nodes, "C").Y);
    }

    /// <summary>
    ///     The General view flows bottom-to-top: for a specialization, a feature membership and a
    ///     feature typing edge the target (the general type, the owner, the definition) is placed
    ///     above the source, and the line leaves the source's top border and enters the target's
    ///     bottom border.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_TopDown_TargetOfEveryEdgeIsAboveSource()
    {
        // Act
        var layout = await LayOutUnscopedAsync("""
            part def Wheel;
            part def Vehicle { part wheel : Wheel; }
            part def Car :> Vehicle;
            """);

        // Assert: Vehicle above Car (specialization), Vehicle above wheel (membership) and Wheel above wheel (typing)
        var vehicle = FindBox(layout.Nodes, "Vehicle");
        var car = FindBox(layout.Nodes, "Car");
        var wheelUsage = FindBox(layout.Nodes, "wheel");
        var wheelDef = FindBox(layout.Nodes, "Wheel");
        Assert.True(vehicle.Y + vehicle.Height <= car.Y);
        Assert.True(vehicle.Y + vehicle.Height <= wheelUsage.Y);
        Assert.True(wheelDef.Y + wheelDef.Height <= wheelUsage.Y);

        // Assert: each line starts at the lower (source) box's top border and ends at the upper box's bottom border
        foreach (var line in CollectLines(layout.Nodes))
        {
            var start = line.Waypoints[0];
            var end = line.Waypoints[^1];
            Assert.True(start.Y > end.Y, $"line from ({start.X},{start.Y}) to ({end.X},{end.Y}) does not run upward");
        }
    }

    /// <summary>
    ///     Regression for an engine defect worked around by laying package folders out with the
    ///     <c>auto</c> algorithm: a folder pinned to the layered algorithm with the bottom-to-top
    ///     direction piles several unconnected definitions of one layer on top of each other. Twelve
    ///     unconnected definitions and one specialization pair in a folder (and at the root) must
    ///     all be drawn without overlap.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_TopDown_UnconnectedDefinitionsDoNotOverlap()
    {
        // Arrange: a folder and the root each holding many unconnected definitions
        var folderDefs = string.Join(' ', Enumerable.Range(1, 12).Select(i => $"part def F{i};"));
        var rootDefs = string.Join(' ', Enumerable.Range(1, 12).Select(i => $"part def R{i};"));

        // Act
        var layout = await LayOutUnscopedAsync($"package P {{ {folderDefs} part def Base; part def Derived :> Base; }} {rootDefs}");

        // Assert: every definition box is drawn and none overlap another
        Assert.Equal(12 + 2 + 12, CollectBoxes(layout.Nodes).Count(b => b.Shape == BoxShape.Rectangle));
        AssertDefinitionBoxesDoNotOverlap(layout.Nodes);
    }

    /// <summary>
    ///     A package folder with content carries its name in the tab (the specification's
    ///     package-with-name-in-tab) — its box asks the renderer for the tab placement and keeps the
    ///     <c>Folder</c> shape — while an exposed package with no content and a depth-truncated
    ///     package keep the name inside the body.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_PackageFolder_NameInTabOnlyWhenItHasContent()
    {
        // Act: a package with content, then an exposed empty package, then a depth-truncated one
        var withContent = await LayOutUnscopedAsync("package P { part def A; }");
        var empty = await LayOutDeclaredViewAsync(ExposeFormsSysml, "Bare");
        var truncated = await LayOutUnscopedAsync("package P { part def A; }", depthLimit: 1);

        // Assert
        Assert.Equal(FolderTitlePlacement.Tab, FindBox(withContent.Nodes, "P").Appearance?.FolderTitle);
        Assert.NotEqual(FolderTitlePlacement.Tab, FindBox(empty.Nodes, "Pkg").Appearance?.FolderTitle ?? FolderTitlePlacement.Body);
        Assert.NotEqual(FolderTitlePlacement.Tab, FindBox(truncated.Nodes, "P").Appearance?.FolderTitle ?? FolderTitlePlacement.Body);
    }
}

