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
/// Result of a port-aware layered placement (see <see cref="LayeredPlacement.PlaceWithPorts"/>).
/// </summary>
/// <param name="Rects">Placed box rectangles, one per input node in input-node order.</param>
/// <param name="EdgePolylines">
/// Routed orthogonal connector polylines, one per input edge in input-edge order. Each polyline is
/// already oriented source-to-target (the algorithm reverses back edges internally).
/// </param>
/// <param name="EdgePorts">
/// The placed source/target ports, one pair per input edge in input-edge order. An element is
/// <see langword="null"/> when the corresponding <see cref="PortEdge.SourcePort"/> or
/// <see cref="PortEdge.TargetPort"/> was itself <see langword="null"/> (no port requested for that
/// endpoint; the edge attaches to the plain node instead).
/// </param>
/// <param name="Width">Overall content width in logical pixels.</param>
/// <param name="Height">Overall content height in logical pixels.</param>
internal sealed record PlacedPortLayout(
    IReadOnlyList<Rect> Rects,
    IReadOnlyList<IReadOnlyList<Point2D>> EdgePolylines,
    IReadOnlyList<(LayoutPort? Source, LayoutPort? Target)> EdgePorts,
    double Width,
    double Height);

/// <summary>
/// Requests that an edge endpoint attach through a named port on its node rather than to the node
/// as a whole, so a caller can label the specific connection point on the node's boundary.
/// </summary>
/// <param name="Label">
/// Optional external label rendered beside the port (see
/// <see cref="LayoutGraphPort.ExternalLabel"/>). <see langword="null"/> when no label should be
/// shown; a port is still created and placed in that case.
/// </param>
internal sealed record EdgePortRef(string? Label);

