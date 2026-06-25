// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

namespace DemaConsulting.SysML2Tools.Semantic.Internal;

/// <summary>
///     Base class for all SysML/KerML AST nodes.
/// </summary>
internal abstract class SysmlNode
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
internal sealed class SysmlPackageNode : SysmlNode
{
}

/// <summary>
///     AST node representing a definition element (part def, attribute def, etc.).
/// </summary>
internal sealed class SysmlDefinitionNode : SysmlNode
{
    /// <summary>
    ///     Gets the definition keyword (e.g., "part def", "attribute def").
    /// </summary>
    public string DefinitionKeyword { get; init; } = string.Empty;
}

/// <summary>
///     AST node representing a usage/feature element (part, attribute, etc.).
/// </summary>
internal sealed class SysmlFeatureNode : SysmlNode
{
}

/// <summary>
///     AST node representing an import declaration.
/// </summary>
internal sealed class SysmlImportNode : SysmlNode
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
internal sealed class SysmlViewNode : SysmlNode
{
}

/// <summary>
///     AST node representing a viewpoint definition.
/// </summary>
internal sealed class SysmlViewpointNode : SysmlNode
{
}
