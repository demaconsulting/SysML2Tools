// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace DemaConsulting.SysML2Tools.Semantic.Model;

/// <summary>
///     Base class for all SysML/KerML AST nodes.
/// </summary>
/// <example>
/// A consumer typically pattern-matches over <see cref="Semantic.SysmlWorkspace.Declarations"/>
/// to find nodes of interest:
/// <code>
/// foreach (var (qualifiedName, node) in workspace.Declarations)
/// {
///     if (node is SysmlDefinitionNode { DefinitionKeyword: "part def" } partDef)
///     {
///         Console.WriteLine($"{qualifiedName}: {partDef.SupertypeNames.Count} supertype(s)");
///     }
/// }
/// </code>
/// </example>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(SysmlPackageNode), "package")]
[JsonDerivedType(typeof(SysmlDefinitionNode), "definition")]
[JsonDerivedType(typeof(SysmlFeatureNode), "feature")]
[JsonDerivedType(typeof(SysmlImportNode), "import")]
[JsonDerivedType(typeof(SysmlViewNode), "view")]
[JsonDerivedType(typeof(SysmlViewpointNode), "viewpoint")]
[JsonDerivedType(typeof(SysmlConnectionNode), "connection")]
[JsonDerivedType(typeof(SysmlTransitionNode), "transition")]
[JsonDerivedType(typeof(SysmlSatisfyNode), "satisfy")]
[JsonDerivedType(typeof(SysmlMetadataNode), "metadata")]
[JsonDerivedType(typeof(SysmlDependencyNode), "dependency")]
public abstract class SysmlNode
{
    /// <summary>
    ///     Gets the simple (unqualified) name of this element, or null if anonymous.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    ///     Gets the fully-qualified name of this element in its containing namespace.
    /// </summary>
    public string? QualifiedName { get; init; }

    /// <summary>
    ///     Gets the children of this node.
    /// </summary>
    public IReadOnlyList<SysmlNode> Children { get; init; } = Array.Empty<SysmlNode>();

    /// <summary>
    ///     Gets the supertype names referenced by specialization.
    /// </summary>
    /// <remarks>
    ///     For <see cref="SysmlFeatureNode"/> specifically, this field is populated only by
    ///     <c>AstBuilder.ExtractSubsettingTargetNames</c> (the <c>subsets &lt;target&gt;</c> /
    ///     <c>:&gt;</c> feature-relationship form) — no other code path populates a feature
    ///     node's <see cref="SupertypeNames"/>. It is therefore, implicitly, a subsetting-only
    ///     field on feature nodes; definition-level specialization (<c>subclassificationPart</c>,
    ///     e.g. <c>part def RacingDrone :&gt; Drone</c>) is a structurally separate grammar rule
    ///     that populates <see cref="SysmlDefinitionNode"/>'s own <see cref="SupertypeNames"/>
    ///     through a different code path and is unaffected by this remark.
    /// </remarks>
    public IReadOnlyList<string> SupertypeNames { get; init; } = Array.Empty<string>();

    /// <summary>
    ///     Gets the imported namespace names.
    /// </summary>
    public IReadOnlyList<string> ImportedNames { get; init; } = Array.Empty<string>();

    /// <summary>
    ///     Gets the raw requirement reference names verified by this node's nested <c>verify</c>
    ///     members (from <c>requirementVerificationMember</c>), one entry per <c>verify</c>
    ///     found directly or transitively nested in this node's specialized body (e.g. a
    ///     <c>requirement</c>/<c>case</c>/<c>verification</c>/<c>analysis</c> body, or an
    ///     <c>objective</c> nested within one). Resolved uniformly by <see cref="ReferenceResolver"/>
    ///     into <see cref="SysmlEdgeKind.Verify"/> edges sourced from this node.
    /// </summary>
    public IReadOnlyList<string> VerifiedRequirementNames { get; init; } = Array.Empty<string>();

