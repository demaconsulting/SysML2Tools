// <copyright file="LayeredPlacer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout.Engine;

/// <summary>
/// Describes a node to be placed by <see cref="LayeredPlacer"/>: its intrinsic width and height.
/// </summary>
/// <param name="Width">Intrinsic width of the node in logical pixels.</param>
/// <param name="Height">Intrinsic height of the node in logical pixels.</param>
internal readonly record struct LayerNode(double Width, double Height);

/// <summary>
/// Describes an undirected edge between two nodes, identified by their zero-based indices in the
/// input node list supplied to <see cref="LayeredPlacer.Place"/>.
/// </summary>
/// <param name="Source">Zero-based index of one endpoint node.</param>
/// <param name="Target">Zero-based index of the other endpoint node.</param>
internal readonly record struct LayerEdge(int Source, int Target);

/// <summary>
/// The placement result produced by <see cref="LayeredPlacer.Place"/>: absolute rectangles for
/// every node, the bounding box of the whole layout, and the layer assignment of every node.
/// </summary>
/// <param name="Rects">
/// Absolute position and size of each node, in input-index order. Rects are non-overlapping by
/// construction: each layer occupies a separate X column and nodes within a column are stacked
/// with the caller-supplied <c>nodeSpacing</c> gap.
/// </param>
/// <param name="TotalWidth">
/// Width of the bounding box that encloses all node rectangles, plus one <c>clearance</c> margin
/// on the right. Use this to size the container that hosts the placed nodes.
/// </param>
/// <param name="TotalHeight">
/// Height of the bounding box that encloses all node rectangles, plus one <c>clearance</c> margin
/// at the bottom.
/// </param>
/// <param name="NodeLayers">
/// Layer index assigned to each node, in input-index order. Layer 0 is the leftmost column; layer
/// 1 is one corridor to the right, and so on. Callers use this to determine which face of each
/// box its inter-layer connectors should leave from and to compute slot X coordinates.
/// </param>
internal sealed record LayerResult(
    IReadOnlyList<Rect> Rects,
    double TotalWidth,
    double TotalHeight,
    IReadOnlyList<int> NodeLayers);

