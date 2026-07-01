### BoxMetrics Verification

#### Verification Approach

`BoxMetrics` has no dedicated unit-test class; its formulas are pure and are verified indirectly
through the interconnection view layout strategy tests, which call `BoxMetrics.TitleAreaHeight` and
assert that nested children are placed below the reserved title area within their container box. This
proves that the space reserved by the strategy and computed by `BoxMetrics` is consistent.

#### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

#### Acceptance Criteria

- The interconnection view tests that consume `BoxMetrics.TitleAreaHeight` pass with zero failures
  across all target frameworks.
- Nested child boxes are positioned below the reserved title area of their container box.
- The folder-tab height matches the space reserved above the box body when a folder box is drawn.

#### Test Scenarios

| Test | Assertion |
| --- | --- |
| `InterconnectionView_BuildLayout_ContainerSize_BoundsChildrenAndTitle` | Container bounds children below title |
| `InterconnectionView_BuildLayout_NestedContainer_PlacesChildrenInsideContainerBox` | Children sit inside the box |
