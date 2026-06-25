// <copyright file="LayoutGrid.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout;

/// <summary>
/// A single cell within a grid row.
/// </summary>
public sealed record LayoutGridCell(
    double Width,
    double Height,
    string Text,
    TextAlign Align,
    int ColSpan);

/// <summary>
/// A single row within a layout grid.
/// </summary>
public sealed record LayoutGridRow(
    bool IsHeader,
    IReadOnlyList<LayoutGridCell> Cells);

/// <summary>
/// A tabular layout node at an absolute position.
/// </summary>
public sealed record LayoutGrid(
    double X,
    double Y,
    IReadOnlyList<LayoutGridRow> Rows) : LayoutNode;
