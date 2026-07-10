#### LayeredPlacement Verification

##### Verification Approach

`LayeredPlacement` is a thin adapter over the off-the-shelf `DemaConsulting.Rendering.Layout`
layered algorithm and has no direct unit test of its own. It is verified indirectly through the
view layout strategy tests that depend on it: `ActionFlowViewLayoutStrategyTests`,
`StateTransitionViewLayoutStrategyTests`, `InterconnectionViewLayoutStrategyTests`, and
`GeneralViewLayoutStrategyTests`. Those strategies pass sized nodes and directed edges to
`LayeredPlacement.Place` and build their `LayoutTree` from the returned rectangles and polylines,
so a passing strategy layout is evidence that the adapter placed nodes and routed edges correctly.
No mocking is used; the real layered algorithm runs on every test.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation and the referenced
`DemaConsulting.Rendering` packages.

##### Acceptance Criteria

- The strategy tests that exercise `LayeredPlacement` pass with zero failures across all target
  frameworks.
- Placement returns one non-overlapping rectangle per input node in input-node order.
- Routing returns one polyline per input edge in input-edge order, oriented source-to-target even
  when the input contains cycles.
- The requested flow direction is honored so a forward chain reads top-to-bottom.
- The additive `mergeParallelEdges` parameter (default `true`, unchanged for
  `ActionFlowViewLayoutStrategy`/`StateTransitionViewLayoutStrategy`) is exercised transitively via
  `InterconnectionViewLayoutStrategyTests`, whose parallel-connection tests assert distinct routed
  waypoints per parallel edge when `mergeParallelEdges: false` is requested. No dedicated
  `LayeredPlacementTests` file exists — consistent with the established pattern of covering this
  adapter only through the strategies that call it.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `ActionFlowView_BuildLayout_ActionsAndSuccessions_ProducesBoxesMarkersAndFlows` | Boxes and flows produced |
| `StateTransitionView_BuildLayout_StatesAndTransitions_ProducesBoxesBadgeAndLines` | State boxes and lines produced |
| `InterconnectionView_BuildLayout_PartsAndConnections_ProducesBoxesPortsAndLines` | Part boxes and lines produced |
| `InterconnectionView_BuildLayout_TwoConnectionsSamePair_ProducesTwoConnectorsWithoutException` | Distinct waypoints |
| `InterconnectionView_BuildLayout_ThreeParallelConnections_ProducesThreeDistinctConnectors` | Distinct routes |
| `InterconnectionView_BuildLayout_PartBoxes_DoNotOverlap` | Placed rectangles do not overlap |
| `ActionFlowView_BuildLayout_NoOverlap` | Placed action boxes do not overlap |
| `ActionFlowView_BuildLayout_SuccessionEdge_IsDashedWithOpenArrowhead` | Polyline oriented to the target |
| `StateTransitionView_BuildLayout_TransitionEdge_HasOpenArrowhead` | Polyline oriented source-to-target |
| `ActionFlowView_BuildLayout_Cycle_IsBroken` | Cyclic input placed, back edges reversed |
| `ActionFlowView_BuildLayout_ForwardChain_FlowsTopToBottomOrthogonally` | Top-to-bottom direction honored |
| `StateTransitionView_BuildLayout_ForwardChain_FlowsTopToBottomOrthogonally` | Top-to-bottom direction honored |
| `ActionFlowView_BuildLayout_Successions_FlowTopToBottom` | Successor placed below predecessor |
