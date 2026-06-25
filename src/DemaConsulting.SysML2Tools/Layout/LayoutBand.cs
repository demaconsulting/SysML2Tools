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
public sealed record LayoutBand(
    double X,
    double Y,
    double Width,
    double Height,
    BandOrientation Orientation,
    string? Label,
    IReadOnlyList<LayoutNode> Children) : LayoutNode;
