#### StateTransitionViewLayoutStrategy Verification

##### Verification Approach

`StateTransitionViewLayoutStrategy` is verified through unit tests in
`StateTransitionViewLayoutStrategyTests` that construct a synthetic `SysmlWorkspace` containing a
state definition with states and transitions, invoke `BuildLayout`, and assert on the returned
`LayoutTree`. Assertions count the state boxes, confirm the initial-state badge, check transition
labels on the lines, compare transition endpoint waypoints to confirm distinct anchors,
and verify the top-to-bottom flow (a forward chain's target boxes sit below their sources) with
orthogonal transition polylines. The notation tests check the entry/do/exit compartments, the
label format, the final badge, and the back-edge approach, and run the renderers' obstacle-aware
`ConnectorLabelPlacer` (with the theme and obstacles the renderers use, the frame included) over the
produced layout and over the Elevator and Heater gallery models. A label is inflated by the
renderers' background padding and must not cover a state box, another connector, or the first or
last marker length of its own connector; synthetic cases confirm that sibling labels on one narrow
face widen the state and that a state with no collision keeps its natural width. A gallery model
that cannot be found fails the test; it is never skipped. No mocking is required; the strategy
depends only on the in-memory model, `LayeredPlacement`, and render options.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, net10.0, and net11.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `StateTransitionViewLayoutStrategyTests` pass with zero failures across all four target frameworks.
- A state definition yields one state box per state, an initial-state badge (when the model
  declares an explicit initial transition), and guard-labeled transition lines.
- The whole diagram is enclosed in a single frame box, inset symmetrically from the canvas edge so
  its outline is not clipped, labeled with the state
  definition's name and keyword `state def`, whose children are the state boxes, initial marker,
  and transitions, with the state boxes positioned below the frame's title band.
- A state named only by a transition is still rendered as a box.
- An outgoing and an incoming transition on the same edge use distinct anchor points.
- Each transition edge carries an open chevron end marker at the target state.
- A forward chain of transitions flows top-to-bottom with orthogonal transition polylines.
- An empty workspace yields a canvas with no nodes.
- A `null` `ViewContext.ViewNode` selects the pre-scoping heuristic root and renders every state,
  unchanged from before this feature — the critical `--auto`/no-expose regression guard.
- A view whose resolved `Expose` edge names a definition other than the heuristic root selects
  that definition as the root instead.
- A view whose resolved `Expose` edge names an inner state of a non-heuristic-root definition
  selects that definition's own root.
- A view whose resolved `Expose` edge names a definition unrelated to any candidate root selects
  no root, producing the minimal empty canvas.
- A view whose resolved `Expose` edge names a single state drops a genuinely isolated
  out-of-scope state while still rendering any excluded state re-referenced by an in-scope
  transition.
- A view whose resolved `Expose` edge names a feature usage (not a definition) still resolves to
  the usage's type as the root, via the shared usage-to-type fallback.
- A view whose resolved `Expose` edge names an inner state of a definition genuinely nested
  inside another eligible root candidate selects the nested definition, not the ancestor, even
  though the ancestor has more transitions and would win the old pure-score tie-break.
- A transition whose source is a pseudostate/entry-action name (`"start"`, or a declared
  entry-action feature's name) is never rendered as its own state box.
- The initial-state marker's arrow lands on the resolved target of a pseudostate- or
  entry-action-sourced transition, not the first-declared state, when such a transition exists.
- When no pseudostate/entry-action-sourced transition exists, no initial-state marker is drawn —
  neither a badge nor an arrow, and no top band is reserved for one — rather than guessing the
  first-declared state.
- The initial-state marker's arrow is a solid line with an open chevron end marker (not a filled
  arrowhead).
- A state's `entry`, `do`, and `exit` actions appear in compartments titled with the keyword
  (centered, italic) and the box is sized to fit them; a state without them has none.
- A transition (including a self-transition) is labeled `trigger [guard] / effect`, omitting each
  part it does not declare.
- A transition into `done` ends in a single bullseye badge, never a state box.
- Every transition's final straight approach is at least as long as the open chevron marker, and
  the frame contains every label as the renderers place it clear of the state boxes.
- Every transition label, with its background padding, is clear of every state box, every other
  connector, and the end marker of its own connector (Elevator and Heater gallery models); two
  sibling labels that arrive on one narrow face widen that state, and a state with no collision
  keeps its natural width.
- The frame ends one margin below the lowest content.
- A missing gallery model fails its test rather than passing vacuously.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `StateTransitionView_BuildLayout_StatesAndTransitions_ProducesBoxesBadgeAndLines` | Boxes, badge, guard line |
| `StateTransitionView_BuildLayout_RootDefinition_IsEnclosedInStateDefFrame` | `«state def»` frame holds all content |
| `StateTransitionView_BuildLayout_UndeclaredStateInTransition_IsCreated` | Transition-only target rendered as a box |
| `StateTransitionView_BuildLayout_EmptyWorkspace_ReturnsMinimalCanvas` | Canvas with no nodes |
| `StateTransitionView_BuildLayout_InAndOutOnSameEdge_UseDistinctAnchors` | In/out transitions use distinct anchors |
| `StateTransitionView_BuildLayout_ParallelTransitions_EachUseTheirOwnEngineRoute` | Parallel transitions keep routes |
| `StateTransitionView_BuildLayout_TransitionEdge_HasOpenArrowhead` | Open chevron end marker at target state |
| `StateTransitionView_BuildLayout_ForwardChain_FlowsTopToBottomOrthogonally` | Top-to-bottom orthogonal flow |
| `StateTransitionView_BuildLayout_NullViewNode_PicksHeuristicRootUnchanged` | Null `ViewNode` renders unchanged |
| `StateTransitionView_BuildLayout_ExposeNonHeuristicRoot_SelectsExposedRoot` | Non-heuristic root is selected |
| `StateTransitionView_BuildLayout_ExposeInnerChildOfNonHeuristicRoot_SelectsItsRoot` | Inner state selects its root |
| `StateTransitionView_BuildLayout_ExposeUnrelatedDefinition_NoRootSelected_ReturnsMinimalCanvas` | Unrelated def |
| `StateTransitionView_BuildLayout_ExposeSingleState_DropsIsolatedOutOfScopeState` | Isolated state dropped |
| `StateTransitionView_BuildLayout_ExposedUsage_ResolvesThroughTypingToRoot` | Usage resolves via `Typing` to root |
| `StateTransitionView_BuildLayout_ExposeInnerStateOfNestedDefinition_SelectsNestedDefinitionNotAncestor` | Nested |
| `StateTransitionView_BuildLayout_PseudostateSourceTransition_MarksResolvedTargetNotFirstDeclared` | Resolved target |
| `StateTransitionView_BuildLayout_PseudostateSourceTransition_NoSpuriousBox` | No `"start"` box |
| `StateTransitionView_BuildLayout_EntryActionSourceTransition_NoSpuriousBoxUsesResolvedTarget` | No entry box |
| `StateTransitionView_BuildLayout_NoExplicitInitialTransition_DrawsNoInitialMarker` | No badge, arrow, or band |
| `StateTransitionView_BuildLayout_DoneSourcedTransition_DrawsNoInitialMarker` | `done` source is not initial |
| `StateTransitionView_BuildLayout_InitialMarkerArrow_IsSolidWithOpenArrowhead` | Solid arrow, open chevron head |
| `StateTransitionView_BuildLayout_EntryDoExit_ProducesTitledCompartments` | Titled compartments, in order |
| `StateTransitionView_BuildLayout_EntryDoExit_NoneDeclared_NoCompartments` | No compartments when none declared |
| `StateTransitionView_BuildLayout_EntryDoExit_WidensBoxToLongestRow` | Box wide and tall enough for its rows |
| `StateTransitionView_BuildLayout_TypedState_NameLineShowsTypeAndMultiplicity` | `name : Type [m]` name line |
| `StateTransitionView_BuildLayout_TransitionTriggerGuardEffect_LabelFormat` | `trigger [guard] / effect` label |
| `StateTransitionView_BuildLayout_SelfTransition_CarriesFullLabel` | Self-loop carries the full label |
| `StateTransitionView_BuildLayout_DoneTarget_RendersBullseyeNotStateBox` | `done` target is a bullseye badge |
| `StateTransitionView_BuildLayout_DoneTarget_MultipleSources_SingleFinalBadge` | One shared final badge |
| `StateTransitionView_BuildLayout_BackEdge_OpenChevronWithFlushApproach` | Approach at least the marker length |
| `StateTransitionView_BuildLayout_FrameWidth_AccountsForLongTransitionLabel` | Frame contains the label |
| `StateTransitionView_BuildLayout_LongLabel_OverhangingLeft_IsContainedByFrame` | Left overhang moves content right |
| `StateTransitionView_BuildLayout_ElevatorGalleryModel_LabelsDoNotOverlapStateBoxes` | Elevator labels clear of all |
| `StateTransitionView_BuildLayout_HeaterGalleryModel_LabelsAreClear` | Heater labels clear of all |
| `StateTransitionView_BuildLayout_SiblingLabelsOnSharedFace_AreWidenedApart` | Sibling labels stay clear |
| `StateTransitionView_BuildLayout_NoLabelCollision_KeepsNaturalWidth` | No collision, natural width |
| `StateTransitionView_BuildLayout_FrameBottom_IsOneMarginBelowContent` | Frame bottom one margin below |
| `StateTransitionView_BuildLayout_ElevatorGalleryModel_ShowsCompartmentsLabelsAndFinal` | Gallery notation |
| `LayeredPlacement_Place_EdgeTargetEnd_ForwardedToGraphEdges` | Edge end decoration reaches the engine |
| `LayeredPlacement_Place_DefaultEdgeTargetEnd_PreservesBehavior` | Default placement unchanged |
