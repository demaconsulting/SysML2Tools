// <copyright file="LayoutBand.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout;

/// <summary>
/// Orientation of a swim-lane band.
/// </summary>
public enum BandOrientation
{
    /// <summary>Band runs horizontally.</summary>
    Horizontal,

    /// <summary>Band runs vertically.</summary>
    Vertical,
}

/// <summary>
/// A swim-lane band with optional label and nested children.
/// </summary>
/// <param name="X">Absolute X coordinate of the band left edge in logical pixels.</param>
/// <param name="Y">Absolute Y coordinate of the band top edge in logical pixels.</param>
/// <param name="Width">Width of the band in logical pixels.</param>
/// <param name="Height">Height of the band in logical pixels.</param>
/// <param name="Orientation">Whether the band runs horizontally or vertically.</param>
/// <param name="Label">Optional header text displayed at the band edge; <see langword="null"/> for unlabelled bands.</param>
/// <param name="Children">Layout nodes nested inside this band.</param>
public sealed record LayoutBand(
    double X,
    double Y,
    double Width,
    double Height,
    BandOrientation Orientation,
    string? Label,
    IReadOnlyList<LayoutNode> Children) : LayoutNode;
