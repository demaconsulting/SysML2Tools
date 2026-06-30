// <copyright file="AxisTransformTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout;
using DemaConsulting.SysML2Tools.Layout.Engine;
using DemaConsulting.SysML2Tools.Layout.Engine.Layered;

namespace DemaConsulting.SysML2Tools.Tests.Layout.Engine.Layered;

/// <summary>
///     Tests for <see cref="AxisTransform"/> covering the left-to-right identity behavior and the
///     coordinate mapping, port-face emergence, and waypoint orthogonality for all four layout
///     directions.
/// </summary>
public sealed class AxisTransformTests
{
    private const double Tolerance = 1e-6;
    private const double NodeWidth = 60.0;
    private const double NodeHeight = 40.0;

    /// <summary>
    ///     With the left-to-right direction, the transform is an identity: the placed coordinates
    ///     are unchanged after it runs.
    /// </summary>
    [Fact]
    public void AxisTransform_Apply_RightDirection_LeavesCoordinatesUnchanged()
    {
        // Arrange: place a chain (left-to-right) up to but not including the axis transform.
        var nodes = new List<LayerNode> { new(60, 40), new(60, 40), new(60, 40) };
        var edges = new List<LayerEdge> { new(0, 1), new(1, 2) };
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);
        new CycleBreaker().Apply(graph);
        new LayerAssigner().Apply(graph);
        new LongEdgeSplitter().Apply(graph);
        new CrossingMinimizer().Apply(graph);
        new BrandesKopfPlacer().Apply(graph);
        new PortDistributor().Apply(graph);
        new OrthogonalRouter().Apply(graph);
        new LongEdgeJoiner().Apply(graph);

        var beforeX = (double[])graph.AugX.Clone();
        var beforeY = (double[])graph.AugY.Clone();

        // Act: apply the identity transform.
        new AxisTransform().Apply(graph);