/// <summary>
/// A directed edge between two nodes (by index), with an optional named-port request at either
/// endpoint. A <see langword="null"/> <see cref="SourcePort"/> or <see cref="TargetPort"/> means
/// that endpoint attaches directly to the node itself rather than through a named port.
/// </summary>
/// <param name="From">Index of the source node.</param>
/// <param name="To">Index of the target node.</param>
/// <param name="SourcePort">Optional named-port request at the source endpoint.</param>
/// <param name="TargetPort">Optional named-port request at the target endpoint.</param>
internal sealed record PortEdge(int From, int To, EdgePortRef? SourcePort, EdgePortRef? TargetPort);

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

    /// <summary>
    /// Places sized nodes and directed edges with <see cref="LayoutEngine.Layout(LayoutGraph)"/>,
    /// attaching each edge endpoint through a named <see cref="LayoutGraphPort"/> when requested so
    /// the engine itself resolves port sides, spacing, and any box-height growth needed to keep
    /// distinct connections visually distinct. This is a second, additive entry point:
    /// <see cref="Place"/> itself is unmodified and remains the entry point for callers that attach
    /// edges directly to nodes.
    /// </summary>
    /// <param name="nodes">
    /// Sized nodes to place, in caller order. <c>HasLabel</c>/<c>HasKeyword</c> indicate whether the
    /// node carries a title (a name and/or keyword) that will be rendered on the box; when either is
    /// <see langword="true"/>, the created <see cref="LayoutGraphNode"/>'s corresponding
    /// <see cref="LayoutGraphNode.Label"/>/<see cref="LayoutGraphNode.Keyword"/> is set to a non-null
    /// sentinel (<see cref="string.Empty"/>) so the layered algorithm's automatic title-vs-side-port
    /// reservation activates for that node (it keys only on whether <c>Label</c>/<c>Keyword</c> are
    /// non-null, not their text) and no named port is ever placed across the box's own title band.
    /// </param>
    /// <param name="edges">Directed edges between nodes (by index), with optional named-port requests at either endpoint, in caller order.</param>
    /// <param name="direction">Primary flow direction for the layered layout.</param>
    /// <returns>
    /// The placed rectangles, routed polylines, correlated placed ports (one pair per input edge),
    /// and overall content size.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="nodes"/> or <paramref name="edges"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// Every edge's endpoint ports are given a unique name within their owning node,
    /// <c>"{edgeIndex}-a"</c> for the source and <c>"{edgeIndex}-b"</c> for the target, so multiple
    /// connections into the same node never collide. <see cref="CoreOptions.MergeParallelEdges"/> is
    /// unconditionally set to <see langword="false"/> (unlike <see cref="Place"/>'s optional
    /// parameter), since every current caller of this method needs parallel-edge preservation.
    /// </remarks>
    public static PlacedPortLayout PlaceWithPorts(
        IReadOnlyList<(double Width, double Height, bool HasLabel, bool HasKeyword)> nodes,
        IReadOnlyList<PortEdge> edges,
        LayoutFlowDirection direction)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);

        var graph = new LayoutGraph();
        var graphNodes = new LayoutGraphNode[nodes.Count];
        for (var i = 0; i < nodes.Count; i++)
        {
            graphNodes[i] = graph.AddNode(
                i.ToString(CultureInfo.InvariantCulture), nodes[i].Width, nodes[i].Height);

            // Give the node a non-null Label/Keyword sentinel (not the real title text, which
            // LayeredPlacement never carries) purely so the layered algorithm's automatic
            // title-vs-side-port reservation (keyed on whether Label/Keyword are non-null) activates
            // for nodes that will actually render a title, keeping ports clear of the title band.
            if (nodes[i].HasLabel)
            {
                graphNodes[i].Label = string.Empty;
            }

            if (nodes[i].HasKeyword)
            {
                graphNodes[i].Keyword = string.Empty;
            }
        }

        // For each edge, resolve the source/target ILayoutConnectable endpoints, creating a named
        // port on the owning node when the caller requested one, and remembering the created
        // LayoutGraphPort (if any) so the placed LayoutPort can be correlated back to it below.
        var sourcePorts = new LayoutGraphPort?[edges.Count];
        var targetPorts = new LayoutGraphPort?[edges.Count];
        for (var e = 0; e < edges.Count; e++)
        {
            var edge = edges[e];

            ILayoutConnectable source = graphNodes[edge.From];
            if (edge.SourcePort is { } sourceRef)
            {
                var port = graphNodes[edge.From].Ports.AddPort(
                    e.ToString(CultureInfo.InvariantCulture) + "-a");
                port.ExternalLabel = sourceRef.Label;
                sourcePorts[e] = port;
                source = port;
            }

            ILayoutConnectable target = graphNodes[edge.To];
            if (edge.TargetPort is { } targetRef)
            {
                var port = graphNodes[edge.To].Ports.AddPort(
                    e.ToString(CultureInfo.InvariantCulture) + "-b");
                port.ExternalLabel = targetRef.Label;
                targetPorts[e] = port;
                target = port;
            }

            graph.AddEdge(e.ToString(CultureInfo.InvariantCulture), source, target);
        }

        graph.Set(CoreOptions.Direction, direction);
        graph.Set(CoreOptions.MergeParallelEdges, false);

        var tree = LayoutEngine.Layout(graph);

        var boxes = tree.Nodes.OfType<LayoutBox>().ToList();
        var lines = tree.Nodes.OfType<LayoutLine>().ToList();
        var ports = tree.Nodes.OfType<LayoutPort>().ToList();

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

        var edgePorts = new (LayoutPort? Source, LayoutPort? Target)[edges.Count];
        for (var e = 0; e < edges.Count; e++)
        {
            LayoutPort? source = null;
            LayoutPort? target = null;
            if (sourcePorts[e] is { } wantedSource)
            {
                source = ports.Find(p => ReferenceEquals(p.SourcePort, wantedSource));
            }

            if (targetPorts[e] is { } wantedTarget)
            {
                target = ports.Find(p => ReferenceEquals(p.SourcePort, wantedTarget));
            }

            edgePorts[e] = (source, target);
        }

        return new PlacedPortLayout(rects, polylines, edgePorts, tree.Width, tree.Height);
    }
}
