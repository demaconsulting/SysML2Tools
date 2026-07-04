// <copyright file="NamespaceDoc.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Rendering;

/// <summary>
/// Renders a loaded SysML/KerML workspace's <c>view</c> declarations to SVG/PNG diagrams.
/// </summary>
/// <remarks>
/// <see cref="DiagramRenderer"/> is the entry point: it iterates the view declarations in a
/// <see cref="Semantic.SysmlWorkspace"/>, selects a layout strategy for each, and writes the
/// resulting diagram through a caller-supplied <c>IRenderer</c> (e.g. <c>SvgRenderer</c>,
/// <c>PngRenderer</c>, both from <c>DemaConsulting.Rendering.*</c>). Custom layout algorithms
/// implement <see cref="ILayoutStrategy"/>, which receives a <see cref="ViewContext"/>
/// identifying the view and workspace to lay out.
/// <para>
/// A workspace must be loaded before it can be rendered: seed the standard library via
/// <c>StdlibProvider.GetSymbolTable()</c> (<see cref="Stdlib.StdlibProvider"/>) and load user
/// files via <c>WorkspaceLoader.LoadAsync</c> (<see cref="Semantic.WorkspaceLoader"/>).
/// </para>
/// <para>
/// The example below additionally requires a renderer package reference beyond what this project
/// (<c>DemaConsulting.SysML2Tools.Core</c>) already brings in transitively via
/// <c>DemaConsulting.Rendering.Layout</c> (which supplies <c>DemaConsulting.Rendering</c> and
/// <c>DemaConsulting.Rendering.Abstractions</c> — the source of <c>RenderOptions</c>,
/// <c>Themes</c>, and <c>RenderOutput</c> used below). Add a concrete renderer package for the
/// output format you need:
/// <code>
/// dotnet add package DemaConsulting.Rendering.Svg   # for SvgRenderer
/// dotnet add package DemaConsulting.Rendering.Skia  # for PngRenderer
/// </code>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
/// var loadResult = await WorkspaceLoader.LoadAsync(["model.sysml"], stdlibTable);
/// if (loadResult.Workspace is null)
/// {
///     return; // parse/resolution errors — see loadResult.Diagnostics
/// }
///
/// var diagramRenderer = new DiagramRenderer();
/// var options = new RenderOptions(Themes.Light);
/// var outputs = diagramRenderer.RenderWorkspace(loadResult.Workspace, new SvgRenderer(), options);
///
/// foreach (var output in outputs)
/// {
///     await using var file = File.Create(output.SuggestedFileName);
///     await output.Data.CopyToAsync(file);
/// }
/// </code>
/// </example>
internal static class NamespaceDoc
{
}