        // Assert: coordinates are unchanged.
        Assert.Equal(beforeX, graph.AugX);
        Assert.Equal(beforeY, graph.AugY);
    }

    /// <summary>
    ///     RIGHT places the target in a later layer to the right of the source; the source exits its
    ///     EAST face and the target enters its WEST face.
    /// </summary>
    [Fact]
    public void AxisTransform_Apply_Right_PlacesTargetEastWithCorrectFaces()
    {
        var graph = PlaceChain(LayoutDirection.Right);

        // Coordinate mapping: target sits to the right of the source.
        Assert.True(graph.AugX[1] > graph.AugX[0]);

        // Port faces: source on its EAST (right) face, target on its WEST (left) face.
        var (source, target) = EndpointsOf(graph, 0);
        Assert.Equal(Right(graph, 0), source.X, Tolerance);
        Assert.Equal(Left(graph, 1), target.X, Tolerance);
    }

    /// <summary>
    ///     DOWN places the target in a later layer below the source; the source exits its SOUTH face
    ///     and the target enters its NORTH face.
    /// </summary>
    [Fact]
    public void AxisTransform_Apply_Down_PlacesTargetSouthWithCorrectFaces()
    {
        var graph = PlaceChain(LayoutDirection.Down);

        // Coordinate mapping: target sits below the source.
        Assert.True(graph.AugY[1] > graph.AugY[0]);

        // Port faces: source on its SOUTH (bottom) face, target on its NORTH (top) face.
        var (source, target) = EndpointsOf(graph, 0);
        Assert.Equal(Bottom(graph, 0), source.Y, Tolerance);
        Assert.Equal(Top(graph, 1), target.Y, Tolerance);
    }

    /// <summary>
    ///     LEFT places the target in a later layer to the left of the source; the source exits its
    ///     WEST face and the target enters its EAST face.
    /// </summary>
    [Fact]
    public void AxisTransform_Apply_Left_PlacesTargetWestWithCorrectFaces()
    {
        var graph = PlaceChain(LayoutDirection.Left);

        // Coordinate mapping: target sits to the left of the source.
        Assert.True(graph.AugX[1] < graph.AugX[0]);

        // Port faces: source on its WEST (left) face, target on its EAST (right) face.
        var (source, target) = EndpointsOf(graph, 0);
        Assert.Equal(Left(graph, 0), source.X, Tolerance);
        Assert.Equal(Right(graph, 1), target.X, Tolerance);
    }

    /// <summary>
    ///     UP places the target in a later layer above the source; the source exits its NORTH face
    ///     and the target enters its SOUTH face.
    /// </summary>
    [Fact]
    public void AxisTransform_Apply_Up_PlacesTargetNorthWithCorrectFaces()
    {
        var graph = PlaceChain(LayoutDirection.Up);

        // Coordinate mapping: target sits above the source.
        Assert.True(graph.AugY[1] < graph.AugY[0]);

        // Port faces: source on its NORTH (top) face, target on its SOUTH (bottom) face.
        var (source, target) = EndpointsOf(graph, 0);
        Assert.Equal(Top(graph, 0), source.Y, Tolerance);
        Assert.Equal(Bottom(graph, 1), target.Y, Tolerance);
    }

    /// <summary>The routed waypoints remain orthogonal after a RIGHT transform.</summary>
    [Fact]
    public void AxisTransform_Apply_Right_ProducesOrthogonalWaypoints() =>
        AssertWaypointsOrthogonal(LayoutDirection.Right);

    /// <summary>The routed waypoints remain orthogonal after a DOWN transform.</summary>
    [Fact]
    public void AxisTransform_Apply_Down_ProducesOrthogonalWaypoints() =>
        AssertWaypointsOrthogonal(LayoutDirection.Down);

    /// <summary>The routed waypoints remain orthogonal after a LEFT transform.</summary>
    [Fact]
    public void AxisTransform_Apply_Left_ProducesOrthogonalWaypoints() =>
        AssertWaypointsOrthogonal(LayoutDirection.Left);

    /// <summary>The routed waypoints remain orthogonal after an UP transform.</summary>
    [Fact]
    public void AxisTransform_Apply_Up_ProducesOrthogonalWaypoints() =>
        AssertWaypointsOrthogonal(LayoutDirection.Up);

    /// <summary>
    ///     Asserts every consecutive segment of the routed polyline is axis-aligned for the given
    ///     direction.
    /// </summary>
    /// <param name="direction">The layout direction under test.</param>
    private static void AssertWaypointsOrthogonal(LayoutDirection direction)
    {
        var graph = PlaceChain(direction);

        var polyline = graph.Waypoints[0];
        for (var i = 0; i + 1 < polyline.Count; i++)
        {
            var dx = Math.Abs(polyline[i + 1].X - polyline[i].X);
            var dy = Math.Abs(polyline[i + 1].Y - polyline[i].Y);
            Assert.True(
                dx < Tolerance || dy < Tolerance,
                $"Segment {i} is not axis-aligned for {direction}: dx={dx}, dy={dy}.");
        }
    }

    /// <summary>Runs the full default pipeline for a two-node chain in the given direction.</summary>
    private static LayeredGraph PlaceChain(LayoutDirection direction)
    {
        var nodes = new List<LayerNode> { new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight) };
        var edges = new List<LayerEdge> { new(0, 1) };
        var graph = new LayeredGraph(nodes, edges, direction);
        var pipeline = LayeredLayoutPipeline.Builder()
            .Direction(direction)
            .Hierarchy(HierarchyHandling.Flat)
            .AddDefaultStages()
            .Build();
        pipeline.Run(graph);
        return graph;
    }

    /// <summary>Returns the first (source) and last (target) waypoint of the given edge.</summary>
    private static (Point2D Source, Point2D Target) EndpointsOf(LayeredGraph graph, int edge)
    {
        var polyline = graph.Waypoints[edge];
        return (polyline[0], polyline[^1]);
    }

    // Node screen rects are always the intrinsic node size drawn at the transformed top-left.
    private static double Left(LayeredGraph graph, int node) => graph.AugX[node];

    private static double Right(LayeredGraph graph, int node) => graph.AugX[node] + NodeWidth;

    private static double Top(LayeredGraph graph, int node) => graph.AugY[node];

    private static double Bottom(LayeredGraph graph, int node) => graph.AugY[node] + NodeHeight;
}
