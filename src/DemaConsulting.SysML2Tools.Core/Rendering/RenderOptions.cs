// <copyright file="RenderOptions.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Rendering;

/// <summary>
/// Options that control the rendering of a <see cref="Layout.LayoutTree"/>.
/// </summary>
/// <param name="Theme">Visual theme (colors, fonts, line metrics).</param>
/// <param name="Scale">Uniform scale factor applied to all coordinates. Default is 1.0.</param>
/// <param name="Dpi">Output resolution in dots per inch. Default is 96.</param>
/// <param name="DepthLimit">Maximum nesting depth to render. 0 means unlimited.</param>
public sealed record RenderOptions(
    Theme Theme,
    double Scale = 1.0,
    double Dpi = 96.0,
    int DepthLimit = 0);
