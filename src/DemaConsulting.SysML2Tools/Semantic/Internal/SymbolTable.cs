// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

namespace DemaConsulting.SysML2Tools.Semantic.Internal;

/// <summary>
///     Registry mapping fully-qualified SysML/KerML names to their declaration nodes.
/// </summary>
internal sealed class SymbolTable
{
    /// <summary>
    ///     The internal dictionary mapping fully-qualified names to their declaration nodes.
    /// </summary>
    private readonly Dictionary<string, SysmlNode> _symbols = new(StringComparer.Ordinal);

    /// <summary>
    ///     Gets the registered symbols as a read-only dictionary.
    /// </summary>
    public IReadOnlyDictionary<string, SysmlNode> Symbols => _symbols;

    /// <summary>
    ///     Registers all named nodes from the given AST root into the symbol table.
    /// </summary>
    public void RegisterAll(SysmlNode? root)
    {
        if (root is null)
        {
            return;
        }

        RegisterNode(root);
    }

    /// <summary>
    ///     Registers a single node and all of its descendants into the symbol dictionary.
    /// </summary>
    private void RegisterNode(SysmlNode node)
    {
        if (node.QualifiedName is { Length: > 0 })
        {
            _symbols.TryAdd(node.QualifiedName, node);
        }

        foreach (var child in node.Children)
        {
            RegisterNode(child);
        }
    }

    /// <summary>
    ///     Looks up a symbol by its fully-qualified name.
    /// </summary>
    public SysmlNode? Lookup(string qualifiedName) =>
        _symbols.TryGetValue(qualifiedName, out var node) ? node : null;

    /// <summary>
    ///     Returns true if the symbol table contains the given qualified name.
    /// </summary>
    public bool Contains(string qualifiedName) => _symbols.ContainsKey(qualifiedName);
}
