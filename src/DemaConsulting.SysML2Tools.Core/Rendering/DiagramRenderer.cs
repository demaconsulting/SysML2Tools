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
    /// Returns the display names of all renderable user-defined views in the workspace,
    /// mirroring the filtering applied by <see cref="RenderWorkspace"/>.
    /// </summary>
    /// <param name="workspace">The SysML workspace whose view declarations are inspected.</param>
    /// <returns>
    /// An ordered list of view display names. Returns an empty list when the workspace
    /// contains no renderable view declarations.
    /// </returns>
    public static IReadOnlyList<string> GetViewNames(SysmlWorkspace workspace)
    {
        // Validate input — null workspace would produce silent failures
        ArgumentNullException.ThrowIfNull(workspace);

        var names = new List<string>();

        // Iterate over all declarations and collect each renderable view name
        foreach (var (qualifiedName, node) in workspace.Declarations)
        {
            // Skip non-view declarations
            if (node is not SysmlViewNode viewNode)
            {
                continue;
            }

            // Skip stdlib view declarations — only user-defined views are considered
            if (Internal.StdlibFilter.IsStdlibElement(qualifiedName, workspace.StdlibNames))
            {
                continue;
            }

            // Skip views whose type is not supported by any available strategy
            var strategy = Internal.DiagramTypeRouter.GetStrategy(viewNode, workspace, out var unsupportedMsg);
            if (unsupportedMsg != null)
            {
                continue;
            }

            // Suppress unused variable warning — strategy is validated but not used here
            _ = strategy;

            // Resolve the display name: prefer the simple name, fall back to qualified name
            names.Add(viewNode.Name ?? qualifiedName);
        }

        return names;
    }

    /// <summary>
    /// Renders every view in the workspace and returns a collection of output streams,
    /// one per view.
    /// </summary>
    /// <param name="workspace">The SysML workspace whose view declarations are rendered.</param>
    /// <param name="renderer">The format-specific renderer used to write each view.</param>
    /// <param name="options">Render options supplying theme, scale, and depth-limit settings.</param>
    /// <param name="viewFilter">
    /// When non-null, only the view whose display name equals this value is rendered.
    /// When null, all renderable views are rendered.
    /// </param>
    /// <returns>
    /// An ordered list of <see cref="RenderOutput"/> instances, one per view in declaration order.
    /// Returns an empty list when the workspace contains no view declarations.
    /// </returns>
    // S2325: instance method — future phases will inject ILayoutStrategy via constructor making this non-static
#pragma warning disable S2325
    public IReadOnlyList<RenderOutput> RenderWorkspace(
        SysmlWorkspace workspace,
        IRenderer renderer,
        RenderOptions options,
        string? viewFilter = null)
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
            if (Internal.StdlibFilter.IsStdlibElement(qualifiedName, workspace.StdlibNames))
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

            // Skip views that do not match the active filter
            if (viewFilter != null && !string.Equals(viewName, viewFilter, StringComparison.Ordinal))
            {
                continue;
            }

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
    /// Synthesizes a <see cref="SysmlViewNode"/> that targets the most representative
    /// top-level element in the workspace, for use when the user passes <c>--auto</c>
    /// and no user-defined views are present.
    /// </summary>
    /// <param name="workspace">
    /// The SysML workspace to inspect. Must not be null.
    /// </param>
    /// <returns>
    /// A synthetic <see cref="SysmlViewNode"/> whose <c>Name</c> is derived from the chosen
    /// element (e.g. <c>"VehicleA"</c> → <c>"VehicleAView"</c>), or <see langword="null"/>
    /// when the workspace contains no non-stdlib declarations to target.
    /// </returns>
    /// <remarks>
    /// Selection priority:
    /// <list type="number">
    ///   <item>The non-stdlib <c>part def</c> with the highest direct-child count.</item>
    ///   <item>The first non-stdlib definition when no <c>part def</c> exists.</item>
    /// </list>
    /// The returned node has empty <see cref="SysmlNode.Children"/> and
    /// <see cref="SysmlNode.SupertypeNames"/> because the GeneralView layout strategy
    /// derives its content from workspace declarations rather than the view's own children.
    /// </remarks>
    public static SysmlViewNode? SynthesizeAutoView(SysmlWorkspace workspace)
    {
        // Validate input — null workspace would cause a NullReferenceException
        ArgumentNullException.ThrowIfNull(workspace);

        // Collect all non-stdlib definition nodes as candidates for the auto view target
        SysmlDefinitionNode? bestPartDef = null;
        SysmlDefinitionNode? firstDefinition = null;

        foreach (var (qualifiedName, node) in workspace.Declarations)
        {
            // Skip stdlib elements — only user-defined declarations are considered
            if (Internal.StdlibFilter.IsStdlibElement(qualifiedName, workspace.StdlibNames))
            {
                continue;
            }

            // Consider only definition nodes (part def, attribute def, etc.)
            if (node is not SysmlDefinitionNode defNode)
            {
                continue;
            }

            // Record the first non-stdlib definition as the fallback target
            firstDefinition ??= defNode;

            // Prefer a part def, and among part defs prefer the one with the most direct children
            if (string.Equals(defNode.DefinitionKeyword, "part def", StringComparison.Ordinal) &&
                (bestPartDef is null || defNode.Children.Count > bestPartDef.Children.Count))
            {
                bestPartDef = defNode;
            }
        }

        // Choose the best candidate: part def with most children, then any definition, then none
        var target = bestPartDef ?? firstDefinition;
        if (target is null)
        {
            return null;
        }

        // Build the synthesized view name by appending "View" to the element's simple name
        var viewName = (target.Name ?? target.QualifiedName ?? "AutoView") + "View";
        var viewQualifiedName = (target.QualifiedName ?? target.Name ?? "AutoView") + "View";

        return new SysmlViewNode
        {
            Name = viewName,
            QualifiedName = viewQualifiedName
        };
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

