// <copyright file="LayoutLine.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout;

/// <summary>
/// An immutable 2-D point with absolute coordinates.
/// </summary>
/// <param name="X">Absolute X coordinate in logical pixels.</param>
/// <param name="Y">Absolute Y coordinate in logical pixels.</param>
public sealed record Point2D(double X, double Y);

/// <summary>
/// Style of an end marker (arrowhead, diamond, circle, or bar) at a line end.
/// </summary>
public enum EndMarkerStyle
{
    /// <summary>No end marker.</summary>
    None,

    /// <summary>Open chevron: a two-stroke V with no closing edge.</summary>
    OpenChevron,

    /// <summary>Hollow (unfilled) closed triangle.</summary>
    HollowTriangle,

    /// <summary>Hollow closed triangle with a perpendicular crossbar (for redefinition).</summary>
    HollowTriangleCrossbar,

    /// <summary>Filled (solid) triangle arrow.</summary>
    FilledArrow,

    /// <summary>Hollow (open) diamond.</summary>
    HollowDiamond,

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
/// <param name="Waypoints">Ordered absolute waypoints; the renderer draws straight segments between consecutive points.</param>
/// <param name="SourceEnd">End-marker style at the source (start) end of the line.</param>
/// <param name="TargetEnd">End-marker style at the target (end) end of the line.</param>
/// <param name="LineStyle">Stroke style (solid, dashed, dotted) for the line.</param>
/// <param name="MidpointLabel">Optional text label placed at the midpoint of the line; <see langword="null"/> for unlabelled lines.</param>
public sealed record LayoutLine(
    IReadOnlyList<Point2D> Waypoints,
    EndMarkerStyle SourceEnd,
    EndMarkerStyle TargetEnd,
    LineStyle LineStyle,
    string? MidpointLabel) : LayoutNode;
