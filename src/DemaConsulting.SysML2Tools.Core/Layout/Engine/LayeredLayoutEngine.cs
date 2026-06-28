// <copyright file="LayeredLayoutEngine.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout.Engine;

/// <summary>
/// A node to be placed by the <see cref="LayeredLayoutEngine"/>, identified by its size.
/// </summary>
/// <param name="Width">Width of the node's bounding box in logical pixels.</param>
/// <param name="Height">Height of the node's bounding box in logical pixels.</param>
internal readonly record struct LayeredNode(double Width, double Height);

/// <summary>
/// A directed edge (from a source node to a target node, by index) used for layering.
/// </summary>
/// <param name="From">Index of the source node.</param>
/// <param name="To">Index of the target node.</param>
internal readonly record struct LayeredEdge(int From, int To);

/// <summary>
/// The result of a layered placement.
/// </summary>
/// <param name="Width">Total width of the placed region (including padding) in logical pixels.</param>
/// <param name="Height">Total height of the placed region (including padding) in logical pixels.</param>
/// <param name="Rects">Placed rectangles, one per input node in the same order.</param>
/// <param name="Layers">The assigned layer index of each node, in node order.</param>
internal sealed record LayeredResult(double Width, double Height, IReadOnlyList<PackedRect> Rects, IReadOnlyList<int> Layers);

/// <summary>
/// A simplified Sugiyama-style layered layout engine for directed graphs. Produces a top-to-bottom
/// flow: cycles are broken, nodes are assigned to layers by longest path from the sources, ordered
/// within layers to reduce edge crossings (Barycenter heuristic), and given coordinates.
/// </summary>
/// <remarks>
/// The engine is deterministic. It guarantees that every edge points from a lower layer (smaller Y)
/// to a higher layer for non-reversed edges, and that no two nodes in the same layer overlap.
/// </remarks>
internal static class LayeredLayoutEngine
{
    /// <summary>Number of Barycenter ordering sweeps (down + up counts as two).</summary>
    private const int OrderingSweeps = 8;

