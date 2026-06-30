// <copyright file="ComponentPacker.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using static DemaConsulting.SysML2Tools.Layout.Engine.Layered.LayeredLayoutMetrics;

namespace DemaConsulting.SysML2Tools.Layout.Engine.Layered;

/// <summary>
/// Composite pipeline stage that separates a disconnected graph into its connected components,
/// lays out each component independently with the wrapped inner stage sequence, then packs the
/// laid-out components side by side into a single non-overlapping arrangement.
/// </summary>
/// <remarks>
/// This is a clean-room re-implementation of the documented behavior of ELK's
/// <c>ComponentsProcessor</c>: (a) split the graph into connected components, (b) run the full
/// layered algorithm on each component, then (c) recombine the components by packing their bounding
/// boxes. The default layered pipeline lays a disconnected graph out as if every component shared
/// the same layers, which stacks unrelated subgraphs into one tall column; packing each component
/// separately produces a compact, readable arrangement instead.
/// <para>
/// The single-component case (including a fully connected graph or a lone node) is a transparent
/// pass-through: the inner stages run directly on the supplied graph, so the output is byte-identical
/// to running the same stages without this wrapper. This preserves the behavior of every caller whose
/// graph happens to be connected.
/// </para>
/// <para>
/// Component detection is deterministic — components are ordered by their lowest original node index
/// and nodes within a component by ascending original index — so renders are reproducible.
/// The stage is stateless and may be shared across pipelines.
/// </para>
/// </remarks>
internal sealed class ComponentPacker : ILayoutStage
{
    /// <summary>The inner stages run, in order, against each component sub-graph.</summary>
    private readonly IReadOnlyList<ILayoutStage> _innerStages;

    /// <summary>Gap, in logical pixels, left between adjacent packed components.</summary>
    private readonly double _spacing;

    /// <summary>Target-aspect multiplier used to choose the packing row width.</summary>
    private readonly double _aspect;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComponentPacker"/> class.
    /// </summary>
    /// <param name="innerStages">
    /// The ordered stage sequence to run on each connected component (and on the whole graph in the
    /// single-component fast path). Must not be null. Typically the default ELK-layered sequence.
    /// </param>
    /// <param name="spacing">Gap in logical pixels between adjacent packed components. Must be non-negative.</param>
    /// <param name="aspect">
    /// Target-aspect multiplier controlling the packing row width (larger values produce wider, shorter
    /// arrangements). Must be positive.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerStages"/> is null.</exception>
    public ComponentPacker(IReadOnlyList<ILayoutStage> innerStages, double spacing, double aspect)
    {
        ArgumentNullException.ThrowIfNull(innerStages);
        _innerStages = innerStages;
        _spacing = spacing;
        _aspect = aspect;
    }

    /// <summary>
    /// Creates a <see cref="ComponentPacker"/> wrapping the default ELK-layered inner stage sequence
    /// (cycle breaking, layer assignment, long-edge splitting, crossing minimization, Brandes-Köpf
    /// placement, port distribution, orthogonal routing, long-edge joining, and the axis transform).
    /// </summary>
    /// <param name="spacing">Gap in logical pixels between adjacent packed components.</param>
    /// <param name="aspect">Target-aspect multiplier controlling the packing row width.</param>
    /// <returns>A packer that lays out each component with the standard layered stages.</returns>
    public static ComponentPacker WithDefaultStages(double spacing = NodeSpacing, double aspect = 1.25) =>
        new(DefaultInnerStages(), spacing, aspect);

