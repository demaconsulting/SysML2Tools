// <copyright file="InterconnectionViewLayoutStrategyTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout;
using DemaConsulting.SysML2Tools.Layout.Internal;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Internal;

namespace DemaConsulting.SysML2Tools.Tests.Layout;

/// <summary>
///     Tests for <see cref="InterconnectionViewLayoutStrategy"/>.
/// </summary>
public sealed class InterconnectionViewLayoutStrategyTests
{
    /// <summary>
    ///     A part definition with nested parts and connections renders as a container box with one
    ///     rounded part box per nested part, port nodes, and one connector line per connection.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_PartsAndConnections_ProducesBoxesPortsAndLines()
    {
        // Arrange: a PowerSystem part def with two parts and one connection between them
        var strategy = new InterconnectionViewLayoutStrategy();
        var powerSystem = new SysmlDefinitionNode
        {
            Name = "PowerSystem",
            QualifiedName = "M::PowerSystem",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "engine", QualifiedName = "M::PowerSystem::engine", FeatureKeyword = "part", FeatureTyping = "Engine" },
                new SysmlFeatureNode { Name = "transmission", QualifiedName = "M::PowerSystem::transmission", FeatureKeyword = "part", FeatureTyping = "Transmission" },
                new SysmlConnectionNode { Name = "c1", QualifiedName = "M::PowerSystem::c1", ConnectionKeyword = "connection", EndpointA = "engine", EndpointB = "transmission" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["M::PowerSystem"] = powerSystem }
        };
        var context = new ViewContext("PowerSystemInterconnectionView", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: one container box, two part boxes, two ports (one per endpoint), one connector line
        var boxes = layout.Nodes.OfType<LayoutBox>().ToList();
        Assert.Contains(boxes, b => b.Keyword == "part def" && b.Label == "PowerSystem");
        Assert.Equal(2, boxes.Count(b => b.Shape == BoxShape.RoundedRectangle));
        Assert.Equal(2, layout.Nodes.OfType<LayoutPort>().Count());
        Assert.Single(layout.Nodes.OfType<LayoutLine>());
    }

    /// <summary>
    ///     Two connection usages between the SAME two parts (both <c>EndpointA=a, EndpointB=b</c>) form
    ///     an identical directed pair. The interconnection engine de-duplicates the pair so its routed
    ///     connector waypoints are not 1:1 with the connections; the strategy must resolve each
    ///     connection by its endpoints (not by input position) and lay out without throwing, emitting a
    ///     connector polyline for each of the two connections.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_TwoConnectionsSamePair_ProducesTwoConnectorsWithoutException()
    {
        // Arrange: a Board part def with parts a, b and two connections both between a and b.
        var strategy = new InterconnectionViewLayoutStrategy();
        var board = new SysmlDefinitionNode
        {
            Name = "Board",
            QualifiedName = "M::Board",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "a", QualifiedName = "M::Board::a", FeatureKeyword = "part", FeatureTyping = "A" },
                new SysmlFeatureNode { Name = "b", QualifiedName = "M::Board::b", FeatureKeyword = "part", FeatureTyping = "B" },
                new SysmlConnectionNode { Name = "power", QualifiedName = "M::Board::power", ConnectionKeyword = "connection", EndpointA = "a", EndpointB = "b" },
                new SysmlConnectionNode { Name = "signal", QualifiedName = "M::Board::signal", ConnectionKeyword = "connection", EndpointA = "a", EndpointB = "b" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["M::Board"] = board }
        };
        var context = new ViewContext("BoardInterconnectionView", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act: laying out must not throw even though the two connections share one routed polyline.
        var layout = strategy.BuildLayout(context, options);

        // Assert: two connector polylines (one per connection), each with at least two waypoints.
        var lines = layout.Nodes.OfType<LayoutLine>().ToList();
        Assert.Equal(2, lines.Count);
        Assert.All(lines, l => Assert.True(l.Waypoints.Count >= 2));

        // Assert: two part boxes and one port pair per connection (four ports total).
        Assert.Equal(2, layout.Nodes.OfType<LayoutBox>().Count(b => b.Shape == BoxShape.RoundedRectangle));
        Assert.Equal(4, layout.Nodes.OfType<LayoutPort>().Count());
    }

    /// <summary>
    ///     The two part boxes produced for connected parts do not overlap.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_PartBoxes_DoNotOverlap()
    {
        // Arrange: three parts in a chain
        var strategy = new InterconnectionViewLayoutStrategy();
        var root = new SysmlDefinitionNode
        {
            Name = "Sys",
            QualifiedName = "M::Sys",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "a", QualifiedName = "M::Sys::a", FeatureKeyword = "part", FeatureTyping = "A" },
                new SysmlFeatureNode { Name = "b", QualifiedName = "M::Sys::b", FeatureKeyword = "part", FeatureTyping = "B" },
                new SysmlFeatureNode { Name = "c", QualifiedName = "M::Sys::c", FeatureKeyword = "part", FeatureTyping = "C" },
                new SysmlConnectionNode { ConnectionKeyword = "connection", EndpointA = "a", EndpointB = "b" },
                new SysmlConnectionNode { ConnectionKeyword = "connection", EndpointA = "b", EndpointB = "c" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["M::Sys"] = root }
        };
        var context = new ViewContext("Interconnection", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: no two rounded part boxes overlap
        var partBoxes = layout.Nodes.OfType<LayoutBox>().Where(b => b.Shape == BoxShape.RoundedRectangle).ToList();
        for (var i = 0; i < partBoxes.Count; i++)
        {
            for (var j = i + 1; j < partBoxes.Count; j++)
            {
                Assert.False(Overlaps(partBoxes[i], partBoxes[j]), $"Part boxes {i} and {j} overlap.");
            }
        }
    }

