#### ActionFlowViewLayoutStrategy Verification

##### Verification Approach

`ActionFlowViewLayoutStrategy` is verified through unit tests in
`ActionFlowViewLayoutStrategyTests` that construct a synthetic `SysmlWorkspace` containing an
action definition with actions and successions, invoke `BuildLayout`, and assert on the returned
`LayoutTree`. Assertions count the action boxes, confirm the start (filled-circle) and done
(bullseye) markers and the flow lines, and compare action box `Y` coordinates to confirm
top-to-bottom ordering. No mocking is required; the strategy depends only on the in-memory model,
the geometric engines, and the theme.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `ActionFlowViewLayoutStrategyTests` pass with zero failures across all three target frameworks.
- An action definition yields one box per action, a start marker, a done marker, and flow lines.
- A succession's target action is positioned below its source action.
- Each succession flow edge is a dashed line with an open arrowhead at the target action.
- An empty workspace yields a canvas with no nodes.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `ActionFlowView_BuildLayout_ActionsAndSuccessions_ProducesBoxesMarkersAndFlows` | Action boxes, markers, and flows |
| `ActionFlowView_BuildLayout_Successions_FlowTopToBottom` | The source action sits above its successor |
| `ActionFlowView_BuildLayout_EmptyWorkspace_ReturnsMinimalCanvas` | Canvas with no nodes |
| `ActionFlowView_BuildLayout_SuccessionEdge_IsDashedWithOpenArrowhead` | Dashed line with open arrowhead |
