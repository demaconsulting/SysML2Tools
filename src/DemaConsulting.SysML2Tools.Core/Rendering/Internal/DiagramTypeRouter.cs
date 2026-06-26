// <copyright file="DiagramTypeRouter.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Internal;
using DemaConsulting.SysML2Tools.Semantic;

namespace DemaConsulting.SysML2Tools.Rendering.Internal;

/// <summary>
/// Routes a view node to the appropriate <see cref="ILayoutStrategy"/> implementation
/// based on the view type.
/// </summary>
/// <remarks>
/// Phase 4 simplification: all view types are routed to <see cref="GeneralViewLayoutStrategy"/>.
/// Future phases will inspect the view's stereotype or keyword to select a specialized strategy
/// (e.g., IBD, sequence, activity).
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
        // Phase 4 simplification: route all view types to the general view strategy.
        // Future phases will add stereotype inspection to select specialized strategies.
        _ = viewNode;
        _ = workspace;
        unsupportedMessage = null;
        return new GeneralViewLayoutStrategy();
    }
}
