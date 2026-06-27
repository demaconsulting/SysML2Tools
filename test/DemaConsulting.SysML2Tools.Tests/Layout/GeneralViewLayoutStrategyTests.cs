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
}
