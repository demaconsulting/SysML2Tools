// <copyright file="LayoutBox.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout;

/// <summary>
/// Shape of a layout box.
/// </summary>
public enum BoxShape
{
    /// <summary>Plain rectangle.</summary>
    Rectangle,

    /// <summary>Rectangle with rounded corners.</summary>
    RoundedRectangle,

    /// <summary>Folder shape (rectangle with a tab on the top-left), used for packages.</summary>
    Folder,

    /// <summary>Note shape (rectangle with a folded-down top-right corner), used for documentation and comments.</summary>
    Note,
}

/// <summary>
/// A single compartment within a box (e.g., attributes section, operations section).
/// </summary>
/// <param name="Title">Optional compartment header text; <see langword="null"/> for untitled compartments.</param>
/// <param name="Rows">Text rows displayed inside the compartment.</param>
public sealed record LayoutCompartment(
    string? Title,
    IReadOnlyList<string> Rows);

/// <summary>
/// A rectangular container node with optional label, depth, compartments, and nested children.
/// </summary>
/// <param name="X">Absolute X coordinate of the left edge in logical pixels.</param>
/// <param name="Y">Absolute Y coordinate of the top edge in logical pixels.</param>
/// <param name="Width">Width of the box in logical pixels.</param>
/// <param name="Height">Height of the box in logical pixels.</param>
/// <param name="Label">Optional text label displayed at the top of the box.</param>
/// <param name="Depth">Nesting depth used by the renderer to index into <see cref="DemaConsulting.SysML2Tools.Rendering.Theme.DepthFillColors"/>.</param>
/// <param name="Shape">Visual shape of the box outline.</param>
/// <param name="Compartments">Ordered list of compartments displayed below the label.</param>
/// <param name="Children">Nested layout nodes contained spatially within this box.</param>
/// <param name="Keyword">
/// Optional SysML keyword (e.g. <c>"part def"</c>, <c>"port"</c>) rendered on a smaller line
/// above the bold label, following the SysML v2 graphical convention. <see langword="null"/> when no
/// keyword should be shown.
/// </param>
public sealed record LayoutBox(
    double X,
    double Y,
    double Width,
    double Height,
    string? Label,
    int Depth,
    BoxShape Shape,
    IReadOnlyList<LayoutCompartment> Compartments,
    IReadOnlyList<LayoutNode> Children,
    string? Keyword = null) : LayoutNode;
