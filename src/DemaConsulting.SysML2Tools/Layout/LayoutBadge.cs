// <copyright file="LayoutBadge.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout;

/// <summary>
/// Shape of a badge icon.
/// </summary>
public enum BadgeShape
{
    /// <summary>Solid filled circle.</summary>
    FilledCircle,

    /// <summary>Circle with concentric inner circle (bullseye).</summary>
    Bullseye,

    /// <summary>Diamond (rotated square).</summary>
    Diamond,

    /// <summary>Short horizontal bar.</summary>
    HorizontalBar,

    /// <summary>Short vertical bar.</summary>
    VerticalBar,
}

/// <summary>
/// A small icon badge at an absolute centre position.
/// </summary>
/// <param name="CentreX">Absolute X coordinate of the badge centre in logical pixels.</param>
/// <param name="CentreY">Absolute Y coordinate of the badge centre in logical pixels.</param>
/// <param name="Size">Diameter of the badge bounding circle in logical pixels.</param>
/// <param name="Shape">Visual shape of the badge icon.</param>
/// <param name="Label">Optional text label displayed beside the badge; <see langword="null"/> for icon-only badges.</param>
public sealed record LayoutBadge(
    double CentreX,
    double CentreY,
    double Size,
    BadgeShape Shape,
    string? Label) : LayoutNode;
