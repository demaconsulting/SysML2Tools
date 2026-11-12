// <copyright file="DiagramTypeRouter.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Internal;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;

namespace DemaConsulting.SysML2Tools.Rendering.Internal;

/// <summary>
/// Routes a view node to the appropriate <see cref="ILayoutStrategy"/> implementation
/// based on the view type.
/// </summary>
/// <remarks>
/// Dispatch order: (1) the view's own <c>render</c> target (<see cref="SysmlViewNode.RenderTargetName"/>,
/// exact <see cref="StringComparison.Ordinal"/> match, optionally qualified with <c>Views::</c>):
/// <c>asTreeDiagram</c> and <c>asInterconnectionDiagram</c> (the Views library renderings) route to
/// the browser and interconnection strategies, and the tool-defined <c>asGeneralDiagram</c>,
/// <c>asStateTransitionDiagram</c>, <c>asActionFlowDiagram</c>, <c>asSequenceDiagram</c>, and
/// <c>asGridDiagram</c> (produced by <c>DynamicViewSynthesizer</c>) route to their strategies.
/// (2) The view's view definition — its resolved <c>Typing</c> edge, then transitively the general
/// view definitions it specializes, nearest first: the first one that declares a recognized
/// <c>render</c> target, or is a standard view definition from <c>StandardViewDefinitions</c>
/// (SysML v2 §9.2.20: <c>GeneralView</c>, <c>InterconnectionView</c>, <c>ActionFlowView</c>,
/// <c>StateTransitionView</c>, <c>SequenceView</c>, <c>GridView</c>, <c>BrowserView</c>), selects the
/// strategy, since a view inherits its definition's rendering (§7.26.2). <c>GeometryView</c> has no
/// strategy and falls through. (3) Otherwise — including <c>asElementTable</c>,
/// <c>asTextualNotation</c>, and views with neither a recognized rendering nor a standard view
/// definition — the tool-defined name/supertype heuristic: the view's declared supertype names (and
/// its own name) are inspected for <c>Interconnection</c>, <c>StateTransition</c>/<c>State</c>,
/// <c>ActionFlow</c>/<c>Action</c>, <c>Grid</c>/<c>Matrix</c>/<c>Tabular</c>,
/// <c>Browser</c>/<c>Tree</c>, and <c>Sequence</c>; everything else falls back to the general view
/// strategy.
/// </remarks>
internal static class DiagramTypeRouter
{
    /// <summary>
    /// Returns the <see cref="ILayoutStrategy"/> appropriate for the given view node.
    /// </summary>
    /// <param name="viewNode">The view node to route. Must not be null.</param>
    /// <param name="workspace">The workspace containing the model being rendered.</param>
    /// <param name="unsupportedMessage">
    /// Set to a non-null diagnostic message when no strategy can be determined for the view type.
    /// The caller should skip rendering this view and may log the message as a diagnostic.
    /// </param>
    /// <returns>
    /// An <see cref="ILayoutStrategy"/> instance to use for this view, or <see langword="null"/>
    /// when <paramref name="unsupportedMessage"/> is non-null.
    /// </returns>
    public static ILayoutStrategy GetStrategy(
        object viewNode,
        SysmlWorkspace workspace,
        out string? unsupportedMessage)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        unsupportedMessage = null;

        if (viewNode is SysmlViewNode view)
        {
            if (FromRenderTarget(view.RenderTargetName) is { } rendered)
            {
                return rendered;
            }

            if (FromViewDefinitions(view, workspace) is { } defined)
            {
                return defined;
            }

            if (Matches(view, "Interconnection"))
            {
                return new InterconnectionViewLayoutStrategy();
            }

            if (Matches(view, "StateTransition") || Matches(view, "State"))
            {
                return new StateTransitionViewLayoutStrategy();
            }

            if (Matches(view, "ActionFlow") || Matches(view, "Action"))
            {
                return new ActionFlowViewLayoutStrategy();
            }

            if (Matches(view, "Grid") || Matches(view, "Matrix") || Matches(view, "Tabular"))
            {
                return new GridViewLayoutStrategy();
            }

            if (Matches(view, "Browser") || Matches(view, "Tree"))
            {
                return new BrowserViewLayoutStrategy();
            }

            if (Matches(view, "Sequence"))
            {
                return new SequenceViewLayoutStrategy();
            }
        }

