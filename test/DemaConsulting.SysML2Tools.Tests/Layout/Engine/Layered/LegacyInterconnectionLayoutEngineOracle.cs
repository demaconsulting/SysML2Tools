// <copyright file="LegacyInterconnectionLayoutEngineOracle.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout;
using DemaConsulting.SysML2Tools.Layout.Engine;

namespace DemaConsulting.SysML2Tools.Tests.Layout.Engine.Layered;

/// <summary>
/// Frozen, byte-for-byte copy of the pre-refactor monolithic interconnection layout engine.
/// Retained only as the behavior oracle for the layered-pipeline equivalence tests; the product
/// engine <see cref="InterconnectionLayoutEngine"/> is now a thin façade over the pipeline, and
/// this copy proves the refactor preserves its output exactly.
/// </summary>
internal static class LegacyInterconnectionLayoutEngineOracle
{
    /// <summary>Vertical gap between adjacent nodes stacked within the same layer.</summary>
    private const double NodeSpacing = 30.0;

    /// <summary>Minimum corridor width (node-node spacing) between adjacent columns.</summary>
    private const double CorridorMinWidth = 70.0;

    /// <summary>Slot-to-slot spacing within a corridor (ELK edgeEdgeSpacing).</summary>
    private const double EdgeSpacing = 16.0;

    /// <summary>Clearance from corridor edge to the nearest routing slot (ELK edgeNodeSpacing).</summary>
    private const double ConnectorClearance = 10.0;

    /// <summary>Uniform padding added around the placed content.</summary>
    private const double Padding = 20.0;

    /// <summary>Number of Barycenter ordering sweeps (down + up = one round).</summary>
    private const int BarycentricSweeps = 4;

    /// <summary>Tolerance for treating a segment as straight (no bend points needed).</summary>
    private const double StraightTolerance = 1e-6;

    // ── Augmented-graph types ────────────────────────────────────────────────

    /// <summary>A node in the augmented Sugiyama graph (real part box or long-edge dummy).</summary>
    private sealed record AugNode(double Width, double Height, int Layer, bool IsDummy = false);

    /// <summary>A sub-edge in the augmented graph after long-edge splitting.</summary>
    private readonly record struct AugEdge(int Source, int Target, int OrigEdgeIndex);

    // ── Routing types ────────────────────────────────────────────────────────

    /// <summary>
    /// A routing segment: one sub-edge crossing a single corridor, carrying its ELK port
    /// coordinates and the slot assigned by topological numbering.
    /// </summary>
    private sealed class Segment
    {
        public int AugEdgeIndex;

        /// <summary>Y coordinate of the EAST (source-layer) port (ELK incomingConnectionCoordinates).</summary>
        public double SourceY;

        /// <summary>Y coordinate of the WEST (target-layer) port (ELK outgoingConnectionCoordinates).</summary>
        public double TargetY;

        /// <summary>Min of SourceY and TargetY, used for ELK crossing-count computation.</summary>
        public double Lo;

        /// <summary>Max of SourceY and TargetY, used for ELK crossing-count computation.</summary>
        public double Hi;

        /// <summary>Assigned routing slot (column offset within corridor). Set by topological numbering.</summary>
        public int RoutingSlot;

        /// <summary>Remaining in-degree during topological BFS; initialised from Incoming.Count.</summary>
        public int InWeight;


        /// <summary>Dependencies for which this segment must precede the target.</summary>
        public List<SegDep> Outgoing { get; } = [];

        /// <summary>Dependencies for which a predecessor must precede this segment.</summary>
        public List<SegDep> Incoming { get; } = [];
    }

    /// <summary>
    /// A directed dependency between two routing segments: the source segment prefers
    /// to occupy a lower slot than the target segment (ELK HyperEdgeSegmentDependency).
    /// </summary>
    private sealed class SegDep
    {
        public Segment Source { get; }
        public Segment Target { get; }
        public int Weight { get; }

        public SegDep(Segment src, Segment tgt, int weight)
        {
            Source = src;
            Target = tgt;
            Weight = weight;
            src.Outgoing.Add(this);
            tgt.Incoming.Add(this);
        }

        /// <summary>Removes this dependency from both endpoints' adjacency lists.</summary>
        public void Remove()
        {
            Source.Outgoing.Remove(this);
            Target.Incoming.Remove(this);
        }
    }

    // ── Public entry point ───────────────────────────────────────────────────

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

        // Phase 1: make the graph acyclic and assign longest-path layers.
        var acyclic = BreakCycles(n, edges);
        var nodeLayers = AssignLayers(n, acyclic);

        // Phase 1.5: insert one dummy node per intermediate layer on each long edge.
        var (augNodes, augEdges) = InsertLongEdgeDummies(n, nodes, nodeLayers, acyclic);

        // Phase 2: Barycenter ordering on the augmented graph.
        var groups = GroupByLayerAug(augNodes);
        OrderLayersAug(groups, augNodes.Count, augEdges);

        // Phase 3: coordinate assignment (stacking + corridor widths).
        var (augX, augY, columnX, maxColWidth) = AssignCoordinatesAug(augNodes, groups, augEdges);

        // Phase 4: port distribution + ELK slot routing.
        var waypoints = BuildRoutesAug(n, nodes, augNodes, augEdges, acyclic, augX, augY, columnX, maxColWidth);

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

