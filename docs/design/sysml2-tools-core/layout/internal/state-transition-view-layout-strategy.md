#### StateTransitionViewLayoutStrategy

##### Purpose

`StateTransitionViewLayoutStrategy` implements `ILayoutStrategy` to produce a State Transition
View diagram. It renders the whole state definition inside one frame headed `«state def» Name`,
with state usages as rounded boxes placed top-to-bottom through `LayeredPlacement`, an initial
pseudo-state marker entering the explicitly declared initial state (and none when the model
declares no initial transition), a final (done) pseudo-state badge, and transitions as orthogonal
arrows labeled `trigger [guard] / effect`. A state that declares `entry`, `do`, or `exit` actions
lists them in compartments.

##### Data Model

`StateTransitionViewLayoutStrategy` has no instance state; all input arrives through the
`BuildLayout` parameters. Layout constants (`MinStateWidth`, `CharWidthFactor`,
`InitialMarkerSize`, `FinalMarkerSize`, `InitialMarkerGap`, `AnchorSpread`, `FrameInset`) and the
reserved name `DoneName` are declared as `private const` fields. Two private records carry
intermediate data: `StateItem` (a state with its computed box size, name line, entry/do/exit
compartments, and an `IsFinal` flag) and `TransitionItem` (a resolved transition between two state
indices with its optional `trigger [guard] / effect` label).

##### Key Methods

###### `BuildLayout(ViewContext context, RenderOptions options)`

Entry point. Resolves the view's `expose` scope via `ExposeScopeResolver.ResolveExposedScope`,
selects the root state definition via `FindRoot(workspace, scope)`, collects its states via
`CollectStates(root, theme, scope)`, resolves its transitions, places the state boxes, adds the
initial marker (only when `FindInitialTargetIndex` finds an explicit initial transition) and the
transition edges, encloses all of that content in a single frame `LayoutBox`, and assembles the
tree. Returns a minimal 200×100 empty `LayoutTree` when no root or no states are found.

*State definition frame.* The returned `LayoutTree` holds exactly one top-level node: a
`LayoutBox` at `(FrameInset, FrameInset)` — a 2 px inset, not the origin — with `Label` = the root
definition's name, `Keyword` = the definition's keyword (`"state def"`), `Depth` 0, and
`BoxShape.Rectangle`, whose `Children` are the state boxes, the initial badge and arrow, and the
transition lines. The SysML v2 graphical notation draws a state definition as a box headed by its
keyword and name that contains its states and transitions (SysML v2 formal/2025-09-03, clause 8.2.3),
so the frame tells the reader which state machine the diagram depicts. The frame's 1 px outline is
centered on its geometric edge, so a frame at the canvas edge would be half clipped; the canvas is
therefore the frame plus `FrameInset` on every side, which keeps the whole outline visible. The
children keep the absolute canvas coordinates they were built with — the content offsets
already include `FrameInset` — so the layout needs no coordinate translation. (The
`InterconnectionViewLayoutStrategy` root box still sits at the origin; that is a known,
separately tracked difference.) The frame is at least as wide as its own title (the
longer of the name and the `«state def»` keyword line, plus padding) so a long definition name is
never clipped. The top of the content area reserves the frame's title band
(`BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true)`) plus the margin — and,
only when an initial marker is drawn, the marker's size and gap — so the states start below the
title band.

###### `FindRoot(workspace, scope)` and `CollectStates(root, theme, scope)`

`FindRoot` chooses the non-standard-library definition with the most transitions, restricted —
when a scope is resolved — to candidates for which `ExposeScopeResolver.IsRootRelevantToScope`
returns `true`. When multiple candidates are relevant to a non-null scope (possible because a
nested definition and its ancestor can both be relevant), the most specific (deepest/longest
qualified name) relevant candidate is preferred via `ExposeScopeResolver.IsMoreSpecificCandidate`,
with the transition-count tie-break used only to break ties among equally specific candidates;
this ordering does not apply when `scope` is `null`. `CollectStates` gathers the declared `state`
usages first (preserving declaration order), excluding — when a scope is resolved — any declared state
feature whose qualified name fails `ExposeScopeResolver.IsInSubjectScope`; it then adds any
additional state named only by a transition endpoint, **except** when that endpoint is a
transition *source* whose name is a pseudostate name (`"start"`/`"done"`) or matches a declared
entry-action feature's name — such a source is never itself a state and must never be rendered as
a box (see "Pseudostate/Entry-Action Source Exclusion" below); building a name → index lookup, and
returning the set of excluded pseudostate/entry-action source names alongside it for
`FindInitialTargetIndex` to use.

###### `ResolveTransitions(root, index, states)`

Maps each transition's source and target — by their last `::`-separated name segment — to state
indices and builds its label with `BuildTransitionLabel`. Because pseudostate/entry-action
transition sources are never added to `index` by `CollectStates`, a transition whose source is such a
name is silently dropped here exactly like any other unresolvable endpoint. A transition whose source
resolves to the final badge is also dropped: nothing leaves a final state.

###### Transition label

`BuildTransitionLabel` produces the specification's `trigger [guard] / effect` label (graphical
BNF, clause 8.2.3, `transition-label`): the trigger text (the text after `accept`), then the guard in
brackets, then `/` and the effect (the text after `do`, without a leading `action` keyword). Each
part is omitted when the transition does not declare it, so a guard-only transition keeps the label
`[guard]`; runs of whitespace collapse to one space; the label is null when none of the three exists.
Self-transitions carry the same label.

