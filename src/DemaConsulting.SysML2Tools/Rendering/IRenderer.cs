// <copyright file="IRenderer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout;

namespace DemaConsulting.SysML2Tools.Rendering;

/// <summary>
/// Low-level renderer: converts one <see cref="LayoutTree"/> to one output stream.
/// Implementations must be pure, stateless, and must not perform filesystem access.
/// </summary>
public interface IRenderer
{
    /// <summary>Gets the MIME media type produced by this renderer.</summary>
    string MediaType { get; }

    /// <summary>Gets the default file extension (including leading dot) produced by this renderer.</summary>
    string DefaultExtension { get; }

    /// <summary>
    /// Renders the layout tree and writes the output to <paramref name="output"/>.
    /// </summary>
    /// <param name="layout">The layout tree describing all nodes to render.</param>
    /// <param name="options">Render options including theme and scale.</param>
    /// <param name="output">Destination stream that receives all rendered bytes.</param>
    void Render(LayoutTree layout, RenderOptions options, Stream output);
}
