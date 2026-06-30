#### StateTransitionViewLayoutStrategy

##### Purpose

`StateTransitionViewLayoutStrategy` implements `ILayoutStrategy` to produce a State Transition
View diagram. It renders state usages as rounded boxes placed top-to-bottom by the layered layout
pipeline, an initial pseudo-state marker entering the first declared state, and transitions as
orthogonal arrows annotated with their guard conditions.

##### Data Model

`StateTransitionViewLayoutStrategy` has no instance state; all input arrives through the
`BuildLayout` parameters. Layout constants (`MinStateWidth`, `CharWidthFactor`, `LabelCharWidthFactor`,
`InitialMarkerSize`, `InitialMarkerGap`, `AnchorSpread`) are declared as `private const double` fields. Two
private records carry intermediate data: `StateItem` (a state with its computed box size) and
`TransitionItem` (a resolved transition between two state indices with an optional guard).

##### Key Methods

###### `BuildLayout(ViewContext context, RenderOptions options)`

Entry point. Selects the root state definition via `FindRoot`, collects its states, resolves its
transitions, places the state boxes, adds the initial marker and the transition edges, and
assembles the tree. Returns a minimal 200×100 empty `LayoutTree` when no root or no states are
found.

###### `FindRoot(workspace)` and `CollectStates(root, theme)`

`FindRoot` chooses the non-standard-library definition with the most transitions. `CollectStates`
gathers the declared `state` usages first (preserving declaration order so the first declared
state becomes the initial state), then adds any additional state named only by a transition
endpoint, building a name → index lookup.

###### `ResolveTransitions(root, index)`

Maps each transition's source and target — by their last `::`-separated name segment — to state
indices, carrying the optional guard.

###### Placement and routing

State boxes are positioned by the `LayeredLayoutPipeline` built with `LayoutDirection.Down` and the
default stage sequence: each state becomes a node and each non-self transition a directed edge, so the
machine reads top-to-bottom (a transition leaves its source on the SOUTH face and enters its target on
the NORTH face). The pipeline's cycle-breaking stage makes the (cyclic) transition graph acyclic, so
the strategy does not need its own back-edge handling. The `LayeredGraph` is built with a
decoration-aware `BackEdgeEntryApproach` equal to the open-chevron end marker's along-line length
(`NotationMetrics.AlongLineLength(EndMarkerStyle.OpenChevron)`) plus `Theme.LineCornerRadius` plus
`Theme.CleanLegMargin` (for the Light theme, 10 + 4 + 1 = 15), so a reversed transition's final
straight approach is long enough that the renderer's rounded corner never intrudes into the end
marker. After the run the placed coordinates are read
back from `AugX`/`AugY`, normalized so the content starts at a margin offset (reserving room at the top
for the initial marker), and the canvas is sized to the full content extent — including the routed
transition polylines, which can bulge beyond the box columns, and the actual rendered extent of each
guard label. Because guard labels are drawn centred on their segment midpoints (using the same
`ConnectorLabelPlacer` the renderers use), only the part of a label that genuinely overhangs the
content widens the canvas: labels sitting on interior vertical segments add little or nothing, so the
canvas no longer reserves a full guard-label width of empty margin on the right.

`AddInitialMarker` places a filled-circle badge above the first state with a straight arrow into it.
`AddTransitions` maps each transition to the orthogonal polyline the pipeline routed for it. Because
the cycle-breaking stage drops self-loops, de-duplicates identical directed pairs, and reverses back
edges, `LayeredGraph.Waypoints` is not 1:1 with the input transitions; a lookup keyed by the routed
`(source, target)` pair recovers each polyline, and a transition whose routed edge was reversed reuses
that polyline in reverse so its open chevron end marker lands on the true target. Successive
transitions sharing
one routed corridor (parallel guards, or a forward/back-edge pair) are spread laterally so their anchor
points and guard labels do not coincide. Each transition is emitted with an open chevron end marker
(`EndMarkerStyle.OpenChevron`, drawn open in both renderers) at the
target state, matching SysML v2 state transition notation, and labelled with its bracketed guard. A
self-transition is drawn as a small loop above its state, also terminated by an open chevron end
marker. The method returns the number of transitions
whose polyline crosses a non-endpoint state box.

##### Error Handling

Null `context` or `options` arguments throw `ArgumentNullException`. The absence of an eligible
state definition or of states is not an error: the method returns the minimal empty canvas.
Transitions whose polyline crosses an unrelated box are still drawn and counted as crossings, which
are surfaced through `LayoutWarnings`.

##### Dependencies

- `ILayoutStrategy`, `ViewContext`, `RenderOptions`, `Theme` (Rendering subsystem) — the strategy contract and inputs.
- `LayeredLayoutPipeline`, `LayeredGraph`, `LayerNode`, `LayerEdge`, `LayoutDirection`, `HierarchyHandling`,
  `BoxMetrics` (Layout Engine subsystem) — top-to-bottom placement and orthogonal routing.
- `StdlibFilter` (Rendering Internal subsystem) — standard-library exclusion.
- `SysmlWorkspace`, `SysmlDefinitionNode`, `SysmlFeatureNode`, `SysmlTransitionNode` (Semantic subsystem) — model input.
- `LayoutWarnings` (Layout Internal subsystem) — crossing-warning construction.
- The `LayoutTree`, `LayoutBox`, `LayoutBadge`, and `LayoutLine` data types (Layout subsystem).

##### Callers

The Rendering subsystem selects `StateTransitionViewLayoutStrategy` when rendering a State
Transition View. No other unit calls it directly.
