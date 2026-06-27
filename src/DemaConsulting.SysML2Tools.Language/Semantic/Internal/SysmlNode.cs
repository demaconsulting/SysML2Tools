// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace DemaConsulting.SysML2Tools.Semantic.Internal;

/// <summary>
///     Base class for all SysML/KerML AST nodes.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(SysmlPackageNode), "package")]
[JsonDerivedType(typeof(SysmlDefinitionNode), "definition")]
[JsonDerivedType(typeof(SysmlFeatureNode), "feature")]
[JsonDerivedType(typeof(SysmlImportNode), "import")]
[JsonDerivedType(typeof(SysmlViewNode), "view")]
[JsonDerivedType(typeof(SysmlViewpointNode), "viewpoint")]
[JsonDerivedType(typeof(SysmlConnectionNode), "connection")]
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
    ///     Gets the connection keyword (e.g., "connection", "binding").
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
