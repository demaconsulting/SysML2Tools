// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

namespace DemaConsulting.SysML2Tools.Semantic.Model;

/// <summary>
///     Classifies the kind of resolved reference a <see cref="SysmlEdge"/> represents.
/// </summary>
public enum SysmlEdgeKind
{
    /// <summary>
    ///     A specialization reference (<see cref="SysmlNode.SupertypeNames"/> / <c>specializes</c> / <c>:&gt;</c>).
    /// </summary>
    Supertype,

    /// <summary>
    ///     A feature typing reference (<see cref="SysmlFeatureNode.FeatureTyping"/> / the type after <c>:</c>).
    /// </summary>
    Typing,

    /// <summary>
    ///     An import reference (<see cref="SysmlNode.ImportedNames"/> / <c>import X::Y</c> or <c>import X::*</c>).
    /// </summary>
    Import,

    /// <summary>
    ///     A requirement satisfaction reference (<c>satisfy X by Y</c>), from the satisfying
    ///     subject (<c>Y</c>) to the satisfied requirement (<c>X</c>).
    /// </summary>
    Satisfy,

    /// <summary>
    ///     A requirement verification reference (<c>verify ... : Requirement</c> or
    ///     <c>verify Requirement</c>), from the verifying case/requirement to the verified
    ///     requirement.
    /// </summary>
    Verify,

    /// <summary>
    ///     An allocation reference (<c>allocate A to B</c>), recorded as a single edge
    ///     <c>Source=A, Target=B</c> reflecting the textual left-to-right order of the
    ///     <c>allocate</c> statement. This ordering is a documentation convention only — it does
    ///     not imply a semantic "source causes target" direction (mirroring how
    ///     <c>connectorPart</c>'s two ends are treated as unordered A/B endpoints elsewhere in
    ///     the model).
    /// </summary>
    Allocate,

    /// <summary>
    ///     A resolved connector/message reference (<c>connect A to B</c> or a <c>message</c>'s
    ///     from/to events), recorded as a single edge <c>Source=A, Target=B</c> reflecting the
    ///     textual left-to-right order of the connector. Either endpoint may be a dotted feature
    ///     chain (e.g. <c>engine.fuelPort</c>), resolved by <c>ReferenceResolver</c>'s
    ///     feature-chain walk; the edge is recorded only when both endpoints resolve. This
    ///     ordering is a documentation convention only — it does not imply a semantic
    ///     "source causes target" direction, mirroring <see cref="Allocate"/>.
    /// </summary>
    Connect,

    /// <summary>
    ///     A resolved state-transition reference (<c>then</c> / <c>first ... then ...</c>), from
    ///     the source state to the target state. Either side may be a dotted feature chain,
    ///     resolved by <c>ReferenceResolver</c>'s feature-chain walk; the edge is recorded only
    ///     when both the source and target resolve — an implied/omitted source (no preceding
    ///     state to walk from) produces no edge, a documented limitation of this unit.
    /// </summary>
    Transition,

    /// <summary>
    ///     A view expose reference (<c>expose &lt;name&gt;;</c> nested in a view's body), from
    ///     the view to a resolved element whose containment subtree is included in the rendered
    ///     scope. One edge is recorded per resolvable entry in
    ///     <see cref="SysmlViewNode.ExposeMembers"/>; an unresolved entry produces an
    ///     unresolved-reference diagnostic instead. <c>GeneralViewLayoutStrategy</c> scopes its
    ///     diagram to the union of every <see cref="Expose"/> edge's target containment subtree;
    ///     a view with no <see cref="Expose"/> edges renders the full workspace, unchanged from
    ///     the pre-scoping baseline.
    /// </summary>
    Expose,

    /// <summary>
    ///     A feature redefinition reference (<see cref="SysmlFeatureNode.RedefinedFeatureName"/> /
    ///     <c>redefines X</c> / <c>:&gt;&gt; X</c>), from the redefining feature's owner to the
    ///     resolved redefined-feature reference.
    /// </summary>
    Redefinition,

    /// <summary>
    ///     A metadata annotation's type reference (<see cref="SysmlMetadataNode.TypeReference"/> /
    ///     <c>@Type</c> / <c>{@Type{...}}</c>), from the annotation to the resolved <c>metadata
    ///     def</c> declaration it references.
    /// </summary>
    MetadataType,

    /// <summary>
    ///     A standalone dependency reference (<c>dependency (id)? (from A(,A)*)? to B(,B)*;</c>,
    ///     <see cref="SysmlDependencyNode.FromNames"/> / <see cref="SysmlDependencyNode.ToNames"/>),
    ///     recorded as one edge per resolvable from×to pair, <c>Source=A, Target=B</c>, reflecting
    ///     the client (<c>from</c>) depending on the supplier (<c>to</c>). Only the common
    ///     single-segment name form is resolved (parity with <see cref="Satisfy"/>/
    ///     <see cref="Allocate"/>); an unresolved name produces a diagnostic instead of an edge.
    /// </summary>
    Dependency,

    /// <summary>
    ///     A binding connector reference (<c>bind A = B;</c>, the common
    ///     <c>bindingConnectorAsUsage</c> shape only), recorded as a single edge
    ///     <c>Source=A, Target=B</c> reflecting the textual left-to-right order of the binding.
    ///     Either endpoint may be a dotted feature chain, resolved by <c>ReferenceResolver</c>'s
    ///     feature-chain walk (parity with <see cref="Connect"/>); the longer
    ///     <c>bindingConnector</c>/<c>typeBody</c> form is a documented, out-of-scope limitation
    ///     (no corpus evidence).
    /// </summary>
    Binding,
}

/// <summary>
///     A resolved directed reference between two qualified names in the semantic model, used to
///     build reverse-lookup indexes for impact analysis and the <c>query</c> command.
/// </summary>
/// <param name="SourceQualifiedName">
///     Qualified name of the referencing node, or <see langword="null"/> when the referencing
///     node is anonymous (e.g., an unnamed import statement or inline feature).
/// </param>
/// <param name="TargetQualifiedName">Qualified name of the resolved target symbol.</param>
/// <param name="Kind">The kind of reference this edge represents.</param>
public sealed record SysmlEdge(string? SourceQualifiedName, string TargetQualifiedName, SysmlEdgeKind Kind);
