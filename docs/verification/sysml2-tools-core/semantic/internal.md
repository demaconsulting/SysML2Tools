# Semantic Internal Subsystem Verification

## Verification Approach

Internal semantic components are verified indirectly through `WorkspaceLoaderTests`. There are
no direct unit tests for `AstBuilder`, `SymbolTable`, `ReferenceResolver`, or `SupertypeWalker`
as these are internal implementation details. Their behavior is observable through the public
`WorkspaceLoader.LoadAsync` API.

## Traceability

| Internal Component | Verified By |
| --- | --- |
| `AstBuilder` | All WorkspaceLoaderTests that check Declarations |
| `SymbolTable` | `WorkspaceLoader_LoadAsync_NestedPackages_RegistersQualifiedNames` |
| `ReferenceResolver` | `WorkspaceLoader_LoadAsync_UnresolvedReference_ProducesWarning`, `WorkspaceLoader_LoadAsync_CircularImport_ProducesWarningNoInfiniteLoop` |
| `SupertypeWalker` | `WorkspaceLoader_LoadAsync_SpecializesChain_Registered` |