        return new GeneralViewLayoutStrategy();
    }

    /// <summary>
    /// Returns the strategy selected by a recognized <c>render</c> target name, or
    /// <see langword="null"/> for any other value.
    /// </summary>
    private static ILayoutStrategy? FromRenderTarget(string? renderTargetName) => StripViewsPrefix(renderTargetName) switch
    {
        "asTreeDiagram" => new BrowserViewLayoutStrategy(),
        "asInterconnectionDiagram" => new InterconnectionViewLayoutStrategy(),
        "asGeneralDiagram" => new GeneralViewLayoutStrategy(),
        "asStateTransitionDiagram" => new StateTransitionViewLayoutStrategy(),
        "asActionFlowDiagram" => new ActionFlowViewLayoutStrategy(),
        "asSequenceDiagram" => new SequenceViewLayoutStrategy(),
        "asGridDiagram" => new GridViewLayoutStrategy(),
        _ => null,
    };

    /// <summary>
    /// Removes a leading <c>Views::</c> qualifier, so the qualified spelling of a standard
    /// rendering (e.g. <c>Views::asTreeDiagram</c>) is recognized like its simple name.
    /// </summary>
    private static string? StripViewsPrefix(string? renderTargetName) =>
        renderTargetName is not null && renderTargetName.StartsWith("Views::", StringComparison.Ordinal)
            ? renderTargetName["Views::".Length..]
            : renderTargetName;

    /// <summary>
    /// Returns the strategy for a standard view definition qualified name (SysML v2 §9.2.20), or
    /// <see langword="null"/> when the name is not one this tool renders.
    /// </summary>
    private static ILayoutStrategy? FromStandardViewDefinition(string qualifiedName) => qualifiedName switch
    {
        "StandardViewDefinitions::GeneralView" => new GeneralViewLayoutStrategy(),
        "StandardViewDefinitions::InterconnectionView" => new InterconnectionViewLayoutStrategy(),
        "StandardViewDefinitions::ActionFlowView" => new ActionFlowViewLayoutStrategy(),
        "StandardViewDefinitions::StateTransitionView" => new StateTransitionViewLayoutStrategy(),
        "StandardViewDefinitions::SequenceView" => new SequenceViewLayoutStrategy(),
        "StandardViewDefinitions::GridView" => new GridViewLayoutStrategy(),
        "StandardViewDefinitions::BrowserView" => new BrowserViewLayoutStrategy(),
        _ => null,
    };

    /// <summary>
    /// Walks a view's resolved view definition (its <c>Typing</c> edge) and, transitively, the
    /// general view definitions it specializes (<c>Supertype</c> edges), nearest first. The first
    /// definition reached that either declares a recognized <c>render</c> target or is a standard
    /// view definition selects the strategy, since a view usage inherits its definition's rendering
    /// and notation (SysML v2 §7.26.2, §9.2.20).
    /// </summary>
    private static ILayoutStrategy? FromViewDefinitions(SysmlViewNode view, SysmlWorkspace workspace)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>(DefinitionTargets(view));
        while (pending.TryDequeue(out var qualifiedName))
        {
            if (!visited.Add(qualifiedName))
            {
                continue;
            }

            if (FromStandardViewDefinition(qualifiedName) is { } standard)
            {
                return standard;
            }

            if (!workspace.Declarations.TryGetValue(qualifiedName, out var node) || node is not SysmlViewNode definition)
            {
                continue;
            }

            if (FromRenderTarget(definition.RenderTargetName) is { } rendered)
            {
                return rendered;
            }

            foreach (var target in DefinitionTargets(definition))
            {
                pending.Enqueue(target);
            }
        }

        return null;
    }

    /// <summary>Returns the targets of a view node's resolved <c>Typing</c> and <c>Supertype</c> edges.</summary>
    private static IEnumerable<string> DefinitionTargets(SysmlViewNode view) =>
        (view.ResolvedEdges ?? [])
            .Where(edge => edge.Kind is SysmlEdgeKind.Typing or SysmlEdgeKind.Supertype)
            .Select(edge => edge.TargetQualifiedName);

    /// <summary>
    /// Determines whether a view declares the given view-kind marker in its name or a supertype.
    /// </summary>
    private static bool Matches(SysmlViewNode view, string marker)
    {
        if (view.Name is not null && view.Name.Contains(marker, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return view.SupertypeNames.Any(s => s.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
