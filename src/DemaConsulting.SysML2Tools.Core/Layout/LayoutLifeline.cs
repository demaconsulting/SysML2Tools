// <copyright file="LayoutLifeline.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout;

/// <summary>
/// A sequence-diagram lifeline: a vertical dashed line with a header box.
/// </summary>
/// <param name="CentreX">Absolute X coordinate of the lifeline centre in logical pixels.</param>
/// <param name="TopY">Absolute Y coordinate of the top of the lifeline in logical pixels.</param>
/// <param name="BottomY">Absolute Y coordinate of the bottom of the lifeline in logical pixels.</param>
/// <param name="Label">Text label displayed in the header box.</param>
/// <param name="HeaderWidth">Width of the header box in logical pixels.</param>
/// <param name="HeaderHeight">Height of the header box in logical pixels.</param>
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
/// <param name="CentreX">Absolute X coordinate of the activation bar centre in logical pixels.</param>
/// <param name="TopY">Absolute Y coordinate of the top of the activation bar in logical pixels.</param>
/// <param name="BottomY">Absolute Y coordinate of the bottom of the activation bar in logical pixels.</param>
public sealed record LayoutActivation(
    double CentreX,
    double TopY,
    double BottomY) : LayoutNode;
