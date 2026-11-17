// <copyright file="LayeredPlacement.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Globalization;

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
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
/// <param name="Lines">
/// The connector lines exactly as the engine emitted them, one per input edge in input-edge order,
/// carrying the end markers, line style, midpoint label, and end labels declared on the
/// <see cref="PortEdge"/> (so the caller does not rebuild them). The waypoints equal
/// <paramref name="EdgePolylines"/>.
/// </param>
/// <param name="Ports">
/// Every placed port glyph the engine emitted, in engine order. A port shared by several edges (the
/// same <see cref="EdgePortRef.Key"/> on the same node) yields one placed port per edge end, all with
/// the same <see cref="LayoutPort.SourcePort"/>; callers that want a single label per declared port keep
/// it on the first of them (see <see cref="LayeredPlacement.PlaceWithPorts"/>).
/// </param>
/// <param name="Width">Overall content width in logical pixels.</param>
/// <param name="Height">Overall content height in logical pixels.</param>
internal sealed record PlacedPortLayout(
    IReadOnlyList<Rect> Rects,
    IReadOnlyList<IReadOnlyList<Point2D>> EdgePolylines,
    IReadOnlyList<LayoutLine> Lines,
    IReadOnlyList<LayoutPort> Ports,
    double Width,
    double Height);

/// <summary>
/// Requests that an edge endpoint attach through a named port on its node rather than to the node
/// as a whole, so a caller can draw and label the specific connection point on the node's boundary.
/// </summary>
/// <param name="Label">
/// Optional external label rendered beside the port (see
/// <see cref="LayoutGraphPort.ExternalLabel"/>). <see langword="null"/> when no label should be
/// shown; a port is still created and placed in that case.
/// </param>
/// <param name="Appearance">
/// Optional glyph, direction arrow, outline style, and label placement of the port (see
/// <see cref="PortAppearance"/>). <see langword="null"/> keeps the engine's legacy filled square.
/// </param>
/// <param name="Key">
/// Optional identity of the port on its node. Edges that name the same key on the same node share one
/// <see cref="LayoutGraphPort"/> (a declared SysML port with several connectors is one port); the first
/// reference supplies the label and appearance. <see langword="null"/> (the default) gives the edge
/// end a port of its own, as before.
/// </param>
internal sealed record EdgePortRef(string? Label, PortAppearance? Appearance = null, string? Key = null);

