// <copyright file="AxisTransformTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Engine;
using DemaConsulting.SysML2Tools.Layout.Engine.Layered;

namespace DemaConsulting.SysML2Tools.Tests.Layout.Engine.Layered;

/// <summary>
///     Tests for <see cref="AxisTransform"/> covering the left-to-right identity behavior and
///     rejection of any other layout direction.
/// </summary>
public sealed class AxisTransformTests
{
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
    ///     A non-left-to-right direction is not yet implemented and throws
    ///     <see cref="NotSupportedException"/>.
    /// </summary>
    [Fact]
    public void AxisTransform_Apply_NonRightDirection_ThrowsNotSupportedException()
    {
        // Arrange: a graph requesting a downward direction.
        var nodes = new List<LayerNode> { new(60, 40) };
        var edges = new List<LayerEdge>();
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Down);

        // Act / Assert: the transform rejects the unsupported direction.
        Assert.Throws<NotSupportedException>(() => new AxisTransform().Apply(graph));
    }
}
