// <copyright file="LayoutTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout;

namespace DemaConsulting.SysML2Tools.Tests.Layout;

/// <summary>
///     Tests for Layout subsystem node types.
/// </summary>
public sealed class LayoutTests
{
    /// <summary>
    ///     A LayoutTree constructed with explicit dimensions and a non-empty Nodes list stores
    ///     all three constructor arguments without modification.
    /// </summary>
    [Fact]
    public void LayoutTree_Construction_StoresWidthHeightNodes()
    {
        // Arrange: a minimal LayoutBox as the single top-level node
        var node = new LayoutBox(0, 0, 10, 10, null, 0, BoxShape.Rectangle, [], []);

        // Act: construct the tree with explicit canvas dimensions
        var tree = new LayoutTree(800.0, 600.0, [node]);

        // Assert: all three properties equal the supplied values
        Assert.Equal(800.0, tree.Width);
        Assert.Equal(600.0, tree.Height);
        Assert.Single(tree.Nodes);
        Assert.Same(node, tree.Nodes[0]);
    }

    /// <summary>
    ///     A LayoutBox constructed with all nine parameters set to non-default values stores
    ///     each property exactly as supplied.
    /// </summary>
    [Fact]
    public void LayoutBox_Construction_StoresAllFields()
    {
        // Arrange: a compartment and a child node
        var compartment = new LayoutCompartment("Attributes", ["radius : Real"]);
        var child = new LayoutPort(15, 25, PortSide.Top, "p");

        // Act: construct a LayoutBox with all nine parameters non-default
        var box = new LayoutBox(
            X: 10.0,
            Y: 20.0,
            Width: 200.0,
            Height: 100.0,
            Label: "MyBlock",
            Depth: 2,
            Shape: BoxShape.RoundedRectangle,
            Compartments: [compartment],
            Children: [child]);

        // Assert: all nine properties equal the supplied values
        Assert.Equal(10.0, box.X);
        Assert.Equal(20.0, box.Y);
        Assert.Equal(200.0, box.Width);
        Assert.Equal(100.0, box.Height);
        Assert.Equal("MyBlock", box.Label);
        Assert.Equal(2, box.Depth);
        Assert.Equal(BoxShape.RoundedRectangle, box.Shape);
        Assert.Single(box.Compartments);
        Assert.Same(compartment, box.Compartments[0]);
        Assert.Single(box.Children);
        Assert.Same(child, box.Children[0]);
    }

    /// <summary>
    ///     LayoutBox.Depth is stored as an integer with the supplied value, confirming the
    ///     depth-not-color invariant: no color property is present on the node.
    /// </summary>
    [Fact]
    public void LayoutBox_Depth_IsInteger()
    {
        // Arrange / Act: construct a LayoutBox with Depth = 3
        var box = new LayoutBox(0, 0, 50, 50, null, 3, BoxShape.Rectangle, [], []);

        // Assert: Depth is stored as int with value 3
        Assert.IsType<int>(box.Depth);
        Assert.Equal(3, box.Depth);
    }

    /// <summary>
    ///     A LayoutBox constructed with explicit X and Y values stores those values without
    ///     applying any coordinate transform, confirming absolute-coordinate invariant.
    /// </summary>
    [Fact]
    public void LayoutBox_Coordinates_AreAbsolute()
    {
        // Arrange / Act: construct a box at an arbitrary absolute position
        var box = new LayoutBox(100.0, 200.0, 50, 50, null, 0, BoxShape.Rectangle, [], []);

        // Assert: X and Y are stored without offset
        Assert.Equal(100.0, box.X);
        Assert.Equal(200.0, box.Y);
    }

    /// <summary>
    ///     LayoutBox.Children can contain heterogeneous LayoutNode instances of different
    ///     concrete types, retrievable in insertion order.
    /// </summary>
    [Fact]
    public void LayoutBox_Children_ContainsNestedNodes()
    {
        // Arrange: one LayoutPort and one nested LayoutBox as children
        var port = new LayoutPort(10, 10, PortSide.Left, null);
        var nested = new LayoutBox(5, 5, 20, 20, null, 1, BoxShape.Rectangle, [], []);

        // Act: construct the parent box with both children
        var parent = new LayoutBox(0, 0, 100, 100, null, 0, BoxShape.Rectangle, [], [port, nested]);

        // Assert: both child nodes are retrievable in insertion order
        Assert.Equal(2, parent.Children.Count);
        Assert.Same(port, parent.Children[0]);
        Assert.Same(nested, parent.Children[1]);
    }

