// <copyright file="InterconnectionViewLayoutStrategyTests.cs" company="DemaConsulting">
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

    /// <summary>
    ///     Builds a workspace where <c>M::SysA</c> (more connections/parts) genuinely nests
    ///     <c>M::SysA::SysC</c> (fewer connections/parts) as one of its own <c>Children</c>, while
    ///     both are also independently registered in <c>Declarations</c> under their own qualified
    ///     names — the shape needed to make both candidates scope-relevant for a subject exposed
    ///     only inside <c>SysC</c>.
    /// </summary>
    private static SysmlWorkspace BuildNestedCandidateWorkspace()
    {
        var sysC = new SysmlDefinitionNode
        {
            Name = "SysC",
            QualifiedName = "M::SysA::SysC",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "c1", QualifiedName = "M::SysA::SysC::c1", FeatureKeyword = "part", FeatureTyping = "C" },
                new SysmlFeatureNode { Name = "c2", QualifiedName = "M::SysA::SysC::c2", FeatureKeyword = "part", FeatureTyping = "C" },
                new SysmlConnectionNode { ConnectionKeyword = "connection", EndpointA = "c1", EndpointB = "c2" }
            ]
        };
        var sysA = new SysmlDefinitionNode
        {
            Name = "SysA",
            QualifiedName = "M::SysA",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "a1", QualifiedName = "M::SysA::a1", FeatureKeyword = "part", FeatureTyping = "A" },
                new SysmlFeatureNode { Name = "a2", QualifiedName = "M::SysA::a2", FeatureKeyword = "part", FeatureTyping = "A" },
                new SysmlFeatureNode { Name = "a3", QualifiedName = "M::SysA::a3", FeatureKeyword = "part", FeatureTyping = "A" },
                new SysmlConnectionNode { ConnectionKeyword = "connection", EndpointA = "a1", EndpointB = "a2" },
                new SysmlConnectionNode { ConnectionKeyword = "connection", EndpointA = "a2", EndpointB = "a3" },
                sysC
            ]
        };
        return new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["M::SysA"] = sysA,
                ["M::SysA::SysC"] = sysC
            }
        };
    }

    /// <summary>
    ///     Exposing an inner part of the nested definition <c>SysC</c> selects <c>SysC</c> as the
    ///     root even though its ancestor <c>SysA</c> has more connections/parts and would win the
    ///     old pure-score tie-break, proving <c>FindRoot</c> now prefers the most specific
    ///     (deepest-qualified-name) scope-relevant candidate over a less specific ancestor.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_ExposeInnerPartOfNestedDefinition_SelectsNestedDefinitionNotAncestor()
    {
        var strategy = new InterconnectionViewLayoutStrategy();
        var workspace = BuildNestedCandidateWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "M::V",
            ExposedNames = ["c1"],
            ResolvedEdges = [new SysmlEdge("M::V", "M::SysA::SysC::c1", SysmlEdgeKind.Expose)]
        };
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var container = layout.Nodes.OfType<LayoutBox>().First(b => b.Keyword == "part def");
        Assert.Equal("SysC", container.Label);

        // Only c1 (SysC's part) is rendered, none of SysA's own parts (a1/a2/a3).
        var partLabels = layout.Nodes.OfType<LayoutBox>()
            .Where(b => b.Shape == BoxShape.RoundedRectangle)
            .Select(b => b.Label)
            .ToList();
        Assert.Contains(partLabels, l => l is not null && l.Contains("c1", StringComparison.Ordinal));
        Assert.DoesNotContain(partLabels, l => l is not null &&
            (l.Contains("a1", StringComparison.Ordinal) || l.Contains("a2", StringComparison.Ordinal) || l.Contains("a3", StringComparison.Ordinal)));
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

    /// <summary>
    ///     Builds a workspace with two candidate roots: <c>M::SysA</c> (two connections, three
    ///     parts — the heuristic's default pick) and <c>M::SysB</c> (one connection, two parts),
    ///     plus an unrelated sibling definition <c>M::Unrelated</c> with no parts/connections.
    /// </summary>
    private static SysmlWorkspace BuildTwoRootWorkspace()
    {
        var sysA = new SysmlDefinitionNode
        {
            Name = "SysA",
            QualifiedName = "M::SysA",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "a1", QualifiedName = "M::SysA::a1", FeatureKeyword = "part", FeatureTyping = "A" },
                new SysmlFeatureNode { Name = "a2", QualifiedName = "M::SysA::a2", FeatureKeyword = "part", FeatureTyping = "A" },
                new SysmlFeatureNode { Name = "a3", QualifiedName = "M::SysA::a3", FeatureKeyword = "part", FeatureTyping = "A" },
                new SysmlConnectionNode { ConnectionKeyword = "connection", EndpointA = "a1", EndpointB = "a2" },
                new SysmlConnectionNode { ConnectionKeyword = "connection", EndpointA = "a2", EndpointB = "a3" }
            ]
        };
        var sysB = new SysmlDefinitionNode
        {
            Name = "SysB",
            QualifiedName = "M::SysB",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "b1", QualifiedName = "M::SysB::b1", FeatureKeyword = "part", FeatureTyping = "B" },
                new SysmlFeatureNode { Name = "b2", QualifiedName = "M::SysB::b2", FeatureKeyword = "part", FeatureTyping = "B" },
                new SysmlConnectionNode { ConnectionKeyword = "connection", EndpointA = "b1", EndpointB = "b2" }
            ]
        };
        var unrelated = new SysmlDefinitionNode { Name = "Unrelated", QualifiedName = "M::Unrelated", DefinitionKeyword = "part def" };
        return new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["M::SysA"] = sysA,
                ["M::SysB"] = sysB,
                ["M::Unrelated"] = unrelated
            }
        };
    }

    /// <summary>
    ///     With no <c>expose</c> statement (null <c>ViewNode</c>), the heuristic picks the
    ///     definition with the most connections (<c>SysA</c>), unchanged from pre-scoping behavior
    ///     — the critical --auto/no-expose regression guard.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_NullViewNode_PicksHeuristicRootUnchanged()
    {
        var strategy = new InterconnectionViewLayoutStrategy();
        var workspace = BuildTwoRootWorkspace();
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var container = layout.Nodes.OfType<LayoutBox>().First(b => b.Keyword == "part def");
        Assert.Equal("SysA", container.Label);
    }

    /// <summary>
    ///     An <c>expose</c> edge pointing at a definition other than the heuristic's default root
    ///     (<c>SysB</c>, which has fewer connections than <c>SysA</c>) selects <c>SysB</c> as the
    ///     root instead, proving <c>FindRoot</c> restricts candidates to scope-relevant ones before
    ///     the connections/parts tie-break applies.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_ExposeNonHeuristicRoot_SelectsExposedRoot()
    {
        var strategy = new InterconnectionViewLayoutStrategy();
        var workspace = BuildTwoRootWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "M::V",
            ExposedNames = ["SysB"],
            ResolvedEdges = [new SysmlEdge("M::V", "M::SysB", SysmlEdgeKind.Expose)]
        };
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var container = layout.Nodes.OfType<LayoutBox>().First(b => b.Keyword == "part def");
        Assert.Equal("SysB", container.Label);
    }

    /// <summary>
    ///     Builds a workspace with two same-depth sibling candidate roots whose qualified names
    ///     have very different lengths: <c>M::AB</c> (short name, two connections/three parts — the
    ///     better score) and <c>M::MuchLongerSiblingName</c> (long name, one connection/two parts —
    ///     the worse score).
    /// </summary>
    private static SysmlWorkspace BuildSameDepthDifferentLengthWorkspace()
    {
        var shortName = new SysmlDefinitionNode
        {
            Name = "AB",
            QualifiedName = "M::AB",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "a1", QualifiedName = "M::AB::a1", FeatureKeyword = "part", FeatureTyping = "A" },
                new SysmlFeatureNode { Name = "a2", QualifiedName = "M::AB::a2", FeatureKeyword = "part", FeatureTyping = "A" },
                new SysmlFeatureNode { Name = "a3", QualifiedName = "M::AB::a3", FeatureKeyword = "part", FeatureTyping = "A" },
                new SysmlConnectionNode { ConnectionKeyword = "connection", EndpointA = "a1", EndpointB = "a2" },
                new SysmlConnectionNode { ConnectionKeyword = "connection", EndpointA = "a2", EndpointB = "a3" }
            ]
        };
        var longName = new SysmlDefinitionNode
        {
            Name = "MuchLongerSiblingName",
            QualifiedName = "M::MuchLongerSiblingName",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "b1", QualifiedName = "M::MuchLongerSiblingName::b1", FeatureKeyword = "part", FeatureTyping = "B" },
                new SysmlFeatureNode { Name = "b2", QualifiedName = "M::MuchLongerSiblingName::b2", FeatureKeyword = "part", FeatureTyping = "B" },
                new SysmlConnectionNode { ConnectionKeyword = "connection", EndpointA = "b1", EndpointB = "b2" }
            ]
        };
        return new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["M::AB"] = shortName,
                ["M::MuchLongerSiblingName"] = longName
            }
        };
    }

    /// <summary>
    ///     When both same-depth sibling roots are made scope-relevant by their own <c>expose</c>
    ///     edges, <c>FindRoot</c> falls back to the connections/parts score heuristic — proving the
    ///     tie-break is depth-based, not a raw qualified-name-length comparison, since the shorter
    ///     name (<c>M::AB</c>) wins purely because it has the better score, not because it happens
    ///     to be shorter.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_ExposeBothSameDepthSiblings_ScoreBreaksTieNotLength()
    {
        var strategy = new InterconnectionViewLayoutStrategy();
        var workspace = BuildSameDepthDifferentLengthWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "M::V",
            ExposedNames = ["AB", "MuchLongerSiblingName"],
            ResolvedEdges =
            [
                new SysmlEdge("M::V", "M::AB", SysmlEdgeKind.Expose),
                new SysmlEdge("M::V", "M::MuchLongerSiblingName", SysmlEdgeKind.Expose)
            ]
        };
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var container = layout.Nodes.OfType<LayoutBox>().First(b => b.Keyword == "part def");
        Assert.Equal("AB", container.Label);
    }

    /// <summary>
    ///     An <c>expose</c> edge pointing at an inner child of a non-heuristic root
    ///     (<c>M::SysB::b1</c>) still selects <c>SysB</c> as the root, since the exposed subject
    ///     lies within the candidate root's own containment subtree.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_ExposeInnerChildOfNonHeuristicRoot_SelectsItsRoot()
    {
        var strategy = new InterconnectionViewLayoutStrategy();
        var workspace = BuildTwoRootWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "M::V",
            ExposedNames = ["b1"],
            ResolvedEdges = [new SysmlEdge("M::V", "M::SysB::b1", SysmlEdgeKind.Expose)]
        };
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var container = layout.Nodes.OfType<LayoutBox>().First(b => b.Keyword == "part def");
        Assert.Equal("SysB", container.Label);

        // And the part collection is narrowed to just b1 (b2 dropped, connection dropped with it).
        var partBoxes = layout.Nodes.OfType<LayoutBox>().Where(b => b.Shape == BoxShape.RoundedRectangle).ToList();
        Assert.Single(partBoxes);
        Assert.Empty(layout.Nodes.OfType<LayoutLine>());
    }

    /// <summary>
    ///     An <c>expose</c> edge pointing at a definition unrelated to every candidate root (no
    ///     containment relationship in either direction) makes no root scope-relevant, so no root
    ///     is chosen and an empty canvas results.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_ExposeUnrelatedDefinition_NoRootSelected_ReturnsMinimalCanvas()
    {
        var strategy = new InterconnectionViewLayoutStrategy();
        var workspace = BuildTwoRootWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "M::V",
            ExposedNames = ["Unrelated"],
            ResolvedEdges = [new SysmlEdge("M::V", "M::Unrelated", SysmlEdgeKind.Expose)]
        };
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        Assert.Empty(layout.Nodes);
    }

    /// <summary>
    ///     Exposing a single part narrows the interconnection to just that part (and drops any
    ///     connection referencing an excluded endpoint), producing strictly fewer part boxes than
    ///     the unscoped rendering of the same root.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_ExposeSinglePart_NarrowsToThatPart()
    {
        var strategy = new InterconnectionViewLayoutStrategy();
        var workspace = BuildTwoRootWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "M::V",
            ExposedNames = ["a1"],
            ResolvedEdges = [new SysmlEdge("M::V", "M::SysA::a1", SysmlEdgeKind.Expose)]
        };
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var scoped = strategy.BuildLayout(context, options);
        var full = strategy.BuildLayout(new ViewContext("full", workspace), options);

        var scopedParts = scoped.Nodes.OfType<LayoutBox>().Count(b => b.Shape == BoxShape.RoundedRectangle);
        var fullParts = full.Nodes.OfType<LayoutBox>().Count(b => b.Shape == BoxShape.RoundedRectangle);
        Assert.True(scopedParts < fullParts, $"expected scoped ({scopedParts}) < full ({fullParts})");
        Assert.Single(scoped.Nodes.OfType<LayoutBox>(), b => b.Shape == BoxShape.RoundedRectangle);
    }

    /// <summary>
    ///     An <c>expose</c> statement naming two separate parts of the same root includes both
    ///     (the union of every exposed target's containment subtree), and their connection is kept
    ///     since both endpoints remain in scope.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_ExposeMultipleParts_UnionsBothSubtrees()
    {
        var strategy = new InterconnectionViewLayoutStrategy();
        var workspace = BuildTwoRootWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "M::V",
            ExposedNames = ["a1", "a2"],
            ResolvedEdges =
            [
                new SysmlEdge("M::V", "M::SysA::a1", SysmlEdgeKind.Expose),
                new SysmlEdge("M::V", "M::SysA::a2", SysmlEdgeKind.Expose)
            ]
        };
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var partBoxes = layout.Nodes.OfType<LayoutBox>().Where(b => b.Shape == BoxShape.RoundedRectangle).ToList();
        Assert.Equal(2, partBoxes.Count);
        Assert.Single(layout.Nodes.OfType<LayoutLine>());
    }

    /// <summary>
    ///     An <c>expose</c> edge that resolves to a feature usage (not a definition) still narrows
    ///     the interconnection to that usage's containment subtree via the shared usage-to-type
    ///     fallback in <c>ExposeScopeResolver.ResolveExposedScope</c>: exposing a usage
    ///     <c>myPart</c> typed by <c>SysA</c> selects <c>SysA</c> as the root.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_ExposedUsage_ResolvesThroughTypingToRoot()
    {
        var strategy = new InterconnectionViewLayoutStrategy();
        var workspace = BuildTwoRootWorkspace();
        workspace.AddDeclaration("M::myPart", new SysmlFeatureNode
        {
            Name = "myPart",
            QualifiedName = "M::myPart",
            FeatureTyping = "SysB",
            ResolvedEdges = [new SysmlEdge("M::myPart", "M::SysB", SysmlEdgeKind.Typing)]
        });
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "M::V",
            ExposedNames = ["myPart"],
            ResolvedEdges = [new SysmlEdge("M::V", "M::myPart", SysmlEdgeKind.Expose)]
        };
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var container = layout.Nodes.OfType<LayoutBox>().First(b => b.Keyword == "part def");
        Assert.Equal("SysB", container.Label);
    }
}
