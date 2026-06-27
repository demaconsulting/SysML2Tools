// <copyright file="BrowserAndGridViewLayoutStrategyTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout;
using DemaConsulting.SysML2Tools.Layout.Internal;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Internal;

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
}
