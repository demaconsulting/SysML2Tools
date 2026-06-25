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
public sealed record LayoutBadge(
    double CentreX,
    double CentreY,
    double Size,
    BadgeShape Shape,
    string? Label) : LayoutNode;
