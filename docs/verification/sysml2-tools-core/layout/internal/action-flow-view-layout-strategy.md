#### ActionFlowViewLayoutStrategy Verification

##### Verification Approach

`ActionFlowViewLayoutStrategy` is verified through unit tests in
`ActionFlowViewLayoutStrategyTests` that construct a synthetic `SysmlWorkspace` containing an
action definition with actions and successions, invoke `BuildLayout`, and assert on the returned
`LayoutTree`. Assertions count the action boxes, confirm the start (filled-circle) and done
(bullseye) markers and the flow lines, compare action box `Y` coordinates to confirm
top-to-bottom ordering, verify that succession polylines are orthogonal and that action boxes do
not overlap, and exercise branch-and-join and cyclic flows. No mocking is required; the strategy
depends only on the in-memory model, the layered layout pipeline, and the theme.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `ActionFlowViewLayoutStrategyTests` pass with zero failures across all three target frameworks.
- An action definition yields one box per action, a start marker, a done marker, and flow lines.
- A succession's target action is positioned below its source action.
- Each succession flow edge is a dashed line with an open end marker at the target action.
- A forward chain of successions flows top-to-bottom with orthogonal succession polylines and no
  overlapping action boxes.
- A branch-and-join flow renders every action, attaches the start marker only to source actions and
  the done marker only to sink actions, and a cyclic flow still emits each succession with an open
  marker at its true target.
- An empty workspace yields a canvas with no nodes.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `ActionFlowView_BuildLayout_ActionsAndSuccessions_ProducesBoxesMarkersAndFlows` | Action boxes, markers, and flows |
| `ActionFlowView_BuildLayout_Successions_FlowTopToBottom` | The source action sits above its successor |
| `ActionFlowView_BuildLayout_EmptyWorkspace_ReturnsMinimalCanvas` | Canvas with no nodes |
| `ActionFlowView_BuildLayout_SuccessionEdge_IsDashedWithOpenArrowhead` | Dashed line with open end marker |
| `ActionFlowView_BuildLayout_ForwardChain_FlowsTopToBottomOrthogonally` | Top-to-bottom orthogonal flow |
| `ActionFlowView_BuildLayout_NoOverlap` | Action boxes do not overlap |
| `ActionFlowView_BuildLayout_BranchAndJoin` | Branch/join renders all boxes, markers, and successions |
| `ActionFlowView_BuildLayout_Cycle_IsBroken` | Cyclic flow successions keep open markers at true targets |
