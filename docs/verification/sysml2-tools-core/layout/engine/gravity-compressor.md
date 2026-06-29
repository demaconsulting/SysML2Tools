#### GravityCompressor Verification

##### Verification Approach

`GravityCompressor` is verified through unit tests in `GravityCompressorTests` that construct explicit
box lists and assert on the returned positions and feasibility flag. A geometric overlap check
verifies the non-overlap property directly on the produced positions. The engine is pure and
deterministic, so determinism is verified by compressing identical input twice. No mocking is required.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `GravityCompressorTests` pass with zero failures across all three target frameworks.
- Overlapping boxes are separated until clear.
- Already-clear boxes stay put; a single overlap adjusts only the colliding pair.
- An impossible request is reported infeasible.
- Identical input produces identical positions.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `Compress_TwoOverlapping_SeparatedToMinGap` | Overlapping pair becomes clear |
| `Compress_Separated_Unchanged` | Clear pair is left untouched |
| `Compress_ThreeChainOneOverlap_AdjustsOnlyThatGap` | Only the overlapping gap changes |
| `Compress_Impossible_ReturnsInfeasible` | Infeasible request flagged false |
| `Compress_SameInput_IsDeterministic` | Identical input yields identical positions |
| `Compress_CorridorConstraint_GapExpandsToMinWidth` | Corridor gap widens to its minimum width |
| `Compress_NoCorridor_ExistingBehaviourUnchanged` | Null corridors leave separation behaviour intact |
