#### LayeredPlacement

##### Purpose

`LayeredPlacement` adapts the `DemaConsulting.Rendering.Layout` off-the-shelf package's public
layout engine to the SysML view layout strategies for flat, flow-chart-like diagrams. Its single
responsibility is to accept plain sized nodes and directed edges from a strategy, lay them out
through the `LayoutEngine.Layout(LayoutGraph)` facade, and return the placed box rectangles
together with the routed connector polylines. It carries no SysML semantics: callers translate
their model into sizes and index pairs, and translate the returned geometry back onto their model
by index.

##### Data Model

`LayeredPlacement` is a static class with no instance state. It exposes one nested immutable record:

- `PlacedLayout` — the result of a placement, with:
  - `Rects` — the placed box rectangles (`Rect`), one per input node, in input-node order.
  - `EdgePolylines` — the routed orthogonal connector polylines (each a list of `Point2D`), one per
    input edge, in input-edge order, already oriented source-to-target.
  - `Width`, `Height` — the overall content extent in logical pixels.

`Rect` and `Point2D` are geometric value types provided by the `DemaConsulting.Rendering` package.

##### Key Methods

###### `Place(nodes, edges, direction, mergeParallelEdges = true)`

Places sized nodes and directed edges by delegating to the public layout engine facade:

1. Validates that `nodes` and `edges` are non-null.
2. Builds a `LayoutGraph`, adding one `LayoutGraphNode` per input node (sized from the supplied
   width and height, keyed by its ordinal index) and one edge per input edge (keyed by its ordinal
   index) between the referenced graph nodes.
3. Sets the requested `LayoutFlowDirection` directly on the graph via
   `graph.Set(CoreOptions.Direction, direction)`. When `mergeParallelEdges` is `false`, also sets
   `graph.Set(CoreOptions.MergeParallelEdges, false)` on the graph — the additive optional
   parameter defaults to `true`, which skips this call entirely and keeps the method's original,
   unconditional behavior for every pre-existing call site
   (`ActionFlowViewLayoutStrategy`/`StateTransitionViewLayoutStrategy`) byte-for-byte identical.
   Calls `LayoutEngine.Layout(graph)`, obtaining a laid-out `LayoutTree`. Both settings must be set
   on the graph itself (not passed through a `LayoutOptions` instance) because the facade always
   seeds its internal cascade with an empty `LayoutOptions`, honoring only settings declared
   directly on the graph. The graph built here is always flat (no container nodes), so the facade's
   default `hierarchical` algorithm is guaranteed byte-for-byte identical to the bundled `layered`
   algorithm applied directly — using the public facade rather than instantiating
   `LayeredLayoutAlgorithm` directly costs nothing and keeps this helper aligned with the package's
   intended entry point.
4. Reads the tree's `LayoutBox` nodes into `Rects` (in emitted order, which mirrors input-node
   order) and the tree's `LayoutLine` nodes into `EdgePolylines` (in emitted order, which mirrors
   input-edge order).
5. Returns a `PlacedLayout` carrying the rectangles, polylines, and the tree's overall `Width` and
   `Height`.

The algorithm emits exactly one placed box per input node and exactly one routed connector per
input edge, preserving the caller's ordering, and reverses back edges internally so every returned
polyline runs source-to-target. When `mergeParallelEdges` is `false`, multiple edges between the
same pair of nodes are each still emitted as their own polyline (as they already were), but with
genuinely distinct routed waypoints instead of all sharing one route — see
`CoreOptions.MergeParallelEdges` in the companion package.

##### Error Handling

`Place` throws `ArgumentNullException` when `nodes` or `edges` is null. All other behavior is
delegated to the off-the-shelf layout engine; the helper adds no additional validation.

##### Dependencies

- `DemaConsulting.Rendering` (OTS) — the layout intermediate representation (`LayoutGraph`,
  `LayoutGraphNode`, `LayoutTree`, `LayoutBox`, `LayoutLine`), the geometric value types (`Rect`,
  `Point2D`), and the layout option system (`CoreOptions.Direction`, `CoreOptions.MergeParallelEdges`,
  `IPropertyHolder.Set`).
- `DemaConsulting.Rendering.Layout` (OTS) — the `LayoutEngine.Layout(LayoutGraph)` facade that
  resolves and runs the appropriate bundled algorithm (the bundled `layered` algorithm, for the
  flat graphs built here) to perform the actual ELK-style layered placement and orthogonal
  connector routing.
- `System.Globalization.CultureInfo` — invariant-culture formatting of the node and edge keys.

##### Callers

The view layout strategies that arrange nodes with the layered algorithm call
`LayeredPlacement.Place`: `InterconnectionViewLayoutStrategy` (with a left-to-right direction and
`mergeParallelEdges: false`, so distinct parallel SysML connections between the same two parts
render as separate independently-routed connectors), and `ActionFlowViewLayoutStrategy` and
`StateTransitionViewLayoutStrategy` (with a top-to-bottom direction, leaving `mergeParallelEdges`
at its default `true`, unchanged from before this parameter existed). `GeneralViewLayoutStrategy`
builds and places its own `LayoutGraph` directly (via `HierarchicalLayoutAlgorithm`, since its
graph is nested with package-folder containers) rather than going through this helper.
