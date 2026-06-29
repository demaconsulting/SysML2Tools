#### InterconnectionLayoutEngine Verification

##### Verification Approach

`InterconnectionLayoutEngine` is verified through unit tests in `InterconnectionLayoutEngineTests`
that construct explicit node lists and edge lists with known topology, invoke `Place`, and assert
on the returned `LayerResult`. Layer monotonicity checks verify the longest-path guarantee; waypoint
count checks verify dummy-node insertion; overlap checks verify the non-overlapping constraint. The
engine is pure and deterministic, so all assertions are stable across repeated runs. No mocking is
required.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `InterconnectionLayoutEngineTests` pass with zero failures across all three target frameworks.
- A linear chain produces strictly monotonically increasing layer indices along the chain direction.
- A span-1 edge produces exactly four waypoints in Z-path order.
- A long edge (span > 1) produces more than four waypoints, proving dummy nodes are in use.
- The returned `Rects` count always equals the input node count (no dummy rectangles leaked).
- No two rectangles from the Workstation topology (seven nodes) overlap.
- The Workstation topology places psu in layer 0, board in layer 1, and the leaf nodes in layer 2+.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `Place_LinearChain_MonotonicLayerAssignment` | Each node's layer > its predecessor's layer along A→B→C chain |
| `Place_SingleEdge_ProducesFourWaypointZPath` | Single span-1 edge yields exactly 4 waypoints |
| `Place_LongEdge_RectCountEqualsInputNodeCount` | Two-node, span-2 graph: Rects.Count == 2 |
| `Place_LongEdge_WaypointsExceedFour` | Span-2 edge yields more than 4 waypoints |
| `Place_WorkstationTopology_CorrectLayersAndNoOverlap` | 7-node Workstation graph: psu=L0, board=L1, memory in highest layer; no rect overlap |
