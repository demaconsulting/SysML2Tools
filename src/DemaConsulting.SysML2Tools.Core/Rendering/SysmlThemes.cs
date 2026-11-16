// <copyright file="SysmlThemes.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.SysML2Tools.Rendering;

/// <summary>
/// SysML-specific <see cref="Theme"/> presets for use with <see cref="RenderOptions"/>.
/// </summary>
/// <remarks>
/// The presets complement the general-purpose <c>Themes</c> shipped with the rendering engine:
/// they encode the SysML v2 graphical-notation conventions rather than a generic color scheme.
/// Instances are immutable and safe to share across threads.
/// </remarks>
public static class SysmlThemes
{
    /// <summary>
    /// Gets the plain, monochrome theme used by the <c>render</c> command by default: every
    /// box, container, and table-header fill is white, and every border and line is a black
    /// one-pixel stroke.
    /// </summary>
    /// <remarks>
    /// The SysML v2 specification (OMG formal/2025-09-03, graphical BNF in clause 8.2.3) draws all
    /// graphical elements black-on-white and encodes meaning through shape, border style, and
    /// text rather than fill color. The depth-coded tints of <c>Themes.Light</c> are therefore
    /// replaced by a single-entry <see cref="Theme.DepthFillColors"/> list (the engine indexes the
    /// list modulo its length, so every nesting depth resolves to white, and the engine-derived
    /// background and grid-header fills are white as well). Fonts, padding, corner radius, and
    /// routing metrics are inherited from <c>Themes.Light</c> so layout metrics are unchanged.
    /// </remarks>
    public static Theme Plain { get; } = Themes.Light with
    {
        DepthFillColors = ["#FFFFFF"],
        StrokeColor = "#000000",
        StrokeWidth = 1.0,
    };
}