    /// <inheritdoc/>
    /// <remarks>
    /// Reads <see cref="LayeredGraph.Edges"/> for component detection and writes the real-node outputs
    /// <see cref="LayeredGraph.AugX"/>, <see cref="LayeredGraph.AugY"/>, <see cref="LayeredGraph.NodeLayers"/>,
    /// and <see cref="LayeredGraph.Waypoints"/> (one polyline per original edge). An empty graph
    /// (<see cref="LayeredGraph.N"/> == 0) is a no-op.
    /// </remarks>
    public void Apply(LayeredGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        // An empty graph has nothing to detect, lay out, or pack; the downstream stages would also
        // fail on an empty augmented graph, so short-circuit here.
        if (graph.N == 0)
        {
            return;
        }

        // Partition the real nodes into connected components over the undirected edge set.
        var components = FindComponents(graph);

        // Single-component fast path: run the inner stages directly so the output is byte-identical
        // to the default pipeline. This covers a fully connected graph and a lone node.
        if (components.Count == 1)
        {
            RunInner(graph);
            return;
        }

        LayoutAndPackComponents(graph, components);
    }

    /// <summary>Builds the default ELK-layered inner stage sequence.</summary>
    /// <returns>A fresh ordered list of the standard stages.</returns>
    private static IReadOnlyList<ILayoutStage> DefaultInnerStages() =>
    [
        new CycleBreaker(),
        new LayerAssigner(),
        new LongEdgeSplitter(),
        new CrossingMinimizer(),
        new BrandesKopfPlacer(),
        new PortDistributor(),
        new OrthogonalRouter(),
        new LongEdgeJoiner(),
        new AxisTransform(),
    ];

    /// <summary>
    /// Partitions the real nodes into connected components using union-find over the undirected edge
    /// set (self-loops are ignored as they connect a node only to itself).
    /// </summary>
    /// <param name="graph">The graph whose nodes and edges are partitioned.</param>
    /// <returns>
    /// One list of original node indices per component, components ordered by their lowest original
    /// node index, nodes within each component ordered by ascending original index.
    /// </returns>
    private static List<List<int>> FindComponents(LayeredGraph graph)
    {
        var n = graph.N;
        var parent = new int[n];
        for (var i = 0; i < n; i++)
        {
            parent[i] = i;
        }

        // Union the endpoints of every non-self edge so connected nodes share a representative root.
        foreach (var edge in graph.Edges)
        {
            if (edge.Source != edge.Target)
            {
                Union(parent, edge.Source, edge.Target);
            }
        }

        // Group nodes by their representative root. Iterating nodes in ascending order makes the first
        // node of each component its lowest index, so first-seen root order == ascending-min order.
        var rootToComponent = new Dictionary<int, List<int>>();
        var order = new List<int>();
        for (var node = 0; node < n; node++)
        {
            var root = Find(parent, node);
            if (!rootToComponent.TryGetValue(root, out var members))
            {
                members = [];
                rootToComponent[root] = members;
                order.Add(root);
            }

            members.Add(node);
        }

        return [.. order.Select(root => rootToComponent[root])];
    }

    /// <summary>Finds the representative root of a node with path compression.</summary>
    /// <param name="parent">The union-find parent array.</param>
    /// <param name="node">The node whose root is sought.</param>
    /// <returns>The representative root index of the node's set.</returns>
    private static int Find(int[] parent, int node)
    {
        var root = node;
        while (parent[root] != root)
        {
            root = parent[root];
        }

        // Path compression: point every node on the walk directly at the root.
        while (parent[node] != root)
        {
            var next = parent[node];
            parent[node] = root;
            node = next;
        }

        return root;
    }

    /// <summary>Merges the sets containing two nodes, keeping the lower index as the root.</summary>
    /// <param name="parent">The union-find parent array.</param>
    /// <param name="a">First node.</param>
    /// <param name="b">Second node.</param>
    private static void Union(int[] parent, int a, int b)
    {
        var ra = Find(parent, a);
        var rb = Find(parent, b);
        if (ra == rb)
        {
            return;
        }

        // Keep the lower index as the root so representatives stay deterministic.
        if (ra < rb)
        {
            parent[rb] = ra;
        }
        else
        {
            parent[ra] = rb;
        }
    }

    /// <summary>Runs every wrapped inner stage, in order, against the supplied graph.</summary>
    /// <param name="graph">The graph (whole graph or a component sub-graph) to lay out in place.</param>
    private void RunInner(LayeredGraph graph)
    {
        foreach (var stage in _innerStages)
        {
            stage.Apply(graph);
        }
    }