    /// <summary>
    ///     Gets or sets the resolved outgoing edges (supertype, typing, import) originating from
    ///     this node, populated by <see cref="ReferenceResolver"/> after symbol-table
    ///     construction. Empty until resolution has run (e.g., for stdlib-only nodes, which are
    ///     registered but never passed through <see cref="ReferenceResolver.ResolveAll"/>).
    /// </summary>
    /// <remarks>
    ///     Settable (not <c>init</c>) because resolution runs after AST construction and after
    ///     the full symbol table is populated, mirroring the post-construction mutation pattern
    ///     already used by <see cref="Semantic.SysmlWorkspace.AddDeclaration"/>.
    /// </remarks>
    public IReadOnlyList<SysmlEdge> ResolvedEdges { get; set; } = Array.Empty<SysmlEdge>();

    /// <summary>
    ///     Gets the comment and documentation annotations captured for this element, in source
    ///     order. Populated by <see cref="AstBuilder"/> from <c>comment</c>/<c>documentation</c>
    ///     members nested directly in this element's body.
    /// </summary>
    /// <remarks>
    ///     An annotation with an explicit <c>about X</c> target is attached to its lexically
    ///     enclosing node rather than to the referenced element <c>X</c>. Comments/docs nested
    ///     inside a relationship body (e.g. <c>alias Car for Automobile { /* ... */ }</c>) are
    ///     not captured, since no <see cref="AstBuilder"/> visitor collects relationship bodies.
    /// </remarks>
    public IReadOnlyList<SysmlAnnotation> Annotations { get; init; } = Array.Empty<SysmlAnnotation>();
}

/// <summary>
///     AST node representing a SysML/KerML package or namespace.
/// </summary>
/// <remarks>
///     Inherited from SysmlNode: Name, QualifiedName, Children, SupertypeNames, ImportedNames,
///     VerifiedRequirementNames, ResolvedEdges, Annotations.
/// </remarks>
public sealed class SysmlPackageNode : SysmlNode
{
}

/// <summary>
///     AST node representing a definition element (part def, attribute def, etc.).
/// </summary>
/// <remarks>
///     Inherited from SysmlNode: Name, QualifiedName, Children, SupertypeNames, ImportedNames,
///     VerifiedRequirementNames, ResolvedEdges, Annotations.
/// </remarks>
public sealed class SysmlDefinitionNode : SysmlNode
{
    /// <summary>
    ///     Gets the definition keyword (e.g., "part def", "attribute def").
    /// </summary>
    public string DefinitionKeyword { get; init; } = string.Empty;
}

/// <summary>
///     AST node representing a usage/feature element (part, attribute, etc.).
/// </summary>
/// <remarks>
///     Inherited from SysmlNode: Name, QualifiedName, Children, SupertypeNames, ImportedNames,
///     VerifiedRequirementNames, ResolvedEdges, Annotations.
/// </remarks>
public sealed class SysmlFeatureNode : SysmlNode
{
    /// <summary>
    ///     Gets the usage keyword (e.g., "part", "port", "attribute", "ref").
    /// </summary>
    public string FeatureKeyword { get; init; } = string.Empty;

    /// <summary>
    ///     Gets the feature typing reference (the type after <c>:</c>), or null when untyped.
    /// </summary>
    public string? FeatureTyping { get; init; }

    /// <summary>
    ///     Gets the raw reference text of this feature's <c>redefines &lt;target&gt;;</c>/
    ///     <c>:&gt;&gt; &lt;target&gt;</c> clause, or null when the feature declares no
    ///     redefinition.
    /// </summary>
    public string? RedefinedFeatureName { get; init; }

    /// <summary>
    ///     Gets the multiplicity text (e.g., "[4]", "[0..*]"), or null when unspecified.
    /// </summary>
    public string? Multiplicity { get; init; }

    /// <summary>
    ///     Gets the raw expression text of a <c>constraint</c>/<c>assume constraint</c>/
    ///     <c>require constraint</c> feature's calculation body (or, for the reference form of a
    ///     requirement constraint, the raw reference text of the constraint it points to), or
    ///     null for features that are not a constraint expression. Captured verbatim, unparsed
    ///     (mirroring the <see cref="SysmlTransitionNode.Guard"/> raw-text-capture precedent) —
    ///     never evaluated, consistent with this unit's "do not model expression trees" scope
    ///     boundary.
    /// </summary>
    public string? ExpressionText { get; init; }
}

