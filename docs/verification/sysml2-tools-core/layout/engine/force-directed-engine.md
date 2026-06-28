#### ForceDirectedEngine Verification

##### Verification Approach

`ForceDirectedEngine` is verified through unit tests in `ForceDirectedEngineTests` that construct
explicit node and edge lists and assert on the returned placement. A geometric helper in the test
class checks whether two rectangles overlap, so the non-overlap property is verified directly on
the produced rectangles. No mocking is required; the engine is pure and deterministic, so the
determinism property is verified by placing identical input twice and comparing the results.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `ForceDirectedEngineTests` pass with zero failures across all three target frameworks.
- A connected graph is placed with no two node rectangles overlapping.
- Every placed rectangle lies within the reported region width and height.
- Identical inputs produce identical region size and rectangle positions.
- An empty node list yields an empty placement sized only by the padding.
- A single node is placed at the padding origin.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `Place_EmptyList_ReturnsPaddingOnlyRegion` | No rectangles; region is `2 * padding` on each axis |
| `Place_SingleNode_PositionsAtPadding` | Lone node placed at the padding origin |
| `Place_ConnectedGraph_ProducesNoOverlaps` | Every pair of placed rectangles is disjoint |
| `Place_ConnectedGraph_AllRectsWithinBounds` | All rectangles lie within the reported region bounds |
| `Place_SameInput_IsDeterministic` | Identical input yields identical region size and positions |