    /// <summary>
    /// Lays out each component on its own sub-graph, packs the component bounding boxes into shelves,
    /// then merges the placed coordinates and routed waypoints back into the parent graph.
    /// </summary>
    /// <param name="graph">The parent graph whose real-node outputs are written.</param>
    /// <param name="components">The connected components (original node indices) to lay out and pack.</param>
    private void LayoutAndPackComponents(LayeredGraph graph, List<List<int>> components)
    {
        // Lay out every component independently on a remapped sub-graph.
        var layouts = new List<ComponentLayout>(components.Count);
        foreach (var members in components)
        {
            layouts.Add(LayoutComponent(graph, members));
        }

        // Choose a packing row width and assign each component a shelf offset.
        PackComponents(layouts);

        // Merge component placements and routed edges back into the parent graph in original index order.
        var augX = new double[graph.N];
        var augY = new double[graph.N];
        var nodeLayers = new int[graph.N];
        var waypoints = new IReadOnlyList<Point2D>[graph.Edges.Count];

        foreach (var layout in layouts)
        {
            // Translate the component so its content bounding box starts at the assigned shelf offset.
            var dx = -layout.MinX + layout.OffsetX;
            var dy = -layout.MinY + layout.OffsetY;

            for (var local = 0; local < layout.Members.Count; local++)
            {
                var orig = layout.Members[local];
                augX[orig] = layout.LocalX[local] + dx;
                augY[orig] = layout.LocalY[local] + dy;
                nodeLayers[orig] = layout.LocalLayers[local];
            }

            for (var localEdge = 0; localEdge < layout.EdgeOrigIndex.Count; localEdge++)
            {
                var origEdge = layout.EdgeOrigIndex[localEdge];
                var localWaypoints = layout.EdgeWaypoints[localEdge];
                var translated = new Point2D[localWaypoints.Count];
                for (var p = 0; p < localWaypoints.Count; p++)
                {
                    translated[p] = new Point2D(localWaypoints[p].X + dx, localWaypoints[p].Y + dy);
                }

                waypoints[origEdge] = translated;
            }
        }

        graph.AugX = augX;
        graph.AugY = augY;
        graph.NodeLayers = nodeLayers;
        graph.Waypoints = waypoints;
    }

    /// <summary>
    /// Builds a sub-graph for one component (with a local-to-original index remap), runs the inner
    /// stages on it, and captures the placed real-node coordinates, layer assignments, content
    /// bounding box, and routed edge waypoints.
    /// </summary>
    /// <param name="graph">The parent graph supplying node sizes, edges, and flow direction.</param>
    /// <param name="members">Original node indices belonging to this component (ascending order).</param>
    /// <returns>The laid-out component, normalized against its own content bounding box.</returns>
    private ComponentLayout LayoutComponent(LayeredGraph graph, List<int> members)
    {
        // Map original node indices to dense local indices for the sub-graph.
        var origToLocal = new Dictionary<int, int>(members.Count);
        for (var local = 0; local < members.Count; local++)
        {
            origToLocal[members[local]] = local;
        }

        var localNodes = new List<LayerNode>(members.Count);
        foreach (var orig in members)
        {
            localNodes.Add(graph.Nodes[orig]);
        }

        // Collect this component's edges (in original edge order for determinism), remapping endpoints
        // to local indices and recording the original edge index so waypoints can be merged back.
        var localEdges = new List<LayerEdge>();
        var edgeOrigIndex = new List<int>();
        for (var e = 0; e < graph.Edges.Count; e++)
        {
            var edge = graph.Edges[e];
            if (origToLocal.TryGetValue(edge.Source, out var localSource) &&
                origToLocal.TryGetValue(edge.Target, out var localTarget))
            {
                localEdges.Add(new LayerEdge(localSource, localTarget));
                edgeOrigIndex.Add(e);
            }
        }

        // Lay out the component sub-graph with the wrapped inner stages.
        var child = new LayeredGraph(localNodes, localEdges, graph.Direction);
        RunInner(child);

        // Compute the content bounding box over the real nodes only (dummies are excluded).
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;
        var localX = new double[members.Count];
        var localY = new double[members.Count];
        var localLayers = new int[members.Count];
        for (var local = 0; local < members.Count; local++)
        {
            var x = child.AugX[local];
            var y = child.AugY[local];
            localX[local] = x;
            localY[local] = y;
            localLayers[local] = child.NodeLayers[local];
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x + localNodes[local].Width);
            maxY = Math.Max(maxY, y + localNodes[local].Height);
        }

