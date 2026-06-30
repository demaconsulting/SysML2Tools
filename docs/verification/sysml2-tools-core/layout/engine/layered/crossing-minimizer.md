##### CrossingMinimizer Verification

###### Verification Approach

`CrossingMinimizer` is verified through unit tests in `CrossingMinimizerTests` that run the
cycle-breaking, layering, splitting, and crossing-minimization stages and assert on the resulting
`Groups`. One test confirms the per-layer grouping shape for a diamond; another confirms that the
grouping is a partition of the augmented nodes, including the dummy nodes of a long edge. The stage
is pure and deterministic, so no mocking is required.

###### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

###### Acceptance Criteria

- All `CrossingMinimizerTests` pass with zero failures across all three target frameworks.
- A diamond produces one group per layer with the expected per-layer counts.
- Every augmented node index appears in exactly one layer group.

###### Test Scenarios

| Test | Assertion |
| --- | --- |
| `CrossingMinimizer_Apply_TwoLayerGraph_GroupsNodesByLayer` | A diamond yields three layer groups with counts 1, 2, 1 |
| `CrossingMinimizer_Apply_AllAugmentedNodesAppearInGroups` | The groups partition all augmented nodes exactly once |
