// <copyright file="InterconnectionLayoutEngine.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout;

namespace DemaConsulting.SysML2Tools.Layout.Engine;

/// <summary>
/// Describes a node for the <see cref="InterconnectionLayoutEngine"/>: its intrinsic size.
/// </summary>
/// <param name="Width">Node bounding-box width in logical pixels.</param>
/// <param name="Height">Node bounding-box height in logical pixels.</param>
internal readonly record struct LayerNode(double Width, double Height);

/// <summary>
/// A directed edge used to orient the layering. Direction follows SysML connection endpoint
/// order — endpoint A is <see cref="Source"/>, endpoint B is <see cref="Target"/>.
/// </summary>
/// <param name="Source">Zero-based index of the source (endpoint-A) node.</param>
/// <param name="Target">Zero-based index of the target (endpoint-B) node.</param>
internal readonly record struct LayerEdge(int Source, int Target);

/// <summary>
/// Result produced by <see cref="InterconnectionLayoutEngine.Place"/>.
/// </summary>
/// <param name="Rects">
/// Absolute bounding rectangle for each input node, in input-index order.
/// </param>
/// <param name="TotalWidth">
/// Total width of the placed content, including internal padding on all sides.
/// </param>
/// <param name="TotalHeight">
/// Total height of the placed content, including internal padding on all sides.
/// </param>
/// <param name="NodeLayers">
/// Longest-path layer index assigned to each input node, in input-index order.
/// Layer 0 is the leftmost column.
/// </param>
/// <param name="ConnectorWaypoints">
/// Complete orthogonal waypoints for each input edge, in input-index order. The first
/// point is the port position on the source node's right face; the last point is the
/// port position on the target node's left face.
/// </param>
internal sealed record LayerResult(
    IReadOnlyList<Rect> Rects,
    double TotalWidth,
    double TotalHeight,
    IReadOnlyList<int> NodeLayers,
    IReadOnlyList<IReadOnlyList<Point2D>> ConnectorWaypoints);

/// <summary>
/// Sugiyama-style layered layout engine for SysML v2 interconnection diagrams.
/// Implements the full ELK-compatible pipeline with left-to-right flow.
/// </summary>
/// <remarks>
/// <para>Pipeline stages:</para>
/// <list type="number">
///   <item><description>
///     <strong>Cycle breaking</strong>: DFS back-edge reversal so the graph is acyclic.
///   </description></item>
///   <item><description>
///     <strong>Longest-path layering</strong>: assigns each node a layer equal to the
///     longest directed-path length from any source. Every edge crosses from a lower layer
///     to a strictly higher layer — no same-layer connections.
///   </description></item>
///   <item><description>
///     <strong>Dummy-node insertion</strong>: edges that span more than one layer receive
///     zero-size placeholder nodes in each intermediate layer. This forces the routing
///     path through the correct corridors and prevents connectors from clipping
///     intermediate boxes.
///   </description></item>
///   <item><description>
///     <strong>Barycenter ordering</strong>: alternating left-to-right and right-to-left
///     sweeps reduce visual edge crossings.
///   </description></item>
///   <item><description>
///     <strong>Coordinate assignment</strong>: column X positions derived from corridor
///     widths (scaled with crossing-edge count, minimum 70 px to match the ELK reference
///     sandbox); Y positions from top-to-bottom stacking within each column with 30 px
///     gaps; dummy nodes take zero height.
///   </description></item>
///   <item><description>
///     <strong>Port distribution</strong>: connection ports spaced evenly along the right
///     face (outgoing) and left face (incoming) of each real node.
///   </description></item>
///   <item><description>
///     <strong>Slot-based routing</strong>: each edge through a corridor receives a
///     unique vertical slot X, making segment conflicts impossible by construction.
///     Long-edge segments are stitched into a single orthogonal polyline.
///   </description></item>
/// </list>
/// <para>
/// Spacing constants match the ELK reference sandbox:
/// 70 px minimum corridor, 30 px node gap, 16 px slot spacing, 10 px clearance.
/// </para>
/// </remarks>
internal static class InterconnectionLayoutEngine
{
    /// <summary>Minimum vertical gap between stacked nodes in the same column.</summary>
    private const double NodeSpacing = 30.0;

    /// <summary>Minimum corridor width regardless of edge count.</summary>
    private const double CorridorMinWidth = 70.0;

