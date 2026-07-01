##### LayeredGraph

###### Purpose

`LayeredGraph` is the mutable shared state threaded through every stage of the layered pipeline. It
replaces the ad-hoc local variables that the monolithic interconnection engine passed between its
private phase methods, while preserving exactly the same intermediate values (and therefore the same
floating-point results).

###### Data Model

The constructor takes the input nodes (`IReadOnlyList<LayerNode>`), the directed input edges
(`IReadOnlyList<LayerEdge>`), and the requested `LayoutDirection`. It stores them on read-only
properties (`Nodes`, `Edges`, `Direction`) and records the real node count `N`. Every other field is
an initially empty, settable property owned by a later stage: `Acyclic` (cycle-broken edges),
`AcyclicReversed` (a flag per acyclic edge, in the same order, marking those produced by reversing a
back edge), `NodeLayers` (per-node layer index), `AugNodes` and `AugEdges` (the augmented graph after
long-edge splitting), `Groups` (augmented-node indices ordered by layer), `AugX`/`AugY` and `ColumnX`/
`MaxColWidth` (coordinates), `AugPortYSrc`/`AugPortYTgt` and `AugBendPoints` (routing), and
`Waypoints` (the assembled per-edge polylines). One settable tuning parameter,
`BackEdgeEntryApproach`, controls the minimum final straight approach the `OrthogonalRouter` gives a
reversed (back) edge before its end marker; it defaults to `LayeredLayoutMetrics.ConnectorClearance`,
which reproduces the original engine byte-for-byte, and a decoration-aware caller raises it to clear
the end-marker along-line length. (It replaces the deleted `ReversedEdgeApproach` magic constant.)
The file also declares the internal `AugNode` record
(width, height, layer, dummy flag) and the `AugEdge` record struct (source, target, original edge
index).

###### Error Handling

A null `nodes` or `edges` argument throws `ArgumentNullException`. The fields owned by later stages
start as empty collections so that reading them before their producing stage runs yields an empty
result rather than a null reference.

###### Dependencies

- `LayerNode`, `LayerEdge` (Layout Engine) — the geometric input records.
- `LayoutDirection` (Layered) — the requested flow direction.
- `Point2D` (Layout) — the point type used for bend points and waypoints.
