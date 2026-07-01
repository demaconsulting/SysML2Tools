// <copyright file="OrthogonalRouter.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>
using static DemaConsulting.SysML2Tools.Layout.Engine.Layered.LayeredLayoutMetrics;

namespace DemaConsulting.SysML2Tools.Layout.Engine.Layered;

/// <summary>
/// Pipeline stage that routes every corridor using ELK's <c>OrthogonalRoutingGenerator</c> slot
/// algorithm, producing the orthogonal bend points for each augmented sub-edge.
/// </summary>
internal sealed class OrthogonalRouter : ILayoutStage
{
    /// <inheritdoc/>
    public void Apply(LayeredGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var augNodes = graph.AugNodes;
        var augEdges = graph.AugEdges;
        var columnX = graph.ColumnX;
        var maxColWidth = graph.MaxColWidth;
        var augPortYSrc = graph.AugPortYSrc;
        var augPortYTgt = graph.AugPortYTgt;
        var numAugEdges = augEdges.Count;

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

                // Reversed (back) edges are stored flipped, so the consumer draws the end marker on
                // the augmented-source face of the first sub-edge (whose source is the real node, not
                // a long-edge dummy). For that sub-edge the wrap-around corridor is the final straight
                // approach into the true target, and at the default slot it is only one
                // ConnectorClearance wide. Guarantee that approach is at least
                // graph.BackEdgeEntryApproach so the rounded corner never intrudes into the end
                // decoration. Math.Max only ever pushes the jog outward, and at the default
                // (BackEdgeEntryApproach == ConnectorClearance == startPos offset) it is a no-op, so
                // forward edges (and every other sub-edge) are byte-identical. It runs in abstract
                // RIGHT-equivalent coordinates so AxisTransform maps it to any requested direction.
                var aug = augEdges[seg.AugEdgeIndex];
                if (graph.AcyclicReversed[aug.OrigEdgeIndex] && !augNodes[aug.Source].IsDummy)
                {
                    segX = Math.Max(segX, columnX[l] + maxColWidth[l] + graph.BackEdgeEntryApproach);
                }

                augBendPoints[seg.AugEdgeIndex] =
                [
                    new Point2D(segX, seg.SourceY),
                    new Point2D(segX, seg.TargetY),
                ];
            }
        }

        graph.AugBendPoints = augBendPoints;
    }

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

        /// <summary>Remaining in-degree during topological BFS; initialized from Incoming.Count.</summary>
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
}
