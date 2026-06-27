// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using DemaConsulting.SysML2Tools.Semantic.Internal;

namespace DemaConsulting.SysML2Tools.Semantic;

/// <summary>
///     Represents a fully-loaded and semantically-resolved SysML/KerML workspace.
/// </summary>
public sealed class SysmlWorkspace
{
    /// <summary>
    ///     Mutable backing store for <see cref="Declarations"/>; allows post-construction
    ///     injection of synthetic nodes (e.g., auto-generated views) without requiring a
    ///     full workspace rebuild.
    /// </summary>
    private Dictionary<string, SysmlNode> _declarations = [];

    /// <summary>
    ///     Gets the list of loaded source file paths (virtual paths for stdlib, real paths for user files).
    /// </summary>
    public IReadOnlyList<string> Files { get; init; } = Array.Empty<string>();

    /// <summary>
    ///     Gets the qualified-name registry mapping fully-qualified names to their declaration nodes.
    /// </summary>
    /// <remarks>
    ///     The property is backed by a mutable <see cref="Dictionary{TKey,TValue}"/> so that
    ///     <see cref="AddDeclaration"/> can inject synthetic nodes after the workspace is constructed.
    ///     The <c>init</c> accessor copies the supplied dictionary so that construction-time
    ///     collection expressions are fully supported.
    /// </remarks>
    public IReadOnlyDictionary<string, SysmlNode> Declarations
    {
        get => _declarations;
        init => _declarations = new Dictionary<string, SysmlNode>(value);
    }

    /// <summary>
    ///     Injects a synthetic declaration into the workspace under the given qualified name.
    /// </summary>
    /// <param name="qualifiedName">
    ///     The fully-qualified name that will key the new entry in <see cref="Declarations"/>.
    ///     If the key already exists it is overwritten.
    /// </param>
    /// <param name="node">The node to register. Must not be null.</param>
    /// <remarks>
    ///     This method is intended for post-load augmentation only (e.g., the <c>--auto</c> flag
    ///     synthesizing a GeneralView). It should not be used during normal workspace construction.
    /// </remarks>
    public void AddDeclaration(string qualifiedName, SysmlNode node)
    {
        // Validate inputs — a null key or node would silently corrupt the declaration table
        ArgumentNullException.ThrowIfNull(qualifiedName);
        ArgumentNullException.ThrowIfNull(node);

        _declarations[qualifiedName] = node;
    }
}
