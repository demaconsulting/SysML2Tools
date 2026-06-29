#### HighwayAssigner Verification

##### Verification Approach

`HighwayAssigner` is verified through unit tests in `HighwayAssignerTests` that construct explicit box
and edge lists and assert on the detected corridors, edge assignments, and cost multipliers. The engine
is pure and deterministic, so identical input yields identical corridors. No mocking is required.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `HighwayAssignerTests` pass with zero failures across all three target frameworks.
- An empty graph returns empty corridors, assignments, and multipliers.
- A single wire forms one corridor that is not a highway.
- A dense fan reserves a trunk wide enough for at least three concurrent lanes.
- Highway corridors discount routing cost; ordinary corridors stay neutral.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `HighwayAssigner_Assign_EmptyGraph_ReturnsEmpty` | Empty graph yields empty result |
| `HighwayAssigner_Assign_TwoBoxesOneWire_OneCorridorNoHighway` | Single wire is a non-highway corridor |
| `HighwayAssigner_Assign_SixBoxFan_PeakLanesAtLeastThreeAndHighway` | Dense fan reserves a highway trunk |
| `HighwayAssigner_Assign_Multipliers_HighwayCheaperNormalNeutral` | Highway cheaper, normal neutral |
