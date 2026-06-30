##### CrossingMinimizer

###### Purpose

`CrossingMinimizer` is the fourth pipeline stage. It groups the augmented nodes by layer and orders
each layer to reduce edge crossings, following ELK's crossing-minimization phase using Barycenter
ordering.

###### Responsibilities

The stage first groups every augmented node index into one list per layer, indexed by the node's
assigned layer. It then runs a fixed number of Barycenter sweeps that alternate forward (left to
right) and backward (right to left). Each sweep sorts a layer by the average position of each node's
neighbors in the adjacent layer; nodes without a neighbor in that layer keep their current relative
order, and ties are broken by the prior order to keep the result deterministic. The grouping is a
partition of the augmented nodes: every node appears in exactly one layer group.

###### Inputs and Outputs

- Reads: `LayeredGraph.AugNodes`, `LayeredGraph.AugEdges`.
- Writes: `LayeredGraph.Groups` (augmented-node indices ordered within each layer).

###### Error Handling

A null graph throws `ArgumentNullException`.

###### Dependencies

- `LayeredGraph` (Layered) — the shared state read from and written to.
- `LongEdgeSplitter` (Layered) — must run first to populate the augmented graph.
- `LayeredLayoutMetrics` (Layered) — supplies the Barycenter sweep count.
