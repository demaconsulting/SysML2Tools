#### StateTransitionViewLayoutStrategy

##### Purpose

`StateTransitionViewLayoutStrategy` implements `ILayoutStrategy` to produce a State Transition
View diagram. It renders state usages as rounded boxes placed top-to-bottom through
`LayeredPlacement`, an initial pseudo-state marker entering the semantically-correct initial
state (falling back to the first declared state), and transitions as orthogonal arrows annotated
with their guard conditions.

##### Data Model

`StateTransitionViewLayoutStrategy` has no instance state; all input arrives through the
`BuildLayout` parameters. Layout constants (`MinStateWidth`, `CharWidthFactor`, `LabelCharWidthFactor`,
`InitialMarkerSize`, `InitialMarkerGap`, `AnchorSpread`) are declared as `private const double` fields. Two
private records carry intermediate data: `StateItem` (a state with its computed box size) and
`TransitionItem` (a resolved transition between two state indices with an optional guard).

##### Key Methods

###### `BuildLayout(ViewContext context, RenderOptions options)`

Entry point. Resolves the view's `expose` scope via `ExposeScopeResolver.ResolveExposedScope`,
selects the root state definition via `FindRoot(workspace, scope)`, collects its states via
`CollectStates(root, theme, scope)`, resolves its transitions, places the state boxes, adds the
initial marker (targeting the semantically-resolved initial state when one is found, otherwise
the first-declared state) and the transition edges, and assembles the tree. Returns a minimal
200×100 empty `LayoutTree` when no root or no states are found.

###### `FindRoot(workspace, scope)` and `CollectStates(root, theme, scope)`

`FindRoot` chooses the non-standard-library definition with the most transitions, restricted —
when a scope is resolved — to candidates for which `ExposeScopeResolver.IsRootRelevantToScope`
returns `true`. When multiple candidates are relevant to a non-null scope (possible because a
nested definition and its ancestor can both be relevant), the most specific (deepest/longest
qualified name) relevant candidate is preferred via `ExposeScopeResolver.IsMoreSpecificCandidate`,
with the transition-count tie-break used only to break ties among equally specific candidates;
this ordering does not apply when `scope` is `null`. `CollectStates` gathers the declared `state`
usages first (preserving declaration order so the first declared state becomes the
fallback-heuristic initial state), excluding — when a scope is resolved — any declared state
feature whose qualified name fails `ExposeScopeResolver.IsInSubjectScope`; it then adds any
additional state named only by a transition endpoint, **except** when that endpoint is a
transition *source* whose name is a pseudostate name (`"start"`/`"done"`) or matches a declared
entry-action feature's name — such a source is never itself a state and must never be rendered as
a box (see "Pseudostate/Entry-Action Source Exclusion" below); building a name → index lookup, and
returning the set of excluded pseudostate/entry-action source names alongside it for
`FindInitialTargetIndex` to use.

###### `ResolveTransitions(root, index)`

Maps each transition's source and target — by their last `::`-separated name segment — to state
indices, carrying the optional guard. Because pseudostate/entry-action transition sources are
never added to `index` by `CollectStates`, a transition whose source is such a name is silently
dropped here exactly like any other unresolvable endpoint — no additional change was needed in
this method for the pseudostate exclusion to take effect.

###### `FindInitialTargetIndex(root, pseudoSourceNames, index)`

Scans the root's transitions for the first whose source name is in `pseudoSourceNames`
(`"start"`/`"done"`, or a declared entry-action feature's name), and returns the resolved
target's state index, or `null` when no such transition exists. `BuildLayout` uses this index in
preference to index 0 (the first-declared state) when placing the initial marker, falling back to
index 0 only when this returns `null` — preserving the pre-existing heuristic unchanged for every
model that declares no explicit initial-pseudostate/entry-action transition (including the
`03-elevator-state.sysml` gallery example prior to its own initial-transition addition).

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

##### Expose Scoping

Because this strategy renders exactly one selected root's states, scoping restricts **which root
is selected** and then narrows **which of that root's states are shown**, mirroring
`InterconnectionViewLayoutStrategy`'s approach. `FindRoot` only considers candidates
`ExposeScopeResolver.IsRootRelevantToScope` accepts, so exposing the current heuristic root
itself, an inner state of it, or a definition that itself contains the heuristic default all
correctly select a root, while exposing an unrelated definition yields no root and thus the
minimal empty canvas. When more than one candidate is relevant (a nested definition and an
ancestor definition can both be relevant to the same exposed subject),
`ExposeScopeResolver.IsMoreSpecificCandidate` prefers the most deeply nested candidate, so
exposing an inner state of a nested definition correctly selects that nested definition rather
than its ancestor, even when the ancestor has more transitions. `CollectStates` then narrows the
selected root's own **declared** state features to those within the resolved scope; however, any
declared-but-excluded state that is still referenced by an in-scope transition is transparently
re-added by the unconditional transition-endpoint pass, since that pass has no independent
qualified name to filter against — so expose-scoping only reliably drops a state that is genuinely
isolated (never referenced by any transition of the selected root). `ResolveTransitions`'s
existing name-lookup approach naturally omits any transition whose endpoint state was never added
— no new edge-side logic was required. A view with no `expose` statement (including the
synthesized `--auto` view, whose `ViewNode` is `null`) resolves no scope, so `FindRoot` considers
every candidate and `CollectStates` keeps every state, unchanged from the pre-scoping behavior.

##### Pseudostate/Entry-Action Source Exclusion

A transition's source can name a pseudostate (`start`/`done`) or an entry-action feature rather
than a genuine declared state — the OMG Annex A.7 idioms `first start then off;` and
`entry action initial; then off;` both produce a `SysmlTransitionNode` whose `Source` is such a
name. Neither name is ever separately declared as a `state` usage, so before this exclusion
existed, `CollectStates`'s "additional state referenced only by a transition endpoint" pass would
synthesize a spurious box labelled `"start"` (or the entry action's name) purely because a
transition referenced it — a state box for something that is not a state. `CollectStates` now
seeds a `pseudoSourceNames` set with the literal names `"start"` and `"done"` plus every declared
`entry`-keyword feature's `Name`, and skips adding a transition's source-side name to the state
list whenever it appears in that set; the target side of any transition is never affected by this
exclusion, since a transition's target is always a genuine state.

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
- `ExposeScopeResolver` (Layout Internal subsystem) — `ResolveExposedScope`,
  `IsRootRelevantToScope`, and `IsInSubjectScope` supply the shared `expose`-scoping used by
  `BuildLayout`, `FindRoot`, and `CollectStates`.
- `SysmlWorkspace`, `SysmlDefinitionNode`, `SysmlFeatureNode`, `SysmlTransitionNode` (Semantic subsystem) — model input.
- `LayoutWarnings` (Layout Internal subsystem) — crossing-warning construction.
- The `LayoutTree`, `LayoutBox`, `LayoutBadge`, and `LayoutLine` data types
  (`DemaConsulting.Rendering`).

##### Callers

The Rendering subsystem selects `StateTransitionViewLayoutStrategy` when rendering a State
Transition View. No other unit calls it directly.
