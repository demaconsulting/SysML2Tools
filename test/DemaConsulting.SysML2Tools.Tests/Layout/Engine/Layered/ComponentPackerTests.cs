// <copyright file="ComponentPackerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout;
using DemaConsulting.SysML2Tools.Layout.Engine;
using DemaConsulting.SysML2Tools.Layout.Engine.Layered;

namespace DemaConsulting.SysML2Tools.Tests.Layout.Engine.Layered;

/// <summary>
///     Tests for <see cref="ComponentPacker"/> covering connected-component detection, non-overlapping
///     packing of disconnected components, the single-component pass-through, the empty/null guards,
///     deterministic ordering, and translation of edge waypoints with their owning component.
/// </summary>
public sealed class ComponentPackerTests
{
    /// <summary>Logical pixel size used for every test node.</summary>
    private const double NodeWidth = 60.0;

    /// <summary>Logical pixel height used for every test node.</summary>
    private const double NodeHeight = 40.0;

    /// <summary>
    ///     A connected core (chain 0-&gt;1-&gt;2) plus a disconnected node keeps the core as one
    ///     component: the core's internal arrangement matches laying the core out on its own.
    /// </summary>
    [Fact]
    public void ComponentPacker_Apply_ConnectedCore_StaysOneComponent()
    {
        // Arrange: a connected chain {0,1,2} and an isolated node 3.
        var nodes = new List<LayerNode> { new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight) };
        var edges = new List<LayerEdge> { new(0, 1), new(1, 2) };
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);

        // Act: pack the disconnected graph.
        ComponentPacker.WithDefaultStages().Apply(graph);

        // Reference: lay the connected core out on its own and normalize to its bounding box.
        var coreNodes = new List<LayerNode> { new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight) };
        var coreEdges = new List<LayerEdge> { new(0, 1), new(1, 2) };
        var core = RunDefaultStages(coreNodes, coreEdges);
        var coreMinX = new[] { core.AugX[0], core.AugX[1], core.AugX[2] }.Min();
        var coreMinY = new[] { core.AugY[0], core.AugY[1], core.AugY[2] }.Min();

        // The packed core is component 0 (offset 0,0), so its coordinates are already normalized.
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(core.AugX[i] - coreMinX, graph.AugX[i], 6);
            Assert.Equal(core.AugY[i] - coreMinY, graph.AugY[i], 6);
        }
    }

    /// <summary>
    ///     Three disconnected nodes become three components placed in distinct, non-overlapping boxes.
    /// </summary>
    [Fact]
    public void ComponentPacker_Apply_DisconnectedSingletons_PackSeparately()
    {
        // Arrange: three isolated nodes, no edges.
        var nodes = new List<LayerNode> { new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight) };
        var graph = new LayeredGraph(nodes, [], LayoutDirection.Right);

        // Act.
        ComponentPacker.WithDefaultStages().Apply(graph);

        // Assert: no two node boxes overlap.
        for (var a = 0; a < nodes.Count; a++)
        {
            for (var b = a + 1; b < nodes.Count; b++)
            {
                Assert.False(
                    BoxesOverlap(graph, a, b),
                    $"nodes {a} and {b} should not overlap");
            }
        }
    }

    /// <summary>
    ///     A single connected component is a transparent pass-through: the packed output equals the
    ///     output of running the same inner stages directly on the graph.
    /// </summary>
    [Fact]
    public void ComponentPacker_Apply_SingleComponent_EqualsDefaultPipeline()
    {
        // Arrange: a diamond (one connected component).
        var nodes = new List<LayerNode> { new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight) };
        var edges = new List<LayerEdge> { new(0, 1), new(0, 2), new(1, 3), new(2, 3) };

        var packed = new LayeredGraph(CloneNodes(nodes), CloneEdges(edges), LayoutDirection.Right);
        var reference = RunDefaultStages(CloneNodes(nodes), CloneEdges(edges));

        // Act.
        ComponentPacker.WithDefaultStages().Apply(packed);

        // Assert: node coordinates and routed waypoints match the default pipeline exactly.
        for (var i = 0; i < nodes.Count; i++)
        {
            Assert.Equal(reference.AugX[i], packed.AugX[i]);
            Assert.Equal(reference.AugY[i], packed.AugY[i]);
        }

        Assert.Equal(reference.Waypoints.Count, packed.Waypoints.Count);
        for (var k = 0; k < reference.Waypoints.Count; k++)
        {
            Assert.Equal(reference.Waypoints[k].Count, packed.Waypoints[k].Count);
            for (var p = 0; p < reference.Waypoints[k].Count; p++)
            {
                Assert.Equal(reference.Waypoints[k][p].X, packed.Waypoints[k][p].X);
                Assert.Equal(reference.Waypoints[k][p].Y, packed.Waypoints[k][p].Y);
            }
        }
    }

    /// <summary>An empty graph is laid out as a no-op without throwing.</summary>
    [Fact]
    public void ComponentPacker_Apply_EmptyGraph_IsNoOp()
    {
        // Arrange: a graph with no nodes or edges.
        var graph = new LayeredGraph([], [], LayoutDirection.Right);

        // Act.
        ComponentPacker.WithDefaultStages().Apply(graph);

        // Assert: nothing was produced.
        Assert.Empty(graph.AugX);
        Assert.Empty(graph.AugY);
        Assert.Empty(graph.Waypoints);
    }

    /// <summary>A null graph throws <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void ComponentPacker_Apply_NullGraph_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ComponentPacker.WithDefaultStages().Apply(null!));
    }

    /// <summary>
    ///     Repeated layouts of the same disconnected graph produce identical coordinates, confirming a
    ///     deterministic component order.
    /// </summary>
    [Fact]
    public void ComponentPacker_Apply_ComponentOrder_IsDeterministic()
    {
        // Arrange: two components — an edge {0,1} and an isolated node 2.
        static LayeredGraph Build()
        {
            var nodes = new List<LayerNode> { new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight) };
            var edges = new List<LayerEdge> { new(0, 1) };
            return new LayeredGraph(nodes, edges, LayoutDirection.Right);
        }

        var first = Build();
        var second = Build();

        // Act.
        ComponentPacker.WithDefaultStages().Apply(first);
        ComponentPacker.WithDefaultStages().Apply(second);

        // Assert: identical placements.
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(first.AugX[i], second.AugX[i]);
            Assert.Equal(first.AugY[i], second.AugY[i]);
        }
    }

    /// <summary>
    ///     Each edge's routed waypoints are translated with its component so the endpoints stay on the
    ///     boxes of the offset component, not at the local origin.
    /// </summary>
    [Fact]
    public void ComponentPacker_Apply_Waypoints_TranslatedWithComponent()
    {
        // Arrange: two components, each with one internal edge: {0->1} and {2->3}.
        var nodes = new List<LayerNode> { new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight) };
        var edges = new List<LayerEdge> { new(0, 1), new(2, 3) };
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);

        // Act.
        ComponentPacker.WithDefaultStages().Apply(graph);

        // Assert: every edge's first/last waypoint lies on its source/target box (within tolerance).
        const double eps = 3.0;
        AssertWaypointOnBox(graph, edge: 0, point: 0, node: 0, eps);
        AssertWaypointOnBox(graph, edge: 0, point: -1, node: 1, eps);
        AssertWaypointOnBox(graph, edge: 1, point: 0, node: 2, eps);
        AssertWaypointOnBox(graph, edge: 1, point: -1, node: 3, eps);
    }

    /// <summary>Runs the default ELK-layered stage sequence directly on a fresh graph.</summary>
    /// <param name="nodes">Input nodes.</param>
    /// <param name="edges">Input edges.</param>
    /// <returns>The graph after the full default stage sequence.</returns>
    private static LayeredGraph RunDefaultStages(List<LayerNode> nodes, List<LayerEdge> edges)
    {
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);
        new CycleBreaker().Apply(graph);
        new LayerAssigner().Apply(graph);
        new LongEdgeSplitter().Apply(graph);
        new CrossingMinimizer().Apply(graph);
        new BrandesKopfPlacer().Apply(graph);
        new PortDistributor().Apply(graph);
        new OrthogonalRouter().Apply(graph);
        new LongEdgeJoiner().Apply(graph);
        new AxisTransform().Apply(graph);
        return graph;
    }

    /// <summary>Returns a fresh copy of a node list.</summary>
    /// <param name="nodes">Nodes to copy.</param>
    /// <returns>A new list with the same node values.</returns>
    private static List<LayerNode> CloneNodes(List<LayerNode> nodes) => [.. nodes];

    /// <summary>Returns a fresh copy of an edge list.</summary>
    /// <param name="edges">Edges to copy.</param>
    /// <returns>A new list with the same edge values.</returns>
    private static List<LayerEdge> CloneEdges(List<LayerEdge> edges) => [.. edges];

    /// <summary>Determines whether the boxes of two real nodes overlap.</summary>
    /// <param name="graph">The laid-out graph.</param>
    /// <param name="a">First node index.</param>
    /// <param name="b">Second node index.</param>
    /// <returns><see langword="true"/> when the two boxes intersect.</returns>
    private static bool BoxesOverlap(LayeredGraph graph, int a, int b)
    {
        var ax = graph.AugX[a];
        var ay = graph.AugY[a];
        var bx = graph.AugX[b];
        var by = graph.AugY[b];
        var overlapX = ax < bx + graph.Nodes[b].Width && bx < ax + graph.Nodes[a].Width;
        var overlapY = ay < by + graph.Nodes[b].Height && by < ay + graph.Nodes[a].Height;
        return overlapX && overlapY;
    }

    /// <summary>Asserts that a given waypoint of an edge lies on a node's box, within a tolerance.</summary>
    /// <param name="graph">The laid-out graph.</param>
    /// <param name="edge">Edge index (original edge order).</param>
    /// <param name="point">Waypoint index; a negative value counts from the end.</param>
    /// <param name="node">The node whose box the waypoint should lie on.</param>
    /// <param name="eps">Tolerance in logical pixels.</param>
    private static void AssertWaypointOnBox(LayeredGraph graph, int edge, int point, int node, double eps)
    {
        var waypoints = graph.Waypoints[edge];
        var p = point < 0 ? waypoints[waypoints.Count + point] : waypoints[point];
        var x = graph.AugX[node];
        var y = graph.AugY[node];
        Assert.InRange(p.X, x - eps, x + graph.Nodes[node].Width + eps);
        Assert.InRange(p.Y, y - eps, y + graph.Nodes[node].Height + eps);
    }
}
