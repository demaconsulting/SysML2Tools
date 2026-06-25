// <copyright file="SvgRenderer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout;
using DemaConsulting.SysML2Tools.Rendering;

namespace DemaConsulting.SysML2Tools.Svg;

/// <summary>
/// SVG renderer stub. Full implementation deferred to Phase 4.
/// </summary>
public sealed class SvgRenderer : IRenderer
{
    /// <inheritdoc/>
    public string MediaType => "image/svg+xml";

    /// <inheritdoc/>
    public string DefaultExtension => ".svg";

    /// <inheritdoc/>
    public void Render(LayoutTree layout, RenderOptions options, Stream output) =>
        throw new NotImplementedException("SvgRenderer is not yet implemented (Phase 4).");
}
