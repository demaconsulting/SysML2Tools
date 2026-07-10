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

`LayeredPlacement` is a static class with no instance state. It exposes nested immutable records:

- `PlacedLayout` — the result of `Place`, with:
  - `Rects` — the placed box rectangles (`Rect`), one per input node, in input-node order.
  - `EdgePolylines` — the routed orthogonal connector polylines (each a list of `Point2D`), one per
    input edge, in input-edge order, already oriented source-to-target.
  - `Width`, `Height` — the overall content extent in logical pixels.
- `PlacedPortLayout` — the result of `PlaceWithPorts`, with the same `Rects`/`EdgePolylines`/
  `Width`/`Height` shape as `PlacedLayout`, plus:
  - `EdgePorts` — the placed source/target ports (`(LayoutPort? Source, LayoutPort? Target)`), one
    pair per input edge, in input-edge order; an element is `null` when the corresponding
    `PortEdge.SourcePort`/`TargetPort` was itself `null` (that endpoint attached to the plain node
    instead of a named port).
- `EdgePortRef` — requests that an edge endpoint attach through a named port rather than to its
  node as a whole, carrying an optional `Label` rendered as that port's `LayoutGraphPort.ExternalLabel`.
- `PortEdge` — a directed edge (`From`, `To`) with an optional `SourcePort`/`TargetPort`
  `EdgePortRef` at either endpoint; a `null` ref means that endpoint attaches directly to the node.

`Place`'s `nodes` parameter is a plain `(double Width, double Height)` tuple list.
`PlaceWithPorts`'s `nodes` parameter is instead `(double Width, double Height, bool HasLabel, bool
HasKeyword)`: the two extra flags carry no SysML semantics of their own — they exist solely to tell
`PlaceWithPorts` whether the caller's node will render a title, so it can set the created
`LayoutGraphNode.Label`/`.Keyword` to a non-null sentinel and activate the engine's automatic
title-vs-side-port reservation for that node (see step 2 below).

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

###### `PlaceWithPorts(nodes, edges, direction)`

A second, additive entry point for callers that need an edge to attach through a specific, named
connection point on a node's boundary — for example so a caller can label the exact port a
connection uses, and let the layout engine itself resolve that port's side, spacing, and any
resulting box growth, instead of computing box heights or port positions by hand. Each input node
also carries `HasLabel`/`HasKeyword` flags stating whether it will render a title, so the engine's
automatic title-vs-side-port reservation activates for it and no port lands across its own title
band. `Place` itself is completely unmodified; `PlaceWithPorts` does not call it and does not share
its code path beyond the identical `LayoutGraph`/`LayoutGraphNode` construction pattern.

1. Validates that `nodes` and `edges` are non-null.
2. Builds a `LayoutGraph`, adding one `LayoutGraphNode` per input node exactly as `Place` does, then
   — a step `Place` does not perform — sets that node's `LayoutGraphNode.Label`/`.Keyword` to a
   non-null sentinel (`string.Empty`) whenever the input node's `HasLabel`/`HasKeyword` is `true`.
   The engine's layered algorithm reserves a title band above a node's ports purely based on whether
   `Label`/`Keyword` are non-null (never their text), so this sentinel is the minimal signal needed
   to keep ports clear of a titled box's own header row.
3. For each input `PortEdge`, resolves its `ILayoutConnectable` source and target: when
   `SourcePort`/`TargetPort` is non-null, calls `graphNodes[from].Ports.AddPort("{edgeIndex}-a")` (or
   `"{edgeIndex}-b"` for the target), sets the new `LayoutGraphPort.ExternalLabel` to the requested
   `EdgePortRef.Label`, and uses that port as the endpoint; otherwise uses the plain node. The
   `"{edgeIndex}-a"`/`"{edgeIndex}-b"` naming scheme is unique per owning node because the edge index
   is unique across the whole input, so multiple connections into the same node never collide. Each
   created `LayoutGraphPort` reference is remembered (per edge, per end) for the correlation step
   below. Calls `graph.AddEdge(edgeIndex, source, target)` with the resolved endpoints.
4. Sets `CoreOptions.Direction` to the requested `direction` and unconditionally sets
   `CoreOptions.MergeParallelEdges` to `false` — unlike `Place`'s optional parameter, this method has
   no parameter for it, since every current caller needs parallel-edge preservation. Calls
   `LayoutEngine.Layout(graph)`.
5. Reads the tree's `LayoutBox` nodes into `Rects` and `LayoutLine` nodes into `EdgePolylines`
   exactly as `Place` does.
6. Builds `EdgePorts[e]` by scanning the tree's `LayoutPort` nodes for the one whose
   `LayoutPort.SourcePort` is reference-equal (`ReferenceEquals`) to the `LayoutGraphPort` created
   for edge `e`'s source (respectively target) in step 3; `null` when no port was requested for that
   end. `LayoutPort.SourcePort` exists specifically so a caller can recover, by reference identity,
   which graph port produced a given placed anchor — its own XML documentation states this is its
   intended purpose, since `ExternalLabel` is frequently `null` and so cannot itself serve as an
   identity key.
7. Returns a `PlacedPortLayout` carrying the rectangles, polylines, correlated ports, and the tree's
   overall `Width` and `Height`.

##### Error Handling

`Place` and `PlaceWithPorts` each throw `ArgumentNullException` when their `nodes` or `edges`
parameter is null. All other behavior is delegated to the off-the-shelf layout engine; the helper
adds no additional validation.

##### Dependencies

- `DemaConsulting.Rendering` (OTS) — the layout intermediate representation (`LayoutGraph`,
  `LayoutGraphNode`, `LayoutGraphPort`, `LayoutGraphPortCollection`, `ILayoutConnectable`,
  `LayoutTree`, `LayoutBox`, `LayoutLine`, `LayoutPort`), the geometric value types (`Rect`,
  `Point2D`), and the layout option system (`CoreOptions.Direction`, `CoreOptions.MergeParallelEdges`,
  `IPropertyHolder.Set`).
- `DemaConsulting.Rendering.Layout` (OTS) — the `LayoutEngine.Layout(LayoutGraph)` facade that
  resolves and runs the appropriate bundled algorithm (the bundled `layered` algorithm, for the
  flat graphs built here) to perform the actual ELK-style layered placement and orthogonal
  connector routing.
- `System.Globalization.CultureInfo` — invariant-culture formatting of the node and edge keys.

##### Callers

The view layout strategies that arrange nodes with the layered algorithm call either `Place` or
`PlaceWithPorts`: `InterconnectionViewLayoutStrategy` calls `PlaceWithPorts` (with a left-to-right
direction), so every connection endpoint attaches through a named, labeled port and the engine
itself resolves port sides, spacing, and any resulting box growth, while distinct parallel SysML
connections between the same two parts still render as separate independently-routed connectors
(parallel-edge merging is unconditionally disabled by `PlaceWithPorts`). `ActionFlowViewLayoutStrategy`
and `StateTransitionViewLayoutStrategy` call `Place` (with a top-to-bottom direction, leaving
`mergeParallelEdges` at its default `true`, unchanged from before this parameter existed).
`GeneralViewLayoutStrategy` builds and places its own `LayoutGraph` directly (via
`HierarchicalLayoutAlgorithm`, since its graph is nested with package-folder containers) rather than
going through this helper.