/// <summary>
///     AST node representing an import declaration.
/// </summary>
/// <remarks>
///     Inherited from SysmlNode: Name, QualifiedName, Children, SupertypeNames, ImportedNames,
///     VerifiedRequirementNames, ResolvedEdges, Annotations.
/// </remarks>
public sealed class SysmlImportNode : SysmlNode
{
    /// <summary>
    ///     Gets the imported namespace or qualified name.
    /// </summary>
    public string ImportedNamespace { get; init; } = string.Empty;

    /// <summary>
    ///     Gets a value indicating whether this is a wildcard import (::*).
    /// </summary>
    public bool IsWildcard { get; init; }

    /// <summary>
    ///     Gets a value indicating whether this import is recursive — derived from a trailing
    ///     <c>::**</c> on a namespace-import wildcard (<c>import X::*::**;</c>), which per the
    ///     KerML/SysML v2 grammar imports members of <c>X</c> and every namespace nested within it
    ///     at any depth, not just <c>X</c>'s direct members. <see langword="false"/> for a plain
    ///     <c>import X::*;</c> (direct members only) or any non-wildcard import.
    /// </summary>
    public bool IsRecursive { get; init; }

    /// <summary>
    ///     Gets the raw source text of this import's bracketed filter expression (from
    ///     <c>expose &lt;path&gt;::**[&lt;expr&gt;]</c>'s <c>filterPackageMember().ownedExpression().GetText()</c>),
    ///     or <see langword="null"/> when the import declares no bracket filter. Captured verbatim
    ///     only — mirroring <see cref="SysmlViewNode.FilterExpressionText"/> — no expression tree is
    ///     built and no evaluation is performed in Phase 1; a non-null value causes
    ///     <c>GeneralViewLayoutStrategy</c> to emit an "unevaluated" warning. Full bracket-filter
    ///     evaluation is deferred future work — see the project ROADMAP.
    /// </summary>
    public string? BracketFilterExpressionText { get; init; }
}

/// <summary>
///     AST node representing an applied metadata annotation (<c>{@Type{attr = value;}}</c> or the
///     bare <c>@Type;</c>/<c>@Type{}</c> forms), captured from a <c>metadataFeature</c> nested in
///     an owning element's body.
/// </summary>
/// <remarks>
///     Inherited from SysmlNode: Name, QualifiedName, Children, SupertypeNames, ImportedNames,
///     VerifiedRequirementNames, ResolvedEdges, Annotations. This node is attached as a
///     <see cref="SysmlNode.Children"/> entry of the element it annotates (its lexically enclosing
///     definition/feature), not as an <see cref="SysmlNode.Annotations"/> entry — unlike
///     comment/documentation, a metadata annotation is a first-class semantic reference (resolved
///     by <see cref="ReferenceResolver"/>) rather than free-text documentation.
/// </remarks>
public sealed class SysmlMetadataNode : SysmlNode
{
    /// <summary>
    ///     Gets the raw reference text of the annotating metadata type (e.g. <c>"Safety"</c> or
    ///     <c>"Pkg::Safety"</c>), from <c>metadataFeatureDeclaration().ownedFeatureTyping()</c>.
    ///     Resolved by <see cref="ReferenceResolver"/> into a <see cref="SysmlEdgeKind.MetadataType"/>
    ///     edge, or an unresolved-reference diagnostic when it does not resolve.
    /// </summary>
    public string TypeReference { get; init; } = string.Empty;

    /// <summary>
    ///     Gets the literal attribute values assigned in this annotation's body (e.g.
    ///     <c>isMandatory = true;</c>), in source order. Only scalar boolean/number/string literal
    ///     values are captured in Phase 1 (see <see cref="MetadataAttributeValue"/>); non-literal
    ///     value expressions are recorded with <see cref="MetadataAttributeValueKind.Unsupported"/>
    ///     and their raw text preserved, never evaluated.
    /// </summary>
    public IReadOnlyList<MetadataAttributeValue> Attributes { get; init; } = Array.Empty<MetadataAttributeValue>();
}

