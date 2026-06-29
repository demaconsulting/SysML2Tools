#### ConnectedPairSpacer Verification

##### Verification Approach

`ConnectedPairSpacer` is verified through unit tests in `ConnectedPairSpacerTests` that construct
explicit box lists and connectivity, then assert on the returned positions. A geometric gap check
verifies the approach-zone property directly on the produced positions. The engine is pure and
deterministic, so determinism is verified by spacing identical input twice. No mocking is required.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `ConnectedPairSpacerTests` pass with zero failures across all three target frameworks.
- A touching connected pair is widened to twice the approach zone.
- Unconnected and already-separated pairs are left untouched.
- Identical input produces identical positions.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `Space_ZeroGapPair_PushedByTwoApproachZones` | Touching pair widens to 2 × approach zone |
| `Space_UnconnectedPair_Unmoved` | No pairs supplied leaves boxes in place |
| `Space_SeparatedPair_Unmoved` | Already-clear pair is left untouched |
| `Space_SameInput_IsDeterministic` | Identical input yields identical positions |
