##### AxisTransform Verification

###### Verification Approach

`AxisTransform` is verified through unit tests in `AxisTransformTests`. One test runs the placement
pipeline with the left-to-right direction, snapshots the coordinates, runs the transform, and asserts
the coordinates are unchanged; another constructs a graph with a non-right direction and asserts that
the transform throws. The stage is pure and deterministic, so no mocking is required.

###### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

###### Acceptance Criteria

- All `AxisTransformTests` pass with zero failures across all three target frameworks.
- The left-to-right transform leaves the placed coordinates unchanged.
- A non-left-to-right direction throws `NotSupportedException`.

###### Test Scenarios

| Test | Assertion |
| --- | --- |
| `AxisTransform_Apply_RightDirection_LeavesCoordinatesUnchanged` | Coordinates are unchanged by the transform |
| `AxisTransform_Apply_NonRightDirection_ThrowsNotSupportedException` | Throws `NotSupportedException` |
