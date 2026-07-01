##### LongEdgeJoiner

###### Purpose

`LongEdgeJoiner` is the eighth pipeline stage. It assembles the final per-original-edge orthogonal
waypoints by concatenating the bend points of each sub-edge, following ELK's long-edge joining phase.

###### Responsibilities

The stage groups the augmented sub-edges by the original edge they belong to and sorts each group
into source-to-target layer order. For each original edge it builds a polyline that starts at the
source node's right face (at the source port Y of the first sub-edge), appends the bend points of
every sub-edge in order, and ends at the target node's left face (at the target port Y of the last
sub-edge). An original edge with no surviving sub-edge yields an empty polyline. The result is one
waypoint list per original edge.

###### Inputs and Outputs

- Reads: `LayeredGraph.AugNodes`, `LayeredGraph.AugEdges`, `LayeredGraph.AugX`,
  `LayeredGraph.AugPortYSrc`, `LayeredGraph.AugPortYTgt`, `LayeredGraph.AugBendPoints`,
  `LayeredGraph.Acyclic` (count).
- Writes: `LayeredGraph.Waypoints` (one polyline per original edge).

###### Error Handling

A null graph throws `ArgumentNullException`.

###### Dependencies

- `LayeredGraph` (Layered) — the shared state read from and written to.
- `OrthogonalRouter` (Layered) — must run first to populate the sub-edge bend points.
- `Point2D` (Layout) — the waypoint type.
