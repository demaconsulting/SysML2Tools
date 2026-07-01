### Semantic Internal Subsystem Verification

#### Verification Approach

Internal semantic components (`AstBuilder`, `SymbolTable`, `ReferenceResolver`, and
`SupertypeWalker`) are verified indirectly through `WorkspaceLoaderTests`. There are no direct
unit tests for these internal classes because they have no public surface. Their behavior is
observable exclusively through the public `WorkspaceLoader.LoadAsync` API.

#### Test Environment

Tests run via `dotnet test` against all three target frameworks: net8.0, net9.0, and net10.0.
Temporary `.sysml` files are created in `Path.GetTempPath()` and deleted after each test. No
external services, network access, or additional configuration are required beyond a standard
.NET SDK installation.

#### Acceptance Criteria

- All `WorkspaceLoaderTests` pass with zero failures across all three target frameworks.
- `AstBuilder` correctly produces qualified names for nested packages and definitions as
  confirmed by tests that check `Declarations` contents.
- `SymbolTable` registers all named nodes from the provided AST roots; duplicate names are
  silently ignored without error.
- `ReferenceResolver` emits exactly one Warning per unresolved supertype name per file; it
  completes without infinite loops when circular imports are present.
- `SupertypeWalker` emits Warning diagnostics for cyclic specialization chains and terminates
  in finite time for any reachable graph.

#### Test Scenarios

Traceability to `WorkspaceLoaderTests` test methods:

| Internal Component | Verified By |
| --- | --- |
| `AstBuilder` — name extraction | `WorkspaceLoader_LoadAsync_SinglePackage_RegistersDeclaration` |
| `AstBuilder` — qualified names | `WorkspaceLoader_LoadAsync_NestedPackages_RegistersQualifiedNames` |
| `AstBuilder` — supertype extraction | `WorkspaceLoader_LoadAsync_SpecializesChain_Registered` |
| `SymbolTable` — registration | `WorkspaceLoader_LoadAsync_SinglePackage_RegistersDeclaration` |
| `SymbolTable` — lookup | `WorkspaceLoader_LoadAsync_SpecializesChain_Registered` |
| `ReferenceResolver` — unresolved ref | `WorkspaceLoader_LoadAsync_UnresolvedReference_ProducesWarning` |
| `ReferenceResolver` — circular import | `WorkspaceLoader_LoadAsync_CircularImport_ProducesWarningNoInfiniteLoop` |
| `SupertypeWalker` — chain walking | `WorkspaceLoader_LoadAsync_SpecializesChain_Registered` |
| `SupertypeWalker` — cyclic detection | `WorkspaceLoader_LoadAsync_CyclicSpecialization_ProducesWarning` |
