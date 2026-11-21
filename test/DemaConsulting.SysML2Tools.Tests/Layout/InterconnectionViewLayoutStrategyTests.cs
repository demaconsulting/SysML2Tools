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
    ///     rounded part box per nested part and one connector line per connection; an endpoint that
    ///     names only a part draws no port glyph.
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

        // Assert: one container box, two part boxes, one connector line, and no port glyphs — the
        // endpoints name the parts themselves, not a port, so the connector attaches to the part
        var boxes = CollectBoxes(layout.Nodes).ToList();
        Assert.Contains(boxes, b => b.Keyword == "part def" && b.Label == "PowerSystem");
        Assert.Equal(2, boxes.Count(b => b.Shape == BoxShape.RoundedRectangle));
        Assert.Empty(CollectPorts(layout.Nodes));
        Assert.Single(CollectLines(layout.Nodes));
    }

    /// <summary>
    ///     Documentation annotations on the root part definition and its part types are never drawn
    ///     by the interconnection view: no box label or compartment row contains the doc text.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_DocumentedDefinitions_DrawNoDocumentation()
    {
        // Arrange: a documented part def with one documented nested part type
        var strategy = new InterconnectionViewLayoutStrategy();
        var engine = new SysmlDefinitionNode
        {
            Name = "Engine",
            QualifiedName = "M::Engine",
            DefinitionKeyword = "part def",
            Annotations = [new SysmlAnnotation(SysmlAnnotationKind.Documentation, "Engine documentation text.")],
        };
        var powerSystem = new SysmlDefinitionNode
        {
            Name = "PowerSystem",
            QualifiedName = "M::PowerSystem",
            DefinitionKeyword = "part def",
            Annotations = [new SysmlAnnotation(SysmlAnnotationKind.Documentation, "PowerSystem documentation text.")],
            Children =
            [
                new SysmlFeatureNode { Name = "engine", QualifiedName = "M::PowerSystem::engine", FeatureKeyword = "part", FeatureTyping = "Engine" },
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["M::Engine"] = engine,
                ["M::PowerSystem"] = powerSystem,
            }
        };
        var context = new ViewContext("PowerSystemInterconnectionView", workspace);

        // Act
        var layout = strategy.BuildLayout(context, new RenderOptions(Themes.Light));

        // Assert: boxes are drawn, but no label or compartment row carries documentation text
        var boxes = CollectBoxes(layout.Nodes).ToList();
        Assert.NotEmpty(boxes);
        Assert.DoesNotContain(boxes, b =>
            (b.Label?.Contains("documentation text") ?? false) ||
            b.Compartments.Any(c => c.Title == "doc" || c.Rows.Any(r => r.Contains("documentation text"))));
        Assert.DoesNotContain(boxes, b => b.Shape == BoxShape.Note);
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

        // Assert: the root box's own Children hold the interior content — two part boxes and one
        // connector line (no port glyphs for part-only endpoints) — none of it as flat top-level siblings.
        Assert.NotEmpty(rootBox.Children);
        Assert.Equal(2, rootBox.Children.OfType<LayoutBox>().Count(b => b.Shape == BoxShape.RoundedRectangle));
        Assert.Empty(rootBox.Children.OfType<LayoutPort>());
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
    ///     No left/right port's center falls within its owning part box's own title area. This
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

        // Assert: every left/right port's center sits at or below its owning box's title area, never
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
                port.CentreY >= owningBox.Y + titleArea,
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

        // Assert: two part boxes, and no port glyphs because the endpoints name only the parts.
        Assert.Equal(2, CollectBoxes(layout.Nodes).Count(b => b.Shape == BoxShape.RoundedRectangle));
        Assert.Empty(CollectPorts(layout.Nodes));
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

        // The "psu" end names only the part, so it has no port; the "board.cpu" end keeps its label.
        var ports = rootBox.Children.OfType<LayoutPort>().ToList();
        var port = Assert.Single(ports);
        Assert.Equal("cpu", port.ExternalLabel);
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
    ///     A container part's box is at least as wide as its own formatted label (short name, type,
    ///     multiplicity) even when the child definition's contents are narrower, so the label never
    ///     overflows the rounded box.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_ContainerWithLongLabel_IsAtLeastLabelWide()
    {
        // Arrange: Computer { part <longShortName> board : Motherboard [1..1000000]; ... } where the
        // Motherboard interior (cpu/chipset) is far narrower than the formatted label.
        var strategy = new InterconnectionViewLayoutStrategy();
        var workspace = BuildNestedWorkspace();
        var computer = (SysmlDefinitionNode)workspace.Declarations["M::Computer"];
        var board = (SysmlFeatureNode)computer.Children[0];
        const string expectedLabel = "<aVeryLongShortNameForTheMainBoard> board : Motherboard [1..1000000]";
        var longComputer = new SysmlDefinitionNode
        {
            Name = computer.Name,
            QualifiedName = computer.QualifiedName,
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode
                {
                    Name = board.Name,
                    ShortName = "aVeryLongShortNameForTheMainBoard",
                    QualifiedName = board.QualifiedName,
                    FeatureKeyword = "part",
                    FeatureTyping = "Motherboard",
                    Multiplicity = "[1..1000000]"
                },
                .. computer.Children.Skip(1)
            ]
        };
        workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["M::Computer"] = longComputer,
                ["M::Motherboard"] = workspace.Declarations["M::Motherboard"]
            }
        };
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(new ViewContext("ComputerInterconnectionView", workspace), options);

        // Assert: container (has children) is at least label-wide (0.62 is the strategy's per-character estimate)
        var boardBox = FindPartBox(layout, expectedLabel);
        Assert.NotEmpty(boardBox.Children);
        var labelWidth = (expectedLabel.Length * options.Theme.FontSizeTitle * 0.62) + (2.0 * options.Theme.LabelPadding);
        Assert.True(boardBox.Width >= labelWidth, $"container width {boardBox.Width} must be >= label width {labelWidth}");
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
    ///     A part usage's box label includes its declared short name and multiplicity
    ///     (<c>&lt;r&gt; ram : Memory [2]</c>), and the box is wide enough for the longer label; a
    ///     plain part keeps the unchanged <c>name : Type</c> form.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_PartWithShortNameAndMultiplicity_ShowsThemInLabel()
    {
        // Arrange: Sys { part cpu : Cpu; part <r> ram : Memory [2]; connect cpu to ram }
        var strategy = new InterconnectionViewLayoutStrategy();
        var root = new SysmlDefinitionNode
        {
            Name = "Sys",
            QualifiedName = "M::Sys",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "cpu", QualifiedName = "M::Sys::cpu", FeatureKeyword = "part", FeatureTyping = "Cpu" },
                new SysmlFeatureNode
                {
                    Name = "ram",
                    ShortName = "r",
                    QualifiedName = "M::Sys::ram",
                    FeatureKeyword = "part",
                    FeatureTyping = "Memory",
                    Multiplicity = "[2]"
                },
                new SysmlConnectionNode { ConnectionKeyword = "connection", EndpointA = "cpu", EndpointB = "ram" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["M::Sys"] = root,
                ["M::Cpu"] = new SysmlDefinitionNode { Name = "Cpu", QualifiedName = "M::Cpu", DefinitionKeyword = "part def" },
                ["M::Memory"] = new SysmlDefinitionNode { Name = "Memory", QualifiedName = "M::Memory", DefinitionKeyword = "part def" }
            }
        };
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(new ViewContext("Interconnection", workspace), options);

        // Assert: labels follow the specification's name-line form and the connector still resolved
        var partBoxes = CollectBoxes(layout.Nodes).Where(b => b.Shape == BoxShape.RoundedRectangle).ToList();
        Assert.Contains(partBoxes, b => b.Label == "cpu : Cpu");
        var ram = Assert.Single(partBoxes, b => b.Label == "<r> ram : Memory [2]");
        Assert.Single(CollectLines(layout.Nodes));

        // Assert: the box is sized to its (longer) label
        var theme = options.Theme;
        var minWidth = ("<r> ram : Memory [2]".Length * theme.FontSizeTitle * 0.62) + (2.0 * theme.LabelPadding);
        Assert.True(ram.Width >= minWidth - 1e-6, $"box width {ram.Width} must fit its label ({minWidth}).");
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

    /// <summary>
    ///     Each declared port direction maps to the matching direction arrow: <c>in</c>, <c>out</c>,
    ///     <c>inout</c> and an undirected port, all drawn as outlined squares labeled outside the part.
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_DeclaredPorts_DirectionAppearanceMatchesInOutInout()
    {
        // Arrange: a part with a port of every direction, each wired to a peer port
        var layout = await LayOutModelAsync("""
            package Q {
                port def P;
                part def Src { in port inA : P; out port outA : P; inout port bothA : P; port plainA : P; }
                part def Dst { out port sourceA : P; in port sinkB : P; inout port bothB : P; port plainB : P; }
                part def Rig {
                    part src : Src; part dst : Dst;
                    connection l1 connect dst.sourceA to src.inA;
                    connection l2 connect src.outA to dst.sinkB;
                    connection l3 connect src.bothA to dst.bothB;
                    connection l4 connect src.plainA to dst.plainB;
                }
            }
            """);

        // Act
        var ports = CollectPorts(layout.Nodes);

        // Assert: direction per declared port, outlined glyph, outside label
        Assert.Equal(PortDirection.In, PortNamed(ports, "inA : P").Appearance!.Direction);
        Assert.Equal(PortDirection.Out, PortNamed(ports, "outA : P").Appearance!.Direction);
        Assert.Equal(PortDirection.InOut, PortNamed(ports, "bothA : P").Appearance!.Direction);
        Assert.Equal(PortDirection.None, PortNamed(ports, "plainA : P").Appearance!.Direction);
        Assert.Equal(8, ports.Count);
        Assert.All(ports, p =>
        {
            Assert.Equal(PortGlyph.Outlined, p.Appearance!.Glyph);
            Assert.Equal(PortLabelPlacement.Outward, p.Appearance.LabelPlacement);
        });
    }

    /// <summary>A reference port is drawn with a dashed outline; an ordinary port keeps a solid one.</summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_ReferencePort_HasDashedOutline()
    {
        // Arrange
        var layout = await LayOutModelAsync("""
            package Q {
                port def P;
                part def A { ref port rp : P; port sp : P; }
                part def B { port bp : P; port bq : P; }
                part def Rig { part a : A; part b : B; connection c1 connect a.rp to b.bp; connection c2 connect a.sp to b.bq; }
            }
            """);

        // Act
        var ports = CollectPorts(layout.Nodes);

        // Assert
        Assert.Equal(LineStyle.Dashed, PortNamed(ports, "rp : P").Appearance!.OutlineStyle);
        Assert.Equal(LineStyle.Solid, PortNamed(ports, "sp : P").Appearance!.OutlineStyle);
    }

    /// <summary>A port typed by a conjugated port definition is labeled <c>name : ~Type</c>.</summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_ConjugatedPort_LabelShowsTildeType()
    {
        // Arrange
        var layout = await LayOutModelAsync("""
            package Q {
                port def P;
                part def A { out port po : P; }
                part def B { in port pi : ~P; }
                part def Rig { part a : A; part b : B; connection c connect a.po to b.pi; }
            }
            """);

        // Act
        var ports = CollectPorts(layout.Nodes);

        // Assert
        Assert.Contains(ports, p => p.ExternalLabel == "po : P");
        Assert.Contains(ports, p => p.ExternalLabel == "pi : ~P");
    }

    /// <summary>A port label is <c>name : Type</c>, with the multiplicity when the port declares one.</summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_PortLabel_IsNameColonTypeWithMultiplicity()
    {
        // Arrange
        var layout = await LayOutModelAsync("""
            package Q {
                port def P;
                part def A { port pa : P [2]; }
                part def B { port pb : P; }
                part def Rig { part a : A; part b : B; connection c connect a.pa to b.pb; }
            }
            """);

        // Act
        var ports = CollectPorts(layout.Nodes);

        // Assert
        Assert.Contains(ports, p => p.ExternalLabel == "pa : P [2]");
        Assert.Contains(ports, p => p.ExternalLabel == "pb : P");
    }

    /// <summary>
    ///     An endpoint segment that no declaration backs keeps its bare name as the label and is drawn
    ///     as an undirected outlined square labeled outside — never the legacy filled square.
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_UndeclaredEndpointSegment_KeepsBareNameLabelWithOutlinedGlyph()
    {
        // Arrange: no part declares a port
        var layout = await LayOutModelAsync("""
            package Q {
                part def A; part def B;
                part def Rig { part a : A; part b : B; connection c connect a.x to b.y; }
            }
            """);

        // Act
        var ports = CollectPorts(layout.Nodes);

        // Assert
        Assert.Equal(2, ports.Count);
        Assert.All(ports, p =>
        {
            Assert.Equal(PortDirection.None, p.Appearance!.Direction);
            Assert.Equal(PortGlyph.Outlined, p.Appearance.Glyph);
            Assert.Equal(PortLabelPlacement.Outward, p.Appearance.LabelPlacement);
            Assert.Equal(LineStyle.Solid, p.Appearance.OutlineStyle);
        });
        Assert.Contains(ports, p => p.ExternalLabel == "x");
        Assert.Contains(ports, p => p.ExternalLabel == "y");
    }

    /// <summary>
    ///     A part's ports come from its own body, from its type, and from the supertypes of that type; a
    ///     port redeclared on a more specific element is listed once.
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_InheritedTypeAndInlinePorts_AreResolved()
    {
        // Arrange: inherited (from Base), own (on Derived), inline (in the usage body)
        var layout = await LayOutModelAsync("""
            package Q {
                port def P;
                part def Base { in port inherited : P; }
                part def Derived :> Base { out port own : P; }
                part def Peer { port x : P; port y : P; port z : P; }
                part def Rig {
                    part d : Derived { inout port inline : P; }
                    part e : Peer;
                    connection c1 connect d.inherited to e.x;
                    connection c2 connect d.own to e.y;
                    connection c3 connect d.inline to e.z;
                }
            }
            """);

        // Act
        var ports = CollectPorts(layout.Nodes);

        // Assert: each is resolved with its declared direction and typed label
        Assert.Equal(PortDirection.In, PortNamed(ports, "inherited : P").Appearance!.Direction);
        Assert.Equal(PortDirection.Out, PortNamed(ports, "own : P").Appearance!.Direction);
        Assert.Equal(PortDirection.InOut, PortNamed(ports, "inline : P").Appearance!.Direction);
    }

    /// <summary>
    ///     A declared port that no connector names is drawn anyway, on the border of its part, clear of
    ///     every other port, label, and box, with its label inside the canvas.
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_UnconnectedDeclaredPort_IsDrawnOnBoxBorderWithoutOverlap()
    {
        // Arrange: spare ports next to a connected one on both faces
        var layout = await LayOutModelAsync("""
            package Q {
                port def P;
                part def A { out port used : P; port spareLeft : P; out port spareRight : P; in port spareIn : P; }
                part def B { in port sink : P; }
                part def Rig { part a : A; part b : B; connection c connect a.used to b.sink; }
            }
            """);

        // Act
        var ports = CollectPorts(layout.Nodes);
        var partA = FindPartBox(layout, "a : A");

        // Assert: all four ports of A are drawn on its border at or below the title area
        var titleArea = BoxMetrics.TitleAreaHeight(Themes.Light, hasLabel: true, hasKeyword: true);
        var aPorts = ports.Where(p => p.ExternalLabel is "used : P" or "spareLeft : P" or "spareRight : P" or "spareIn : P").ToList();
        Assert.Equal(4, aPorts.Count);
        foreach (var port in aPorts)
        {
            var onBorder = Math.Abs(port.CentreX - partA.X) < 0.5 || Math.Abs(port.CentreX - (partA.X + partA.Width)) < 0.5;
            Assert.True(onBorder, $"{port.ExternalLabel} is not on the border of its part");
            Assert.True(port.CentreY >= partA.Y + titleArea && port.CentreY <= partA.Y + partA.Height, $"{port.ExternalLabel} is outside the part's port area");
        }

        // Assert: the out port went to the right face, the in and undirected ports to the left face
        Assert.Equal(PortSide.Right, PortNamed(ports, "spareRight : P").Side);
        Assert.Equal(PortSide.Left, PortNamed(ports, "spareLeft : P").Side);
        Assert.Equal(PortSide.Left, PortNamed(ports, "spareIn : P").Side);

        // Assert: no glyph or label of any port overlaps another port's, a box's, or leaves the canvas
        AssertPortsClear(layout, ports);
    }

    /// <summary>
    ///     A declared port used by two connectors is one port: its label appears once even though the
    ///     layout engine emits a glyph per connector end.
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_PortWithTwoConnections_LabelAppearsOnce()
    {
        // Arrange: a.shared feeds both b and c
        var layout = await LayOutModelAsync("""
            package Q {
                port def P;
                part def A { out port shared : P; }
                part def B { in port bi : P; }
                part def C { in port ci : P; }
                part def Rig {
                    part a : A; part b : B; part c : C;
                    connection c1 connect a.shared to b.bi;
                    connection c2 connect a.shared to c.ci;
                }
            }
            """);

        // Act
        var ports = CollectPorts(layout.Nodes);

        // Assert
        Assert.Single(ports, p => p.ExternalLabel == "shared : P");
        Assert.Equal(2, CollectLines(layout.Nodes).Count);
    }

    /// <summary>
    ///     A part with a shared declared port (one port, two connectors; its repeated glyph carries no
    ///     label) and an unconnected declared port lays out without throwing, and the unconnected port is
    ///     placed clear of the shared port's glyphs and its single label.
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_SharedPortAndUnconnectedPort_LaysOutClear()
    {
        // Arrange: a.shared feeds both b and c; a.spare is declared but never connected
        var layout = await LayOutModelAsync("""
            package Q {
                port def P;
                part def A { out port shared : P; out port spare : P; }
                part def B { in port bi : P; }
                part def C { in port ci : P; }
                part def Rig {
                    part a : A; part b : B; part c : C;
                    connection c1 connect a.shared to b.bi;
                    connection c2 connect a.shared to c.ci;
                }
            }
            """);

        // Act
        var ports = CollectPorts(layout.Nodes);

        // Assert: the shared port is labeled once, the spare port is drawn, and nothing overlaps
        Assert.Single(ports, p => p.ExternalLabel == "shared : P");
        Assert.Equal(5, ports.Count);
        var spare = PortNamed(ports, "spare : P");
        Assert.Equal(PortSide.Right, spare.Side);
        AssertPortsClear(layout, ports);
    }

    /// <summary>
    ///     The declared ports of the root definition are drawn on the frame's border (left face, or right
    ///     face for an <c>out</c> port), below its header, and the canvas makes room for their labels.
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_RootFramePorts_DrawnOnFrameBorder()
    {
        // Arrange
        var layout = await LayOutModelAsync("""
            package Q {
                port def MainsPort;
                part def A; part def B;
                part def Rig {
                    in port mains : MainsPort;
                    out port video : MainsPort;
                    part a : A; part b : B;
                    connection c connect a to b;
                }
            }
            """);

        // Act
        var frame = Assert.IsType<LayoutBox>(Assert.Single(layout.Nodes));
        var framePorts = frame.Children.OfType<LayoutPort>().ToList();

        // Assert: both ports sit on the frame border and below its header
        var mains = PortNamed(framePorts, "mains : MainsPort");
        var video = PortNamed(framePorts, "video : MainsPort");
        Assert.Equal(PortSide.Left, mains.Side);
        Assert.Equal(frame.X, mains.CentreX, 0.5);
        Assert.Equal(PortSide.Right, video.Side);
        Assert.Equal(frame.X + frame.Width, video.CentreX, 0.5);
        Assert.Equal(PortDirection.In, mains.Appearance!.Direction);
        Assert.Equal(PortDirection.Out, video.Appearance!.Direction);
        Assert.True(mains.CentreY > BoxMetrics.TitleAreaHeight(Themes.Light, true, true));

        // Assert: the frame moved right to make room for the left label, and the canvas fits the right one
        Assert.True(frame.X > 0.0);
        AssertPortsClear(layout, framePorts);
    }

    /// <summary>
    ///     A reference part is drawn with a dashed outline and the keyword <c>ref part</c>; an ordinary
    ///     part has a solid outline and the keyword <c>part</c>.
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_ReferencePart_HasDashedOutlineAndRefKeyword()
    {
        // Arrange
        var layout = await LayOutModelAsync("""
            package Q {
                part def A; part def B;
                part def Rig { part a : A; ref part b : B; connection c connect a to b; }
            }
            """);

        // Act
        var reference = FindPartBox(layout, "b : B");
        var ordinary = FindPartBox(layout, "a : A");

        // Assert
        Assert.Equal("ref part", reference.Keyword);
        Assert.Equal(LineStyle.Dashed, reference.Appearance?.OutlineStyle);
        Assert.Equal("part", ordinary.Keyword);
        Assert.NotEqual(LineStyle.Dashed, ordinary.Appearance?.OutlineStyle);
    }

    /// <summary>Item, action, state, and occurrence usages are nodes of the view, with their own keyword.</summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_ItemActionStateOccurrenceUsages_AreNodes()
    {
        // Arrange: a part plus one usage of each other node kind, all connected to it
        var layout = await LayOutModelAsync("""
            package Q {
                item def I; part def A;
                part def Rig {
                    part a : A;
                    item i : I;
                    action act;
                    state st;
                    occurrence oc;
                    connection c1 connect a to i;
                    connection c2 connect a to act;
                    connection c3 connect a to st;
                    connection c4 connect a to oc;
                }
            }
            """);

        // Act
        var boxes = CollectBoxes(layout.Nodes).Where(b => b.Shape == BoxShape.RoundedRectangle).ToList();

        // Assert
        Assert.Equal(5, boxes.Count);
        Assert.Contains(boxes, b => b.Keyword == "item" && b.Label == "i : I");
        Assert.Contains(boxes, b => b.Keyword == "action" && b.Label == "act");
        Assert.Contains(boxes, b => b.Keyword == "state" && b.Label == "st");
        Assert.Contains(boxes, b => b.Keyword == "occurrence" && b.Label == "oc");
        Assert.Equal(4, CollectLines(layout.Nodes).Count);
    }

    /// <summary>
    ///     A part usage declared directly in a package whose body declares nested parts and connectors is
    ///     a root even without a definition: its frame is a rounded <c>«part» name</c> box.
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_UsageRoot_RendersBodyWithoutDefinition()
    {
        // Arrange: no part definition at all
        var layout = await LayOutModelAsync("""
            package Q {
                part system {
                    part a; part b;
                    connection c connect a to b;
                }
            }
            """);

        // Act
        var frame = Assert.IsType<LayoutBox>(Assert.Single(layout.Nodes));

        // Assert
        Assert.Equal("part", frame.Keyword);
        Assert.Equal("system", frame.Label);
        Assert.Equal(BoxShape.RoundedRectangle, frame.Shape);
        Assert.Equal(2, frame.Children.OfType<LayoutBox>().Count());
        Assert.Single(frame.Children.OfType<LayoutLine>());
    }

    /// <summary>
    ///     A part that declares a doc comment and attributes shows <c>doc</c> and <c>attributes</c>
    ///     compartments; a part that declares neither has no compartments.
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_PartWithAttributesAndDoc_ShowsCompartments()
    {
        // Arrange
        var layout = await LayOutModelAsync("""
            package Q {
                attribute def Frequency;
                part def Cpu; part def Ram;
                part def Rig {
                    part cpu : Cpu {
                        doc /* Executes instructions. */
                        attribute clock : Frequency;
                    }
                    part ram : Ram;
                    connection c connect cpu to ram;
                }
            }
            """);

        // Act
        var cpu = FindPartBox(layout, "cpu : Cpu");
        var ram = FindPartBox(layout, "ram : Ram");

        // Assert
        Assert.Equal(["doc", "attributes"], cpu.Compartments.Select(c => c.Title).ToArray());
        Assert.Contains(cpu.Compartments[0].Rows, r => r.Contains("Executes instructions", StringComparison.Ordinal));
        Assert.Equal(["clock : Frequency"], cpu.Compartments[1].Rows.ToArray());
        Assert.Empty(ram.Compartments);
        Assert.True(cpu.Height > ram.Height);
    }

    /// <summary>
    ///     The root frame carries a centered italic <c>interconnection</c> compartment title below its
    ///     name, and its content starts below that title.
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_RootFrame_HasInterconnectionTitleCompartment()
    {
        // Arrange
        var layout = await LayOutModelAsync("""
            package Q {
                part def A; part def B;
                part def Rig { part a : A; part b : B; connection c connect a to b; }
            }
            """);

        // Act
        var frame = Assert.IsType<LayoutBox>(Assert.Single(layout.Nodes));

        // Assert: one title-only compartment, centered and italic
        var title = Assert.Single(frame.Compartments);
        Assert.Equal("interconnection", title.Title);
        Assert.Equal(TextAlign.Center, title.TitleAlign);
        Assert.Equal(TextStyle.Italic, title.ResolvedTitleStyle);
        Assert.Empty(title.Rows);

        // Assert: every inner box starts below the name compartment and the title band
        var headerBottom = BoxMetrics.TitleAreaHeight(Themes.Light, true, true) + Themes.Light.LabelPadding + Themes.Light.FontSizeBody + Themes.Light.LabelPadding;
        Assert.All(frame.Children.OfType<LayoutBox>(), b => Assert.True(b.Y >= headerBottom, $"{b.Label} starts above the interconnection title band"));
    }

    /// <summary>A binding connector is a plain line labeled <c>=</c>.</summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_BindingConnector_HasEqualsLabel()
    {
        // Arrange
        var layout = await LayOutModelAsync("""
            package Q {
                port def P;
                part def A { port pa : P; }
                part def B { port pb : P; }
                part def Rig { part a : A; part b : B; bind a.pa = b.pb; }
            }
            """);

        // Act
        var line = Assert.Single(CollectLines(layout.Nodes));

        // Assert
        Assert.Equal("=", line.MidpointLabel);
        Assert.Equal(EndMarkerStyle.None, line.TargetEnd);
        Assert.Equal(EndMarkerStyle.None, line.SourceEnd);
        Assert.Equal(LineStyle.Solid, line.LineStyle);
    }

    /// <summary>
    ///     A flow connector ends in a filled arrowhead at its target and is labeled <c>«flow»</c>, then its
    ///     name and payload when it declares them.
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_FlowConnector_HasFilledArrowAndFlowLabel()
    {
        // Arrange: one named flow with a payload, one anonymous flow without
        var layout = await LayOutModelAsync("""
            package Q {
                item def Torque;
                port def P;
                part def A { out port o1 : P; out port o2 : P; }
                part def B { in port i1 : P; in port i2 : P; }
                part def Rig {
                    part a : A; part b : B;
                    flow torque of Torque from a.o1 to b.i1;
                    flow from a.o2 to b.i2;
                }
            }
            """);

        // Act
        var lines = CollectLines(layout.Nodes);

        // Assert
        Assert.Equal(2, lines.Count);
        Assert.All(lines, l => Assert.Equal(EndMarkerStyle.FilledArrow, l.TargetEnd));
        Assert.Contains(lines, l => l.MidpointLabel == "«flow» torque of Torque");
        Assert.Contains(lines, l => l.MidpointLabel == "«flow»");
    }

    /// <summary>An interface connector is a plain line labeled <c>«interface»</c>, then its name.</summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_InterfaceConnector_HasInterfaceLabel()
    {
        // Arrange
        var layout = await LayOutModelAsync("""
            package Q {
                port def P;
                part def A { port pa : P; }
                part def B { port pb : P; }
                part def Rig { part a : A; part b : B; interface bus connect a.pa to b.pb; }
            }
            """);

        // Act
        var line = Assert.Single(CollectLines(layout.Nodes));

        // Assert
        Assert.Equal("«interface» bus", line.MidpointLabel);
        Assert.Equal(EndMarkerStyle.None, line.TargetEnd);
    }

    /// <summary>
    ///     Message and allocation usages are not interconnection connectors, so they are not drawn (and do
    ///     not count when choosing the root), while a connection beside them still is.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_MessageAndAllocation_AreNotDrawn()
    {
        // Arrange: a message, an allocation, and one connection between the same two parts
        var rig = new SysmlDefinitionNode
        {
            Name = "Rig",
            QualifiedName = "M::Rig",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "a", QualifiedName = "M::Rig::a", FeatureKeyword = "part" },
                new SysmlFeatureNode { Name = "b", QualifiedName = "M::Rig::b", FeatureKeyword = "part" },
                new SysmlConnectionNode { Name = "m", QualifiedName = "M::Rig::m", ConnectionKeyword = "message", EndpointA = "a", EndpointB = "b" },
                new SysmlConnectionNode { Name = "x", QualifiedName = "M::Rig::x", ConnectionKeyword = "allocation", EndpointA = "a", EndpointB = "b" },
                new SysmlConnectionNode { Name = "c", QualifiedName = "M::Rig::c", ConnectionKeyword = "connection", EndpointA = "a", EndpointB = "b" },
            ],
        };
        var workspace = new SysmlWorkspace { Declarations = new Dictionary<string, SysmlNode> { ["M::Rig"] = rig } };

        // Act
        var layout = new InterconnectionViewLayoutStrategy().BuildLayout(new ViewContext("v", workspace), new RenderOptions(Themes.Light));

        // Assert: only the connection is a line
        Assert.Single(CollectLines(layout.Nodes));
    }

    /// <summary>
    ///     A role name and a multiplicity declared on a connector end become the end label of that end:
    ///     role name above, multiplicity below; an end that declares only one of them labels only that.
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_ConnectionEnds_RoleNameAndMultiplicityBecomeEndLabels()
    {
        // Arrange
        var layout = await LayOutModelAsync("""
            package Q {
                port def P;
                part def A { port pa : P; }
                part def B { port pb : P; }
                part def Rig {
                    part a : A; part b : B;
                    connection c connect [2] supply ::> a.pa to drain ::> b.pb;
                }
            }
            """);

        // Act
        var line = Assert.Single(CollectLines(layout.Nodes));

        // Assert
        Assert.Equal(new LineEndLabel("supply", "[2]"), line.SourceEndLabel);
        Assert.Equal(new LineEndLabel("drain", null), line.TargetEndLabel);
    }

    /// <summary>A connector whose ends declare neither a role name nor a multiplicity carries no end labels.</summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_ConnectionWithoutEnds_HasNoEndLabels()
    {
        // Arrange
        var layout = await LayOutModelAsync("""
            package Q {
                part def A; part def B;
                part def Rig { part a : A; part b : B; connection c connect a to b; }
            }
            """);

        // Act
        var line = Assert.Single(CollectLines(layout.Nodes));

        // Assert
        Assert.Null(line.SourceEndLabel);
        Assert.Null(line.TargetEndLabel);
        Assert.Null(line.MidpointLabel);
    }

    /// <summary>
    ///     A part usage whose own body declares nested parts is a container just like a part typed by a
    ///     definition with parts: its inner parts and connector are nested in its box.
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_InlineBodyPart_RecursesAsContainer()
    {
        // Arrange
        var layout = await LayOutModelAsync("""
            package Q {
                part def Other;
                part def Rig {
                    part outer { part i1; part i2; connection ci connect i1 to i2; }
                    part other : Other;
                    connection co connect outer to other;
                }
            }
            """);

        // Act
        var outer = FindPartBox(layout, "outer");

        // Assert
        Assert.Equal(["i1", "i2"], outer.Children.OfType<LayoutBox>().Select(b => b.Label).Order().ToArray());
        Assert.Single(outer.Children.OfType<LayoutLine>());
    }

    /// <summary>
    ///     Every port glyph and outside label of a layout with long labels at both faces of the diagram stays
    ///     inside the canvas.
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_OutwardLabels_StayInsideCanvas()
    {
        // Arrange: long port labels on the outermost faces, connected and unconnected
        var layout = await LayOutModelAsync("""
            package Q {
                port def AVeryLongPortDefinitionName;
                part def A { in port aLongInboundPortName : AVeryLongPortDefinitionName; out port anotherLongOutboundPortName : AVeryLongPortDefinitionName; }
                part def B { in port bLongInboundPortName : AVeryLongPortDefinitionName; port unconnectedLongPortName : AVeryLongPortDefinitionName; }
                part def Rig {
                    in port frameInbound : AVeryLongPortDefinitionName;
                    part a : A; part b : B;
                    connection c connect a.anotherLongOutboundPortName to b.bLongInboundPortName;
                }
            }
            """);

        // Act
        var ports = CollectPorts(layout.Nodes);

        // Assert
        Assert.True(ports.Count >= 5);
        AssertPortsClear(layout, ports);
    }

    /// <summary>
    ///     On the motor-rig gallery model the flow and interface labels, which the layout engine does not
    ///     reserve room for, are moved clear of every port label by widening the corridors.
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_MotorRigGalleryModel_MidpointLabelsClearOfPortLabels()
    {
        // Arrange
        var workspace = await LabelClearance.LoadGalleryWorkspaceAsync("09-motor-controller-multi-port.sysml");

        // Act
        var layout = new InterconnectionViewLayoutStrategy().BuildLayout(new ViewContext("v", workspace), new RenderOptions(SysmlThemes.Plain));
        var lines = CollectLines(layout.Nodes);
        var labels = ConnectorLabelPlacer.ResolveLabels(lines, CollectBoxes(layout.Nodes), CollectPorts(layout.Nodes), SysmlThemes.Plain);

        // Assert: five connectors of four kinds, every midpoint label clear of every port glyph and label
        Assert.Equal(5, lines.Count);
        Assert.Equal(3, lines.Count(l => l.MidpointLabel is not null));
        foreach (var line in lines.Where(l => l.MidpointLabel is not null))
        {
            var label = labels.Midpoints[line];
            Assert.All(labels.PortRects, r => Assert.False(
                Math.Abs(label.Y - r.Y) < label.HalfHeight + r.HalfHeight && Math.Abs(label.X - r.X) < label.HalfWidth + r.HalfWidth,
                $"The label '{line.MidpointLabel}' overlaps a port label or glyph."));
        }
    }

    /// <summary>
    ///     The motor-rig gallery model draws every declared port (eleven, one of them unconnected), a
    ///     reference port with a dashed outline, a conjugated label, and the end labels of the power link.
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_MotorRigGalleryModel_ShowsPortNotation()
    {
        // Arrange
        var workspace = await LabelClearance.LoadGalleryWorkspaceAsync("09-motor-controller-multi-port.sysml");

        // Act
        var layout = new InterconnectionViewLayoutStrategy().BuildLayout(new ViewContext("v", workspace), new RenderOptions(SysmlThemes.Plain));
        var ports = CollectPorts(layout.Nodes);
        var lines = CollectLines(layout.Nodes);

        // Assert
        Assert.Equal(11, ports.Count);
        Assert.Contains(ports, p => p.ExternalLabel == "status : StatusPort");
        Assert.Contains(ports, p => p.ExternalLabel == "powerIn : ~PowerPort");
        Assert.Equal(LineStyle.Dashed, PortNamed(ports, "mount : MountPort").Appearance!.OutlineStyle);
        Assert.Contains(lines, l => l.SourceEndLabel == new LineEndLabel("supply", "[1]") && l.TargetEndLabel == new LineEndLabel("drive", "[1]"));
        Assert.Contains(lines, l => l.MidpointLabel == "=");
        Assert.Contains(lines, l => l.TargetEnd == EndMarkerStyle.FilledArrow);
        Assert.Contains(lines, l => l.MidpointLabel == "«flow» of Torque");
        Assert.Contains(lines, l => l.MidpointLabel == "«interface»");
        AssertPortsClear(layout, ports);
    }

    /// <summary>
    ///     The workstation gallery model shows a reference part, an item, a part with compartments, and an
    ///     interface connector between two declared ports.
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_WorkstationGalleryModel_ShowsUsageNotation()
    {
        // Arrange
        var workspace = await LabelClearance.LoadGalleryWorkspaceAsync("02-computer-interconnection.sysml");

        // Act
        var layout = new InterconnectionViewLayoutStrategy().BuildLayout(new ViewContext("v", workspace), new RenderOptions(SysmlThemes.Plain));
        var boxes = CollectBoxes(layout.Nodes);

        // Assert
        Assert.Contains(boxes, b => b.Keyword == "ref part" && b.Label == "monitor : Display" && b.Appearance?.OutlineStyle == LineStyle.Dashed);
        Assert.Contains(boxes, b => b.Keyword == "item" && b.Label == "firmware : Firmware");
        Assert.Contains(boxes, b => b.Label == "memory : Ram [2]");
        Assert.Contains(boxes, b => b.Label == "cpu : Cpu" && b.Compartments.Count == 2);
        Assert.Contains(CollectLines(layout.Nodes), l => l.MidpointLabel == "«interface»");
        Assert.Equal(2, CollectPorts(layout.Nodes).Count);
    }

    /// <summary>
    ///     The nested-computer gallery model draws the computer's own ports on its frame, the motherboard's
    ///     ports on its border, and the connectors between the part ports.
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_ComputerGalleryModel_ShowsFrameAndNestedPorts()
    {
        // Arrange
        var workspace = await LabelClearance.LoadGalleryWorkspaceAsync("08-nested-interconnection.sysml");

        // Act
        var layout = new InterconnectionViewLayoutStrategy().BuildLayout(new ViewContext("v", workspace), new RenderOptions(SysmlThemes.Plain));
        var frame = Assert.IsType<LayoutBox>(Assert.Single(layout.Nodes));

        // Assert: two ports on the frame border, plus the six part ports of the three connectors
        var onFrameBorder = frame.Children.OfType<LayoutPort>()
            .Where(p => Math.Abs(p.CentreX - frame.X) < 0.5 || Math.Abs(p.CentreX - (frame.X + frame.Width)) < 0.5)
            .ToList();
        Assert.Equal(["mains : MainsPort", "video : VideoPort"], onFrameBorder.Select(p => p.ExternalLabel!).Order().ToArray());
        var ports = CollectPorts(layout.Nodes);
        Assert.Equal(8, ports.Count);
        AssertPortsClear(layout, ports);
    }
    /// <summary>Loads a model from source text and lays out the interconnection view with its default root.</summary>
    private static async Task<LayoutTree> LayOutModelAsync(string sysml)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, sysml, TestContext.Current.CancellationToken);
            var (stdlibTable, _) = DemaConsulting.SysML2Tools.Stdlib.StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);
            Assert.NotNull(result.Workspace);
            return new InterconnectionViewLayoutStrategy().BuildLayout(new ViewContext("v", result.Workspace), new RenderOptions(Themes.Light));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>Returns the single port with the given label.</summary>
    private static LayoutPort PortNamed(IReadOnlyList<LayoutPort> ports, string label) =>
        Assert.Single(ports, p => p.ExternalLabel == label);

    /// <summary>
    ///     Asserts that, for the given ports, no glyph or outward label overlaps another port's glyph or
    ///     label or a part box (own box included for labels), and that all of them lie inside the canvas.
    /// </summary>
    private static void AssertPortsClear(LayoutTree layout, IReadOnlyList<LayoutPort> ports)
    {
        var theme = Themes.Light;
        var parts = CollectBoxes(layout.Nodes).Where(b => b.Shape == BoxShape.RoundedRectangle).ToList();
        var footprints = new List<(string Name, LabelPlacement Glyph, LabelPlacement? Label)>();
        foreach (var port in ports)
        {
            // A repeated glyph of a shared port has no label of its own (the first glyph carries it)
            var label = NotationMetrics.IsOutwardLabel(port) && port.ExternalLabel is { } text
                ? NotationMetrics.PortOutwardLabelBounds(port, text, theme)
                : (LabelPlacement?)null;
            footprints.Add((port.ExternalLabel ?? "?", NotationMetrics.PortGlyphBounds(port), label));
        }

        static bool Hit(LabelPlacement a, LabelPlacement b) =>
            Math.Abs(a.X - b.X) < a.HalfWidth + b.HalfWidth && Math.Abs(a.Y - b.Y) < a.HalfHeight + b.HalfHeight;

        for (var i = 0; i < footprints.Count; i++)
        {
            var (name, glyph, label) = footprints[i];
            foreach (var rect in label is { } l ? new[] { glyph, l } : [glyph])
            {
                // Inside the canvas
                Assert.True(rect.X - rect.HalfWidth >= -0.01 && rect.X + rect.HalfWidth <= layout.Width + 0.01, $"{name} leaves the canvas horizontally");
                Assert.True(rect.Y - rect.HalfHeight >= -0.01 && rect.Y + rect.HalfHeight <= layout.Height + 0.01, $"{name} leaves the canvas vertically");

                // Clear of every other port
                for (var j = 0; j < footprints.Count; j++)
                {
                    if (j == i)
                    {
                        continue;
                    }

                    Assert.False(Hit(rect, footprints[j].Glyph), $"{name} overlaps the glyph of {footprints[j].Name}");
                    Assert.False(footprints[j].Label is { } other && Hit(rect, other), $"{name} overlaps the label of {footprints[j].Name}");
                }
            }

            // A label (outside its part) overlaps no part box
            if (label is { } text)
            {
                foreach (var part in parts)
                {
                    var box = new LabelPlacement(part.X + (part.Width / 2.0), part.Y + (part.Height / 2.0), part.Width / 2.0, part.Height / 2.0);
                    Assert.False(Hit(text, box), $"The label of {name} overlaps the box {part.Label}");
                }
            }
        }
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
    ///     When two independent composition roots are both exposed, every root is drawn (SysML v2
    ///     §8.2.3.11 interconnection-view is a repeated element list): each gets its own container box,
    ///     in declaration order, side by side with aligned tops and no overlap, each holding only its
    ///     own parts.
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_ExposeTwoIndependentRoots_DrawsBothSideBySide()
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

        var roots = layout.Nodes.OfType<LayoutBox>().ToList();
        Assert.Equal(["AB", "MuchLongerSiblingName"], roots.Select(r => r.Label));
        Assert.Equal(roots[0].Y, roots[1].Y);
        Assert.True(roots[0].X + roots[0].Width < roots[1].X);
        Assert.True(layout.Width >= roots[1].X + roots[1].Width);
        Assert.All(roots[1].Children.OfType<LayoutBox>(), child =>
        {
            Assert.StartsWith("b", child.Label, StringComparison.Ordinal);
            Assert.True(child.X >= roots[1].X);
        });
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
    ///     An <c>expose</c> edge that resolves to a part usage (not a definition) draws that usage as
    ///     the root frame with the interior its type provides, not the type's definition: exposing a
    ///     usage <c>myPart</c> typed by <c>SysB</c> selects <c>myPart : SysB</c> as the root (the
    ///     typing target the shared resolver adds still scopes the type's own members).
    /// </summary>
    [Fact]
    public void InterconnectionView_BuildLayout_ExposedUsage_DrawsUsageWithTypeInteriorAsRoot()
    {
        var strategy = new InterconnectionViewLayoutStrategy();
        var workspace = BuildTwoRootWorkspace();
        workspace.AddDeclaration("M::myPart", new SysmlFeatureNode
        {
            Name = "myPart",
            QualifiedName = "M::myPart",
            FeatureKeyword = "part",
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

        var boxes = CollectBoxes(layout.Nodes);
        Assert.DoesNotContain(boxes, b => b.Keyword == "part def");
        var frame = Assert.Single(boxes, b => b.Keyword == "part" && b.Label == "myPart : SysB");
        Assert.NotEmpty(frame.Children.OfType<LayoutBox>());
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
                new SysmlFeatureNode
                {
                    Name = "sub",
                    QualifiedName = "Root::NsA::System::sub",
                    FeatureKeyword = "part",
                    FeatureTyping = "Sub",
                    ResolvedEdges = [new SysmlEdge("Root::NsA::System::sub", "Root::NsB::Sub", SysmlEdgeKind.Typing)]
                }
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
    ///     When two packages each define a container part def with the same simple name
    ///     (<c>Inner</c>), a part typed <c>Inner</c> inside <c>PkgB::Outer</c> must expand to
    ///     <c>PkgB::Inner</c>'s own interior, not to the first same-named <c>PkgA::Inner</c> found
    ///     workspace-wide.
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_SameNamedContainerDefs_ExpandsOwnPackagesDefinition()
    {
        // Arrange: PkgA::Inner (declared first) and PkgB::Inner have different interiors
        const string sysml = """
            package PkgA { part def Leaf; part def Inner { part a1 : Leaf; } }
            package PkgB {
                part def Leaf;
                part def Inner { part b1 : Leaf; }
                part def Outer { part inner : Inner; part other : Leaf; }
                view OuterView { expose Outer::**; render asInterconnectionDiagram; }
            }
            """;
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, sysml, TestContext.Current.CancellationToken);
            var (stdlibTable, _) = DemaConsulting.SysML2Tools.Stdlib.StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);
            Assert.NotNull(result.Workspace);
            var view = Assert.IsType<SysmlViewNode>(result.Workspace.Declarations["PkgB::OuterView"]);
            var context = new ViewContext("OuterView", result.Workspace, view);

            // Act
            var layout = new InterconnectionViewLayoutStrategy().BuildLayout(context, new RenderOptions(Themes.Light));

            // Assert: the nested inner part shows PkgB::Inner's own b1 part, never PkgA's a1
            var labels = CollectBoxes(layout.Nodes).Select(b => b.Label).ToList();
            Assert.Contains(labels, l => l?.StartsWith("b1", StringComparison.Ordinal) == true);
            Assert.DoesNotContain(labels, l => l?.StartsWith("a1", StringComparison.Ordinal) == true);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A part typed through an import of its own package expands the definition that import
    ///     brings in, even when another package in the same file imports a same-named definition
    ///     first: the semantic typing edge already applies KerML name resolution, which uses only
    ///     the referencing namespace's imports, and the layout trusts it.
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_SameNamedImportsInOneFile_ExpandsOwnImportsDefinition()
    {
        // Arrange: UseA imports A::* (declared first); UseB imports B::*
        const string sysml = """
            package A { part def Leaf; part def Inner { part a1 : Leaf; } }
            package B { part def Leaf; part def Inner { part b1 : Leaf; } }
            package UseA { private import A::*; part def OuterA { part inner : Inner; } }
            package UseB {
                private import B::*;
                part def OuterB { part inner : Inner; part other : Leaf; }
                view OuterView { expose OuterB::**; render asInterconnectionDiagram; }
            }
            """;
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, sysml, TestContext.Current.CancellationToken);
            var (stdlibTable, _) = DemaConsulting.SysML2Tools.Stdlib.StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);
            Assert.NotNull(result.Workspace);
            var view = Assert.IsType<SysmlViewNode>(result.Workspace.Declarations["UseB::OuterView"]);
            var context = new ViewContext("OuterView", result.Workspace, view);

            // Act
            var layout = new InterconnectionViewLayoutStrategy().BuildLayout(context, new RenderOptions(Themes.Light));

            // Assert: the inner part shows B::Inner's own b1 part, never A::Inner's a1
            var labels = CollectBoxes(layout.Nodes).Select(b => b.Label).ToList();
            Assert.Contains(labels, l => l?.StartsWith("b1", StringComparison.Ordinal) == true);
            Assert.DoesNotContain(labels, l => l?.StartsWith("a1", StringComparison.Ordinal) == true);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A part typed by a private member of another package gets no semantic typing edge, so the
    ///     hidden definition is not expanded (the part is a leaf, no layout warning); the public
    ///     control is wired through its semantic edge and expands.
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_PrivateMemberTyping_IsLeaf()
    {
        // Arrange / Act
        var layout = await LayOutOuterViewAsync("""
            package P {
                part def Leaf;
                private part def Hidden { part h1 : Leaf; }
                part def Shown { part s1 : Leaf; }
            }
            package Q {
                part def Outer { part a : P::Hidden; part b : P::Shown; }
                view OuterView { expose Outer::**; render asInterconnectionDiagram; }
            }
            """);

        // Assert
        var labels = CollectBoxes(layout.Nodes).Select(b => b.Label).ToList();
        Assert.Contains(labels, l => l?.StartsWith("s1", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(labels, l => l?.StartsWith("h1", StringComparison.Ordinal) == true);
        Assert.Empty(layout.Warnings);
    }

    /// <summary>
    ///     A part typed through another package's import (not its own) is a leaf: it does not
    ///     expand the definition that import brings in, and the layout reports no warning.
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_ImportOfSiblingPackage_IsLeaf()
    {
        // Arrange / Act
        var layout = await LayOutOuterViewAsync("""
            package A { part def Leaf; part def Inner { part a1 : Leaf; } }
            package UseA { private import A::*; part def OuterA { part inner : Inner; } }
            package UseB {
                part def OuterB { part inner : Inner; }
                view OuterView { expose OuterB::**; render asInterconnectionDiagram; }
            }
            """, "UseB::OuterView");

        // Assert
        var labels = CollectBoxes(layout.Nodes).Select(b => b.Label).ToList();
        Assert.DoesNotContain(labels, l => l?.StartsWith("a1", StringComparison.Ordinal) == true);
        Assert.Empty(layout.Warnings);
    }

    /// <summary>
    ///     A part typed through a filtered import that rejects the definition is a leaf; the
    ///     definition the filter accepts expands. The layout reports no warning.
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_FilteredOutImportCandidate_IsLeaf()
    {
        // Arrange / Act
        var layout = await LayOutOuterViewAsync("""
            metadata def X { }
            package P {
                part def Leaf;
                part def Marked { @X; part m1 : Leaf; }
                part def Plain { part p1 : Leaf; }
            }
            package Q {
                private import P::*[@X];
                part def Outer { part a : Marked; part b : Plain; }
                view OuterView { expose Outer::**; render asInterconnectionDiagram; }
            }
            """, "Q::OuterView");

        // Assert
        var labels = CollectBoxes(layout.Nodes).Select(b => b.Label).ToList();
        Assert.Contains(labels, l => l?.StartsWith("m1", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(labels, l => l?.StartsWith("p1", StringComparison.Ordinal) == true);
        Assert.Empty(layout.Warnings);
    }

    /// <summary>
    ///     A part typed by a name that exists only in an unrelated package (no import, not in an
    ///     enclosing namespace) is a leaf: the same-named definition is not expanded and the layout
    ///     reports no warning.
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_SameSimpleNameInUnrelatedPackage_IsLeaf()
    {
        // Arrange / Act
        var layout = await LayOutOuterViewAsync("""
            package A { part def Leaf; part def Inner { part a1 : Leaf; } }
            package Q {
                part def Outer { part inner : Inner; }
                view OuterView { expose Outer::**; render asInterconnectionDiagram; }
            }
            """);

        // Assert
        var labels = CollectBoxes(layout.Nodes).Select(b => b.Label).ToList();
        Assert.DoesNotContain(labels, l => l?.StartsWith("a1", StringComparison.Ordinal) == true);
        Assert.Empty(layout.Warnings);
    }

    /// <summary>
    ///     Model for the exposed-definition and exposed-usage tests: <c>Car</c> has two nested parts, a
    ///     port and a connection between the parts; <c>Plain</c> has no parts; <c>Fleet</c> holds a
    ///     nested container part; <c>car</c> is a usage of <c>Car</c>.
    /// </summary>
    private const string ExposedCarSysml = """
        package Q {
            port def FuelPort;
            part def Engine { part cylinder : Cylinder; }
            part def Cylinder;
            part def Wheel;
            part def Car {
                port fuelIn : FuelPort;
                part engine : Engine;
                part wheels : Wheel [4];
                connect engine to wheels;
            }
            part def Plain;
            part def Fleet { part lead : Car; part spare : Wheel; }
            part car : Car;
            view ExposeDef { expose Car; render asInterconnectionDiagram; }
            view ExposeUsage { expose car; render asInterconnectionDiagram; }
            view ExposeRecursive { expose Car::**; render asInterconnectionDiagram; }
            view ExposePlain { expose Plain; render asInterconnectionDiagram; }
            view ExposeFleet { expose Fleet; render asInterconnectionDiagram; }
            view ExposeNarrow { expose Fleet::lead; render asInterconnectionDiagram; }
        }
        """;

    /// <summary>
    ///     <c>expose Car;</c> draws the part definition's frame with its nested parts inside, as the
    ///     dynamic view does (issue #145).
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_ExposePartDef_DrawsFrameWithNestedParts()
    {
        // Act
        var layout = await LayOutOuterViewAsync(ExposedCarSysml, "Q::ExposeDef");

        // Assert
        var boxes = CollectBoxes(layout.Nodes);
        var frame = Assert.Single(boxes, b => b.Keyword == "part def" && b.Label == "Car");
        Assert.Contains(frame.Compartments, c => c.Title == "interconnection");
        var nested = frame.Children.OfType<LayoutBox>().Select(b => b.Label).ToList();
        Assert.Contains("engine : Engine", nested);
        Assert.Contains("wheels : Wheel [4]", nested);
    }

    /// <summary>
    ///     An exposed part definition's frame also carries its ports, and the connection between its
    ///     parts is drawn as a line.
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_ExposePartDef_DrawsPortsAndConnections()
    {
        // Act
        var layout = await LayOutOuterViewAsync(ExposedCarSysml, "Q::ExposeDef");

        // Assert
        var frame = Assert.Single(CollectBoxes(layout.Nodes), b => b.Label == "Car");
        Assert.Contains(frame.Children.OfType<LayoutPort>(), p => p.ExternalLabel == "fuelIn : FuelPort");
        Assert.Single(CollectLines(layout.Nodes));
    }

    /// <summary>
    ///     An exposed definition is not expanded recursively: a nested part that is itself a container
    ///     stays a leaf box, as for any non-recursive expose.
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_ExposePartDef_NestedContainerPartStaysLeaf()
    {
        // Act
        var layout = await LayOutOuterViewAsync(ExposedCarSysml, "Q::ExposeFleet");

        // Assert
        var boxes = CollectBoxes(layout.Nodes);
        Assert.Contains(boxes, b => b.Label == "lead : Car");
        Assert.DoesNotContain(boxes, b => b.Label == "engine : Engine");
    }

    /// <summary>
    ///     <c>expose car;</c> draws the usage <c>car : Car</c> as the frame — not its definition —
    ///     with the parts, ports and connections it gets from its type.
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_ExposePartUsage_DrawsUsageFrameWithTypeStructure()
    {
        // Act
        var layout = await LayOutOuterViewAsync(ExposedCarSysml, "Q::ExposeUsage");

        // Assert
        var boxes = CollectBoxes(layout.Nodes);
        Assert.DoesNotContain(boxes, b => b.Keyword == "part def");
        var frame = Assert.Single(boxes, b => b.Keyword == "part" && b.Label == "car : Car");
        var nested = frame.Children.OfType<LayoutBox>().Select(b => b.Label).ToList();
        Assert.Contains("engine : Engine", nested);
        Assert.Contains("wheels : Wheel [4]", nested);
        Assert.Contains(frame.Children.OfType<LayoutPort>(), p => p.ExternalLabel == "fuelIn : FuelPort");
        Assert.Single(CollectLines(layout.Nodes));
    }

    /// <summary>
    ///     <c>expose Car::**;</c> still draws the nested parts exactly once, with no repeated top-level boxes.
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_ExposePartDefRecursive_DrawsEachNestedPartOnce()
    {
        // Act
        var layout = await LayOutOuterViewAsync(ExposedCarSysml, "Q::ExposeRecursive");

        // Assert
        var boxes = CollectBoxes(layout.Nodes);
        Assert.Single(boxes, b => b.Label == "Car");
        Assert.Single(boxes, b => b.Label == "engine : Engine");
        Assert.Single(boxes, b => b.Label == "wheels : Wheel [4]");
    }

    /// <summary>
    ///     Exposing one nested part of a definition (<c>expose Fleet::lead;</c>) still narrows the frame
    ///     of its owner to that part: the owner's other parts are not drawn.
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_ExposeNestedPart_StillNarrowsToThatPart()
    {
        // Act
        var layout = await LayOutOuterViewAsync(ExposedCarSysml, "Q::ExposeNarrow");

        // Assert
        var labels = CollectBoxes(layout.Nodes).Select(b => b.Label).ToList();
        Assert.Contains("lead : Car", labels);
        Assert.DoesNotContain("spare : Wheel", labels);
    }

    /// <summary>An exposed part definition without nested parts has nothing to draw: the canvas stays empty.</summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_ExposePartDefWithoutParts_DrawsNothing()
    {
        // Act
        var layout = await LayOutOuterViewAsync(ExposedCarSysml, "Q::ExposePlain");

        // Assert
        Assert.Empty(CollectBoxes(layout.Nodes));
    }

    /// <summary>
    ///     Model for the imported-visibility root test: <c>Host</c> imports the membership
    ///     <c>Lib::Widget</c>, whose private nested part definition <c>Secret</c> is a container the
    ///     import does not reach.
    /// </summary>
    private const string ImportedPrivateContainerSysml = """
        package Q {
            package Lib {
                part def Gear;
                part def Cog;
                part def Widget {
                    part gear : Gear;
                    private part def Secret { part first : Cog; part second : Cog; }
                }
            }
            package Host { import Lib::Widget; }
            view Imported { expose Host::**; render asInterconnectionDiagram; }
        }
        """;

    /// <summary>
    ///     A private container the exposed import does not reach cannot become the diagram's root:
    ///     <c>expose Host::**;</c> with <c>import Lib::Widget;</c> draws <c>Widget</c> with its public
    ///     <c>gear</c>, never the hidden <c>Secret</c> or its parts.
    /// </summary>
    [Fact]
    public async Task InterconnectionView_BuildLayout_ExposeImportedMember_HiddenPrivateContainerIsNotRoot()
    {
        // Act
        var layout = await LayOutOuterViewAsync(ImportedPrivateContainerSysml, "Q::Imported");

        // Assert
        var labels = CollectBoxes(layout.Nodes).Select(b => b.Label).ToList();
        Assert.Contains("Widget", labels);
        Assert.Contains("gear : Gear", labels);
        Assert.DoesNotContain("Secret", labels);
        Assert.DoesNotContain("first : Cog", labels);
    }

    /// <summary>Loads <paramref name="sysml"/> and lays out the interconnection view named <paramref name="viewQualifiedName"/>.</summary>
    private static async Task<LayoutTree> LayOutOuterViewAsync(string sysml, string viewQualifiedName = "Q::OuterView")
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, sysml, TestContext.Current.CancellationToken);
            var (stdlibTable, _) = DemaConsulting.SysML2Tools.Stdlib.StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);
            Assert.NotNull(result.Workspace);
            var view = Assert.IsType<SysmlViewNode>(result.Workspace.Declarations[viewQualifiedName]);
            var context = new ViewContext("OuterView", result.Workspace, view);
            return new InterconnectionViewLayoutStrategy().BuildLayout(context, new RenderOptions(Themes.Light));
        }
        finally
        {
            File.Delete(tempFile);
        }
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
