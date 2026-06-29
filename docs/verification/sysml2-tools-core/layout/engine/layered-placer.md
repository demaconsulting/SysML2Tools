#### LayeredPlacer Verification

##### Verification Approach

`LayeredPlacer` is verified through unit tests in `LayeredPlacerTests` that construct explicit node
lists and edge lists with known topology, invoke `Place`, and assert on the returned `LayerResult`.
Geometric overlap checks verify the non-overlapping guarantee directly on the produced rectangles.
The engine is pure and deterministic, so all assertions are stable across repeated runs. No mocking
is required.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `LayeredPlacerTests` pass with zero failures across all three target frameworks.
- A linear chain produces the highest-degree node in layer 0 and its neighbours in layer 1.
- A star topology places the centre node in layer 0 and all spokes in layer 1.
- Nodes with no edges all receive layer 0 and share a single X column.
- A dense corridor (five crossing edges) produces a wider column gap than a sparse corridor (one edge).
- Any pair of rectangles from a connected six-node graph is strictly non-overlapping.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `LayeredPlacer_Place_LinearChain_ThreeLayers` | Highest-degree node (B) is the seed; A and C land in a different layer from B; exactly two distinct layer values |
| `LayeredPlacer_Place_StarTopology_CenterInLayer0_SpokesInLayer1` | Centre (degree 4) in layer 0; all four spokes in layer 1 |
| `LayeredPlacer_Place_NoEdges_AllInLayer0` | All three isolated nodes assigned layer 0; single X column |
| `LayeredPlacer_Place_DenseCorridorEdges_CorridorWidthScales` | Wide (8-edge hub-and-spokes) corridor column span exceeds narrow (1-edge) column span by more than 50 px |
| `LayeredPlacer_Place_AllRects_NonOverlapping` | No two of the six placed rectangles overlap |
