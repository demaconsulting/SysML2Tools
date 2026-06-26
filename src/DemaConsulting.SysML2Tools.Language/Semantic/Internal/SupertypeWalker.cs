// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using DemaConsulting.SysML2Tools.Parser;

namespace DemaConsulting.SysML2Tools.Semantic.Internal;

/// <summary>
///     Walks specialization chains to validate supertype references and detect cycles.
/// </summary>
internal sealed class SupertypeWalker
{
    /// <summary>
    ///     The symbol table providing all registered declarations for chain traversal.
    /// </summary>
    private readonly SymbolTable _symbolTable;

    /// <summary>
    ///     The shared diagnostics list to which cyclic-specialization Warning entries are appended.
    /// </summary>
    private readonly List<SysmlDiagnostic> _diagnostics;

    /// <summary>
    ///     Initializes a new instance of <see cref="SupertypeWalker"/> with the given symbol
    ///     table and diagnostics list.
    /// </summary>
    public SupertypeWalker(SymbolTable symbolTable, List<SysmlDiagnostic> diagnostics)
    {
        _symbolTable = symbolTable;
        _diagnostics = diagnostics;
    }

    /// <summary>
    ///     Walks all specialization chains for every symbol in the table and emits Warning
    ///     diagnostics for any cyclic specialization detected.
    /// </summary>
    public void WalkAll()
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (name, node) in _symbolTable.Symbols)
        {
            if (!visited.Contains(name))
            {
                WalkNode(node, name, new HashSet<string>(StringComparer.Ordinal), visited);
            }
        }
    }

    /// <summary>
    ///     Recursive DFS helper that traverses the specialization chain rooted at the given node,
    ///     emitting a Warning when a back-edge (cycle) is detected.
    /// </summary>
    private void WalkNode(
        SysmlNode node,
        string qualifiedName,
        HashSet<string> chainVisited,
        HashSet<string> globalVisited)
    {
        globalVisited.Add(qualifiedName);
        chainVisited.Add(qualifiedName);

        foreach (var supertypeName in node.SupertypeNames)
        {
            if (chainVisited.Contains(supertypeName))
            {
                _diagnostics.Add(new SysmlDiagnostic(
                    string.Empty,
                    0, 0,
                    DiagnosticSeverity.Warning,
                    $"Cyclic specialization detected: '{qualifiedName}' specializes '{supertypeName}'"));
                continue;
            }

            var supertypeNode = _symbolTable.Lookup(supertypeName);
            if (supertypeNode is not null && !globalVisited.Contains(supertypeName))
            {
                WalkNode(supertypeNode, supertypeName, new HashSet<string>(chainVisited), globalVisited);
            }
        }
    }
}
