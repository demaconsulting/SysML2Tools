// <copyright file="LayeredGraph.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout.Engine.Layered;

/// <summary>A node in the augmented Sugiyama graph (real part box or long-edge dummy).</summary>
/// <param name="Width">Width of the node's bounding box in logical pixels.</param>
/// <param name="Height">Height of the node's bounding box in logical pixels.</param>
/// <param name="Layer">Assigned Sugiyama layer index.</param>
/// <param name="IsDummy">Whether this node is a zero-size long-edge dummy.</param>
internal sealed record AugNode(double Width, double Height, int Layer, bool IsDummy = false);

/// <summary>A sub-edge in the augmented graph after long-edge splitting.</summary>
/// <param name="Source">Index of the source augmented node.</param>
/// <param name="Target">Index of the target augmented node.</param>
/// <param name="OrigEdgeIndex">Index of the original (pre-split) edge this sub-edge belongs to.</param>
internal readonly record struct AugEdge(int Source, int Target, int OrigEdgeIndex);

/// <summary>
/// The mutable shared state threaded through every <see cref="ILayoutStage"/> of the layered
/// pipeline. Each stage reads the fields produced by earlier stages and writes the fields it owns.
/// </summary>
/// <remarks>
/// This object replaces the ad-hoc local variables that the monolithic interconnection engine
/// passed between its private phase methods, while preserving exactly the same intermediate values
/// (and therefore the same floating-point results).
/// </remarks>
internal sealed class LayeredGraph
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LayeredGraph"/> class.
    /// </summary>
    /// <param name="nodes">Input nodes to place, in caller order.</param>
    /// <param name="edges">Directed edges between nodes (by index).</param>
    /// <param name="direction">The requested layout flow direction.</param>
    public LayeredGraph(
        IReadOnlyList<LayerNode> nodes,
        IReadOnlyList<LayerEdge> edges,
        LayoutDirection direction)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);

        Nodes = nodes;
        Edges = edges;
        Direction = direction;
        N = nodes.Count;
    }

    /// <summary>Gets the number of real input nodes.</summary>
    public int N { get; }

    /// <summary>Gets the input nodes, in caller order.</summary>
    /// <remarks>
    /// The setter is private; the only in-place mutation is <see cref="SwapNodeAxes"/>, the seam
    /// used by <see cref="AxisTransform.NormalizeInputAxes"/> to feed the direction-agnostic stages
    /// node sizes whose along-extent matches the requested flow direction.
    /// </remarks>
    public IReadOnlyList<LayerNode> Nodes { get; private set; }

    /// <summary>Gets the directed input edges (by node index).</summary>
    public IReadOnlyList<LayerEdge> Edges { get; }

    /// <summary>Gets the requested layout flow direction.</summary>
    public LayoutDirection Direction { get; }

    /// <summary>
    /// Gets or sets the minimum straight entry approach reserved for a reversed (back) edge's final
    /// sub-edge — the wrap-around corridor that ends at the true target where the consumer draws the
    /// end marker.
    /// </summary>
    /// <remarks>
    /// The default is <see cref="LayeredLayoutMetrics.ConnectorClearance"/>, which exactly reproduces
    /// the original engine: the router's first slot already starts one
    /// <see cref="LayeredLayoutMetrics.ConnectorClearance"/> past the source column, so the
    /// <c>Math.Max</c> clamp in <see cref="OrthogonalRouter"/> is a no-op at the default and forward
    /// geometry stays byte-identical. A consumer that draws a longer end decoration (for example the
    /// state-transition view's open chevron) raises this so the rounded corner never intrudes into the
    /// decoration.
    /// </remarks>
    public double BackEdgeEntryApproach { get; set; } = LayeredLayoutMetrics.ConnectorClearance;

    /// <summary>Gets or sets the acyclic edge set after cycle breaking.</summary>
    public List<LayerEdge> Acyclic { get; set; } = [];

    /// <summary>
    /// Gets or sets, parallel to <see cref="Acyclic"/> (same index order), whether each retained
    /// acyclic edge was produced by reversing a cycle-causing back edge.
    /// </summary>
    /// <remarks>
    /// <see cref="CycleBreaker"/> records this flag so later stages can recognize edges whose true
    /// direction was flipped for layering. <see cref="OrthogonalRouter"/> reads it to guarantee a
    /// minimum entry approach for the arrowhead that the consumer draws on the (un-reversed) target.
    /// </remarks>
    public bool[] AcyclicReversed { get; set; } = [];

    /// <summary>Gets or sets the assigned layer index for each real node, in node order.</summary>
    public int[] NodeLayers { get; set; } = [];

    /// <summary>Gets or sets the augmented nodes (real boxes followed by long-edge dummies).</summary>
    public List<AugNode> AugNodes { get; set; } = [];

    /// <summary>Gets or sets the augmented sub-edges produced by long-edge splitting.</summary>
    public List<AugEdge> AugEdges { get; set; } = [];

    /// <summary>Gets or sets the augmented-node indices grouped (and ordered) by layer.</summary>
    public List<List<int>> Groups { get; set; } = [];

    /// <summary>Gets or sets the X coordinate of each augmented node.</summary>
    public double[] AugX { get; set; } = [];

    /// <summary>Gets or sets the Y coordinate of each augmented node.</summary>
    public double[] AugY { get; set; } = [];

    /// <summary>Gets or sets the left X coordinate of each layer column.</summary>
    public double[] ColumnX { get; set; } = [];

    /// <summary>Gets or sets the maximum real-node width per layer column.</summary>
    public double[] MaxColWidth { get; set; } = [];

    /// <summary>Gets or sets the source-side (right face) port Y for each augmented sub-edge.</summary>
    public double[] AugPortYSrc { get; set; } = [];

    /// <summary>Gets or sets the target-side (left face) port Y for each augmented sub-edge.</summary>
    public double[] AugPortYTgt { get; set; } = [];

    /// <summary>Gets or sets the orthogonal bend points for each augmented sub-edge.</summary>
    public List<Point2D>[] AugBendPoints { get; set; } = [];

    /// <summary>Gets or sets the assembled orthogonal waypoints for each original (acyclic) edge.</summary>
    public IReadOnlyList<IReadOnlyList<Point2D>> Waypoints { get; set; } = [];

    /// <summary>
    /// Swaps each input node's <see cref="LayerNode.Width"/> and <see cref="LayerNode.Height"/>.
    /// </summary>
    /// <remarks>
    /// The direction-agnostic stages always treat a node's width as its along-axis (layer
    /// progression) extent and its height as its cross-axis (within-layer) extent. For a top-to-bottom
    /// (<see cref="LayoutDirection.Down"/>) or bottom-to-top (<see cref="LayoutDirection.Up"/>) flow,
    /// the along-axis must instead be the node height, so <see cref="AxisTransform.NormalizeInputAxes"/>
    /// calls this seam before the stages run. It is never invoked for the
    /// <see cref="LayoutDirection.Right"/>/<see cref="LayoutDirection.Left"/> paths, which keeps those
    /// outputs byte-identical.
    /// </remarks>
    public void SwapNodeAxes()
    {
        var swapped = new LayerNode[Nodes.Count];
        for (var i = 0; i < Nodes.Count; i++)
        {
            // S2234: width and height are deliberately swapped so the stages space layers by height.
#pragma warning disable S2234
            swapped[i] = new LayerNode(Nodes[i].Height, Nodes[i].Width);
#pragma warning restore S2234
        }

        Nodes = swapped;
    }
}
