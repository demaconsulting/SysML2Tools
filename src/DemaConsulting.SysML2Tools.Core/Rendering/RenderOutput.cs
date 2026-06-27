// <copyright file="RenderOutput.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Rendering;

/// <summary>
/// A single rendered output stream with metadata.
/// </summary>
/// <param name="SuggestedFileName">Suggested file name including extension (no path).</param>
/// <param name="MediaType">MIME media type of the output stream.</param>
/// <param name="Data">The rendered output data stream.</param>
public sealed record RenderOutput(
    string SuggestedFileName,
    string MediaType,
    Stream Data)
{
    /// <summary>
    /// Gets non-fatal layout-quality warnings produced while laying out this view (e.g. connectors
    /// that could not be routed without crossing a box). Empty when the layout is clean.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