###### State box content

`BuildStateItem` builds each declared state's name line with `GeneralViewLayoutStrategy.FormatNameLine`
(`name : Type [multiplicity]` for a typed usage) and its compartments with `BuildActionCompartments`:
one `LayoutCompartment` per sub-action kind present, in the fixed order `entry`, `do`, `exit`, titled
with the keyword (centered, italic) and listing each action's declaration text without a leading
`action` keyword (the name when there is no text; the empty form `entry;` adds no row). The box is
sized by `ComputeStateSize`: wide enough for the name line and the longest compartment title or row,
and tall enough for the title area plus each compartment, using the same height arithmetic as the
General view (`ComputeCompartmentHeight` is deliberately repeated rather than shared, so the two
strategies stay independent units).

###### Final state

A transition whose target is the reserved name `done` (the target of `then done;`) ends in a single
shared final badge, however many transitions reach it. `CollectStates` registers it as a `StateItem`
with `IsFinal` set (an 18 px node in the layered graph) and `BuildLayout` draws it as a
`BadgeShape.Bullseye` badge instead of a box. A declared state usage that is itself named `done` is
drawn as an ordinary box.

###### Limitations

Composite (nested) states are not drawn: a state with nested states renders as an ordinary box and its
nested states are ignored (tracked as the open "Nested state regions" item in the roadmap, which needs
nested layout scopes). Fork, join, decision, merge, and terminate nodes cannot occur in a state body:
the SysML v2 textual grammar does not admit them there, so the view has nothing to draw.

###### `FindInitialTargetIndex(root, pseudoSourceNames, index)`

Scans the root's transitions for the first whose source name is in `pseudoSourceNames`
(`"start"`/`"done"`, or a declared entry-action feature's name), and returns the resolved
target's state index, or `null` when no such transition exists (or its target does not resolve to
a known state). `BuildLayout` draws the initial marker at this index when it is non-null, and
draws **no** initial marker when it is `null` — the former fallback that guessed the
first-declared state was removed because it invented an initial state the model never declared;
the SysML v2 notation draws the initial marker only for an explicit initial transition (`first
start then X;` or an entry-action-sourced transition). When no marker is drawn, no top band is
reserved for it either.

###### Placement and routing

State boxes are positioned by calling `LayeredPlacement.Place` with a top-to-bottom flow
direction: each state becomes a sized node and each non-self transition a directed edge, so the
machine reads top-to-bottom. `LayeredPlacement` delegates to the off-the-shelf
`DemaConsulting.Rendering.Layout` layered algorithm, which returns placed rectangles for the states
and routed orthogonal polylines for the transitions, each already oriented source-to-target because
the algorithm reverses back edges internally. The strategy passes `EndMarkerStyle.OpenChevron` as
the placement's `edgeTargetEnd`, so the engine sizes every edge's final approach — including a back
edge's — to the open-chevron marker and the arrowhead enters on a clean straight run.
The placed coordinates
are normalized so the content starts at a margin offset below the frame's title band
(reserving room for the initial marker only when one is drawn), and the frame and canvas are sized
to the full content extent, including routed transition polylines that can bulge beyond the box
columns and the actual
rendered extent of each transition label. Transition labels are placed by the renderers with the
obstacle-aware `ConnectorLabelPlacer.Place(lines, fontSize, boxes, markerScale)` overload from
`DemaConsulting.Rendering.Abstractions` (the state boxes are the obstacles, so a label is moved clear
of a box corner). `LabelRightExtent` calls the same overload with the same obstacles and uses
`EstimateLabelWidth` for the text width, so the frame tracks where the renderers really draw each
label; only the part of a label that genuinely overhangs the content widens the canvas.

`AddInitialMarker` places a filled-circle badge above the initial state with a straight, solid
arrow into it that ends in an open chevron (`EndMarkerStyle.OpenChevron`) — the specification's
open arrowhead for initial-state and succession-style transitions (graphical BNF, p268) — rather
than a filled arrowhead.
`AddTransitions` maps each transition to the corresponding orthogonal polyline returned by
`LayeredPlacement`, preserving input-edge order and source-to-target orientation. Successive
transitions sharing one routed corridor are spread laterally so their anchor points and guard labels
do not coincide. Each transition is emitted with an open chevron end marker
(`EndMarkerStyle.OpenChevron`, drawn open in both renderers) at the target state, matching SysML v2
state transition notation, and labeled with its `trigger [guard] / effect` text. A self-transition is
drawn as a small loop above its state, also terminated by an open chevron end marker. The method returns the
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
synthesize a spurious box labeled `"start"` (or the entry action's name) purely because a
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
- The `LayoutTree`, `LayoutBox`, `LayoutBadge`, `LayoutCompartment`, and `LayoutLine` data types
  (`DemaConsulting.Rendering`).
- `GeneralViewLayoutStrategy.FormatNameLine` (Layout Internal subsystem) — the shared name-line formatter.

##### Callers

The Rendering subsystem selects `StateTransitionViewLayoutStrategy` when rendering a State
Transition View. No other unit calls it directly.
