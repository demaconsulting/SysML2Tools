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
- A view usage's resolved `expose <name>;` reference is recorded as an `Expose`-kind `SysmlEdge`;
  an unresolved `expose` reference produces a Warning diagnostic naming the unresolved
  identifier and no edge. A view's `render <target>;` member (a rendering style/format selector
  per the SysML v2 grammar, never content) is captured on `SysmlViewNode.RenderTargetName` but
  never inspected by `ReferenceResolver` — no edge is produced and no diagnostic is emitted for
  it, even when the named identifier is not declared anywhere in the file.
- A resolved feature redefinition reference (`redefines X;` / `:>> X`) is recorded as a
  `Redefinition`-kind `SysmlEdge`; an unresolved one produces a Warning diagnostic naming the
  unresolved identifier and no edge, mirroring `FeatureTyping`'s resolution behavior exactly.
- A standalone `dependency A, B to C, D;` declaration resolves each `FromNames`/`ToNames` entry
  independently and emits one `Dependency`-kind `SysmlEdge` per resolved (from, to) pair (a
  cross product); an unresolvable name on either side produces its own Warning diagnostic
  without suppressing edges for the other resolvable names.
- A `bind A = B;` binding connector's endpoints resolve via the same dotted-feature-chain walk
  used for `connect`/`message`, recording a `Binding`-kind `SysmlEdge` only when both endpoints
  resolve.

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
| RenderTargetName captured raw | `WorkspaceLoader_LoadAsync_ViewRenderTarget_CapturedRawNeverResolvedNoDiagnostic` |
| Resolved expose name records edge | `WorkspaceLoader_LoadAsync_ViewUsageWithExpose_RecordsExposeEdge` |
| E2E diagnostic visibility | `RenderSubsystem_ViewsWithDistinctExposeTargets_ProduceDifferingOutputsAndDiagnostic` |
| Resolved redefinition records edge | `WorkspaceLoader_LoadAsync_ResolvedRedefinition_RecordsRedefinitionEdge` |
| Unresolved redefinition — no edge | `WorkspaceLoader_LoadAsync_UnresolvedRedefinition_ProducesWarningNoEdge` |
| Bare-name feature | `WorkspaceLoader_LoadAsync_BareRedefinitionOfInheritedFeature_RecordsRedefinitionEdgeNoWarning` |
| Out-of-order | `WorkspaceLoader_LoadAsync_OutOfOrderRedefinitionChain_RecordsRedefinitionEdgeNoWarning` |
| Cross-file | `WorkspaceLoader_LoadAsync_CrossFileOutOfOrderRedefinitionChain_RecordsRedefinitionEdgeNoWarning` |
| OMG `RedefinitionExample` | `WorkspaceLoader_LoadAsync_RedefinitionExampleFixture_NoUnresolvedReferenceWarnings` |
| OMG PartsTree fixture | `WorkspaceLoader_LoadAsync_1cPartsTreeRedefinitionFixture_NoUnresolvedReferenceWarnings` |
| Dependency binary ends | `WorkspaceLoader_LoadAsync_DependencyBinaryEnds_RecordsDependencyEdge` |
| Dependency comma-list cross product | `WorkspaceLoader_LoadAsync_DependencyCommaLists_RecordsCrossProductEdges` |
| Dependency unresolved end | `WorkspaceLoader_LoadAsync_DependencyUnresolvedEnd_ProducesWarningNoEdge` |
| Dependency OMG corpus fixtures | `Dependency_OmgCorpusFixtures_ResolveExpectedEdges` |
| Binding dotted-chain resolution | `WorkspaceLoader_LoadAsync_BindingDottedChain_RecordsBindingEdge` |
| Binding unresolved end | `WorkspaceLoader_LoadAsync_BindingUnresolvedEnd_ProducesWarningNoEdge` |
| Binding OMG corpus fixture (documented limitation) | `Binding_OmgCorpusFixture_ParsesAndResolvesWithoutCrashing` |
