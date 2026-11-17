// <copyright file="LayeredPlacementTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.SysML2Tools.Layout.Internal;

namespace DemaConsulting.SysML2Tools.Tests.Layout;

/// <summary>
///     Tests for <see cref="LayeredPlacement"/>.
/// </summary>
public sealed class LayeredPlacementTests
{
    /// <summary>A three-node cycle (a -> b -> c -> a) whose last edge is a back edge.</summary>
    private static readonly (double Width, double Height)[] CycleNodes = [(100, 40), (100, 40), (100, 40)];

    /// <summary>The edges of the three-node cycle.</summary>
    private static readonly (int From, int To)[] CycleEdges = [(0, 1), (1, 2), (2, 0)];

    /// <summary>Returns the length of the final segment of a polyline.</summary>
    private static double FinalApproach(IReadOnlyList<Point2D> polyline)
    {
        var (last, previous) = (polyline[^1], polyline[^2]);
        return Math.Abs(last.X - previous.X) + Math.Abs(last.Y - previous.Y);
    }

    /// <summary>
    ///     An end decoration passed as <c>edgeTargetEnd</c> is declared on every edge, so the layout
    ///     engine sizes the final approach of every edge, including the back edge, to the marker.
    /// </summary>
    [Fact]
    public void LayeredPlacement_Place_EdgeTargetEnd_ForwardedToGraphEdges()
    {
        // Act
        var placed = LayeredPlacement.Place(
            CycleNodes,
            CycleEdges,
            LayoutFlowDirection.Down,
            mergeParallelEdges: false,
            edgeTargetEnd: EndMarkerStyle.OpenChevron);

        // Assert
        var marker = NotationMetrics.RenderedAlongLineLength(EndMarkerStyle.OpenChevron, Themes.Light);
        Assert.Equal(CycleEdges.Length, placed.EdgePolylines.Count);
        Assert.All(placed.EdgePolylines, polyline => Assert.True(FinalApproach(polyline) >= marker - 0.01));
    }

    /// <summary>
    ///     Without an <c>edgeTargetEnd</c> the placement still returns one routed polyline per edge,
    ///     exactly as before the parameter existed.
    /// </summary>
    [Fact]
    public void LayeredPlacement_Place_DefaultEdgeTargetEnd_PreservesBehavior()
    {
        // Act
        var placed = LayeredPlacement.Place(CycleNodes, CycleEdges, LayoutFlowDirection.Down, mergeParallelEdges: false);

        // Assert
        Assert.Equal(CycleNodes.Length, placed.Rects.Count);
        Assert.Equal(CycleEdges.Length, placed.EdgePolylines.Count);
        Assert.All(placed.EdgePolylines, polyline => Assert.True(polyline.Count >= 2));
    }

    /// <summary>The outward, outlined port look used by the port tests.</summary>
    private static readonly PortAppearance OutwardOutlined =
        new(PortDirection.Out, PortGlyph.Outlined, LineStyle.Solid, PortLabelPlacement.Outward);