    /// <summary>
    ///     A LayoutPort constructed with all four parameters stores each property as supplied,
    ///     confirming that ports carry sufficient information for absolute positioning.
    /// </summary>
    [Fact]
    public void LayoutPort_Construction_StoresAllFields()
    {
        // Arrange / Act: construct a port with all four parameters set
        var port = new LayoutPort(250.0, 150.0, PortSide.Right, "myPort");

        // Assert: all four properties equal the supplied values
        Assert.Equal(250.0, port.CentreX);
        Assert.Equal(150.0, port.CentreY);
        Assert.Equal(PortSide.Right, port.Side);
        Assert.Equal("myPort", port.Label);
    }

    /// <summary>
    ///     A LayoutPort constructed with explicit CentreX and CentreY stores those values
    ///     without offset, confirming the absolute-coordinate invariant.
    /// </summary>
    [Fact]
    public void LayoutPort_Coordinates_AreAbsolute()
    {
        // Arrange / Act: construct a port at explicit absolute coordinates
        var port = new LayoutPort(250.0, 150.0, PortSide.Top, null);

        // Assert: coordinates are stored without transformation
        Assert.Equal(250.0, port.CentreX);
        Assert.Equal(150.0, port.CentreY);
    }

    /// <summary>
    ///     A LayoutLine constructed with all five parameters set stores each property as
    ///     supplied, including the full waypoints list, arrowhead styles, line style, and label.
    /// </summary>
    [Fact]
    public void LayoutLine_Construction_StoresAllFields()
    {
        // Arrange: a two-element waypoints list and a midpoint label
        var p1 = new Point2D(10.0, 20.0);
        var p2 = new Point2D(200.0, 300.0);

        // Act: construct a LayoutLine with all five parameters non-default
        var line = new LayoutLine(
            [p1, p2],
            ArrowheadStyle.Open,
            ArrowheadStyle.Filled,
            LineStyle.Dashed,
            "myLabel");

        // Assert: all five properties equal the supplied values
        Assert.Equal(2, line.Waypoints.Count);
        Assert.Same(p1, line.Waypoints[0]);
        Assert.Same(p2, line.Waypoints[1]);
        Assert.Equal(ArrowheadStyle.Open, line.SourceArrowhead);
        Assert.Equal(ArrowheadStyle.Filled, line.TargetArrowhead);
        Assert.Equal(LineStyle.Dashed, line.LineStyle);
        Assert.Equal("myLabel", line.MidpointLabel);
    }

    /// <summary>
    ///     A LayoutLine constructed with explicit waypoints stores those waypoints with their
    ///     supplied X and Y values intact, confirming that routing produces absolute coordinates.
    /// </summary>
    [Fact]
    public void LayoutLine_Waypoints_AreAbsolute()
    {
        // Arrange: two waypoints at known absolute positions
        var p1 = new Point2D(10.0, 20.0);
        var p2 = new Point2D(200.0, 300.0);

        // Act: construct the line with these waypoints
        var line = new LayoutLine([p1, p2], ArrowheadStyle.None, ArrowheadStyle.None, LineStyle.Solid, null);

        // Assert: both waypoints retain their supplied X and Y values
        Assert.Equal(10.0, line.Waypoints[0].X);
        Assert.Equal(20.0, line.Waypoints[0].Y);
        Assert.Equal(200.0, line.Waypoints[1].X);
        Assert.Equal(300.0, line.Waypoints[1].Y);
    }

    /// <summary>
    ///     A LayoutLabel constructed with all eight parameters stores each property as supplied.
    /// </summary>
    [Fact]
    public void LayoutLabel_Construction_StoresAllFields()
    {
        // Arrange / Act: construct a label with all eight parameters non-default
        var label = new LayoutLabel(50.0, 75.0, 200.0, "Hello World", TextAlign.Center, FontWeight.Regular, FontStyle.Normal, 12.0);

        // Assert: all eight properties equal the supplied values
        Assert.Equal(50.0, label.X);
        Assert.Equal(75.0, label.Y);
        Assert.Equal(200.0, label.MaxWidth);
        Assert.Equal("Hello World", label.Text);
        Assert.Equal(TextAlign.Center, label.Align);
        Assert.Equal(FontWeight.Regular, label.Weight);
        Assert.Equal(FontStyle.Normal, label.Style);
        Assert.Equal(12.0, label.FontSize);
    }

