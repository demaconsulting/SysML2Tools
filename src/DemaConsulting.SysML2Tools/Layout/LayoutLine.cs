// <copyright file="LayoutLine.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout;

/// <summary>
/// An immutable 2-D point with absolute coordinates.
/// </summary>
public sealed record Point2D(double X, double Y);

/// <summary>
/// Style of an arrowhead at a line end.
/// </summary>
public enum ArrowheadStyle
{
    /// <summary>No arrowhead.</summary>
    None,

    /// <summary>Open (hollow) arrowhead.</summary>
    Open,

    /// <summary>Filled arrowhead.</summary>
    Filled,

    /// <summary>Open diamond.</summary>
    Diamond,

    /// <summary>Filled diamond.</summary>
    FilledDiamond,

    /// <summary>Circle.</summary>
    Circle,

    /// <summary>Bar (perpendicular line).</summary>
    Bar,
}

/// <summary>
/// Stroke style of a line.
/// </summary>
public enum LineStyle
{
    /// <summary>Solid line.</summary>
    Solid,

    /// <summary>Dashed line.</summary>
    Dashed,

    /// <summary>Dotted line.</summary>
    Dotted,
}

/// <summary>
/// A pre-routed orthogonal line connecting two nodes. Corner rounding is controlled by <see cref="DemaConsulting.SysML2Tools.Rendering.Theme.LineCornerRadius"/>.
/// </summary>
public sealed record LayoutLine(
    IReadOnlyList<Point2D> Waypoints,
    ArrowheadStyle SourceArrowhead,
    ArrowheadStyle TargetArrowhead,
    LineStyle LineStyle,
    string? MidpointLabel) : LayoutNode;
