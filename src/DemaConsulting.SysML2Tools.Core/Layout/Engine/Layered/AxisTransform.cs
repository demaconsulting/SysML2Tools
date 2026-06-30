// <copyright file="AxisTransform.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout.Engine.Layered;

/// <summary>
/// Pipeline stage that maps the abstract along/cross coordinates computed by the earlier stages
/// onto screen coordinates for the requested <see cref="LayoutDirection"/>.
/// </summary>
/// <remarks>
/// <see cref="LayoutDirection.Right"/> is the abstract identity direction (along = +X,
/// cross = +Y); the earlier stages already emit those screen coordinates, so this stage is a
/// no-op for RIGHT and the pipeline output is unchanged. Non-RIGHT directions are reserved
/// scaffolding for a later phase and are not yet supported.
/// </remarks>
internal sealed class AxisTransform : ILayoutStage
{
    /// <inheritdoc/>
    public void Apply(LayeredGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        if (graph.Direction != LayoutDirection.Right)
        {
            throw new NotSupportedException(
                $"Layout direction '{graph.Direction}' is not yet supported by {nameof(AxisTransform)}.");
        }

        // RIGHT identity: coordinates are already in screen space; nothing to transform.
    }
}
