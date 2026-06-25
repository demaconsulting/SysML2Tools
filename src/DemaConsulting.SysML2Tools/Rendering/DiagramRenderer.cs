// <copyright file="DiagramRenderer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Semantic;

namespace DemaConsulting.SysML2Tools.Rendering;

/// <summary>
/// High-level orchestrator: iterates over all views in a <see cref="SysmlWorkspace"/>,
/// builds a <see cref="Layout.LayoutTree"/> via an <see cref="ILayoutStrategy"/>,
/// and renders each view via an <see cref="IRenderer"/>.
/// </summary>
public sealed class DiagramRenderer
{
    /// <summary>
    /// Renders every view in the workspace and returns a collection of output streams.
    /// </summary>
    /// <param name="workspace">The SysML workspace to render.</param>
    /// <param name="renderer">The renderer to use for output generation.</param>
    /// <param name="options">Render options including theme and scale.</param>
    /// <returns>A list of render outputs, one per view.</returns>
    // S2325: instance method — Phase 4 will inject ILayoutStrategy via constructor making this non-static
#pragma warning disable S2325
    public IReadOnlyList<RenderOutput> RenderWorkspace(
        SysmlWorkspace workspace,
        IRenderer renderer,
        RenderOptions options) =>
        throw new NotImplementedException("DiagramRenderer is not yet implemented (Phase 4).");
#pragma warning restore S2325
}
