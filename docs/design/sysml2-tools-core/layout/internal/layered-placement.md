#### LayeredPlacement

##### Purpose

`LayeredPlacement` adapts the reusable "layered" layout algorithm bundled in the
`DemaConsulting.Rendering.Layout` off-the-shelf package to the SysML view layout strategies. Its
single responsibility is to accept plain sized nodes and directed edges from a strategy, run the
package's layered algorithm, and return the placed box rectangles together with the routed
connector polylines. It carries no SysML semantics: callers translate their model into sizes and
index pairs, and translate the returned geometry back onto their model by index.

##### Data Model

`LayeredPlacement` is a static class with no instance state. It exposes one nested immutable record:

- `PlacedLayout` — the result of a placement, with:
  - `Rects` — the placed box rectangles (`Rect`), one per input node, in input-node order.
  - `EdgePolylines` — the routed orthogonal connector polylines (each a list of `Point2D`), one per
    input edge, in input-edge order, already oriented source-to-target.
  - `Width`, `Height` — the overall content extent in logical pixels.

`Rect` and `Point2D` are geometric value types provided by the `DemaConsulting.Rendering` package.

##### Key Methods

###### `Place(nodes, edges, direction)`

Places sized nodes and directed edges with the bundled layered algorithm:

1. Validates that `nodes` and `edges` are non-null.
2. Builds a `LayoutGraph`, adding one `LayoutGraphNode` per input node (sized from the supplied
   width and height, keyed by its ordinal index) and one edge per input edge (keyed by its ordinal
   index) between the referenced graph nodes.
3. Sets the `CoreOptions.Direction` layout option to the requested `LayoutFlowDirection` and runs
   `LayeredLayoutAlgorithm.Apply`, obtaining a laid-out `LayoutTree`.
4. Reads the tree's `LayoutBox` nodes into `Rects` (in emitted order, which mirrors input-node
   order) and the tree's `LayoutLine` nodes into `EdgePolylines` (in emitted order, which mirrors
   input-edge order).
5. Returns a `PlacedLayout` carrying the rectangles, polylines, and the tree's overall `Width` and
   `Height`.

The algorithm emits exactly one placed box per input node and exactly one routed connector per
input edge, preserving the caller's ordering, and reverses back edges internally so every returned
polyline runs source-to-target.

##### Error Handling

`Place` throws `ArgumentNullException` when `nodes` or `edges` is null. All other behavior is
delegated to the off-the-shelf layered algorithm; the helper adds no additional validation.

##### Dependencies

- `DemaConsulting.Rendering` (OTS) — the layout intermediate representation (`LayoutGraph`,
  `LayoutGraphNode`, `LayoutTree`, `LayoutBox`, `LayoutLine`), the geometric value types (`Rect`,
  `Point2D`), and the layout option system (`LayoutOptions`, `CoreOptions.Direction`).
- `DemaConsulting.Rendering.Layout` (OTS) — the `LayeredLayoutAlgorithm` that performs the actual
  ELK-style layered placement and orthogonal connector routing.
- `System.Globalization.CultureInfo` — invariant-culture formatting of the node and edge keys.

##### Callers

The view layout strategies that arrange nodes with the layered algorithm call
`LayeredPlacement.Place`: `GeneralViewLayoutStrategy` and `InterconnectionViewLayoutStrategy` (with
a left-to-right direction), and `ActionFlowViewLayoutStrategy` and
`StateTransitionViewLayoutStrategy` (with a top-to-bottom direction).
