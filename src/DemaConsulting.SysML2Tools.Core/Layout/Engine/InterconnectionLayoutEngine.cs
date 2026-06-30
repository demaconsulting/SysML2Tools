// <copyright file="InterconnectionLayoutEngine.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Engine.Layered;

using static DemaConsulting.SysML2Tools.Layout.Engine.Layered.LayeredLayoutMetrics;

namespace DemaConsulting.SysML2Tools.Layout.Engine;

/// <summary>
/// A node to be placed by the <see cref="InterconnectionLayoutEngine"/>, identified by its size.
/// </summary>
/// <param name="Width">Width of the node's bounding box in logical pixels.</param>
/// <param name="Height">Height of the node's bounding box in logical pixels.</param>
internal readonly record struct LayerNode(double Width, double Height);

/// <summary>
/// A directed edge (from a source node to a target node, by index) used for layering.
/// </summary>
/// <param name="Source">Index of the source node.</param>
/// <param name="Target">Index of the target node.</param>
internal readonly record struct LayerEdge(int Source, int Target);

/// <summary>
/// The result of an interconnection layout pass.
/// </summary>
/// <param name="Rects">Placed rectangles, one per input node in the same order.</param>
/// <param name="TotalWidth">Total diagram width in logical pixels, including padding.</param>
/// <param name="TotalHeight">Total diagram height in logical pixels, including padding.</param>
/// <param name="NodeLayers">Assigned Sugiyama layer index for each node, in node order.</param>
/// <param name="ConnectorWaypoints">Orthogonal connector waypoints for each acyclic edge.</param>
internal sealed record LayerResult(
    IReadOnlyList<Rect> Rects,
    double TotalWidth,
    double TotalHeight,
    IReadOnlyList<int> NodeLayers,
    IReadOnlyList<IReadOnlyList<Point2D>> ConnectorWaypoints);

/// <summary>
/// Thin façade over the reusable layered layout pipeline (see
/// <see cref="DemaConsulting.SysML2Tools.Layout.Engine.Layered.LayeredLayoutPipeline"/>).
/// Assembles the default ELK-layered stage sequence and adapts its output to the
/// <see cref="LayerResult"/> contract consumed by the interconnection view strategy.
/// </summary>
/// <remarks>
/// All placement and routing logic lives in the individual pipeline stages under
/// <c>Layout/Engine/Layered/</c>. This type exists only to preserve the original public entry
/// point and result shape; it is behavior-preserving with respect to the previous monolithic
/// implementation (verified byte for byte by the pipeline-equivalence tests).
/// </remarks>
internal static class InterconnectionLayoutEngine
{
    /// <summary>
    /// Computes a full Sugiyama layered placement and ELK-style slot routing for the given nodes
    /// and directed edges, returning box positions and orthogonal connector waypoints.
    /// </summary>
    /// <param name="nodes">Input nodes to place, in caller order.</param>
    /// <param name="edges">Directed edges between nodes (by index).</param>
    /// <returns>Placement result with rects, layer assignments, and connector waypoints.</returns>
    public static LayerResult Place(
        IReadOnlyList<LayerNode> nodes,
        IReadOnlyList<LayerEdge> edges)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);

        var n = nodes.Count;
        if (n == 0)
        {
            return new LayerResult([], 2.0 * Padding, 2.0 * Padding, [], []);
        }

        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);
        var pipeline = LayeredLayoutPipeline.Builder()
            .Direction(LayoutDirection.Right)
            .Hierarchy(HierarchyHandling.Flat)
            .AddDefaultStages()
            .Build();
        pipeline.Run(graph);

        var augX = graph.AugX;
        var augY = graph.AugY;
        var columnX = graph.ColumnX;
        var maxColWidth = graph.MaxColWidth;

        // Assemble result.
        var rects = new Rect[n];
        for (var i = 0; i < n; i++)
        {
            rects[i] = new Rect(augX[i], augY[i], nodes[i].Width, nodes[i].Height);
        }

        var lastLayer = columnX.Length - 1;
        var totalWidth = columnX[lastLayer] + maxColWidth[lastLayer] + Padding;
        var totalHeight = Padding;
        for (var i = 0; i < n; i++)
        {
            totalHeight = Math.Max(totalHeight, augY[i] + nodes[i].Height + Padding);
        }

        return new LayerResult(rects, totalWidth, totalHeight, graph.NodeLayers, graph.Waypoints);
    }
}
