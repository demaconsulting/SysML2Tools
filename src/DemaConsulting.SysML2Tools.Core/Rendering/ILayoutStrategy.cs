// <copyright file="ILayoutStrategy.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout;
using DemaConsulting.SysML2Tools.Semantic;

namespace DemaConsulting.SysML2Tools.Rendering;

/// <summary>
/// Context passed to an <see cref="ILayoutStrategy"/> describing the view to lay out.
/// </summary>
/// <param name="ViewName">Name of the view being rendered.</param>
/// <param name="Workspace">The SysML workspace containing model elements.</param>
public sealed record ViewContext(
    string ViewName,
    SysmlWorkspace Workspace);

/// <summary>
/// Computes a <see cref="LayoutTree"/> from a <see cref="ViewContext"/>.
/// Implementations are responsible for node placement and line routing (including A* path-finding).
/// </summary>
public interface ILayoutStrategy
{
    /// <summary>
    /// Builds the complete layout for the given view.
    /// </summary>
    /// <param name="context">View context identifying the workspace and view name to lay out.</param>
    /// <param name="options">Render options supplying scale and depth limit hints.</param>
    /// <returns>A fully resolved <see cref="LayoutTree"/> with all positions and waypoints computed.</returns>
    LayoutTree BuildLayout(ViewContext context, RenderOptions options);
}
