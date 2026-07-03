#### StateTransitionViewLayoutStrategy

##### Purpose

`StateTransitionViewLayoutStrategy` implements `ILayoutStrategy` to produce a State Transition
View diagram. It renders state usages as rounded boxes placed top-to-bottom through
`LayeredPlacement`, an initial pseudo-state marker entering the first declared state, and transitions
as orthogonal arrows annotated with their guard conditions.

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

State boxes are positioned by calling `LayeredPlacement.Place` with a top-to-bottom flow
direction: each state becomes a sized node and each non-self transition a directed edge, so the
machine reads top-to-bottom. `LayeredPlacement` delegates to the off-the-shelf
`DemaConsulting.Rendering.Layout` layered algorithm, which returns placed rectangles for the states
and routed orthogonal polylines for the transitions, each already oriented source-to-target because
the algorithm reverses back edges internally. (The previous internal engine reserved a custom
straight approach for the open-chevron marker on reversed edges; that knob has no public equivalent,
so a reversed transition's final approach may differ by about a pixel — a purely cosmetic change.)
The placed coordinates
are normalized so the content starts at a margin offset
(reserving room at the top for the initial marker), and the canvas is sized to the full content
extent, including routed transition polylines that can bulge beyond the box columns and the actual
rendered extent of each guard label. Because guard labels are drawn centred on their segment
midpoints with `ConnectorLabelPlacer` from `DemaConsulting.Rendering.Abstractions`, only the part of a
label that genuinely overhangs the content widens the canvas: labels sitting on interior vertical
segments add little or nothing.

`AddInitialMarker` places a filled-circle badge above the first state with a straight arrow into it.
`AddTransitions` maps each transition to the corresponding orthogonal polyline returned by
`LayeredPlacement`, preserving input-edge order and source-to-target orientation. Successive
transitions sharing one routed corridor are spread laterally so their anchor points and guard labels
do not coincide. Each transition is emitted with an open chevron end marker
(`EndMarkerStyle.OpenChevron`, drawn open in both renderers) at the target state, matching SysML v2
state transition notation, and labelled with its bracketed guard. A self-transition is drawn as a
small loop above its state, also terminated by an open chevron end marker. The method returns the
number of transitions whose polyline crosses a non-endpoint state box.

##### Error Handling

Null `context` or `options` arguments throw `ArgumentNullException`. The absence of an eligible
state definition or of states is not an error: the method returns the minimal empty canvas.
Transitions whose polyline crosses an unrelated box are still drawn and counted as crossings, which
are surfaced through `LayoutWarnings`.

##### Dependencies

- `ILayoutStrategy` and `ViewContext` (Rendering subsystem) — the strategy contract and view input.
- `RenderOptions`, `Theme`, `BoxMetrics`, `NotationMetrics`, and `ConnectorLabelPlacer`
  (`DemaConsulting.Rendering.Abstractions`) — render options, sizing, and label metrics.
- `LayeredPlacement` (Layout Internal subsystem) — top-to-bottom placement and orthogonal routing
  through `DemaConsulting.Rendering.Layout`.
- `StdlibFilter` (Rendering Internal subsystem) — standard-library exclusion.
- `SysmlWorkspace`, `SysmlDefinitionNode`, `SysmlFeatureNode`, `SysmlTransitionNode` (Semantic subsystem) — model input.
- `LayoutWarnings` (Layout Internal subsystem) — crossing-warning construction.
- The `LayoutTree`, `LayoutBox`, `LayoutBadge`, and `LayoutLine` data types
  (`DemaConsulting.Rendering`).

##### Callers

The Rendering subsystem selects `StateTransitionViewLayoutStrategy` when rendering a State
Transition View. No other unit calls it directly.
