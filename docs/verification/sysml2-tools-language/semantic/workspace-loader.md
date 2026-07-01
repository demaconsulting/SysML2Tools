### WorkspaceLoader Verification

#### Verification Approach

`WorkspaceLoader` is verified through 9 progressive-level integration tests in
`WorkspaceLoaderTests`. Tests call `WorkspaceLoader.LoadAsync` with controlled inputs and assert
on the returned `SysmlLoadResult` properties. No mocking of internal components is used; each
test exercises the complete semantic pipeline end-to-end.

#### Test Environment

Tests run via `dotnet test` against all three target frameworks: net8.0, net9.0, and net10.0.
Temporary `.sysml` files are created in `Path.GetTempPath()` and deleted after each test. No
external services, network access, or additional configuration are required beyond a standard
.NET SDK installation.

#### Acceptance Criteria

- All `WorkspaceLoaderTests` pass with zero failures across all three target frameworks.
- An empty `.sysml` file returns a non-null `SysmlWorkspace` with `HasErrors = false`.
- A single-package file registers exactly the package qualified name in `Declarations`.
- Nested packages register both parent and child qualified names.
- Part definitions register their qualified names.
- Calling `LoadAsync` with no files returns a non-null workspace containing stdlib declarations.
- Seeding from the pre-compiled stdlib symbol table with no user files results in
  `HasErrors = false` and a non-empty `Declarations`.
- A resolved specialization (`specializes` with a known type) produces no unresolved Warning.
- An unresolved supertype reference produces exactly one `Warning`-severity diagnostic.
- A circular import produces a `Warning`-severity diagnostic and completes in finite time.

#### Test Scenarios

| Test | Level | Assertion |
| --- | --- | --- |
| `WorkspaceLoader_LoadAsync_EmptyFile_ReturnsNonNullWorkspace` | 1 | Empty file; non-null workspace, HasErrors false |
| `WorkspaceLoader_LoadAsync_SinglePackage_RegistersDeclaration` | 2 | Single package name registered |
| `WorkspaceLoader_LoadAsync_NestedPackages_RegistersQualifiedNames` | 3 | Nested package qualified names registered |
| `WorkspaceLoader_LoadAsync_PartDef_RegistersDefinition` | 4 | Part def qualified name registered |
| `WorkspaceLoader_LoadAsync_NoFiles_ReturnsNonNullWorkspace` | 5 | No-files load returns non-null workspace |
| `WorkspaceLoader_LoadAsync_StdlibDeclarations_Registered` | 6 | Stdlib contributes declarations without errors |
| `WorkspaceLoader_LoadAsync_SpecializesChain_Registered` | 7 | Resolved supertype produces no unresolved warning |
| `WorkspaceLoader_LoadAsync_UnresolvedReference_ProducesWarning` | 8 | Unresolved supertype produces Warning |
| `WorkspaceLoader_LoadAsync_CircularImport_ProducesWarningNoInfiniteLoop` | 9 | Circular import; Warning emitted |

#### Level 10 Gate

`SemanticOmgModels_AllModels_ResolveWithZeroErrors` confirms that all OMG model files in
`test/SysMLModels/` load with zero Error-level diagnostics.
