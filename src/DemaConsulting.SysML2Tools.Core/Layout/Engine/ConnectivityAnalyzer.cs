// <copyright file="ConnectivityAnalyzer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout.Engine;

/// <summary>
/// A node in the connectivity graph, identified by a stable string. Callers map results back to
/// model elements by index.
/// </summary>
/// <param name="Id">Stable identifier of the node, used only for deterministic tie-breaking.</param>
internal readonly record struct ConnectivityNode(string Id);

/// <summary>
/// A directed edge between two nodes (by index) used for layer hints; treated as undirected for
/// community detection and the affinity adjacency.
/// </summary>
/// <param name="From">Index of the source node.</param>
/// <param name="To">Index of the target node.</param>
internal readonly record struct ConnectivityEdge(int From, int To);

/// <summary>
/// The result of a connectivity analysis: per-node layer hints, per-node community ids, and the
/// sparse undirected adjacency list.
/// </summary>
/// <param name="LayerHints">Longest-path layer of each node (0 = source), in node order.</param>
/// <param name="CommunityIds">Community id of each node (0-based, dense), in node order.</param>
/// <param name="Adjacency">Sparse undirected neighbour lists, one per node in node order.</param>
internal sealed record ConnectivityResult(
    IReadOnlyList<int> LayerHints,
    IReadOnlyList<int> CommunityIds,
    IReadOnlyList<IReadOnlyList<int>> Adjacency);

/// <summary>
/// Analyses graph topology before any geometry is computed: builds a sparse affinity adjacency list,
/// derives longest-path layer hints, and groups nodes into natural communities with label
/// propagation. Hub-and-spoke fans collapse to a single community because each spoke adopts the
/// hub's label even though the spokes have zero mutual affinity.
/// </summary>
/// <remarks>
/// The analyser never materialises a dense n² affinity matrix; adjacency is built in O(m) from the
/// edge list. All passes are deterministic so identical inputs produce identical results.
/// </remarks>
internal static class ConnectivityAnalyzer
{
    /// <summary>Maximum label-propagation rounds before the partition is considered settled.</summary>
    private const int MaxPropagationRounds = 20;