/// <summary>
///     Classifies the kind of literal value captured for a <see cref="MetadataAttributeValue"/>.
/// </summary>
public enum MetadataAttributeValueKind
{
    /// <summary>A boolean literal (<c>true</c>/<c>false</c>).</summary>
    Boolean,

    /// <summary>A numeric literal (integer or real).</summary>
    Number,

    /// <summary>A double-quoted string literal.</summary>
    String,

    /// <summary>
    ///     A value expression that is not a scalar literal (e.g. a feature reference, arithmetic
    ///     expression, or constructor call) — captured as raw text only, never evaluated.
    /// </summary>
    Unsupported,
}

/// <summary>
///     A single literal attribute value assigned within a <see cref="SysmlMetadataNode"/>'s body
///     (e.g. <c>isMandatory = true;</c>).
/// </summary>
/// <param name="Name">The attribute's simple name (e.g. <c>"isMandatory"</c>).</param>
/// <param name="Kind">The kind of literal value captured.</param>
/// <param name="RawText">The raw source text of the value expression (e.g. <c>"true"</c>).</param>
/// <param name="BooleanValue">The parsed boolean value when <paramref name="Kind"/> is <see cref="MetadataAttributeValueKind.Boolean"/>.</param>
/// <param name="NumberValue">The parsed numeric value when <paramref name="Kind"/> is <see cref="MetadataAttributeValueKind.Number"/>.</param>
/// <param name="StringValue">The parsed (unquoted) string value when <paramref name="Kind"/> is <see cref="MetadataAttributeValueKind.String"/>.</param>
public sealed record MetadataAttributeValue(
    string Name,
    MetadataAttributeValueKind Kind,
    string RawText,
    bool? BooleanValue = null,
    double? NumberValue = null,
    string? StringValue = null);

/// <summary>
///     AST node representing a view definition or view usage.
/// </summary>
/// <remarks>
///     Inherited from SysmlNode: Name, QualifiedName, Children, SupertypeNames, ImportedNames,
///     VerifiedRequirementNames, ResolvedEdges, Annotations.
/// </remarks>
public sealed class SysmlViewNode : SysmlNode
{
    /// <summary>
    ///     Gets the raw reference text of this view's <c>render &lt;target&gt;;</c> statement
    ///     (from <c>viewRenderingMember().viewRenderingUsage()</c>), or <see langword="null"/>
    ///     when the view declares no rendering member. Per the SysML v2 grammar, this names a
    ///     rendering style/format usage (e.g. <c>asTreeDiagram</c>, <c>asElementTable</c>) — never
    ///     a content-scoping subject. Captured verbatim only: <see cref="ReferenceResolver"/> never
    ///     inspects or resolves this value (no edge is produced, no diagnostic is emitted). This
    ///     value is used by <c>DiagramTypeRouter</c> to select a rendering strategy — an exact
    ///     match against <c>asTreeDiagram</c> or <c>asInterconnectionDiagram</c> takes precedence
    ///     over the name/supertype heuristic; any other value (including <c>asElementTable</c> and
    ///     <c>asTextualNotation</c>, which have no corresponding strategy) falls through unchanged.
    /// </summary>
    public string? RenderTargetName { get; init; }

    /// <summary>
    ///     Gets each <c>expose &lt;name&gt;;</c> member nested in this view's body, in source
    ///     order, paired with its own bracketed filter expression text (if any). Empty when the
    ///     view declares no <c>expose</c> members. Each entry's <see cref="ExposeMember.QualifiedName"/>
    ///     is resolved by <see cref="ReferenceResolver"/> into a <see cref="SysmlEdgeKind.Expose"/>
    ///     edge when it resolves, or an unresolved-reference diagnostic when it does not.
    /// </summary>
    public IReadOnlyList<ExposeMember> ExposeMembers { get; init; } = Array.Empty<ExposeMember>();

