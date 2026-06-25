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
}

/// <summary>
/// A single compartment within a box (e.g., attributes section, operations section).
/// </summary>
public sealed record LayoutCompartment(
    string? Title,
    IReadOnlyList<string> Rows);

/// <summary>
/// A rectangular container node with optional label, depth, compartments, and nested children.
/// </summary>
public sealed record LayoutBox(
    double X,
    double Y,
    double Width,
    double Height,
    string? Label,
    int Depth,
    BoxShape Shape,
    IReadOnlyList<LayoutCompartment> Compartments,
    IReadOnlyList<LayoutNode> Children) : LayoutNode;
