##### CycleBreaker Verification

###### Verification Approach

`CycleBreaker` is verified through unit tests in `CycleBreakerTests` that construct graphs with
known cycles, self-loops, and duplicate edges, run the stage, and assert on the resulting `Acyclic`
edge set. Acyclicity is checked with a local topological-sort helper; self-loop and duplicate
removal are checked by inspecting the resulting edges. The stage is pure and deterministic, so no
mocking is required.

###### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

###### Acceptance Criteria

- All `CycleBreakerTests` pass with zero failures across all three target frameworks.
- A graph containing a directed cycle yields an acyclic edge set.
- Self-loops are dropped and duplicate source-target pairs are collapsed.

###### Test Scenarios

| Test | Assertion |
| --- | --- |
| `CycleBreaker_Apply_GraphWithCycle_ProducesAcyclicEdgeSet` | The resulting edge set has no directed cycle |
| `CycleBreaker_Apply_SelfLoopsAndDuplicates_AreRemoved` | No self-loop remains and duplicate edges are collapsed |