/// <summary>
/// BFS-layered node placement engine for interconnection diagrams.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="LayeredPlacer"/> places a set of nodes in left-to-right layers using a six-step
/// algorithm: (1) degree-based BFS layering seeds the highest-degree node in layer 0 and spreads
/// each unvisited neighbour one layer to the right; (2) a single barycentric ordering pass within
/// each layer reduces visual edge crossings; (3) inter-layer corridor widths are computed
/// proportional to the number of edges crossing each gap; (4) layer column X coordinates are
/// derived from node widths and corridor widths; (5) within each column nodes are centred
/// vertically relative to the tallest column; (6) the result records are assembled with bounding-
/// box totals that include a clearance margin.
/// </para>
/// <para>
/// All steps are pure, deterministic, and thread-safe — no mutable static state is shared between
/// calls. The engine consumes and returns plain geometric value types (<see cref="LayerNode"/>,
/// <see cref="LayerEdge"/>, <see cref="Rect"/>) and has no dependency on the SysML semantic model.
/// </para>
/// </remarks>
internal static class LayeredPlacer
{
    /// <summary>
    /// Places the given nodes in BFS-derived layers and returns their absolute rectangles together
    /// with the bounding-box totals and layer assignments needed for slot-based connector routing.
    /// </summary>
    /// <remarks>
    /// When <paramref name="nodes"/> is empty the method returns a zero-size result with empty
    /// lists without performing any computation. Self-loops and edges with out-of-range indices are
    /// silently ignored. Nodes that are unreachable from the highest-degree seed (e.g. isolated
    /// nodes or disconnected components) are assigned layer 0.
    /// </remarks>
    /// <param name="nodes">Nodes to place. An empty list returns a zero-size result immediately.</param>
    /// <param name="edges">
    /// Undirected connections between nodes by index. Self-loops and out-of-range indices are
    /// silently ignored. Duplicate edges between the same pair are accepted and counted
    /// independently for corridor-width scaling.
    /// </param>
    /// <param name="nodeSpacing">Minimum vertical gap between stacked nodes within the same layer column. Default: 20.0.</param>
    /// <param name="minCorridorWidth">
    /// Minimum width of each inter-layer corridor regardless of edge count. Default: 60.0.
    /// </param>
    /// <param name="edgeSpacing">
    /// Additional corridor width per edge crossing that corridor, so denser corridors stay wider.
    /// Default: 12.0.
    /// </param>
    /// <param name="clearance">
    /// Gap appended to the right of the last column and to the bottom of the tallest column, so
    /// callers have room to draw connector stubs. Also contributes to the minimum corridor width
    /// formula as 2 × clearance. Default: 10.0.
    /// </param>
    /// <returns>
    /// A <see cref="LayerResult"/> with one <see cref="Rect"/> per node in input-index order,
    /// <see cref="LayerResult.TotalWidth"/> and <see cref="LayerResult.TotalHeight"/> for
    /// container sizing, and <see cref="LayerResult.NodeLayers"/> for connector routing.
    /// </returns>
    public static LayerResult Place(
        IReadOnlyList<LayerNode> nodes,
        IReadOnlyList<LayerEdge> edges,
        double nodeSpacing = 20.0,
        double minCorridorWidth = 60.0,
        double edgeSpacing = 12.0,
        double clearance = 10.0)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);

        // Degenerate case: nothing to place; return a well-typed empty result
        if (nodes.Count == 0)
        {
            return new LayerResult([], 0.0, 0.0, []);
        }

        // Step 1: Build adjacency lists and derive degree per node from validated edges
        var adjacency = BuildAdjacency(nodes.Count, edges);
        var degrees = adjacency.Select(a => a.Count).ToArray();

        // Step 1 (continued): BFS layering — seed the highest-degree node, spread outward
        var nodeLayers = BfsLayers(nodes.Count, adjacency, degrees);
        var maxLayer = nodeLayers.Max();

        // Group node indices by their assigned layer for subsequent steps
        var layerGroups = GroupByLayer(nodes.Count, nodeLayers, maxLayer);

        // Step 2: Barycentric ordering — sort within each layer to reduce visual edge crossings
        BarycentricOrder(layerGroups, adjacency);

        // Step 3: Corridor widths — scale proportionally with the number of edges crossing each gap
        var corridorWidths = ComputeCorridorWidths(maxLayer, nodeLayers, edges, minCorridorWidth, edgeSpacing, clearance);

        // Step 4: X assignment — left edge of each layer column
        var colX = ComputeColumnX(maxLayer, layerGroups, nodes, corridorWidths);

        // Step 5: Y assignment — centre each column vertically within the tallest column
        var rects = ComputeRects(maxLayer, layerGroups, nodes, colX, nodeSpacing);

        // Step 6: Return bounding-box totals (rightmost/lowest rect edge plus clearance margin)
        var totalWidth = rects.Max(r => r.X + r.Width) + clearance;
        var totalHeight = rects.Max(r => r.Y + r.Height) + clearance;

        return new LayerResult(rects, totalWidth, totalHeight, nodeLayers);
    }

    /// <summary>
    /// Builds an undirected adjacency list per node. Self-loops and out-of-range indices are
    /// silently skipped so callers do not need to pre-validate their edge lists.
    /// </summary>
    private static List<int>[] BuildAdjacency(int nodeCount, IReadOnlyList<LayerEdge> edges)
    {
        var adj = new List<int>[nodeCount];
        for (var i = 0; i < nodeCount; i++)
        {
            adj[i] = [];
        }

        foreach (var e in edges)
        {
            // Skip self-loops and out-of-range indices without throwing
            if (e.Source < 0 || e.Source >= nodeCount) { continue; }
            if (e.Target < 0 || e.Target >= nodeCount) { continue; }
            if (e.Source == e.Target) { continue; }

            adj[e.Source].Add(e.Target);
            adj[e.Target].Add(e.Source);
        }

        return adj;
    }

    /// <summary>
    /// Assigns a layer to every node using BFS from the highest-degree seed. Nodes unreachable from
    /// the seed (isolated nodes or disconnected components) receive layer 0.
    /// </summary>
    private static int[] BfsLayers(int nodeCount, List<int>[] adjacency, int[] degrees)
    {
        // Choose the seed: highest-degree node; use lowest index to break ties deterministically
        var seed = 0;
        for (var i = 1; i < nodeCount; i++)
        {
            if (degrees[i] > degrees[seed])
            {
                seed = i;
            }
        }

        var layers = new int[nodeCount];
        Array.Fill(layers, -1);

        // BFS from seed: each unvisited neighbour gets parent_layer + 1
        var queue = new Queue<int>();
        layers[seed] = 0;
        queue.Enqueue(seed);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            foreach (var nb in adjacency[cur])
            {
                if (layers[nb] == -1)
                {
                    layers[nb] = layers[cur] + 1;
                    queue.Enqueue(nb);
                }
            }
        }

        // Assign layer 0 to any node not reached by the BFS (isolated or in a separate component)
        for (var i = 0; i < nodeCount; i++)
        {
            if (layers[i] == -1)
            {
                layers[i] = 0;
            }
        }

        return layers;
    }

    /// <summary>
    /// Groups node indices by their layer assignment, producing one list per layer in ascending
    /// layer order. Within each list, nodes appear in ascending index order (BFS arrival order).
    /// </summary>
    private static List<List<int>> GroupByLayer(int nodeCount, int[] nodeLayers, int maxLayer)
    {
        var groups = new List<List<int>>(maxLayer + 1);
        for (var l = 0; l <= maxLayer; l++)
        {
            groups.Add([]);
        }

        for (var i = 0; i < nodeCount; i++)
        {
            groups[nodeLayers[i]].Add(i);
        }

        return groups;
    }

    /// <summary>
    /// Performs one barycentric ordering pass for layers 1 and above: within each layer the nodes
    /// are sorted by the mean rank (position index within its own layer) of their connected
    /// neighbours, with node index as a deterministic tie-break. Layer 0 retains BFS arrival order.
    /// After each layer is sorted its rank table is updated so that later layers benefit from the
    /// improved ordering of earlier ones.
    /// </summary>
    private static void BarycentricOrder(List<List<int>> layerGroups, List<int>[] adjacency)
    {
        // Initialize rank table: position of each node within its layer group
        var rankInLayer = new int[adjacency.Length];
        for (var l = 0; l < layerGroups.Count; l++)
        {
            for (var k = 0; k < layerGroups[l].Count; k++)
            {
                rankInLayer[layerGroups[l][k]] = k;
            }
        }

        // Sort each layer > 0 by mean neighbour rank, then update ranks so later layers benefit
        for (var l = 1; l < layerGroups.Count; l++)
        {
            layerGroups[l].Sort((a, b) =>
            {
                // Barycentric coordinate: mean rank of all connected neighbours
                var nA = adjacency[a];
                var nB = adjacency[b];
                var meanA = nA.Count > 0 ? nA.Average(n => (double)rankInLayer[n]) : (double)a;
                var meanB = nB.Count > 0 ? nB.Average(n => (double)rankInLayer[n]) : (double)b;
                var cmp = meanA.CompareTo(meanB);

                // Stable tiebreak by node index so output is fully deterministic
                return cmp != 0 ? cmp : a.CompareTo(b);
            });

            // Update rank table after sorting so subsequent layers see the refined ordering
            for (var k = 0; k < layerGroups[l].Count; k++)
            {
                rankInLayer[layerGroups[l][k]] = k;
            }
        }
    }

    /// <summary>
    /// Computes the width of each inter-layer corridor. Width is at least
    /// <paramref name="minCorridorWidth"/> and grows by <paramref name="edgeSpacing"/> for every
    /// edge that crosses the corridor, plus an additional 2 × <paramref name="clearance"/>.
    /// An edge crosses corridor <c>gap</c> when one endpoint is in layer ≤ gap and the other is
    /// in layer ≥ gap + 1.
    /// </summary>
    private static double[] ComputeCorridorWidths(
        int maxLayer,
        int[] nodeLayers,
        IReadOnlyList<LayerEdge> edges,
        double minCorridorWidth,
        double edgeSpacing,
        double clearance)
    {
        // Zero layers means only one column; no corridors to compute
        if (maxLayer == 0)
        {
            return [];
        }

        var widths = new double[maxLayer];
        for (var gap = 0; gap < maxLayer; gap++)
        {
            // Count edges whose two endpoints straddle this inter-layer gap
            var crossings = 0;
            foreach (var e in edges)
            {
                if (e.Source < 0 || e.Source >= nodeLayers.Length) { continue; }
                if (e.Target < 0 || e.Target >= nodeLayers.Length) { continue; }

                var lo = Math.Min(nodeLayers[e.Source], nodeLayers[e.Target]);
                var hi = Math.Max(nodeLayers[e.Source], nodeLayers[e.Target]);

                if (lo <= gap && hi >= gap + 1)
                {
                    crossings++;
                }
            }

            widths[gap] = Math.Max(minCorridorWidth, (crossings * edgeSpacing) + (2.0 * clearance));
        }

        return widths;
    }

    /// <summary>
    /// Computes the left-edge X coordinate of each layer column. Layer 0 starts at X = 0; each
    /// subsequent column starts at the right edge of the previous column plus its corridor width.
    /// </summary>
    private static double[] ComputeColumnX(
        int maxLayer,
        List<List<int>> layerGroups,
        IReadOnlyList<LayerNode> nodes,
        double[] corridorWidths)
    {
        var colX = new double[maxLayer + 1];
        colX[0] = 0.0;

        for (var l = 1; l <= maxLayer; l++)
        {
            // Right edge of the previous column = its left edge + the widest node in that column
            var maxWidth = layerGroups[l - 1].Max(i => nodes[i].Width);
            colX[l] = colX[l - 1] + maxWidth + corridorWidths[l - 1];
        }

        return colX;
    }

    /// <summary>
    /// Assigns absolute rectangles to every node. Within each layer column, nodes are stacked
    /// top-to-bottom in barycentric order with <paramref name="nodeSpacing"/> between them. Each
    /// column is centred vertically relative to the tallest column so short columns align to the
    /// visual midline of the diagram rather than sitting at the top.
    /// </summary>
    private static Rect[] ComputeRects(
        int maxLayer,
        List<List<int>> layerGroups,
        IReadOnlyList<LayerNode> nodes,
        double[] colX,
        double nodeSpacing)
    {
        // Total content height of each column: sum of node heights plus gaps between them
        var colHeights = new double[maxLayer + 1];
        for (var l = 0; l <= maxLayer; l++)
        {
            var totalNodeHeight = layerGroups[l].Sum(i => nodes[i].Height);
            var totalGaps = layerGroups[l].Count > 1
                ? (layerGroups[l].Count - 1) * nodeSpacing
                : 0.0;
            colHeights[l] = totalNodeHeight + totalGaps;
        }

        var maxColHeight = colHeights.Max();

        var rects = new Rect[nodes.Count];
        for (var l = 0; l <= maxLayer; l++)
        {
            // Offset the column top so shorter columns align to the vertical centre of the tallest
            var colTop = (maxColHeight - colHeights[l]) / 2.0;
            var y = colTop;

            foreach (var i in layerGroups[l])
            {
                rects[i] = new Rect(colX[l], y, nodes[i].Width, nodes[i].Height);
                y += nodes[i].Height + nodeSpacing;
            }
        }

        return rects;
    }
}
