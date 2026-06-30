##### ComponentPacker

###### Purpose

`ComponentPacker` is a composite pipeline stage that lays out a disconnected graph as a compact,
non-overlapping arrangement of its connected components. It is a clean-room re-implementation of the
documented behavior of ELK's `ComponentsProcessor`: split the graph into connected components, run
the wrapped inner stages on each component independently, then pack the laid-out components side by
side.

###### Responsibilities

The stage partitions the real nodes into connected components using union-find over the undirected
edge set, ignoring self-loops. When the graph forms a single component (including a fully connected
graph or a lone node), the stage is a transparent pass-through: it runs the wrapped inner stages
directly on the supplied graph so the output is byte-identical to running those stages without the
wrapper. When the graph is disconnected, it builds a sub-graph for each component (with a
local-to-original index remap), runs the inner stages on each, normalizes each component to its own
content bounding box, then assigns each component a shelf offset with a greedy row packer whose
target row width is the larger of the widest component and `sqrt(totalArea) * aspect`. Finally it
merges the placed coordinates, layer assignments, and routed edge waypoints back into the parent
graph in original node and edge index order, translating each component's waypoints by its assigned
offset. Component detection is deterministic — components are ordered by their lowest original node
index and nodes within a component by ascending original index — so renders are reproducible.

###### Inputs and Outputs

- Reads: `LayeredGraph.Nodes`, `LayeredGraph.Edges`, `LayeredGraph.Direction`.
- Writes: `LayeredGraph.AugX`, `LayeredGraph.AugY`, `LayeredGraph.NodeLayers`, `LayeredGraph.Waypoints`
  (one polyline per original edge), for the multi-component case; for the single-component case the
  wrapped inner stages write these fields directly.

###### Error Handling

A null graph throws `ArgumentNullException`. An empty graph (`LayeredGraph.N` == 0) is a no-op,
because the downstream stages cannot operate on an empty augmented graph.

###### Dependencies

- `LayeredGraph` (Layered) — the shared state read from and written to.
- `ILayoutStage` (Layered) — the inner stages wrapped and run per component.
- `LayeredLayoutMetrics` (Layered) — supplies the default packing spacing.
- The default inner stage sequence (`CycleBreaker`, `LayerAssigner`, `LongEdgeSplitter`,
  `CrossingMinimizer`, `BrandesKopfPlacer`, `PortDistributor`, `OrthogonalRouter`, `LongEdgeJoiner`,
  `AxisTransform`) when constructed via `WithDefaultStages`.