    /// <summary>
    ///     A LayoutBadge constructed with all five parameters stores each property as supplied.
    /// </summary>
    [Fact]
    public void LayoutBadge_Construction_StoresAllFields()
    {
        // Arrange / Act: construct a badge with all five parameters set
        var badge = new LayoutBadge(30.0, 40.0, 12.0, BadgeShape.FilledCircle, "initial");

        // Assert: all five properties equal the supplied values
        Assert.Equal(30.0, badge.CentreX);
        Assert.Equal(40.0, badge.CentreY);
        Assert.Equal(12.0, badge.Size);
        Assert.Equal(BadgeShape.FilledCircle, badge.Shape);
        Assert.Equal("initial", badge.Label);
    }

    /// <summary>
    ///     A LayoutBand constructed with all seven parameters stores each property as supplied.
    /// </summary>
    [Fact]
    public void LayoutBand_Construction_StoresAllFields()
    {
        // Arrange: a child node for the band
        var child = new LayoutBox(5, 5, 10, 10, null, 0, BoxShape.Rectangle, [], []);

        // Act: construct a band with all seven parameters set
        var band = new LayoutBand(10.0, 20.0, 400.0, 200.0, BandOrientation.Horizontal, "Lane 1", [child]);

        // Assert: all seven properties equal the supplied values
        Assert.Equal(10.0, band.X);
        Assert.Equal(20.0, band.Y);
        Assert.Equal(400.0, band.Width);
        Assert.Equal(200.0, band.Height);
        Assert.Equal(BandOrientation.Horizontal, band.Orientation);
        Assert.Equal("Lane 1", band.Label);
        Assert.Single(band.Children);
        Assert.Same(child, band.Children[0]);
    }

    /// <summary>
    ///     A LayoutLifeline constructed with all six parameters stores each property as supplied.
    /// </summary>
    [Fact]
    public void LayoutLifeline_Construction_StoresAllFields()
    {
        // Arrange / Act: construct a lifeline with all six parameters set
        var lifeline = new LayoutLifeline(150.0, 10.0, 500.0, ":Actor", 80.0, 40.0);

        // Assert: all six properties equal the supplied values
        Assert.Equal(150.0, lifeline.CentreX);
        Assert.Equal(10.0, lifeline.TopY);
        Assert.Equal(500.0, lifeline.BottomY);
        Assert.Equal(":Actor", lifeline.Label);
        Assert.Equal(80.0, lifeline.HeaderWidth);
        Assert.Equal(40.0, lifeline.HeaderHeight);
    }

    /// <summary>
    ///     A LayoutActivation constructed with CentreX, TopY, and BottomY stores all three
    ///     properties as supplied.
    /// </summary>
    [Fact]
    public void LayoutActivation_Construction_StoresAllFields()
    {
        // Arrange / Act: construct an activation bar with explicit coordinates
        var activation = new LayoutActivation(150.0, 100.0, 250.0);

        // Assert: all three properties equal the supplied values
        Assert.Equal(150.0, activation.CentreX);
        Assert.Equal(100.0, activation.TopY);
        Assert.Equal(250.0, activation.BottomY);
    }

    /// <summary>
    ///     A LayoutGrid constructed with X, Y, and a Rows list stores all fields as supplied,
    ///     including LayoutGridRow.IsHeader and the LayoutGridCell values.
    /// </summary>
    [Fact]
    public void LayoutGrid_Construction_StoresAllFields()
    {
        // Arrange: one header row containing one cell
        var cell = new LayoutGridCell(80.0, 24.0, "Name", TextAlign.Left, 1);
        var row = new LayoutGridRow(IsHeader: true, Cells: [cell]);

        // Act: construct the grid at an explicit absolute position
        var grid = new LayoutGrid(X: 50.0, Y: 100.0, Rows: [row]);

        // Assert: grid-level properties
        Assert.Equal(50.0, grid.X);
        Assert.Equal(100.0, grid.Y);
        Assert.Single(grid.Rows);

        // Assert: row-level properties
        Assert.True(grid.Rows[0].IsHeader);
        Assert.Single(grid.Rows[0].Cells);

        // Assert: cell-level properties
        var storedCell = grid.Rows[0].Cells[0];
        Assert.Equal(80.0, storedCell.Width);
        Assert.Equal(24.0, storedCell.Height);
        Assert.Equal("Name", storedCell.Text);
        Assert.Equal(TextAlign.Left, storedCell.Align);
        Assert.Equal(1, storedCell.ColSpan);
    }
}
