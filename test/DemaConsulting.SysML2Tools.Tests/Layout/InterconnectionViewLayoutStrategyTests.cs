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
        var boxes = CollectBoxes(layout.Nodes).ToList();
        Assert.Contains(boxes, b => b.Keyword == "part def" && b.Label == "PowerSystem");
        Assert.Equal(2, boxes.Count(b => b.Shape == BoxShape.RoundedRectangle));
        Assert.Equal(2, CollectPorts(layout.Nodes).Count());
        Assert.Single(CollectLines(layout.Nodes));
    }

    /// <summary>
    ///     The root container box nests its interior content (part boxes, ports, and connector
    ///     lines) as its own <see cref="LayoutBox.Children"/> rather than as flat top-level
    ///     siblings: <see cref="LayoutTree.Nodes"/> contains exactly one element (the root box), and
    ///     that box's <c>Children</c> contains the expected part boxes, ports, and connector line.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_RootContent_IsNestedAsRootBoxChildren()
    {
        // Arrange: a simple part def with two parts and one connection between them.
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

        // Assert: exactly one top-level node (the root container box).
        var root = Assert.Single(layout.Nodes);
        var rootBox = Assert.IsType<LayoutBox>(root);
        Assert.Equal("part def", rootBox.Keyword);
        Assert.Equal("PowerSystem", rootBox.Label);

        // Assert: the root box's own Children hold the interior content — two part boxes, two
        // ports, and one connector line — none of it as flat top-level siblings.
        Assert.NotEmpty(rootBox.Children);
        Assert.Equal(2, rootBox.Children.OfType<LayoutBox>().Count(b => b.Shape == BoxShape.RoundedRectangle));
        Assert.Equal(2, rootBox.Children.OfType<LayoutPort>().Count());
        Assert.Single(rootBox.Children.OfType<LayoutLine>());
    }

    /// <summary>
    ///     A part with a high connection degree (many incident connections) still produces
    ///     non-overlapping boxes and a labeled port for every incident connection, now that box
    ///     sizing/port spacing is fully delegated to the layered engine instead of the removed
    ///     <c>MinPortSlot</c>/<c>ConnectorClearance</c> heuristic.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_HighConnectionDegreePart_BoxesDoNotOverlapAndPortsAreLabeled()
    {
        // Arrange: a Gantry part def with a controller wired to a motor by five separate connections
        // (a higher connection degree than the ThreeParallelConnections test), exercising the
        // engine's own port-spacing/box-height resolution.
        var strategy = new InterconnectionViewLayoutStrategy();
        var gantry = new SysmlDefinitionNode
        {
            Name = "Gantry",
            QualifiedName = "M::Gantry",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "controller", QualifiedName = "M::Gantry::controller", FeatureKeyword = "part", FeatureTyping = "Controller" },
                new SysmlFeatureNode { Name = "motor", QualifiedName = "M::Gantry::motor", FeatureKeyword = "part", FeatureTyping = "Motor" },
                new SysmlConnectionNode { Name = "power", QualifiedName = "M::Gantry::power", ConnectionKeyword = "connection", EndpointA = "controller.power", EndpointB = "motor.power" },
                new SysmlConnectionNode { Name = "encoder", QualifiedName = "M::Gantry::encoder", ConnectionKeyword = "connection", EndpointA = "controller.J40", EndpointB = "motor.encoder" },
                new SysmlConnectionNode { Name = "sensor", QualifiedName = "M::Gantry::sensor", ConnectionKeyword = "connection", EndpointA = "controller.sensor", EndpointB = "motor.SensorPort" },
                new SysmlConnectionNode { Name = "opto", QualifiedName = "M::Gantry::opto", ConnectionKeyword = "connection", EndpointA = "controller.opto", EndpointB = "motor.OptoSensor" },
                new SysmlConnectionNode { Name = "limit", QualifiedName = "M::Gantry::limit", ConnectionKeyword = "connection", EndpointA = "controller.limit", EndpointB = "motor.LimitSwitch" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["M::Gantry"] = gantry }
        };
        var context = new ViewContext("GantryInterconnectionView", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: the two part boxes never overlap, regardless of how many ports the engine placed
        // on either of them.
        var partBoxes = CollectBoxes(layout.Nodes).Where(b => b.Shape == BoxShape.RoundedRectangle).ToList();
        Assert.Equal(2, partBoxes.Count);
        Assert.False(Overlaps(partBoxes[0], partBoxes[1]));

        // Assert: every incident connection produced a labeled port (ten ports total: five
        // connections, two endpoints each).
        var ports = CollectPorts(layout.Nodes).ToList();
        Assert.Equal(10, ports.Count);
        Assert.Contains(ports, p => p.ExternalLabel == "power");
        Assert.Contains(ports, p => p.ExternalLabel == "J40");
        Assert.Contains(ports, p => p.ExternalLabel == "encoder");
        Assert.Contains(ports, p => p.ExternalLabel == "sensor");
        Assert.Contains(ports, p => p.ExternalLabel == "SensorPort");
        Assert.Contains(ports, p => p.ExternalLabel == "opto");
        Assert.Contains(ports, p => p.ExternalLabel == "OptoSensor");
        Assert.Contains(ports, p => p.ExternalLabel == "limit");
        Assert.Contains(ports, p => p.ExternalLabel == "LimitSwitch");
    }

    /// <summary>
    ///     No left/right port's centre falls within its owning part box's own title area. This
    ///     guards the label-collision defect fixed by flagging every part node as carrying a title
    ///     (<c>HasLabel</c>/<c>HasKeyword</c>) when handed to the layered algorithm (see
    ///     <see cref="LayeredPlacement.PlaceWithPorts"/>), which activates the engine's automatic
    ///     title-vs-side-port reservation so ports never land in the header row a titled box renders.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_PartWithPorts_PortsNeverOverlapBoxTitleArea()
    {
        // Arrange: reuse the high-connection-degree Gantry fixture, which already exercises many
        // ports on one titled box.
        var strategy = new InterconnectionViewLayoutStrategy();
        var gantry = new SysmlDefinitionNode
        {
            Name = "Gantry",
            QualifiedName = "M::Gantry",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "controller", QualifiedName = "M::Gantry::controller", FeatureKeyword = "part", FeatureTyping = "Controller" },
                new SysmlFeatureNode { Name = "motor", QualifiedName = "M::Gantry::motor", FeatureKeyword = "part", FeatureTyping = "Motor" },
                new SysmlConnectionNode { Name = "power", QualifiedName = "M::Gantry::power", ConnectionKeyword = "connection", EndpointA = "controller.power", EndpointB = "motor.power" },
                new SysmlConnectionNode { Name = "encoder", QualifiedName = "M::Gantry::encoder", ConnectionKeyword = "connection", EndpointA = "controller.J40", EndpointB = "motor.encoder" },
                new SysmlConnectionNode { Name = "sensor", QualifiedName = "M::Gantry::sensor", ConnectionKeyword = "connection", EndpointA = "controller.sensor", EndpointB = "motor.SensorPort" },
                new SysmlConnectionNode { Name = "opto", QualifiedName = "M::Gantry::opto", ConnectionKeyword = "connection", EndpointA = "controller.opto", EndpointB = "motor.OptoSensor" },
                new SysmlConnectionNode { Name = "limit", QualifiedName = "M::Gantry::limit", ConnectionKeyword = "connection", EndpointA = "controller.limit", EndpointB = "motor.LimitSwitch" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["M::Gantry"] = gantry }
        };
        var context = new ViewContext("GantryInterconnectionView", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: every left/right port's centre sits at or below its owning box's title area, never
        // inside the header row where the box's "«keyword» / name : type" title is drawn.
        var partBoxes = CollectBoxes(layout.Nodes).Where(b => b.Shape == BoxShape.RoundedRectangle).ToList();
        var titleArea = BoxMetrics.TitleAreaHeight(options.Theme, hasLabel: true, hasKeyword: true);
        var sidePorts = CollectPorts(layout.Nodes).Where(p => p.Side is PortSide.Left or PortSide.Right).ToList();
        Assert.NotEmpty(sidePorts);

        foreach (var port in sidePorts)
        {
            var owningBox = partBoxes.FirstOrDefault(b =>
                port.CentreY >= b.Y - 0.01 && port.CentreY <= b.Y + b.Height + 0.01 &&
                (Math.Abs(port.CentreX - b.X) < 0.5 || Math.Abs(port.CentreX - (b.X + b.Width)) < 0.5));

            Assert.NotNull(owningBox);
            Assert.True(
                port.CentreY >= owningBox!.Y + titleArea,
                $"port at ({port.CentreX}, {port.CentreY}) overlaps the title area of its box (Y={owningBox.Y}, titleArea={titleArea})");
        }
    }

    /// <summary>
    ///     Two connection usages between the SAME two parts (both <c>EndpointA=a, EndpointB=b</c>) form
    ///     an identical directed pair. With parallel-edge merging disabled for interconnection views,
    ///     every distinct SysML connection is preserved as its own independently-routed connector: the
    ///     two connections must resolve to two connector polylines with genuinely distinct waypoints
    ///     (separate parallel lanes), not one shared route that happens to be emitted twice.
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

        // Act: laying out must not throw, and each parallel connection gets its own routed lane.
        var layout = strategy.BuildLayout(context, options);

        // Assert: two connector polylines (one per connection), each with at least two waypoints,
        // and the two polylines are NOT identical — each is routed as its own distinct parallel lane.
        var lines = CollectLines(layout.Nodes).ToList();
        Assert.Equal(2, lines.Count);
        Assert.All(lines, l => Assert.True(l.Waypoints.Count >= 2));
        Assert.NotEqual(lines[0].Waypoints, lines[1].Waypoints);

        // Assert: two part boxes and one port pair per connection (four ports total).
        Assert.Equal(2, CollectBoxes(layout.Nodes).Count(b => b.Shape == BoxShape.RoundedRectangle));
        Assert.Equal(4, CollectPorts(layout.Nodes).Count());
    }

    /// <summary>
    ///     Three distinct connections between the same two parts (mirroring the 3-axis-gantry
    ///     wiring model that revealed the collapsing bug) each render as their own independently
    ///     routed connector with pairwise-distinct waypoints, and each connector's ports carry the
    ///     real SysML port name from the endpoint reference.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_ThreeParallelConnections_ProducesThreeDistinctConnectors()
    {
        // Arrange: a Gantry part def with a controller and a motor wired by three separate connections.
        var strategy = new InterconnectionViewLayoutStrategy();
        var gantry = new SysmlDefinitionNode
        {
            Name = "Gantry",
            QualifiedName = "M::Gantry",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "controller", QualifiedName = "M::Gantry::controller", FeatureKeyword = "part", FeatureTyping = "Controller" },
                new SysmlFeatureNode { Name = "motor", QualifiedName = "M::Gantry::motor", FeatureKeyword = "part", FeatureTyping = "Motor" },
                new SysmlConnectionNode { Name = "power", QualifiedName = "M::Gantry::power", ConnectionKeyword = "connection", EndpointA = "controller.power", EndpointB = "motor.power" },
                new SysmlConnectionNode { Name = "encoder", QualifiedName = "M::Gantry::encoder", ConnectionKeyword = "connection", EndpointA = "controller.J40", EndpointB = "motor.encoder" },
                new SysmlConnectionNode { Name = "sensor", QualifiedName = "M::Gantry::sensor", ConnectionKeyword = "connection", EndpointA = "controller.sensor", EndpointB = "motor.SensorPort" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["M::Gantry"] = gantry }
        };
        var context = new ViewContext("GantryInterconnectionView", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: three distinct connectors, pairwise-different waypoints, six ports total.
        var lines = CollectLines(layout.Nodes).ToList();
        Assert.Equal(3, lines.Count);
        Assert.NotEqual(lines[0].Waypoints, lines[1].Waypoints);
        Assert.NotEqual(lines[1].Waypoints, lines[2].Waypoints);
        Assert.NotEqual(lines[0].Waypoints, lines[2].Waypoints);

        var ports = CollectPorts(layout.Nodes).ToList();
        Assert.Equal(6, ports.Count);
        Assert.Contains(ports, p => p.ExternalLabel == "power");
        Assert.Contains(ports, p => p.ExternalLabel == "J40");
        Assert.Contains(ports, p => p.ExternalLabel == "encoder");
        Assert.Contains(ports, p => p.ExternalLabel == "sensor");
        Assert.Contains(ports, p => p.ExternalLabel == "SensorPort");
    }

    /// <summary>
    ///     A connection endpoint referencing a dotted port segment (e.g. <c>StepperMotorX.encoder</c>)
    ///     produces a <see cref="LayoutPort"/> whose <see cref="LayoutPort.ExternalLabel"/> is the real
    ///     SysML port name, not <see langword="null"/> (the pre-fix behavior).
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_ConnectionEndpointWithPortSegment_PortLabelReflectsSysmlPortName()
    {
        // Arrange
        var strategy = new InterconnectionViewLayoutStrategy();
        var root = new SysmlDefinitionNode
        {
            Name = "LBO3AxisGantry",
            QualifiedName = "M::LBO3AxisGantry",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "StepperMotorX", QualifiedName = "M::LBO3AxisGantry::StepperMotorX", FeatureKeyword = "part", FeatureTyping = "StepperMotor" },
                new SysmlFeatureNode { Name = "LBO3AxisGantry", QualifiedName = "M::LBO3AxisGantry::LBO3AxisGantry", FeatureKeyword = "part", FeatureTyping = "Controller" },
                new SysmlConnectionNode
                {
                    Name = "encoderConn",
                    QualifiedName = "M::LBO3AxisGantry::encoderConn",
                    ConnectionKeyword = "connection",
                    EndpointA = "StepperMotorX.encoder",
                    EndpointB = "LBO3AxisGantry.J40"
                }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["M::LBO3AxisGantry"] = root }
        };
        var context = new ViewContext("GantryInterconnectionView", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: both ends carry their real SysML port-name label.
        var ports = CollectPorts(layout.Nodes).ToList();
        Assert.Equal(2, ports.Count);
        Assert.Contains(ports, p => p.ExternalLabel == "encoder");
        Assert.Contains(ports, p => p.ExternalLabel == "J40");
    }

    /// <summary>
    ///     A connection endpoint referencing a nested/cross-boundary path (e.g. <c>board.cpu</c>, into
    ///     a part inside a container) still resolves the connector to the containing part's own
    ///     boundary (the documented cross-boundary limitation is not fully lifted by this feature — see
    ///     the design documentation), but the port label now reflects the true, full dotted reference
    ///     rather than discarding everything after the container's name.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_CrossBoundaryEndpoint_LabelReflectsNestedTarget()
    {
        // Arrange: Computer { board : Motherboard{cpu, chipset, ...}, psu, connect psu to board.cpu }
        var strategy = new InterconnectionViewLayoutStrategy();
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
                new SysmlConnectionNode { ConnectionKeyword = "connection", EndpointA = "psu", EndpointB = "board.cpu" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["M::Computer"] = computer,
                ["M::Motherboard"] = motherboard
            }
        };
        var context = new ViewContext("ComputerInterconnectionView", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: the connector still terminates at the "board" container box (one line at the root
        // level, no exception), but the target-side port label is the true nested target "cpu", not
        // discarded. Only the root box's own (non-recursive) Children are inspected here, since the
        // nested Motherboard's own internal cpu-chipset connector is a separate, deeper connector
        // that CollectLines/CollectPorts would otherwise also surface via recursion.
        var rootBox = Assert.IsType<LayoutBox>(Assert.Single(layout.Nodes));
        var lines = rootBox.Children.OfType<LayoutLine>().ToList();
        Assert.Single(lines);

        var ports = rootBox.Children.OfType<LayoutPort>().ToList();
        Assert.Equal(2, ports.Count);
        Assert.Contains(ports, p => p.ExternalLabel == "cpu");
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
        var partBoxes = CollectBoxes(layout.Nodes).Where(b => b.Shape == BoxShape.RoundedRectangle).ToList();
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
        var partBoxes = CollectBoxes(layout.Nodes).Where(b => b.Shape == BoxShape.RoundedRectangle).ToList();
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
            ExposeMembers = [new ExposeMember("c1", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("M::V", "M::SysA::SysC::c1", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var container = CollectBoxes(layout.Nodes).First(b => b.Keyword == "part def");
        Assert.Equal("SysC", container.Label);

        // Only c1 (SysC's part) is rendered, none of SysA's own parts (a1/a2/a3).
        var partLabels = CollectBoxes(layout.Nodes)
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
        var box = CollectBoxes(layout.Nodes)
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
    ///     Recursively collects all <see cref="LayoutBox"/> nodes from a node list, including those
    ///     nested inside a container box's <see cref="LayoutBox.Children"/>.
    /// </summary>
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
    ///     Recursively collects all <see cref="LayoutPort"/> nodes from a node list, including those
    ///     nested inside a container box's <see cref="LayoutBox.Children"/>.
    /// </summary>
    private static IReadOnlyList<LayoutPort> CollectPorts(IReadOnlyList<LayoutNode> nodes)
    {
        var result = new List<LayoutPort>();
        void Walk(IReadOnlyList<LayoutNode> ns)
        {
            foreach (var n in ns)
            {
                switch (n)
                {
                    case LayoutPort port:
                        result.Add(port);
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
    ///     Recursively collects all <see cref="LayoutLine"/> nodes from a node list, including those
    ///     nested inside a container box's <see cref="LayoutBox.Children"/>.
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

        var container = CollectBoxes(layout.Nodes).First(b => b.Keyword == "part def");
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
            ExposeMembers = [new ExposeMember("SysB", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("M::V", "M::SysB", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var container = CollectBoxes(layout.Nodes).First(b => b.Keyword == "part def");
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
            ExposeMembers = [new ExposeMember("AB", null, ExposeRecursionKind.MembershipRecursive), new ExposeMember("MuchLongerSiblingName", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges =
            [
                new SysmlEdge("M::V", "M::AB", SysmlEdgeKind.Expose),
                new SysmlEdge("M::V", "M::MuchLongerSiblingName", SysmlEdgeKind.Expose)
            ]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var container = CollectBoxes(layout.Nodes).First(b => b.Keyword == "part def");
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
            ExposeMembers = [new ExposeMember("b1", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("M::V", "M::SysB::b1", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var container = CollectBoxes(layout.Nodes).First(b => b.Keyword == "part def");
        Assert.Equal("SysB", container.Label);

        // And the part collection is narrowed to just b1 (b2 dropped, connection dropped with it).
        var partBoxes = CollectBoxes(layout.Nodes).Where(b => b.Shape == BoxShape.RoundedRectangle).ToList();
        Assert.Single(partBoxes);
        Assert.Empty(CollectLines(layout.Nodes));
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
            ExposeMembers = [new ExposeMember("Unrelated", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("M::V", "M::Unrelated", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
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
            ExposeMembers = [new ExposeMember("a1", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("M::V", "M::SysA::a1", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var scoped = strategy.BuildLayout(context, options);
        var full = strategy.BuildLayout(new ViewContext("full", workspace), options);

        var scopedParts = CollectBoxes(scoped.Nodes).Count(b => b.Shape == BoxShape.RoundedRectangle);
        var fullParts = CollectBoxes(full.Nodes).Count(b => b.Shape == BoxShape.RoundedRectangle);
        Assert.True(scopedParts < fullParts, $"expected scoped ({scopedParts}) < full ({fullParts})");
        Assert.Single(CollectBoxes(scoped.Nodes), b => b.Shape == BoxShape.RoundedRectangle);
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
            ExposeMembers = [new ExposeMember("a1", null, ExposeRecursionKind.MembershipRecursive), new ExposeMember("a2", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges =
            [
                new SysmlEdge("M::V", "M::SysA::a1", SysmlEdgeKind.Expose),
                new SysmlEdge("M::V", "M::SysA::a2", SysmlEdgeKind.Expose)
            ]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var partBoxes = CollectBoxes(layout.Nodes).Where(b => b.Shape == BoxShape.RoundedRectangle).ToList();
        Assert.Equal(2, partBoxes.Count);
        Assert.Single(CollectLines(layout.Nodes));
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
            ExposeMembers = [new ExposeMember("myPart", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("M::V", "M::myPart", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var container = CollectBoxes(layout.Nodes).First(b => b.Keyword == "part def");
        Assert.Equal("SysB", container.Label);
    }

    /// <summary>
    ///     Builds a workspace where the root system definition (<c>Root::NsA::System</c>) composes a
    ///     subsystem (<c>Root::NsB::Sub</c>) via a typed <c>part</c> feature, and that subsystem in
    ///     turn composes two of its own nested units — but the subsystem lives in a <em>different</em>
    ///     namespace than the root, so the subsystem's own part features' qualified names
    ///     (<c>Root::NsB::Sub::unit1</c>/<c>unit2</c>) fall outside the <c>Root::NsA::System::</c>
    ///     prefix. This is the exact shape that reproduced the VersionMark bug: composition structure
    ///     and namespace/file organization are independent in SysML v2, so a re-applied expose-scope
    ///     namespace-prefix check at recursion depth &gt; 0 incorrectly hid a genuinely nested part's
    ///     own interior.
    /// </summary>
    private static SysmlWorkspace BuildCrossNamespaceCompositionWorkspace()
    {
        var subsystem = new SysmlDefinitionNode
        {
            Name = "Sub",
            QualifiedName = "Root::NsB::Sub",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "unit1", QualifiedName = "Root::NsB::Sub::unit1", FeatureKeyword = "part", FeatureTyping = "Unit" },
                new SysmlFeatureNode { Name = "unit2", QualifiedName = "Root::NsB::Sub::unit2", FeatureKeyword = "part", FeatureTyping = "Unit" }
            ]
        };
        var system = new SysmlDefinitionNode
        {
            Name = "System",
            QualifiedName = "Root::NsA::System",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "sub", QualifiedName = "Root::NsA::System::sub", FeatureKeyword = "part", FeatureTyping = "Sub" }
            ]
        };
        return new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["Root::NsA::System"] = system,
                ["Root::NsB::Sub"] = subsystem
            }
        };
    }

    /// <summary>
    ///     Regression test (SysML v2 spec §9.2.20.2.6, "nested features as nested nodes"): when the
    ///     view exposes only the root definition recursively (<c>expose Root::NsA::System::**;</c>),
    ///     the root's nested subsystem part still renders its own nested units, even though the
    ///     subsystem is declared in a different namespace than the root's exposed subject and its own
    ///     part features' qualified names fall outside the root subject's namespace prefix. Before
    ///     the fix, the same expose scope was re-applied unchanged at every recursion depth, so the
    ///     subsystem's own parts were incorrectly filtered out and rendered as an empty box.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_ExposeRootOnly_NestedSubsystemInDifferentNamespace_RendersItsOwnUnits()
    {
        var strategy = new InterconnectionViewLayoutStrategy();
        var workspace = BuildCrossNamespaceCompositionWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            ExposeMembers = [new ExposeMember("Root::NsA::System", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("Root::V", "Root::NsA::System", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var container = CollectBoxes(layout.Nodes).First(b => b.Keyword == "part def");
        Assert.Equal("System", container.Label);

        // The subsystem's own nested units render as nested part boxes, not an empty container.
        var subBox = FindPartBox(layout, "sub : Sub");
        var nestedLabels = CollectBoxes(subBox.Children).Select(b => b.Label).ToList();
        Assert.Contains(nestedLabels, l => l is not null && l.Contains("unit1", StringComparison.Ordinal));
        Assert.Contains(nestedLabels, l => l is not null && l.Contains("unit2", StringComparison.Ordinal));
    }

    /// <summary>
    ///     When the resolved <c>expose</c> scope is purely non-recursive — here <c>expose
    ///     Root::NsA::System;</c> plus <c>expose Root::NsA::System::*;</c>, i.e. one
    ///     <see cref="ExposeRecursionKind.MembershipExact"/> subject and one
    ///     <see cref="ExposeRecursionKind.NamespaceDirectChildren"/> subject, with <em>no</em> subject
    ///     carrying unlimited-depth recursion — <c>HasUnlimitedRecursion</c> evaluates to
    ///     <see langword="false"/>, so interior expansion stops at depth 0: the root's own direct part
    ///     child (<c>sub : Sub</c>) still renders as its own box, but that box carries no nested
    ///     <c>unit1</c>/<c>unit2</c> boxes of its own, even though <c>Sub</c> has its own nested parts.
    ///     This is the fix-targeting scenario: contrast with
    ///     <see cref="InterconnectionView_BuildLayout_ExposeRootOnly_NestedSubsystemInDifferentNamespace_RendersItsOwnUnits"/>,
    ///     which uses a recursive subject on the same workspace shape and must retain full recursion.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_ExposeNonRecursiveSubjects_DoesNotRecurseIntoNestedPartsInterior()
    {
        var strategy = new InterconnectionViewLayoutStrategy();
        var workspace = BuildCrossNamespaceCompositionWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            ExposeMembers =
            [
                new ExposeMember("Root::NsA::System", null, ExposeRecursionKind.MembershipExact),
                new ExposeMember("Root::NsA::System", null, ExposeRecursionKind.NamespaceDirectChildren)
            ],
            ResolvedEdges =
            [
                new SysmlEdge("Root::V", "Root::NsA::System", SysmlEdgeKind.Expose),
                new SysmlEdge("Root::V", "Root::NsA::System", SysmlEdgeKind.Expose)
            ]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var container = CollectBoxes(layout.Nodes).First(b => b.Keyword == "part def");
        Assert.Equal("System", container.Label);

        // The subsystem itself still renders as a box, but as a leaf — its own unit1/unit2 interior
        // is never drawn under a non-recursive scope.
        var subBox = FindPartBox(layout, "sub : Sub");
        Assert.Empty(CollectBoxes(subBox.Children));
    }

    /// <summary>
    ///     Regression test for the per-branch recursion fix: when the resolved <c>expose</c> scope
    ///     combines a subject with unlimited-depth recursion (<c>expose System::a::**;</c>, i.e.
    ///     <see cref="ExposeRecursionKind.MembershipRecursive"/> on the root's own <c>a</c> feature)
    ///     with a subject that has no depth limit at all (<c>expose System::b;</c>, i.e.
    ///     <see cref="ExposeRecursionKind.MembershipExact"/> on the root's own <c>b</c> feature), only
    ///     the branch matched by the recursive subject fully recurses — <c>a</c>'s own nested parts
    ///     (<c>a1</c>/<c>a2</c>) render as nested boxes — while the branch matched only by the
    ///     non-recursive subject stays depth-limited to itself: <c>b</c> still renders as its own box,
    ///     but as a leaf, even though its type (<c>SubB</c>) has its own nested parts (<c>b1</c>/
    ///     <c>b2</c>). Before the fix, a single diagram-wide <c>unlimitedRecursion</c> boolean meant
    ///     the presence of the recursive subject on <c>a</c> would have also (incorrectly) unlocked
    ///     full recursion for <c>b</c>'s branch.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_ExposeMixedRecursion_OnlyRecursiveBranchRecurses()
    {
        // Arrange: System { a : SubA { a1, a2 }, b : SubB { b1, b2 } }, exposed via one recursive
        // subject on "a" and one exact (non-recursive) subject on "b".
        var strategy = new InterconnectionViewLayoutStrategy();
        var subA = new SysmlDefinitionNode
        {
            Name = "SubA",
            QualifiedName = "SubA",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "a1", QualifiedName = "SubA::a1", FeatureKeyword = "part", FeatureTyping = "UnitA1" },
                new SysmlFeatureNode { Name = "a2", QualifiedName = "SubA::a2", FeatureKeyword = "part", FeatureTyping = "UnitA2" }
            ]
        };
        var subB = new SysmlDefinitionNode
        {
            Name = "SubB",
            QualifiedName = "SubB",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "b1", QualifiedName = "SubB::b1", FeatureKeyword = "part", FeatureTyping = "UnitB1" },
                new SysmlFeatureNode { Name = "b2", QualifiedName = "SubB::b2", FeatureKeyword = "part", FeatureTyping = "UnitB2" }
            ]
        };
        var system = new SysmlDefinitionNode
        {
            Name = "System",
            QualifiedName = "System",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "a", QualifiedName = "System::a", FeatureKeyword = "part", FeatureTyping = "SubA" },
                new SysmlFeatureNode { Name = "b", QualifiedName = "System::b", FeatureKeyword = "part", FeatureTyping = "SubB" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["System"] = system,
                ["SubA"] = subA,
                ["SubB"] = subB
            }
        };
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "System::V",
            ExposeMembers =
            [
                new ExposeMember("System::a", null, ExposeRecursionKind.MembershipRecursive),
                new ExposeMember("System::b", null, ExposeRecursionKind.MembershipExact)
            ],
            ResolvedEdges =
            [
                new SysmlEdge("System::V", "System::a", SysmlEdgeKind.Expose),
                new SysmlEdge("System::V", "System::b", SysmlEdgeKind.Expose)
            ]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: the root's own two direct children both render, but only "a"'s branch recurses.
        var container = CollectBoxes(layout.Nodes).First(b => b.Keyword == "part def");
        Assert.Equal("System", container.Label);

        var aBox = FindPartBox(layout, "a : SubA");
        var aNestedLabels = CollectBoxes(aBox.Children).Select(b => b.Label).ToList();
        Assert.Contains(aNestedLabels, l => l is not null && l.Contains("a1", StringComparison.Ordinal));
        Assert.Contains(aNestedLabels, l => l is not null && l.Contains("a2", StringComparison.Ordinal));

        var bBox = FindPartBox(layout, "b : SubB");
        Assert.Empty(CollectBoxes(bBox.Children));
    }

    /// <summary>
    ///     Builds a workspace with a genuine two-tier composition root (<c>Root::NsA::System</c>
    ///     composes <c>Root::NsB::Sub</c> via a typed <c>part</c> feature) alongside an unrelated
    ///     orphan definition (<c>Root::NsC::Deeply::Nested::OrphanLeaf</c>) that has a deeper
    ///     qualified name than <c>System</c> but composes nothing and is composed by nothing.
    /// </summary>
    private static SysmlWorkspace BuildBroadNamespaceWorkspace()
    {
        var system = new SysmlDefinitionNode
        {
            Name = "System",
            QualifiedName = "Root::NsA::System",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "sub", QualifiedName = "Root::NsA::System::sub", FeatureKeyword = "part", FeatureTyping = "Sub" }
            ]
        };
        var sub = new SysmlDefinitionNode { Name = "Sub", QualifiedName = "Root::NsB::Sub", DefinitionKeyword = "part def" };
        var orphanLeaf = new SysmlDefinitionNode
        {
            Name = "OrphanLeaf",
            QualifiedName = "Root::NsC::Deeply::Nested::OrphanLeaf",
            DefinitionKeyword = "part def"
        };
        return new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["Root::NsA::System"] = system,
                ["Root::NsB::Sub"] = sub,
                ["Root::NsC::Deeply::Nested::OrphanLeaf"] = orphanLeaf
            }
        };
    }

    /// <summary>
    ///     Regression test: exposing an entire namespace recursively (<c>expose Root::**;</c>) makes
    ///     every definition it contains scope-relevant, including an unrelated orphan leaf definition
    ///     with a deeper qualified name than the genuine composition root. <c>FindRoot</c> must select
    ///     <c>System</c> — the one candidate nothing else composes and that itself composes
    ///     something — rather than <c>OrphanLeaf</c>, which the old pure qualified-name-depth
    ///     specificity tie-break would have picked purely for having more <c>"::"</c> segments,
    ///     despite composing and being composed by nothing (the exact "arbitrary root" bug diagnosed
    ///     against the real VersionMark model).
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_ExposeWholeNamespace_SelectsGenuineCompositionRootNotDeepestOrphan()
    {
        var strategy = new InterconnectionViewLayoutStrategy();
        var workspace = BuildBroadNamespaceWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Root::V",
            ExposeMembers = [new ExposeMember("Root", null, ExposeRecursionKind.NamespaceRecursive)],
            ResolvedEdges = [new SysmlEdge("Root::V", "Root", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var container = CollectBoxes(layout.Nodes).First(b => b.Keyword == "part def");
        Assert.Equal("System", container.Label);
    }

    /// <summary>
    ///     Regression test: a part def whose parts have no connections between them (a common case
    ///     for a top-level system composed purely of independent subsystems) must be packed into a
    ///     compact multi-row/column grid via the containment-packing algorithm, not stacked into a
    ///     single tall vertical column. Reproduces the shape of the real VersionMark model that
    ///     surfaced the bug: nine unconnected peer parts. (Fewer than about nine same-sized minimum-
    ///     width parts is not actually enough for the packer's area-based width budget to clear the
    ///     two-column threshold — this part count is the smallest that reliably demonstrates the
    ///     fix rather than an artifact of the packer's own sizing heuristic.) A degenerate single-
    ///     column layout would place every box at a distinct X (or Y) coordinate; the containment-
    ///     packed layout instead reuses coordinates across rows/columns.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_UnconnectedPeerParts_PacksIntoCompactGridNotSingleColumn()
    {
        // Arrange: a System part def with nine unconnected parts (no SysmlConnectionNode children).
        var strategy = new InterconnectionViewLayoutStrategy();
        var system = new SysmlDefinitionNode
        {
            Name = "System",
            QualifiedName = "M::System",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "p1", QualifiedName = "M::System::p1", FeatureKeyword = "part", FeatureTyping = "P1" },
                new SysmlFeatureNode { Name = "p2", QualifiedName = "M::System::p2", FeatureKeyword = "part", FeatureTyping = "P2" },
                new SysmlFeatureNode { Name = "p3", QualifiedName = "M::System::p3", FeatureKeyword = "part", FeatureTyping = "P3" },
                new SysmlFeatureNode { Name = "p4", QualifiedName = "M::System::p4", FeatureKeyword = "part", FeatureTyping = "P4" },
                new SysmlFeatureNode { Name = "p5", QualifiedName = "M::System::p5", FeatureKeyword = "part", FeatureTyping = "P5" },
                new SysmlFeatureNode { Name = "p6", QualifiedName = "M::System::p6", FeatureKeyword = "part", FeatureTyping = "P6" },
                new SysmlFeatureNode { Name = "p7", QualifiedName = "M::System::p7", FeatureKeyword = "part", FeatureTyping = "P7" },
                new SysmlFeatureNode { Name = "p8", QualifiedName = "M::System::p8", FeatureKeyword = "part", FeatureTyping = "P8" },
                new SysmlFeatureNode { Name = "p9", QualifiedName = "M::System::p9", FeatureKeyword = "part", FeatureTyping = "P9" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["M::System"] = system }
        };
        var context = new ViewContext("SystemInterconnectionView", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: nine non-overlapping part boxes, no connector lines (nothing to route).
        var parts = CollectBoxes(layout.Nodes).Where(b => b.Shape == BoxShape.RoundedRectangle).ToList();
        Assert.Equal(9, parts.Count);
        Assert.Empty(CollectLines(layout.Nodes));
        for (var i = 0; i < parts.Count; i++)
        {
            for (var j = i + 1; j < parts.Count; j++)
            {
                Assert.False(Overlaps(parts[i], parts[j]), $"{parts[i].Label} overlaps {parts[j].Label}");
            }
        }

        // Assert: packed into more than one row and more than one column — a degenerate single
        // vertical column would have six distinct Y values and a single shared X value.
        var distinctX = parts.Select(p => p.X).Distinct().Count();
        var distinctY = parts.Select(p => p.Y).Distinct().Count();
        Assert.True(distinctX > 1, "Expected parts to span more than one column, not a single vertical column.");
        Assert.True(distinctY < parts.Count, "Expected some parts to share a row, not one row per part.");
    }

    /// <summary>
    ///     Regression test for the reported crash/empty-diagram bug: a view exposing a namespace's
    ///     direct children (<c>expose PublishingSubsystem::*;</c>) where <c>PublishingSubsystem</c>
    ///     is itself only a namespace-like <c>part def</c> with a single nested <c>part</c> feature
    ///     usage (<c>markdownFormatter</c>) typed by a leaf <c>part def</c>. <c>FindRoot</c> selects
    ///     no root (<c>PublishingSubsystem</c> is excluded from self-matching by the
    ///     <c>NamespaceDirectChildren</c> recursion kind), so before this fix the diagram rendered
    ///     as a totally empty canvas. Now the exposed direct-child feature renders directly as a
    ///     single boxless leaf node, with no <c>PublishingSubsystem</c>-labeled frame anywhere.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_ExposeNamespaceDirectChildren_NoRootDef_RendersTopLevelFeatureWithoutFrame()
    {
        // Arrange: PublishingSubsystem::markdownFormatter : MarkdownFormatter (a leaf part def),
        // exposed via "expose PublishingSubsystem::*;" (NamespaceDirectChildren, no trailing "::**").
        var strategy = new InterconnectionViewLayoutStrategy();
        var publishingSubsystem = new SysmlDefinitionNode
        {
            Name = "PublishingSubsystem",
            QualifiedName = "M::PublishingSubsystem",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode
                {
                    Name = "markdownFormatter",
                    QualifiedName = "M::PublishingSubsystem::markdownFormatter",
                    FeatureKeyword = "part",
                    FeatureTyping = "MarkdownFormatter"
                }
            ]
        };
        var markdownFormatter = new SysmlDefinitionNode
        {
            Name = "MarkdownFormatter",
            QualifiedName = "M::MarkdownFormatter",
            DefinitionKeyword = "part def"
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["M::PublishingSubsystem"] = publishingSubsystem,
                ["M::PublishingSubsystem::markdownFormatter"] = publishingSubsystem.Children[0],
                ["M::MarkdownFormatter"] = markdownFormatter
            }
        };
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "M::V",
            ExposeMembers = [new ExposeMember("PublishingSubsystem", null, ExposeRecursionKind.NamespaceDirectChildren)],
            ResolvedEdges = [new SysmlEdge("M::V", "M::PublishingSubsystem", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: exactly one box overall (no PublishingSubsystem frame anywhere), a leaf/interior
        // rounded box labeled "markdownFormatter : MarkdownFormatter" with keyword "part".
        var boxes = CollectBoxes(layout.Nodes).ToList();
        var box = Assert.Single(boxes);
        Assert.Equal("part", box.Keyword);
        Assert.Contains("markdownFormatter", box.Label, StringComparison.Ordinal);
        Assert.Equal(BoxShape.RoundedRectangle, box.Shape);
        Assert.DoesNotContain(boxes, b => b.Label is not null && b.Label.Contains("PublishingSubsystem", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Two independent top-level <c>part</c> feature usages that are both direct children of a
    ///     common exposed namespace render as two separate boxless nodes placed side by side (via
    ///     the shared <c>LayeredPlacement.PlaceWithPorts</c> containment-packing algorithm), with no
    ///     wrapping frame and no overlap between them.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_ExposeNamespaceDirectChildren_TwoTopLevelParts_ArrangesSideBySideNoFrame()
    {
        // Arrange: NsRoot::widgetA and NsRoot::widgetB, each typed by an unrelated leaf part def,
        // exposed via "expose NsRoot::*;" (NamespaceDirectChildren).
        var strategy = new InterconnectionViewLayoutStrategy();
        var workspace = BuildTwoTopLevelPartsWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "M::V",
            ExposeMembers = [new ExposeMember("NsRoot", null, ExposeRecursionKind.NamespaceDirectChildren)],
            ResolvedEdges = [new SysmlEdge("M::V", "NsRoot", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: exactly two top-level boxes (no wrapping frame), and they do not overlap.
        Assert.Equal(2, layout.Nodes.Count);
        var boxes = layout.Nodes.OfType<LayoutBox>().ToList();
        Assert.Equal(2, boxes.Count);
        Assert.False(Overlaps(boxes[0], boxes[1]), "the two top-level boxes overlap");
    }

    /// <summary>
    ///     When the exposed namespace's direct-child part is itself typed by a <c>part def</c> that
    ///     has its own nested parts, the resolved scope here is purely non-recursive
    ///     (<c>NamespaceDirectChildren</c>, i.e. <c>expose NsRoot::*;</c>, with no recursive subject),
    ///     so <c>HasUnlimitedRecursion</c> is <see langword="false"/> and interior expansion must stop
    ///     at depth 0: the single top-level box renders as an intrinsic-sized leaf with no nested part
    ///     boxes of its own, even though its type (<c>Motherboard</c>) has its own <c>cpu</c>/
    ///     <c>chipset</c> parts. This also proves the boxless top-level fallback
    ///     (<c>CollectTopLevelScopedParts</c>) shares the exact same depth-limiting gate as the normal
    ///     container-rooted path, since both funnel through the same <c>BuildPartItem</c>.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_ExposeNamespaceDirectChildren_ContainerFeature_RendersAsLeaf()
    {
        // Arrange: NsRoot::board : Motherboard { cpu, chipset, connect cpu to chipset } — two levels
        // deep — exposed via "expose NsRoot::*;" (NamespaceDirectChildren, non-recursive).
        var strategy = new InterconnectionViewLayoutStrategy();
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
        var board = new SysmlFeatureNode
        {
            Name = "board",
            QualifiedName = "NsRoot::board",
            FeatureKeyword = "part",
            FeatureTyping = "Motherboard"
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["NsRoot::board"] = board,
                ["M::Motherboard"] = motherboard
            }
        };
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "M::V",
            ExposeMembers = [new ExposeMember("NsRoot", null, ExposeRecursionKind.NamespaceDirectChildren)],
            ResolvedEdges = [new SysmlEdge("M::V", "NsRoot", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: exactly one top-level box (no frame), and it carries no nested part boxes — the
        // non-recursive scope limits expansion to depth 0, so board's own cpu/chipset interior is
        // never drawn.
        var topBox = Assert.Single(layout.Nodes.OfType<LayoutBox>());
        Assert.Contains("board", topBox.Label, StringComparison.Ordinal);
        var nestedBoxes = CollectBoxes(topBox.Children).ToList();
        Assert.Empty(nestedBoxes);
    }

    /// <summary>
    ///     Pins down the preserved existing empty-canvas fallback (requirement: no regression): when
    ///     the resolved <c>expose</c> scope matches no <c>part</c> feature at all (its only member
    ///     is a non-part feature), <c>FindRoot</c> still returns no root, and
    ///     <c>CollectTopLevelScopedParts</c> returns an empty list, so the diagram falls back to the
    ///     unchanged minimal canvas rather than the new boxless-nodes path.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_ExposeNamespaceDirectChildren_NoMatchingFeature_ReturnsMinimalCanvas()
    {
        // Arrange: NsRoot::onlyAttribute is an "attribute" feature, not a "part" — no part
        // feature is a direct child of the exposed namespace.
        var strategy = new InterconnectionViewLayoutStrategy();
        var onlyAttribute = new SysmlFeatureNode
        {
            Name = "onlyAttribute",
            QualifiedName = "NsRoot::onlyAttribute",
            FeatureKeyword = "attribute",
            FeatureTyping = "String"
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["NsRoot::onlyAttribute"] = onlyAttribute }
        };
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "M::V",
            ExposeMembers = [new ExposeMember("NsRoot", null, ExposeRecursionKind.NamespaceDirectChildren)],
            ResolvedEdges = [new SysmlEdge("M::V", "NsRoot", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: unchanged minimal-canvas fallback.
        Assert.Empty(layout.Nodes);
    }

    /// <summary>
    ///     A connector declared between two top-level scoped part features is still drawn when the
    ///     connector's own qualified name is itself in scope and both endpoints resolve into the
    ///     top-level part set (requirement: "if a connector is itself resolvable and in scope, draw
    ///     it").
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_ExposeNamespaceDirectChildren_ConnectionBetweenTopLevelFeatures_DrawsEdge()
    {
        // Arrange: NsRoot { widgetA, widgetB, connect widgetA to widgetB } — the connection's own
        // qualified name is a direct child of NsRoot, so it too satisfies the NamespaceDirectChildren
        // scope alongside its two endpoints.
        var strategy = new InterconnectionViewLayoutStrategy();
        var workspace = BuildTwoTopLevelPartsWithConnectionWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "M::V",
            ExposeMembers = [new ExposeMember("NsRoot", null, ExposeRecursionKind.NamespaceDirectChildren)],
            ResolvedEdges = [new SysmlEdge("M::V", "NsRoot", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: two top-level boxes, connected by exactly one connector line.
        Assert.Equal(2, layout.Nodes.OfType<LayoutBox>().Count());
        Assert.Single(CollectLines(layout.Nodes));
    }

    /// <summary>
    ///     A matched feature whose own qualified name is itself nested (<c>"::"</c>-prefixed) under
    ///     another matched feature's qualified name is excluded from the top-level set — it is
    ///     already reachable as that ancestor's own nested content, so it must not also be
    ///     duplicated as its own separate top-level node.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_ExposeNamespaceDirectChildren_NestedTopLevelFeatureExcluded_NotDuplicated()
    {
        // Arrange: an exotic scope shape where both "NsRoot::widgetA" (the exposed subject itself,
        // MembershipRecursive so self-matches) and "NsRoot::widgetA::sub" (nested under it by
        // qualified-name prefix alone) satisfy IsInSubjectScope for the same subject.
        var strategy = new InterconnectionViewLayoutStrategy();
        var widgetA = new SysmlFeatureNode
        {
            Name = "widgetA",
            QualifiedName = "NsRoot::widgetA",
            FeatureKeyword = "part",
            FeatureTyping = "WidgetA"
        };
        var sub = new SysmlFeatureNode
        {
            Name = "sub",
            QualifiedName = "NsRoot::widgetA::sub",
            FeatureKeyword = "part",
            FeatureTyping = "SubPart"
        };
        var widgetADef = new SysmlDefinitionNode { Name = "WidgetA", QualifiedName = "M::WidgetA", DefinitionKeyword = "part def" };
        var subPartDef = new SysmlDefinitionNode { Name = "SubPart", QualifiedName = "M::SubPart", DefinitionKeyword = "part def" };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["NsRoot::widgetA"] = widgetA,
                ["NsRoot::widgetA::sub"] = sub,
                ["M::WidgetA"] = widgetADef,
                ["M::SubPart"] = subPartDef
            }
        };
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "M::V",
            ExposeMembers = [new ExposeMember("widgetA", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("M::V", "NsRoot::widgetA", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: only the outer (ancestor) feature appears as a top-level box; "sub" is not
        // duplicated as its own separate top-level node.
        var topBoxes = layout.Nodes.OfType<LayoutBox>().ToList();
        var topBox = Assert.Single(topBoxes);
        Assert.Contains("widgetA", topBox.Label, StringComparison.Ordinal);
        Assert.DoesNotContain("sub", topBox.Label, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Builds a workspace with two independent top-level parts (<c>NsRoot::widgetA</c> and
    ///     <c>NsRoot::widgetB</c>), each typed by its own unrelated leaf <c>part def</c>, and no
    ///     connection between them.
    /// </summary>
    private static SysmlWorkspace BuildTwoTopLevelPartsWorkspace()
    {
        var widgetA = new SysmlFeatureNode { Name = "widgetA", QualifiedName = "NsRoot::widgetA", FeatureKeyword = "part", FeatureTyping = "WidgetA" };
        var widgetB = new SysmlFeatureNode { Name = "widgetB", QualifiedName = "NsRoot::widgetB", FeatureKeyword = "part", FeatureTyping = "WidgetB" };
        var widgetADef = new SysmlDefinitionNode { Name = "WidgetA", QualifiedName = "M::WidgetA", DefinitionKeyword = "part def" };
        var widgetBDef = new SysmlDefinitionNode { Name = "WidgetB", QualifiedName = "M::WidgetB", DefinitionKeyword = "part def" };
        return new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["NsRoot::widgetA"] = widgetA,
                ["NsRoot::widgetB"] = widgetB,
                ["M::WidgetA"] = widgetADef,
                ["M::WidgetB"] = widgetBDef
            }
        };
    }

    /// <summary>
    ///     Builds the same two-top-level-parts shape as <see cref="BuildTwoTopLevelPartsWorkspace"/>,
    ///     but with both features and a connector between them nested as children of a common
    ///     <c>NsRoot</c> <c>part def</c> container — so the connector's own qualified name
    ///     (<c>NsRoot::conn1</c>) is itself a direct child of the exposed namespace, and the
    ///     container is scanned by <c>ResolveTopLevelConnections</c> via <c>BuildDefinitionIndex</c>
    ///     without itself qualifying as the diagram's single root (excluded from self-matching by
    ///     the <c>NamespaceDirectChildren</c> recursion kind, same as <c>PublishingSubsystem</c>
    ///     above).
    /// </summary>
    private static SysmlWorkspace BuildTwoTopLevelPartsWithConnectionWorkspace()
    {
        var widgetADef = new SysmlDefinitionNode { Name = "WidgetA", QualifiedName = "M::WidgetA", DefinitionKeyword = "part def" };
        var widgetBDef = new SysmlDefinitionNode { Name = "WidgetB", QualifiedName = "M::WidgetB", DefinitionKeyword = "part def" };
        var nsRoot = new SysmlDefinitionNode
        {
            Name = "NsRoot",
            QualifiedName = "NsRoot",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "widgetA", QualifiedName = "NsRoot::widgetA", FeatureKeyword = "part", FeatureTyping = "WidgetA" },
                new SysmlFeatureNode { Name = "widgetB", QualifiedName = "NsRoot::widgetB", FeatureKeyword = "part", FeatureTyping = "WidgetB" },
                new SysmlConnectionNode
                {
                    Name = "conn1",
                    QualifiedName = "NsRoot::conn1",
                    ConnectionKeyword = "connection",
                    EndpointA = "widgetA",
                    EndpointB = "widgetB"
                }
            ]
        };
        return new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["NsRoot"] = nsRoot,
                ["NsRoot::widgetA"] = nsRoot.Children[0],
                ["NsRoot::widgetB"] = nsRoot.Children[1],
                ["NsRoot::conn1"] = nsRoot.Children[2],
                ["M::WidgetA"] = widgetADef,
                ["M::WidgetB"] = widgetBDef
            }
        };
    }

    /// <summary>
    ///     Regression test for the owner-scoped top-level connection resolution fix: two different
    ///     containing <c>part def</c>s (<c>SubsystemA</c>, <c>SubsystemB</c>) each declare their own
    ///     <c>logger</c>-simple-named part feature, and both subsystems' own direct children are
    ///     exposed as separate top-level scoped features (via one <c>NamespaceDirectChildren</c>
    ///     subject per subsystem, so neither subsystem itself qualifies as the diagram's single
    ///     root — see <see cref="InterconnectionView_BuildLayout_ExposeNamespaceDirectChildren_ConnectionBetweenTopLevelFeatures_DrawsEdge"/>
    ///     for the same self-exclusion mechanism). <c>SubsystemA</c> also declares a connection
    ///     between its own <c>logger</c> and <c>sensor</c> parts. <c>SubsystemB</c>'s own <c>logger</c>
    ///     is declared first in <see cref="SysmlWorkspace.Declarations"/> enumeration order, so a flat
    ///     simple-name index built across all four collected top-level parts (the pre-fix
    ///     <c>BuildPartIndex</c>-of-everything approach) would resolve <c>SubsystemA</c>'s connection
    ///     endpoint <c>"logger"</c> to <c>SubsystemB</c>'s <c>logger</c> instead of its own — a bogus
    ///     cross-owner connector. The fix must resolve the connection against only <c>SubsystemA</c>'s
    ///     own restricted index, drawing exactly one connector, between <c>SubsystemA</c>'s own
    ///     <c>logger</c> and <c>sensor</c> boxes.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_TopLevelNameCollisionAcrossOwners_ResolvesOwnConnection()
    {
        // Arrange: SubsystemB { logger : LoggerB, other : OtherB } declared first, then
        // SubsystemA { logger : LoggerA, sensor : SensorA, connect logger to sensor }. Both exposed
        // via "expose SubsystemA::*;" and "expose SubsystemB::*;" (NamespaceDirectChildren), so
        // neither subsystem def itself is root-relevant (self-excluded by that recursion kind) and
        // FindRoot selects no root, falling to the boxless top-level path.
        var strategy = new InterconnectionViewLayoutStrategy();
        var subsystemB = new SysmlDefinitionNode
        {
            Name = "SubsystemB",
            QualifiedName = "SubsystemB",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "logger", QualifiedName = "SubsystemB::logger", FeatureKeyword = "part", FeatureTyping = "LoggerB" },
                new SysmlFeatureNode { Name = "other", QualifiedName = "SubsystemB::other", FeatureKeyword = "part", FeatureTyping = "OtherB" },
                new SysmlConnectionNode
                {
                    Name = "connB",
                    QualifiedName = "SubsystemB::connB",
                    ConnectionKeyword = "connection",
                    EndpointA = "logger",
                    EndpointB = "other"
                }
            ]
        };
        var subsystemA = new SysmlDefinitionNode
        {
            Name = "SubsystemA",
            QualifiedName = "SubsystemA",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "logger", QualifiedName = "SubsystemA::logger", FeatureKeyword = "part", FeatureTyping = "LoggerA" },
                new SysmlFeatureNode { Name = "sensor", QualifiedName = "SubsystemA::sensor", FeatureKeyword = "part", FeatureTyping = "SensorA" },
                new SysmlConnectionNode
                {
                    Name = "conn1",
                    QualifiedName = "SubsystemA::conn1",
                    ConnectionKeyword = "connection",
                    EndpointA = "logger",
                    EndpointB = "sensor"
                }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["SubsystemB"] = subsystemB,
                ["SubsystemB::logger"] = subsystemB.Children[0],
                ["SubsystemB::other"] = subsystemB.Children[1],
                ["SubsystemA"] = subsystemA,
                ["SubsystemA::logger"] = subsystemA.Children[0],
                ["SubsystemA::sensor"] = subsystemA.Children[1],
                ["SubsystemA::conn1"] = subsystemA.Children[2]
            }
        };
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "M::V",
            ExposeMembers =
            [
                new ExposeMember("SubsystemA", null, ExposeRecursionKind.NamespaceDirectChildren),
                new ExposeMember("SubsystemB", null, ExposeRecursionKind.NamespaceDirectChildren)
            ],
            ResolvedEdges =
            [
                new SysmlEdge("M::V", "SubsystemA", SysmlEdgeKind.Expose),
                new SysmlEdge("M::V", "SubsystemB", SysmlEdgeKind.Expose)
            ]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: four top-level boxes (both subsystems' own two parts), exactly two connectors (one
        // per subsystem's own connection), and SubsystemA's connector connects SubsystemA's own
        // logger and sensor boxes — not SubsystemB's logger box.
        Assert.Equal(4, layout.Nodes.OfType<LayoutBox>().Count());
        var lines = CollectLines(layout.Nodes).ToList();
        Assert.Equal(2, lines.Count);

        var loggerABox = FindPartBox(layout, "logger : LoggerA");
        var sensorABox = FindPartBox(layout, "sensor : SensorA");
        var loggerBBox = FindPartBox(layout, "logger : LoggerB");
        var otherBBox = FindPartBox(layout, "other : OtherB");

        // The connector linking SubsystemA's own logger and sensor boxes must exist, and must not
        // instead link SubsystemB's logger box (the pre-fix bogus cross-owner connection).
        var subsystemAConnector = lines.SingleOrDefault(l => LineTouchesBox(l, loggerABox) && LineTouchesBox(l, sensorABox));
        Assert.NotNull(subsystemAConnector);
        Assert.False(LineTouchesBox(subsystemAConnector, loggerBBox), "connector must not also touch SubsystemB's logger box");
        Assert.False(LineTouchesBox(subsystemAConnector, otherBBox), "connector must not also touch SubsystemB's other box");
    }

    /// <summary>
    ///     Determines whether either end of <paramref name="line"/>'s waypoints falls within
    ///     <paramref name="box"/>'s bounds (with a small tolerance for port placement on the box's
    ///     own border), used to identify which rendered box a connector actually terminates at.
    /// </summary>
    private static bool LineTouchesBox(LayoutLine line, LayoutBox box, double tolerance = 5.0) =>
        line.Waypoints.Any(p =>
            p.X >= box.X - tolerance && p.X <= box.X + box.Width + tolerance &&
            p.Y >= box.Y - tolerance && p.Y <= box.Y + box.Height + tolerance);
}
