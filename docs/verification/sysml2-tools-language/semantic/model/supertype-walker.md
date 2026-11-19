#### SupertypeWalker Verification

##### Verification Approach

`SupertypeWalker` is an internal class verified indirectly through `WorkspaceLoaderTests`.
Tests construct files with specialization chains (resolved and cyclic) and assert on the
diagnostics returned by `WorkspaceLoader.LoadAsync`. A resolved specialization chain with no
cycles produces no Warning from the walker; a cyclic chain produces a Warning diagnostic.

##### Test Environment

Tests run via `dotnet test` against all four target frameworks: net8.0, net9.0, net10.0, and net11.0.
Temporary `.sysml` files are created in `Path.GetTempPath()` and deleted after each test. No
external services or additional configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- A resolved specialization chain (`A specializes B`, both registered) produces no cyclic
  specialization Warning diagnostic.
- A cyclic chain (`A specializes B`, `B specializes A`) produces a `Warning`-severity diagnostic
  and `LoadAsync` returns in finite time.
- Symbols with no supertypes are processed without producing any diagnostic.
- The cyclic-specialization Warning names the declaring file and is located at the supertype
  reference that closes the cycle.
- A model made only of implicit specializations (no declared supertype) produces no
  cyclic-specialization Warning.

##### Test Scenarios

| Scenario | Verified By |
| --- | --- |
| Cycle Warning file and position | `WorkspaceLoader_LoadAsync_CyclicSpecialization_ReportsFileAndPosition` |
| Implicit specializations not walked | `WorkspaceLoader_LoadAsync_ImplicitSpecializationsOnly_NoCycleWarning` |
| Resolved chain — no Warning | `WorkspaceLoader_LoadAsync_SpecializesChain_Registered` |