    /// <summary>Width added per crossing edge to the corridor, for slot-X assignment.</summary>
    private const double EdgeSpacing = 16.0;

    /// <summary>Gap between a box face and the nearest connector slot or port.</summary>
    private const double ConnectorClearance = 10.0;

    /// <summary>Uniform padding around the placed content region.</summary>
    private const double Padding = 20.0;

    /// <summary>Number of Barycenter ordering sweeps (each sweep = one full pass).</summary>
    private const int BarycentricSweeps = 4;

    // ── Public entry point ───────────────────────────────────────────────────

    /// <summary>
    /// Places nodes and routes edges, returning absolute rectangles and complete
    /// orthogonal connector waypoints.
    /// </summary>
    /// <param name="nodes">
    /// Real nodes in caller order. Heights should already be scaled for port count
    /// (minimum <c>degree × minPortSlot + 2 × clearance</c>) by the caller.
    /// </param>
    /// <param name="edges">
    /// Directed edges in caller order, following SysML endpoint-A → endpoint-B order.
    /// </param>
    /// <returns>
    /// A <see cref="LayerResult"/> with one <see cref="Rect"/> per node and one
    /// waypoint list per edge; outer padding is embedded in the coordinates.
    /// </returns>
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

        // Phase 1: acyclic directed graph + longest-path layer assignment.
        var acyclic = BreakCycles(n, edges);
        var realLayers = AssignLayers(n, acyclic);

        // Phase 2: augmented graph — insert dummy nodes for multi-layer edges.
        var aug = BuildAugmentedGraph(n, nodes, realLayers, acyclic);

        // Phase 3: group all augmented nodes by layer then apply Barycenter ordering.
        var groups = GroupByLayer(aug.Nodes.Length, aug.Layers);
        OrderLayers(groups, aug.Edges);

        // Phase 4: absolute coordinates (column X from corridor widths, Y from stacking).
        var coords = AssignCoordinates(aug, groups);

        // Phase 5: port distribution, slot assignment, waypoint construction.
        var routes = BuildRoutes(aug, coords);

        // Build result: real-node rects and per-edge connector waypoints.
        var rects = new Rect[n];
        for (var i = 0; i < n; i++)
        {
            rects[i] = new Rect(coords.NodeX[i], coords.NodeY[i], nodes[i].Width, nodes[i].Height);
        }

        // Total dimensions: rightmost column right edge + padding; deepest column bottom + padding.
        var totalWidth = coords.ColumnX[^1] + coords.MaxColWidth[^1] + Padding;
        var totalHeight = Padding;
        for (var l = 0; l < groups.Count; l++)
        {
            if (groups[l].Count == 0)
            {
                continue;
            }

            var lastNode = groups[l][^1];
            totalHeight = Math.Max(totalHeight, coords.NodeY[lastNode] + aug.Nodes[lastNode].H + Padding);
        }

        var waypointLists = new IReadOnlyList<Point2D>[acyclic.Count];
        for (var e = 0; e < acyclic.Count; e++)
        {
            waypointLists[e] = routes.Waypoints[e] ?? [];
        }

