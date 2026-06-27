// <copyright file="LayoutGrid.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout;

/// <summary>
/// A single cell within a grid row.
/// </summary>
/// <param name="Width">Width of the cell in logical pixels.</param>
/// <param name="Height">Height of the cell in logical pixels.</param>
/// <param name="Text">Text content displayed inside the cell.</param>
/// <param name="Align">Horizontal alignment of the text within the cell.</param>
/// <param name="ColSpan">Number of columns this cell spans; normally 1.</param>
public sealed record LayoutGridCell(
    double Width,
    double Height,
    string Text,
    TextAlign Align,
    int ColSpan);

/// <summary>
/// A single row within a layout grid.
/// </summary>
/// <param name="IsHeader">When <see langword="true"/>, the row is rendered with header styling.</param>
/// <param name="Cells">Ordered list of cells in this row.</param>
public sealed record LayoutGridRow(
    bool IsHeader,
    IReadOnlyList<LayoutGridCell> Cells);

/// <summary>
/// A tabular layout node at an absolute position.
/// </summary>
/// <param name="X">Absolute X coordinate of the grid left edge in logical pixels.</param>
/// <param name="Y">Absolute Y coordinate of the grid top edge in logical pixels.</param>
/// <param name="Rows">Ordered list of rows in the grid.</param>
public sealed record LayoutGrid(
    double X,
    double Y,
    IReadOnlyList<LayoutGridRow> Rows) : LayoutNode;
