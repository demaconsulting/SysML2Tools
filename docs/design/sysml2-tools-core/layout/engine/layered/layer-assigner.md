##### LayerAssigner

###### Purpose

`LayerAssigner` is the second pipeline stage. It assigns each node to a layer using longest-path
layering over the acyclic edge set, so that columns can be placed left to right with no same-layer
connection.

###### Responsibilities

The stage reads the acyclic edge set produced by `CycleBreaker` and assigns each node a layer equal
to the length of the longest directed path that reaches it. Nodes with no incoming edge are placed
at layer zero; every other node is placed one layer past the deepest of its predecessors. The pass
is a topological relaxation (Kahn's algorithm) that processes each node after all its predecessors,
guaranteeing that every edge runs from a strictly lower layer to a strictly higher layer.

###### Inputs and Outputs

- Reads: `LayeredGraph.Nodes` (count only), `LayeredGraph.Acyclic`.
- Writes: `LayeredGraph.NodeLayers` (one layer index per real node, in node order).

###### Error Handling

A null graph throws `ArgumentNullException`. A graph with no edges leaves every node at layer zero.

###### Dependencies

- `LayeredGraph` (Layered) — the shared state read from and written to.
- `CycleBreaker` (Layered) — must run first to populate the acyclic edge set.