        return new LayerResult(rects, totalWidth, totalHeight, nodeLayers, waypoints);
    }

    // ── Phase 1: Cycle breaking ──────────────────────────────────────────────

    /// <summary>
    /// Returns the edge set with cycle-causing back edges reversed, using DFS to classify any
    /// edge to a node still on the recursion stack as a back edge.
    /// </summary>
    private static List<LayerEdge> BreakCycles(int n, IReadOnlyList<LayerEdge> edges)
    {
        var adjacency = new List<int>[n];
        for (var i = 0; i < n; i++)
        {
            adjacency[i] = [];
        }

        foreach (var e in edges)
        {
            if (e.Source != e.Target)
            {
                adjacency[e.Source].Add(e.Target);
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

        var result = new List<LayerEdge>();
        var seen = new HashSet<(int, int)>();
        foreach (var e in edges)
        {
            if (e.Source == e.Target)
            {
                continue;
            }

            var (from, to) = backEdges.Contains((e.Source, e.Target))
                ? (e.Target, e.Source)
                : (e.Source, e.Target);

            if (from != to && seen.Add((from, to)))
            {
                result.Add(new LayerEdge(from, to));
            }
        }

        return result;
    }

    // ── Phase 1: Longest-path layer assignment ───────────────────────────────

    /// <summary>
    /// Assigns each node to a layer equal to the length of its longest incoming path
    /// (sources at layer 0, sinks at the maximum layer).
    /// </summary>
    private static int[] AssignLayers(int n, List<LayerEdge> edges)
    {
        var outgoing = new List<int>[n];
        var inDegree = new int[n];
        for (var i = 0; i < n; i++)
        {
            outgoing[i] = [];
        }

        foreach (var e in edges)
        {
            outgoing[e.Source].Add(e.Target);
            inDegree[e.Target]++;
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

    // ── Phase 1.5: Long-edge dummy insertion (ELK LongEdgeSplitter) ──────────

    /// <summary>
    /// Splits every edge spanning more than one layer into a chain of unit-span sub-edges by
    /// inserting one zero-size dummy node at each intermediate layer, following
    /// ELK's <c>LongEdgeSplitter</c> phase.
    /// </summary>
    private static (List<AugNode> AugNodes, List<AugEdge> AugEdges) InsertLongEdgeDummies(
        int n,
        IReadOnlyList<LayerNode> nodes,
        int[] nodeLayers,
        List<LayerEdge> acyclic)
    {
        var augNodes = new List<AugNode>(n + acyclic.Count);
        for (var i = 0; i < n; i++)
        {
            augNodes.Add(new AugNode(nodes[i].Width, nodes[i].Height, nodeLayers[i]));
        }

        var augEdges = new List<AugEdge>(acyclic.Count * 2);
        for (var e = 0; e < acyclic.Count; e++)
        {
            var edge = acyclic[e];
            var span = nodeLayers[edge.Target] - nodeLayers[edge.Source];

            if (span <= 0)
            {
                continue;
            }

            if (span == 1)
            {
                augEdges.Add(new AugEdge(edge.Source, edge.Target, e));
            }
            else
            {
                // Chain: src → d1 → d2 → … → tgt with one dummy per intermediate layer.
                var prev = edge.Source;
                for (var l = nodeLayers[edge.Source] + 1; l < nodeLayers[edge.Target]; l++)
                {
                    var dIdx = augNodes.Count;
                    augNodes.Add(new AugNode(0.0, 0.0, l, IsDummy: true));
                    augEdges.Add(new AugEdge(prev, dIdx, e));
                    prev = dIdx;
                }

                augEdges.Add(new AugEdge(prev, edge.Target, e));
            }
        }

        return (augNodes, augEdges);
    }

    // ── Phase 2: Barycenter ordering on augmented graph ──────────────────────

    /// <summary>Groups augmented-node indices by layer.</summary>
    private static List<List<int>> GroupByLayerAug(List<AugNode> augNodes)
    {
        var maxLayer = augNodes.Max(a => a.Layer);
        var groups = new List<List<int>>(maxLayer + 1);
        for (var l = 0; l <= maxLayer; l++)
        {
            groups.Add([]);
        }

        for (var i = 0; i < augNodes.Count; i++)
        {
            groups[augNodes[i].Layer].Add(i);
        }

        return groups;
    }

    /// <summary>
    /// Runs <see cref="BarycentricSweeps"/> Barycenter sweeps over the augmented graph
    /// (real nodes and dummies) to reduce edge crossings.
    /// </summary>
    private static void OrderLayersAug(List<List<int>> groups, int numAug, List<AugEdge> augEdges)
    {
        var leftNeighbors = new List<int>[numAug];
        var rightNeighbors = new List<int>[numAug];
        for (var i = 0; i < numAug; i++)
        {
            leftNeighbors[i] = [];
            rightNeighbors[i] = [];
        }

        foreach (var ae in augEdges)
        {
            rightNeighbors[ae.Source].Add(ae.Target);
            leftNeighbors[ae.Target].Add(ae.Source);
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
    /// Sorts <paramref name="layer"/> by the average position of each node's neighbors in
    /// <paramref name="adjacentLayer"/>; nodes without neighbors keep their current relative order.
    /// </summary>
    private static void SortByBarycenter(List<int> layer, List<int> adjacentLayer, List<int>[] neighbors)
    {
        var position = new Dictionary<int, int>();
        for (var i = 0; i < adjacentLayer.Count; i++)
        {
            position[adjacentLayer[i]] = i;
        }

        var keyed = new List<(int Node, double Key, int Original)>(layer.Count);
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

    // ── Phase 3: Coordinate assignment (BK Y-placement + ELK placeNodesHorizontally) ──────

    /// <summary>
    /// Assigns absolute X and Y coordinates to all augmented nodes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Y assignment follows the Brandes-Köpf (BK) four-layout balanced algorithm
    /// (<c>BKNodePlacer</c> + <c>BKAligner</c> + <c>BKCompactor</c>): four independent
    /// vertical alignments (DOWN/UP × RIGHT/LEFT) are compacted and their per-node medians
    /// averaged to produce port-aligned, crossing-minimized vertical positions.
    /// </para>
    /// <para>
    /// X assignment follows ELK's <c>LGraphUtil.placeNodesHorizontally</c>: dummies are placed at
    /// the horizontal center of their column; real nodes are left-aligned to their column start.
    /// Corridor widths are derived from sub-edge counts per corridor.
    /// </para>
    /// </remarks>
    private static (double[] AugX, double[] AugY, double[] ColumnX, double[] MaxColWidth) AssignCoordinatesAug(
        List<AugNode> augNodes,
        List<List<int>> groups,
        List<AugEdge> augEdges)
    {
        var layerCount = groups.Count;
        var numAug = augNodes.Count;

        // Maximum real-node width per layer (dummies have width 0).
        var maxColWidth = new double[layerCount];
        for (var i = 0; i < numAug; i++)
        {
            if (!augNodes[i].IsDummy)
            {
                maxColWidth[augNodes[i].Layer] = Math.Max(maxColWidth[augNodes[i].Layer], augNodes[i].Width);
            }
        }

        // Sub-edges per corridor: one sub-edge per augEdge, keyed on source layer.
        var corridorEdgeCounts = new int[Math.Max(1, layerCount - 1)];
        foreach (var ae in augEdges)
        {
            var l = augNodes[ae.Source].Layer;
            if (l >= 0 && l < corridorEdgeCounts.Length)
            {
                corridorEdgeCounts[l]++;
            }
        }

        // Column X positions. Corridor width: ELK routingWidth = 2*edgeNodeSpacing + (n-1)*edgeEdgeSpacing.
        var columnX = new double[layerCount];
        columnX[0] = Padding;
        for (var l = 1; l < layerCount; l++)
        {
            var cnt = corridorEdgeCounts[l - 1];
            var corridorWidth = cnt > 0
                ? Math.Max(CorridorMinWidth, (2.0 * ConnectorClearance) + ((cnt - 1) * EdgeSpacing))
                : CorridorMinWidth;
            columnX[l] = columnX[l - 1] + maxColWidth[l - 1] + corridorWidth;
        }

        // Assign X coordinates: dummies are centered in their column; real nodes left-align.
        var augX = new double[numAug];
        for (var l = 0; l < layerCount; l++)
        {
            var colCenterX = columnX[l] + (maxColWidth[l] / 2.0);
            foreach (var ni in groups[l])
            {
                augX[ni] = augNodes[ni].IsDummy ? colCenterX : columnX[l];
            }
        }

        // Assign Y coordinates using the Brandes-Köpf balanced four-layout algorithm.
        var augY = BkAssignYCoordinates(augNodes, groups, augEdges);

        return (augX, augY, columnX, maxColWidth);
    }

    // ── Phase 3 helpers: Brandes-Köpf Y-coordinate assignment ────────────────

    /// <summary>
    /// Assigns Y coordinates to all augmented nodes using the four-layout Brandes-Köpf
    /// balanced algorithm, producing port-aligned vertical positions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runs four independent (vDown × hRight) alignment-and-compaction pipelines, one for
    /// each combination of vertical scan direction (DOWN = top-to-bottom, UP = bottom-to-top)
    /// and horizontal scan direction (RIGHT = layer 0 → max, LEFT = layer max → 0). The
    /// per-node average of the two middle values of the four results gives the final balanced
    /// position. Padding is added once at the end.
    /// </para>
    /// <para>
    /// Corresponds to ELK's BKNodePlacer orchestrating BKAligner and BKCompactor.
    /// </para>
    /// </remarks>
    private static double[] BkAssignYCoordinates(
        List<AugNode> augNodes,
        List<List<int>> groups,
        List<AugEdge> augEdges)
    {
        var numAug = augNodes.Count;

        // Step 0: precompute port positions, layer positions, and neighbor-edge lists.
        BkPreprocess(
            augNodes, groups, augEdges,
            out var posInLayer,
            out var srcRelPortY,
            out var tgtRelPortY,
            out var leftNeighborEdges,
            out var rightNeighborEdges);

        // Step 1: mark type-1 conflicts (non-inner segments crossing inner segments).
        var markedEdges = BkMarkConflicts(augNodes, groups, augEdges, posInLayer, leftNeighborEdges);

        // Steps 2–4: compute four independent (vDown × hRight) layouts.
        var layouts = new double[4][];
        for (var d = 0; d < 4; d++)
        {
            // d=0: DOWN+RIGHT, d=1: UP+RIGHT, d=2: DOWN+LEFT, d=3: UP+LEFT.
            var vDown = d % 2 == 0;
            var hRight = d < 2;

            // Step 2: vertical alignment — builds block chains along the scan direction.
            BkVerticalAlignment(
                augNodes, groups, augEdges, posInLayer,
                leftNeighborEdges, rightNeighborEdges,
                markedEdges, vDown, hRight,
                out var root, out var align);

            // Step 3: inside-block shift — adjusts nodes within each block to align ports.
            var innerShift = BkInsideBlockShift(
                augNodes, augEdges, root, align,
                srcRelPortY, tgtRelPortY, hRight,
                rightNeighborEdges, leftNeighborEdges);

            // Step 4: horizontal compaction — assigns absolute Y to each block root.
            var blockY = BkHorizontalCompaction(augNodes, groups, root, align, innerShift, posInLayer, vDown);

            // Compute absolute Y for every node in this layout.
            var y = new double[numAug];
            for (var i = 0; i < numAug; i++)
            {
                y[i] = blockY[root[i]] + innerShift[i];
            }

            layouts[d] = y;
        }

        // Step 5: normalize each layout and return the balanced (median average) result.
        return BkBalancedLayout(layouts, numAug);
    }

    /// <summary>
    /// Precomputes the lookup tables required by all four Brandes-Köpf layout passes:
    /// per-node layer position, relative port Y offsets, and sorted neighbor-edge lists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Port positions follow ELK's BKAligner preprocessing convention: a dummy node
    /// contributes relative port Y = 0 (the wire passes straight through); a real node
    /// distributes its ports evenly between <see cref="ConnectorClearance"/> insets, or
    /// at the midpoint when it has only one port on that face.
    /// </para>
    /// <para>
    /// <paramref name="leftNeighborEdges"/>[v] lists augmented-edge indices whose target is v,
    /// sorted ascending by the source node's position within its layer — used for RIGHT-direction
    /// alignment. <paramref name="rightNeighborEdges"/>[v] lists augmented-edge indices whose
    /// source is v, sorted ascending by the target node's position — used for LEFT-direction
    /// alignment. Both lists are stable-sorted by edge index as a tiebreaker.
    /// </para>
    /// </remarks>
    private static void BkPreprocess(
        List<AugNode> augNodes,
        List<List<int>> groups,
        List<AugEdge> augEdges,
        out int[] posInLayer,
        out double[] srcRelPortY,
        out double[] tgtRelPortY,
        out List<int>[] leftNeighborEdges,
        out List<int>[] rightNeighborEdges)
    {
        var numAug = augNodes.Count;
        var numEdges = augEdges.Count;

        // Position of each node within its layer group (0-based index in groups[layer]).
        posInLayer = new int[numAug];
        for (var l = 0; l < groups.Count; l++)
        {
            for (var k = 0; k < groups[l].Count; k++)
            {
                posInLayer[groups[l][k]] = k;
            }
        }

        // Collect outgoing/incoming edge indices per node for port computation.
        // Capture posInLayer in a local so it can be used inside lambda expressions
        // (C# prohibits capturing out parameters directly in lambdas — CS1628).
        var posLayer = posInLayer;
        var outEdges = new List<int>[numAug];
        var inEdges = new List<int>[numAug];
        for (var i = 0; i < numAug; i++)
        {
            outEdges[i] = [];
            inEdges[i] = [];
        }

        for (var ei = 0; ei < numEdges; ei++)
        {
            outEdges[augEdges[ei].Source].Add(ei);
            inEdges[augEdges[ei].Target].Add(ei);
        }

        // srcRelPortY[e]: Y of the source (EAST) port relative to source node's top-left.
        // Dummy nodes pass the wire through at Y = 0 relative to their own position.
        srcRelPortY = new double[numEdges];
        for (var ni = 0; ni < numAug; ni++)
        {
            var edges = outEdges[ni];
            if (edges.Count == 0)
            {
                continue;
            }

            if (augNodes[ni].IsDummy)
            {
                foreach (var ei in edges)
                {
                    srcRelPortY[ei] = 0.0;
                }
            }
            else
            {
                // Sort by target's position in its layer, then edge index for stability.
                var sorted = edges
                    .OrderBy(ei => posLayer[augEdges[ei].Target])
                    .ThenBy(ei => ei)
                    .ToList();
                var portCount = sorted.Count;
                for (var k = 0; k < portCount; k++)
                {
                    srcRelPortY[sorted[k]] = portCount == 1
                        ? augNodes[ni].Height / 2.0
                        : ConnectorClearance + (k * (augNodes[ni].Height - (2.0 * ConnectorClearance)) / (portCount - 1));
                }
            }
        }

        // tgtRelPortY[e]: Y of the target (WEST) port relative to target node's top-left.
        tgtRelPortY = new double[numEdges];
        for (var ni = 0; ni < numAug; ni++)
        {
            var edges = inEdges[ni];
            if (edges.Count == 0)
            {
                continue;
            }

            if (augNodes[ni].IsDummy)
            {
                foreach (var ei in edges)
                {
                    tgtRelPortY[ei] = 0.0;
                }
            }
            else
            {
                // Sort by source's position in its layer, then edge index for stability.
                var sorted = edges
                    .OrderBy(ei => posLayer[augEdges[ei].Source])
                    .ThenBy(ei => ei)
                    .ToList();
                var portCount = sorted.Count;
                for (var k = 0; k < portCount; k++)
                {
                    tgtRelPortY[sorted[k]] = portCount == 1
                        ? augNodes[ni].Height / 2.0
                        : ConnectorClearance + (k * (augNodes[ni].Height - (2.0 * ConnectorClearance)) / (portCount - 1));
                }
            }
        }

        // leftNeighborEdges[v]: edges whose Target == v, sorted by source posInLayer.
        leftNeighborEdges = new List<int>[numAug];
        for (var i = 0; i < numAug; i++)
        {
            leftNeighborEdges[i] = [];
        }

        for (var ei = 0; ei < numEdges; ei++)
        {
            leftNeighborEdges[augEdges[ei].Target].Add(ei);
        }

        for (var i = 0; i < numAug; i++)
        {
            leftNeighborEdges[i].Sort((a, b) =>
            {
                var c = posLayer[augEdges[a].Source].CompareTo(posLayer[augEdges[b].Source]);
                return c != 0 ? c : a.CompareTo(b);
            });
        }

        // rightNeighborEdges[v]: edges whose Source == v, sorted by target posInLayer.
        rightNeighborEdges = new List<int>[numAug];
        for (var i = 0; i < numAug; i++)
        {
            rightNeighborEdges[i] = [];
        }

        for (var ei = 0; ei < numEdges; ei++)
        {
            rightNeighborEdges[augEdges[ei].Source].Add(ei);
        }

        for (var i = 0; i < numAug; i++)
        {
            rightNeighborEdges[i].Sort((a, b) =>
            {
                var c = posLayer[augEdges[a].Target].CompareTo(posLayer[augEdges[b].Target]);
                return c != 0 ? c : a.CompareTo(b);
            });
        }
    }

    /// <summary>
    /// Returns true when <paramref name="v"/> is incident to an inner segment — a sub-edge
    /// where both endpoints are dummy nodes, forming part of a long-edge chain.
    /// </summary>
    /// <remarks>
    /// An inner segment has both endpoints as dummy nodes. The check inspects v's
    /// lowest-positioned left neighbor (leftNeighborEdges[v][0]). Used by type-1 conflict
    /// detection to identify nodes that anchor inner segments across layer boundaries.
    /// </remarks>
    private static bool IsIncidentToInnerSegment(
        int v,
        List<AugNode> augNodes,
        List<AugEdge> augEdges,
        List<int>[] leftNeighborEdges)
        => augNodes[v].IsDummy
            && leftNeighborEdges[v].Count > 0
            && augNodes[augEdges[leftNeighborEdges[v][0]].Source].IsDummy;

    /// <summary>
    /// Marks augmented edges that participate in type-1 conflicts: a non-inner segment
    /// (at least one real-node endpoint) that crosses an inner segment (both endpoints
    /// are dummy nodes from a long-edge chain).
    /// </summary>
    /// <remarks>
    /// Implements ELK's <c>markConflicts</c> procedure from BKNodePlacer. For each pair of
    /// adjacent middle layers (i, i+1), the algorithm tracks the permitted source-position
    /// range [k0, k1] established by each inner segment and marks any non-inner segment
    /// whose source falls outside that range. Marked edges are excluded from vertical
    /// alignment to preserve the topology of inner segments.
    /// </remarks>
    private static HashSet<int> BkMarkConflicts(
        List<AugNode> augNodes,
        List<List<int>> groups,
        List<AugEdge> augEdges,
        int[] posInLayer,
        List<int>[] leftNeighborEdges)
    {
        var markedEdges = new HashSet<int>();
        var maxLayer = groups.Count - 1;

        // Examine middle layers: i is the source side, i+1 is the target side.
        for (var i = 1; i < maxLayer; i++)
        {
            var leftLayerSize = groups[i].Count;
            var rightLayer = groups[i + 1];

            // k0: lower bound of the permitted source-position range for the current batch.
            var k0 = 0;

            // l: left cursor into the right layer (start of the current batch).
            var l = 0;

            for (var l1 = 0; l1 < rightLayer.Count; l1++)
            {
                var v = rightLayer[l1];
                var incident = IsIncidentToInnerSegment(v, augNodes, augEdges, leftNeighborEdges);

                // Flush the batch at the last node or at each inner-segment anchor.
                if (l1 != rightLayer.Count - 1 && !incident)
                {
                    continue;
                }

                // k1: upper bound of the permitted source-position range for this batch.
                var k1 = leftLayerSize - 1;
                if (incident)
                {
                    var innerEdge = leftNeighborEdges[v][0];
                    k1 = posInLayer[augEdges[innerEdge].Source];
                }

                // Mark non-inner segments in the batch whose sources are out of range.
                while (l <= l1)
                {
                    var vl = rightLayer[l];
                    if (!IsIncidentToInnerSegment(vl, augNodes, augEdges, leftNeighborEdges))
                    {
                        foreach (var edgeIdx in leftNeighborEdges[vl])
                        {
                            var k = posInLayer[augEdges[edgeIdx].Source];
                            if (k < k0 || k > k1)
                            {
                                markedEdges.Add(edgeIdx);
                            }
                        }
                    }

                    l++;
                }

                k0 = k1;
            }
        }

        return markedEdges;
    }

    /// <summary>
    /// Performs vertical alignment for one Brandes-Köpf layout direction, building the
    /// circular block-chain structure that groups co-aligned nodes into blocks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implements ELK's BKAligner.verticalAlignment. Each node v is aligned with the
    /// median neighbor in the previous (RIGHT) or next (LEFT) layer that is still
    /// unaligned (align[v] == v) and whose layer-position satisfies the monotone
    /// constraint r. The monotone constraint prevents crossings between aligned pairs.
    /// </para>
    /// <para>
    /// On output, <paramref name="root"/>[i] identifies the block root for every node i,
    /// and <paramref name="align"/>[i] is the next node in the circular chain
    /// (root → n1 → n2 → … → root).
    /// </para>
    /// </remarks>
    private static void BkVerticalAlignment(
        List<AugNode> augNodes,
        List<List<int>> groups,
        List<AugEdge> augEdges,
        int[] posInLayer,
        List<int>[] leftNeighborEdges,
        List<int>[] rightNeighborEdges,
        HashSet<int> markedEdges,
        bool vDown,
        bool hRight,
        out int[] root,
        out int[] align)
    {
        var numAug = augNodes.Count;
        var maxLayer = groups.Count - 1;

        // Every node starts as its own singleton block.
        root = new int[numAug];
        align = new int[numAug];
        for (var i = 0; i < numAug; i++)
        {
            root[i] = i;
            align[i] = i;
        }

        // Layer iteration order: RIGHT scans forward (0..maxLayer); LEFT scans in reverse.
        var layerStart = hRight ? 0 : maxLayer;
        var layerEnd = hRight ? maxLayer : 0;
        var layerStep = hRight ? 1 : -1;

        for (var l = layerStart; hRight ? l <= layerEnd : l >= layerEnd; l += layerStep)
        {
            var layer = groups[l];

            // r: monotone position constraint; tracks the last aligned neighbor's position.
            var r = vDown ? -1 : int.MaxValue;

            // Node iteration order: DOWN scans forward (0..N-1); UP scans in reverse.
            var nodeStart = vDown ? 0 : layer.Count - 1;
            var nodeEnd = vDown ? layer.Count - 1 : 0;
            var nodeStep = vDown ? 1 : -1;

            for (var ni = nodeStart; vDown ? ni <= nodeEnd : ni >= nodeEnd; ni += nodeStep)
            {
                var v = layer[ni];
                var neighbors = hRight ? leftNeighborEdges[v] : rightNeighborEdges[v];
                var d = neighbors.Count;
                if (d == 0)
                {
                    continue;
                }

                // Median index range for this node's neighbor list.
                var low = (int)Math.Floor((d + 1) / 2.0) - 1;
                var high = (int)Math.Ceiling((d + 1) / 2.0) - 1;

                // Try median neighbors in vdir order; stop as soon as v is aligned.
                var mStart = vDown ? low : high;
                var mEnd = vDown ? high : low;
                var mStep = vDown ? 1 : -1;

                for (var m = mStart; vDown ? m <= mEnd : m >= mEnd; m += mStep)
                {
                    // Stop iterating once v has been aligned with a neighbor.
                    if (align[v] != v)
                    {
                        break;
                    }

                    var edgeIdx = neighbors[m];
                    var uIdx = hRight ? augEdges[edgeIdx].Source : augEdges[edgeIdx].Target;

                    if (markedEdges.Contains(edgeIdx))
                    {
                        continue;
                    }

                    var pos = posInLayer[uIdx];
                    if (vDown ? r < pos : r > pos)
                    {
                        // Extend the block chain: insert v between uIdx and the current root.
                        align[uIdx] = v;
                        root[v] = root[uIdx];
                        align[v] = root[v];
                        r = pos;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Computes the inside-block shift for each node: the vertical offset relative to its
    /// block root that makes the connecting ports co-linear within the block.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implements ELK's BKAligner.insideBlockShift. For each block root, walks the circular
    /// align chain accumulating port-position differences between consecutive nodes. The
    /// accumulated shifts are then normalized so the topmost node in the block has
    /// innerShift = 0, making blockY the absolute Y of the block's highest point.
    /// </para>
    /// <para>
    /// For hdir=RIGHT the edge between consecutive chain nodes goes source → target (earlier
    /// layer to later layer), so portDiff = srcRelPortY − tgtRelPortY. For hdir=LEFT the
    /// direction reverses, so portDiff = tgtRelPortY − srcRelPortY.
    /// </para>
    /// </remarks>
    private static double[] BkInsideBlockShift(
        List<AugNode> augNodes,
        List<AugEdge> augEdges,
        int[] root,
        int[] align,
        double[] srcRelPortY,
        double[] tgtRelPortY,
        bool hRight,
        List<int>[] rightNeighborEdges,
        List<int>[] leftNeighborEdges)
    {
        var numAug = augNodes.Count;
        var innerShift = new double[numAug];

        // Process each block identified by its root node.
        for (var r = 0; r < numAug; r++)
        {
            if (root[r] != r)
            {
                continue;
            }

            // Walk the circular chain accumulating port-difference shifts.
            var spaceAbove = 0.0;
            var spaceBelow = augNodes[r].Height;

            var current = r;
            var next = align[r];
            while (next != r)
            {
                // Locate the augmented edge that links consecutive block-chain nodes.
                // For RIGHT: edge current → next (earlier → later layer).
                // For LEFT: edge next → current (earlier → later layer, chain walks backward).
                var edgeIdx = hRight
                    ? BkFindEdge(rightNeighborEdges[current], augEdges, next, findByTarget: true)
                    : BkFindEdge(leftNeighborEdges[current], augEdges, next, findByTarget: false);

                // Port alignment: accumulate the source-minus-target port offset.
                var portDiff = hRight
                    ? srcRelPortY[edgeIdx] - tgtRelPortY[edgeIdx]
                    : tgtRelPortY[edgeIdx] - srcRelPortY[edgeIdx];

                innerShift[next] = innerShift[current] + portDiff;
                spaceAbove = Math.Max(spaceAbove, -innerShift[next]);
                spaceBelow = Math.Max(spaceBelow, innerShift[next] + augNodes[next].Height);

                current = next;
                next = align[current];
            }

            // Normalize: add spaceAbove to all shifts so the topmost node is at offset 0.
            if (spaceAbove > 0.0)
            {
                var node = r;
                do
                {
                    innerShift[node] += spaceAbove;
                    node = align[node];
                }
                while (node != r);
            }
        }

        return innerShift;
    }

    /// <summary>
    /// Finds the augmented edge in <paramref name="edgeList"/> that connects to
    /// <paramref name="matchNode"/>, searching by target when
    /// <paramref name="findByTarget"/> is true, or by source otherwise.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="BkInsideBlockShift"/> to locate the edge that links consecutive
    /// nodes in a block chain. Because block chains are built strictly along real augmented
    /// edges, the edge is always present; returning −1 would indicate a logic error in the
    /// vertical-alignment phase.
    /// </remarks>
    private static int BkFindEdge(
        List<int> edgeList,
        List<AugEdge> augEdges,
        int matchNode,
        bool findByTarget)
    {
        foreach (var ei in edgeList)
        {
            if ((findByTarget ? augEdges[ei].Target : augEdges[ei].Source) == matchNode)
            {
                return ei;
            }
        }

        return -1; // Should not occur: block chains are always built along real edges.
    }

    /// <summary>
    /// Assigns an absolute Y coordinate (blockY) to each block root by compacting blocks
    /// in the vertical direction, respecting per-node heights and <see cref="NodeSpacing"/> gaps.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implements ELK's BKCompactor.horizontalCompaction / placeBlock. For vdir=DOWN,
    /// blocks are packed top-to-bottom starting at Y = 0, constrained from above by the
    /// node immediately above each chain member in its layer. For vdir=UP, blocks are
    /// packed bottom-to-top starting at Y = 0, constrained from below (yielding negative
    /// values that are normalized in <see cref="BkBalancedLayout"/>).
    /// </para>
    /// <para>
    /// The local <c>PlaceBlock</c> function places a block root's Y coordinate by recursively
    /// ensuring every constraining block is placed first (memoized via a NaN sentinel).
    /// </para>
    /// </remarks>
    private static double[] BkHorizontalCompaction(
        List<AugNode> augNodes,
        List<List<int>> groups,
        int[] root,
        int[] align,
        double[] innerShift,
        int[] posInLayer,
        bool vDown)
    {
        var maxLayer = groups.Count - 1;
        var blockY = new double[augNodes.Count];
        Array.Fill(blockY, double.NaN);

        // Recursively place a block root, memoized by the NaN-unplaced sentinel.
        void PlaceBlock(int v)
        {
            if (!double.IsNaN(blockY[v]))
            {
                return;
            }

            blockY[v] = 0.0;

            // Enforce separation constraints for every node in this block's chain.
            var current = v;
            do
            {
                var layer = augNodes[current].Layer;
                var idx = posInLayer[current];
                var layerNodes = groups[layer];

                if (vDown)
                {
                    // DOWN: constrain from above — current must be below the node at idx−1.
                    if (idx > 0)
                    {
                        var above = layerNodes[idx - 1];
                        var aboveRoot = root[above];
                        PlaceBlock(aboveRoot);

                        var requiredY = blockY[aboveRoot]
                            + innerShift[above]
                            + augNodes[above].Height
                            + NodeSpacing
                            - innerShift[current];
                        blockY[v] = Math.Max(blockY[v], requiredY);
                    }
                }
                else
                {
                    // UP: constrain from below — current must be above the node at idx+1.
                    if (idx < layerNodes.Count - 1)
                    {
                        var below = layerNodes[idx + 1];
                        var belowRoot = root[below];
                        PlaceBlock(belowRoot);

                        var requiredY = blockY[belowRoot]
                            + innerShift[below]
                            - NodeSpacing
                            - augNodes[current].Height
                            - innerShift[current];
                        blockY[v] = Math.Min(blockY[v], requiredY);
                    }
                }

                current = align[current];
            }
            while (current != v);
        }

        // Trigger placement for all block roots in vdir processing order.
        var layerStart = vDown ? 0 : maxLayer;
        var layerEnd = vDown ? maxLayer : 0;
        var layerStep = vDown ? 1 : -1;

        for (var l = layerStart; vDown ? l <= layerEnd : l >= layerEnd; l += layerStep)
        {
            var layer = groups[l];
            var nodeStart = vDown ? 0 : layer.Count - 1;
            var nodeEnd = vDown ? layer.Count - 1 : 0;
            var nodeStep = vDown ? 1 : -1;

            for (var ni = nodeStart; vDown ? ni <= nodeEnd : ni >= nodeEnd; ni += nodeStep)
            {
                var v = layer[ni];
                if (root[v] == v)
                {
                    PlaceBlock(v);
                }
            }
        }

        return blockY;
    }

    /// <summary>
    /// Normalizes four independent Brandes-Köpf layouts (each shifted so its minimum Y = 0)
    /// and returns the per-node average of the two middle values, giving the balanced result.
    /// </summary>
    /// <remarks>
    /// Implements ELK's BKNodePlacer balanced-layout combination. Each of the four layouts
    /// has a distinct direction bias (DOWN/UP × RIGHT/LEFT); the median average cancels
    /// those biases while preserving the port-alignment constraints of each individual
    /// layout. <see cref="Padding"/> is added once so all returned coordinates are absolute.
    /// </remarks>
    private static double[] BkBalancedLayout(double[][] layouts, int numAug)
    {
        // Normalize each layout: shift so the minimum absolute Y across all nodes is 0.
        foreach (var y in layouts)
        {
            var minY = y.Min();
            for (var i = 0; i < numAug; i++)
            {
                y[i] -= minY;
            }
        }

        // For each node, sort the four Y values and average the two middle ones.
        var finalY = new double[numAug];
        for (var i = 0; i < numAug; i++)
        {
            var ys = new[] { layouts[0][i], layouts[1][i], layouts[2][i], layouts[3][i] };
            Array.Sort(ys);
            finalY[i] = ((ys[1] + ys[2]) / 2.0) + Padding;
        }

        return finalY;
    }

    // ── Phase 4: Port distribution + ELK slot routing ────────────────────────

    /// <summary>
    /// Distributes ports on each box face and routes all corridors using ELK's
    /// <c>OrthogonalRoutingGenerator</c> slot algorithm.
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<Point2D>> BuildRoutesAug(
        int n,
        IReadOnlyList<LayerNode> nodes,
        List<AugNode> augNodes,
        List<AugEdge> augEdges,
        List<LayerEdge> acyclic,
        double[] augX,
        double[] augY,
        double[] columnX,
        double[] maxColWidth)
    {
        var numAugEdges = augEdges.Count;
        var numOrigEdges = acyclic.Count;

        // Port Y values: augPortYSrc[i] = source (right face) Y; augPortYTgt[i] = target (left face) Y.
        var augPortYSrc = new double[numAugEdges];
        var augPortYTgt = new double[numAugEdges];

        // Distribute outgoing (source-side) ports on each real node's right face.
        var outByNode = new Dictionary<int, List<int>>();
        for (var ei = 0; ei < numAugEdges; ei++)
        {
            var src = augEdges[ei].Source;
            if (!outByNode.TryGetValue(src, out var list))
            {
                list = [];
                outByNode[src] = list;
            }

            list.Add(ei);
        }

        foreach (var (ni, edgeList) in outByNode)
        {
            if (augNodes[ni].IsDummy)
            {
                // Dummies pass the wire straight through at their own Y.
                foreach (var ei in edgeList)
                {
                    augPortYSrc[ei] = augY[ni];
                }
            }
            else
            {
                // Sort by target Y center, then edge index for stability.
                var sorted = edgeList
                    .OrderBy(ei => augY[augEdges[ei].Target] + (augNodes[augEdges[ei].Target].Height / 2.0))
                    .ThenBy(ei => ei)
                    .ToList();
                DistributePorts(sorted, augY[ni], nodes[ni].Height, augPortYSrc);
            }
        }

        // Distribute incoming (target-side) ports on each real node's left face.
        var inByNode = new Dictionary<int, List<int>>();
        for (var ei = 0; ei < numAugEdges; ei++)
        {
            var tgt = augEdges[ei].Target;
            if (!inByNode.TryGetValue(tgt, out var list))
            {
                list = [];
                inByNode[tgt] = list;
            }

            list.Add(ei);
        }

        foreach (var (ni, edgeList) in inByNode)
        {
            if (augNodes[ni].IsDummy)
            {
                foreach (var ei in edgeList)
                {
                    augPortYTgt[ei] = augY[ni];
                }
            }
            else
            {
                var sorted = edgeList
                    .OrderBy(ei => augY[augEdges[ei].Source] + (augNodes[augEdges[ei].Source].Height / 2.0))
                    .ThenBy(ei => ei)
                    .ToList();
                DistributePorts(sorted, augY[ni], nodes[ni < n ? ni : 0].Height, augPortYTgt);
            }
        }

        // Route each corridor using ELK's slot algorithm.
        var layerCount = columnX.Length;
        var augBendPoints = new List<Point2D>[numAugEdges];
        for (var ei = 0; ei < numAugEdges; ei++)
        {
            augBendPoints[ei] = [];
        }

        for (var l = 0; l + 1 < layerCount; l++)
        {
            // Collect sub-edges whose source is in layer l.
            var corridorEdges = augEdges
                .Select((ae, i) => (ae, i))
                .Where(x => augNodes[x.ae.Source].Layer == l)
                .ToList();

            if (corridorEdges.Count == 0)
            {
                continue;
            }

            // First slot starts one ConnectorClearance past the right edge of the source column.
            var startPos = columnX[l] + maxColWidth[l] + ConnectorClearance;

            // Build routing segments.
            var segments = corridorEdges
                .Select(x =>
                {
                    var srcY = augPortYSrc[x.i];
                    var tgtY = augPortYTgt[x.i];
                    return new Segment
                    {
                        AugEdgeIndex = x.i,
                        SourceY = srcY,
                        TargetY = tgtY,
                        Lo = Math.Min(srcY, tgtY),
                        Hi = Math.Max(srcY, tgtY),
                    };
                })
                .ToList();

            // Build crossing-based dependencies between segment pairs (ELK countCrossings).
            for (var i = 0; i < segments.Count - 1; i++)
            {
                for (var j = i + 1; j < segments.Count; j++)
                {
                    CreateDependency(segments[i], segments[j]);
                }
            }

            // Break cycles in the dependency graph by removing back edges.
            BreakSegmentCycles(segments);

            // Assign routing slots via topological BFS (ELK topologicalNumbering).
            TopologicalNumbering(segments);

            // Emit bend points: ELK WestToEastRoutingStrategy.calculateBendPoints.
            foreach (var seg in segments)
            {
                if (Math.Abs(seg.SourceY - seg.TargetY) < StraightTolerance)
                {
                    // Straight edge: no bend points, no slot consumed.
                    continue;
                }

                var segX = startPos + (seg.RoutingSlot * EdgeSpacing);
                augBendPoints[seg.AugEdgeIndex] =
                [
                    new Point2D(segX, seg.SourceY),
                    new Point2D(segX, seg.TargetY),
                ];
            }
        }

        // Assemble per-original-edge waypoints from sub-edge bend points (ELK LongEdgeJoiner).
        var subEdgesByOrig = new List<int>[numOrigEdges];
        for (var ei = 0; ei < numOrigEdges; ei++)
        {
            subEdgesByOrig[ei] = [];
        }

        for (var ei = 0; ei < numAugEdges; ei++)
        {
            subEdgesByOrig[augEdges[ei].OrigEdgeIndex].Add(ei);
        }

        // Sub-edges must be in source-to-target layer order for concatenation.
        for (var ei = 0; ei < numOrigEdges; ei++)
        {
            subEdgesByOrig[ei].Sort((a, b) =>
                augNodes[augEdges[a].Source].Layer.CompareTo(augNodes[augEdges[b].Source].Layer));
        }

        var result = new IReadOnlyList<Point2D>[numOrigEdges];
        for (var origIdx = 0; origIdx < numOrigEdges; origIdx++)
        {
            var subEdges = subEdgesByOrig[origIdx];
            if (subEdges.Count == 0)
            {
                result[origIdx] = [];
                continue;
            }

            var firstSubEdge = augEdges[subEdges[0]];
            var lastSubEdge = augEdges[subEdges[^1]];

            var srcNodeIdx = firstSubEdge.Source;
            var tgtNodeIdx = lastSubEdge.Target;

            var srcRight = augX[srcNodeIdx] + augNodes[srcNodeIdx].Width;
            var tgtLeft = augX[tgtNodeIdx];
            var srcPortY = augPortYSrc[subEdges[0]];
            var tgtPortY = augPortYTgt[subEdges[^1]];

            var wps = new List<Point2D>
            {
                new(srcRight, srcPortY),
            };

            foreach (var subEdgeIdx in subEdges)
            {
                wps.AddRange(augBendPoints[subEdgeIdx]);
            }

            wps.Add(new Point2D(tgtLeft, tgtPortY));
            result[origIdx] = wps;
        }

        return result;
    }

    // ── ELK OrthogonalRoutingGenerator: dependency creation ──────────────────

    /// <summary>
    /// Creates a directed dependency between two segments based on ELK's crossing-count heuristic
    /// (<c>countCrossings</c>): the segment whose left placement causes fewer crossings becomes the
    /// source (lower slot) of the dependency.
    /// </summary>
    /// <remarks>
    /// For 1:1 edges each segment has exactly one EAST port (SourceY, ELK incomingConnectionCoordinates)
    /// and one WEST port (TargetY, ELK outgoingConnectionCoordinates). The crossing count is computed
    /// per ELK's <c>countCrossings(posis, start, end)</c>: number of positions in posis that fall
    /// within [start, end].
    /// </remarks>
    private static void CreateDependency(Segment s1, Segment s2)
    {
        // crossings1: cost of placing s1 LEFT of s2.
        //   outgoing(s1)=[s1.TargetY] ∩ [s2.Lo, s2.Hi]  → does s1's exit cross s2's extent?
        //   incoming(s2)=[s2.SourceY] ∩ [s1.Lo, s1.Hi]  → does s2's entry fall inside s1's extent?
        var c1 = CountCrossings(s1.TargetY, s2.Lo, s2.Hi)
               + CountCrossings(s2.SourceY, s1.Lo, s1.Hi);

        // crossings2: cost of placing s2 LEFT of s1.
        //   outgoing(s2)=[s2.TargetY] ∩ [s1.Lo, s1.Hi]
        //   incoming(s1)=[s1.SourceY] ∩ [s2.Lo, s2.Hi]
        var c2 = CountCrossings(s2.TargetY, s1.Lo, s1.Hi)
               + CountCrossings(s1.SourceY, s2.Lo, s2.Hi);

        if (c1 < c2)
        {
            // s1 prefers to be left of s2.
            _ = new SegDep(s1, s2, c2 - c1);
        }
        else if (c2 < c1)
        {
            // s2 prefers to be left of s1.
            _ = new SegDep(s2, s1, c1 - c2);
        }
        else if (c1 > 0)
        {
            // Equal non-zero crossings: unavoidable conflict — pick s1 left (deterministic tie-break).
            _ = new SegDep(s1, s2, 0);
        }

        // c1 == c2 == 0: both orderings cross-free; no dependency needed.
    }

    /// <summary>
    /// Returns 1 if <paramref name="pos"/> falls within [<paramref name="lo"/>,
    /// <paramref name="hi"/>], 0 otherwise. Implements ELK's single-position
    /// <c>countCrossings</c>.
    /// </summary>
    private static int CountCrossings(double pos, double lo, double hi)
        => (pos >= lo && pos <= hi) ? 1 : 0;

    // ── ELK OrthogonalRoutingGenerator: cycle breaking ───────────────────────

    /// <summary>
    /// Detects and removes back edges in the dependency graph using DFS coloring, following
    /// ELK's <c>breakNonCriticalCycles</c>. Removing a back edge accepts one additional crossing
    /// in exchange for an acyclic ordering.
    /// </summary>
    private static void BreakSegmentCycles(List<Segment> segments)
    {
        // 0 = unvisited, 1 = on stack (gray), 2 = done (black).
        var color = new Dictionary<Segment, int>();

        void Dfs(Segment s)
        {
            color[s] = 1;

            // Snapshot the list before iterating to allow safe removal.
            foreach (var dep in s.Outgoing.ToList())
            {
                var t = dep.Target;
                if (!color.TryGetValue(t, out var c))
                {
                    color[t] = 0;
                    Dfs(t);
                }
                else if (c == 1)
                {
                    // Back edge detected: remove it to break the cycle.
                    dep.Remove();
                }
            }

            // S4143: standard DFS coloring across recursion boundary.
#pragma warning disable S4143
            color[s] = 2;
#pragma warning restore S4143
        }

        foreach (var s in segments.Where(s => !color.ContainsKey(s)))
        {
            color[s] = 0;
            Dfs(s);
        }
    }

    // ── ELK OrthogonalRoutingGenerator: topological numbering ────────────────

    /// <summary>
    /// Assigns routing slots to segments by topological BFS, implementing ELK's
    /// <c>topologicalNumbering</c>. Each segment's slot equals the maximum predecessor slot
    /// plus one (Kahn's algorithm on the dependency DAG).
    /// </summary>
    private static void TopologicalNumbering(List<Segment> segments)
    {
        // Reset weights from the current (post-cycle-breaking) dependency graph.
        foreach (var s in segments)
        {
            s.InWeight = s.Incoming.Count;
            s.RoutingSlot = 0;
        }

        var sources = segments.Where(s => s.InWeight == 0).ToList();

        while (sources.Count > 0)
        {
            var node = sources[0];
            sources.RemoveAt(0);

            foreach (var tgt in node.Outgoing.Select(dep => dep.Target))
            {
                tgt.RoutingSlot = Math.Max(tgt.RoutingSlot, node.RoutingSlot + 1);
                tgt.InWeight--;
                if (tgt.InWeight == 0)
                {
                    sources.Add(tgt);
                }
            }
        }

        // Any segments unreachable due to remaining cycles (should not occur after cycle-breaking)
        // keep slot 0 and will be routed at the first available position.
    }

    // ── Port distribution ─────────────────────────────────────────────────────

    /// <summary>
    /// Evenly distributes port Y positions along a node face, with
    /// <see cref="ConnectorClearance"/> inset from the top and bottom edges.
    /// </summary>
    private static void DistributePorts(
        IReadOnlyList<int> sortedEdgeIndices,
        double nodeTop,
        double nodeHeight,
        double[] portY)
    {
        var count = sortedEdgeIndices.Count;
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

            portY[sortedEdgeIndices[k]] = Math.Clamp(
                y,
                nodeTop + ConnectorClearance,
                nodeTop + nodeHeight - ConnectorClearance);
        }
    }
}
