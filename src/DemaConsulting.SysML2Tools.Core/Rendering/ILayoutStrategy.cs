// <copyright file="ILayoutStrategy.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;

namespace DemaConsulting.SysML2Tools.Rendering;

/// <summary>
/// Context passed to an <see cref="ILayoutStrategy"/> describing the view to lay out.
/// </summary>
/// <param name="ViewName">
/// Name of the view being rendered — the display name resolved by
/// <see cref="DiagramRenderer.RenderWorkspace"/> (the view's simple <c>Name</c> when set,
/// otherwise its fully-qualified name).
/// </param>
/// <param name="Workspace">
/// The loaded <see cref="SysmlWorkspace"/> containing all model elements, so the strategy can
/// resolve the view's target element(s) and traverse related declarations while building the
/// layout tree.
/// </param>
/// <param name="ViewNode">
/// The resolved <see cref="SysmlViewNode"/> the view was declared from, carrying its resolved
/// <c>Expose</c> edges and raw <c>FilterExpressionText</c> so a strategy can scope
/// its diagram accordingly. Nullable to preserve the <c>--auto</c> synthetic-view path, whose
/// synthesized <see cref="SysmlViewNode"/> carries no render/expose/filter data; defaults to
/// <see langword="null"/> so existing two-argument construction call sites remain unchanged.
/// </param>
public sealed record ViewContext(
    string ViewName,
    SysmlWorkspace Workspace,
    SysmlViewNode? ViewNode = null);

/// <summary>
/// Computes a <see cref="LayoutTree"/> from a <see cref="ViewContext"/>.
/// Implementations are responsible for node placement and line routing (including A* path-finding).
/// </summary>
/// <remarks>
/// Implement this interface to add support for a new diagram kind (e.g. a new SysML view
/// keyword), or to swap in an alternative layout algorithm for an existing kind. Strategy
/// selection for a given view is performed by <c>Internal.DiagramTypeRouter</c>, which
/// <see cref="DiagramRenderer.RenderWorkspace"/> consults for each view declaration in the
/// workspace.
/// </remarks>
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
