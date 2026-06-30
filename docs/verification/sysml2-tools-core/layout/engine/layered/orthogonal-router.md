##### OrthogonalRouter Verification

###### Verification Approach

`OrthogonalRouter` is verified through unit tests in `OrthogonalRouterTests` that run the stages up
to and including routing and assert on the produced bend points. One test confirms that a single,
already-aligned edge produces no bend points; another confirms the structural contract that every
sub-edge's bend list is either empty or a two-point vertical segment. The stage is pure and
deterministic, so no mocking is required.

###### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

###### Acceptance Criteria

- All `OrthogonalRouterTests` pass with zero failures across all three target frameworks.
- An aligned single edge produces no bend points.
- Every sub-edge bend list is empty or a two-point vertical segment.

###### Test Scenarios

| Test | Assertion |
| --- | --- |
| `OrthogonalRouter_Apply_StraightEdge_ProducesNoBendPoints` | A single aligned edge has no bend points |
| `OrthogonalRouter_Apply_EveryBendListIsEmptyOrVerticalSegment` | Each bend list is empty or two points sharing an X |
