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
- A dotted feature-chain endpoint (e.g. `engine.fuelPort`, `rearAxle.leftHalfAxle.axleToWheelPort`)
  resolves segment-by-segment via `TryResolveFeatureChain`/`FindFeatureMember`, producing an
  instance-relative qualified name for the final segment regardless of whether each segment
  resolved via a direct-child match or the type-hierarchy fallback branch: for the dominant
  real-world shape — two sibling features declared directly in their owning `part def`s,
  referenced from an enclosing part via bare usages with no per-instance nested redeclaration —
  every segment resolves via the fallback branch, and the returned qualified name is still
  instance-relative (e.g. `Drone::controller::power`, not the shared port type's own declared
  path), so that two structurally distinct endpoints of the same type never collapse to the same
  resolved name.

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
| Binding OMG corpus fixture | `Binding_OmgCorpusFixture_ResolvesBindingEdgesViaImplicitRedefinitionNames` |
| Connect single-segment endpoints | `WorkspaceLoader_LoadAsync_ConnectionSingleSegmentEndpoints_RecordsConnectEdge` |
| Direct-child chain | `WorkspaceLoader_LoadAsync_ConnectionTwoSegmentChain_ResolvesViaDirectChild` |
| Fallback chain, instance path | `WorkspaceLoader_LoadAsync_ConnectionTwoSegmentChain_ResolvesViaTypingFallback` |
| Mixed chain branches | `WorkspaceLoader_LoadAsync_ConnectionThreeSegmentChain_MixesDirectChildAndTypingFallback` |
| Chain via inherited feature | `WorkspaceLoader_LoadAsync_ConnectionChain_ResolvesInheritedFeatureViaSupertype` |
| Dominant shape, distinct paths | `WorkspaceLoader_LoadAsync_ConnectionDominantShape_ResolvesDistinctInstancePaths` |
| Unresolved chain endpoint | `WorkspaceLoader_LoadAsync_ConnectionUnresolvedEndpoint_ProducesWarningNoEdge` |
| Supertype-cycle chain segment | `WorkspaceLoader_LoadAsync_ConnectionChain_SupertypeCycleTerminatesGracefully` |
| OMG Connections example fixture | `WorkspaceLoader_LoadAsync_ConnectionsExampleFixture_RecordsConnectEdge` |
