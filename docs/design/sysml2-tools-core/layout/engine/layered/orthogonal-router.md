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

###### Inputs and Outputs

- Reads: `LayeredGraph.AugNodes`, `LayeredGraph.AugEdges`, `LayeredGraph.ColumnX`,
  `LayeredGraph.MaxColWidth`, `LayeredGraph.AugPortYSrc`, `LayeredGraph.AugPortYTgt`.
- Writes: `LayeredGraph.AugBendPoints` (zero or two bend points per sub-edge).

###### Error Handling

A null graph throws `ArgumentNullException`.

###### Dependencies

- `LayeredGraph` (Layered) — the shared state read from and written to.
- `PortDistributor` (Layered) — must run first to populate the port coordinates.
- `LayeredLayoutMetrics` (Layered) — supplies the edge spacing, clearance, and straight tolerance.
- `Point2D` (Layout) — the bend-point type.
