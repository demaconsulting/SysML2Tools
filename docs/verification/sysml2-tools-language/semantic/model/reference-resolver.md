#### ReferenceResolver Verification

##### Verification Approach

`ReferenceResolver` is an internal class verified indirectly through `WorkspaceLoaderTests`.
Tests construct files with deliberate unresolved supertype, feature-typing, and import
references and circular import declarations, then call `WorkspaceLoader.LoadAsync` and assert
that the returned diagnostics contain the expected Warning entries and that
`SysmlWorkspace.Index` contains the expected resolved `SysmlEdge` entries. The absence of an
infinite loop is verified implicitly by test completion within the xUnit v3 timeout.

##### Test Environment

Tests run via `dotnet test` against all three target frameworks: net8.0, net9.0, and net10.0.
Temporary `.sysml` files are created in `Path.GetTempPath()` and deleted after each test. No
external services or additional configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- An unresolved supertype name produces exactly one `Warning`-severity diagnostic per file
  containing that name.
- A circular import chain between two files produces a `Warning`-severity diagnostic and
  `LoadAsync` returns (does not hang).
- A resolved supertype name (registered in `SymbolTable`) produces no Warning diagnostic and
  is recorded as a `Supertype`-kind `SysmlEdge`.
- A resolved feature typing reference is recorded as a `Typing`-kind `SysmlEdge`; an
  unresolved one produces a Warning diagnostic and no edge.
- A resolved import reference (wildcard or named) is recorded as an `Import`-kind `SysmlEdge`;
  an unresolved import reference produces a Warning diagnostic without crashing.
- A view's resolved `render <target>;` reference is recorded as a `Render`-kind `SysmlEdge`; an
  unresolved render target produces a Warning diagnostic naming the unresolved identifier and no
  edge — the exact fix for the reported bug (previously, an unresolved render target rendered
  the full workspace silently with no diagnostic).
- A view usage's resolved `expose <name>;` reference is recorded as an `Expose`-kind `SysmlEdge`.

##### Test Scenarios

| Scenario | Verified By |
| --- | --- |
| Unresolved supertype reference | `WorkspaceLoader_LoadAsync_UnresolvedReference_ProducesWarning` |
| Circular import — terminates | `WorkspaceLoader_LoadAsync_CircularImport_ProducesWarningNoInfiniteLoop` |
| Resolved reference — no Warning | `WorkspaceLoader_LoadAsync_SpecializesChain_Registered` |
| Resolved supertype records edge | `WorkspaceLoader_LoadAsync_ResolvedSupertype_RecordsSupertypeEdge` |
| Resolved feature typing records edge | `WorkspaceLoader_LoadAsync_ResolvedFeatureTyping_RecordsTypingEdge` |
| Unresolved typing — Warning, no edge | `WorkspaceLoader_LoadAsync_UnresolvedFeatureTyping_ProducesWarningNoEdge` |
| Wildcard import records edge | `WorkspaceLoader_LoadAsync_WildcardImport_RecordsImportEdge` |
| Named import records edge | `WorkspaceLoader_LoadAsync_NamedImport_RecordsImportEdge` |
| Unresolved import — Warning, no crash | `WorkspaceLoader_LoadAsync_UnresolvedImport_ProducesWarningNoCrash` |
| Resolved render target records edge | `WorkspaceLoader_LoadAsync_ViewRenderTargetResolves_RecordsRenderEdge` |
| Unresolved render target, no edge | `WorkspaceLoader_LoadAsync_ViewRenderTargetUnresolved_ProducesWarningNoEdge` |
| Resolved expose name records edge | `WorkspaceLoader_LoadAsync_ViewUsageWithExpose_RecordsExposeEdge` |
| E2E diagnostic visibility | `RenderSubsystem_ViewsWithDistinctRenderTargets_ProduceDifferingOutputsAndDiagnostic` |
