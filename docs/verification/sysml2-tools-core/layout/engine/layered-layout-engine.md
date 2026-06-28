#### LayeredLayoutEngine Verification

##### Verification Approach

`LayeredLayoutEngine` is verified through unit tests in `LayeredLayoutEngineTests` that construct
explicit node and directed-edge lists and assert on the returned placement. Layer assignment is
checked by comparing the reported `Layers` against the expected flow and by asserting that each
node's Y increases with its layer; a geometric helper in the test class checks whether two
rectangles overlap. A cyclic input is exercised to confirm the engine terminates and places every
node. No mocking is required; the engine is pure and deterministic.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `LayeredLayoutEngineTests` pass with zero failures across all three target frameworks.
- A chain assigns strictly increasing layers with increasing Y coordinates.
- Every edge runs from a strictly smaller layer to a larger layer.
- Nodes sharing a layer do not overlap.
- A cyclic graph terminates and places every node within the region bounds.
- An empty node list yields an empty placement sized only by the padding.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `Place_EmptyList_ReturnsPaddingOnlyRegion` | No rectangles; region is `2 * padding` on each axis |
| `Place_Chain_AssignsIncreasingLayers` | Chain layers are 0,1,2,3 and Y increases with layer |
| `Place_Branching_EdgesPointDownward` | Every edge source sits in a strictly smaller layer than its target |
| `Place_SameLayerNodes_DoNotOverlap` | Nodes sharing a layer are pairwise disjoint |
| `Place_Cycle_TerminatesAndPlacesAllNodes` | Cyclic input places all nodes within the region bounds |
