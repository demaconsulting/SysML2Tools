#### StateTransitionViewLayoutStrategy

##### Purpose

`StateTransitionViewLayoutStrategy` implements `ILayoutStrategy` to produce a State Transition
View diagram. It renders state usages as rounded boxes placed by the force-directed engine, an
initial pseudo-state marker entering the first declared state, and transitions as orthogonal
arrows annotated with their guard conditions.

##### Data Model

`StateTransitionViewLayoutStrategy` has no instance state; all input arrives through the
`BuildLayout` parameters. Layout constants (`MinStateWidth`, `CharWidthFactor`, `StateSpacing`,
`TransitionClearance`, `InitialMarkerSize`) are declared as `private const double` fields. Two
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

State boxes are positioned by `ForceDirectedEngine.Place`, using the non-self transitions as
springs at `StateSpacing`. `AddInitialMarker` places a filled-circle badge above the first state
with a straight arrow into it. `AddTransitions` routes each transition with
`ChannelRouter.RouteWithStatus`, keeping `TransitionClearance` from unrelated boxes, labelling
each line with its bracketed guard. Each transition is emitted with an open arrowhead at the
target state, matching SysML v2 state transition notation. Each transition end attaches to the box side facing the other
state; when several transitions share a side, their endpoints are distributed along that side and
ordered by counterpart position to reduce crossings, and runs of consecutive same-direction
endpoints are collapsed into shared anchor slots so that incoming and outgoing transitions on one
edge never coincide. A self-transition is drawn as a small loop above its state. The method
returns the number of transitions that had to cross a box.

##### Error Handling

Null `context` or `options` arguments throw `ArgumentNullException`. The absence of an eligible
state definition or of states is not an error: the method returns the minimal empty canvas.
Transitions that cannot be routed cleanly are still drawn and counted as crossings, which are
surfaced through `LayoutWarnings`.

##### Dependencies

- `ILayoutStrategy`, `ViewContext`, `RenderOptions`, `Theme` (Rendering subsystem) — the strategy contract and inputs.
- `ForceDirectedEngine`, `ChannelRouter`, `BoxMetrics` (Layout Engine subsystem) — placement and routing.
- `StdlibFilter` (Rendering Internal subsystem) — standard-library exclusion.
- `SysmlWorkspace`, `SysmlDefinitionNode`, `SysmlFeatureNode`, `SysmlTransitionNode` (Semantic subsystem) — model input.
- `LayoutWarnings` (Layout Internal subsystem) — crossing-warning construction.
- The `LayoutTree`, `LayoutBox`, `LayoutBadge`, and `LayoutLine` data types (Layout subsystem).

##### Callers

The Rendering subsystem selects `StateTransitionViewLayoutStrategy` when rendering a State
Transition View. No other unit calls it directly.
