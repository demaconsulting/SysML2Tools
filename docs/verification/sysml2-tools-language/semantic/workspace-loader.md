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
- Files are processed in canonical path order: when two files declare the same root-level name,
  the declaration in the canonically first file is used whatever order the files are supplied
  in, the later one is ignored as a whole (its members are not merged and nothing inside it is
  reported), and the diagnostics are identical for any supplied order.
- Every root-level declaration whose name — quotes ignored — an earlier declaration already uses
  (a package or a definition, in another file or earlier in the same file) produces exactly one
  `Warning` at its name token (one-based line and column), also with `strictUnresolved: true`,
  naming the file and position of the declaration that is used. A path supplied twice, distinct
  root names, imports at the root of several files, and a user package named like a
  standard-library package produce no such warning.
- A package split across two files, each part importing a library (issue #144), produces only
  the duplicate warning: no unresolved reference, in either file order, whichever part carries
  the import. A package and a definition sharing a name (issue #139) are duplicates too.
- A named declaration records the position of its name token in `SysmlNode.NamePosition`.
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

Binary compatibility of the public API is verified by two further tests, and the canonical file
order and the duplicate root declarations by those below (listed outside the table because of their
long names). The duplicate tests write named files (`a.sysml`, `b.sysml`, ...) into a fresh
temporary directory, so the canonical order is the ordinal order of the names, and load them in the
order given or reversed:

- `WorkspaceLoader_LoadAsync_DuplicateTopLevelName_IsIndependentOfFileOrder` — two files whose
  top-level declarations share a name are loaded in both orders; the declaration in the canonically
  first file is used, `Files` is in canonical order, the later declaration is reported as a duplicate
  and its unresolved reference is not reported, and the diagnostics are identical.
- `WorkspaceLoader_LoadAsync_SameRootPackageInTwoFiles_WarnsAtLaterDeclarationNamingFirstFile` —
  `package P` in two files (with and without `strictUnresolved`): exactly one `Warning`, in the later
  file at `(1,9)`, with the exact message naming the first file at `(1,9)`, and no error.
- `WorkspaceLoader_LoadAsync_SameRootPackageInTwoFiles_IsIndependentOfFileOrder` — both argument
  orders give the same diagnostics and declarations; the first file's package is used.
- `WorkspaceLoader_LoadAsync_SameRootPackageInTwoFiles_DoesNotMergeMembers` — `P::B` of the ignored
  declaration is absent, and a third file's reference to it is unresolved while `P::A` resolves.
- `WorkspaceLoader_LoadAsync_SplitPackageWithWildcardImport_ReportsOnlyTheDuplicate` and
  `…_SplitPackageWithNamedImports_ReportsOnlyTheDuplicate` — issue #144: a split package whose
  parts import `ScalarValues` (wildcard or by name, in either part, in either order, in strict
  mode) yields one duplicate warning and no unresolved reference.
- `WorkspaceLoader_LoadAsync_PackageAndPartDefWithSameName_WarnsAndFirstFileWins` — issue #139:
  a package and a `part def` of one name, in both file orders; the first file's declaration is used
  and the other is reported at its own name column.
- `WorkspaceLoader_LoadAsync_SameNameTwiceInOneFile_WarnsAtSecondDeclaration` — the second
  declaration in source order is reported, naming the first in the same file.
- `WorkspaceLoader_LoadAsync_QuotedAndUnquotedSameName_AreDuplicates` and
  `…_DuplicateQuotedNameWithSpace_MessageShowsNameOnce` — `'P'` and `P` are one name; the message
  shows the name once in single quotes.
- `WorkspaceLoader_LoadAsync_RootAliasAndPackageWithSameName_WarnsAndFirstFileWins` and
  `…_TwoRootAliasesWithSameName_WarnsAtSecondAlias` — a root-level alias is a named member: an
  alias and a package of one name (both file orders, strict mode) and two aliases (one quoted, one
  with a short name) are duplicates, reported at the alias's name token, not the `alias` keyword.
- `WorkspaceLoader_LoadAsync_ShortNameEqualToDeclaredName_WarnsAtDeclaredName` — with
  `alias <N> N for Q;` and `part def <A> A;` the warning is at the declared name, not the equal
  short name written before it.
- `WorkspaceLoader_LoadAsync_SamePathSuppliedTwice_ReportsNoDuplicate` and
  `…_DistinctRootNames_ReportNoDuplicate` — negative cases: a repeated path, distinct root names
  with the same member names, and imports repeated at the root of several files.
- `WorkspaceLoader_LoadAsync_Declarations_CaptureNamePosition` — `NamePosition` is the name token,
  quotes included, not the declaration's first token.
- `WorkspaceLoader_LoadAsync_UserPackageNamedLikeStdlibPackage_MembersAreNotStdlibOrigin` — a
  user `Parts` package is not a duplicate of the standard library's.
- `WorkspaceLoader_LoadAsync_OriginalTwoParameterOverload_ReportsUnresolvedAsWarning` — calls that
  overload directly with one and two arguments and compares it with the three-argument form, and
  asserts that an unresolved reference is a Warning, identical to `strictUnresolved: false`.

#### Level 10 Gate

`SemanticOmgModels_AllModels_ResolveWithZeroErrors` confirms that all OMG model files in
`test/SysMLModels/` load with zero Error-level diagnostics.
