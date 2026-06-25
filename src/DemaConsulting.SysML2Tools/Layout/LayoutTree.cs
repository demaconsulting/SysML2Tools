// <copyright file="LayoutTree.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout;

/// <summary>
/// Complete layout for one rendered view. All coordinates are absolute (origin = top-left).
/// </summary>
public sealed record LayoutTree(
    double Width,
    double Height,
    IReadOnlyList<LayoutNode> Nodes);