    /// <summary>An empty workspace yields a minimal canvas.</summary>
    [Fact]
    public void InterconnectionView_BuildLayout_EmptyWorkspace_ReturnsMinimalCanvas()
    {
        var strategy = new InterconnectionViewLayoutStrategy();
        var workspace = new SysmlWorkspace();
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        Assert.Empty(layout.Nodes);
    }

    /// <summary>
    ///     A part typed by a definition that has its own internal parts is rendered as a container
    ///     box whose nested children sit inside it, below its title area.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_NestedContainer_PlacesChildrenInsideContainerBox()
    {
        // Arrange: Computer { board : Motherboard{cpu, chipset, connect cpu to chipset}, psu, connect psu to board }
        var strategy = new InterconnectionViewLayoutStrategy();
        var workspace = BuildNestedWorkspace();
        var context = new ViewContext("ComputerInterconnectionView", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: the board box is a container with non-empty children, each fully inside its bounds.
        var boardBox = FindPartBox(layout, "board : Motherboard");
        Assert.NotEmpty(boardBox.Children);

        var titleArea = BoxMetrics.TitleAreaHeight(options.Theme, hasLabel: true, hasKeyword: true);
        foreach (var child in boardBox.Children.OfType<LayoutBox>())
        {
            Assert.True(child.X >= boardBox.X, "child left edge inside container");
            Assert.True(child.Y >= boardBox.Y + titleArea, "child below container title area");
            Assert.True(child.X + child.Width <= boardBox.X + boardBox.Width, "child right edge inside container");
            Assert.True(child.Y + child.Height <= boardBox.Y + boardBox.Height, "child bottom edge inside container");
        }
    }

    /// <summary>
    ///     A container box is sized to bound its nested children plus its title area and insets.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_ContainerSize_BoundsChildrenAndTitle()
    {
        // Arrange
        var strategy = new InterconnectionViewLayoutStrategy();
        var workspace = BuildNestedWorkspace();
        var context = new ViewContext("ComputerInterconnectionView", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: the container height clears the title area plus the children content extent.
        var boardBox = FindPartBox(layout, "board : Motherboard");
        var childBoxes = boardBox.Children.OfType<LayoutBox>().ToList();
        Assert.NotEmpty(childBoxes);

        var titleArea = BoxMetrics.TitleAreaHeight(options.Theme, hasLabel: true, hasKeyword: true);
        var childBottom = childBoxes.Max(c => c.Y + c.Height) - boardBox.Y;
        var childRight = childBoxes.Max(c => c.X + c.Width) - boardBox.X;

        Assert.True(boardBox.Height >= titleArea + (options.Theme.LabelPadding * 2.0), "container reserves title area and insets");
        Assert.True(boardBox.Height >= childBottom, "container height bounds children");
        Assert.True(boardBox.Width >= childRight, "container width bounds children");
    }

    /// <summary>
    ///     Nested children are emitted at absolute coordinates offset by the container origin, so the
    ///     renderer (which uses absolute coordinates) draws them in the right place.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_NestedChildren_RenderedAtAbsoluteCoordinates()
    {
        // Arrange
        var strategy = new InterconnectionViewLayoutStrategy();
        var workspace = BuildNestedWorkspace();
        var context = new ViewContext("ComputerInterconnectionView", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: each inner child's absolute position is at or beyond the container origin
        // (proving the translate pass ran rather than leaving children at local (0,0)).
        var boardBox = FindPartBox(layout, "board : Motherboard");
        var childBoxes = boardBox.Children.OfType<LayoutBox>().ToList();
        Assert.NotEmpty(childBoxes);
        Assert.All(childBoxes, c =>
        {
            Assert.True(c.X >= boardBox.X, "child translated to absolute X");
            Assert.True(c.Y >= boardBox.Y, "child translated to absolute Y");
        });

        // The cpu/chipset boxes carry depth 2 (root container 0, board 1, inner parts 2).
        Assert.Contains(childBoxes, c => c.Label == "cpu : Cpu" && c.Depth == 2);
        Assert.Contains(childBoxes, c => c.Label == "chipset : Chipset" && c.Depth == 2);
    }

    /// <summary>
    ///     A model without any nested internal structure produces only leaf part boxes with no
    ///     children, proving the recursion is a strict no-op without nesting.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_NoNesting_ProducesFlatLeafBoxes()
    {
        // Arrange: a flat model whose part types have no internal parts.
        var strategy = new InterconnectionViewLayoutStrategy();
        var root = new SysmlDefinitionNode
        {
            Name = "Sys",
            QualifiedName = "M::Sys",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "a", QualifiedName = "M::Sys::a", FeatureKeyword = "part", FeatureTyping = "A" },
                new SysmlFeatureNode { Name = "b", QualifiedName = "M::Sys::b", FeatureKeyword = "part", FeatureTyping = "B" },
                new SysmlConnectionNode { ConnectionKeyword = "connection", EndpointA = "a", EndpointB = "b" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["M::Sys"] = root,
                ["M::A"] = new SysmlDefinitionNode { Name = "A", QualifiedName = "M::A", DefinitionKeyword = "part def" },
                ["M::B"] = new SysmlDefinitionNode { Name = "B", QualifiedName = "M::B", DefinitionKeyword = "part def" }
            }
        };
        var context = new ViewContext("Interconnection", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: every rounded part box is a leaf (no children).
        var partBoxes = layout.Nodes.OfType<LayoutBox>().Where(b => b.Shape == BoxShape.RoundedRectangle).ToList();
        Assert.Equal(2, partBoxes.Count);
        Assert.All(partBoxes, b => Assert.Empty(b.Children));
    }

    /// <summary>
    ///     A part typed by a definition that (via its own part) refers back to itself does not
    ///     recurse infinitely; the cycle is broken and the part renders as a leaf box.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_SelfReferentialType_TreatedAsLeaf()
    {
        // Arrange: Node { child : Node, peer : Other, connect child to peer } — Node refers to itself.
        var strategy = new InterconnectionViewLayoutStrategy();
        var node = new SysmlDefinitionNode
        {
            Name = "Node",
            QualifiedName = "M::Node",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "child", QualifiedName = "M::Node::child", FeatureKeyword = "part", FeatureTyping = "Node" },
                new SysmlFeatureNode { Name = "peer", QualifiedName = "M::Node::peer", FeatureKeyword = "part", FeatureTyping = "Other" },
                new SysmlConnectionNode { ConnectionKeyword = "connection", EndpointA = "child", EndpointB = "peer" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["M::Node"] = node,
                ["M::Other"] = new SysmlDefinitionNode { Name = "Other", QualifiedName = "M::Other", DefinitionKeyword = "part def" }
            }
        };
        var context = new ViewContext("Interconnection", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act: must terminate. The self-referential child is treated as a leaf (cycle guard).
        var layout = strategy.BuildLayout(context, options);

        // Assert
        var childBox = FindPartBox(layout, "child : Node");
        Assert.Empty(childBox.Children);
    }

    /// <summary>
    ///     Builds a two-level workspace: Computer { board : Motherboard, psu } with a Motherboard
    ///     definition that has its own internal cpu/chipset parts and a connection.
    /// </summary>
    private static SysmlWorkspace BuildNestedWorkspace()
    {
        var motherboard = new SysmlDefinitionNode
        {
            Name = "Motherboard",
            QualifiedName = "M::Motherboard",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "cpu", QualifiedName = "M::Motherboard::cpu", FeatureKeyword = "part", FeatureTyping = "Cpu" },
                new SysmlFeatureNode { Name = "chipset", QualifiedName = "M::Motherboard::chipset", FeatureKeyword = "part", FeatureTyping = "Chipset" },
                new SysmlConnectionNode { ConnectionKeyword = "connection", EndpointA = "cpu", EndpointB = "chipset" }
            ]
        };
        var computer = new SysmlDefinitionNode
        {
            Name = "Computer",
            QualifiedName = "M::Computer",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "board", QualifiedName = "M::Computer::board", FeatureKeyword = "part", FeatureTyping = "Motherboard" },
                new SysmlFeatureNode { Name = "psu", QualifiedName = "M::Computer::psu", FeatureKeyword = "part", FeatureTyping = "PowerSupply" },
                new SysmlConnectionNode { ConnectionKeyword = "connection", EndpointA = "psu", EndpointB = "board" }
            ]
        };
        return new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["M::Computer"] = computer,
                ["M::Motherboard"] = motherboard
            }
        };
    }

    /// <summary>Finds the rounded part box with the given label across the whole layout tree.</summary>
    private static LayoutBox FindPartBox(LayoutTree layout, string label)
    {
        var box = layout.Nodes
            .OfType<LayoutBox>()
            .FirstOrDefault(b => b.Shape == BoxShape.RoundedRectangle && b.Label == label);
        Assert.NotNull(box);
        return box;
    }

    /// <summary>Determines whether two boxes overlap.</summary>
    private static bool Overlaps(LayoutBox a, LayoutBox b) =>
        a.X < b.X + b.Width &&
        b.X < a.X + a.Width &&
        a.Y < b.Y + b.Height &&
        b.Y < a.Y + a.Height;
}