    /// <summary>
    ///     Gets the qualified name of each <c>expose &lt;name&gt;;</c> member nested in this view's
    ///     body, in source order — a computed convenience projection of
    ///     <see cref="ExposeMembers"/> retained for source-level compatibility with existing
    ///     readers (e.g. <see cref="ReferenceResolver"/>'s <c>Expose</c> edge construction).
    /// </summary>
    /// <returns>The qualified name of each <c>expose</c> member, in source order.</returns>
    public IReadOnlyList<string> GetExposedNames() => ExposeMembers.Select(m => m.QualifiedName).ToList();

    /// <summary>
    ///     Gets each <see cref="ExposeMember"/> paired directly with its own resolved qualified
    ///     name, populated by <see cref="ReferenceResolver"/> for every <c>ExposeMembers</c> entry
    ///     that successfully resolves (in the same relative order, skipping entries that fail to
    ///     resolve without inserting a placeholder). Unlike re-deriving this pairing from
    ///     <see cref="SysmlNode.ResolvedEdges"/>'s <see cref="SysmlEdgeKind.Expose"/> edges by
    ///     forward-scanning and loose-matching raw reference text against resolved qualified names
    ///     (ambiguous when an earlier entry fails to resolve but its raw text happens to be a
    ///     suffix of a later entry's resolved target), this list unambiguously identifies which
    ///     specific <see cref="ExposeMember"/> object produced each resolved qualified name, so
    ///     <c>ExposeScopeResolver</c> can read each resolved target's own
    ///     <see cref="ExposeMember.BracketFilterExpressionText"/>/<see cref="ExposeMember.RecursionKind"/>
    ///     correctly. Empty when the view has no <c>expose</c> members or none resolved. The
    ///     <see cref="SysmlEdgeKind.Expose"/> edges on <see cref="SysmlNode.ResolvedEdges"/> are
    ///     still populated in parallel and retain their existing role (e.g. impact analysis, the
    ///     <c>query</c> command's reverse-lookup index).
    /// </summary>
    public IReadOnlyList<(ExposeMember Member, string ResolvedQualifiedName)> ResolvedExposeMembers { get; set; } =
        Array.Empty<(ExposeMember Member, string ResolvedQualifiedName)>();

    /// <summary>
    ///     Gets the raw source text of this view's <c>filter [&lt;expression&gt;];</c> statement
    ///     (from <c>elementFilterMember().ownedExpression().GetText()</c>), or
    ///     <see langword="null"/> when the view declares no filter member. Captured verbatim only
    ///     — no expression tree is built and no evaluation is performed; a non-null value causes
    ///     <c>GeneralViewLayoutStrategy</c> to emit a "not yet evaluated" warning while still
    ///     rendering the (unfiltered) resolved scope. Full filter-expression evaluation is
    ///     deferred future work — see the project ROADMAP.
    /// </summary>
    public string? FilterExpressionText { get; init; }

}

/// <summary>
///     A single <c>expose &lt;path&gt;[::**[&lt;expr&gt;]];</c> member nested in a
///     <see cref="SysmlViewNode"/>'s body, pairing the exposed path's raw reference text with its
///     own bracketed filter expression text (if any) — fixing a Phase 1 gap where a view's
///     multiple <c>expose</c> members' paths and bracket-filter texts were captured as two
///     separate, unpaired, flattened lists (<c>ExposedNames</c>/<c>ExposeBracketFilterTexts</c>),
///     making it impossible to tell which exposed path a given bracket filter belonged to.
/// </summary>
/// <param name="QualifiedName">
///     The raw reference text of the exposed path (e.g. <c>"vehicle"</c> or <c>"Pkg::vehicle"</c>).
///     Resolved by <see cref="ReferenceResolver"/> into a <see cref="SysmlEdgeKind.Expose"/> edge
///     when it resolves, or an unresolved-reference diagnostic when it does not.
/// </param>
/// <param name="BracketFilterExpressionText">
///     The raw source text of this entry's bracketed filter expression (from
///     <c>expose &lt;path&gt;::**[&lt;expr&gt;]</c>'s
///     <c>filterPackageMember().ownedExpression().GetText()</c>), or <see langword="null"/> when
///     this entry declares no bracket filter. Parsed (via <c>FilterExpressionParser.Parse</c>) and
///     evaluated (via <c>FilterExpressionEvaluator.Evaluate</c>) by
///     <c>ExposeScopeResolver</c> to narrow this entry's own contribution to the view's exposed
///     scope to the matching descendant definitions only, falling back to whole-subtree inclusion
///     (plus a diagnostic) when the expression fails to parse or evaluate.
/// </param>
/// <param name="RecursionKind">
///     Classifies which SysML v2 <c>expose</c> grammar form and recursion setting produced this
///     entry (<see cref="ExposeRecursionKind"/>). Consumed by <c>ExposeScopeResolver</c> to decide
///     between an exact/direct-children match and a whole-subtree match when resolving this
///     entry's contribution to a view's exposed scope. Defaults to
///     <see cref="ExposeRecursionKind.MembershipRecursive"/> — the pre-existing whole-subtree
///     scoping behavior — so external code constructing an <see cref="ExposeMember"/> via the
///     previous two-argument form continues to compile and behave exactly as before this
///     parameter was added.
/// </param>
public sealed record ExposeMember(
    string QualifiedName,
    string? BracketFilterExpressionText,
    ExposeRecursionKind RecursionKind = ExposeRecursionKind.MembershipRecursive);

