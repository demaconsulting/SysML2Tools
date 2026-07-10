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
    ///     Gets the raw reference text of each <c>expose &lt;name&gt;;</c> member nested in this
    ///     view's body, in source order. Empty when the view declares no <c>expose</c> members.
    ///     Each entry is resolved by <see cref="ReferenceResolver"/> into a
    ///     <see cref="SysmlEdgeKind.Expose"/> edge when it resolves, or an unresolved-reference
    ///     diagnostic when it does not.
    /// </summary>
    public IReadOnlyList<string> ExposedNames { get; init; } = Array.Empty<string>();

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

    /// <summary>
    ///     Gets the raw source text of every bracketed <c>expose &lt;path&gt;::**[&lt;expr&gt;]</c>
    ///     filter expression found among this view's <c>expose</c> members, in source order.
    ///     Captured verbatim only (from <c>filterPackageMember().ownedExpression().GetText()</c>) —
    ///     no expression tree is built and no evaluation is performed in Phase 1; a non-empty list
    ///     causes <c>GeneralViewLayoutStrategy</c> to emit an "unevaluated" warning distinct from
    ///     <see cref="FilterExpressionText"/>'s. Full bracket-filter evaluation is deferred future
    ///     work — see the project ROADMAP.
    /// </summary>
    public IReadOnlyList<string> ExposeBracketFilterTexts { get; init; } = Array.Empty<string>();
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
    ///     Gets the connection keyword. One of <c>"connection"</c>, <c>"message"</c>, or
    ///     <c>"allocation"</c> (the latter reusing this node's endpoint shape for
    ///     <c>allocate A to B</c>, since <c>allocationUsageDeclaration</c>'s <c>connectorPart</c>
    ///     is the exact same grammar rule used by <c>connectionUsage</c>).
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
