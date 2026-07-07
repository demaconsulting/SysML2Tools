#### StateTransitionViewLayoutStrategy Verification

##### Verification Approach

`StateTransitionViewLayoutStrategy` is verified through unit tests in
`StateTransitionViewLayoutStrategyTests` that construct a synthetic `SysmlWorkspace` containing a
state definition with states and transitions, invoke `BuildLayout`, and assert on the returned
`LayoutTree`. Assertions count the state boxes, confirm the initial-state badge, check guard
labels on the transition lines, compare transition endpoint waypoints to confirm distinct anchors,
and verify the top-to-bottom flow (a forward chain's target boxes sit below their sources) with
orthogonal transition polylines. No mocking is required; the strategy depends only on the in-memory
model, `LayeredPlacement`, and render options.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `StateTransitionViewLayoutStrategyTests` pass with zero failures across all three target frameworks.
- A state definition yields one state box per state, an initial-state badge, and guard-labelled
  transition lines.
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

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `StateTransitionView_BuildLayout_StatesAndTransitions_ProducesBoxesBadgeAndLines` | State boxes, badge, guard line |
| `StateTransitionView_BuildLayout_UndeclaredStateInTransition_IsCreated` | Transition-only target rendered as a box |
| `StateTransitionView_BuildLayout_EmptyWorkspace_ReturnsMinimalCanvas` | Canvas with no nodes |
| `StateTransitionView_BuildLayout_InAndOutOnSameEdge_UseDistinctAnchors` | In/out transitions use distinct anchors |
| `StateTransitionView_BuildLayout_TransitionEdge_HasOpenArrowhead` | Open chevron end marker at target state |
| `StateTransitionView_BuildLayout_ForwardChain_FlowsTopToBottomOrthogonally` | Top-to-bottom orthogonal flow |
| `StateTransitionView_BuildLayout_NullViewNode_PicksHeuristicRootUnchanged` | Null `ViewNode` renders unchanged |
| `StateTransitionView_BuildLayout_ExposeNonHeuristicRoot_SelectsExposedRoot` | Non-heuristic root is selected |
| `StateTransitionView_BuildLayout_ExposeInnerChildOfNonHeuristicRoot_SelectsItsRoot` | Inner state selects its root |
| `StateTransitionView_BuildLayout_ExposeUnrelatedDefinition_NoRootSelected_ReturnsMinimalCanvas` | Unrelated def |
| `StateTransitionView_BuildLayout_ExposeSingleState_DropsIsolatedOutOfScopeState` | Isolated state dropped |
| `StateTransitionView_BuildLayout_ExposedUsage_ResolvesThroughTypingToRoot` | Usage resolves via `Typing` to root |
| `StateTransitionView_BuildLayout_ExposeInnerStateOfNestedDefinition_SelectsNestedDefinitionNotAncestor` | Nested |