/// <summary>
///     Classifies which SysML v2 <c>expose</c> grammar form and recursion setting produced an
///     <see cref="ExposeMember"/> entry, per formal-26-03-02.md §8.3.26.2-4: MembershipExpose
///     (<c>expose X;</c> / <c>expose X::**;</c>) and NamespaceExpose (<c>expose X::*;</c> /
///     <c>expose X::*::**;</c>) each carry their own independent <c>isRecursive</c> flag derived
///     from a trailing <c>::**</c>. A bracket-filtered entry (<c>expose X::**[expr]</c>) is always
///     one of the two *Recursive kinds — the grammar's filterPackage form is treated as always
///     recursive by design, since that alternative is only reachable via <c>::**[filterExpr]</c>
///     (regardless of the nested optional STAR_STAR token) — because
///     <c>ExposeScopeResolver</c> only consults this classification for its unfiltered/fallback
///     whole-subtree-vs-narrow behavior, never for a successfully-evaluated bracket filter's own
///     (already-exact) <c>ExplicitMembers</c>.
/// </summary>
public enum ExposeRecursionKind
{
    /// <summary>
    ///     MembershipExpose, non-recursive: <c>expose X;</c> — only X itself (plus, for a usage
    ///     target, its resolved type itself — see <c>ExposeScopeResolver</c>) is in scope.
    /// </summary>
    MembershipExact,

    /// <summary>
    ///     MembershipExpose, recursive: <c>expose X::**;</c> — X and its entire containment
    ///     subtree (the pre-fix, still-correct-for-this-case whole-subtree behavior).
    /// </summary>
    MembershipRecursive,

    /// <summary>
    ///     NamespaceExpose, non-recursive: <c>expose X::*;</c> — only X's direct (one-level)
    ///     children, not X itself and not deeper descendants.
    /// </summary>
    NamespaceDirectChildren,

    /// <summary>
    ///     NamespaceExpose, recursive: <c>expose X::*::**;</c> — X's descendants at any depth
    ///     (not just direct children), but never X itself: per formal-26-03-02.md §8.3.26.4, a
    ///     NamespaceExpose exposes the subject's own Memberships (its members), and a namespace is
    ///     never a member of itself regardless of the recursive flag.
    /// </summary>
    NamespaceRecursive,
}

/// <summary>
///     AST node representing a viewpoint definition.
/// </summary>
/// <remarks>
///     Inherited from SysmlNode: Name, QualifiedName, Children, SupertypeNames, ImportedNames,
///     VerifiedRequirementNames, ResolvedEdges, Annotations.
/// </remarks>
public sealed class SysmlViewpointNode : SysmlNode
{
}

