##### LongEdgeJoiner Verification

###### Verification Approach

`LongEdgeJoiner` is verified through unit tests in `LongEdgeJoinerTests` that run the full routing
pipeline and assert on the assembled `Waypoints`. One test confirms one polyline per original edge
for a single short edge; another confirms that a long edge's polyline begins and ends at the right
boxes and that its point count equals the concatenation of its sub-edge bend points plus the two
endpoints. The stage is pure and deterministic, so no mocking is required.

###### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

###### Acceptance Criteria

- All `LongEdgeJoinerTests` pass with zero failures across all three target frameworks.
- One waypoint polyline is produced per original edge.
- A long edge's polyline endpoints sit at the source right face and the target left face.
- A long edge's point count equals two endpoints plus its sub-edges' bend points.

###### Test Scenarios

| Test | Assertion |
| --- | --- |
| `LongEdgeJoiner_Apply_SingleEdge_ProducesWaypointsPerOriginalEdge` | Two-point polyline for a single short edge |
| `LongEdgeJoiner_Apply_LongEdge_ConcatenatesSubEdgeBendPoints` | Endpoints and point count match the concatenation |
