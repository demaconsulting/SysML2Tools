### LayoutTree Verification

#### Verification Approach

The `LayoutTree` data-model unit is verified through unit tests in `LayoutTests` that construct each
record type with explicit values and assert that every constructor argument is stored unchanged, that
coordinates remain absolute, and that boxes carry an integer depth and a heterogeneous children list.
The records are pure immutable value types, so no mocking is required.

#### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

#### Acceptance Criteria

- All `LayoutTests` pass with zero failures across all target frameworks.
- Every record type stores its constructor arguments unchanged.
- All coordinate fields are stored as absolute values without transformation.
- `LayoutBox.Depth` is an integer and `LayoutBox.Children` holds heterogeneous nodes in order.

#### Test Scenarios

| Test | Assertion |
| --- | --- |
| `LayoutTree_Construction_StoresWidthHeightNodes` | The tree stores width, height, and nodes |
| `LayoutBox_Construction_StoresAllFields` | A box stores all nine fields |
| `LayoutPort_Construction_StoresAllFields` | A port stores all four fields |
| `LayoutLine_Construction_StoresAllFields` | A line stores waypoints, end markers, style, and label |
| `LayoutLabel_Construction_StoresAllFields` | A label stores all eight fields |
| `LayoutBadge_Construction_StoresAllFields` | A badge stores all five fields |
| `LayoutBand_Construction_StoresAllFields` | A band stores all seven fields |
| `LayoutLifeline_Construction_StoresAllFields` | A lifeline stores all six fields |
| `LayoutActivation_Construction_StoresAllFields` | An activation stores all three fields |
| `LayoutGrid_Construction_StoresAllFields` | A grid stores rows, cells, and cell fields |
| `LayoutBox_Coordinates_AreAbsolute` | Box coordinates are stored without offset |
| `LayoutPort_Coordinates_AreAbsolute` | Port coordinates are stored without offset |
| `LayoutLine_Waypoints_AreAbsolute` | Line waypoints retain their absolute values |
| `LayoutBox_Depth_IsInteger` | Depth is stored as an integer with no color field |
| `LayoutBox_Children_ContainsNestedNodes` | Children hold heterogeneous nodes in insertion order |