/// <summary>
///     AST node representing a connection/binding usage between two endpoints.
/// </summary>
/// <remarks>
///     Inherited from SysmlNode: Name, QualifiedName, Children, SupertypeNames, ImportedNames,
///     VerifiedRequirementNames, ResolvedEdges, Annotations.
/// </remarks>
public sealed class SysmlConnectionNode : SysmlNode
{
    /// <summary>
    ///     Gets the connection keyword. One of <c>"connection"</c>, <c>"message"</c>,
    ///     <c>"allocation"</c>, or <c>"binding"</c> (the latter two reusing this node's endpoint
    ///     shape for <c>allocate A to B</c> and <c>bind A = B</c> respectively, since
    ///     <c>allocationUsageDeclaration</c>'s and <c>bindingConnectorAsUsage</c>'s
    ///     <c>connectorPart</c> is the exact same grammar rule used by <c>connectionUsage</c>).
    /// </summary>
    public string ConnectionKeyword { get; init; } = string.Empty;

    /// <summary>
    ///     Gets the first endpoint reference (e.g., "engine.fuelPort"), or null when unresolved.
    /// </summary>
    public string? EndpointA { get; init; }

    /// <summary>
    ///     Gets the second endpoint reference (e.g., "transmission.input"), or null when unresolved.
    /// </summary>
    public string? EndpointB { get; init; }
}

/// <summary>
///     AST node representing a state transition (source state, target state, optional guard).
/// </summary>
/// <remarks>
///     Inherited from SysmlNode: Name, QualifiedName, Children, SupertypeNames, ImportedNames,
///     VerifiedRequirementNames, ResolvedEdges, Annotations.
/// </remarks>
public sealed class SysmlTransitionNode : SysmlNode
{
    /// <summary>
    ///     Gets the source state reference, or null when implied by the containing state.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    ///     Gets the target state reference.
    /// </summary>
    public string? Target { get; init; }

    /// <summary>
    ///     Gets the guard expression text (the condition after <c>if</c>), or null when unguarded.
    /// </summary>
    public string? Guard { get; init; }
}

/// <summary>
///     AST node representing a <c>satisfy X by Y;</c> requirement-satisfaction usage.
/// </summary>
/// <remarks>
///     Inherited from SysmlNode: Name, QualifiedName, Children, SupertypeNames, ImportedNames,
///     VerifiedRequirementNames, ResolvedEdges, Annotations.
/// </remarks>
public sealed class SysmlSatisfyNode : SysmlNode
{
    /// <summary>
    ///     Gets the raw reference text of the requirement being satisfied (from
    ///     <c>ownedReferenceSubsetting</c> when the <c>satisfy &lt;ref&gt;</c> form is used, or
    ///     from the declared/typed name of the <c>satisfy requirement &lt;usageDeclaration&gt;</c>
    ///     form), or null if it could not be determined.
    /// </summary>
    public string? RequirementName { get; init; }

    /// <summary>
    ///     Gets the raw reference text of the satisfying subject (from the <c>by &lt;subject&gt;</c>
    ///     clause), or null when no <c>by</c> clause is present.
    /// </summary>
    public string? SubjectName { get; init; }
}

/// <summary>
///     AST node representing a standalone <c>dependency (id)? (from A(,A)*)? to B(,B)*;</c>
///     relationship.
/// </summary>
/// <remarks>
///     Inherited from SysmlNode: Name, QualifiedName, Children, SupertypeNames, ImportedNames,
///     VerifiedRequirementNames, ResolvedEdges, Annotations.
/// </remarks>
public sealed class SysmlDependencyNode : SysmlNode
{
    /// <summary>
    ///     Gets the raw reference text of the client ("from") names. Populated for both the
    ///     explicit shape (<c>dependency from A to B;</c>) and the implicit-from shape with the
    ///     <c>from</c> keyword omitted (e.g. <c>dependency z to x;</c>, where <c>z</c> is still
    ///     classified as a from-name by its token position before <c>to</c>); empty only when no
    ///     qualified name at all precedes <c>to</c> (e.g. <c>dependency to x;</c>).
    /// </summary>
    public IReadOnlyList<string> FromNames { get; init; } = Array.Empty<string>();

    /// <summary>
    ///     Gets the raw reference text of the supplier ("to") names.
    /// </summary>
    public IReadOnlyList<string> ToNames { get; init; } = Array.Empty<string>();
}
