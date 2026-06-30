##### OrthogonalRouter

###### Purpose

`OrthogonalRouter` is the seventh pipeline stage. It routes every inter-layer corridor with ELK's
orthogonal slot algorithm, producing the axis-aligned bend points for each augmented sub-edge.

###### Responsibilities

For each corridor (the gap between two adjacent layers) the stage collects the sub-edges whose source
lies in the left layer and turns each into a routing segment carrying its source and target port Y.
It builds crossing-based dependencies between segment pairs (the placement with fewer crossings wins),
breaks any cycles in those dependencies, and assigns each segment a routing slot by topological
numbering, so dependent segments occupy successively higher slots. A segment whose source and target
ports already share a Y is straight and consumes no slot and no bend points; any other segment emits
two bend points that form a vertical run at the slot's X, offset from the source column by the slot
index times the edge spacing.

For a reversed (back) edge — one stored flipped by `CycleBreaker`, so the consumer draws the open
chevron end marker on the augmented-source face of the first sub-edge whose source is a real node
(not a long-edge dummy) — the wrap-around corridor is the connector's final straight approach into
its true target. At the default slot that approach is only one `ConnectorClearance` wide, shorter
than the end marker. The stage therefore clamps that sub-edge's bend X outward to guarantee a
minimum approach of `graph.BackEdgeEntryApproach`. That parameter defaults to
`LayeredLayoutMetrics.ConnectorClearance`, so the clamp is a byte-for-byte no-op unless a caller
raises it; a decoration-aware caller (the state-transition view) sets it to the end-marker
along-line length plus corner radius plus clean-leg margin. The clamp uses
`Math.Max`, so it only ever pushes the jog outward: forward edges, long-edge middle sub-edges, and
every acyclic graph are byte-identical to the unclamped output. Because the stage runs in abstract
RIGHT-equivalent coordinates that `AxisTransform` maps to the requested direction, the guarantee is
orientation-agnostic (it holds for RIGHT and DOWN flows alike).

###### Inputs and Outputs

- Reads: `LayeredGraph.AugNodes`, `LayeredGraph.AugEdges`, `LayeredGraph.ColumnX`,
  `LayeredGraph.MaxColWidth`, `LayeredGraph.AugPortYSrc`, `LayeredGraph.AugPortYTgt`,
  `LayeredGraph.AcyclicReversed` (to recognize reversed edges' end-marker-bearing sub-edge).
- Writes: `LayeredGraph.AugBendPoints` (zero or two bend points per sub-edge).

###### Error Handling

A null graph throws `ArgumentNullException`.

###### Dependencies

- `LayeredGraph` (Layered) — the shared state read from and written to, including the
  `BackEdgeEntryApproach` parameter that sets the minimum entry stub for reversed edges.
- `PortDistributor` (Layered) — must run first to populate the port coordinates.
- `LayeredLayoutMetrics` (Layered) — supplies the edge spacing, clearance, straight tolerance, and
  the `ConnectorClearance` default for `BackEdgeEntryApproach`.
- `Point2D` (Layout) — the bend-point type.
