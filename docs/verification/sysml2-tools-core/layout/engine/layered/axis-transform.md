##### AxisTransform Verification

###### Verification Approach

`AxisTransform` is verified through unit tests in `AxisTransformTests`. One test runs the placement
pipeline with the left-to-right direction, snapshots the coordinates, runs the transform, and asserts
the coordinates are unchanged. A small two-node chain is then run through the full default pipeline
(so input-axis normalization is applied) once per direction, asserting the coordinate mapping (target
on the correct side of its source), the port faces (down: source SOUTH / target NORTH; and the
analogous faces for right, left, and up), and the orthogonality of the routed waypoints. The stage is
pure and deterministic, so no mocking is required.

###### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

###### Acceptance Criteria

- All `AxisTransformTests` pass with zero failures across all three target frameworks.
- The left-to-right transform leaves the placed coordinates unchanged.
- Each non-identity direction places later-layer targets on the correct side of their source with the
  correct exit/entry faces, and keeps every routed waypoint polyline orthogonal.

###### Test Scenarios

| Test | Assertion |
| --- | --- |
| `AxisTransform_Apply_RightDirection_LeavesCoordinatesUnchanged` | Coordinates unchanged by the identity transform |
| `AxisTransform_Apply_Right_PlacesTargetEastWithCorrectFaces` | Target placed east; source EAST / target WEST face |
| `AxisTransform_Apply_Down_PlacesTargetSouthWithCorrectFaces` | Target placed south; source SOUTH / target NORTH face |
| `AxisTransform_Apply_Left_PlacesTargetWestWithCorrectFaces` | Target placed west; source WEST / target EAST face |
| `AxisTransform_Apply_Up_PlacesTargetNorthWithCorrectFaces` | Target placed north; source NORTH / target SOUTH face |
| `AxisTransform_Apply_Right_ProducesOrthogonalWaypoints` | Routed waypoints stay axis-aligned (right) |
| `AxisTransform_Apply_Down_ProducesOrthogonalWaypoints` | Routed waypoints stay axis-aligned (down) |
| `AxisTransform_Apply_Left_ProducesOrthogonalWaypoints` | Routed waypoints stay axis-aligned (left) |
| `AxisTransform_Apply_Up_ProducesOrthogonalWaypoints` | Routed waypoints stay axis-aligned (up) |
