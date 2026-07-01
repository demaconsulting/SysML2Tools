##### PortDistributor

###### Purpose

`PortDistributor` is the sixth pipeline stage. It distributes connector ports evenly along each box
face and records the source-side and target-side port Y coordinate for every augmented sub-edge.

###### Responsibilities

The stage groups sub-edges by their source node and by their target node. For each real node it
sorts the attached sub-edges by the opposite endpoint's center Y (then by edge index for stability)
and spreads their ports evenly along the relevant face — outgoing ports along the right face, incoming
ports along the left face — inset by a fixed clearance from the top and bottom edges, and clamped to
stay within the face. A single port on a face is placed at the face center. Dummy nodes pass their
wire straight through at their own Y. The result is two arrays, one source port Y and one target port
Y per sub-edge.

###### Inputs and Outputs

- Reads: `LayeredGraph.Nodes`, `LayeredGraph.AugNodes`, `LayeredGraph.AugEdges`, `LayeredGraph.AugY`.
- Writes: `LayeredGraph.AugPortYSrc`, `LayeredGraph.AugPortYTgt`.

###### Error Handling

A null graph throws `ArgumentNullException`.

###### Dependencies

- `LayeredGraph` (Layered) — the shared state read from and written to.
- `BrandesKopfPlacer` (Layered) — must run first to populate the node Y coordinates.
- `LayeredLayoutMetrics` (Layered) — supplies the connector clearance.
