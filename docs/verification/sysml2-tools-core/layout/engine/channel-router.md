#### ChannelRouter Verification

##### Verification Approach

`ChannelRouter` is verified through unit tests in `ChannelRouterTests` that construct
explicit source/target anchors and obstacle rectangles and assert on the returned path.
Geometric helpers in the test class confirm that every segment is axis-aligned, that no
segment passes through an obstacle interior, and that segments keep the requested clearance.
No mocking is required; the engine is pure and deterministic.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services,
files, or configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `ChannelRouterTests` pass with zero failures across all three target frameworks.
- A route with no obstacles consists solely of axis-aligned segments.
- A route around an obstacle never enters the obstacle interior.
- A clean route keeps every segment at least the requested clearance from obstacles.
- A route with a given source or target side leaves or enters perpendicular to that side.
- A route that cannot avoid an obstacle reports `Crossed = true`; a clean route reports
  `Crossed = false`.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `Route_NoObstacles_ProducesOrthogonalPath` | Path endpoints correct; all segments orthogonal |
| `Route_AlignedEndpoints_ProducesStraightLine` | Aligned anchors yield a single straight run |
| `Route_ObstacleBetween_RoutesAround` | Path avoids the obstacle interior |
| `Route_MultipleObstacles_RemainsValid` | Valid orthogonal path among several obstacles |
| `Route_WithSourceSide_LeavesPerpendicular` | First segment perpendicular to the source side |
| `Route_WithTargetSide_EntersPerpendicular` | Last segment perpendicular to the target side |
| `RouteWithStatus_NoBlockingObstacle_ReportsNotCrossed` | Clean route reports not crossed |
| `RouteWithStatus_ObstacleBetween_RoutesAroundWithoutCrossing` | Routed around; not crossed |
| `RouteWithStatus_CleanRoute_KeepsClearanceFromObstacles` | Segments respect clearance |
| `RouteWithStatus_TargetEnclosedByObstacle_ReportsCrossed` | Enclosed target reports crossed |
| `RouteWithStatus_HighwayBand_PrefersBandedDetour` | Route prefers a cheaper cost band |