    /// <summary>
    ///     Two edges that name the same port key on the same node attach to one port: the layout
    ///     carries the label once (the engine still emits a glyph per edge end), and a request without
    ///     a key keeps a port of its own.
    /// </summary>
    [Fact]
    public void LayeredPlacement_PlaceWithPorts_SharedPortKey_LabelAppearsOnce()
    {
        // Arrange: node 0 has one declared port "out" used by two edges to nodes 1 and 2
        var nodes = new (double Width, double Height, bool HasLabel, bool HasKeyword)[]
        {
            (120, 80, true, true), (120, 80, true, true), (120, 80, true, true),
        };
        var shared = new EdgePortRef("out : Fuel", OutwardOutlined, "p:out");
        var edges = new[]
        {
            new PortEdge(0, 1, shared, null),
            new PortEdge(0, 2, shared, null),
        };

        // Act
        var placed = LayeredPlacement.PlaceWithPorts(nodes, edges, LayoutFlowDirection.Right);

        // Assert: every placed glyph of the shared port belongs to one graph port, and only one is labeled
        var sharedGlyphs = placed.Ports.Where(p => p.SourcePort is not null && p.SourcePort.Id.EndsWith("p:out", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(sharedGlyphs);
        Assert.Single(sharedGlyphs.Select(p => p.SourcePort).Distinct());
        Assert.Single(sharedGlyphs, p => p.ExternalLabel == "out : Fuel");
        Assert.Equal(2, placed.Lines.Count);
    }

    /// <summary>
    ///     The appearance, end marker, line style, end labels, and midpoint label declared on a
    ///     <see cref="PortEdge"/> reach the placed ports and lines.
    /// </summary>
    [Fact]
    public void LayeredPlacement_PlaceWithPorts_EdgeNotation_ReachesPlacedPortsAndLines()
    {
        // Arrange
        var nodes = new (double Width, double Height, bool HasLabel, bool HasKeyword)[] { (120, 80, true, true), (120, 80, true, true) };
        var edges = new[]
        {
            new PortEdge(
                0,
                1,
                new EdgePortRef("a : A", OutwardOutlined, "p:a"),
                new EdgePortRef("b : ~B", new PortAppearance(PortDirection.In, PortGlyph.Outlined, LineStyle.Dashed, PortLabelPlacement.Outward), "p:b"),
                EndMarkerStyle.FilledArrow,
                LineStyle.Dashed,
                new LineEndLabel("supply", "[1]"),
                new LineEndLabel("drain", null),
                "«flow» of Fuel"),
        };

        // Act
        var placed = LayeredPlacement.PlaceWithPorts(nodes, edges, LayoutFlowDirection.Right);

        // Assert: the line carries the declared decoration and labels; the ports carry their appearance
        var line = Assert.Single(placed.Lines);
        Assert.Equal(EndMarkerStyle.FilledArrow, line.TargetEnd);
        Assert.Equal(LineStyle.Dashed, line.LineStyle);
        Assert.Equal("«flow» of Fuel", line.MidpointLabel);
        Assert.Equal(new LineEndLabel("supply", "[1]"), line.SourceEndLabel);
        Assert.Equal(new LineEndLabel("drain", null), line.TargetEndLabel);
        Assert.Equal(2, placed.Ports.Count);
        Assert.Contains(placed.Ports, p => p.ExternalLabel == "a : A" && p.Appearance == OutwardOutlined);
        Assert.Contains(placed.Ports, p => p.ExternalLabel == "b : ~B" && p.Appearance?.OutlineStyle == LineStyle.Dashed);
    }

    /// <summary>
    ///     An edge without port requests attaches to its nodes and yields no port glyphs, so a part-only
    ///     endpoint draws no port.
    /// </summary>
    [Fact]
    public void LayeredPlacement_PlaceWithPorts_NoPortRequests_YieldsNoPorts()
    {
        // Arrange
        var nodes = new (double Width, double Height, bool HasLabel, bool HasKeyword)[] { (120, 80, true, true), (120, 80, true, true) };

        // Act
        var placed = LayeredPlacement.PlaceWithPorts(nodes, [new PortEdge(0, 1, null, null)], LayoutFlowDirection.Right);

        // Assert
        Assert.Empty(placed.Ports);
        Assert.Single(placed.Lines);
        Assert.Equal(2, placed.Rects.Count);
    }

    /// <summary>
    ///     A midpoint label wider than the room the engine left between two port labels is moved clear
    ///     by widening the corridor: afterwards the resolved label overlaps no port label or glyph, and
    ///     the right-hand box moved right.
    /// </summary>
    [Fact]
    public void LayeredPlacement_WidenForMidpointLabels_LabelOverlappingPortLabels_IsSeparated()
    {
        // Arrange: a connector whose long midpoint label would sit between two long port labels
        var nodes = new (double Width, double Height, bool HasLabel, bool HasKeyword)[] { (125, 286, true, true), (110, 119, true, true) };
        var edges = new[]
        {
            new PortEdge(
                0,
                1,
                new EdgePortRef("shaft : ShaftPort", OutwardOutlined, "p:a"),
                new EdgePortRef("shaftIn : ShaftPort", OutwardOutlined, "p:b"),
                EndMarkerStyle.FilledArrow,
                LineStyle.Solid,
                null,
                null,
                "«flow» torque of Torque"),
            new PortEdge(
                0,
                1,
                new EdgePortRef("mount : MountPort", OutwardOutlined, "p:c"),
                new EdgePortRef("base : MountPort", OutwardOutlined, "p:d"),
                EndMarkerStyle.None,
                LineStyle.Solid,
                null,
                null,
                "«interface» mounting"),
        };
        var placed = LayeredPlacement.PlaceWithPorts(nodes, edges, LayoutFlowDirection.Right);

        // Act
        var widened = LayeredPlacement.WidenForMidpointLabels(placed, edges, Themes.Light);

        // Assert: nothing overlaps after widening, and the layout grew to the right
        var boxes = widened.Rects.Select(r => new LayoutBox(r.X, r.Y, r.Width, r.Height, string.Empty, 0, BoxShape.Rectangle, [], [])).ToList();
        var labels = ConnectorLabelPlacer.ResolveLabels(widened.Lines, boxes, widened.Ports, Themes.Light);
        Assert.All(widened.Lines, line =>
        {
            var label = labels.Midpoints[line];
            Assert.All(labels.PortRects, r => Assert.False(
                Math.Abs(label.Y - r.Y) < label.HalfHeight + r.HalfHeight && Math.Abs(label.X - r.X) < label.HalfWidth + r.HalfWidth,
                "The midpoint label overlaps a port label or glyph."));
        });
        Assert.True(widened.Width > placed.Width, "The corridor was not widened.");
        Assert.True(widened.Rects[1].X > placed.Rects[1].X, "The right-hand box did not move right.");
    }

    /// <summary>
    ///     A layout without midpoint labels is returned unchanged by the widening pass (the same instance).
    /// </summary>
    [Fact]
    public void LayeredPlacement_WidenForMidpointLabels_NoMidpointLabels_ReturnsSameLayout()
    {
        // Arrange
        var nodes = new (double Width, double Height, bool HasLabel, bool HasKeyword)[] { (120, 80, true, true), (120, 80, true, true) };
        var edges = new[] { new PortEdge(0, 1, null, null) };
        var placed = LayeredPlacement.PlaceWithPorts(nodes, edges, LayoutFlowDirection.Right);

        // Act
        var widened = LayeredPlacement.WidenForMidpointLabels(placed, edges, Themes.Light);

        // Assert
        Assert.Same(placed, widened);
    }
}