    /// <summary>
    /// Computes a layered placement for the given nodes and directed edges.
    /// </summary>
    /// <param name="nodes">Nodes to place, in caller order.</param>
    /// <param name="edges">Directed edges (indices into <paramref name="nodes"/>).</param>
    /// <param name="layerGap">Vertical gap between adjacent layers.</param>
    /// <param name="nodeGap">Horizontal gap between adjacent nodes in a layer.</param>
    /// <param name="padding">Uniform padding added around the placed region.</param>
    /// <returns>A <see cref="LayeredResult"/> with one rectangle per node and the region size.</returns>
    public static LayeredResult Place(
        IReadOnlyList<LayeredNode> nodes,
        IReadOnlyList<LayeredEdge> edges,
        double layerGap,
        double nodeGap,
        double padding)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);

        var n = nodes.Count;
        if (n == 0)
        {
            return new LayeredResult(2.0 * padding, 2.0 * padding, [], []);
        }

        // Break cycles so layering terminates, then assign layers by longest path.
        var acyclic = BreakCycles(n, edges);
        var layers = AssignLayers(n, acyclic);

        // Group nodes by layer and order within each layer to reduce crossings.
        var layerGroups = GroupByLayer(layers);
        OrderLayers(layerGroups, acyclic);

        return AssignCoordinates(nodes, layerGroups, layers, acyclic, layerGap, nodeGap, padding);
    }

    /// <summary>
    /// Computes a layered placement by trying several deterministic per-seed within-layer
    /// permutations and keeping the one with the lowest edge-crossing count. <c>seeds = 1</c> is
    /// identical to <see cref="Place"/>; higher seed counts explore alternate orderings.
    /// </summary>
    /// <param name="nodes">Nodes to place, in caller order.</param>
    /// <param name="edges">Directed edges (indices into <paramref name="nodes"/>).</param>
    /// <param name="seeds">Number of orderings to evaluate (clamped to at least 1).</param>
    /// <param name="layerGap">Vertical gap between adjacent layers.</param>
    /// <param name="nodeGap">Horizontal gap between adjacent nodes in a layer.</param>
    /// <param name="padding">Uniform padding added around the placed region.</param>
    /// <returns>The lowest-crossing <see cref="LayeredResult"/> over all seeds.</returns>
    public static LayeredResult PlaceMultiSeed(
        IReadOnlyList<LayeredNode> nodes,
        IReadOnlyList<LayeredEdge> edges,
        int seeds,
        double layerGap,
        double nodeGap,
        double padding)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);

        var baseResult = Place(nodes, edges, layerGap, nodeGap, padding);
        if (seeds <= 1 || nodes.Count == 0)
        {
            return baseResult;
        }

        var acyclic = BreakCycles(nodes.Count, edges);
        var layers = AssignLayers(nodes.Count, acyclic);
        var best = baseResult;
        var bestCrossings = TotalCrossings(GroupByLayerSeeded(layers, 0), acyclic);

        for (var seed = 1; seed < seeds; seed++)
        {
            var groups = GroupByLayerSeeded(layers, seed);
            OrderLayers(groups, acyclic);
            var crossings = TotalCrossings(groups, acyclic);
            if (crossings < bestCrossings)
            {
                bestCrossings = crossings;
                best = AssignCoordinates(nodes, groups, layers, acyclic, layerGap, nodeGap, padding);
            }
        }

        return best;
    }

    /// <summary>Groups nodes by layer using a deterministic per-seed within-layer permutation.</summary>
    private static List<List<int>> GroupByLayerSeeded(int[] layers, int seed)
    {
        var groups = GroupByLayer(layers);
        if (seed == 0)
        {
            return groups;
        }

        foreach (var group in groups)
        {
            // Deterministic shuffle keyed by seed and node index — no shared RNG state.
            group.Sort((a, b) => ((a * 2654435761L) ^ (seed * 40503L)).GetHashCode()
                .CompareTo(((b * 2654435761L) ^ (seed * 40503L)).GetHashCode()));
        }

        return groups;
    }

    /// <summary>Sums the crossing count across all adjacent layer pairs for the given ordering.</summary>
    private static int TotalCrossings(List<List<int>> groups, List<LayeredEdge> edges)
    {
        var connEdges = edges.Select(e => new ConnectivityEdge(e.From, e.To)).ToList();
        var total = 0;
        for (var l = 0; l + 1 < groups.Count; l++)
        {
            total += ConnectivityAnalyzer.CrossingScore(groups[l], groups[l + 1], connEdges);
        }

        return total;
    }

    /// <summary>
    /// Returns the edge set with cycle-causing back edges reversed, using a DFS that classifies an
    /// edge to a node currently on the recursion stack as a back edge.
    /// </summary>
    private static List<LayeredEdge> BreakCycles(int n, IReadOnlyList<LayeredEdge> edges)
    {
        var adjacency = new List<int>[n];
        for (var i = 0; i < n; i++)
        {
            adjacency[i] = [];
        }

        foreach (var e in edges)
        {
            if (e.From != e.To)
            {
                adjacency[e.From].Add(e.To);
            }
        }

        var visited = new bool[n];
        var onStack = new bool[n];
        var backEdges = new HashSet<(int, int)>();

        void Dfs(int u)
        {
            visited[u] = true;
            onStack[u] = true;
            foreach (var v in adjacency[u])
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

            // S4143: standard DFS coloring — onStack[u] is read by recursive calls between the
            // true/false assignments; the analyzer cannot see across the recursion.
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

        // Rebuild the edge list with back edges reversed and self/duplicate edges dropped.
        var result = new List<LayeredEdge>();
        var seen = new HashSet<(int, int)>();
        foreach (var e in edges)
        {
            if (e.From == e.To)
            {
                continue;
            }

            var (from, to) = backEdges.Contains((e.From, e.To)) ? (e.To, e.From) : (e.From, e.To);
            if (from != to && seen.Add((from, to)))
            {
                result.Add(new LayeredEdge(from, to));
            }
        }

        return result;
    }

    /// <summary>Assigns each node to a layer equal to its longest path from any source.</summary>
    private static int[] AssignLayers(int n, List<LayeredEdge> edges)
    {
        var incoming = new List<int>[n];
        var outgoing = new List<int>[n];
        var inDegree = new int[n];
        for (var i = 0; i < n; i++)
        {
            incoming[i] = [];
            outgoing[i] = [];
        }

        foreach (var e in edges)
        {
            outgoing[e.From].Add(e.To);
            incoming[e.To].Add(e.From);
            inDegree[e.To]++;
        }

        // Topological order (the edge set is acyclic after BreakCycles).
        var layer = new int[n];
        var queue = new Queue<int>();
        for (var i = 0; i < n; i++)
        {
            if (inDegree[i] == 0)
            {
                queue.Enqueue(i);
            }
        }

        var remaining = (int[])inDegree.Clone();
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

    /// <summary>Groups node indices by their assigned layer, ordered by layer then index.</summary>
    private static List<List<int>> GroupByLayer(int[] layers)
    {
        var maxLayer = layers.Length == 0 ? 0 : layers.Max();
        var groups = new List<List<int>>();
        for (var l = 0; l <= maxLayer; l++)
        {
            groups.Add([]);
        }

        for (var i = 0; i < layers.Length; i++)
        {
            groups[layers[i]].Add(i);
        }

        return groups;
    }

    /// <summary>
    /// Reorders nodes within each layer using repeated Barycenter sweeps over the adjacent layers
    /// to reduce edge crossings.
    /// </summary>
    private static void OrderLayers(List<List<int>> layerGroups, List<LayeredEdge> edges)
    {
        var n = layerGroups.Sum(g => g.Count);
        var neighborsUp = new List<int>[n];
        var neighborsDown = new List<int>[n];
        for (var i = 0; i < n; i++)
        {
            neighborsUp[i] = [];
            neighborsDown[i] = [];
        }

        foreach (var e in edges)
        {
            neighborsDown[e.From].Add(e.To);
            neighborsUp[e.To].Add(e.From);
        }

        for (var sweep = 0; sweep < OrderingSweeps; sweep++)
        {
            var downward = sweep % 2 == 0;
            if (downward)
            {
                for (var l = 1; l < layerGroups.Count; l++)
                {
                    SortByBarycenter(layerGroups[l], layerGroups[l - 1], neighborsUp);
                }
            }
            else
            {
                for (var l = layerGroups.Count - 2; l >= 0; l--)
                {
                    SortByBarycenter(layerGroups[l], layerGroups[l + 1], neighborsDown);
                }
            }
        }
    }

    /// <summary>
    /// Sorts a layer by the average position of each node's neighbors in the adjacent layer; nodes
    /// with no neighbors keep their current relative order.
    /// </summary>
    private static void SortByBarycenter(List<int> layer, List<int> adjacentLayer, List<int>[] neighbors)
    {
        var position = new Dictionary<int, int>();
        for (var i = 0; i < adjacentLayer.Count; i++)
        {
            position[adjacentLayer[i]] = i;
        }

        // Compute a stable sort key: Barycenter when neighbors exist, else current index.
        var keyed = new List<(int Node, double Key, int Original)>();
        for (var i = 0; i < layer.Count; i++)
        {
            var node = layer[i];
            var ns = neighbors[node].Where(position.ContainsKey).ToList();
            var key = ns.Count > 0 ? ns.Average(x => position[x]) : i;
            keyed.Add((node, key, i));
        }

        keyed.Sort((a, b) =>
        {
            var c = a.Key.CompareTo(b.Key);
            return c != 0 ? c : a.Original.CompareTo(b.Original);
        });

        for (var i = 0; i < layer.Count; i++)
        {
            layer[i] = keyed[i].Node;
        }
    }

    /// <summary>Number of x-coordinate alignment sweeps (down + up) used to straighten the flow.</summary>
    private const int AlignmentSweeps = 8;

    /// <summary>
    /// Assigns absolute coordinates: layers stacked vertically, nodes within a layer ordered and
    /// given x-coordinates that align each node near the average position of its neighbors so the
    /// flow forms a straight spine instead of left-aligning every layer.
    /// </summary>
    private static LayeredResult AssignCoordinates(
        IReadOnlyList<LayeredNode> nodes,
        List<List<int>> layerGroups,
        int[] layers,
        List<LayeredEdge> edges,
        double layerGap,
        double nodeGap,
        double padding)
    {
        var n = nodes.Count;
        var rects = new PackedRect[n];
        var half = new double[n];
        for (var i = 0; i < n; i++)
        {
            half[i] = nodes[i].Width / 2.0;
        }

        // Layer heights and cumulative Y positions.
        var layerY = new double[layerGroups.Count];
        var y = padding;
        for (var l = 0; l < layerGroups.Count; l++)
        {
            layerY[l] = y;
            var layerHeight = layerGroups[l].Count == 0 ? 0.0 : layerGroups[l].Max(i => nodes[i].Height);
            y += layerHeight + layerGap;
        }

        // Initial x: centre each layer around 0 (preserving the crossing-reduced order).
        var cx = new double[n];
        foreach (var layer in layerGroups)
        {
            var total = layer.Sum(i => nodes[i].Width) + (Math.Max(0, layer.Count - 1) * nodeGap);
            var pos = -total / 2.0;
            foreach (var node in layer)
            {
                cx[node] = pos + half[node];
                pos += nodes[node].Width + nodeGap;
            }
        }

        // Neighbor lists in adjacent layers.
        var up = new List<int>[n];
        var down = new List<int>[n];
        for (var i = 0; i < n; i++)
        {
            up[i] = [];
            down[i] = [];
        }

        foreach (var e in edges)
        {
            down[e.From].Add(e.To);
            up[e.To].Add(e.From);
        }

        // Relaxation: alternately pull each node toward the average centre of its Neighbors,
        // resolving overlaps within the layer while preserving order.
        for (var sweep = 0; sweep < AlignmentSweeps; sweep++)
        {
            if (sweep % 2 == 0)
            {
                for (var l = 1; l < layerGroups.Count; l++)
                {
                    AlignLayer(layerGroups[l], up, cx, half, nodeGap);
                }
            }
            else
            {
                for (var l = layerGroups.Count - 2; l >= 0; l--)
                {
                    AlignLayer(layerGroups[l], down, cx, half, nodeGap);
                }
            }
        }

        // Normalize so the left-most node edge sits at padding, then build rects.
        var minLeft = double.MaxValue;
        for (var i = 0; i < n; i++)
        {
            minLeft = Math.Min(minLeft, cx[i] - half[i]);
        }

        var shift = padding - (n == 0 ? 0.0 : minLeft);
        var maxRight = padding;
        for (var l = 0; l < layerGroups.Count; l++)
        {
            var layerHeight = layerGroups[l].Count == 0 ? 0.0 : layerGroups[l].Max(i => nodes[i].Height);
            foreach (var node in layerGroups[l])
            {
                var nodeX = cx[node] + shift - half[node];
                var nodeY = layerY[l] + ((layerHeight - nodes[node].Height) / 2.0);
                rects[node] = new PackedRect(nodeX, nodeY, nodes[node].Width, nodes[node].Height);
                maxRight = Math.Max(maxRight, nodeX + nodes[node].Width);
            }
        }

        var width = maxRight + padding;

        var lastLayerHeight = layerGroups[^1].Count == 0
            ? 0.0
            : layerGroups[^1].Max(i => nodes[i].Height);
        var height = layerY[^1] + lastLayerHeight + padding;

        return new LayeredResult(width, height, rects, layers);
    }

    /// <summary>
    /// Pulls each node in a layer toward the average centre of its Neighbors in the adjacent layer,
    /// then resolves overlaps while preserving the layer's order by averaging an order-preserving
    /// left-to-right and right-to-left placement (both respect the minimum node gap, so their
    /// average does too).
    /// </summary>
    private static void AlignLayer(List<int> layer, List<int>[] Neighbors, double[] cx, double[] half, double nodeGap)
    {
        var count = layer.Count;
        if (count == 0)
        {
            return;
        }

        var desired = new double[count];
        for (var i = 0; i < count; i++)
        {
            var ns = Neighbors[layer[i]];
            desired[i] = ns.Count > 0 ? ns.Average(v => cx[v]) : cx[layer[i]];
        }

        var lr = new double[count];
        lr[0] = desired[0];
        for (var i = 1; i < count; i++)
        {
            var minCentre = lr[i - 1] + half[layer[i - 1]] + nodeGap + half[layer[i]];
            lr[i] = Math.Max(desired[i], minCentre);
        }

        var rl = new double[count];
        rl[count - 1] = desired[count - 1];
        for (var i = count - 2; i >= 0; i--)
        {
            var maxCentre = rl[i + 1] - half[layer[i + 1]] - nodeGap - half[layer[i]];
            rl[i] = Math.Min(desired[i], maxCentre);
        }

        for (var i = 0; i < count; i++)
        {
            cx[layer[i]] = (lr[i] + rl[i]) / 2.0;
        }
    }
}

