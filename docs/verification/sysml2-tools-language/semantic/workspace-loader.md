### WorkspaceLoader Verification

#### Verification Approach

`WorkspaceLoader` is verified through 9 progressive-level integration tests in
`WorkspaceLoaderTests`. Tests call `WorkspaceLoader.LoadAsync` with controlled inputs and assert
on the returned `SysmlLoadResult` properties. No mocking of internal components is used; each
test exercises the complete semantic pipeline end-to-end.

#### Test Environment

Tests run via `dotnet test` against all four target frameworks: net8.0, net9.0, net10.0, and net11.0.
Temporary `.sysml` files are created in `Path.GetTempPath()` and deleted after each test. No
external services, network access, or additional configuration are required beyond a standard
.NET SDK installation.

#### Acceptance Criteria

- All `WorkspaceLoaderTests` pass with zero failures across all four target frameworks.
- An empty `.sysml` file returns a non-null `SysmlWorkspace` with `HasErrors = false`.
- A single-package file registers exactly the package qualified name in `Declarations`.
- Nested packages register both parent and child qualified names.
- Part definitions register their qualified names.
- Calling `LoadAsync` with no files returns a non-null workspace containing stdlib declarations.
- Seeding from the pre-compiled stdlib symbol table with no user files results in
  `HasErrors = false` and a non-empty `Declarations`.
- A resolved specialization (`specializes` with a known type) produces no unresolved diagnostic.
- An unresolved supertype reference produces exactly one `Warning`-severity diagnostic.
- With `strictUnresolved: true`, unresolved references of every kind produce `Error`-severity
  diagnostics instead, with the same message and position; the default is `false`, and the
  default severity is the single constant `WorkspaceLoader.DefaultUserUnresolvedSeverity`
  (`Warning`).
- Standard-library references in a user file resolve without diagnostics, because the seed is
  pre-resolved and never resolved again at load time.
- A circular import produces a `Warning`-severity diagnostic and completes in finite time; a
  circular import or cyclic specialization stays a Warning with `strictUnresolved` either way.
- A user package named like a standard-library package (`Parts`) contributes members that are
  not in `SysmlWorkspace.StdlibNames`, which holds exactly the embedded seed's qualified names.
- The original public two-parameter overload `LoadAsync(IEnumerable<string>, SymbolTable? = null)`
  remains in the assembly metadata (public, static, returning `Task<SysmlLoadResult>`, with an
  optional second parameter defaulting to `null`), so consumers compiled against earlier
  releases keep working, and it reports an unresolved reference as a Warning exactly as the
  three-parameter overload does with `strictUnresolved: false`.

#### Test Scenarios

| Test | Level | Assertion |
| --- | --- | --- |
| `WorkspaceLoader_LoadAsync_EmptyFile_ReturnsNonNullWorkspace` | 1 | Empty file; non-null workspace, HasErrors false |
| `WorkspaceLoader_LoadAsync_SinglePackage_RegistersDeclaration` | 2 | Single package name registered |
| `WorkspaceLoader_LoadAsync_NestedPackages_RegistersQualifiedNames` | 3 | Nested package qualified names registered |
| `WorkspaceLoader_LoadAsync_PartDef_RegistersDefinition` | 4 | Part def qualified name registered |
| `WorkspaceLoader_LoadAsync_NoFiles_ReturnsNonNullWorkspace` | 5 | No-files load returns non-null workspace |
| `WorkspaceLoader_LoadAsync_StdlibDeclarations_Registered` | 6 | Stdlib contributes declarations without errors |
| `WorkspaceLoader_LoadAsync_SpecializesChain_Registered` | 7 | Resolved supertype produces no unresolved diagnostic |
| `WorkspaceLoader_LoadAsync_UnresolvedReference_ProducesWarning` | 8 | Unresolved supertype produces Warning |
| `WorkspaceLoader_LoadAsync_CircularImport_ProducesWarningNoInfiniteLoop` | 9 | Circular import; Warning emitted |
| `WorkspaceLoader_LoadAsync_UserPackageNamedLikeStdlibPackage_MembersAreNotStdlibOrigin` | — | Not stdlib |
| `WorkspaceLoader_LoadAsync_UnresolvedExpressionReference_ProducesWarningAtToken` | — | Warning at token |
| `WorkspaceLoader_LoadAsync_StrictUnresolved_UnresolvedExpressionReference_ProducesErrorAtToken` | — | Strict Error |
| `WorkspaceLoader_LoadAsync_StrictUnresolved_UnresolvedReferenceKinds_ProduceErrors` | — | Errors when strict |
| `WorkspaceLoader_DefaultUnresolvedSeverity_IsWarning` | — | Default severity constant is Warning |
| `WorkspaceLoader_LoadAsync_CircularImportAndCyclicSpecialization_RemainWarnings` | — | Stay Warnings |
| `WorkspaceLoader_LoadAsync_StdlibReferences_ProduceNoDiagnostics` | — | Seeded stdlib adds no diagnostics |

Binary compatibility of the public API is verified by two further tests (listed outside the table because
of their long names):

- `WorkspaceLoader_LoadAsync_OriginalTwoParameterSignature_RemainsPublic` — a reflection tripwire
  asserting that the original public static two-parameter overload exists, returns
  `Task<SysmlLoadResult>`, and has an optional second parameter defaulting to `null`.
- `WorkspaceLoader_LoadAsync_OriginalTwoParameterOverload_ReportsUnresolvedAsWarning` — invokes that
  overload through its reflected metadata signature and by direct one- and two-argument calls, and
  asserts that an unresolved reference is a Warning, identical to `strictUnresolved: false`.

#### Level 10 Gate

`SemanticOmgModels_AllModels_ResolveWithZeroErrors` confirms that all OMG model files in
`test/SysMLModels/` load with zero Error-level diagnostics.
