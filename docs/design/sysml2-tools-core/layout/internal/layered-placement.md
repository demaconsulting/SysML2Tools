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
  - `Lines` — the connector lines exactly as the engine emitted them, one per input edge in input-edge
    order, carrying the end marker, line style, midpoint label, and end labels declared on the `PortEdge`.
  - `Ports` — every placed port glyph the engine emitted. A port shared by several edges yields one
    placed port per edge end (an engine limitation), all with the same `LayoutPort.SourcePort`; only the
    first keeps its label.
- `EdgePortRef` — requests that an edge endpoint attach through a named port rather than to its
  node as a whole, carrying an optional `Label` rendered as that port's `LayoutGraphPort.ExternalLabel`, an
  optional `PortAppearance` (direction arrow, glyph, outline style, and label placement), and an optional
  `Key`: edges that name the same key on the same node share one `LayoutGraphPort`.
- `PortEdge` — a directed edge (`From`, `To`) with an optional `SourcePort`/`TargetPort`
  `EdgePortRef` at either endpoint (a `null` ref means that endpoint attaches directly to the node), and the
  notation of the connector: `TargetEnd` decoration, `LineStyle`, `SourceEndLabel`/`TargetEndLabel` (role name
  and multiplicity), and `MidpointLabel`.

`Place`'s `nodes` parameter is a plain `(double Width, double Height)` tuple list.
`PlaceWithPorts`'s `nodes` parameter is instead `(double Width, double Height, bool HasLabel, bool
HasKeyword)`: the two extra flags carry no SysML semantics of their own — they exist solely to tell
`PlaceWithPorts` whether the caller's node will render a title, so it can set the created
`LayoutGraphNode.Label`/`.Keyword` to a non-null sentinel and activate the engine's automatic
title-vs-side-port reservation for that node (see step 2 below).

`Rect` and `Point2D` are geometric value types provided by the `DemaConsulting.Rendering` package.

##### Key Methods

###### `Place(nodes, edges, direction, mergeParallelEdges = true, edgeTargetEnd = None)`

Places sized nodes and directed edges by delegating to the public layout engine facade:

1. Validates that `nodes` and `edges` are non-null.
2. Builds a `LayoutGraph`, adding one `LayoutGraphNode` per input node (sized from the supplied
   width and height, keyed by its ordinal index) and one edge per input edge (keyed by its ordinal
   index) between the referenced graph nodes, setting each edge's `TargetEnd` to the
   `edgeTargetEnd` argument (default `EndMarkerStyle.None`, which leaves the engine's behavior
   unchanged). The layered algorithm reads the end decoration to size the final approach of a back
   edge, so a caller that draws an arrowhead at the target passes the same style it puts on the drawn
   line (`ActionFlowViewLayoutStrategy` and `StateTransitionViewLayoutStrategy` pass
   `EndMarkerStyle.OpenChevron`).
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
3. For each input `PortEdge`, resolves its `ILayoutConnectable` source and target through
   `ResolveEndpoint`: when `SourcePort`/`TargetPort` is non-null and carries a `Key` that the node already
   has a port for, that port is reused (a declared SysML port with several connectors is one port);
   otherwise it calls `graphNodes[from].Ports.AddPort(...)` (named `"k:{key}"`, or `"{edgeIndex}-a"`/
   `"{edgeIndex}-b"` for a request without a key, which is unique per owning node because the edge index is
   unique across the whole input), sets the new port's `ExternalLabel` and `Appearance` from the request, and
   uses that port as the endpoint; with no request it uses the plain node. Calls
   `graph.AddEdge(edgeIndex, source, target)` and sets the edge's `TargetEnd`, `LineStyle`, `SourceEndLabel`,
   `TargetEndLabel`, and `Label` (the midpoint label) from the `PortEdge`, so the layered algorithm reserves
   room for them.
4. Sets `CoreOptions.Direction` to the requested `direction` and unconditionally sets
   `CoreOptions.MergeParallelEdges` to `false` — unlike `Place`'s optional parameter, this method has
   no parameter for it, since every current caller needs parallel-edge preservation. Calls
   `LayoutEngine.Layout(graph)`.
5. Reads the tree's `LayoutBox` nodes into `Rects` and `LayoutLine` nodes into `EdgePolylines`
   exactly as `Place` does.
6. Collects every `LayoutPort` of the tree. The engine (0.3.0-beta.4) emits one placed port per edge end even
   for a shared graph port, each repeating the label, so the first placed port of each distinct
   `LayoutPort.SourcePort` (compared by reference) keeps its label and the others have `ExternalLabel` and
   `InternalLabel` cleared: a declared port is named once.
7. Returns a `PlacedPortLayout` carrying the rectangles, polylines, the engine's connector lines, the
   ports, and the tree's overall `Width` and `Height`.

###### `WidenForMidpointLabels(placed, edges, theme)`

A post-placement pass for callers that draw port labels outside the boxes and connector midpoint labels
(`InterconnectionViewLayoutStrategy`). The layered algorithm reserves room between two columns of boxes for
end labels and outward port labels, but not for a connector's midpoint label, which lands on the port labels
when the corridor is only just wide enough for them. The method returns `placed` itself when no line has a
midpoint label; otherwise it repeats, at most `MaxWideningPasses` times:

1. Resolves the labels of the current layout with `ConnectorLabelPlacer.ResolveLabels` (boxes rebuilt from
   `Rects`), the resolver the renderers use.
2. Finds the first labeled connector whose midpoint label penetrates a box or a port rectangle (glyph or
   outside label, `PortRects`) horizontally, treating labels within `LabelBackgroundPad` (4 px) vertically as
   touching because the renderers draw an opaque background taller than the text rectangle. A connector whose
   endpoints share a column cannot be separated and is skipped.
3. Shifts the right-hand endpoint's column — every box, port, and waypoint at or right of the leftmost box that
   overlaps the node's own horizontal extent — right by twice the penetration plus `WideningClearance` (6 px),
   and widens the layout by the same amount.

The loop ends when no label overlaps.

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
- `DemaConsulting.Rendering.Abstractions` (OTS) — `ConnectorLabelPlacer.ResolveLabels` (the label resolver the
  renderers use) and `Theme`, for `WidenForMidpointLabels`.
- `DemaConsulting.Rendering.Layout` (OTS) — the `LayoutEngine.Layout(LayoutGraph)` facade that
  resolves and runs the appropriate bundled algorithm (the bundled `layered` algorithm, for the
  flat graphs built here) to perform the actual ELK-style layered placement and orthogonal
  connector routing.
- `System.Globalization.CultureInfo` — invariant-culture formatting of the node and edge keys.

##### Callers

The view layout strategies that arrange nodes with the layered algorithm call either `Place` or
`PlaceWithPorts`: `InterconnectionViewLayoutStrategy` calls `PlaceWithPorts` (with a left-to-right
direction) and then `WidenForMidpointLabels`, so every connection endpoint that names a port attaches through
a named, labeled, shared port and the engine itself resolves port sides, spacing, and any resulting box
growth, while distinct parallel SysML
connections between the same two parts still render as separate independently-routed connectors
(parallel-edge merging is unconditionally disabled by `PlaceWithPorts`). `ActionFlowViewLayoutStrategy`
and `StateTransitionViewLayoutStrategy` call `Place` (with a top-to-bottom direction, leaving
`mergeParallelEdges` at its default `true`, unchanged from before this parameter existed).
`GeneralViewLayoutStrategy` builds and places its own `LayoutGraph` directly (via
`HierarchicalLayoutAlgorithm`, since its graph is nested with package-folder containers) rather than
going through this helper.