/// <summary>
/// A directed edge between two nodes (by index), with an optional named-port request at either
/// endpoint. A <see langword="null"/> <see cref="SourcePort"/> or <see cref="TargetPort"/> means
/// that endpoint attaches directly to the node itself rather than through a named port.
/// </summary>
/// <param name="From">Index of the source node.</param>
/// <param name="To">Index of the target node.</param>
/// <param name="SourcePort">Optional named-port request at the source endpoint.</param>
/// <param name="TargetPort">Optional named-port request at the target endpoint.</param>
/// <param name="TargetEnd">
/// End decoration at the target end (see <see cref="LayoutGraphEdge.TargetEnd"/>); the layered
/// algorithm reads it to size the final approach into the target.
/// </param>
/// <param name="LineStyle">Stroke style of the connector.</param>
/// <param name="SourceEndLabel">Role name and multiplicity beside the source end, or <see langword="null"/>.</param>
/// <param name="TargetEndLabel">Role name and multiplicity beside the target end, or <see langword="null"/>.</param>
/// <param name="MidpointLabel">Text centered above the connector, or <see langword="null"/>.</param>
internal sealed record PortEdge(
    int From,
    int To,
    EdgePortRef? SourcePort,
    EdgePortRef? TargetPort,
    EndMarkerStyle TargetEnd = EndMarkerStyle.None,
    LineStyle LineStyle = LineStyle.Solid,
    LineEndLabel? SourceEndLabel = null,
    LineEndLabel? TargetEndLabel = null,
    string? MidpointLabel = null);
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
    /// connector. Defaults to <see langword="true"/> (the library's own default). Pass
    /// <see langword="false"/> to have every parallel edge preserved as its own independently-routed
    /// connector (see <see cref="CoreOptions.MergeParallelEdges"/>). Every current caller —
    /// <c>ActionFlowViewLayoutStrategy</c>, <c>StateTransitionViewLayoutStrategy</c>, and
    /// <c>InterconnectionViewLayoutStrategy</c> — passes <see langword="false"/>: each treats
    /// <see cref="PlacedLayout.EdgePolylines"/> as 1:1 with <paramref name="edges"/> by index, which
    /// only holds when no two edges are collapsed onto one shared line. Leaving the parameter's
    /// default at <see langword="true"/> for a caller whose edges can be cyclic or parallel (e.g. a
    /// back edge sharing a node pair with a forward edge) will corrupt that indexing — collapsing
    /// yields fewer entries in <see cref="PlacedLayout.EdgePolylines"/> than <paramref name="edges"/>.
    /// </param>
    /// <param name="edgeTargetEnd">
    /// The end decoration declared on every edge's target end (see
    /// <see cref="LayoutGraphEdge.TargetEnd"/>). Defaults to <see cref="EndMarkerStyle.None"/>. The
    /// layered algorithm reads the decoration to size the final approach of a back edge (the straight
    /// run into the target must be at least as long as the marker), so a caller that draws an
    /// arrowhead at the target should pass the same style it puts on the drawn line.
    /// </param>
    /// <returns>The placed rectangles, routed polylines, and overall content size.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="nodes"/> or <paramref name="edges"/> is <see langword="null"/>.
    /// </exception>
    public static PlacedLayout Place(
        IReadOnlyList<(double Width, double Height)> nodes,
        IReadOnlyList<(int From, int To)> edges,
        LayoutFlowDirection direction,
        bool mergeParallelEdges = true,
        EndMarkerStyle edgeTargetEnd = EndMarkerStyle.None)
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
            var edge = graph.AddEdge(e.ToString(CultureInfo.InvariantCulture), graphNodes[from], graphNodes[to]);
            edge.TargetEnd = edgeTargetEnd;
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
    /// The placed rectangles, the engine's connector lines (end markers, labels, and style as declared
    /// on each edge), every placed port, and the overall content size.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="nodes"/> or <paramref name="edges"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// An endpoint port with a <see cref="EdgePortRef.Key"/> is created once per node and key, so
    /// several connectors that name the same port of a node attach to one <see cref="LayoutGraphPort"/>
    /// (the first reference supplies its label and appearance). The layout engine (0.3.0-beta.4) still
    /// emits one placed port per edge end for such a shared port, each repeating the label; this method
    /// keeps the label on the first placed port of every shared <see cref="LayoutGraphPort"/> and clears
    /// it on the others, so a declared port is named once. An endpoint without a key gets a port named
    /// <c>"{edgeIndex}-a"</c> (source) or <c>"{edgeIndex}-b"</c> (target), unique within its node.
    /// <see cref="CoreOptions.MergeParallelEdges"/> is unconditionally set to <see langword="false"/>
    /// (unlike <see cref="Place"/>'s optional parameter), since every current caller of this method needs
    /// parallel-edge preservation.
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
        // port on the owning node when the caller requested one. A keyed request reuses the node's
        // existing port of that key, so every connector of one declared port shares it.
        var sharedPorts = new Dictionary<(int Node, string Key), LayoutGraphPort>();
        for (var e = 0; e < edges.Count; e++)
        {
            var edge = edges[e];
            var source = ResolveEndpoint(graphNodes[edge.From], edge.From, edge.SourcePort, e + "-a", sharedPorts);
            var target = ResolveEndpoint(graphNodes[edge.To], edge.To, edge.TargetPort, e + "-b", sharedPorts);

            var graphEdge = graph.AddEdge(e.ToString(CultureInfo.InvariantCulture), source, target);
            graphEdge.TargetEnd = edge.TargetEnd;
            graphEdge.LineStyle = edge.LineStyle;
            graphEdge.SourceEndLabel = edge.SourceEndLabel;
            graphEdge.TargetEndLabel = edge.TargetEndLabel;
            graphEdge.Label = edge.MidpointLabel;
        }

        graph.Set(CoreOptions.Direction, direction);
        graph.Set(CoreOptions.MergeParallelEdges, false);
        graph.Set(CoreOptions.Algorithm, AutoLayoutAlgorithm.AlgorithmId);

        var tree = LayoutEngine.Layout(graph);

        var boxes = tree.Nodes.OfType<LayoutBox>().ToList();
        var lines = tree.Nodes.OfType<LayoutLine>().ToList();

        // A shared port is emitted once per edge end; name it only on its first placed glyph.
        var named = new HashSet<LayoutGraphPort>(ReferenceEqualityComparer.Instance);
        var ports = new List<LayoutPort>();
        foreach (var port in tree.Nodes.OfType<LayoutPort>())
        {
            var repeated = port.SourcePort is not null && !named.Add(port.SourcePort);
            ports.Add(repeated ? port with { ExternalLabel = null, InternalLabel = null } : port);
        }

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

        return new PlacedPortLayout(rects, polylines, lines, ports, tree.Width, tree.Height);
    }

    /// <summary>
    /// Resolves one edge endpoint to the node itself (no port requested) or to a named port on it,
    /// creating the port on first use and reusing it for every later request with the same key.
    /// </summary>
    /// <param name="node">The graph node the endpoint belongs to.</param>
    /// <param name="nodeIndex">Index of <paramref name="node"/>, part of the shared-port identity.</param>
    /// <param name="request">The caller's port request, or <see langword="null"/> to attach to the node.</param>
    /// <param name="uniqueId">The port id used when the request carries no key.</param>
    /// <param name="sharedPorts">The keyed ports created so far, by node index and key.</param>
    /// <returns>The node or port the edge should attach to.</returns>
    private static ILayoutConnectable ResolveEndpoint(
        LayoutGraphNode node,
        int nodeIndex,
        EdgePortRef? request,
        string uniqueId,
        Dictionary<(int Node, string Key), LayoutGraphPort> sharedPorts)
    {
        if (request is null)
        {
            return node;
        }

        if (request.Key is { } key && sharedPorts.TryGetValue((nodeIndex, key), out var existing))
        {
            return existing;
        }

        var port = node.Ports.AddPort(request.Key is { } keyed ? "k:" + keyed : uniqueId);
        port.ExternalLabel = request.Label;
        port.Appearance = request.Appearance;
        if (request.Key is { } newKey)
        {
            sharedPorts[(nodeIndex, newKey)] = port;
        }

        return port;
    }

    /// <summary>
    /// Upper bound on the number of corridor-widening passes of <see cref="WidenForMidpointLabels"/>, so
    /// the pass always terminates even when a label cannot be separated.
    /// </summary>
    private const int MaxWideningPasses = 12;

    /// <summary>Clearance, in logical pixels, a widened corridor keeps between a midpoint label and what it was overlapping.</summary>
    private const double WideningClearance = 6.0;

    /// <summary>
    /// Vertical padding, in logical pixels, added when testing whether a midpoint label touches another
    /// label: the renderers draw an opaque background taller than the text rectangle the placer reasons
    /// about, so labels that are merely close would still hide each other.
    /// </summary>
    private const double LabelBackgroundPad = 4.0;

    /// <summary>
    /// Widens the corridor in front of a connector's target (or source, for a back edge) until the
    /// connector's midpoint label no longer overlaps a box, a port glyph, or an outward port label.
    /// </summary>
    /// <remarks>
    /// The layered algorithm reserves room between two columns of boxes for the end labels and the outward
    /// port labels of the connectors that cross it, but not for a connector's midpoint label (which the
    /// label placer centers on the connector's longest run). When the corridor is only just wide enough for
    /// the port labels at both ends, a midpoint label such as <c>«flow» torque of Torque</c> therefore
    /// lands on top of them. This pass resolves the labels with the same placer the renderers use, finds
    /// the first midpoint label that overlaps something it must not, and shifts every box, port, and
    /// waypoint at or to the right of the right-hand endpoint's column by enough to clear it, repeating
    /// (bounded by <see cref="MaxWideningPasses"/>) until no label overlaps. A connector whose endpoints
    /// share a column cannot be separated this way and is left alone. A layout with no midpoint labels, or
    /// whose labels are already clear, is returned unchanged (the same instance).
    /// </remarks>
    /// <param name="placed">The placed layout to widen, from <see cref="PlaceWithPorts"/>.</param>
    /// <param name="edges">The edges the layout was placed from, in the same order as <see cref="PlacedPortLayout.Lines"/>.</param>
    /// <param name="theme">The theme the layout will be rendered with (font size and marker scale).</param>
    /// <returns>The widened layout, or <paramref name="placed"/> itself when nothing overlaps.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is <see langword="null"/>.</exception>
    public static PlacedPortLayout WidenForMidpointLabels(PlacedPortLayout placed, IReadOnlyList<PortEdge> edges, Theme theme)
    {
        ArgumentNullException.ThrowIfNull(placed);
        ArgumentNullException.ThrowIfNull(edges);
        ArgumentNullException.ThrowIfNull(theme);

        if (!placed.Lines.Any(l => l.MidpointLabel is not null))
        {
            return placed;
        }

        var current = placed;
        var separated = new HashSet<int>();
        for (var pass = 0; pass < MaxWideningPasses; pass++)
        {
            var boxes = current.Rects
                .Select(r => new LayoutBox(r.X, r.Y, r.Width, r.Height, string.Empty, 0, BoxShape.Rectangle, [], []))
                .ToList();
            var labels = ConnectorLabelPlacer.ResolveLabels(current.Lines, boxes, current.Ports, theme);

            // Find the first labeled connector whose label overlaps a box, port glyph, or port label.
            var violation = FindLabelOverlap(current, edges, labels, separated);
            if (violation is null)
            {
                break;
            }

            var (rightNode, shift) = violation.Value;
            current = ShiftRightOf(current, rightNode, shift);
        }

        return current;
    }

    /// <summary>
    /// Finds the first midpoint label of a placed layout that overlaps a box or a port rectangle and whose
    /// connector joins two different columns, returning the node whose column to shift right and by how
    /// much; null when every label is clear. A connector whose endpoints share a column is recorded in
    /// <paramref name="separated"/> and skipped from then on.
    /// </summary>
    private static (int RightNode, double Shift)? FindLabelOverlap(
        PlacedPortLayout placed,
        IReadOnlyList<PortEdge> edges,
        ConnectorLabelLayout labels,
        HashSet<int> separated)
    {
        var boxRects = placed.Rects
            .Select(r => new LabelPlacement(r.X + (r.Width / 2.0), r.Y + (r.Height / 2.0), r.Width / 2.0, r.Height / 2.0))
            .Concat(labels.PortRects)
            .ToList();

        for (var k = 0; k < placed.Lines.Count && k < edges.Count; k++)
        {
            if (separated.Contains(k) || !labels.Midpoints.TryGetValue(placed.Lines[k], out var label))
            {
                continue;
            }

            // The deepest horizontal penetration into any obstacle decides how far to open the corridor.
            var penetration = boxRects.Max(obstacle => HorizontalPenetration(label, obstacle));
            if (penetration <= 0.0)
            {
                continue;
            }

            // The right-hand endpoint's column moves; endpoints in one column cannot be separated.
            var (from, to) = (placed.Rects[edges[k].From], placed.Rects[edges[k].To]);
            var rightNode = from.X >= to.X ? edges[k].From : edges[k].To;
            var leftRect = from.X >= to.X ? to : from;
            if (placed.Rects[rightNode].X < leftRect.X + leftRect.Width)
            {
                separated.Add(k);
                continue;
            }

            // The label sits at the middle of the run, so stretching the run by twice the overlap moves
            // it clear of an obstacle at either end.
            return (rightNode, 2.0 * (penetration + WideningClearance));
        }

        return null;
    }

    /// <summary>
    /// Returns how far two rectangles overlap horizontally, or zero when they do not overlap at all (in
    /// either axis). The rectangles are given as a center and half extents.
    /// </summary>
    private static double HorizontalPenetration(LabelPlacement a, LabelPlacement b)
    {
        var overlapsVertically = Math.Abs(a.Y - b.Y) < a.HalfHeight + b.HalfHeight + LabelBackgroundPad;
        var overlap = (a.HalfWidth + b.HalfWidth) - Math.Abs(a.X - b.X);
        return overlapsVertically && overlap > 0.0 ? overlap : 0.0;
    }

    /// <summary>
    /// Shifts every box, port, and connector waypoint in the column of <paramref name="node"/> and in every
    /// column to its right by <paramref name="shift"/> pixels to the right, and widens the layout to match.
    /// The cut is the left edge of the leftmost box overlapping the node's own horizontal extent (its
    /// column), so boxes of one column move together.
    /// </summary>
    private static PlacedPortLayout ShiftRightOf(PlacedPortLayout placed, int node, double shift)
    {
        var anchor = placed.Rects[node];
        var cut = placed.Rects
            .Where(r => r.X < anchor.X + anchor.Width && r.X + r.Width > anchor.X)
            .Min(r => r.X) - 1e-6;

        double ShiftX(double x) => x >= cut ? x + shift : x;

        var rects = placed.Rects.Select(r => r.X >= cut ? new Rect(r.X + shift, r.Y, r.Width, r.Height) : r).ToList();
        var lines = placed.Lines
            .Select(l => l with { Waypoints = l.Waypoints.Select(p => new Point2D(ShiftX(p.X), p.Y)).ToList() })
            .ToList();
        var ports = placed.Ports.Select(p => p with { CentreX = ShiftX(p.CentreX) }).ToList();
        return new PlacedPortLayout(
            rects,
            lines.Select(l => l.Waypoints).ToList(),
            lines,
            ports,
            placed.Width + shift,
            placed.Height);
    }
}
