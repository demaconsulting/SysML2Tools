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

/// <summary>Weight of a font.</summary>
public enum FontWeight
{
    /// <summary>Regular (normal) weight.</summary>
    Regular,

    /// <summary>Bold weight.</summary>
    Bold,
}

/// <summary>Style of a font.</summary>
public enum FontStyle
{
    /// <summary>Normal (upright) style.</summary>
    Normal,

    /// <summary>Italic style.</summary>
    Italic,
}

/// <summary>
/// A standalone text label at an absolute position. Width is capped to <see cref="MaxWidth"/>.
/// </summary>
/// <param name="X">Absolute X coordinate of the label origin in logical pixels.</param>
/// <param name="Y">Absolute Y coordinate of the label baseline in logical pixels.</param>
/// <param name="MaxWidth">Maximum width before text wraps or truncates.</param>
/// <param name="Text">Text content of the label.</param>
/// <param name="Align">Horizontal alignment of the text within the label bounds.</param>
/// <param name="Weight">Font weight applied to the label text.</param>
/// <param name="Style">Font style applied to the label text.</param>
/// <param name="FontSize">Font size in logical pixels.</param>
public sealed record LayoutLabel(
    double X,
    double Y,
    double MaxWidth,
    string Text,
    TextAlign Align,
    FontWeight Weight,
    FontStyle Style,
    double FontSize) : LayoutNode;
