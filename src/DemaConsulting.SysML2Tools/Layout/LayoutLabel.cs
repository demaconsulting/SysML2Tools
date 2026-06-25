// <copyright file="LayoutLabel.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout;

/// <summary>
/// Horizontal text alignment.
/// </summary>
public enum TextAlign
{
    /// <summary>Align text to the left.</summary>
    Left,

    /// <summary>Align text to the center.</summary>
    Center,

    /// <summary>Align text to the right.</summary>
    Right,
}

/// <summary>
/// A standalone text label at an absolute position. Width is capped to <see cref="MaxWidth"/>.
/// </summary>
public sealed record LayoutLabel(
    double X,
    double Y,
    double MaxWidth,
    string Text,
    TextAlign Align) : LayoutNode;
