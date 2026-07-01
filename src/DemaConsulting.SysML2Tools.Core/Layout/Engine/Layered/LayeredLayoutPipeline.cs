// <copyright file="LayeredLayoutPipeline.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout.Engine.Layered;

/// <summary>
/// An ordered sequence of <see cref="ILayoutStage"/> instances that, when run, transforms a
/// <see cref="LayeredGraph"/> from raw nodes and edges into a fully placed and routed layout.
/// </summary>
/// <remarks>
/// Pipelines are assembled with the fluent <see cref="PipelineBuilder"/> returned by
/// <see cref="Builder"/>. The default stage sequence reproduces ELK's layered algorithm in the
/// order used by the original interconnection engine.
/// </remarks>
internal sealed class LayeredLayoutPipeline
{
    private readonly IReadOnlyList<ILayoutStage> _stages;

    private LayeredLayoutPipeline(
        LayoutDirection direction,
        HierarchyHandling hierarchy,
        IReadOnlyList<ILayoutStage> stages)
    {
        Direction = direction;
        Hierarchy = hierarchy;
        _stages = stages;
    }

    /// <summary>Gets the layout flow direction this pipeline was built for.</summary>
    public LayoutDirection Direction { get; }

    /// <summary>Gets the hierarchy-handling mode this pipeline was built for.</summary>
    public HierarchyHandling Hierarchy { get; }

    /// <summary>Creates a new <see cref="PipelineBuilder"/>.</summary>
    /// <returns>A fresh builder with default direction and hierarchy.</returns>
    public static PipelineBuilder Builder() => new();

    /// <summary>Runs every stage, in order, against the supplied graph.</summary>
    /// <param name="graph">The graph to lay out; mutated in place.</param>
    public void Run(LayeredGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        // Normalize the input node axes for the requested direction before any stage runs, so the
        // direction-agnostic stages space layers by the correct extent (a no-op for RIGHT/LEFT).
        AxisTransform.NormalizeInputAxes(graph);

        foreach (var stage in _stages)
        {
            stage.Apply(graph);
        }
    }

    /// <summary>
    /// Fluent builder that assembles a <see cref="LayeredLayoutPipeline"/> from an ordered list
    /// of stages plus a direction and hierarchy-handling selection.
    /// </summary>
    internal sealed class PipelineBuilder
    {
        private readonly List<ILayoutStage> _stages = [];
        private LayoutDirection _direction = LayoutDirection.Right;
        private HierarchyHandling _hierarchy = HierarchyHandling.Flat;

        /// <summary>Sets the layout flow direction.</summary>
        /// <param name="direction">The desired direction.</param>
        /// <returns>This builder, for chaining.</returns>
        public PipelineBuilder Direction(LayoutDirection direction)
        {
            _direction = direction;
            return this;
        }

        /// <summary>Sets the hierarchy-handling mode.</summary>
        /// <param name="hierarchy">The desired hierarchy handling.</param>
        /// <returns>This builder, for chaining.</returns>
        public PipelineBuilder Hierarchy(HierarchyHandling hierarchy)
        {
            _hierarchy = hierarchy;
            return this;
        }

        /// <summary>Appends a single stage to the pipeline.</summary>
        /// <param name="stage">The stage to append.</param>
        /// <returns>This builder, for chaining.</returns>
        public PipelineBuilder AddStage(ILayoutStage stage)
        {
            ArgumentNullException.ThrowIfNull(stage);
            _stages.Add(stage);
            return this;
        }

        /// <summary>
        /// Appends the default ELK-layered stage sequence: cycle breaking, layer assignment,
        /// long-edge splitting, crossing minimization, Brandes-Kopf placement, port distribution,
        /// orthogonal routing, long-edge joining, and the final axis transform.
        /// </summary>
        /// <returns>This builder, for chaining.</returns>
        public PipelineBuilder AddDefaultStages()
        {
            _stages.Add(new CycleBreaker());
            _stages.Add(new LayerAssigner());
            _stages.Add(new LongEdgeSplitter());
            _stages.Add(new CrossingMinimizer());
            _stages.Add(new BrandesKopfPlacer());
            _stages.Add(new PortDistributor());
            _stages.Add(new OrthogonalRouter());
            _stages.Add(new LongEdgeJoiner());
            _stages.Add(new AxisTransform());
            return this;
        }

        /// <summary>Builds the configured pipeline.</summary>
        /// <returns>A new <see cref="LayeredLayoutPipeline"/>.</returns>
        public LayeredLayoutPipeline Build()
        {
            if (_hierarchy == HierarchyHandling.Recursive)
            {
                throw new NotSupportedException(
                    "Recursive hierarchy handling is not yet supported.");
            }

            return new LayeredLayoutPipeline(_direction, _hierarchy, _stages.ToArray());
        }
    }
}