        return new ComponentLayout
        {
            Members = members,
            LocalX = localX,
            LocalY = localY,
            LocalLayers = localLayers,
            MinX = minX,
            MinY = minY,
            Width = maxX - minX,
            Height = maxY - minY,
            EdgeOrigIndex = edgeOrigIndex,
            EdgeWaypoints = child.Waypoints,
        };
    }

    /// <summary>
    /// Assigns each component a shelf offset using a greedy row (shelf) packer. The target row width is
    /// the larger of the widest component and <c>sqrt(totalArea) * aspect</c>, which biases the overall
    /// arrangement toward the requested aspect ratio.
    /// </summary>
    /// <param name="layouts">The laid-out components; their offsets are assigned in place.</param>
    private void PackComponents(List<ComponentLayout> layouts)
    {
        var totalArea = 0.0;
        var widest = 0.0;
        foreach (var layout in layouts)
        {
            totalArea += layout.Width * layout.Height;
            widest = Math.Max(widest, layout.Width);
        }

        var targetRowWidth = Math.Max(widest, Math.Sqrt(totalArea) * _aspect);

        var cursorX = 0.0;
        var shelfTop = 0.0;
        var shelfHeight = 0.0;
        foreach (var layout in layouts)
        {
            // Wrap to a new shelf when the running row width would exceed the target (but never leave a
            // shelf empty — an over-wide component sits alone on its own shelf).
            if (cursorX > 0.0 && cursorX + layout.Width > targetRowWidth)
            {
                shelfTop += shelfHeight + _spacing;
                cursorX = 0.0;
                shelfHeight = 0.0;
            }

            layout.OffsetX = cursorX;
            layout.OffsetY = shelfTop;
            cursorX += layout.Width + _spacing;
            shelfHeight = Math.Max(shelfHeight, layout.Height);
        }
    }

    /// <summary>The laid-out result of one connected component, normalized to its own bounding box.</summary>
    private sealed class ComponentLayout
    {
        /// <summary>Original node indices in this component, ascending.</summary>
        public required IReadOnlyList<int> Members { get; init; }

        /// <summary>Placed X coordinate of each real node, by local index.</summary>
        public required double[] LocalX { get; init; }

        /// <summary>Placed Y coordinate of each real node, by local index.</summary>
        public required double[] LocalY { get; init; }

        /// <summary>Assigned layer of each real node, by local index.</summary>
        public required int[] LocalLayers { get; init; }

        /// <summary>Left edge of the component's content bounding box.</summary>
        public required double MinX { get; init; }

        /// <summary>Top edge of the component's content bounding box.</summary>
        public required double MinY { get; init; }

        /// <summary>Width of the component's content bounding box.</summary>
        public required double Width { get; init; }

        /// <summary>Height of the component's content bounding box.</summary>
        public required double Height { get; init; }

        /// <summary>Original edge index for each local edge, used to merge waypoints back.</summary>
        public required IReadOnlyList<int> EdgeOrigIndex { get; init; }

        /// <summary>Routed waypoints for each local edge, in <see cref="EdgeOrigIndex"/> order.</summary>
        public required IReadOnlyList<IReadOnlyList<Point2D>> EdgeWaypoints { get; init; }

        /// <summary>Packed shelf X offset assigned to this component.</summary>
        public double OffsetX { get; set; }

        /// <summary>Packed shelf Y offset assigned to this component.</summary>
        public double OffsetY { get; set; }
    }
}
