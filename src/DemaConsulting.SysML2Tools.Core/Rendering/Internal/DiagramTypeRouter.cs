// <copyright file="DiagramTypeRouter.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Internal;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Internal;

namespace DemaConsulting.SysML2Tools.Rendering.Internal;

/// <summary>
/// Routes a view node to the appropriate <see cref="ILayoutStrategy"/> implementation
/// based on the view type.
/// </summary>
/// <remarks>
/// Dispatch inspects the view's declared supertype names (and its own name) for a recognized view
/// kind. A view that specializes a name containing <c>Interconnection</c> routes to the
/// interconnection strategy; everything else falls back to the general view strategy.
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
        _ = workspace;
        unsupportedMessage = null;

        if (viewNode is SysmlViewNode view && IsInterconnectionView(view))
        {
            return new InterconnectionViewLayoutStrategy();
        }

        return new GeneralViewLayoutStrategy();
    }

    /// <summary>
    /// Determines whether a view declares itself as an interconnection view by specializing (or
    /// being named after) a view kind whose name contains <c>Interconnection</c>.
    /// </summary>
    private static bool IsInterconnectionView(SysmlViewNode view)
    {
        const string Marker = "Interconnection";
        if (view.Name is not null && view.Name.Contains(Marker, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return view.SupertypeNames.Any(s => s.Contains(Marker, StringComparison.OrdinalIgnoreCase));
    }
}
