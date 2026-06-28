### ConnectorLabelPlacer Verification

#### Verification Approach

`ConnectorLabelPlacer` is verified through unit tests in `ConnectorLabelPlacerTests` that construct
explicit `LayoutLine` values, call `Place`, and assert on the returned position dictionary. The unit
is pure and deterministic, so no mocking is required.

#### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

#### Acceptance Criteria

- All `ConnectorLabelPlacerTests` pass with zero failures across all target frameworks.
- A line without a label is absent from the result.
- A single labelled line is placed at the midpoint of its longest segment.
- Two labels whose preferred positions coincide are separated so they do not overlap.

#### Test Scenarios

| Test | Assertion |
| --- | --- |
| `Place_LineWithoutLabel_IsOmitted` | An unlabelled line is omitted from the result |
| `Place_SingleLine_UsesLongestSegmentMidpoint` | A label lands at the midpoint of the longest segment |
| `Place_CollidingLabels_AreSeparated` | Colliding labels are separated; the first keeps its preferred midpoint |
