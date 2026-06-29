#### LayeredPlacer

##### Purpose

`LayeredPlacer` places the nested part boxes of an Interconnection View in left-to-right layers
using a degree-based BFS algorithm and sizes each inter-layer corridor proportionally to the number
of edges passing through it. It replaces the earlier force-directed placement, highway routing, and
compress-and-quantize pipeline, producing deterministic, non-overlapping geometry suitable for
slot-based connector routing without any post-processing pass.

##### Data Model

`LayeredPlacer` is a static class with no instance state. Input is a `IReadOnlyList<LayerNode>`
(width and height per node), a `IReadOnlyList<LayerEdge>` (undirected node-index pairs), and four
optional numeric parameters: `nodeSpacing`, `minCorridorWidth`, `edgeSpacing`, and `clearance`. The
result is a `LayerResult` record carrying one `Rect` per node in input-index order, the bounding-
box totals, and a `NodeLayers` list of layer indices for the caller.

##### Key Methods

###### `Place(nodes, edges, nodeSpacing, minCorridorWidth, edgeSpacing, clearance)`

Six-step algorithm:

1. **Degree-based BFS layering.** An undirected adjacency list is built from the validated edges.
   Self-loops and out-of-range indices are silently ignored. The highest-degree node (lowest index
   on ties) seeds layer 0; BFS assigns each unvisited neighbour `parent_layer + 1`. Nodes
   unreachable from the seed (isolated nodes or disconnected components) receive layer 0.

2. **Barycentric ordering.** Within each layer beyond layer 0, nodes are sorted by the mean rank
   (position index within its own layer) of their connected neighbours. Layer 0 retains BFS arrival
   order. The rank table is updated after each layer is sorted so later layers benefit from earlier
   improvements. Node index serves as a stable tie-break.

3. **Corridor width computation.** For each gap between adjacent layers the number of edges whose
   endpoints straddle that gap is counted. The corridor width is
   `max(minCorridorWidth, crossings × edgeSpacing + 2 × clearance)`.

4. **X assignment.** Layer 0 starts at X = 0. Each subsequent column starts at the right edge of
   the previous column plus its corridor width: `x[L] = x[L-1] + maxWidth[L-1] + corridor[L-1]`.

5. **Y assignment.** The total content height of each column is the sum of node heights plus
   `(count - 1) × nodeSpacing` gaps. Each column is centred vertically relative to the tallest
   column so short columns align to the diagram midline rather than the top edge.

6. **Result assembly.** `TotalWidth` is the rightmost rect right edge plus `clearance`;
   `TotalHeight` is the lowest rect bottom edge plus `clearance`. The `Rects` list and `NodeLayers`
   list are both in input-index order.

##### Error Handling

Null `nodes` or `edges` arguments throw `ArgumentNullException`. An empty `nodes` list returns a
zero-size `LayerResult` with empty lists without performing any computation. Out-of-range edge
indices and self-loops are silently ignored.

##### Dependencies

- `Rect` (Layout Engine) — the geometric rectangle value type shared by all layout engines.

##### Callers

`InterconnectionViewLayoutStrategy.BuildLayout` calls `Place` to obtain the `LayerResult`, uses
`NodeLayers` to classify pairs as cross-layer or same-layer, and uses `Rects` and the bounding-box
totals to position part boxes and size the container.
