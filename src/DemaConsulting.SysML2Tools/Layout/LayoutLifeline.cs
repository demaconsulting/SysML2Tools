// <copyright file="LayoutLifeline.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout;

/// <summary>
/// A sequence-diagram lifeline: a vertical dashed line with a header box.
/// </summary>
public sealed record LayoutLifeline(
    double CentreX,
    double TopY,
    double BottomY,
    string Label,
    double HeaderWidth,
    double HeaderHeight) : LayoutNode;

/// <summary>
/// An activation bar on a lifeline: a narrow rectangle indicating when the lifeline is active.
/// </summary>
public sealed record LayoutActivation(
    double CentreX,
    double TopY,
    double BottomY) : LayoutNode;
