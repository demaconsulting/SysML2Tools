# WorkspaceLoader Verification

## Verification Approach

`WorkspaceLoader` is verified through 9 progressive-level tests in `WorkspaceLoaderTests`:

| Test | Level | Assertion |
| --- | --- | --- |
| `WorkspaceLoader_LoadAsync_EmptyFile_ReturnsNonNullWorkspace` | 1 | Empty file returns non-null workspace with `HasErrors = false` |
| `WorkspaceLoader_LoadAsync_SinglePackage_RegistersDeclaration` | 2 | Single package name registered |
| `WorkspaceLoader_LoadAsync_NestedPackages_RegistersQualifiedNames` | 3 | Nested package qualified names registered |
| `WorkspaceLoader_LoadAsync_PartDef_RegistersDefinition` | 4 | Part def qualified name registered |
| `WorkspaceLoader_LoadAsync_NoFiles_ReturnsNonNullWorkspace` | 5 | No-files load returns non-null workspace |
| `WorkspaceLoader_LoadAsync_StdlibDeclarations_Registered` | 6 | Stdlib contributes declarations without errors |
| `WorkspaceLoader_LoadAsync_SpecializesChain_Registered` | 7 | Resolved supertype produces no unresolved warning |
| `WorkspaceLoader_LoadAsync_UnresolvedReference_ProducesWarning` | 8 | Unresolved supertype produces Warning |
| `WorkspaceLoader_LoadAsync_CircularImport_ProducesWarningNoInfiniteLoop` | 9 | Circular import produces Warning, completes in finite time |

## Level 10 Gate

`SemanticOmgModels_AllModels_ResolveWithZeroErrors` confirms that all OMG model files in
`test/SysMLModels/` load with zero Error-level diagnostics.
