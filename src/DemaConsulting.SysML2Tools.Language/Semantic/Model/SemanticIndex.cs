// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

namespace DemaConsulting.SysML2Tools.Semantic.Model;

/// <summary>
///     Reverse-lookup index over resolved semantic edges (supertype, typing, import), answering
///     "what does X reference" and "what references X" queries in O(1) average time. Built once
///     per <see cref="ReferenceResolver.ResolveAll"/> pass and exposed via
///     <see cref="Semantic.SysmlWorkspace.Index"/> for consumption by the <c>query</c> command's
///     <c>uses</c>/<c>used-by</c>/<c>impact</c>/<c>hierarchy</c> verbs.
/// </summary>
/// <example>
/// <code>
/// foreach (var edge in workspace.Index.GetOutgoingEdges("Vehicles::Car"))
/// {
///     Console.WriteLine($"Car --{edge.Kind}--> {edge.TargetQualifiedName}");
/// }
/// </code>
/// </example>
public sealed class SemanticIndex
{
    /// <summary>
    ///     All edges in this index, in resolution order.
    /// </summary>
    private readonly List<SysmlEdge> _edges;

    /// <summary>
    ///     Maps a source qualified name to the edges it originates.
    /// </summary>
    private readonly Dictionary<string, List<SysmlEdge>> _outgoing = new(StringComparer.Ordinal);

    /// <summary>
    ///     Maps a target qualified name to the edges that reference it.
    /// </summary>
    private readonly Dictionary<string, List<SysmlEdge>> _incoming = new(StringComparer.Ordinal);

    /// <summary>
    ///     Initializes a new instance of <see cref="SemanticIndex"/>, indexing the given edges
    ///     by source and target qualified name.
    /// </summary>
    /// <param name="edges">The resolved edges to index.</param>
    public SemanticIndex(IEnumerable<SysmlEdge> edges)
    {
        ArgumentNullException.ThrowIfNull(edges);
        _edges = edges.ToList();

        foreach (var edge in _edges)
        {
            if (edge.SourceQualifiedName is { Length: > 0 } source)
            {
                if (!_outgoing.TryGetValue(source, out var outgoingList))
                {
                    outgoingList = [];
                    _outgoing[source] = outgoingList;
                }

                outgoingList.Add(edge);
            }

            if (!_incoming.TryGetValue(edge.TargetQualifiedName, out var incomingList))
            {
                incomingList = [];
                _incoming[edge.TargetQualifiedName] = incomingList;
            }

            incomingList.Add(edge);
        }
    }

    /// <summary>
    ///     Gets all resolved edges in this index, in resolution order.
    /// </summary>
    public IReadOnlyList<SysmlEdge> AllEdges => _edges;

    /// <summary>
    ///     Gets all edges whose source is the given qualified name (what <paramref name="qualifiedName"/> references).
    /// </summary>
    /// <param name="qualifiedName">The qualified name of the referencing node.</param>
    /// <returns>The outgoing edges, or an empty list when none are recorded.</returns>
    public IReadOnlyList<SysmlEdge> GetOutgoingEdges(string qualifiedName) =>
        _outgoing.TryGetValue(qualifiedName, out var list) ? list : Array.Empty<SysmlEdge>();

    /// <summary>
    ///     Gets all edges whose target is the given qualified name (what references <paramref name="qualifiedName"/>).
    /// </summary>
    /// <param name="qualifiedName">The qualified name of the referenced target.</param>
    /// <returns>The incoming edges, or an empty list when none are recorded.</returns>
    public IReadOnlyList<SysmlEdge> GetIncomingEdges(string qualifiedName) =>
        _incoming.TryGetValue(qualifiedName, out var list) ? list : Array.Empty<SysmlEdge>();
}
