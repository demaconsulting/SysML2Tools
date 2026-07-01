// <copyright file="ILayoutStage.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout.Engine.Layered;

/// <summary>
/// A single composable phase of the layered layout pipeline.
/// </summary>
/// <remarks>
/// Each stage reads the state it needs from the shared <see cref="LayeredGraph"/> and writes its
/// results back onto the same instance, so stages can be ordered, replaced, and unit-tested in
/// isolation. Stages are stateless and may be shared across pipelines.
/// </remarks>
internal interface ILayoutStage
{
    /// <summary>
    /// Applies this stage to the supplied graph, mutating it in place.
    /// </summary>
    /// <param name="graph">The shared layered-layout state to read from and write to.</param>
    void Apply(LayeredGraph graph);
}
