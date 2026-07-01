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

###### Data Model

`CrossingMinimizer` is stateless; it holds no fields and operates entirely on the shared
`LayeredGraph` passed to `Apply`:

- `LayeredGraph.AugNodes` (input) — the augmented node list, each node carrying its assigned `Layer`.
- `LayeredGraph.AugEdges` (input) — augmented edges (source/target indices) that each span one layer.
- `LayeredGraph.Groups` (output) — a `List<List<int>>` with one inner list per layer, holding the
  augmented-node indices in left-to-right order within that layer. The lists form a partition: every
  augmented node index appears in exactly one group.

###### Key Methods

- `Apply(LayeredGraph graph)` — the `ILayoutStage` entry point. Rejects a null graph, builds
  `graph.Groups` by grouping nodes by layer, then reorders each layer in place. Postcondition:
  `graph.Groups` is populated and partitions every augmented node exactly once.
- `GroupByLayerAug` (private) — partitions the augmented-node indices into one list per layer index.
- `OrderLayersAug` (private) — precomputes each node's left and right neighbor lists once, then runs
  `BarycentricSweeps` sweeps that alternate forward (left-to-right) and backward (right-to-left).
- `SortByBarycenter` (private) — sorts one layer by the average index of each node's neighbors in the
  adjacent layer; nodes with no neighbor in that layer keep their relative order, and ties break by the
  prior order so the result is deterministic.

###### Error Handling

A null graph throws `ArgumentNullException`.

###### Dependencies

- `LayeredGraph` (Layered) — the shared state read from and written to.
- `LongEdgeSplitter` (Layered) — must run first to populate the augmented graph.
- `LayeredLayoutMetrics` (Layered) — supplies the Barycenter sweep count.

###### Callers

- `LayeredLayoutPipeline` (Layered) — adds `CrossingMinimizer` as the fourth stage (after
  `LongEdgeSplitter`, before `BrandesKopfPlacer`) when it assembles its ordered stage list.
- `ComponentPacker` (Layered) — constructs the same stage sequence, including `CrossingMinimizer`, to
  lay out each connected component independently.
