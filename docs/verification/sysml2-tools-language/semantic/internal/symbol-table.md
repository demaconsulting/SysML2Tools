#### SymbolTable Verification

##### Verification Approach

`SymbolTable` is an internal class verified indirectly through `WorkspaceLoaderTests`. Tests
call `WorkspaceLoader.LoadAsync` with controlled source files and assert on
`SysmlLoadResult.Workspace.Declarations`, confirming that `SymbolTable` correctly registered
named nodes and that `Contains` and `Lookup` return the expected results when queried by
`ReferenceResolver` and `SupertypeWalker`.

##### Test Environment

Tests run via `dotnet test` against all three target frameworks: net8.0, net9.0, and net10.0.
Temporary `.sysml` files are created in `Path.GetTempPath()` and deleted after each test. No
external services or additional configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- A registered package name appears in `Declarations` after `LoadAsync` returns.
- Both parent and child qualified names appear in `Declarations` for nested packages.
- Duplicate qualified names are silently ignored; no Error diagnostic is produced.
- `Contains` returns `true` for a registered name used by `ReferenceResolver`, preventing a
  spurious unresolved-reference Warning for that name.

##### Test Scenarios

| Scenario | Verified By |
| --- | --- |
| Single name registration | `WorkspaceLoader_LoadAsync_SinglePackage_RegistersDeclaration` |
| Nested qualified name registration | `WorkspaceLoader_LoadAsync_NestedPackages_RegistersQualifiedNames` |
| Lookup used by ReferenceResolver | `WorkspaceLoader_LoadAsync_SpecializesChain_Registered` |
| Lookup used by SupertypeWalker | `WorkspaceLoader_LoadAsync_SpecializesChain_Registered` |
