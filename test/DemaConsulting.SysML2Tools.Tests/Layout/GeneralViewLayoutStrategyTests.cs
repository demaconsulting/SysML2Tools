// <copyright file="GeneralViewLayoutStrategyTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

// cspell:ignore Dpart Epart Xpart Ypart

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
                // SysML stdlib element — must be filtered (it originates from the stdlib seed)
                ["SysML::Parts::PartDef"] = new SysmlDefinitionNode
                {
                    Name = "PartDef",
                    QualifiedName = "SysML::Parts::PartDef",
                    DefinitionKeyword = "part def"
                }
            },
            StdlibNames = new HashSet<string>(StringComparer.Ordinal) { "SysML::Parts::PartDef" }
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
    ///     <see cref="SysmlWorkspace.StdlibNames"/> set whatever their names (the filter is based on
    ///     element origin, never on a list of stdlib root-package names).
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
    ///     An <c>event occurrence</c> usage is sequence data, not a compartment row: a definition that owns
    ///     one gets the same compartments as before the AST captured event occurrences.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_EventOccurrenceUsage_IsNotACompartmentRow()
    {
        // Arrange: a part def owning an attribute usage and an event occurrence usage
        var strategy = new GeneralViewLayoutStrategy(NodePolicy.RowsOnly);
        var vehicle = new SysmlDefinitionNode
        {
            Name = "Vehicle",
            QualifiedName = "P::Vehicle",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "mass", QualifiedName = "P::Vehicle::mass", FeatureKeyword = "attribute", FeatureTyping = "Real" },
                new SysmlFeatureNode { Name = "start", QualifiedName = "P::Vehicle::start", FeatureKeyword = "event occurrence" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["P::Vehicle"] = vehicle }
        };

        // Act
        var layout = strategy.BuildLayout(new ViewContext("v", workspace), new RenderOptions(Themes.Light));

        // Assert: only the attributes compartment is drawn
        var box = CollectBoxes(layout.Nodes).First(b => b.Label == "Vehicle");
        Assert.Equal("attributes", Assert.Single(box.Compartments).Title);
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
    ///     Model for the expose-through-import tests: <c>A</c> re-exports <c>B</c> through a
    ///     <c>public import</c> and merely uses <c>C</c> through a <c>private import</c>; <c>A2</c>
    ///     publicly imports one member, <c>A3</c> imports <c>B</c> recursively, <c>A4</c> reaches
    ///     <c>D</c> through the public import of <c>B2</c> (which also privately imports <c>E</c>),
    ///     and <c>X</c> and <c>Y</c> publicly import each other.
    /// </summary>
    private const string ExposeImportsSysml = """
        package B {
            part def Widget;
            part def Gadget;
            private part def Hidden;
            package Inner { part def Deep; }
        }
        package C { part def Cee; }
        package A {
            public import B::*;
            private import C::*;
            part def Owner;
            package Nested { part def NestedPart; }
        }
        package A2 { public import B::Widget; part def Owner2; }
        package A3 { public import B::**; part def Owner3; }
        package D { part def Dpart; }
        package E { part def Epart; }
        package B2 { public import D::*; private import E::*; part def B2own; }
        package A4 { public import B2::*; part def Owner4; }
        package X { public import Y::*; part def Xpart; }
        package Y { public import X::*; part def Ypart; }
        package Diagrams {
            view Members { expose A::*; }
            view Recursive { expose A::**; }
            view MembershipImport { expose A2::*; }
            view RecursiveImport { expose A3::*; }
            view Transitive { expose A4::*; }
            view Cycle { expose X::**; }
            view BareNamespace { expose A; }
            view FrameAndMembers { expose A; expose A::*; }
        }
        """;

    /// <summary>Returns the labels of every box in <paramref name="layout"/>.</summary>
    private static List<string?> LabelsOf(LayoutTree layout) => CollectBoxes(layout.Nodes).Select(b => b.Label).ToList();

    /// <summary>
    ///     <c>expose A::*;</c> draws the members <c>A</c> re-exports through <c>public import B::*;</c>
    ///     next to its own members (issue #148).
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ExposeNamespace_PublicWildcardImport_DrawsImportedMembers()
    {
        // Act
        var layout = await LayOutDeclaredViewAsync(ExposeImportsSysml, "Members");

        // Assert
        var labels = LabelsOf(layout);
        Assert.Contains("Owner", labels);
        Assert.Contains("Widget", labels);
        Assert.Contains("Gadget", labels);
        Assert.Contains("Inner", labels);
    }

    /// <summary>
    ///     Private imports stay excluded: <c>A</c> merely uses <c>C</c> through <c>private import C::*;</c>,
    ///     so <c>expose A::*;</c> does not draw <c>Cee</c> — a deliberate difference from the OMG Pilot —
    ///     and a private member of an imported package is not drawn either.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ExposeNamespace_PrivateImport_DrawsNothingFromIt()
    {
        // Act
        var layout = await LayOutDeclaredViewAsync(ExposeImportsSysml, "Members");

        // Assert
        var labels = LabelsOf(layout);
        Assert.DoesNotContain("Cee", labels);
        Assert.DoesNotContain("Hidden", labels);
    }

    /// <summary>
    ///     A public membership import (<c>public import B::Widget;</c>) brings in exactly that member.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ExposeNamespace_PublicMembershipImport_DrawsOnlyThatMember()
    {
        // Act
        var layout = await LayOutDeclaredViewAsync(ExposeImportsSysml, "MembershipImport");

        // Assert
        var labels = LabelsOf(layout);
        Assert.Contains("Owner2", labels);
        Assert.Contains("Widget", labels);
        Assert.DoesNotContain("Gadget", labels);
    }

    /// <summary>
    ///     A public recursive import (<c>public import B::**;</c>) brings in the imported package and
    ///     everything below it.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ExposeNamespace_PublicRecursiveImport_DrawsWholeImportedTree()
    {
        // Act
        var layout = await LayOutDeclaredViewAsync(ExposeImportsSysml, "RecursiveImport");

        // Assert
        var labels = LabelsOf(layout);
        Assert.Contains("Owner3", labels);
        Assert.Contains("B", labels);
        Assert.Contains("Widget", labels);
        Assert.Contains("Deep", labels);
    }

    /// <summary>
    ///     Imports are followed transitively through <c>public</c> imports only: <c>A4</c> reaches
    ///     <c>Dpart</c> through <c>B2</c>'s public import, but not <c>Epart</c> through its private one.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ExposeNamespace_TransitiveImports_FollowsPublicImportsOnly()
    {
        // Act
        var layout = await LayOutDeclaredViewAsync(ExposeImportsSysml, "Transitive");

        // Assert
        var labels = LabelsOf(layout);
        Assert.Contains("B2own", labels);
        Assert.Contains("Dpart", labels);
        Assert.DoesNotContain("Epart", labels);
    }

    /// <summary>
    ///     <c>expose A::**;</c> follows public imports and draws the imported packages' contents
    ///     (<c>Deep</c> inside the imported <c>Inner</c>), still without the private import.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ExposeRecursive_FollowsPublicImportsIntoImportedPackages()
    {
        // Act
        var layout = await LayOutDeclaredViewAsync(ExposeImportsSysml, "Recursive");

        // Assert
        var labels = LabelsOf(layout);
        Assert.Contains("NestedPart", labels);
        Assert.Contains("Widget", labels);
        var inner = Assert.Single(CollectBoxes(layout.Nodes), b => b.Label == "Inner");
        Assert.Contains(inner.Children.OfType<LayoutBox>(), b => b.Label == "Deep");
        Assert.DoesNotContain("Cee", labels);
    }

    /// <summary>
    ///     Packages that publicly import each other do not make the expose loop: it terminates and
    ///     draws each member once.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ExposeRecursive_ImportCycle_TerminatesWithoutDuplicates()
    {
        // Act
        var layout = await LayOutDeclaredViewAsync(ExposeImportsSysml, "Cycle");

        // Assert
        var labels = LabelsOf(layout);
        Assert.Single(labels, l => l == "Xpart");
        Assert.Single(labels, l => l == "Ypart");
    }

    /// <summary>A bare <c>expose A;</c> exposes only the package itself, so nothing it imports is drawn.</summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_BareNamespaceExpose_DrawsNoImportedMember()
    {
        // Act
        var layout = await LayOutDeclaredViewAsync(ExposeImportsSysml, "BareNamespace");

        // Assert
        var box = Assert.Single(CollectBoxes(layout.Nodes));
        Assert.Equal("A", box.Label);
    }

    /// <summary>
    ///     With the package exposed as well (<c>expose A; expose A::*;</c>) its frame holds only the
    ///     members it owns; the imported members belong to their own package and are drawn outside it.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ExposePackageAndMembers_DrawsImportedMembersOutsideFrame()
    {
        // Act
        var layout = await LayOutDeclaredViewAsync(ExposeImportsSysml, "FrameAndMembers");

        // Assert
        var frame = Assert.Single(CollectBoxes(layout.Nodes), b => b.Label == "A");
        var framed = frame.Children.OfType<LayoutBox>().Select(b => b.Label).ToList();
        Assert.Contains("Owner", framed);
        Assert.DoesNotContain("Widget", framed);
        Assert.Contains(layout.Nodes.OfType<LayoutBox>(), b => b.Label == "Widget");
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

        // Assert: both definitions are drawn, the part is a node of its owner with no typing edge to
        // the unrelated same-named definition, and the layout reports no warning
        var boxes = CollectBoxes(layout.Nodes);
        Assert.Contains(boxes, b => b.Label == "TurboEngineX");
        Assert.Contains(boxes, b => b.Label == "Consumer");
        Assert.DoesNotContain(CollectLines(layout.Nodes), l => l.TargetEnd == EndMarkerStyle.HollowTriangleTwoDots);
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
                part def Child :> Holder { part childSub :> slotX; part childRedefining redefines spareX; }
            }
            package App { part def Consumer { part consumerSub :> slotX; part consumerRedefining redefines spareX; } }
            package Diagrams { view V { expose Lib::**; expose App::**; } }
            """);

        // Assert: exactly one subsetting and one redefinition edge (Child's), no warning
        var lines = CollectLines(layout.Nodes);
        var subsetting = Assert.Single(lines, l => l.TargetEnd == EndMarkerStyle.HollowTriangle && RunsBetween(l, FindBox(layout.Nodes, "childSub"), FindBox(layout.Nodes, "slotX")));
        Assert.Equal(LineStyle.Solid, subsetting.LineStyle);
        var redefinition = Assert.Single(lines, l => l.TargetEnd == EndMarkerStyle.HollowTriangleCrossbar);
        Assert.True(RunsBetween(redefinition, FindBox(layout.Nodes, "childRedefining"), FindBox(layout.Nodes, "spareX")));
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
            package App { part def Consumer { part consumerSub :> slot; part consumerRedefining redefines spare; } }
            package Diagrams { view V { expose Lib::**; expose App::**; } }
            """);

        // Assert
        var lines = CollectLines(layout.Nodes);
        Assert.Single(lines, l => l.TargetEnd == EndMarkerStyle.HollowTriangle && RunsBetween(l, FindBox(layout.Nodes, "consumerSub"), FindBox(layout.Nodes, "slot")));
        var redefinition = Assert.Single(lines, l => l.TargetEnd == EndMarkerStyle.HollowTriangleCrossbar);
        Assert.True(RunsBetween(redefinition, FindBox(layout.Nodes, "consumerRedefining"), FindBox(layout.Nodes, "spare")));
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
    ///     line with a hollow diamond at the owner (non-composite feature membership).
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
    ///     A part def that owns a <c>port</c> usage with nested content draws the port as its own
    ///     node, joined to the owning definition's box by a filled diamond at the owner.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_PortFeature_ProducesFilledDiamondEdge()
    {
        // Arrange: Vehicle owns a port typed as FuelPort that itself owns an attribute; both are user definitions
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
                        new SysmlFeatureNode
                        {
                            Name = "fuel",
                            QualifiedName = "P::Vehicle::fuel",
                            FeatureKeyword = "port",
                            FeatureTyping = "FuelPort",
                            Children = [new SysmlFeatureNode { Name = "level", QualifiedName = "P::Vehicle::fuel::level", FeatureKeyword = "attribute" }]
                        }
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
    ///     A <c>port</c> usage without nested content is a row of the owner's <c>ports</c> compartment
    ///     (like an attribute), not a node, under the default policy: only the owner is drawn, with no
    ///     membership edge and no typing edge for the port.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_PlainPort_IsRowOfOwnerPortsCompartment()
    {
        // Act
        var layout = await LayOutUnscopedAsync("port def FuelPort; part def Vehicle { port fuel : FuelPort; }");

        // Assert: no box for the port, one row in the owner's ports compartment, no edge
        Assert.DoesNotContain(CollectBoxes(layout.Nodes), b => IsNamed(b, "fuel"));
        var vehicle = FindBox(layout.Nodes, "Vehicle");
        var ports = Assert.Single(vehicle.Compartments, c => c.Title == "ports");
        Assert.Equal(["fuel : FuelPort"], ports.Rows);
        Assert.Empty(CollectLines(layout.Nodes));
    }

    /// <summary>
    ///     A <c>port</c> usage that owns nested features is a node of its own (joined to its owner by a
    ///     filled diamond) and lists the nested features in its own compartments, while a plain sibling
    ///     port stays a row of the owner.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_PortWithNestedContent_IsNodeAndPlainPortStaysRow()
    {
        // Act
        var layout = await LayOutUnscopedAsync("""
            port def FuelPort;
            part def Vehicle {
                port plain : FuelPort;
                port rich : FuelPort { attribute level : Real; }
            }
            """);

        // Assert
        var rich = FindBox(layout.Nodes, "rich");
        Assert.Equal("port", rich.Keyword);
        Assert.Contains(rich.Compartments, c => c.Title == "attributes" && c.Rows.Contains("level : Real"));
        Assert.DoesNotContain(CollectBoxes(layout.Nodes), b => IsNamed(b, "plain"));
        var vehicle = FindBox(layout.Nodes, "Vehicle");
        Assert.Equal(["plain : FuelPort"], Assert.Single(vehicle.Compartments, c => c.Title == "ports").Rows);
        Assert.Single(CollectLines(layout.Nodes), l => l.TargetEnd == EndMarkerStyle.FilledDiamond);
    }

    /// <summary>
    ///     The all-usages policy still draws a plain port as a node (the variant exists so a later
    ///     option can flip the decision), and the row-only policy draws no port as a node.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_PortPolicyVariants_AllUsagesDrawsPlainPortAsNode_RowsOnlyDoesNot()
    {
        // Arrange
        const string model = "port def FuelPort; part def Vehicle { port fuel : FuelPort { attribute level : Real; } }";

        // Act
        var allUsages = await LayOutUnscopedAsync("port def FuelPort; part def Vehicle { port fuel : FuelPort; }", policy: NodePolicy.AllUsages);
        var rowsOnly = await LayOutUnscopedAsync(model, policy: NodePolicy.RowsOnly);

        // Assert
        Assert.Contains(CollectBoxes(allUsages.Nodes), b => IsNamed(b, "fuel"));
        Assert.DoesNotContain(CollectBoxes(rowsOnly.Nodes), b => IsNamed(b, "fuel"));
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
    ///     Builds a model of <c>packageCount</c> sibling packages inside one root package, each holding
    ///     <c>definitionsPerPackage</c> part definitions, in which every definition of package
    ///     <c>n</c> specializes the same-numbered definition of package <c>n + 1</c> (wrapping around),
    ///     so the root scope carries <c>packageCount * definitionsPerPackage</c> connectors between
    ///     nested boxes of different folders — the shape of the OMG Annex A "General" views. The
    ///     text depends only on the arguments, so every run lays out the same model.
    /// </summary>
    private static string BuildCrossPackageModel(int packageCount, int definitionsPerPackage)
    {
        var text = new System.Text.StringBuilder("package Root {\n");
        for (var p = 0; p < packageCount; p++)
        {
            text.Append($"  package P{p} {{\n");
            for (var d = 0; d < definitionsPerPackage; d++)
            {
                text.Append($"    part def D{d} :> Root::P{(p + 1) % packageCount}::D{d};\n");
            }

            text.Append("  }\n");
        }

        return text.Append("}\n").ToString();
    }

    /// <summary>
    ///     A model with so many connectors between boxes nested in different folders that the layout
    ///     engine draws them with a reduced routing effort reports that through
    ///     <see cref="LayoutTree.Warnings"/>: the strategy passes the engine's own warning on instead
    ///     of replacing it with its own.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_OversizeScope_PassesEngineReducedEffortWarningThrough()
    {
        // Arrange / Act: six folders of ten definitions, every definition specializing one in the next folder
        var layout = await LayOutUnscopedAsync(BuildCrossPackageModel(6, 10));

        // Assert: the engine's reduced-effort hint is reported, exactly once, and the diagram is still drawn
        var hint = Assert.Single(layout.Warnings, w => w.Contains("work limit", StringComparison.Ordinal));
        Assert.Contains("without the extra quality searches", hint, StringComparison.Ordinal);
        Assert.Equal(60, CollectBoxes(layout.Nodes).Count(b => b.Label!.StartsWith('D')));
    }

    /// <summary>
    ///     A model small enough for the full routing effort draws without any warning, so the pass-through
    ///     does not invent one.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_SmallScope_HasNoEngineWarning()
    {
        // Arrange / Act
        var layout = await LayOutUnscopedAsync(BuildCrossPackageModel(2, 2));

        // Assert
        Assert.Empty(layout.Warnings);
    }

    /// <summary>
    ///     A view condition that cannot be evaluated gives the strategy a warning of its own; it and the engine's
    ///     hint both reach <see cref="LayoutTree.Warnings"/>, ours first.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_OversizeScopeWithOwnWarning_ReportsBothWarnings()
    {
        // Arrange: the oversize model, laid out by a view whose filter expression cannot be evaluated
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, BuildCrossPackageModel(6, 10), TestContext.Current.CancellationToken);
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);
            Assert.NotNull(result.Workspace);
            var viewNode = new SysmlViewNode { Name = "V", QualifiedName = "Root::V", FilterExpressionText = "1 + 2" };

            // Act
            var layout = new GeneralViewLayoutStrategy().BuildLayout(
                new ViewContext("v", result.Workspace, viewNode), new RenderOptions(Themes.Light));

            // Assert
            var ours = layout.Warnings.ToList().FindIndex(w => w.Contains("could not be evaluated", StringComparison.Ordinal));
            var engine = layout.Warnings.ToList().FindIndex(w => w.Contains("work limit", StringComparison.Ordinal));
            Assert.True(ours >= 0, "the strategy's own warning is kept");
            Assert.True(engine > ours, "the engine's warning follows it");
        }
        finally
        {
            File.Delete(tempFile);
        }
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
    ///     definitions is drawn as a dashed line with an open chevron at the depended-upon box and
    ///     the <c>«depend»</c> keyword label (the edge has no declared name to show).
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

        // Assert: a dashed open-chevron edge labeled with the keyword is emitted.
        var lines = CollectLines(layout.Nodes);
        var dependencyEdge = lines.FirstOrDefault(l => l.TargetEnd == EndMarkerStyle.OpenChevron && l.LineStyle == LineStyle.Dashed && l.MidpointLabel == "«depend»");
        Assert.NotNull(dependencyEdge);
    }

    /// <summary>
    ///     A named dependency (<c>dependency link from A to B;</c>) is labeled with its declared name
    ///     instead of the keyword, an unnamed dependency of the same model keeps <c>«depend»</c>, and
    ///     two named dependencies between the same boxes each show their own name.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_Dependency_Named_ShowsDeclaredNameInsteadOfKeyword()
    {
        // Act
        var layout = await LayOutUnscopedAsync("""
            package P {
                part def A; part def B; part def C; part def D;
                dependency link from A to B;
                dependency A to D;
                dependency alpha from C to D;
                dependency beta from C to D;
            }
            """);

        // Assert: every dashed open-chevron edge carries exactly the expected label
        var labels = CollectLines(layout.Nodes)
            .Where(l => l.TargetEnd == EndMarkerStyle.OpenChevron && l.LineStyle == LineStyle.Dashed)
            .Select(l => l.MidpointLabel)
            .OrderBy(label => label, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(["alpha", "beta", "link", "«depend»"], labels);
    }

    /// <summary>
    ///     A named and an unnamed dependency between the same client and supplier each keep their own
    ///     label, whichever is declared first: the layout lists the edges in declaration order, so the
    ///     labels read <c>«depend»</c> then <c>link</c> when the unnamed one comes first, and the reverse
    ///     when the named one does.
    /// </summary>
    [Theory]
    [InlineData("dependency A to B; dependency link from A to B;", new[] { "«depend»", "link" })]
    [InlineData("dependency link from A to B; dependency A to B;", new[] { "link", "«depend»" })]
    public async Task GeneralViewLayoutStrategy_BuildLayout_Dependency_NamedAndUnnamedSameEnds_KeepOwnLabels(
        string dependencies, string[] expectedLabels)
    {
        // Act
        var layout = await LayOutUnscopedAsync($"package P {{ part def A; part def B; {dependencies} }}");

        // Assert
        var labels = CollectLines(layout.Nodes)
            .Where(l => l.TargetEnd == EndMarkerStyle.OpenChevron && l.LineStyle == LineStyle.Dashed)
            .Select(l => l.MidpointLabel)
            .ToList();
        Assert.Equal(expectedLabels, labels);
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
    ///     A view condition <c>filter not (@SysML::Documentation);</c> (SysML 7.26.2) hides the
    ///     <c>doc</c> compartment, while the element itself and its comment note remain.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_FilterNotDocumentation_HidesDocButKeepsComment()
    {
        // Arrange
        var workspace = BuildDocumentedBatteryWorkspace("Primary power source.");
        var view = new SysmlViewNode { Name = "v", QualifiedName = "P::v", FilterExpressionTexts = ["not (@SysML::Documentation)"] };

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
    ///     by a view definition owning <c>filter not (@SysML::Documentation);</c> hides documentation.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_InheritedViewCondition_HidesDoc()
    {
        // Act
        var layout = await LayOutViewVAsync(DocumentedPackageSysml + """

            package Diagrams {
                view def NoDocs { filter not (@SysML::Documentation); }
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
                view V : StandardViewDefinitions::GeneralView { expose P::**; filter not (@SysML::Documentation); }
            }
            """);

        // Assert
        var battery = Assert.Single(CollectBoxes(layout.Nodes), b => b.Label == "Battery");
        Assert.DoesNotContain(battery.Compartments, c => c.Title == "doc");
    }

    /// <summary>
    ///     An expose filter (<c>expose P::**[not (@SysML::Documentation)];</c>, SysML 7.26.2) excludes
    ///     the exposed documentation while still exposing the definitions and comments.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ExposeFilterNotDocumentation_HidesDoc()
    {
        // Act
        var layout = await LayOutViewVAsync(DocumentedPackageSysml + """

            package Diagrams { view V { expose P::**[not (@SysML::Documentation)]; } }
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
    ///     Regression test for the box-to-node pairing in the truncation decoration: a scope that
    ///     places a routed intra-scope edge (here a specialization line) alongside its boxes must
    ///     still attach the "…" truncation marker to the box that was truncated (<c>B</c>, whose
    ///     nested definition is hidden by the depth limit) and not to its untruncated sibling
    ///     (<c>A</c>), whatever order the engine places the line and the boxes in.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_TruncatedBoxBesideRoutedEdge_DecoratesOnlyTruncatedBox()
    {
        // Arrange / Act: B specializes A (a line in the Sys scope); only B owns a nested definition
        var layout = await LayOutUnscopedAsync("""
            package Sys {
                part def A;
                part def B :> A { part def Inner; }
            }
            """, depthLimit: 2);

        // Assert: the specialization line is drawn and Inner is hidden
        Assert.Contains(CollectLines(layout.Nodes), l => l.TargetEnd == EndMarkerStyle.HollowTriangle);
        var boxes = CollectBoxes(layout.Nodes);
        Assert.DoesNotContain(boxes, b => b.Label == "Inner");

        // Assert: only B carries the ellipsis row; A is undecorated
        var boxA = boxes.Single(b => b.Label!.Split(' ')[0] == "A");
        var boxB = boxes.Single(b => b.Label!.Split(' ')[0] == "B");
        Assert.DoesNotContain(boxA.Compartments, c => c.Rows.Contains("\u2026"));
        Assert.Empty(boxA.Children);
        Assert.Equal(["\u2026"], boxB.Compartments[^1].Rows);
    }
    /// <summary>
    ///     Implicit specializations are lookup input only: a model that declares no specialization
    ///     draws only its own elements, with no library-type box and no specialization line.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ImplicitSpecializations_DrawNoLibraryTypesOrSpecializationLines()
    {
        // Act
        var layout = await LayOutUnscopedAsync("""
            part def A; item def I; action def R; state def S; port def Q;
            part def Owner { part a : A; port q : Q; }
            """);

        // Assert
        Assert.Empty(DirectSpecializationLines(layout.Nodes));
        Assert.DoesNotContain(CollectLines(layout.Nodes), l => l.TargetEnd == EndMarkerStyle.HollowTriangle);
        string[] declared = ["A", "I", "R", "S", "Q", "Owner", "a", "q"];
        Assert.All(
            CollectBoxes(layout.Nodes),
            b => Assert.Contains(b.Label!.Split(' ')[0], declared));
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

    /// <summary>Builds a minimal placed box for the layout fixtures.</summary>
    private static LayoutBox MakeFixtureBox(string label, BoxShape shape, string? keyword, params LayoutNode[] children) =>
        new(
            X: 0, Y: 0, Width: 100, Height: 60, Label: label, Depth: 1, Shape: shape,
            Compartments: [], Children: children, Keyword: keyword, RoundedCornerRadius: null,
            FolderTabWidth: null, FolderTabHeight: null, ContentInsetLeft: 0, ContentInsetRight: 0,
            ContentInsetTop: 0, ContentInsetBottom: 0, CenterTitle: false);

    /// <summary>
    ///     The ellipsis decoration is paired with the box standing for the truncated node, at every
    ///     nesting level: with several sibling packages and a nested package at each of two scopes,
    ///     only the packages whose contents the depth limit hides (<c>Y</c> and <c>Q</c>) carry the
    ///     "…" label, and no other box is decorated.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_TruncatedNestedPackages_DecorateOnlyMatchingBoxes()
    {
        // Arrange / Act: P holds X and a package Y; B holds a package Q; A is a root definition
        var layout = await LayOutUnscopedAsync("""
            part def A;
            package P { part def X; package Y { part def Hidden1; } }
            package B { package Q { part def Hidden2; } }
            """, depthLimit: 2);

        // Assert: the contents hidden by the depth limit are not drawn
        var boxes = CollectBoxes(layout.Nodes);
        Assert.DoesNotContain(boxes, b => b.Label is "Hidden1" or "Hidden2");

        // Assert: only Y and Q carry a bare "…" label
        static bool HasEllipsis(LayoutBox b) =>
            b.Children.OfType<LayoutLabel>().Any(l => l.Text == "\u2026");
        Assert.Equal(["Q", "Y"], boxes.Where(HasEllipsis).Select(b => b.Label).Order(StringComparer.Ordinal));
        Assert.Contains(boxes, b => b.Label == "X");
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
    public async Task GeneralViewLayoutStrategy_BuildLayout_ExternalEdges_DeclarationOrder_DoesNotMoveBoxes()
    {
        // Act: two declaration orders of the same folder contents
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
    ///     order: the engine stacks its unconnected definitions by the order they were added.
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
    ///     A partner hidden inside a depth-truncated folder has no box to connect to: the layout
    ///     completes, both of the folder's definitions are drawn and they keep their declaration order.
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
    ///     Relationships from a definition nested in <c>A::B</c> to partners in the deeply nested
    ///     <c>Lo::In</c> and <c>Hi::In</c> folders do not cross one another, whichever way the
    ///     definitions were declared.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ExternalEdges_DeepNesting_DoNotCross()
    {
        // Arrange: the same four packages, with the definitions of A::B declared in both orders
        const string packages = "package Lo { package In { part def L; } } package Hi { package In { part def H; } }";
        var viaLowFirst = packages + " package A { package B { part def ToLo :> Lo::In::L; part def ToHi :> Hi::In::H; } }";
        var viaHighFirst = packages + " package A { package B { part def ToHi :> Hi::In::H; part def ToLo :> Lo::In::L; } }";

        // Act / Assert: the routed relationships do not cross
        foreach (var sysml in new[] { viaLowFirst, viaHighFirst })
        {
            var layout = await LayOutUnscopedAsync(sysml);
            Assert.Equal(2, CollectLines(layout.Nodes).Count);
            Assert.Equal(0, CountLineCrossings(layout.Nodes));
        }
    }

    /// <summary>
    ///     A definition that is a direct member of a package and whose partners live in different nested
    ///     packages of that same package (the lowest common scope of each relationship is the package's
    ///     own scope) is drawn without crossing lines, whichever way the definitions were declared.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ExternalEdges_DefinitionToSiblingNestedPackages_DoNotCross()
    {
        // Arrange: the same packages, with the owner's definitions declared in both orders
        const string packages = "package A { part def X; } package B { part def Y; }";
        var toAFirst = "package P { part def ToA :> A::X; part def ToB :> B::Y; " + packages + " }";
        var toBFirst = "package P { part def ToB :> B::Y; part def ToA :> A::X; " + packages + " }";

        // Act / Assert
        foreach (var sysml in new[] { toAFirst, toBFirst })
        {
            var layout = await LayOutUnscopedAsync(sysml);
            Assert.Equal(2, CollectLines(layout.Nodes).Count);
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
    ///     Builds the layered-layout graph used as OTS evidence for the way the engine orders a
    ///     container's contents: a <c>B</c> folder holding <c>C</c>, <c>S</c> (specializing <c>C</c>)
    ///     and <c>H</c>, added in the given order, beside a <c>P</c> folder holding <c>E</c> and a leaf
    ///     <c>K</c>, with relationships <c>H</c> to <c>E</c>, <c>E</c> to <c>K</c> and <c>C</c> to
    ///     <c>K</c> declared at the root.
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
    ///     OTS evidence for the contract the General view builds on: the layered algorithm orders the
    ///     contents of a container by the relationships that leave it, so the order in which the nodes
    ///     were added (<c>H</c> first or last) does not change where they are placed and the routed
    ///     relationships do not cross.
    /// </summary>
    [Fact]
    public void LayeredLayoutAlgorithm_Layout_ContainerContents_OrderFollowsCrossHierarchyEdges()
    {
        // Act
        var hybridLast = LayOutOrderProbe("CSH");
        var hybridFirst = LayOutOrderProbe("HCS");

        // Assert: the same relative placement whichever way the nodes were added
        Assert.Equal(
            FindBox(hybridLast.Nodes, "H").Y > FindBox(hybridLast.Nodes, "C").Y,
            FindBox(hybridFirst.Nodes, "H").Y > FindBox(hybridFirst.Nodes, "C").Y);
        Assert.Equal(0, CountLineCrossings(hybridLast.Nodes));
        Assert.Equal(0, CountLineCrossings(hybridFirst.Nodes));
    }

    /// <summary>
    ///     The General view flows top-to-bottom: the owner and the general type are placed above the
    ///     element they own or specialize, and the definition typing a usage is placed below the
    ///     usage. The connectors keep their drawn direction: a specialization and a feature
    ///     membership line run upward from the source, with the marker at the upper box's bottom
    ///     border, while a typing line runs downward with the marker at the definition's top border.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_TopDown_OwnerAndGeneralAboveUsageAndDefinitionBelow()
    {
        // Act
        var layout = await LayOutUnscopedAsync("""
            part def Wheel;
            part def Vehicle { part wheel : Wheel; }
            part def Car :> Vehicle;
            """);

        // Assert: Vehicle above Car (specialization), Vehicle above wheel (membership) and Wheel below wheel (typing)
        var vehicle = FindBox(layout.Nodes, "Vehicle");
        var car = FindBox(layout.Nodes, "Car");
        var wheelUsage = FindBox(layout.Nodes, "wheel");
        var wheelDef = FindBox(layout.Nodes, "Wheel");
        Assert.True(vehicle.Y + vehicle.Height <= car.Y);
        Assert.True(vehicle.Y + vehicle.Height <= wheelUsage.Y);
        Assert.True(wheelUsage.Y + wheelUsage.Height <= wheelDef.Y);

        // Assert: specialization and membership lines start at the lower box and end at the upper one
        // (marker at the upper box); the typing line starts at the usage and ends at the definition below
        var lines = CollectLines(layout.Nodes);
        Assert.Equal(3, lines.Count);
        foreach (var line in lines)
        {
            var start = line.Waypoints[0];
            var end = line.Waypoints[^1];
            if (line.TargetEnd == EndMarkerStyle.HollowTriangleTwoDots)
            {
                Assert.True(RunsBetween(line, wheelUsage, wheelDef));
                Assert.True(start.Y < end.Y, "the typing line does not run downward");
            }
            else
            {
                Assert.True(start.Y > end.Y, $"line from ({start.X},{start.Y}) to ({end.X},{end.Y}) does not run upward");
            }
        }
    }

    /// <summary>
    ///     Layout compactness regression for a drone-like model (one definition owning eight part usages,
    ///     one specialization with a subsetting usage, and a handful of unrelated definitions): the
    ///     usages of the owner share one row directly below it, each typing definition hangs below its
    ///     usage, and the drawing stays within a readable size instead of the former two rigid tiers
    ///     that grew several thousand pixels wide.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_DroneLikeModel_IsCompactAndTopDown()
    {
        // Act
        var layout = await LayOutUnscopedAsync("""
            part def Mass; part def Frame; part def Propeller;
            part def Battery { port output : PowerPort; }
            part def FlightController { port power : PowerPort; port sensors : SensorPort; }
            part def Motor { port control : MotorControlPort; }
            part def ImuSensor { port data : SensorPort; }
            part def GpsSensor { port data : SensorPort; }
            interface def PowerBus;
            port def PowerPort; port def SensorPort; port def MotorControlPort;
            enum def FlightMode { enum idle; enum manual; }
            part def Drone {
                attribute totalMass : Mass;
                part airframe : Frame;
                part battery : Battery;
                part controller : FlightController;
                part motors : Motor[4];
                part propellers : Propeller[4];
                part imu : ImuSensor;
                part gps : GpsSensor;
            }
            part def RacingDrone :> Drone { part frontMotors : Motor[2] subsets motors; }
            """);

        // Assert: the drawing is compact and nothing overlaps
        Assert.InRange(layout.Width, 1.0, 2000.0);
        Assert.InRange(layout.Height, 1.0, 1300.0);
        AssertDefinitionBoxesDoNotOverlap(layout.Nodes);

        // Assert: the owner is on top, its seven direct usages share one row below it, and every typing
        // definition is below the usage it types
        var drone = FindBox(layout.Nodes, "Drone");
        var usageNames = new[] { "airframe", "battery", "controller", "motors", "propellers", "imu", "gps" };
        var usages = usageNames.Select(name => FindBox(layout.Nodes, name)).ToList();
        Assert.All(usages, usage => Assert.True(drone.Y + drone.Height <= usage.Y));
        Assert.Single(usages.Select(usage => Math.Round(usage.Y)).Distinct());
        foreach (var (usage, definition) in usages.Zip(new[] { "Frame", "Battery", "FlightController", "Motor", "Propeller", "ImuSensor", "GpsSensor" }))
        {
            Assert.True(usage.Y + usage.Height <= FindBox(layout.Nodes, definition).Y, $"{definition} is not below its usage");
        }

        // Assert: every connector end lies inside the canvas
        foreach (var point in CollectLines(layout.Nodes).SelectMany(l => l.Waypoints))
        {
            Assert.InRange(point.X, 0.0, layout.Width);
            Assert.InRange(point.Y, 0.0, layout.Height);
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

    /// <summary>
    ///     Regression for an engine defect worked around by fitting the canvas to the connectors: with the
    ///     bottom-to-top direction the engine routes a connector between two root-level folders through
    ///     a channel outside the drawing (negative coordinates) without growing the canvas, so the
    ///     renderer would clip it. Every waypoint of every connector must lie inside the canvas.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_TopDown_ConnectorsBetweenRootFolders_StayInsideCanvas()
    {
        // Act: an import and a typing edge join the two root-level packages
        var layout = await LayOutUnscopedAsync("package Lib { part def X; } package Design { private import Lib::*; part def A { part p : X; } }");

        // Assert
        var lines = CollectLines(layout.Nodes);
        Assert.NotEmpty(lines);
        foreach (var point in lines.SelectMany(l => l.Waypoints))
        {
            Assert.InRange(point.X, 0.0, layout.Width);
            Assert.InRange(point.Y, 0.0, layout.Height);
        }
    }

    /// <summary>Returns the node policy named <paramref name="name"/> (<c>Pilot</c>, <c>AllUsages</c> or <c>RowsOnly</c>).</summary>
    private static NodePolicy PolicyByName(string name) => name switch
    {
        "Pilot" => NodePolicy.Pilot,
        "AllUsages" => NodePolicy.AllUsages,
        _ => NodePolicy.RowsOnly,
    };

    /// <summary>
    ///     The node policy decides by usage keyword whether a nested usage is a node: the Pilot policy
    ///     draws occurrence-kind usages and ports that own nested content as nodes and keeps attributes,
    ///     plain references, plain ports (no nested content), enumeration values and constraints as
    ///     rows; the all-usages policy draws every named usage
    ///     with a keyword except constraints and enumeration values; the row-only policy draws none.
    /// </summary>
    /// <param name="policy">The policy name.</param>
    /// <param name="keyword">The usage keyword.</param>
    /// <param name="expected">Whether the usage is a node.</param>
    [Theory]
    [InlineData("Pilot", "part", true)]
    [InlineData("Pilot", "item", true)]
    [InlineData("Pilot", "port", false)]
    [InlineData("Pilot", "action", true)]
    [InlineData("Pilot", "state", true)]
    [InlineData("Pilot", "requirement", true)]
    [InlineData("Pilot", "occurrence", true)]
    [InlineData("Pilot", "attribute", false)]
    [InlineData("Pilot", "ref", false)]
    [InlineData("Pilot", "enum value", false)]
    [InlineData("Pilot", "constraint", false)]
    [InlineData("Pilot", "subject", false)]
    [InlineData("AllUsages", "part", true)]
    [InlineData("AllUsages", "port", true)]
    [InlineData("AllUsages", "attribute", true)]
    [InlineData("AllUsages", "ref", true)]
    [InlineData("AllUsages", "enum value", false)]
    [InlineData("AllUsages", "constraint", false)]
    [InlineData("AllUsages", "require constraint", false)]
    [InlineData("RowsOnly", "part", false)]
    [InlineData("RowsOnly", "port", false)]
    [InlineData("RowsOnly", "attribute", false)]
    public void GeneralViewLayoutStrategy_NodePolicy_IsNode_KeywordMatrix_ReturnsExpected(string policy, string keyword, bool expected)
    {
        // Arrange: a named usage of the keyword
        var usage = new SysmlFeatureNode { Name = "u", FeatureKeyword = keyword };

        // Act
        var isNode = PolicyByName(policy).IsNode(usage);

        // Assert
        Assert.Equal(expected, isNode);
    }

    /// <summary>
    ///     An unnamed usage, a usage without keyword and a usage whose payload is an expression are never
    ///     nodes, whatever the policy: there is nothing to key or label a box with.
    /// </summary>
    /// <param name="policy">The policy name.</param>
    [Theory]
    [InlineData("Pilot")]
    [InlineData("AllUsages")]
    [InlineData("RowsOnly")]
    public void GeneralViewLayoutStrategy_NodePolicy_IsNode_UnnamedWithoutKeywordOrExpression_IsNeverNode(string policy)
    {
        // Arrange
        var nodePolicy = PolicyByName(policy);

        // Act / Assert
        Assert.False(nodePolicy.IsNode(new SysmlFeatureNode { FeatureKeyword = "part" }));
        Assert.False(nodePolicy.IsNode(new SysmlFeatureNode { Name = "u", FeatureKeyword = string.Empty }));
        Assert.False(nodePolicy.IsNode(new SysmlFeatureNode { Name = "u", FeatureKeyword = "part", ExpressionText = "x > 1" }));
    }

    /// <summary>
    ///     Under the Pilot policy a <c>port</c> usage is a node only when it owns at least one named
    ///     nested feature; a port without nested features, or whose only children are unnamed, stays a row.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_NodePolicy_IsNode_PilotPort_IsNodeOnlyWithNestedContent()
    {
        // Arrange
        var plain = new SysmlFeatureNode { Name = "p", FeatureKeyword = "port" };
        var unnamedChild = new SysmlFeatureNode { Name = "p", FeatureKeyword = "port", Children = [new SysmlFeatureNode { FeatureKeyword = "attribute" }] };
        var withContent = new SysmlFeatureNode
        {
            Name = "p",
            FeatureKeyword = "port",
            Children = [new SysmlFeatureNode { Name = "a", FeatureKeyword = "attribute" }]
        };

        // Act / Assert
        Assert.False(NodePolicy.Pilot.IsNode(plain));
        Assert.False(NodePolicy.Pilot.IsNode(unnamedChild));
        Assert.True(NodePolicy.Pilot.IsNode(withContent));
        Assert.False(NodePolicy.RowsOnly.IsNode(withContent));
    }

    /// <summary>
    ///     Under the Pilot policy a part usage owned by a definition is a rounded node placed beside
    ///     its owner in the owner's own scope (the folder), not inside the owner's box, and the owner
    ///     no longer lists it as a row.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_PilotPolicy_PartUsage_IsOwnNodeBesideOwner()
    {
        // Act
        var layout = await LayOutUnscopedAsync("package P { part def Wheel; part def Vehicle { part wheel : Wheel; } }");

        // Assert: the usage is a rounded sibling of Vehicle inside folder P, and Vehicle has no rows for it
        var folder = FindBox(layout.Nodes, "P");
        var wheel = Assert.Single(folder.Children.OfType<LayoutBox>(), b => IsNamed(b, "wheel"));
        Assert.Equal(BoxShape.RoundedRectangle, wheel.Shape);
        Assert.Equal("part", wheel.Keyword);
        Assert.Equal("wheel : Wheel", wheel.Label);
        var vehicle = Assert.Single(folder.Children.OfType<LayoutBox>(), b => b.Label == "Vehicle");
        Assert.Empty(vehicle.Children);
        Assert.Empty(vehicle.Compartments);
    }

    /// <summary>
    ///     Under the Pilot policy attribute usages and plain reference usages stay compartment rows of
    ///     their owner while a part usage of the same owner becomes a node.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_PilotPolicy_AttributeAndPlainReference_StayRows()
    {
        // Act
        var layout = await LayOutUnscopedAsync("part def Other; part def V { attribute mass : Real; ref link : Other; part p; }");

        // Assert: p is a node, mass and link are rows; no box exists for either row
        var boxes = CollectBoxes(layout.Nodes);
        Assert.Contains(boxes, b => IsNamed(b, "p"));
        Assert.DoesNotContain(boxes, b => IsNamed(b, "mass") || IsNamed(b, "link"));
        var v = FindBox(layout.Nodes, "V");
        Assert.Contains(v.Compartments, c => c.Title == "attributes" && c.Rows.Contains("mass : Real"));
        Assert.Contains(v.Compartments, c => c.Title == "references" && c.Rows.Contains("link : Other"));
        Assert.DoesNotContain(v.Compartments, c => c.Title == "parts");
    }

    /// <summary>
    ///     Under the row-only policy a part usage stays a row of its owner and no usage edge of any kind
    ///     (ownership, typing) is drawn.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_RowsOnlyPolicy_PartUsage_StaysRowAndDrawsNoEdges()
    {
        // Act
        var layout = await LayOutUnscopedAsync("part def W; part def V { part p : W; }", policy: NodePolicy.RowsOnly);

        // Assert
        Assert.DoesNotContain(CollectBoxes(layout.Nodes), b => IsNamed(b, "p"));
        Assert.Contains(FindBox(layout.Nodes, "V").Compartments, c => c.Title == "parts" && c.Rows.Contains("p : W"));
        Assert.Empty(CollectLines(layout.Nodes));
    }

    /// <summary>
    ///     A usage nested in a usage (<c>part def A { part b { part c; } }</c>) is a node whose owner is
    ///     the enclosing usage: the owner chain A, b, c is drawn with a filled diamond from each usage
    ///     to its owner.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_PilotPolicy_NestedUsageInUsage_ChainsOwners()
    {
        // Act
        var layout = await LayOutUnscopedAsync("part def A { part b { part c; } }");

        // Assert: c is owned by b and b by A
        var diamonds = CollectLines(layout.Nodes).Where(l => l.TargetEnd == EndMarkerStyle.FilledDiamond).ToList();
        Assert.Equal(2, diamonds.Count);
        Assert.Contains(diamonds, l => RunsBetween(l, FindBox(layout.Nodes, "b"), FindBox(layout.Nodes, "A")));
        Assert.Contains(diamonds, l => RunsBetween(l, FindBox(layout.Nodes, "c"), FindBox(layout.Nodes, "b")));
    }

    /// <summary>
    ///     The keyword of a usage node carries the prefix words the specification places inside the
    ///     guillemet marks (<c>abstract part</c>, <c>variation part</c>, <c>individual part</c>,
    ///     <c>variant part</c>, <c>ref part</c>); the engine adds the guillemet marks themselves.
    /// </summary>
    /// <param name="isAbstract">Whether the usage is abstract.</param>
    /// <param name="isVariation">Whether the usage is a variation.</param>
    /// <param name="isIndividual">Whether the usage is individual.</param>
    /// <param name="isVariant">Whether the usage is a variant.</param>
    /// <param name="isReference">Whether the usage is a reference usage.</param>
    /// <param name="expected">The expected keyword.</param>
    [Theory]
    [InlineData(false, false, false, false, false, "part")]
    [InlineData(true, false, false, false, false, "abstract part")]
    [InlineData(false, true, false, false, false, "variation part")]
    [InlineData(false, false, true, false, false, "individual part")]
    [InlineData(false, false, false, true, false, "variant part")]
    [InlineData(false, false, false, false, true, "ref part")]
    [InlineData(true, false, false, false, true, "abstract ref part")]
    public void GeneralViewLayoutStrategy_BuildLayout_UsageKeyword_PrefixWords_AreInsideTheKeyword(
        bool isAbstract, bool isVariation, bool isIndividual, bool isVariant, bool isReference, string expected)
    {
        // Arrange: a top-level part usage with the modifiers
        var usage = new SysmlFeatureNode
        {
            Name = "p",
            QualifiedName = "p",
            FeatureKeyword = "part",
            IsAbstract = isAbstract,
            IsVariation = isVariation,
            IsIndividual = isIndividual,
            IsVariant = isVariant,
            IsReference = isReference
        };
        var workspace = new SysmlWorkspace { Declarations = new Dictionary<string, SysmlNode> { ["p"] = usage } };

        // Act
        var layout = new GeneralViewLayoutStrategy().BuildLayout(new ViewContext("v", workspace), new RenderOptions(Themes.Light));

        // Assert
        Assert.Equal(expected, FindBox(layout.Nodes, "p").Keyword);
    }

    /// <summary>
    ///     A plain <c>ref</c> usage (keyword <c>ref</c>, reference by definition) is not prefixed a
    ///     second time with <c>ref</c>.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_PlainReferenceUsage_KeywordIsNotDoubled()
    {
        // Arrange
        var usage = new SysmlFeatureNode { Name = "r", QualifiedName = "r", FeatureKeyword = "ref", IsReference = true };
        var workspace = new SysmlWorkspace { Declarations = new Dictionary<string, SysmlNode> { ["r"] = usage } };

        // Act
        var layout = new GeneralViewLayoutStrategy().BuildLayout(new ViewContext("v", workspace), new RenderOptions(Themes.Light));

        // Assert
        Assert.Equal("ref", FindBox(layout.Nodes, "r").Keyword);
    }

    /// <summary>
    ///     A usage node's name line follows the textual notation: short name, name, typing,
    ///     multiplicity, ordering, then subsetting, redefinition and reference subsetting.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_UsageNameLine_ShowsTypeMultiplicitySubsettingRedefinitionAndReferenceSubsetting()
    {
        // Arrange
        var usage = new SysmlFeatureNode
        {
            Name = "p",
            ShortName = "s",
            QualifiedName = "p",
            FeatureKeyword = "part",
            FeatureTyping = "T",
            Multiplicity = "[2]",
            IsOrdered = true,
            IsNonunique = true,
            SupertypeNames = ["a", "b"],
            RedefinedFeatureName = "r",
            ReferenceSubsettingName = "c"
        };
        var workspace = new SysmlWorkspace { Declarations = new Dictionary<string, SysmlNode> { ["p"] = usage } };

        // Act
        var layout = new GeneralViewLayoutStrategy().BuildLayout(new ViewContext("v", workspace), new RenderOptions(Themes.Light));

        // Assert
        Assert.Equal("<s> p : T [2] ordered nonunique :> a, b :>> r ::> c", Assert.Single(CollectBoxes(layout.Nodes)).Label);
    }

    /// <summary>
    ///     A conjugated typing is shown as <c>~Type</c> in the name line and drawn as a typing edge to
    ///     the underlying port definition.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ConjugatedTyping_ShowsTildeAndTypesTheUsageByThePortDefinition()
    {
        // Act
        var layout = await LayOutUnscopedAsync("port def PortT; part def A { port p : ~PortT { attribute x : Real; } }");

        // Assert
        var p = FindBox(layout.Nodes, "p");
        Assert.Equal("p : ~PortT", p.Label);
        var typing = Assert.Single(CollectLines(layout.Nodes), l => l.TargetEnd == EndMarkerStyle.HollowTriangleTwoDots);
        Assert.True(RunsBetween(typing, p, FindBox(layout.Nodes, "PortT")));
    }

    /// <summary>
    ///     Only a reference usage has a dashed outline; a composite usage keeps the default solid outline.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ReferenceUsage_OutlineIsDashedAndCompositeUsageIsSolid()
    {
        // Act
        var layout = await LayOutUnscopedAsync("part def T; part def V { part own : T; ref part shared : T; }");

        // Assert
        Assert.Equal(LineStyle.Dashed, FindBox(layout.Nodes, "shared").Appearance?.OutlineStyle);
        Assert.NotEqual(LineStyle.Dashed, FindBox(layout.Nodes, "own").Appearance?.OutlineStyle ?? LineStyle.Solid);
    }

    /// <summary>
    ///     The name of an abstract definition is italic (the specification's Table 4) and its keyword
    ///     reads <c>abstract part def</c>; the name of an abstract usage stays upright.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_AbstractDefinition_NameIsItalicButAbstractUsageIsNot()
    {
        // Arrange: an abstract part def and an abstract part usage
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["D"] = new SysmlDefinitionNode { Name = "D", QualifiedName = "D", DefinitionKeyword = "part def", IsAbstract = true },
                ["u"] = new SysmlFeatureNode { Name = "u", QualifiedName = "u", FeatureKeyword = "part", IsAbstract = true }
            }
        };

        // Act
        var layout = new GeneralViewLayoutStrategy().BuildLayout(new ViewContext("v", workspace), new RenderOptions(Themes.Light));

        // Assert
        var definition = FindBox(layout.Nodes, "D");
        Assert.Equal("abstract part def", definition.Keyword);
        Assert.Equal(TextStyle.Italic, definition.Appearance?.NameStyle);
        var usage = FindBox(layout.Nodes, "u");
        Assert.Equal("abstract part", usage.Keyword);
        Assert.Null(usage.Appearance?.NameStyle);
    }

    /// <summary>
    ///     A row follows the textual notation: a direction keyword, a conjugated type, multiplicity with
    ///     its ordering modifiers, subsetting and redefinition clauses and the value clause.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_FeatureRows_UseTextualNotation()
    {
        // Arrange: one definition owning rows of every shape (row-only policy)
        var def = new SysmlDefinitionNode
        {
            Name = "D",
            QualifiedName = "D",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "speed", FeatureKeyword = "attribute", FeatureTyping = "Real", Direction = SysmlFeatureDirection.In },
                new SysmlFeatureNode { Name = "xs", FeatureKeyword = "attribute", FeatureTyping = "Real", Multiplicity = "[0..*]", IsOrdered = true, IsNonunique = true },
                new SysmlFeatureNode { Name = "m", FeatureKeyword = "attribute", FeatureTyping = "Mass", SupertypeNames = ["base"], RedefinedFeatureName = "old" },
                new SysmlFeatureNode { Name = "mass", FeatureKeyword = "attribute", FeatureTyping = "Real", ValueText = "= 5 [kg]" },
                new SysmlFeatureNode { Name = "q", FeatureKeyword = "attribute", ConjugatedFeatureTyping = "PortT", Direction = SysmlFeatureDirection.Out }
            ]
        };
        var workspace = new SysmlWorkspace { Declarations = new Dictionary<string, SysmlNode> { ["D"] = def } };

        // Act
        var layout = new GeneralViewLayoutStrategy(NodePolicy.RowsOnly).BuildLayout(new ViewContext("v", workspace), new RenderOptions(Themes.Light));

        // Assert
        var rows = FindBox(layout.Nodes, "D").Compartments.Single(c => c.Title == "attributes").Rows;
        Assert.Equal(
            ["in speed : Real", "xs : Real [0..*] ordered nonunique", "m : Mass :> base :>> old", "mass : Real = 5 [kg]", "out q : ~PortT"],
            rows);
    }

    /// <summary>
    ///     A usage that reference-subsets another (<c>part b ::&gt; a</c>) draws a solid line from
    ///     <c>b</c> to <c>a</c> with a hollow triangle with four dots at <c>a</c>; an unresolvable
    ///     reference draws nothing.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ReferenceSubsetting_ProducesHollowTriangleFourDotsEdgeOnlyWhenResolved()
    {
        // Act
        var resolved = await LayOutUnscopedAsync("part def D { part a; part b ::> a; }");
        var unresolved = await LayOutUnscopedAsync("part def D { part a; part b ::> nothing; }");

        // Assert
        var edge = Assert.Single(CollectLines(resolved.Nodes), l => l.TargetEnd == EndMarkerStyle.HollowTriangleFourDots);
        Assert.Equal(LineStyle.Solid, edge.LineStyle);
        Assert.True(RunsBetween(edge, FindBox(resolved.Nodes, "b"), FindBox(resolved.Nodes, "a")));
        Assert.DoesNotContain(CollectLines(unresolved.Nodes), l => l.TargetEnd == EndMarkerStyle.HollowTriangleFourDots);
    }

    /// <summary>
    ///     With a depth limit that does not permit the usage nodes of a definition, the definition stays
    ///     a leaf whose last compartment row is the specification's truncation marker (<c>…</c>), and no
    ///     usage node or edge is drawn.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_DepthLimit_OwnerWithUsageNodes_StaysLeafWithEllipsisRow()
    {
        // Act
        var layout = await LayOutUnscopedAsync("part def W; part def V { part p : W; }", depthLimit: 1);

        // Assert
        Assert.DoesNotContain(CollectBoxes(layout.Nodes), b => IsNamed(b, "p"));
        var v = FindBox(layout.Nodes, "V");
        Assert.Equal("\u2026", v.Compartments[^1].Rows[^1]);
        Assert.DoesNotContain(CollectLines(layout.Nodes), l => l.TargetEnd is EndMarkerStyle.FilledDiamond or EndMarkerStyle.HollowTriangleTwoDots);
    }

    /// <summary>
    ///     A recursive expose (<c>P::**</c>) and a namespace expose (<c>P::*</c>) both draw the usage
    ///     owned by the exposed definition as a node of its own joined to the definition by a filled
    ///     diamond, and neither lists it a second time as a <c>parts</c> row.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ScopedExpose_RecursiveAndNamespace_BothDrawUsageNodesAsNodes()
    {
        // Arrange
        const string model = """
            package P { part def W; part def V { part p : W; } }
            package Diagrams {
                view Deep { expose P::**; }
                view Shallow { expose P::*; }
            }
            """;

        // Act
        var deep = await LayOutDeclaredViewAsync(model, "Deep");
        var shallow = await LayOutDeclaredViewAsync(model, "Shallow");

        // Assert
        foreach (var layout in new[] { deep, shallow })
        {
            var p = FindBox(layout.Nodes, "p");
            var v = FindBox(layout.Nodes, "V");
            Assert.Contains(CollectLines(layout.Nodes), l => l.TargetEnd == EndMarkerStyle.FilledDiamond && RunsBetween(l, p, v));
            Assert.DoesNotContain(v.Compartments, c => c.Title == "parts");
        }
    }

    /// <summary>
    ///     An exposed part definition (<c>expose V;</c>) is drawn with each part it owns as a rounded
    ///     node joined to it by a filled diamond, instead of as <c>parts</c> rows; its attribute stays a
    ///     row, and the definitions that the parts are typed by are not pulled into the diagram.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ExposedPartDef_DrawsOwnedPartsAsNodesJoinedByFilledDiamond()
    {
        // Act
        var layout = await LayOutDeclaredViewAsync(PartTreeModel("expose V;"), "Tree");

        // Assert: both parts are rounded nodes joined to V by filled diamonds
        var v = FindBox(layout.Nodes, "V");
        foreach (var name in new[] { "a", "b" })
        {
            var node = FindBox(layout.Nodes, name);
            Assert.Equal(BoxShape.RoundedRectangle, node.Shape);
            Assert.Contains(CollectLines(layout.Nodes), l => l.TargetEnd == EndMarkerStyle.FilledDiamond && RunsBetween(l, node, v));
        }

        // Assert: no parts rows, the attribute stays a row, and the typing definitions are absent
        Assert.DoesNotContain(v.Compartments, c => c.Title == "parts");
        Assert.Contains(v.Compartments, c => c.Title == "attributes" && c.Rows.Contains("m : Real"));
        Assert.DoesNotContain(CollectBoxes(layout.Nodes), b => b.Label is "A" or "B" or "R");
        Assert.DoesNotContain(CollectLines(layout.Nodes), l => l.TargetEnd == EndMarkerStyle.HollowTriangleTwoDots);
    }

    /// <summary>
    ///     When the definitions that an exposed definition's parts are typed by are exposed as well, each
    ///     part is joined to its definition by a feature-typing edge (hollow triangle with two dots).
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ExposedPartDefAndItsTypes_DrawsTypingEdgeFromUsageToDefinition()
    {
        // Act
        var layout = await LayOutDeclaredViewAsync(PartTreeModel("expose V; expose A;"), "Tree");

        // Assert: a is typed by the exposed A; b is typed by B, which is not exposed, so no edge
        var typing = Assert.Single(CollectLines(layout.Nodes), l => l.TargetEnd == EndMarkerStyle.HollowTriangleTwoDots);
        Assert.True(RunsBetween(typing, FindBox(layout.Nodes, "a"), FindBox(layout.Nodes, "A")));
        Assert.DoesNotContain(CollectBoxes(layout.Nodes), b => b.Label == "B");
    }

    /// <summary>
    ///     A reference part owned by an exposed definition is a dashed node joined to its owner by a
    ///     hollow diamond.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ExposedPartDef_ReferencePartUsesHollowDiamondAndDashedNode()
    {
        // Act
        var layout = await LayOutDeclaredViewAsync(PartTreeModel("expose V;"), "Tree");

        // Assert
        var r = FindBox(layout.Nodes, "r");
        Assert.Equal(LineStyle.Dashed, r.Appearance?.OutlineStyle);
        Assert.Contains(
            CollectLines(layout.Nodes),
            l => l.TargetEnd == EndMarkerStyle.HollowDiamond && RunsBetween(l, r, FindBox(layout.Nodes, "V")));
    }

    /// <summary>
    ///     A part nested inline in a part of an exposed definition is drawn too, chained to its owner:
    ///     the closure follows ownership one usage after the other.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ExposedPartDef_NestedInlineUsageChainsOwners()
    {
        // Act
        var layout = await LayOutDeclaredViewAsync("""
            part def V { part e { part c; } }
            package Diagrams { view Tree { expose V; } }
            """, "Tree");

        // Assert
        var diamonds = CollectLines(layout.Nodes).Where(l => l.TargetEnd == EndMarkerStyle.FilledDiamond).ToList();
        Assert.Equal(2, diamonds.Count);
        Assert.Contains(diamonds, l => RunsBetween(l, FindBox(layout.Nodes, "e"), FindBox(layout.Nodes, "V")));
        Assert.Contains(diamonds, l => RunsBetween(l, FindBox(layout.Nodes, "c"), FindBox(layout.Nodes, "e")));
    }

    /// <summary>
    ///     A recursive expose draws every box exactly once — an owned part is a node, never also a
    ///     <c>parts</c> row — and combining it with the exposed owner (<c>expose V; expose V::**;</c>)
    ///     does not draw anything twice.
    /// </summary>
    [Theory]
    [InlineData("expose V::**;")]
    [InlineData("expose V; expose V::**;")]
    [InlineData("expose V::**; expose V;")]
    public async Task GeneralViewLayoutStrategy_BuildLayout_RecursiveExpose_DoesNotDuplicateUsageNodes(string exposes)
    {
        // Act
        var layout = await LayOutDeclaredViewAsync(PartTreeModel(exposes), "Tree");

        // Assert: each box label occurs once, and the parts are not also rows
        var boxes = CollectBoxes(layout.Nodes);
        Assert.Equal(boxes.Count, boxes.Select(b => b.Label).Distinct().Count());
        foreach (var name in new[] { "a", "b", "r" })
        {
            FindBox(layout.Nodes, name);
        }

        var v = FindBox(layout.Nodes, "V");
        Assert.DoesNotContain(v.Compartments, c => c.Title is "parts" or "references");
        Assert.Equal(3, CollectLines(layout.Nodes).Count(l => l.TargetEnd is EndMarkerStyle.FilledDiamond or EndMarkerStyle.HollowDiamond));
    }

    /// <summary>
    ///     An exposed part usage draws the parts it owns as nodes, in the same way an exposed definition does.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ExposedUsage_DrawsOwnedPartsAsNodes()
    {
        // Act
        var layout = await LayOutDeclaredViewAsync("""
            part def Car;
            part car : Car { part engine; part wheels; }
            package Diagrams { view Tree { expose car; } }
            """, "Tree");

        // Assert
        var car = FindBox(layout.Nodes, "car");
        foreach (var name in new[] { "engine", "wheels" })
        {
            Assert.Contains(
                CollectLines(layout.Nodes),
                l => l.TargetEnd == EndMarkerStyle.FilledDiamond && RunsBetween(l, FindBox(layout.Nodes, name), car));
        }

        Assert.DoesNotContain(car.Compartments, c => c.Title == "parts");
    }

    /// <summary>
    ///     Exposing a usage (<c>expose myVehicle;</c>) also draws its typing definition, but only as a
    ///     box: the definition is not itself exposed, so the parts it owns stay <c>parts</c> rows, while
    ///     the parts the exposed usage owns are drawn as nodes.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ExposedUsage_TypingDefinitionIsBoxWithoutPartTree()
    {
        // Act
        var layout = await LayOutDeclaredViewAsync("""
            part def Wheel;
            part def Vehicle { part wheel : Wheel; part engine; }
            part myVehicle : Vehicle { part body; }
            package Diagrams { view Tree { expose myVehicle; } }
            """, "Tree");

        // Assert: the typing definition and the usage are boxes; the usage's own part is a node
        var vehicle = FindBox(layout.Nodes, "Vehicle");
        var myVehicle = FindBox(layout.Nodes, "myVehicle");
        var body = FindBox(layout.Nodes, "body");
        Assert.Contains(
            CollectLines(layout.Nodes),
            l => l.TargetEnd == EndMarkerStyle.FilledDiamond && RunsBetween(l, body, myVehicle));

        // Assert: the definition's own parts are rows of it, not nodes of the diagram
        Assert.DoesNotContain(CollectBoxes(layout.Nodes), b => IsNamed(b, "wheel") || IsNamed(b, "engine") || b.Label == "Wheel");
        Assert.Contains(vehicle.Compartments, c => c.Title == "parts" && c.Rows.Contains("wheel : Wheel") && c.Rows.Contains("engine"));
    }

    /// <summary>
    ///     A bracket-filtered expose is a precise selection: the selected definition is drawn but its
    ///     parts, which the filter does not select, stay rows of it.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_BracketFilteredExpose_KeepsUnselectedUsagesAsRows()
    {
        // Act
        var layout = await LayOutDeclaredViewAsync("""
            package P {
                metadata def Safety;
                part def W;
                part def Safe { @Safety; part w : W; }
                part def Other { part x : W; }
            }
            package Diagrams { view Tree { expose P::**[@Safety]; } }
            """, "Tree");

        // Assert
        var safe = FindBox(layout.Nodes, "Safe");
        Assert.DoesNotContain(CollectBoxes(layout.Nodes), b => IsNamed(b, "w") || b.Label == "Other");
        Assert.Contains(safe.Compartments, c => c.Title == "parts" && c.Rows.Contains("w : W"));
    }

    /// <summary>
    ///     Part trees follow the depth limit exactly as unscoped views do: an exposed definition at depth
    ///     one is a leaf with a trailing <c>…</c> row and no part node while the limit does not reach its
    ///     parts (limits one and two), and the parts are drawn once the limit does (three).
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_DepthLimit_ExposedDefWithUsageNodes_TruncatesToEllipsisThenDrawsNodes()
    {
        // Arrange
        var model = PartTreeModel("expose V;");

        // Act
        var limitOne = await LayOutDeclaredViewAsync(model, "Tree", depthLimit: 1);
        var limitTwo = await LayOutDeclaredViewAsync(model, "Tree", depthLimit: 2);
        var limitThree = await LayOutDeclaredViewAsync(model, "Tree", depthLimit: 3);

        // Assert
        foreach (var truncated in new[] { limitOne, limitTwo })
        {
            Assert.DoesNotContain(CollectBoxes(truncated.Nodes), b => IsNamed(b, "a"));
            Assert.Equal("\u2026", FindBox(truncated.Nodes, "V").Compartments[^1].Rows[^1]);
        }

        Assert.Contains(CollectBoxes(limitThree.Nodes), b => IsNamed(b, "a"));
    }

    /// <summary>
    ///     A view condition that rejects the usages (here: keeping only part definitions) drops the part
    ///     nodes again, and the owner lists its parts as rows.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ViewConditionRejectingUsage_KeepsItAsRow()
    {
        // Act
        var layout = await LayOutDeclaredViewAsync(
            PartTreeModel("expose V; filter @SysML::PartDefinition;"), "Tree");

        // Assert
        Assert.DoesNotContain(CollectBoxes(layout.Nodes), b => IsNamed(b, "a") || IsNamed(b, "b"));
        Assert.Contains(FindBox(layout.Nodes, "V").Compartments, c => c.Title == "parts" && c.Rows.Contains("a : A"));
    }

    /// <summary>The part-tree model with a view <c>Tree</c> carrying the given <paramref name="viewBody"/> statements.</summary>
    private static string PartTreeModel(string viewBody) => $$"""
        part def A;
        part def B;
        part def R;
        part def V {
            part a : A;
            part b : B;
            ref part r : R;
            attribute m : Real;
        }
        package Diagrams { view Tree { {{viewBody}} } }
        """;

    /// <summary>
    ///     Two usage nodes of different owners that read exactly alike (<c>data : P</c>) are both drawn
    ///     and the layout completes, although the layout cannot tell their boxes apart by label.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_IdenticalUsageNodesOfDifferentOwners_AreBothDrawn()
    {
        // Act
        var layout = await LayOutUnscopedAsync("""
            package Q {
                port def P;
                part def A { port data : P { attribute x : Real; } }
                part def B { port data : P { attribute x : Real; } }
            }
            """);

        // Assert
        Assert.Equal(2, CollectBoxes(layout.Nodes).Count(b => b.Label == "data : P"));
        Assert.Equal(2, CollectLines(layout.Nodes).Count(l => l.TargetEnd == EndMarkerStyle.HollowTriangleTwoDots));
    }

    /// <summary>
    ///     A nested part's own <c>doc</c> is a compartment of the part's own node (not of its owner), and a
    ///     <c>comment</c> on the nested part is a note joined to the part's own node by a dashed line
    ///     without arrowhead.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_NestedPartDocAndComment_AttachToOwnNode()
    {
        // Act
        var layout = await LayOutUnscopedAsync("""
            part def Vehicle {
                part engine {
                    doc /* The propulsion unit. */
                    comment /* Sized for 120 kW. */
                }
            }
            """);

        // Assert: the doc is on engine, not on Vehicle
        var engine = FindBox(layout.Nodes, "engine");
        Assert.Contains(engine.Compartments, c => c.Title == "doc" && c.Rows.Any(r => r.Contains("propulsion", StringComparison.Ordinal)));
        Assert.DoesNotContain(FindBox(layout.Nodes, "Vehicle").Compartments, c => c.Title == "doc");

        // Assert: the comment is a note linked to engine by a dashed line without an arrowhead
        var note = Assert.Single(CollectBoxes(layout.Nodes), b => b.Shape == BoxShape.Note);
        var link = Assert.Single(CollectLines(layout.Nodes), l => l.LineStyle == LineStyle.Dashed && l.TargetEnd == EndMarkerStyle.None);
        Assert.True(RunsBetween(link, engine, note));
    }

    /// <summary>
    ///     A <c>satisfy</c> relationship is drawn from the satisfying element to the requirement as a
    ///     solid line with an open arrowhead and the <c>«satisfy»</c> label.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_Satisfy_ProducesSolidOpenArrowEdgeWithKeywordLabel()
    {
        // Act
        var layout = await LayOutUnscopedAsync("""
            package P {
                requirement def R;
                requirement req : R;
                part def Q;
                part subj : Q;
                satisfy req by subj;
            }
            """);

        // Assert
        var edge = Assert.Single(CollectLines(layout.Nodes), l => l.MidpointLabel == "«satisfy»");
        Assert.Equal(EndMarkerStyle.OpenChevron, edge.TargetEnd);
        Assert.Equal(LineStyle.Solid, edge.LineStyle);
        Assert.True(RunsBetween(edge, FindBox(layout.Nodes, "subj"), FindBox(layout.Nodes, "req")));
    }

    /// <summary>
    ///     A <c>verify</c> relationship is drawn from the verifying element to the requirement with the
    ///     <c>«verify»</c> label; when the verifying declaration is nested in a rendered box (an
    ///     objective of a verification definition) the nearest rendered box on its path is the source.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_Verify_NestedSourceMapsToNearestRenderedBox()
    {
        // Arrange: a verification definition V (rendered) whose nested objective verifies requirement req
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::req"] = new SysmlFeatureNode { Name = "req", QualifiedName = "P::req", FeatureKeyword = "requirement" },
                ["P::V"] = new SysmlDefinitionNode { Name = "V", QualifiedName = "P::V", DefinitionKeyword = "verification def" }
            },
            Index = new SemanticIndex([new SysmlEdge("P::V::obj", "P::req", SysmlEdgeKind.Verify)])
        };

        // Act
        var layout = new GeneralViewLayoutStrategy().BuildLayout(new ViewContext("v", workspace), new RenderOptions(Themes.Light));

        // Assert
        var edge = Assert.Single(CollectLines(layout.Nodes), l => l.MidpointLabel == "«verify»");
        Assert.Equal(EndMarkerStyle.OpenChevron, edge.TargetEnd);
        Assert.Equal(LineStyle.Solid, edge.LineStyle);
        Assert.True(RunsBetween(edge, FindBox(layout.Nodes, "V"), FindBox(layout.Nodes, "req")));
    }

    /// <summary>
    ///     An import is drawn from the importing package's folder to the imported package's folder (or
    ///     the imported definition's box) as a dashed line with an open arrowhead, labeled <c>«import»</c>
    ///     for a membership import, <c>«import»*</c> for a namespace import and <c>«import»**</c> for a
    ///     recursive one.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_Imports_ProduceDashedOpenArrowEdgesLabeledByKind()
    {
        // Act
        var layout = await LayOutUnscopedAsync("""
            package A { part def X; }
            package B { private import A::*; part def Y; }
            package C { import A::**; part def Z; }
            package D { import A::X; part def W; }
            """);

        // Assert: one dashed open-arrow edge per import, labeled by kind, from the importer's folder
        var imports = CollectLines(layout.Nodes).Where(l => l.TargetEnd == EndMarkerStyle.OpenChevron && l.LineStyle == LineStyle.Dashed).ToList();
        Assert.Equal(3, imports.Count);
        var namespaceImport = Assert.Single(imports, l => l.MidpointLabel == "«import»*");
        Assert.True(RunsBetween(namespaceImport, FindBox(layout.Nodes, "B"), FindBox(layout.Nodes, "A")));
        var recursiveImport = Assert.Single(imports, l => l.MidpointLabel == "«import»**");
        Assert.True(RunsBetween(recursiveImport, FindBox(layout.Nodes, "C"), FindBox(layout.Nodes, "A")));
        var membershipImport = Assert.Single(imports, l => l.MidpointLabel == "«import»");
        Assert.True(RunsBetween(membershipImport, FindBox(layout.Nodes, "D"), FindBox(layout.Nodes, "X")));
    }

    /// <summary>
    ///     A namespace import and a recursive import of the same package, written by one importer, are
    ///     two distinct relationships: both edges are drawn between the same two folders, each with its
    ///     own label, and neither replaces the other.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_Imports_NamespaceAndRecursiveOfSamePackage_DrawTwoEdgesWithDistinctLabels()
    {
        // Act
        var layout = await LayOutUnscopedAsync("""
            package A { part def X; }
            package B { import A::*; import A::**; part def Y; }
            """);

        // Assert
        var imports = CollectLines(layout.Nodes).Where(l => l.TargetEnd == EndMarkerStyle.OpenChevron && l.LineStyle == LineStyle.Dashed).ToList();
        Assert.Equal(2, imports.Count);
        var namespaceImport = Assert.Single(imports, l => l.MidpointLabel == "«import»*");
        var recursiveImport = Assert.Single(imports, l => l.MidpointLabel == "«import»**");
        var importer = FindBox(layout.Nodes, "B");
        var imported = FindBox(layout.Nodes, "A");
        foreach (var edge in imports)
        {
            // The edge ends on the imported folder and starts within the importing folder's span
            Assert.True(OnBorder(edge.Waypoints[^1].X, edge.Waypoints[^1].Y, imported));
            Assert.InRange(edge.Waypoints[0].X, importer.X, importer.X + importer.Width);
        }

        // Assert: the two parallel edges do not run exactly on top of each other
        Assert.NotEqual(
            namespaceImport.Waypoints.Select(p => (p.X, p.Y)),
            recursiveImport.Waypoints.Select(p => (p.X, p.Y)));
    }

    /// <summary>
    ///     A membership import and a namespace import that resolve to the same imported box are two
    ///     distinct relationships and draw two edges with distinct labels.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_Imports_MembershipAndNamespaceOfSameTarget_DrawTwoEdgesWithDistinctLabels()
    {
        // Act
        var layout = await LayOutUnscopedAsync("""
            package A { part def X; }
            package B { import A::X; import A::X::*; part def Y; }
            """);

        // Assert: both imports end at the box of X
        var imports = CollectLines(layout.Nodes).Where(l => l.TargetEnd == EndMarkerStyle.OpenChevron && l.LineStyle == LineStyle.Dashed).ToList();
        Assert.Equal(2, imports.Count);
        Assert.Single(imports, l => l.MidpointLabel == "«import»");
        Assert.Single(imports, l => l.MidpointLabel == "«import»*");
    }

    /// <summary>
    ///     Two identical imports (same importer, same imported namespace, same kind) draw one edge.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_Imports_ExactDuplicate_DrawsOneEdge()
    {
        // Act
        var layout = await LayOutUnscopedAsync("""
            package A { part def X; }
            package B { import A::*; import A::*; part def Y; }
            """);

        // Assert
        var imports = CollectLines(layout.Nodes).Where(l => l.TargetEnd == EndMarkerStyle.OpenChevron && l.LineStyle == LineStyle.Dashed).ToList();
        var edge = Assert.Single(imports);
        Assert.Equal("«import»*", edge.MidpointLabel);
    }

    /// <summary>
    ///     A <c>satisfy</c> and a <c>verify</c> relationship between the same pair of boxes are two
    ///     distinct relationships and draw two edges with their own keyword labels, while a repeated
    ///     <c>satisfy</c> of the same pair draws one edge.
    /// </summary>
    [Fact]
    public void GeneralViewLayoutStrategy_BuildLayout_SatisfyAndVerifyOfSamePair_DrawTwoEdgesAndRepeatsCoalesce()
    {
        // Arrange
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::req"] = new SysmlFeatureNode { Name = "req", QualifiedName = "P::req", FeatureKeyword = "requirement" },
                ["P::V"] = new SysmlDefinitionNode { Name = "V", QualifiedName = "P::V", DefinitionKeyword = "verification def" }
            },
            Index = new SemanticIndex(
            [
                new SysmlEdge("P::V", "P::req", SysmlEdgeKind.Satisfy),
                new SysmlEdge("P::V::obj", "P::req", SysmlEdgeKind.Verify),
                new SysmlEdge("P::V", "P::req", SysmlEdgeKind.Satisfy)
            ])
        };

        // Act
        var layout = new GeneralViewLayoutStrategy().BuildLayout(new ViewContext("v", workspace), new RenderOptions(Themes.Light));

        // Assert
        var lines = CollectLines(layout.Nodes).Where(l => l.TargetEnd == EndMarkerStyle.OpenChevron).ToList();
        Assert.Equal(2, lines.Count);
        Assert.Single(lines, l => l.MidpointLabel == "«satisfy»");
        Assert.Single(lines, l => l.MidpointLabel == "«verify»");
    }

    /// <summary>
    ///     An import of the standard library (no rendered endpoint) and an import between a package and a
    ///     package nested in it draw no edge and produce no warning.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_Imports_StdlibAndContainedNamespace_DrawNoEdge()
    {
        // Act
        var layout = await LayOutUnscopedAsync("""
            package Outer {
                import ScalarValues::*;
                import Inner::*;
                package Inner { part def I; }
                part def O;
            }
            """);

        // Assert
        Assert.DoesNotContain(CollectLines(layout.Nodes), l => l.LineStyle == LineStyle.Dashed);
        Assert.Empty(layout.Warnings);
    }

    /// <summary>
    ///     Asserts that every connector with an end decoration that reaches it over a lane (four or more
    ///     waypoints) ends with a straight leg at least as long as the decoration plus one corner radius,
    ///     and returns the number of connectors checked.
    /// </summary>
    private static int AssertEndDecorationsHaveCleanApproach(LayoutTree layout, Theme theme)
    {
        var checkedLines = 0;
        foreach (var line in CollectLines(layout.Nodes).Where(l => l.TargetEnd != EndMarkerStyle.None && l.Waypoints.Count >= 4))
        {
            var n = line.Waypoints.Count;
            var leg = Math.Abs(line.Waypoints[n - 1].X - line.Waypoints[n - 2].X) + Math.Abs(line.Waypoints[n - 1].Y - line.Waypoints[n - 2].Y);
            var required = NotationMetrics.RenderedAlongLineLength(line.TargetEnd, theme) + theme.LineCornerRadius;
            Assert.True(leg + 0.001 >= required, $"{line.TargetEnd} decoration has a final leg of {leg:F2} px, shorter than the required {required:F2} px");
            checkedLines++;
        }

        return checkedLines;
    }

    /// <summary>
    ///     The first lane out of an owner face lies a decoration length away from the box or farther: a
    ///     charger-like owner with two part usages draws both filled diamonds with a straight approach of
    ///     at least the diamond length plus the corner radius (diagram-quality C10), the same distance
    ///     the engine keeps at the target end of an edge.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_OwnerFanOut_EndDecorationsHaveCleanApproach()
    {
        // Act
        var layout = await LayOutUnscopedAsync("""
            part def Cable; part def Plug;
            part def Charger { part cable : Cable; part plug : Plug; }
            """);

        // Assert: both composition diamonds are drawn and each sits on a straight final leg at least as
        // long as the diamond, whether the engine routes the connector straight or over a lane (beta.4
        // routes both straight, so the lane-only helper has nothing to check here); any that does reach
        // the owner over a lane also has the full clean approach.
        var theme = Themes.Light;
        var diamonds = CollectLines(layout.Nodes).Where(l => l.TargetEnd == EndMarkerStyle.FilledDiamond).ToList();
        Assert.Equal(2, diamonds.Count);
        var diamondLength = NotationMetrics.RenderedAlongLineLength(EndMarkerStyle.FilledDiamond, theme);
        foreach (var diamond in diamonds)
        {
            var n = diamond.Waypoints.Count;
            var leg = Math.Abs(diamond.Waypoints[n - 1].X - diamond.Waypoints[n - 2].X) + Math.Abs(diamond.Waypoints[n - 1].Y - diamond.Waypoints[n - 2].Y);
            Assert.True(leg + 0.001 >= diamondLength, $"The diamond has a final leg of {leg:F2} px, shorter than the diamond ({diamondLength:F2} px).");
        }

        var withLane = diamonds.Count(d => d.Waypoints.Count >= 4);
        Assert.Equal(withLane, AssertEndDecorationsHaveCleanApproach(layout, theme));
    }

    /// <summary>
    ///     A hollow diamond (reference usage), a filled diamond (composite usage) and a specialization
    ///     triangle that end on the same face of one owner are all moved clear of their bends: each has a
    ///     straight approach of its own decoration length plus the corner radius.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_SpecializationAndReferenceUsageFanOut_EndDecorationsHaveCleanApproach()
    {
        // Act
        var layout = await LayOutUnscopedAsync("""
            abstract part def Motor;
            part def InductionMotor :> Motor;
            part def DriveUnit {
                part motors : Motor [2];
                part main : Motor;
                ref part spare : InductionMotor;
            }
            part def RearDrive :> DriveUnit { part frontMotor :> motors; part mainMotor :>> main; }
            """);

        // Assert: the three kinds of owner-end decoration are present and all have a clean approach
        var lines = CollectLines(layout.Nodes);
        Assert.Contains(lines, l => l.TargetEnd == EndMarkerStyle.HollowDiamond);
        Assert.Contains(lines, l => l.TargetEnd == EndMarkerStyle.FilledDiamond);
        Assert.Contains(lines, l => l.TargetEnd == EndMarkerStyle.HollowTriangle);
        Assert.True(AssertEndDecorationsHaveCleanApproach(layout, Themes.Light) >= 3);
    }

    /// <summary>A wide drone-like model (seven usages in one row, typed definitions below) plus nine definitions that nothing refers to.</summary>
    private const string WideTreeWithIsolatedDefinitions = """
        part def Frame; part def Propeller;
        part def Battery { port output : PowerPort; }
        part def FlightController { port power : PowerPort; port sensors : SensorPort; }
        part def Motor { port control : MotorControlPort; }
        part def Motor2 :> Motor; part def Motor3 :> Motor2; part def Motor4 :> Motor3; part def Motor5 :> Motor4;
        part def ImuSensor { port data : SensorPort; }
        part def GpsSensor { port data : SensorPort; }
        part def Drone {
            part airframe : Frame; part battery : Battery; part controller : FlightController;
            part motors : Motor[4]; part propellers : Propeller[4]; part imu : ImuSensor; part gps : GpsSensor;
        }
        part def Lonely1; part def Lonely2; part def Lonely3; part def Lonely4; part def Lonely5;
        part def Lonely6; part def Lonely7; part def Lonely8; part def Lonely9;
        """;

    /// <summary>The labels of the nine isolated definitions of <see cref="WideTreeWithIsolatedDefinitions"/>.</summary>
    private static readonly string[] IsolatedLabels = [.. Enumerable.Range(1, 9).Select(i => $"Lonely{i}")];

    /// <summary>Returns the bounds of the boxes and connector waypoints of the connected part of a layout: everything except the boxes labeled <paramref name="isolated"/>.</summary>
    private static (double Left, double Top, double Right, double Bottom) ConnectedBounds(LayoutTree layout, IReadOnlyCollection<string> isolated)
    {
        var boxes = CollectBoxes(layout.Nodes).Where(b => b.Shape != BoxShape.Folder && !isolated.Contains(b.Label ?? string.Empty)).ToList();
        var points = CollectLines(layout.Nodes).SelectMany(l => l.Waypoints).ToList();
        return (
            Math.Min(boxes.Min(b => b.X), points.Min(p => p.X)),
            Math.Min(boxes.Min(b => b.Y), points.Min(p => p.Y)),
            Math.Max(boxes.Max(b => b.X + b.Width), points.Max(p => p.X)),
            Math.Max(boxes.Max(b => b.Y + b.Height), points.Max(p => p.Y)));
    }

    /// <summary>Returns whether any segment of any connector of <paramref name="layout"/> passes through the box.</summary>
    private static bool AnyConnectorCrosses(LayoutTree layout, LayoutBox box)
    {
        foreach (var waypoints in CollectLines(layout.Nodes).Select(l => l.Waypoints))
        {
            for (var i = 1; i < waypoints.Count; i++)
            {
                var (minX, maxX) = (Math.Min(waypoints[i - 1].X, waypoints[i].X), Math.Max(waypoints[i - 1].X, waypoints[i].X));
                var (minY, maxY) = (Math.Min(waypoints[i - 1].Y, waypoints[i].Y), Math.Max(waypoints[i - 1].Y, waypoints[i].Y));
                if (maxX > box.X && minX < box.X + box.Width && maxY > box.Y && minY < box.Y + box.Height)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     The definitions that nothing refers to are placed in the free space of a wide connected tree
    ///     instead of beside it (diagram-quality B4/B5): each lies inside the bounds of the connected
    ///     drawing, none overlaps another box or touches a connector, every one is drawn exactly once,
    ///     and every box is inside the canvas.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_ManyUnconnectedDefinitions_AreTuckedIntoFreeCornerOfWideTree()
    {
        // Act
        var layout = await LayOutUnscopedAsync(WideTreeWithIsolatedDefinitions);

        // Assert: every isolated definition is drawn once, inside the connected part's bounds
        var (left, top, right, bottom) = ConnectedBounds(layout, IsolatedLabels);
        foreach (var label in IsolatedLabels)
        {
            var box = FindBox(layout.Nodes, label);
            Assert.True(box.X >= left && box.Y >= top && box.X + box.Width <= right && box.Y + box.Height <= bottom, $"{label} is outside the connected drawing");
            Assert.False(AnyConnectorCrosses(layout, box), $"a connector crosses {label}");
            Assert.True(box.X + box.Width <= layout.Width && box.Y + box.Height <= layout.Height, $"{label} is outside the canvas");
        }

        // Assert: nothing overlaps, and the canvas is no wider than the connected drawing plus its folder margin
        AssertDefinitionBoxesDoNotOverlap(layout.Nodes);
        Assert.True(layout.Width <= right + 40.0, $"the canvas ({layout.Width:F0}) is wider than the connected drawing ({right:F0})");
    }

    /// <summary>
    ///     When the connected drawing has no free region big enough (a small tree and several isolated
    ///     definitions) the engine's own packing is kept: the isolated definitions sit outside the bounds
    ///     of the connected drawing, in the engine's shelf, with no overlap.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_UnconnectedDefinitions_NoFreeSpace_KeepEngineShelf()
    {
        // Act
        var layout = await LayOutUnscopedAsync("""
            part def A; part def B :> A;
            part def U1; part def U2; part def U3; part def U4; part def U5;
            """);

        // Assert: all seven definitions are drawn, the isolated ones outside the connected bounds, none overlapping
        string[] isolated = ["U1", "U2", "U3", "U4", "U5"];
        var (left, top, right, bottom) = ConnectedBounds(layout, isolated);
        Assert.Equal(7, CollectBoxes(layout.Nodes).Count(b => b.Shape == BoxShape.Rectangle));
        foreach (var label in isolated)
        {
            var box = FindBox(layout.Nodes, label);
            var inside = box.X >= left && box.Y >= top && box.X + box.Width <= right && box.Y + box.Height <= bottom;
            Assert.False(inside, $"{label} was tucked into a connected drawing without free space");
        }

        AssertDefinitionBoxesDoNotOverlap(layout.Nodes);
    }

    /// <summary>
    ///     The isolated definitions of a nested package folder are tucked into that folder's own free
    ///     space: each stays inside the nested folder's box, none overlaps another box.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_UnconnectedDefinitions_InNestedFolder_AreTuckedInsideThatFolder()
    {
        // Act
        var layout = await LayOutUnscopedAsync($"package Outer {{ package Inner {{ {WideTreeWithIsolatedDefinitions} }} part def Sibling; }}");

        // Assert: the nested folder holds all nine isolated definitions inside its own box
        var inner = FindBox(layout.Nodes, "Inner", BoxShape.Folder);
        foreach (var label in IsolatedLabels)
        {
            var box = FindBox(layout.Nodes, label);
            Assert.True(box.X >= inner.X && box.Y >= inner.Y && box.X + box.Width <= inner.X + inner.Width && box.Y + box.Height <= inner.Y + inner.Height, $"{label} is outside the Inner folder");
            Assert.False(AnyConnectorCrosses(layout, box), $"a connector crosses {label}");
        }

        AssertDefinitionBoxesDoNotOverlap(layout.Nodes);
        Assert.Single(CollectBoxes(layout.Nodes), b => b.Label == "Sibling");
    }

    /// <summary>
    ///     A folder whose definitions have no relationship at all (the shape of a package of independent
    ///     elements) keeps the engine's packing: every box is drawn once and nothing overlaps.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_AllDefinitionsUnconnected_KeepsEngineLayout()
    {
        // Act
        var layout = await LayOutUnscopedAsync("package P { part def A; part def B; part def C; part def D; part def E; part def F; }");

        // Assert
        Assert.Equal(6, CollectBoxes(layout.Nodes).Count(b => b.Shape == BoxShape.Rectangle));
        AssertDefinitionBoxesDoNotOverlap(layout.Nodes);
        var folder = FindBox(layout.Nodes, "P", BoxShape.Folder);
        Assert.All(CollectBoxes(layout.Nodes).Where(b => b.Shape == BoxShape.Rectangle), b => Assert.True(b.X >= folder.X && b.X + b.Width <= folder.X + folder.Width));
    }

    /// <summary>
    ///     A definition that carries a comment is joined to its note by a line, so it is part of the
    ///     connected drawing and is not tucked as an isolated definition: its box and note stay
    ///     connected by a dashed line.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_DefinitionWithCommentNote_IsNotDeferred()
    {
        // Act
        var layout = await LayOutUnscopedAsync(WideTreeWithIsolatedDefinitions + " part def Noted { comment /* a remark */ }");

        // Assert: the noted definition and its note are joined by a dashed line
        var noted = FindBox(layout.Nodes, "Noted");
        var note = Assert.Single(CollectBoxes(layout.Nodes), b => b.Shape == BoxShape.Note);
        Assert.Contains(CollectLines(layout.Nodes), l => l.LineStyle == LineStyle.Dashed && RunsBetween(l, noted, note));
        AssertDefinitionBoxesDoNotOverlap(layout.Nodes);
    }

    /// <summary>
    ///     Standing regression for diagram-quality B4/B5 on the quadcopter drone gallery model: the leaf
    ///     blocks cover at least 20 % of the canvas, and the largest empty square (obstacles are the
    ///     boxes, the folder outline and the connectors, each grown by 12 px) is at most three times the
    ///     average leaf block height.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_DroneGalleryModel_MeetsBlockPlacementCriteria()
    {
        // Arrange
        var layout = await LayOutGalleryViewAsync("01-drone-general.sysml", "QuadcopterDrone::DroneGeneralView", SysmlThemes.Plain);
        if (layout is null)
        {
            return;
        }

        // Act
        var leaves = CollectBoxes(layout.Nodes).Where(b => b.Shape is BoxShape.Rectangle or BoxShape.RoundedRectangle).ToList();
        var share = leaves.Sum(b => b.Width * b.Height) / (layout.Width * layout.Height);
        var averageHeight = leaves.Average(b => b.Height);
        var largestSquare = LargestEmptySquare(layout, 12.0);

        // Assert
        Assert.True(share >= 0.20, $"the leaf blocks cover {share:P1} of the canvas, below 20 %");
        Assert.True(largestSquare <= 3.0 * averageHeight, $"the largest empty square is {largestSquare:F0} px, above {3.0 * averageHeight:F0} px");
    }

    /// <summary>
    ///     Measures, in logical pixels on a 4 px grid, the side of the largest empty square of a layout,
    ///     where the obstacles are every box (a folder only by its outline) and every connector segment,
    ///     grown by <paramref name="clearance"/> on every side.
    /// </summary>
    private static double LargestEmptySquare(LayoutTree layout, double clearance)
    {
        const double step = 4.0;
        var columns = (int)(layout.Width / step);
        var rows = (int)(layout.Height / step);
        var blocked = new bool[columns * rows];
        void Block(double left, double top, double right, double bottom)
        {
            var x0 = Math.Max(0, (int)Math.Floor((left - clearance) / step));
            var x1 = Math.Min(columns - 1, (int)Math.Floor((right + clearance) / step));
            var y0 = Math.Max(0, (int)Math.Floor((top - clearance) / step));
            var y1 = Math.Min(rows - 1, (int)Math.Floor((bottom + clearance) / step));
            for (var y = y0; y <= y1; y++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    blocked[(y * columns) + x] = true;
                }
            }
        }

        foreach (var box in CollectBoxes(layout.Nodes))
        {
            if (box.Shape == BoxShape.Folder)
            {
                Block(box.X, box.Y, box.X + box.Width, box.Y);
                Block(box.X, box.Y + box.Height, box.X + box.Width, box.Y + box.Height);
                Block(box.X, box.Y, box.X, box.Y + box.Height);
                Block(box.X + box.Width, box.Y, box.X + box.Width, box.Y + box.Height);
            }
            else
            {
                Block(box.X, box.Y, box.X + box.Width, box.Y + box.Height);
            }
        }

        foreach (var waypoints in CollectLines(layout.Nodes).Select(l => l.Waypoints))
        {
            for (var i = 1; i < waypoints.Count; i++)
            {
                Block(Math.Min(waypoints[i - 1].X, waypoints[i].X), Math.Min(waypoints[i - 1].Y, waypoints[i].Y), Math.Max(waypoints[i - 1].X, waypoints[i].X), Math.Max(waypoints[i - 1].Y, waypoints[i].Y));
            }
        }

        var previous = new int[columns + 1];
        var current = new int[columns + 1];
        var largest = 0;
        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                current[x + 1] = blocked[(y * columns) + x] ? 0 : 1 + Math.Min(Math.Min(previous[x], previous[x + 1]), current[x]);
                largest = Math.Max(largest, current[x + 1]);
            }

            (previous, current) = (current, previous);
        }

        return largest * step;
    }

    /// <summary>
    ///     Moving the first lane of an owner face away from the box moves every lane of that face by the
    ///     same distance, so the lanes of a seven-usage fan-out stay distinct, keep their order and are at
    ///     least one lane pitch (8 px) apart, with no two collinear segments overlapping.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_FanOutLanes_KeepDistinctOrderedLanes()
    {
        // Act
        var layout = await LayOutUnscopedAsync("""
            part def Frame; part def Battery; part def FlightController; part def Motor;
            part def Propeller; part def ImuSensor; part def GpsSensor;
            part def Drone {
                part airframe : Frame; part battery : Battery; part controller : FlightController;
                part motors : Motor[4]; part propellers : Propeller[4]; part imu : ImuSensor; part gps : GpsSensor;
            }
            """);

        // Assert: lanes whose horizontal extents overlap are at least 8 px apart, the nearest lane clears the decoration
        var drone = FindBox(layout.Nodes, "Drone");
        var diamondLines = CollectLines(layout.Nodes).Where(l => l.TargetEnd == EndMarkerStyle.FilledDiamond && l.Waypoints.Count >= 4).ToList();
        Assert.True(diamondLines.Count >= 4, "the fan-out has no lanes");
        Assert.All(diamondLines, l => Assert.True(l.Waypoints[^2].Y > drone.Y + drone.Height));
        for (var i = 0; i < diamondLines.Count; i++)
        {
            for (var j = i + 1; j < diamondLines.Count; j++)
            {
                var (a, b) = (diamondLines[i].Waypoints, diamondLines[j].Waypoints);
                var overlap = Math.Min(Math.Max(a[^2].X, a[^3].X), Math.Max(b[^2].X, b[^3].X)) - Math.Max(Math.Min(a[^2].X, a[^3].X), Math.Min(b[^2].X, b[^3].X));
                if (overlap > 0.0)
                {
                    Assert.True(Math.Abs(a[^2].Y - b[^2].Y) >= 8.0, $"lanes at {a[^2].Y:F1} and {b[^2].Y:F1} overlap and are closer than 8 px");
                }
            }
        }

        var nearest = diamondLines.Min(l => l.Waypoints[^2].Y);
        Assert.True(nearest - (drone.Y + drone.Height) >= NotationMetrics.RenderedAlongLineLength(EndMarkerStyle.FilledDiamond, Themes.Light));
        Assert.Equal(0, CountCollinearOverlaps(diamondLines));
    }

    /// <summary>Counts pairs of horizontal segments of different lines that lie on the same y and overlap in x.</summary>
    private static int CountCollinearOverlaps(IReadOnlyList<LayoutLine> lines)
    {
        var count = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            for (var j = i + 1; j < lines.Count; j++)
            {
                for (var a = 0; a + 1 < lines[i].Waypoints.Count; a++)
                {
                    for (var b = 0; b + 1 < lines[j].Waypoints.Count; b++)
                    {
                        var (a1, a2) = (lines[i].Waypoints[a], lines[i].Waypoints[a + 1]);
                        var (b1, b2) = (lines[j].Waypoints[b], lines[j].Waypoints[b + 1]);
                        var sameY = Math.Abs(a1.Y - a2.Y) < 0.01 && Math.Abs(b1.Y - b2.Y) < 0.01 && Math.Abs(a1.Y - b1.Y) < 0.01;
                        var overlap = Math.Min(Math.Max(a1.X, a2.X), Math.Max(b1.X, b2.X)) - Math.Max(Math.Min(a1.X, a2.X), Math.Min(b1.X, b2.X));
                        if (sameY && overlap > 0.01)
                        {
                            count++;
                        }
                    }
                }
            }
        }

        return count;
    }

    /// <summary>
    ///     Real-corpus guard (diagram-quality C10): every General View of the gallery models that ends a
    ///     connector in an end decoration over a lane gives it a clean straight approach, with the theme
    ///     the tool renders with (plain) and, for the models whose lanes have room, the light theme. The
    ///     light-theme drone view is not asserted: its decorations are longer, and moving the lanes of the
    ///     <c>Battery</c> face would leave a note link and a dependency lane almost collinear, so the
    ///     post-pass keeps the engine's routing there by design.
    /// </summary>
    /// <param name="modelFile">The gallery model file name.</param>
    /// <param name="viewQualifiedName">The qualified name of the General View to lay out.</param>
    /// <param name="plainTheme">True for the tool's plain theme, false for the light theme.</param>
    [Theory]
    [InlineData("01-drone-general.sysml", "QuadcopterDrone::DroneGeneralView", true)]
    [InlineData("01-drone-general.sysml", "QuadcopterDrone::BatterySubsystemView", true)]
    [InlineData("11-nested-packages-general.sysml", "Vehicle::VehiclePackagesGeneralView", true)]
    [InlineData("14-usages-typing-general.sysml", "DriveSystem::DriveUnitGeneralView", true)]
    [InlineData("14-usages-typing-general.sysml", "DriveSystem::DriveUnitGeneralView", false)]
    [InlineData("15-doc-comment-general.sysml", "ChargingStation::ChargerGeneralView", true)]
    [InlineData("15-doc-comment-general.sysml", "ChargingStation::ChargerGeneralView", false)]
    [InlineData("16-nested-packages-typing-general.sysml", "TypedVehicle::VehicleTypingGeneralView", true)]
    public async Task GeneralViewLayoutStrategy_BuildLayout_GalleryGeneralViews_EndDecorationsHaveCleanApproach(
        string modelFile, string viewQualifiedName, bool plainTheme)
    {
        // Arrange
        var theme = plainTheme ? SysmlThemes.Plain : Themes.Light;
        var layout = await LayOutGalleryViewAsync(modelFile, viewQualifiedName, theme);
        if (layout is null)
        {
            return;
        }

        // Assert
        AssertEndDecorationsHaveCleanApproach(layout, theme);
    }

    /// <summary>
    ///     Loads a gallery model by file name and lays out one of its views with the general view
    ///     strategy; returns null when the gallery is not available (for example in a packaged test run).
    /// </summary>
    private static async Task<LayoutTree?> LayOutGalleryViewAsync(string modelFile, string viewQualifiedName, Theme theme)
    {
        var galleryModelsRoot = FindGalleryModelsRoot();
        var modelPath = galleryModelsRoot is null ? null : Path.Combine(galleryModelsRoot, modelFile);
        if (modelPath is null || !File.Exists(modelPath))
        {
            return null;
        }

        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([modelPath], stdlibTable);
        Assert.NotNull(result.Workspace);
        var viewNode = Assert.IsType<SysmlViewNode>(result.Workspace.Declarations[viewQualifiedName]);
        return new GeneralViewLayoutStrategy().BuildLayout(
            new ViewContext("v", result.Workspace, viewNode), new RenderOptions(theme));
    }

    /// <summary>
    ///     A user package named like a standard-library package (<c>Parts</c>) is drawn with its
    ///     members: the stdlib filter identifies the standard library by origin, not by name, so
    ///     the user's <c>Parts</c> folder and <c>Wheel</c> definition render, and no member of the
    ///     real standard-library <c>Parts</c> package (such as <c>Part</c>) appears.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_UserPackageNamedLikeStdlibPackage_RendersUserMembersOnly()
    {
        // Act
        var layout = await LayOutUnscopedAsync("package Parts { part def Wheel; }");

        // Assert: exactly the user's folder and definition are drawn
        var boxes = CollectBoxes(layout.Nodes);
        var folder = FindBox(layout.Nodes, "Parts", BoxShape.Folder);
        Assert.Contains(FindBox(layout.Nodes, "Wheel"), CollectBoxes(folder.Children));
        Assert.Equal(["Parts", "Wheel"], boxes.Select(b => b.Label).Order(StringComparer.Ordinal));
    }

    /// <summary>
    ///     User definitions in a package whose name collides with a standard-library root package
    ///     are drawn alongside definitions of an ordinary package, and a stdlib-only name is still
    ///     excluded: the real <c>Parts::Part</c> definition never appears.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_UserPackageNamedLikeStdlibPackage_StdlibMembersStayExcluded()
    {
        // Act
        var layout = await LayOutUnscopedAsync("package Parts { part def Wheel; } package Mine { part def Hub; }");

        // Assert
        var labels = CollectBoxes(layout.Nodes).Select(b => b.Label).ToList();
        Assert.Contains("Wheel", labels);
        Assert.Contains("Hub", labels);
        Assert.DoesNotContain("Part", labels);
    }

    /// <summary>
    ///     Every occurrence-kind usage drawn as a node of its own keeps its typing: the box carries the
    ///     <c>name : Type</c> label and a feature-typing edge (hollow triangle with two dots) runs from
    ///     the usage to its definition. Requirement, concern, use case, analysis, verification, case
    ///     and calc usages previously lost the typing.
    /// </summary>
    /// <param name="usageKeyword">The usage keyword as written in the source.</param>
    /// <param name="definitionKeyword">The matching definition keyword as written in the source.</param>
    [Theory]
    [InlineData("part", "part def")]
    [InlineData("item", "item def")]
    [InlineData("occurrence", "occurrence def")]
    [InlineData("action", "action def")]
    [InlineData("state", "state def")]
    [InlineData("requirement", "requirement def")]
    [InlineData("concern", "concern def")]
    [InlineData("use case", "use case def")]
    [InlineData("analysis", "analysis def")]
    [InlineData("verification", "verification def")]
    [InlineData("case", "case def")]
    [InlineData("calc", "calc def")]
    public async Task GeneralViewLayoutStrategy_BuildLayout_OccurrenceKindUsage_ShowsTypingLabelAndEdge(
        string usageKeyword,
        string definitionKeyword)
    {
        // Act: a definition and a usage of the same kind, typed by it
        var layout = await LayOutUnscopedAsync(
            $"package P {{ {definitionKeyword} Def; {usageKeyword} u : Def; }}",
            policy: NodePolicy.AllUsages);

        // Assert: the usage box reads "u : Def" and a typing edge joins it to the definition
        var usageBox = FindBox(layout.Nodes, "u");
        Assert.Equal("u : Def", usageBox.Label);
        var typingEdge = Assert.Single(CollectLines(layout.Nodes), l => l.TargetEnd == EndMarkerStyle.HollowTriangleTwoDots);
        Assert.True(RunsBetween(typingEdge, usageBox, FindBox(layout.Nodes, "Def")));
    }

    /// <summary>
    ///     With the Pilot policy a nested requirement usage (<c>requirement r : R;</c>) is a node of its
    ///     own labeled <c>r : R</c> with a feature-typing edge to <c>R</c> (issue #113).
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_NestedRequirementUsage_ShowsTypingLabelAndEdge()
    {
        // Act
        var layout = await LayOutUnscopedAsync("""
            package P {
                requirement def R;
                part def Holder { requirement r : R; }
            }
            """);

        // Assert
        var usageBox = FindBox(layout.Nodes, "r");
        Assert.Equal("r : R", usageBox.Label);
        var typingEdge = Assert.Single(CollectLines(layout.Nodes), l => l.TargetEnd == EndMarkerStyle.HollowTriangleTwoDots);
        Assert.True(RunsBetween(typingEdge, usageBox, FindBox(layout.Nodes, "R")));
    }

    /// <summary>
    ///     A typed constraint usage declared directly in a package (<c>constraint k : K;</c>) is drawn
    ///     as a node labeled <c>k : K</c> with a feature-typing edge to the constraint definition
    ///     (issue #113). The typing comes from the constraint-usage parser path.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_PackageConstraintUsage_ShowsTypingLabelAndEdge()
    {
        // Act
        var layout = await LayOutUnscopedAsync("package P { constraint def K; constraint k : K; }");

        // Assert
        var usageBox = FindBox(layout.Nodes, "k");
        Assert.Equal("k : K", usageBox.Label);
        var typingEdge = Assert.Single(CollectLines(layout.Nodes), l => l.TargetEnd == EndMarkerStyle.HollowTriangleTwoDots);
        Assert.True(RunsBetween(typingEdge, usageBox, FindBox(layout.Nodes, "K")));
    }

    /// <summary>
    ///     Under the Pilot node policy a constraint usage nested in a definition is never a node of its
    ///     own: no box is drawn for it, and so no typing edge.
    /// </summary>
    [Fact]
    public async Task GeneralViewLayoutStrategy_BuildLayout_NestedConstraintUsage_IsNotANode()
    {
        // Act
        var layout = await LayOutUnscopedAsync("package P { constraint def K; part def Holder { constraint k : K; } }");

        // Assert
        Assert.DoesNotContain(CollectBoxes(layout.Nodes), b => b.Label?.StartsWith("k", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(CollectLines(layout.Nodes), l => l.TargetEnd == EndMarkerStyle.HollowTriangleTwoDots);
    }
}

