##### OrthogonalRouter Verification

###### Verification Approach

`OrthogonalRouter` is verified through unit tests in `OrthogonalRouterTests` that run the stages up
to and including routing and assert on the produced bend points. One test confirms that a single,
already-aligned edge produces no bend points; another confirms the structural contract that every
sub-edge's bend list is either empty or a two-point vertical segment. A separate
`OrthogonalRouterBackEdgeTests` suite verifies the reversed (back) edge entry-approach guarantee,
which is now driven by the `LayeredGraph.BackEdgeEntryApproach` parameter (the magic
`ReversedEdgeApproach` constant was deleted). The suite verifies that at the default approach
(equal to `LayeredLayoutMetrics.ConnectorClearance`) the clamp is a provable no-op so a cyclic
graph stays byte-identical to the reference engine; that raising the approach pushes each reversed
edge's marker-side stub outward; that every reversed edge's entry approach still clears
`ConnectorClearance` at the default; that forward edges in a cyclic graph stay byte-identical; that
an acyclic graph is unchanged (no reversed edge, so the clamp is a no-op); and that a
decoration-aware approach derived as the state-transition view derives it (longest end-marker
along-line length plus corner radius plus clean-leg margin) leaves a final straight leg at least as
long as the open-chevron end marker. The stage is pure and deterministic, so no mocking is
required.

###### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

###### Acceptance Criteria

- All `OrthogonalRouterTests` and `OrthogonalRouterBackEdgeTests` pass with zero failures across all
  three target frameworks.
- An aligned single edge produces no bend points.
- Every sub-edge bend list is empty or a two-point vertical segment.
- Every reversed edge that bends has a final straight approach of at least
  `LayeredGraph.BackEdgeEntryApproach` (which defaults to `ConnectorClearance`).
- Forward and acyclic edge geometry is byte-identical to the reference engine (the clamp is a no-op
  for them).

###### Test Scenarios

| Test | Assertion |
| --- | --- |
| `OrthogonalRouter_Apply_StraightEdge_ProducesNoBendPoints` | A single aligned edge has no bend points |
| `OrthogonalRouter_Apply_EveryBendListIsEmptyOrVerticalSegment` | Each bend list is empty or two points sharing an X |
| `OrthogonalRouter_DefaultApproach_IsByteIdenticalToLegacy` | Default approach is byte-identical to the reference |
| `OrthogonalRouter_CustomApproach_PushesEntryStubOutward` | A larger approach pushes a reversed stub outward |
| `OrthogonalRouter_ReversedEdge_DefaultApproachClearsClearance` | Reversed stub clears the default clearance |
| `OrthogonalRouter_ForwardEdges_GeometryUnchanged` | Forward edges in a cyclic graph match the reference engine |
| `OrthogonalRouter_AcyclicGraph_NoApproachChange` | An acyclic graph has no reversed edges and is byte-identical |
| `OrthogonalRouter_DecorationAwareApproach_ClearsMarkerAlongLength` | Clean leg at least the open-chevron length |
