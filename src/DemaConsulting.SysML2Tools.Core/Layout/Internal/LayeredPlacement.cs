// <copyright file="LayeredPlacement.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Globalization;

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Layout;

namespace DemaConsulting.SysML2Tools.Layout.Internal;

/// <summary>
/// Result of a layered placement.
/// </summary>
/// <param name="Rects">Placed box rectangles, one per input node in input-node order.</param>
/// <param name="EdgePolylines">
/// Routed orthogonal connector polylines, one per input edge in input-edge order. Each polyline is
/// already oriented source-to-target (the algorithm reverses back edges internally).
/// </param>
/// <param name="Width">Overall content width in logical pixels.</param>
/// <param name="Height">Overall content height in logical pixels.</param>
internal sealed record PlacedLayout(
    IReadOnlyList<Rect> Rects,
    IReadOnlyList<IReadOnlyList<Point2D>> EdgePolylines,
    double Width,
    double Height);

/// <summary>
/// Adapts the DemaConsulting.Rendering layout engine to the SysML view strategies for flat,
/// flow-chart-like diagrams. It builds a <see cref="LayoutGraph"/> from sized nodes and directed
/// edges, lays it out through the public <see cref="LayoutEngine.Layout(LayoutGraph)"/> facade, and
/// returns the placed box rectangles (in input-node order) together with the routed connector
/// polylines (one per input edge, in input-edge order).
/// </summary>
/// <remarks>
/// The graphs built here are always flat (no container nodes), so <see cref="LayoutEngine"/>'s
/// default algorithm — <c>hierarchical</c> — is guaranteed byte-for-byte identical to the bundled
/// <c>layered</c> algorithm applied directly. The primary flow direction is set on the
/// <see cref="LayoutGraph"/> itself via <see cref="IPropertyHolder.Set{TValue}"/>, since
/// <see cref="LayoutEngine.Layout(LayoutGraph)"/> seeds its internal cascade with an empty
/// <see cref="LayoutOptions"/> and only honors settings declared directly on the graph. The
/// algorithm emits exactly one placed box per input node and exactly one routed connector per
/// input edge, preserving the caller's ordering, so callers map results back to their own model by
/// index. Back edges are reversed internally, so every returned polyline runs source-to-target.
/// </remarks>
internal static class LayeredPlacement
{
    /// <summary>
    /// Places sized nodes and directed edges with <see cref="LayoutEngine.Layout(LayoutGraph)"/>.
    /// </summary>
    /// <param name="nodes">Sized nodes to place, in caller order.</param>
    /// <param name="edges">Directed edges between nodes (by index), in caller order.</param>
    /// <param name="direction">Primary flow direction for the layered layout.</param>
    /// <param name="mergeParallelEdges">
    /// Whether multiple edges between the same pair of nodes are merged into a single routed
    /// connector. Defaults to <see langword="true"/> (the library's own default and this method's
    /// original, unconditional behavior), which keeps every pre-existing call site — including
    /// <c>ActionFlowViewLayoutStrategy</c> and <c>StateTransitionViewLayoutStrategy</c> — byte-for-byte
    /// unchanged. Pass <see langword="false"/> to have every parallel edge preserved as its own
    /// independently-routed connector (see <see cref="CoreOptions.MergeParallelEdges"/>), which
    /// <c>InterconnectionViewLayoutStrategy</c> requests so distinct SysML connections between the same
    /// two parts never collapse onto one shared route.
    /// </param>
    /// <returns>The placed rectangles, routed polylines, and overall content size.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="nodes"/> or <paramref name="edges"/> is <see langword="null"/>.
    /// </exception>
    public static PlacedLayout Place(
        IReadOnlyList<(double Width, double Height)> nodes,
        IReadOnlyList<(int From, int To)> edges,
        LayoutFlowDirection direction,
        bool mergeParallelEdges = true)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);

        var graph = new LayoutGraph();
        var graphNodes = new LayoutGraphNode[nodes.Count];
        for (var i = 0; i < nodes.Count; i++)
        {
            graphNodes[i] = graph.AddNode(
                i.ToString(CultureInfo.InvariantCulture), nodes[i].Width, nodes[i].Height);
        }

        for (var e = 0; e < edges.Count; e++)
        {
            var (from, to) = edges[e];
            graph.AddEdge(e.ToString(CultureInfo.InvariantCulture), graphNodes[from], graphNodes[to]);
        }

        graph.Set(CoreOptions.Direction, direction);
        if (!mergeParallelEdges)
        {
            graph.Set(CoreOptions.MergeParallelEdges, false);
        }

        var tree = LayoutEngine.Layout(graph);

        var boxes = tree.Nodes.OfType<LayoutBox>().ToList();
        var lines = tree.Nodes.OfType<LayoutLine>().ToList();

        var rects = new Rect[boxes.Count];
        for (var i = 0; i < boxes.Count; i++)
        {
            rects[i] = new Rect(boxes[i].X, boxes[i].Y, boxes[i].Width, boxes[i].Height);
        }

        var polylines = new IReadOnlyList<Point2D>[lines.Count];
        for (var i = 0; i < lines.Count; i++)
        {
            polylines[i] = lines[i].Waypoints;
        }

        return new PlacedLayout(rects, polylines, tree.Width, tree.Height);
    }
}
