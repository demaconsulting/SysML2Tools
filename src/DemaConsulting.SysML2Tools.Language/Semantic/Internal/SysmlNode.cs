// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace DemaConsulting.SysML2Tools.Semantic.Internal;

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
    ///     An annotation with an explicit <c>about X</c> target is still attached to its
    ///     lexically enclosing node rather than to the referenced element <c>X</c>; resolving
    ///     explicit <c>about</c> targets is deferred to a future unit. Comments/docs nested
    ///     inside a relationship body (e.g. <c>alias Car for Automobile { /* ... */ }</c>) are
    ///     also not yet captured, since no <see cref="AstBuilder"/> visitor currently collects
    ///     relationship bodies.
    /// </remarks>
    public IReadOnlyList<SysmlAnnotation> Annotations { get; init; } = Array.Empty<SysmlAnnotation>();
}

/// <summary>
///     AST node representing a SysML/KerML package or namespace.
/// </summary>
public sealed class SysmlPackageNode : SysmlNode
{
}

/// <summary>
///     AST node representing a definition element (part def, attribute def, etc.).
/// </summary>
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
    ///     Gets the multiplicity text (e.g., "[4]", "[0..*]"), or null when unspecified.
    /// </summary>
    public string? Multiplicity { get; init; }
}

/// <summary>
///     AST node representing an import declaration.
/// </summary>
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
}

/// <summary>
///     AST node representing a view definition.
/// </summary>
public sealed class SysmlViewNode : SysmlNode
{
}

/// <summary>
///     AST node representing a viewpoint definition.
/// </summary>
public sealed class SysmlViewpointNode : SysmlNode
{
}

/// <summary>
///     AST node representing a connection/binding usage between two endpoints.
/// </summary>
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
