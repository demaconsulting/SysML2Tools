#### LayeredLayoutEngine

##### Purpose

`LayeredLayoutEngine` arranges a directed graph into a top-to-bottom layered flow. It assigns each
node to a horizontal layer, orders the nodes within each layer to reduce edge crossings, and gives
each node absolute coordinates so the diagram reads as a directed flow from the top down. The
result is a region size, one rectangle per input node, and the layer index of each node.

##### Data Model

`LayeredLayoutEngine` is a static class with no instance state. Inputs are a list of `LayeredNode`
records (each a `Width` and `Height`), a list of `LayeredEdge` records (each a directed `From`/`To`
index pair), a `layerGap`, a `nodeGap`, and a `padding`. The result is a `LayeredResult` record
carrying the region `Width`, `Height`, the ordered list of placed `PackedRect` rectangles (one per
node in input order), and the `Layers` list giving each node's assigned layer index.

##### Key Methods

###### `Place(nodes, edges, layerGap, nodeGap, padding)`

Computes the placement. The algorithm is a simplified Sugiyama pipeline:

1. **Degenerate case.** An empty node list returns a region of `2 * padding` on each axis with no
   rectangles and no layers.
2. **Cycle breaking.** A depth-first traversal classifies any edge that points back to a node still
   on the recursion stack as a back edge and reverses it. Self-loops and duplicate edges are
   dropped. This produces an acyclic edge set so that layering terminates even when the input
   contains feedback loops.
3. **Layer assignment.** Over the acyclic edge set, each node is assigned the layer equal to its
   longest path from any source, computed by a topological sweep. This guarantees every non-reversed
   edge runs from a strictly smaller layer to a larger one.
4. **Crossing reduction.** Nodes within each layer are reordered by repeated barycenter sweeps
   (alternating downward and upward) that place each node near the average position of its
   neighbors in the adjacent layer, reducing edge crossings while keeping the order stable for
   nodes with no neighbors.
5. **Coordinate assignment.** Layers are stacked vertically using `layerGap` and the tallest node
   in each layer. Within a layer, nodes are given x-coordinates by an alignment relaxation that
   pulls each node toward the average centre of its neighbors while enforcing the minimum
   `nodeGap`, by averaging an order-preserving left-to-right and right-to-left placement (both of
   which respect the gap, so their average does too). This straightens the flow into a spine
   without letting same-layer nodes overlap. The arrangement is finally translated so the left-most
   node edge sits at `padding`, and the region width and height are computed from the extent plus
   padding.

##### Error Handling

Null `nodes` or `edges` arguments throw `ArgumentNullException`. All other inputs are handled
without throwing: cycles are broken so layering always terminates, and empty input returns a
well-formed empty result.

##### Dependencies

- `PackedRect` (Layout subsystem) — the placed-rectangle value type returned in the result.

##### Callers

View layout strategies that render directed graphs with an inherent flow direction, such as
activity and state diagrams, where a layered top-to-bottom arrangement is the expected reading
order.
