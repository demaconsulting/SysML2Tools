// <copyright file="PngRenderer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout;
using DemaConsulting.SysML2Tools.Rendering;

namespace DemaConsulting.SysML2Tools.Png;

/// <summary>
/// PNG renderer stub. Full implementation deferred to Phase 4.
/// </summary>
public sealed class PngRenderer : IRenderer
{
    /// <inheritdoc/>
    public string MediaType => "image/png";

    /// <inheritdoc/>
    public string DefaultExtension => ".png";

    /// <inheritdoc/>
    public void Render(LayoutTree layout, RenderOptions options, Stream output) =>
        throw new NotImplementedException("PngRenderer is not yet implemented (Phase 4).");
}
