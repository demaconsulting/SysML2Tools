#### ReferenceResolver Verification

##### Verification Approach

`ReferenceResolver` is an internal class verified indirectly through `WorkspaceLoaderTests`.
Tests construct files with deliberate unresolved supertype references and circular import
declarations, then call `WorkspaceLoader.LoadAsync` and assert that the returned diagnostics
contain the expected Warning entries. The absence of an infinite loop is verified implicitly by
test completion within the xUnit v3 timeout.

##### Test Environment

Tests run via `dotnet test` against all three target frameworks: net8.0, net9.0, and net10.0.
Temporary `.sysml` files are created in `Path.GetTempPath()` and deleted after each test. No
external services or additional configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- An unresolved supertype name produces exactly one `Warning`-severity diagnostic per file
  containing that name.
- A circular import chain between two files produces a `Warning`-severity diagnostic and
  `LoadAsync` returns (does not hang).
- A resolved supertype name (registered in `SymbolTable`) produces no Warning diagnostic.

##### Test Scenarios

| Scenario | Verified By |
| --- | --- |
| Unresolved supertype reference | `WorkspaceLoader_LoadAsync_UnresolvedReference_ProducesWarning` |
| Circular import — terminates | `WorkspaceLoader_LoadAsync_CircularImport_ProducesWarningNoInfiniteLoop` |
| Resolved reference — no Warning | `WorkspaceLoader_LoadAsync_SpecializesChain_Registered` |
