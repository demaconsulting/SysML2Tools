// <copyright file="LayeredLayoutPipelineTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Engine;
using DemaConsulting.SysML2Tools.Layout.Engine.Layered;

namespace DemaConsulting.SysML2Tools.Tests.Layout.Engine.Layered;

/// <summary>
///     Tests for <see cref="LayeredLayoutPipeline"/> covering default-stage assembly and
///     execution, rejection of recursive hierarchy handling, and null-argument validation.
/// </summary>
public sealed class LayeredLayoutPipelineTests
{
    /// <summary>
    ///     The default pipeline runs over a three-node chain without throwing and produces one
    ///     connector waypoint list per input edge.
    /// </summary>
    [Fact]
    public void LayeredLayoutPipeline_RunDefaultStages_ChainGraph_PopulatesWaypointsWithoutThrowing()
    {
        // Arrange: a 0->1->2 chain and a default pipeline.
        var nodes = new List<LayerNode> { new(60, 40), new(60, 40), new(60, 40) };
        var edges = new List<LayerEdge> { new(0, 1), new(1, 2) };
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);
        var pipeline = LayeredLayoutPipeline.Builder()
            .Direction(LayoutDirection.Right)
            .Hierarchy(HierarchyHandling.Flat)
            .AddDefaultStages()
            .Build();

        // Act: run the full pipeline.
        pipeline.Run(graph);

        // Assert: one waypoint list was produced per edge.
        Assert.Equal(edges.Count, graph.Waypoints.Count);
    }

    /// <summary>
    ///     Building a pipeline with recursive hierarchy handling throws
    ///     <see cref="NotSupportedException"/> because the mode is not yet implemented.
    /// </summary>
    [Fact]
    public void LayeredLayoutPipeline_Build_RecursiveHierarchy_ThrowsNotSupportedException()
    {
        // Arrange: a builder configured for recursive hierarchy handling.
        var builder = LayeredLayoutPipeline.Builder()
            .Hierarchy(HierarchyHandling.Recursive)
            .AddDefaultStages();

        // Act / Assert: building rejects the unsupported mode.
        Assert.Throws<NotSupportedException>(() => builder.Build());
    }

    /// <summary>
    ///     Adding a null stage to the builder throws <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void LayeredLayoutPipeline_AddStage_NullStage_ThrowsArgumentNullException()
    {
        // Arrange: a fresh builder.
        var builder = LayeredLayoutPipeline.Builder();

        // Act / Assert: a null stage is rejected.
        Assert.Throws<ArgumentNullException>(() => builder.AddStage(null!));
    }

    /// <summary>
    ///     Running a pipeline with a null graph throws <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void LayeredLayoutPipeline_Run_NullGraph_ThrowsArgumentNullException()
    {
        // Arrange: a built default pipeline.
        var pipeline = LayeredLayoutPipeline.Builder().AddDefaultStages().Build();

        // Act / Assert: running with a null graph is rejected.
        Assert.Throws<ArgumentNullException>(() => pipeline.Run(null!));
    }
}