        return new LayerResult(rects, totalWidth, totalHeight, realLayers, waypointLists);
    }

    // ── Phase 1: Cycle breaking ──────────────────────────────────────────────

    /// <summary>
    /// Returns the edge set with back edges reversed (DFS coloring).
    /// Self-loops and duplicate edges are dropped.
    /// </summary>
    private static List<LayerEdge> BreakCycles(int n, IReadOnlyList<LayerEdge> edges)
    {
        var adj = new List<int>[n];
        for (var i = 0; i < n; i++)
        {
            adj[i] = [];
        }

        foreach (var e in edges)
        {
            if (e.Source >= 0 && e.Source < n && e.Target >= 0 && e.Target < n && e.Source != e.Target)
            {
                adj[e.Source].Add(e.Target);
            }
        }

        var visited = new bool[n];
        var onStack = new bool[n];
        var backEdges = new HashSet<(int, int)>();

        void Dfs(int u)
        {
            visited[u] = true;
            onStack[u] = true;

            foreach (var v in adj[u])
            {
                if (onStack[v])
                {
                    backEdges.Add((u, v));
                }
                else if (!visited[v])
                {
                    Dfs(v);
                }
            }

            // S4143: standard DFS coloring — onStack[u] is read by recursive calls between
            // the true/false assignments; the analyzer cannot see across the recursion.
#pragma warning disable S4143
            onStack[u] = false;
#pragma warning restore S4143
        }

        for (var i = 0; i < n; i++)
        {
            if (!visited[i])
            {
                Dfs(i);
            }
        }

        var result = new List<LayerEdge>();
        var seen = new HashSet<(int, int)>();

        foreach (var e in edges)
        {
            if (e.Source < 0 || e.Source >= n || e.Target < 0 || e.Target >= n || e.Source == e.Target)
            {
                continue;
            }

            var (from, to) = backEdges.Contains((e.Source, e.Target))
                ? (e.Target, e.Source)
                : (e.Source, e.Target);

            if (seen.Add((from, to)))
            {
                result.Add(new LayerEdge(from, to));
            }
        }

        return result;
    }

    // ── Phase 1 (continued): Longest-path layering ──────────────────────────

    /// <summary>
    /// Assigns each node a layer equal to the length of the longest directed path from
    /// any source. This guarantees that every edge goes from a strictly lower layer to a
    /// strictly higher layer — no same-layer connections are possible.
    /// </summary>
    private static int[] AssignLayers(int n, List<LayerEdge> acyclic)
    {
        var outgoing = new List<int>[n];
        var inDegree = new int[n];

        for (var i = 0; i < n; i++)
        {
            outgoing[i] = [];
        }

        foreach (var e in acyclic)
        {
            outgoing[e.Source].Add(e.Target);
            inDegree[e.Target]++;
        }

        var layer = new int[n];
        var remaining = (int[])inDegree.Clone();

        var queue = new Queue<int>();
        for (var i = 0; i < n; i++)
        {
            if (remaining[i] == 0)
            {
                queue.Enqueue(i);
            }
        }

        while (queue.Count > 0)
        {
            var u = queue.Dequeue();
            foreach (var v in outgoing[u])
            {
                layer[v] = Math.Max(layer[v], layer[u] + 1);
                if (--remaining[v] == 0)
                {
                    queue.Enqueue(v);
                }
            }
        }

        return layer;
    }

    // ── Phase 2: Augmented graph ─────────────────────────────────────────────

    /// <summary>
    /// Bundled data for the augmented graph (real nodes + dummy nodes).
    /// </summary>
    private sealed record AugGraph(
        (double W, double H, bool IsDummy)[] Nodes,
        int[] Layers,
        List<(int From, int To, int OrigEdge)> Edges,
        int[][] Chains);

    /// <summary>
    /// Extends the real-node set with zero-size dummy nodes for every edge whose endpoint
    /// layers differ by more than one. Returns the augmented nodes/layers/edges and the
    /// per-original-edge chain (source → dummy … dummy → target).
    /// </summary>
    private static AugGraph BuildAugmentedGraph(
        int n,
        IReadOnlyList<LayerNode> realNodes,
        int[] realLayers,
        List<LayerEdge> acyclic)
    {
        var augNodes = new List<(double W, double H, bool IsDummy)>(n);
        for (var i = 0; i < n; i++)
        {
            augNodes.Add((realNodes[i].Width, realNodes[i].Height, false));
        }

        var augLayers = new List<int>(realLayers);
        var augEdges = new List<(int From, int To, int OrigEdge)>();
        var chains = new int[acyclic.Count][];

        for (var e = 0; e < acyclic.Count; e++)
        {
            var src = acyclic[e].Source;
            var tgt = acyclic[e].Target;
            var span = realLayers[tgt] - realLayers[src];

            if (span <= 0)
            {
                // Defensive: should not occur after cycle-breaking + longest-path.
                chains[e] = [src, tgt];
                augEdges.Add((src, tgt, e));
                continue;
            }

            if (span == 1)
            {
                chains[e] = [src, tgt];
                augEdges.Add((src, tgt, e));
            }
            else
            {
                // Insert span−1 zero-size dummy nodes at intermediate layers.
                var chain = new int[span + 1];
                chain[0] = src;
                chain[span] = tgt;

                for (var k = 1; k < span; k++)
                {
                    var dummyIdx = augNodes.Count;
                    augNodes.Add((0.0, 0.0, true));
                    augLayers.Add(realLayers[src] + k);
                    chain[k] = dummyIdx;
                }

                chains[e] = chain;
                for (var k = 0; k < span; k++)
                {
                    augEdges.Add((chain[k], chain[k + 1], e));
                }
            }
        }

        return new AugGraph(
            augNodes.ToArray(),
            augLayers.ToArray(),
            augEdges,
            chains);
    }

    // ── Phase 3: Barycenter ordering ─────────────────────────────────────────

    /// <summary>Groups augmented node indices into per-layer lists.</summary>
    private static List<List<int>> GroupByLayer(int nodeCount, int[] layers)
    {
        var maxLayer = nodeCount == 0 ? 0 : layers.Max();
        var groups = new List<List<int>>(maxLayer + 1);
        for (var l = 0; l <= maxLayer; l++)
        {
            groups.Add([]);
        }

        for (var i = 0; i < nodeCount; i++)
        {
            groups[layers[i]].Add(i);
        }

        return groups;
    }

    /// <summary>
    /// Alternating left-to-right and right-to-left Barycenter sweeps to reduce crossings.
    /// </summary>
    private static void OrderLayers(
        List<List<int>> groups,
        List<(int From, int To, int OrigEdge)> augEdges)
    {
        var totalNodes = groups.Sum(g => g.Count);
        var leftNeighbors = new List<int>[totalNodes];   // neighbors in layer L−1
        var rightNeighbors = new List<int>[totalNodes];  // neighbors in layer L+1

        for (var i = 0; i < totalNodes; i++)
        {
            leftNeighbors[i] = [];
            rightNeighbors[i] = [];
        }

        foreach (var (from, to, _) in augEdges)
        {
            if (from < totalNodes && to < totalNodes)
            {
                rightNeighbors[from].Add(to);
                leftNeighbors[to].Add(from);
            }
        }

        for (var sweep = 0; sweep < BarycentricSweeps; sweep++)
        {
            if (sweep % 2 == 0)
            {
                for (var l = 1; l < groups.Count; l++)
                {
                    SortByBarycenter(groups[l], groups[l - 1], leftNeighbors);
                }
            }
            else
            {
                for (var l = groups.Count - 2; l >= 0; l--)
                {
                    SortByBarycenter(groups[l], groups[l + 1], rightNeighbors);
                }
            }
        }
    }

    /// <summary>
    /// Sorts <paramref name="layer"/> by the mean rank of each node's neighbors in
    /// <paramref name="adjacentLayer"/>; nodes with no neighbors keep their current order.
    /// </summary>
    private static void SortByBarycenter(
        List<int> layer,
        List<int> adjacentLayer,
        List<int>[] neighbors)
    {
        var pos = new Dictionary<int, int>(adjacentLayer.Count);
        for (var i = 0; i < adjacentLayer.Count; i++)
        {
            pos[adjacentLayer[i]] = i;
        }

        var keyed = new (int Node, double Key, int Idx)[layer.Count];
        for (var i = 0; i < layer.Count; i++)
        {
            var node = layer[i];
            var ns = neighbors[node].Where(pos.ContainsKey).ToList();
            keyed[i] = (node, ns.Count > 0 ? ns.Average(v => pos[v]) : i, i);
        }

        Array.Sort(keyed, (a, b) =>
        {
            var c = a.Key.CompareTo(b.Key);
            return c != 0 ? c : a.Idx.CompareTo(b.Idx);
        });

        for (var i = 0; i < layer.Count; i++)
        {
            layer[i] = keyed[i].Node;
        }
    }

    // ── Phase 4: Coordinate assignment ───────────────────────────────────────

    /// <summary>
    /// Bundled coordinate data: absolute X/Y for every augmented node, column-left X
    /// values for each layer, and the maximum real-node width per layer.
    /// </summary>
    private sealed record Coords(
        double[] NodeX,
        double[] NodeY,
        double[] ColumnX,
        double[] MaxColWidth);

    /// <summary>
    /// Assigns X coordinates from corridor widths (scaled by crossing-edge count) and
    /// Y coordinates from top-to-bottom stacking within each column.
    /// </summary>
    private static Coords AssignCoordinates(AugGraph aug, List<List<int>> groups)
    {
        var layerCount = groups.Count;
        var augCount = aug.Nodes.Length;

        // Max node width per layer (dummies are zero-width and do not contribute).
        var maxColWidth = new double[layerCount];
        for (var i = 0; i < augCount; i++)
        {
            var l = aug.Layers[i];
            maxColWidth[l] = Math.Max(maxColWidth[l], aug.Nodes[i].W);
        }

        // Count augmented edges that cross each inter-layer corridor.
        var corridorEdges = new int[Math.Max(1, layerCount - 1)];
        foreach (var (from, to, _) in aug.Edges)
        {
            var l = Math.Min(aug.Layers[from], aug.Layers[to]);
            if (l >= 0 && l < corridorEdges.Length)
            {
                corridorEdges[l]++;
            }
        }

        // Corridor widths: at least CorridorMinWidth; wider for denser corridors.
        var corridorWidth = new double[corridorEdges.Length];
        for (var l = 0; l < corridorWidth.Length; l++)
        {
            corridorWidth[l] = Math.Max(
                CorridorMinWidth,
                ConnectorClearance + (corridorEdges[l] * EdgeSpacing) + ConnectorClearance);
        }

        // Column left-edge X positions.
        var columnX = new double[layerCount];
        columnX[0] = Padding;
        for (var l = 1; l < layerCount; l++)
        {
            columnX[l] = columnX[l - 1] + maxColWidth[l - 1] + corridorWidth[l - 1];
        }

        // Stack nodes top-to-bottom within each column; dummies take zero vertical space.
        var nodeX = new double[augCount];
        var nodeY = new double[augCount];

        for (var l = 0; l < layerCount; l++)
        {
            var y = Padding;
            foreach (var ni in groups[l])
            {
                nodeX[ni] = columnX[l];
                nodeY[ni] = y;
                y += aug.Nodes[ni].H + NodeSpacing;
            }
        }

        return new Coords(nodeX, nodeY, columnX, maxColWidth);
    }

    // ── Phases 5-7: Port distribution, slot assignment, waypoint building ────

    private sealed record Routes(List<Point2D>?[] Waypoints);

    /// <summary>
    /// Distributes port Y coordinates on each box face, assigns unique slot-X values in
    /// each corridor, and produces the final orthogonal waypoints for every original edge.
    /// </summary>
    private static Routes BuildRoutes(AugGraph aug, Coords coords)
    {
        var augEdgeCount = aug.Edges.Count;
        var origEdgeCount = aug.Chains.Length;

        // portYRight[ai] = port Y at source's right face for augmented edge ai.
        // portYLeft[ai]  = port Y at target's left face for augmented edge ai.
        var portYRight = new double[augEdgeCount];
        var portYLeft = new double[augEdgeCount];

        // Group augmented edge indices by their source node (for right-face distribution).
        var outByNode = new Dictionary<int, List<int>>();
        for (var ai = 0; ai < augEdgeCount; ai++)
        {
            var src = aug.Edges[ai].From;
            if (!outByNode.TryGetValue(src, out var list))
            {
                list = [];
                outByNode[src] = list;
            }

            list.Add(ai);
        }

        foreach (var (nodeIdx, augIdxList) in outByNode)
        {
            var (_, h, isDummy) = aug.Nodes[nodeIdx];
            var centerY = coords.NodeY[nodeIdx] + (h / 2.0);

            if (isDummy)
            {
                foreach (var ai in augIdxList)
                {
                    portYRight[ai] = centerY;
                }
            }
            else
            {
                // Sort outgoing edges by target center-Y so ports are ordered top-to-bottom.
                var sorted = augIdxList
                    .OrderBy(ai =>
                    {
                        var tgt = aug.Edges[ai].To;
                        return coords.NodeY[tgt] + (aug.Nodes[tgt].H / 2.0);
                    })
                    .ThenBy(ai => ai)
                    .ToList();

                DistributePorts(sorted, coords.NodeY[nodeIdx], h, portYRight);
            }
        }

        // Group augmented edge indices by their target node (for left-face distribution).
        var inByNode = new Dictionary<int, List<int>>();
        for (var ai = 0; ai < augEdgeCount; ai++)
        {
            var tgt = aug.Edges[ai].To;
            if (!inByNode.TryGetValue(tgt, out var list))
            {
                list = [];
                inByNode[tgt] = list;
            }

            list.Add(ai);
        }

        foreach (var (nodeIdx, augIdxList) in inByNode)
        {
            var (_, h, isDummy) = aug.Nodes[nodeIdx];
            var centerY = coords.NodeY[nodeIdx] + (h / 2.0);

            if (isDummy)
            {
                foreach (var ai in augIdxList)
                {
                    portYLeft[ai] = centerY;
                }
            }
            else
            {
                // Sort incoming edges by source center-Y so ports are ordered top-to-bottom.
                var sorted = augIdxList
                    .OrderBy(ai =>
                    {
                        var src = aug.Edges[ai].From;
                        return coords.NodeY[src] + (aug.Nodes[src].H / 2.0);
                    })
                    .ThenBy(ai => ai)
                    .ToList();

                DistributePorts(sorted, coords.NodeY[nodeIdx], h, portYLeft);
            }
        }

        // Assign slot X: per corridor, sort crossing edges by mean port Y, step right.
        var slotX = new double[augEdgeCount];
        var layerCount = coords.ColumnX.Length;

        for (var l = 0; l + 1 < layerCount; l++)
        {
            var corridorLeft = coords.ColumnX[l] + coords.MaxColWidth[l];

            var crossing = aug.Edges
                .Select((e, i) => (e, i))
                .Where(x => Math.Min(aug.Layers[x.e.From], aug.Layers[x.e.To]) == l)
                .OrderBy(x => (portYRight[x.i] + portYLeft[x.i]) / 2.0)
                .ThenBy(x => x.i)
                .ToList();

            for (var k = 0; k < crossing.Count; k++)
            {
                slotX[crossing[k].i] = corridorLeft + ConnectorClearance + (k * EdgeSpacing);
            }
        }

        // Build waypoints: for each original edge stitch its segment Z-paths into one polyline.
        var waypoints = new List<Point2D>?[origEdgeCount];

        for (var e = 0; e < origEdgeCount; e++)
        {
            var chain = aug.Chains[e];
            if (chain is not { Length: >= 2 })
            {
                continue;
            }

            var wp = new List<Point2D>();

            for (var k = 0; k < chain.Length - 1; k++)
            {
                var segFrom = chain[k];
                var segTo = chain[k + 1];

                // Find the augmented-edge index for this segment.
                var ai = -1;
                for (var j = 0; j < augEdgeCount; j++)
                {
                    if (aug.Edges[j].From == segFrom &&
                        aug.Edges[j].To == segTo &&
                        aug.Edges[j].OrigEdge == e)
                    {
                        ai = j;
                        break;
                    }
                }

                if (ai < 0)
                {
                    continue;
                }

                var srcX = coords.NodeX[segFrom] + aug.Nodes[segFrom].W;  // right face X
                var srcY = portYRight[ai];
                var tgtX = coords.NodeX[segTo];                            // left face X
                var tgtY = portYLeft[ai];
                var sx = slotX[ai];

                // Add the source port on the first segment only (avoids duplicates during stitching).
                if (k == 0)
                {
                    wp.Add(new Point2D(srcX, srcY));
                }

                // Z-path: horizontal to slot → vertical to target Y → horizontal to target.
                wp.Add(new Point2D(sx, srcY));
                wp.Add(new Point2D(sx, tgtY));
                wp.Add(new Point2D(tgtX, tgtY));
            }

            waypoints[e] = wp;
        }

        return new Routes(waypoints);
    }

    /// <summary>
    /// Writes evenly distributed Y coordinates into <paramref name="portY"/> at positions
    /// <paramref name="sortedAugEdgeIndices"/> for a node face spanning
    /// [<paramref name="nodeTop"/>, <paramref name="nodeTop"/> + <paramref name="nodeHeight"/>].
    /// A single port is centred; multiple ports are spaced from
    /// <c>nodeTop + ConnectorClearance</c> to <c>nodeTop + nodeHeight − ConnectorClearance</c>.
    /// </summary>
    private static void DistributePorts(
        IReadOnlyList<int> sortedAugEdgeIndices,
        double nodeTop,
        double nodeHeight,
        double[] portY)
    {
        var count = sortedAugEdgeIndices.Count;

        for (var k = 0; k < count; k++)
        {
            double y;
            if (count == 1)
            {
                y = nodeTop + (nodeHeight / 2.0);
            }
            else
            {
                var usable = nodeHeight - (2.0 * ConnectorClearance);
                y = nodeTop + ConnectorClearance + (k * usable / (count - 1));
            }

            portY[sortedAugEdgeIndices[k]] = Math.Clamp(
                y,
                nodeTop + ConnectorClearance,
                nodeTop + nodeHeight - ConnectorClearance);
        }
    }
}
