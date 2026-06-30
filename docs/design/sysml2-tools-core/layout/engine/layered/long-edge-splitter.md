##### LongEdgeSplitter

###### Purpose

`LongEdgeSplitter` is the third pipeline stage. It builds the augmented graph in which every edge
spans exactly one layer, following ELK's long-edge splitting phase, so that routing can treat each
inter-layer gap independently.

###### Responsibilities

The stage first copies each real node into an `AugNode` carrying its size and assigned layer. It then
walks the acyclic edges: a unit-span edge becomes a single `AugEdge`; an edge whose endpoints differ
by more than one layer is replaced by a chain `source → d1 → … → target`, inserting one zero-size
dummy `AugNode` at each intermediate layer and one `AugEdge` per chain link. Every sub-edge records
the index of the original edge it belongs to, so the routing result can later be reassembled per
original connection.

###### Inputs and Outputs

- Reads: `LayeredGraph.Nodes`, `LayeredGraph.NodeLayers`, `LayeredGraph.Acyclic`.
- Writes: `LayeredGraph.AugNodes`, `LayeredGraph.AugEdges`.

###### Error Handling

A null graph throws `ArgumentNullException`. A graph with only unit-span edges produces an augmented
node list equal in count to the input node list (no dummies).

###### Dependencies

- `LayeredGraph` (Layered) — the shared state read from and written to.
- `LayerAssigner` (Layered) — must run first to populate the node layers.
