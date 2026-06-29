#### PortAssigner Verification

##### Verification Approach

`PortAssigner` is verified through unit tests in `PortAssignerTests` that construct explicit box
rectangles and port requests and assert on the returned placements. Side selection is checked with
a parameterized theory covering all four directions; boundary placement and even distribution are
checked by asserting exact coordinates against the box geometry. No mocking is required; the
assigner is pure and deterministic.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `PortAssignerTests` pass with zero failures across all three target frameworks.
- A single port is assigned to the box side facing its target for each of the four directions.
- A placed port's centre lies on the boundary of its assigned side.
- Multiple ports on the same side occupy distinct, evenly spaced positions.
- An empty request list yields no placements.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `Assign_SinglePort_ChoosesSideFacingTarget` | Port lands on the side facing its target (all four directions) |
| `Assign_Port_CentreLiesOnBoxBoundary` | Port centre sits on the assigned side's edge, within the box extent |
| `Assign_MultiplePortsSameSide_AreEvenlyDistributed` | Same-side ports occupy distinct, evenly spaced slots |
| `Assign_Empty_ReturnsEmpty` | An empty request list yields no placements |
| `AssignHighway_SameKey_ShareTrunkGroup` | Matching corridor ports merge to a shared trunk group |
| `AssignHighway_DifferentConnectorType_DistinctGroups` | Differing connector type yields distinct groups |
| `AssignHighway_NoCorridor_StaysIndependent` | Corridor id -1 stays independent (group -1) |
| `AssignHighway_MergedGroup_TrunkSitsOffFace` | A merged group forms its trunk one approach zone off the face |
| `AssignHighway_SingleCorridorPort_RoutesToFace` | A single corridor port routes to the face midpoint |
| `Assign_ManyPortsOnShortFace_UsesMinimumSlot` | Short faces compress ports to a centred minimum slot |
