##### LayerAssigner Verification

###### Verification Approach

`LayerAssigner` is verified through unit tests in `LayerAssignerTests` that run `CycleBreaker` and
then `LayerAssigner` over graphs with known topology and assert on the resulting `NodeLayers`. A
linear chain checks monotonic, contiguous layer indices; a diamond checks the longest-path rule
where a join node sits one layer past the deeper of its two branches. The stage is pure and
deterministic, so no mocking is required.

###### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

###### Acceptance Criteria

- All `LayerAssignerTests` pass with zero failures across all three target frameworks.
- A linear chain produces strictly increasing, contiguous layer indices.
- A diamond places the join node at the longest-path layer.

###### Test Scenarios

| Test | Assertion |
| --- | --- |
| `LayerAssigner_Apply_LinearChain_AssignsMonotonicLayers` | Chain nodes receive layers 0, 1, 2 |
| `LayerAssigner_Apply_DiamondGraph_AssignsLongestPathLayers` | Source layer 0, branches layer 1, join layer 2 |
