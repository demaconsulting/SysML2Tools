#### ConnectivityAnalyzer Verification

##### Verification Approach

`ConnectivityAnalyzer` is verified through unit tests in `ConnectivityAnalyzerTests` that construct
explicit node and edge lists and assert on the returned layer hints, community ids, and adjacency. The
engine is pure and deterministic, so determinism is verified by analysing identical input twice and
comparing the results. No mocking is required.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `ConnectivityAnalyzerTests` pass with zero failures across all three target frameworks.
- An empty graph yields empty results.
- A single node is layer 0, community 0.
- A chain produces strictly increasing layers in one community.
- A hub-and-spoke fan collapses to one community; disconnected components get distinct ids.
- Identical input produces identical analysis.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `Analyze_Empty_ReturnsEmpty` | No hints, communities, or adjacency |
| `Analyze_SingleNode_LayerZeroCommunityZero` | Lone node at layer 0, community 0 |
| `Analyze_Chain_LayersAndOneCommunity` | Layers 0,1,2 in a single community |
| `Analyze_Star_SameCommunity` | Hub and spokes share one community |
| `Analyze_TwoComponents_DifferentCommunities` | Components get distinct community ids |
| `Analyze_SameInput_IsDeterministic` | Identical input yields identical analysis |
