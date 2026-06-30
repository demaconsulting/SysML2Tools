##### CycleBreaker

###### Purpose

`CycleBreaker` is the first pipeline stage. It makes the input graph acyclic by reversing the edges
that close cycles, following ELK's cycle-breaking phase, so that the layering stage can assume a
directed acyclic graph.

###### Responsibilities

The stage reads `Nodes` (for the count) and `Edges` from the shared graph and writes the cleaned,
acyclic edge list to `Acyclic`. It classifies edges with a depth-first search: any edge that targets
a node still on the recursion stack is a back edge and is reversed; all other edges keep their
orientation. While building the result it discards self-loops and collapses duplicate source-target
pairs, so the output contains each retained connection exactly once with no self-loop.

###### Inputs and Outputs

- Reads: `LayeredGraph.Nodes` (count only), `LayeredGraph.Edges`.
- Writes: `LayeredGraph.Acyclic`.

###### Error Handling

A null graph throws `ArgumentNullException`. An empty edge list yields an empty acyclic set.

###### Dependencies

- `LayeredGraph` (Layered) — the shared state read from and written to.
- `LayerEdge` (Layout Engine) — the directed-edge record produced in the acyclic set.
