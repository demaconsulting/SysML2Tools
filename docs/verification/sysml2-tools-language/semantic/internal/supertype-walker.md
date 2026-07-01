#### SupertypeWalker Verification

##### Verification Approach

`SupertypeWalker` is an internal class verified indirectly through `WorkspaceLoaderTests`.
Tests construct files with specialization chains (resolved and cyclic) and assert on the
diagnostics returned by `WorkspaceLoader.LoadAsync`. A resolved specialization chain with no
cycles produces no Warning from the walker; a cyclic chain produces a Warning diagnostic.

##### Test Environment

Tests run via `dotnet test` against all three target frameworks: net8.0, net9.0, and net10.0.
Temporary `.sysml` files are created in `Path.GetTempPath()` and deleted after each test. No
external services or additional configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- A resolved specialization chain (`A specializes B`, both registered) produces no cyclic
  specialization Warning diagnostic.
- A cyclic chain (`A specializes B`, `B specializes A`) produces a `Warning`-severity diagnostic
  and `LoadAsync` returns in finite time.
- Symbols with no supertypes are processed without producing any diagnostic.

##### Test Scenarios

| Scenario | Verified By |
| --- | --- |
| Resolved chain — no Warning | `WorkspaceLoader_LoadAsync_SpecializesChain_Registered` |
