#### InterconnectionLayoutEngine

##### Purpose

`InterconnectionLayoutEngine` places the nested part boxes of an Interconnection View and routes
all connector lines using a full Sugiyama-style pipeline, matching the ELK reference sandbox
output. It is now a thin **façade** that assembles and runs the reusable `LayeredLayoutPipeline`
(see the *Layout Engine Layered Subsystem*) with its default stage sequence, the **Right** layout
direction, and **flat** hierarchy handling. The public `Place` API and the `LayerResult` output are
unchanged — the façade produces byte-for-byte identical geometry to the previous monolithic
implementation, which is proven by a bit-exact equivalence test against a legacy oracle. The
pipeline replaced the earlier BFS-layered `LayeredPlacer`, eliminating same-layer connections and
connector clipping caused by incomplete layering and absent dummy-node support.

##### Data Model

`InterconnectionLayoutEngine` is a static class with no instance state. `Place` builds a
`LayeredGraph` from the inputs and runs it through a `LayeredLayoutPipeline` configured with the
default stages; the phases below describe the behavior realized by those stages. Input is a
`IReadOnlyList<LayerNode>` (width and height per node) and a `IReadOnlyList<LayerEdge>` (directed
edges following SysML endpoint-A → endpoint-B order). The result is a `LayerResult` record
carrying one `Rect` per real node in input-index order, the bounding-box totals, a `NodeLayers`
list of longest-path layer indices, and a `ConnectorWaypoints` list of complete orthogonal
waypoints per edge.

##### Key Methods

###### `Place(nodes, edges)`

Eight-phase algorithm:

1. **Cycle breaking.** DFS back-edge detection reverses any edges that form cycles, producing a
   directed acyclic graph. Self-loops and duplicate edges are dropped.

2. **Longest-path layering.** A topological pass assigns each node a layer equal to the length of
   the longest directed path from any source. Every edge goes from a strictly lower layer to a
   strictly higher layer — no same-layer connections are possible.

3. **Dummy-node insertion.** For each edge whose endpoint layers differ by more than one, zero-size
   placeholder nodes are inserted at each intermediate layer. This forces the routing path through
   the correct inter-layer corridors and prevents connector lines from clipping the boxes of
   intermediate nodes.

4. **Barycenter ordering.** Alternating left-to-right and right-to-left sweep passes sort nodes
   within each layer by the mean position of their neighbors in the adjacent layer, reducing visual
   edge crossings.

5. **Coordinate assignment.** Column left-edge X positions are derived from corridor widths, which
   scale with the number of augmented edges that cross each gap
   (`max(70, clearance + count × 16 + clearance)`). Within each column, real nodes and dummy nodes
   are stacked top-to-bottom with 30 px gaps; dummy nodes take zero vertical space.

6. **Port distribution.** Outgoing connections are distributed evenly along the right face of each
   real node, sorted by target center-Y; incoming connections are distributed along the left face,
   sorted by source center-Y. Dummy nodes route at their center Y.

7. **Slot assignment.** For each inter-layer corridor all crossing augmented edges are sorted by
   mean port Y and assigned a unique vertical slot X:
   `slotX[k] = corridorLeft + clearance + k × edgeSpacing`. Slot conflicts are impossible by
   construction.

8. **Waypoint stitching.** Each original edge is reconstructed from its augmented segment chain.
   Every segment contributes a Z-path `[srcPort, slotX, slotX, tgtPort]`; consecutive segments
   share the dummy node as a join point, producing a single orthogonal polyline per connection.

##### Error Handling

Null `nodes` or `edges` arguments throw `ArgumentNullException`. An empty `nodes` list returns a
minimal-size `LayerResult` with empty lists without performing any computation. Out-of-range edge
indices and self-loops are silently ignored.

##### Dependencies

- `LayeredLayoutPipeline` (Layout Engine Layered) — the reusable staged pipeline the façade
  assembles and runs to compute the placement and routing.
- `Rect` (Layout Engine) — the geometric rectangle value type shared by all layout engines.
- `Point2D` (Layout) — the immutable 2-D point type used for connector waypoints.

##### Callers

`InterconnectionViewLayoutStrategy.BuildLayout` calls `Place` to obtain the `LayerResult`, applies
the container offset to `Rects` and `ConnectorWaypoints`, and emits `LayoutPort` and `LayoutLine`
nodes directly from the pre-computed waypoints.
