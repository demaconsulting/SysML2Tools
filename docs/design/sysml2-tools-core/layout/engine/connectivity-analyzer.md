#### ConnectivityAnalyzer

##### Purpose

`ConnectivityAnalyzer` analyses the topology of a graph before any geometry is computed. It builds a
sparse undirected adjacency list, derives a longest-path layer hint per node (0 = source), and groups
nodes into natural communities with label propagation. The community pass collapses a hub-and-spoke
fan into a single community even though the spokes have no mutual affinity. The outputs bias seed
generation, coarse placement, and the reading-direction hierarchy of the downstream engines.

##### Data Model

`ConnectivityAnalyzer` is a static class with no instance state. Inputs are a list of
`ConnectivityNode` records (each a stable `Id`) and a list of `ConnectivityEdge` records (each a
directed pair of indices). The result is a `ConnectivityResult` record carrying per-node `LayerHints`,
per-node 0-based `CommunityIds`, and the sparse undirected `Adjacency` list, all in input order.

##### Key Methods

###### `Analyze(nodes, edges)`

1. **Degenerate case.** An empty node list returns empty hints, communities, and adjacency.
2. **Adjacency.** A symmetric neighbour list is built in O(m) from the edges; self-loops and
   duplicates are dropped and never a dense n² matrix.
3. **Layer hints.** Back edges are reversed (DFS) so longest-path layering terminates; each node's
   layer is the longest path from any source.
4. **Communities.** Deterministic label propagation, tie-broken toward the smallest label, settles a
   partition; labels are renumbered densely by first appearance.

###### `CrossingScore(upperOrder, lowerOrder, edges)`

Counts the pairwise edge crossings between two ordered layers — a helper for seed scoring.

##### Error Handling

Null `nodes` or `edges` arguments throw `ArgumentNullException`. Edges with out-of-range indices are
ignored. All other inputs are handled without throwing.

##### Dependencies

None beyond the BCL collections.

##### Callers

View layout strategies and multi-seed placement engines that need layer hints, communities, or a
crossing estimate before placing geometry.
