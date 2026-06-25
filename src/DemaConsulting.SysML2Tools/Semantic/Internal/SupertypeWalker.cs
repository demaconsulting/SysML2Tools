// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using DemaConsulting.SysML2Tools.Parser;

namespace DemaConsulting.SysML2Tools.Semantic.Internal;

/// <summary>
///     Walks specialization chains to validate supertype references and detect cycles.
/// </summary>
internal sealed class SupertypeWalker
{
    private readonly SymbolTable _symbolTable;
    private readonly List<SysmlDiagnostic> _diagnostics;

    public SupertypeWalker(SymbolTable symbolTable, List<SysmlDiagnostic> diagnostics)
    {
        _symbolTable = symbolTable;
        _diagnostics = diagnostics;
    }

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
