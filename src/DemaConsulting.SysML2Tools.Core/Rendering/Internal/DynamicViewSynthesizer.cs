// <copyright file="DynamicViewSynthesizer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;

namespace DemaConsulting.SysML2Tools.Rendering.Internal;

/// <summary>
/// Synthesizes an in-memory <see cref="SysmlViewNode"/> targeting any resolvable, non-stdlib
/// element in a loaded workspace, without requiring the user to add a <c>view def</c> to the
/// model — the engine behind the <c>render --view-type &lt;kind&gt; --view-target
/// &lt;qualified-name&gt; [--filter &lt;expr&gt;]</c> CLI feature ("dynamic" or "ad-hoc" views).
/// </summary>
/// <remarks>
/// <para>
/// The synthesized node is scoped to its target by manually populating <see
/// cref="SysmlNode.ResolvedEdges"/> with a single <see cref="SysmlEdgeKind.Expose"/> edge (plus a
/// matching <see cref="SysmlViewNode.ExposeMembers"/> entry using
/// <see cref="ExposeRecursionKind.MembershipRecursive"/>) — the same mechanism a real,
/// parsed <c>view def V { expose Target::**; }</c> produces via <c>ReferenceResolver</c>, so
/// that a dynamic view shows the requested target's whole containment subtree rather than the
/// target alone. <see cref="Layout.Internal.ExposeScopeResolver.ResolveExposedScope"/> reads
/// only these two properties and has no notion of provenance, so it treats a synthesized node
/// identically to a parsed one. This differs from <see cref="DiagramRenderer.SynthesizeAutoView"/>, whose node
/// carries no <c>ResolvedEdges</c> at all — that absence is what makes <c>ExposeScopeResolver</c>
/// return a <see langword="null"/> scope (render everything); a dynamic view instead always
/// resolves to a definite, non-null scope rooted at the requested target's whole subtree.
/// </para>
/// <para>
/// The synthesized view's <see cref="SysmlNode.QualifiedName"/> uses a leading <c>$</c> — a
/// character illegal in a SysML identifier — so it cannot collide with any real, parsed
/// declaration; <see cref="Synthesize"/> additionally checks <see
/// cref="SysmlWorkspace.Declarations"/> for the (extremely unlikely) case where the same
/// synthesized name was already injected, and reports a diagnostic rather than silently
/// overwriting an existing entry.
/// </para>
/// </remarks>
internal static class DynamicViewSynthesizer
{
    /// <summary>
    /// Maps each accepted <c>--view-type</c> CLI value to the <see
    /// cref="SysmlViewNode.RenderTargetName"/> token <see cref="DiagramTypeRouter"/> dispatches on
    /// (its highest-precedence, exact-match dispatch path — see <see
    /// cref="DiagramTypeRouter"/>'s remarks), avoiding any dependency on the target's own name
    /// coincidentally containing (or not containing) a name-heuristic marker word.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> ViewTypeToRenderTarget = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["general"] = "asGeneralDiagram",
        ["interconnection"] = "asInterconnectionDiagram",
        ["state"] = "asStateTransitionDiagram",
        ["action"] = "asActionFlowDiagram",
        ["sequence"] = "asSequenceDiagram",
        ["grid"] = "asGridDiagram",
        ["browser"] = "asTreeDiagram",
    };

    /// <summary>
    /// Synthesizes a <see cref="SysmlViewNode"/> targeting <paramref name="targetQualifiedName"/>
    /// with the layout kind named by <paramref name="viewType"/>.
    /// </summary>
    /// <param name="workspace">The loaded workspace to resolve the target against. Must not be null.</param>
    /// <param name="viewType">
    /// One of <c>"general"</c>, <c>"interconnection"</c>, <c>"state"</c>, <c>"action"</c>,
    /// <c>"sequence"</c>, <c>"grid"</c>, or <c>"browser"</c> (case-sensitive).
    /// </param>
    /// <param name="targetQualifiedName">
    /// The fully-qualified name of the element to render, looked up in <see
    /// cref="SysmlWorkspace.Declarations"/>.
    /// </param>
    /// <param name="filterExpressionText">
    /// The raw <c>--filter</c> expression text, passed through unchanged to the synthesized
    /// node's <see cref="SysmlViewNode.FilterExpressionText"/>, or <see langword="null"/> when no
    /// filter was supplied.
    /// </param>
    /// <returns>
    /// A tuple: on success, a non-null <c>ViewNode</c> and a null <c>Diagnostic</c>; on failure, a
    /// null <c>ViewNode</c> and a non-null, human-readable <c>Diagnostic</c> describing why
    /// synthesis could not proceed (unrecognized <paramref name="viewType"/>, unresolved or
    /// wrong-kind target, a per-kind structural compatibility failure, or a name collision).
    /// </returns>
    internal static (SysmlViewNode? ViewNode, string? Diagnostic) Synthesize(
        SysmlWorkspace workspace,
        string viewType,
        string targetQualifiedName,
        string? filterExpressionText)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(viewType);
        ArgumentNullException.ThrowIfNull(targetQualifiedName);

        // (a) Validate the requested view type and resolve it to a RenderTargetName token.
        if (!ViewTypeToRenderTarget.TryGetValue(viewType, out var renderTargetName))
        {
            var known = string.Join(", ", ViewTypeToRenderTarget.Keys);
            return (null, $"unrecognized --view-type '{viewType}'; valid values are: {known}");
        }

        // (b) Resolve the target and reject unresolved or wrong-kind targets.
        if (!workspace.Declarations.TryGetValue(targetQualifiedName, out var target))
        {
            return (null, $"--view-target '{targetQualifiedName}' was not found in the workspace");
        }

        if (target is SysmlViewNode or SysmlViewpointNode or SysmlImportNode or SysmlMetadataNode
            or SysmlTransitionNode or SysmlConnectionNode)
        {
            return (null, $"--view-target '{targetQualifiedName}' is a {DescribeKind(target)}, which cannot be used as a dynamic view target");
        }

        if (StdlibFilter.IsStdlibElement(targetQualifiedName, workspace.StdlibNames))
        {
            return (null, $"--view-target '{targetQualifiedName}' is a standard-library element, which cannot be used as a dynamic view target");
        }

        // (c) Run the cheap, per-kind structural compatibility pre-check.
        var compatibilityDiagnostic = CheckCompatibility(viewType, target, targetQualifiedName);
        if (compatibilityDiagnostic is not null)
        {
            return (null, compatibilityDiagnostic);
        }

        // (d) Construct the synthesized view node, scoped to the target via a manually
        // populated Expose edge/member pair (see remarks).
        var viewQualifiedName = "$" + targetQualifiedName;
        if (workspace.Declarations.ContainsKey(viewQualifiedName))
        {
            return (null, $"internal error: synthesized view name '{viewQualifiedName}' already exists in the workspace");
        }

        var viewName = "$" + (target.Name ?? targetQualifiedName);

        var viewNode = new SysmlViewNode
        {
            Name = viewName,
            QualifiedName = viewQualifiedName,
            RenderTargetName = renderTargetName,
            ExposeMembers = [new ExposeMember(targetQualifiedName, null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge(viewQualifiedName, targetQualifiedName, SysmlEdgeKind.Expose)],
            FilterExpressionText = filterExpressionText,
        };

        return (viewNode, null);
    }

    /// <summary>
    /// Runs the cheap, necessary-but-not-sufficient structural compatibility pre-check for the
    /// requested view kind against the target's own <see cref="SysmlNode.Children"/>, mirroring
    /// each strategy's own <c>FindRoot</c>/root-selection gating condition (see the design
    /// documentation for the exact rule per kind and known limitations).
    /// </summary>
    /// <returns>A diagnostic message when the target fails the pre-check; otherwise null.</returns>
    private static string? CheckCompatibility(string viewType, SysmlNode target, string targetQualifiedName)
    {
        switch (viewType)
        {
            case "general":
            case "grid":
            case "browser":
                // GeneralViewLayoutStrategy/GridViewLayoutStrategy/BrowserViewLayoutStrategy scope
                // to any resolvable, non-stdlib definition or usage — no further structural
                // precondition applies.
                return null;

            case "interconnection":
                if (target is not SysmlDefinitionNode { DefinitionKeyword: "part def" })
                {
                    return $"--view-target '{targetQualifiedName}' is not a 'part def', which the interconnection view requires";
                }

                if (!target.Children.OfType<SysmlFeatureNode>().Any(f => f.FeatureKeyword == "part"))
                {
                    return $"--view-target '{targetQualifiedName}' has no nested 'part' features, so the interconnection view would render nothing";
                }

                return null;

            case "state":
                var hasStateTransitions = target.Children.OfType<SysmlTransitionNode>().Any();
                var hasStateFeature = target.Children.OfType<SysmlFeatureNode>().Any(f => f.FeatureKeyword == "state");
                if (!hasStateTransitions && !hasStateFeature)
                {
                    return $"--view-target '{targetQualifiedName}' has no nested state transitions or 'state' features, so the state-transition view would render nothing";
                }

                return null;

            case "action":
                var hasTransitions = target.Children.OfType<SysmlTransitionNode>().Any();
                var hasActionFeature = target.Children.OfType<SysmlFeatureNode>().Any(f => f.FeatureKeyword == "action");
                if (!hasTransitions && !hasActionFeature)
                {
                    return $"--view-target '{targetQualifiedName}' has no successions or 'action' features, so the action-flow view would render nothing";
                }

                return null;

            case "sequence":
                // KNOWN LIMITATION (cheap necessary-but-not-sufficient pre-check only): the AST
                // has no dedicated "lifeline" node — SequenceViewLayoutStrategy.CollectLifelines
                // derives lifelines from each `message` usage's endpoint references — so this
                // check approximates "at least one lifeline" as "at least one nested `message`
                // usage", which is both necessary (no messages means no lifelines, and the real
                // strategy's FindRoot/CollectLifelines/ResolveMessages gate would reject the
                // target too) and cheap (no endpoint resolution). It is not sufficient: a target
                // whose message endpoints fail to resolve to any lifeline index still passes this
                // pre-check yet still renders the canonical near-blank `LayoutTree` sentinel — see
                // the design documentation and docs/user_guide/introduction.md for the same
                // documented gap.
                var hasMessage = target.Children.OfType<SysmlConnectionNode>().Any(c => c.ConnectionKeyword == "message");
                if (!hasMessage)
                {
                    return $"--view-target '{targetQualifiedName}' has no nested messages (lifelines), so the sequence view would render nothing";
                }

                return null;

            default:
                // Unreachable: viewType was already validated against ViewTypeToRenderTarget's
                // keys before this method is called.
                throw new InvalidOperationException($"internal error: unhandled view type '{viewType}'");
        }
    }

    /// <summary>Returns a short human-readable description of a node's kind, for diagnostics.</summary>
    private static string DescribeKind(SysmlNode node) => node switch
    {
        SysmlViewNode => "view",
        SysmlViewpointNode => "viewpoint",
        SysmlImportNode => "import",
        SysmlMetadataNode => "metadata annotation",
        SysmlTransitionNode => "transition",
        SysmlConnectionNode => "connection",
        _ => node.GetType().Name,
    };
}
