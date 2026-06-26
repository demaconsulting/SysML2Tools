// <copyright file="DiagramRenderer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Internal;

namespace DemaConsulting.SysML2Tools.Rendering;

/// <summary>
/// High-level rendering orchestrator that iterates over all views in a
/// <see cref="SysmlWorkspace"/>, builds a <see cref="Layout.LayoutTree"/> via an
/// <see cref="ILayoutStrategy"/>, and writes each view to an output stream via
/// an <see cref="IRenderer"/>.
/// </summary>
/// <remarks>
/// <see cref="RenderWorkspace"/> is the single entry point for the rendering pipeline.
/// It delegates layout selection to <see cref="Internal.DiagramTypeRouter"/> and collects
/// one <see cref="RenderOutput"/> per view found in <see cref="SysmlWorkspace.Declarations"/>.
/// Views whose type is not supported by any available strategy are silently skipped.
/// </remarks>
public sealed class DiagramRenderer
{
    /// <summary>
    /// Renders every view in the workspace and returns a collection of output streams,
    /// one per view.
    /// </summary>
    /// <param name="workspace">The SysML workspace whose view declarations are rendered.</param>
    /// <param name="renderer">The format-specific renderer used to write each view.</param>
    /// <param name="options">Render options supplying theme, scale, and depth-limit settings.</param>
    /// <returns>
    /// An ordered list of <see cref="RenderOutput"/> instances, one per view in declaration order.
    /// Returns an empty list when the workspace contains no view declarations.
    /// </returns>
    // S2325: instance method — future phases will inject ILayoutStrategy via constructor making this non-static
#pragma warning disable S2325
    public IReadOnlyList<RenderOutput> RenderWorkspace(
        SysmlWorkspace workspace,
        IRenderer renderer,
        RenderOptions options)
#pragma warning restore S2325
    {
        // Validate inputs — null workspace or renderer would produce silent failures
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(options);

        var results = new List<RenderOutput>();

        // Iterate over all declarations and process each view node
        foreach (var (qualifiedName, node) in workspace.Declarations)
        {
            // Skip non-view declarations
            if (node is not SysmlViewNode viewNode)
            {
                continue;
            }

            // Skip stdlib view declarations — only user-defined views are rendered
            if (Internal.StdlibFilter.IsStdlibElement(qualifiedName))
            {
                continue;
            }

            // Route the view to an appropriate layout strategy
            var strategy = Internal.DiagramTypeRouter.GetStrategy(viewNode, workspace, out var unsupportedMsg);
            if (unsupportedMsg != null)
            {
                // Strategy not available for this view type — skip silently
                continue;
            }

            // Resolve the display name: prefer the simple name, fall back to qualified name
            var viewName = viewNode.Name ?? qualifiedName;
            var context = new ViewContext(viewName, workspace);

            // Build the layout tree for this view
            var layout = strategy.BuildLayout(context, options);

            // Render the layout tree to an in-memory stream
            var stream = new MemoryStream();
            renderer.Render(layout, options, stream);
            stream.Position = 0;

            // Derive a safe file name from the view name and add to results
            var fileName = SanitizeFileName(viewName) + renderer.DefaultExtension;
            results.Add(new RenderOutput(fileName, renderer.MediaType, stream));
        }

        return results;
    }

    /// <summary>
    /// Produces a file-system-safe name by replacing any character that is invalid
    /// in a file name with an underscore.
    /// </summary>
    /// <param name="name">The raw view name to sanitize.</param>
    /// <returns>A string containing only characters that are valid in file names.</returns>
    private static string SanitizeFileName(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}

