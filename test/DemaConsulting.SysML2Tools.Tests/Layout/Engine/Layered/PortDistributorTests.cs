// <copyright file="PortDistributorTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Engine;
using DemaConsulting.SysML2Tools.Layout.Engine.Layered;

namespace DemaConsulting.SysML2Tools.Tests.Layout.Engine.Layered;

/// <summary>
///     Tests for <see cref="PortDistributor"/> covering that ports lie within node faces and that a
///     source and target port Y is recorded for every augmented sub-edge.
/// </summary>
public sealed class PortDistributorTests
{
    /// <summary>
    ///     A single edge's source and target ports lie within the faces of their respective nodes.
    /// </summary>
    [Fact]
    public void PortDistributor_Apply_SingleEdge_PortsLieWithinNodeFaces()
    {
        // Arrange / Act: distribute ports for a single 0->1 edge.
        var nodes = new List<LayerNode> { new(60, 40), new(60, 40) };
        var graph = BuildPortedGraph(nodes, new List<LayerEdge> { new(0, 1) });

        var src = graph.AugEdges[0].Source;
        var tgt = graph.AugEdges[0].Target;

        // Assert: the source port is on the source node's face and the target port on the target's.
        Assert.InRange(graph.AugPortYSrc[0], graph.AugY[src], graph.AugY[src] + nodes[src].Height);
        Assert.InRange(graph.AugPortYTgt[0], graph.AugY[tgt], graph.AugY[tgt] + nodes[tgt].Height);
    }

    /// <summary>
    ///     A diamond graph yields one source port Y and one target port Y per augmented sub-edge,
    ///     each a finite value.
    /// </summary>
    [Fact]
    public void PortDistributor_Apply_AssignsPortYForEverySubEdge()
    {
        // Arrange / Act: distribute ports for a four-node diamond.
        var nodes = new List<LayerNode> { new(60, 40), new(60, 40), new(60, 40), new(60, 40) };
        var graph = BuildPortedGraph(nodes, new List<LayerEdge> { new(0, 1), new(0, 2), new(1, 3), new(2, 3) });

        // Assert: one source and one target port per sub-edge.
        Assert.Equal(graph.AugEdges.Count, graph.AugPortYSrc.Length);
        Assert.Equal(graph.AugEdges.Count, graph.AugPortYTgt.Length);
        Assert.All(graph.AugPortYSrc, y => Assert.True(double.IsFinite(y)));
        Assert.All(graph.AugPortYTgt, y => Assert.True(double.IsFinite(y)));
    }

    /// <summary>Runs the stages up to and including port distribution and returns the graph.</summary>
    /// <param name="nodes">Input nodes.</param>
    /// <param name="edges">Input edges.</param>
    /// <returns>The graph after the port-distribution stage.</returns>
    private static LayeredGraph BuildPortedGraph(List<LayerNode> nodes, List<LayerEdge> edges)
    {
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);
        new CycleBreaker().Apply(graph);
        new LayerAssigner().Apply(graph);
        new LongEdgeSplitter().Apply(graph);
        new CrossingMinimizer().Apply(graph);
        new BrandesKopfPlacer().Apply(graph);
        new PortDistributor().Apply(graph);
        return graph;
    }
}
