#### StateTransitionViewLayoutStrategy Verification

##### Verification Approach

`StateTransitionViewLayoutStrategy` is verified through unit tests in
`StateTransitionViewLayoutStrategyTests` that construct a synthetic `SysmlWorkspace` containing a
state definition with states and transitions, invoke `BuildLayout`, and assert on the returned
`LayoutTree`. Assertions count the state boxes, confirm the initial-state badge, check guard
labels on the transition lines, and compare transition endpoint waypoints to confirm distinct
anchors. No mocking is required; the strategy depends only on the in-memory model, the geometric
engines, and the theme.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `StateTransitionViewLayoutStrategyTests` pass with zero failures across all three target frameworks.
- A state definition yields one state box per state, an initial-state badge, and guard-labelled
  transition lines.
- A state named only by a transition is still rendered as a box.
- An outgoing and an incoming transition on the same edge use distinct anchor points.
- Each transition edge carries an open arrowhead at the target state.
- An empty workspace yields a canvas with no nodes.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `StateTransitionView_BuildLayout_StatesAndTransitions_ProducesBoxesBadgeAndLines` | State boxes, badge, guard line |
| `StateTransitionView_BuildLayout_UndeclaredStateInTransition_IsCreated` | Transition-only target rendered as a box |
| `StateTransitionView_BuildLayout_EmptyWorkspace_ReturnsMinimalCanvas` | Canvas with no nodes |
| `StateTransitionView_BuildLayout_InAndOutOnSameEdge_UseDistinctAnchors` | In/out transitions use distinct anchors |
| `StateTransitionView_BuildLayout_TransitionEdge_HasOpenArrowhead` | Open arrowhead at target state |