    /// <summary>
    /// Analyses the supplied nodes and edges and returns layer hints, communities, and adjacency.
    /// </summary>
    /// <param name="nodes">Nodes to analyse, in caller order.</param>
    /// <param name="edges">Directed edges (indices into <paramref name="nodes"/>).</param>
    /// <returns>A <see cref="ConnectivityResult"/> with one entry per node in input order.</returns>
    public static ConnectivityResult Analyze(
        IReadOnlyList<ConnectivityNode> nodes,
        IReadOnlyList<ConnectivityEdge> edges)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);

        var n = nodes.Count;
        if (n == 0)
        {
            return new ConnectivityResult([], [], []);
        }

        var adjacency = BuildAdjacency(n, edges);
        var layerHints = ComputeLayerHints(n, edges);
        var communities = DetectCommunities(n, adjacency);
        return new ConnectivityResult(layerHints, communities, adjacency);
    }

    /// <summary>
    /// Counts edge crossings between two adjacent ordered layers using their barycenter positions —
    /// a helper for seed scoring. The score is the number of inversions in the neighbour positions.
    /// </summary>
    /// <param name="upperOrder">Node order of the upper layer.</param>
    /// <param name="lowerOrder">Node order of the lower layer.</param>
    /// <param name="edges">Edges between the two layers (From in upper, To in lower).</param>
    /// <returns>The number of pairwise edge crossings.</returns>
    public static int CrossingScore(
        IReadOnlyList<int> upperOrder,
        IReadOnlyList<int> lowerOrder,
        IReadOnlyList<ConnectivityEdge> edges)
    {
        ArgumentNullException.ThrowIfNull(upperOrder);
        ArgumentNullException.ThrowIfNull(lowerOrder);
        ArgumentNullException.ThrowIfNull(edges);

        var upper = new Dictionary<int, int>();
        for (var i = 0; i < upperOrder.Count; i++)
        {
            upper[upperOrder[i]] = i;
        }

        var lower = new Dictionary<int, int>();
        for (var i = 0; i < lowerOrder.Count; i++)
        {
            lower[lowerOrder[i]] = i;
        }

        var pairs = new List<(int U, int L)>();
        foreach (var e in edges)
        {
            if (upper.TryGetValue(e.From, out var u) && lower.TryGetValue(e.To, out var l))
            {
                pairs.Add((u, l));
            }
        }

        var crossings = 0;
        for (var i = 0; i < pairs.Count; i++)
        {
            for (var j = i + 1; j < pairs.Count; j++)
            {
                if ((pairs[i].U < pairs[j].U && pairs[i].L > pairs[j].L) ||
                    (pairs[i].U > pairs[j].U && pairs[i].L < pairs[j].L))
                {
                    crossings++;
                }
            }
        }

        return crossings;
    }

    /// <summary>Builds the sparse undirected adjacency list, ignoring self-loops and duplicates.</summary>
    private static IReadOnlyList<IReadOnlyList<int>> BuildAdjacency(int n, IReadOnlyList<ConnectivityEdge> edges)
    {
        var sets = new HashSet<int>[n];
        for (var i = 0; i < n; i++)
        {
            sets[i] = [];
        }

        foreach (var e in edges)
        {
            if (e.From < 0 || e.To < 0 || e.From >= n || e.To >= n || e.From == e.To)
            {
                continue;
            }

            sets[e.From].Add(e.To);
            sets[e.To].Add(e.From);
        }

        var result = new IReadOnlyList<int>[n];
        for (var i = 0; i < n; i++)
        {
            var list = sets[i].ToList();
            list.Sort();
            result[i] = list;
        }

        return result;
    }

    /// <summary>Assigns each node a longest-path layer from any source (0 = source), breaking cycles.</summary>
    private static int[] ComputeLayerHints(int n, IReadOnlyList<ConnectivityEdge> edges)
    {
        var outgoing = new List<int>[n];
        var inDegree = new int[n];
        for (var i = 0; i < n; i++)
        {
            outgoing[i] = [];
        }

        // Reverse cycle-causing back edges with a DFS so longest-path layering terminates.
        var kept = BreakCycles(n, edges);
        foreach (var (from, to) in kept)
        {
            outgoing[from].Add(to);
            inDegree[to]++;
        }

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

    /// <summary>Returns the edge set with cycle-causing back edges reversed and self/duplicates dropped.</summary>
    private static List<(int From, int To)> BreakCycles(int n, IReadOnlyList<ConnectivityEdge> edges)
    {
        var adjacency = new List<int>[n];
        for (var i = 0; i < n; i++)
        {
            adjacency[i] = [];
        }

        foreach (var e in edges)
        {
            if (e.From >= 0 && e.To >= 0 && e.From < n && e.To < n && e.From != e.To)
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

            // S4143: standard DFS coloring — onStack is read by recursive calls between assignments.
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

        var result = new List<(int, int)>();
        var seen = new HashSet<(int, int)>();
        foreach (var e in edges)
        {
            if (e.From < 0 || e.To < 0 || e.From >= n || e.To >= n || e.From == e.To)
            {
                continue;
            }

            var (from, to) = backEdges.Contains((e.From, e.To)) ? (e.To, e.From) : (e.From, e.To);
            if (from != to && seen.Add((from, to)))
            {
                result.Add((from, to));
            }
        }

        return result;
    }

    /// <summary>Groups nodes into communities with deterministic label propagation, then renumbers densely.</summary>
    private static int[] DetectCommunities(int n, IReadOnlyList<IReadOnlyList<int>> adjacency)
    {
        var label = new int[n];
        for (var i = 0; i < n; i++)
        {
            label[i] = i;
        }

        for (var round = 0; round < MaxPropagationRounds; round++)
        {
            var changed = false;
            for (var i = 0; i < n; i++)
            {
                var best = MostCommonNeighbourLabel(adjacency[i], label, label[i]);
                if (best != label[i])
                {
                    label[i] = best;
                    changed = true;
                }
            }

            if (!changed)
            {
                break;
            }
        }

        // Renumber labels densely by first appearance for stable, 0-based ids.
        var map = new Dictionary<int, int>();
        var result = new int[n];
        for (var i = 0; i < n; i++)
        {
            if (!map.TryGetValue(label[i], out var id))
            {
                id = map.Count;
                map[label[i]] = id;
            }

            result[i] = id;
        }

        return result;
    }

    /// <summary>Returns the most frequent neighbour label, ties broken toward the smallest label.</summary>
    private static int MostCommonNeighbourLabel(IReadOnlyList<int> neighbours, int[] label, int current)
    {
        if (neighbours.Count == 0)
        {
            return current;
        }

        var counts = new Dictionary<int, int>();
        foreach (var v in neighbours)
        {
            counts[label[v]] = counts.GetValueOrDefault(label[v]) + 1;
        }

        var best = current;
        var bestCount = counts.GetValueOrDefault(current);
        foreach (var (lbl, count) in counts)
        {
            if (count > bestCount || (count == bestCount && lbl < best))
            {
                best = lbl;
                bestCount = count;
            }
        }

        return best;
    }
}
