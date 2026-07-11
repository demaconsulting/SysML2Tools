// <copyright file="BrowserAndGridViewLayoutStrategyTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.SysML2Tools.Layout.Internal;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;

namespace DemaConsulting.SysML2Tools.Tests.Layout;

/// <summary>
///     Tests for <see cref="BrowserViewLayoutStrategy"/> and <see cref="GridViewLayoutStrategy"/>.
/// </summary>
public sealed class BrowserAndGridViewLayoutStrategyTests
{
    /// <summary>
    ///     The browser view indents nested elements more than their parents.
    /// </summary>
    [Fact]
    public void BrowserView_BuildLayout_NestedElements_AreIndentedByDepth()
    {
        // Arrange: a package containing a nested package and a def
        var strategy = new BrowserViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["Cat"] = new SysmlPackageNode { Name = "Cat", QualifiedName = "Cat" },
                ["Cat::Comp"] = new SysmlPackageNode { Name = "Comp", QualifiedName = "Cat::Comp" },
                ["Cat::Comp::Engine"] = new SysmlDefinitionNode { Name = "Engine", QualifiedName = "Cat::Comp::Engine", DefinitionKeyword = "part def" }
            }
        };
        var context = new ViewContext("CatBrowserView", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: the deeply nested Engine box has a larger X than the root package box
        var boxes = layout.Nodes.OfType<LayoutBox>().ToList();
        var root = boxes.First(b => b.Label!.Contains("Cat"));
        var engine = boxes.First(b => b.Label!.Contains("Engine"));
        Assert.True(engine.X > root.X, "Nested element should be indented more than its ancestor.");
    }

    /// <summary>
    ///     The grid view produces a relationship matrix with a header row and a mark where a row
    ///     definition specializes a column definition.
    /// </summary>
    [Fact]
    public void GridView_BuildLayout_Specialization_ProducesMarkedMatrix()
    {
        // Arrange: Car specializes Vehicle
        var strategy = new GridViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Vehicle"] = new SysmlDefinitionNode { Name = "Vehicle", QualifiedName = "P::Vehicle", DefinitionKeyword = "part def" },
                ["P::Car"] = new SysmlDefinitionNode { Name = "Car", QualifiedName = "P::Car", DefinitionKeyword = "part def", SupertypeNames = ["Vehicle"] }
            }
        };
        var context = new ViewContext("SpecMatrixView", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: a grid with a header row exists and contains exactly one specialization mark
        var grid = Assert.Single(layout.Nodes.OfType<LayoutGrid>());
        Assert.True(grid.Rows[0].IsHeader);
        var markCount = grid.Rows.SelectMany(r => r.Cells).Count(c => c.Text == "X");
        Assert.Equal(1, markCount);
    }

    /// <summary>Both strategies return a minimal canvas for an empty workspace.</summary>
    [Fact]
    public void BrowserAndGrid_BuildLayout_EmptyWorkspace_ReturnMinimalCanvas()
    {
        var workspace = new SysmlWorkspace();
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        Assert.Empty(new BrowserViewLayoutStrategy().BuildLayout(context, options).Nodes);
        Assert.Empty(new GridViewLayoutStrategy().BuildLayout(context, options).Nodes);
    }

    /// <summary>
    ///     Builds the fixed three-definition workspace shared by the expose-scoping tests:
    ///     <c>Root::A</c> (an expose target), <c>Root::A::Child</c> (inside <c>A</c>'s containment
    ///     subtree), and <c>Root::B</c> (an unrelated sibling definition, outside <c>A</c>'s
    ///     subtree).
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
    ///     Builds a workspace modeling the usage-vs-definition containment gap: a
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
    ///     A Grid View with a resolved <c>Expose</c> edge to <c>Root::A</c> scopes the matrix to
    ///     <c>Root::A</c> plus its containment subtree (<c>Root::A::Child</c>), excluding the
    ///     unrelated sibling <c>Root::B</c> — producing fewer rows/columns than rendering the full
    ///     workspace.
    /// </summary>
    [Fact]
    public void GridView_BuildLayout_ExposedName_UnionsAdditionalSubtree()
    {
        var strategy = new GridViewLayoutStrategy();
        var workspace = BuildScopingWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            ExposeMembers = [new ExposeMember("A", null)],
            ResolvedEdges = [new SysmlEdge("Root::V", "Root::A", SysmlEdgeKind.Expose)]
        };
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var scoped = strategy.BuildLayout(context, options);
        var full = strategy.BuildLayout(new ViewContext("full", workspace), options);

        var scopedGrid = Assert.Single(scoped.Nodes.OfType<LayoutGrid>());
        var fullGrid = Assert.Single(full.Nodes.OfType<LayoutGrid>());
        var scopedLabels = scopedGrid.Rows[0].Cells.Select(c => c.Text).ToList();
        Assert.Contains("A", scopedLabels);
        Assert.Contains("Child", scopedLabels);
        Assert.DoesNotContain("B", scopedLabels);
        Assert.True(scopedGrid.Rows.Count < fullGrid.Rows.Count);
    }

    /// <summary>
    ///     With no <c>expose</c> statement (null <c>ViewNode</c>), the Grid View renders every
    ///     non-stdlib definition unchanged — the critical --auto/no-expose regression guard.
    /// </summary>
    [Fact]
    public void GridView_BuildLayout_NullViewNode_RendersFullWorkspaceUnchanged()
    {
        var strategy = new GridViewLayoutStrategy();
        var workspace = BuildScopingWorkspace();
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var grid = Assert.Single(layout.Nodes.OfType<LayoutGrid>());
        var labels = grid.Rows[0].Cells.Select(c => c.Text).ToList();
        Assert.Contains("A", labels);
        Assert.Contains("Child", labels);
        Assert.Contains("B", labels);
    }

    /// <summary>
    ///     A Grid View whose <c>Expose</c> edge resolves to a feature usage (not a definition)
    ///     still renders that usage's type's containment subtree, via the shared usage-to-type
    ///     fallback in <c>ExposeScopeResolver.ResolveExposedScope</c>.
    /// </summary>
    [Fact]
    public void GridView_BuildLayout_ExposedUsage_ResolvesThroughTypingToDefinitionSubtree()
    {
        var strategy = new GridViewLayoutStrategy();
        var workspace = BuildUsageTypingWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            ExposeMembers = [new ExposeMember("myVehicle", null)],
            ResolvedEdges = [new SysmlEdge("Root::V", "Root::myVehicle", SysmlEdgeKind.Expose)]
        };
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var grid = Assert.Single(layout.Nodes.OfType<LayoutGrid>());
        var labels = grid.Rows[0].Cells.Select(c => c.Text).ToList();
        Assert.Contains("Vehicle", labels);
        Assert.Contains("Engine", labels);
        Assert.DoesNotContain("Other", labels);
    }

    /// <summary>
    ///     A Grid View <c>expose</c> statement naming two separate definitions unions both their
    ///     containment subtrees.
    /// </summary>
    [Fact]
    public void GridView_BuildLayout_ExposeMultipleTargets_UnionsBothSubtrees()
    {
        var strategy = new GridViewLayoutStrategy();
        var workspace = BuildScopingWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            ExposeMembers = [new ExposeMember("A", null), new ExposeMember("B", null)],
            ResolvedEdges =
            [
                new SysmlEdge("Root::V", "Root::A", SysmlEdgeKind.Expose),
                new SysmlEdge("Root::V", "Root::B", SysmlEdgeKind.Expose)
            ]
        };
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var grid = Assert.Single(layout.Nodes.OfType<LayoutGrid>());
        var labels = grid.Rows[0].Cells.Select(c => c.Text).ToList();
        Assert.Contains("A", labels);
        Assert.Contains("Child", labels);
        Assert.Contains("B", labels);
    }

    /// <summary>
    ///     A Grid View <c>expose</c> statement naming only the specific side of a specialization
    ///     relationship (<c>Root::A::Sub</c>, which specializes <c>Root::A</c>) still keeps both
    ///     sides visible in the matrix: the general side (<c>A</c>) is not in <c>Sub</c>'s
    ///     containment subtree, but the two participate in the same specialization relationship, so
    ///     both remain as header rows/columns and the <c>Sub</c>-&gt;<c>A</c> mark is present, while
    ///     the unrelated <c>Root::B</c> is excluded.
    /// </summary>
    [Fact]
    public void GridView_BuildLayout_ExposeOneSideOfSpecialization_KeepsBothRowAndColumn()
    {
        var strategy = new GridViewLayoutStrategy();
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["Root::A"] = new SysmlDefinitionNode { Name = "A", QualifiedName = "Root::A", DefinitionKeyword = "part def" },
                ["Root::A::Sub"] = new SysmlDefinitionNode { Name = "Sub", QualifiedName = "Root::A::Sub", DefinitionKeyword = "part def", SupertypeNames = ["A"] },
                ["Root::B"] = new SysmlDefinitionNode { Name = "B", QualifiedName = "Root::B", DefinitionKeyword = "part def" }
            }
        };
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            ExposeMembers = [new ExposeMember("Sub", null)],
            ResolvedEdges = [new SysmlEdge("Root::V", "Root::A::Sub", SysmlEdgeKind.Expose)]
        };
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var grid = Assert.Single(layout.Nodes.OfType<LayoutGrid>());
        var labels = grid.Rows[0].Cells.Select(c => c.Text).ToList();
        Assert.Contains("A", labels);
        Assert.Contains("Sub", labels);
        Assert.DoesNotContain("B", labels);

        var subRow = grid.Rows.Single(r => !r.IsHeader && r.Cells[0].Text == "Sub");
        var aColumnIndex = grid.Rows[0].Cells.Select((c, i) => (c, i)).Single(x => x.c.Text == "A").i;
        Assert.Equal("X", subRow.Cells[aColumnIndex].Text);
    }

    /// <summary>
    ///     A Browser View with a resolved <c>Expose</c> edge to <c>Root::A</c> scopes the tree to
    ///     <c>Root::A</c>'s containment subtree, excluding the unrelated sibling <c>Root::B</c> —
    ///     <c>Root::A</c> itself is promoted to a forest root since its own parent (<c>Root</c>) has
    ///     no declaration in the workspace and is thus not part of the filtered <c>names</c> list.
    /// </summary>
    [Fact]
    public void BrowserView_BuildLayout_ExposedName_UnionsAdditionalSubtree()
    {
        var strategy = new BrowserViewLayoutStrategy();
        var workspace = BuildScopingWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            ExposeMembers = [new ExposeMember("A", null)],
            ResolvedEdges = [new SysmlEdge("Root::V", "Root::A", SysmlEdgeKind.Expose)]
        };
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var scoped = strategy.BuildLayout(context, options);
        var full = strategy.BuildLayout(new ViewContext("full", workspace), options);

        var scopedLabels = scoped.Nodes.OfType<LayoutBox>().Select(b => b.Label).ToList();
        var fullLabels = full.Nodes.OfType<LayoutBox>().Select(b => b.Label).ToList();
        Assert.Contains(scopedLabels, l => l!.Contains("A") && !l.Contains("Child"));
        Assert.Contains(scopedLabels, l => l!.Contains("Child"));
        Assert.DoesNotContain(scopedLabels, l => l!.Contains("B") && !l.Contains("A") && !l.Contains("Child"));
        Assert.True(scopedLabels.Count < fullLabels.Count,
            $"expected scoped ({scopedLabels.Count}) < full ({fullLabels.Count})");
    }

    /// <summary>
    ///     With no <c>expose</c> statement (null <c>ViewNode</c>), the Browser View renders the
    ///     full membership forest unchanged — the critical --auto/no-expose regression guard.
    /// </summary>
    [Fact]
    public void BrowserView_BuildLayout_NullViewNode_RendersFullWorkspaceUnchanged()
    {
        var strategy = new BrowserViewLayoutStrategy();
        var workspace = BuildScopingWorkspace();
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var labels = layout.Nodes.OfType<LayoutBox>().Select(b => b.Label).ToList();
        Assert.Contains(labels, l => l!.Contains("A") && !l.Contains("Child"));
        Assert.Contains(labels, l => l!.Contains("Child"));
        Assert.Contains(labels, l => l!.Contains("B") && !l.Contains("A") && !l.Contains("Child"));
    }

    /// <summary>
    ///     A Browser View whose <c>Expose</c> edge resolves to a feature usage (not a definition)
    ///     still renders that usage's type's containment subtree, via the shared usage-to-type
    ///     fallback in <c>ExposeScopeResolver.ResolveExposedScope</c>.
    /// </summary>
    [Fact]
    public void BrowserView_BuildLayout_ExposedUsage_ResolvesThroughTypingToDefinitionSubtree()
    {
        var strategy = new BrowserViewLayoutStrategy();
        var workspace = BuildUsageTypingWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            ExposeMembers = [new ExposeMember("myVehicle", null)],
            ResolvedEdges = [new SysmlEdge("Root::V", "Root::myVehicle", SysmlEdgeKind.Expose)]
        };
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var labels = layout.Nodes.OfType<LayoutBox>().Select(b => b.Label).ToList();
        Assert.Contains(labels, l => l!.Contains("Vehicle") && !l.Contains("Engine"));
        Assert.Contains(labels, l => l!.Contains("Engine"));
        Assert.DoesNotContain(labels, l => l!.Contains("Other"));
    }

    /// <summary>
    ///     A Browser View <c>expose</c> statement naming two separate definitions unions both their
    ///     containment subtrees, each becoming (or remaining under) a forest root.
    /// </summary>
    [Fact]
    public void BrowserView_BuildLayout_ExposeMultipleTargets_UnionsBothSubtrees()
    {
        var strategy = new BrowserViewLayoutStrategy();
        var workspace = BuildScopingWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            ExposeMembers = [new ExposeMember("A", null), new ExposeMember("B", null)],
            ResolvedEdges =
            [
                new SysmlEdge("Root::V", "Root::A", SysmlEdgeKind.Expose),
                new SysmlEdge("Root::V", "Root::B", SysmlEdgeKind.Expose)
            ]
        };
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var labels = layout.Nodes.OfType<LayoutBox>().Select(b => b.Label).ToList();
        Assert.Contains(labels, l => l!.Contains("A") && !l.Contains("Child"));
        Assert.Contains(labels, l => l!.Contains("Child"));
        Assert.Contains(labels, l => l!.Contains("B") && !l.Contains("A") && !l.Contains("Child"));
    }
}
