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
}
