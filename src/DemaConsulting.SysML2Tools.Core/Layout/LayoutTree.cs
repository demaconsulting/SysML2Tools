// <copyright file="LayoutTree.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout;

/// <summary>
/// Complete layout for one rendered view. All coordinates are absolute (origin = top-left).
/// </summary>
/// <param name="Width">Canvas width in logical pixels.</param>
/// <param name="Height">Canvas height in logical pixels.</param>
/// <param name="Nodes">Flat list of top-level layout nodes to render.</param>
public sealed record LayoutTree(
    double Width,
    double Height,
    IReadOnlyList<LayoutNode> Nodes)
{
    /// <summary>
    /// Gets non-fatal layout-quality warnings produced while building this view (e.g. connectors
    /// that could not be routed without crossing a box). Empty when the layout is clean.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
