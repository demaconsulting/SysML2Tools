#### ReferenceResolver Verification

##### Verification Approach

`ReferenceResolver` is an internal class verified indirectly through `WorkspaceLoaderTests`.
Tests construct files with deliberate unresolved supertype, feature-typing, and import
references and circular import declarations, then call `WorkspaceLoader.LoadAsync` and assert
that the returned diagnostics contain the expected Warning entries and that
`SysmlWorkspace.Index` contains the expected resolved `SysmlEdge` entries. The absence of an
infinite loop is verified implicitly by test completion within the xUnit v3 timeout.

##### Test Environment

Tests run via `dotnet test` against all four target frameworks: net8.0, net9.0, net10.0, and net11.0.
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
  unresolved identifier and no edge. A redefinition never resolves to the redefining feature
  itself or to a private inherited member, and a qualified target resolves even when the owner
  has no general types.
- A private (or, outside a specialization, protected) member is not reachable through a
  qualified name, short name, alias, or `expose` from another package: no edge and an
  `Unresolved reference` Warning. `import all`, references from the owning package, and (for
  protected members) specializations still resolve it.
- A filtered import (`import P::*[@X];`, also recursive) admits only the candidates its filter
  accepts — including a candidate whose metadata is declared in a later file, and metaclass-kind
  filters; a rejected candidate gets no edge and an `Unresolved reference` Warning.
- An import applies only to the namespace declaring it and the namespaces nested in it, an
  import written at the root of a file applies to that file only, and a private import is not
  re-exported.
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
  resolved name. Segments after the first match public members only (visible resolution in the
  previous feature): a private or protected member of the segment's type or of an inherited
  supertype, or a private inline member, gives no edge and an unresolved-reference Warning,
  while a private feature as the *first* segment still resolves locally.
- A `SysmlTransitionNode`'s `Source` that names an inherited pseudostate feature (`"start"`
  or `"done"`) not declared locally on the enclosing state/usage resolves via the new
  `TryResolveInheritedActionMember` fallback — including the narrow last-resort case where no
  explicit supertype is declared at all, resolving against the standard library's
  `Actions::Action` direct children — producing a `Transition`-kind `SysmlEdge` and no
  unresolved-reference diagnostic. The `Target` side of a transition never receives this
  fallback.

##### Test Scenarios

| Scenario | Verified By |
| --- | --- |
| Unresolved supertype reference | `WorkspaceLoader_LoadAsync_UnresolvedReference_ProducesWarning` |
| Same-named defs, own package | `WorkspaceLoader_LoadAsync_SameNamedDefsInSiblingPackages_ResolveToOwningPackage` |
| Local name before global library name | `WorkspaceLoader_LoadAsync_LocalNameShadowsGlobalLibraryName_ResolvesLocal` |
| Local short name and alias win | `WorkspaceLoader_LoadAsync_LocalShortNameAndAlias_WinOverGlobalNames` |
| Circular import — terminates | `WorkspaceLoader_LoadAsync_CircularImport_ProducesWarningNoInfiniteLoop` |
| Resolved reference — no Warning | `WorkspaceLoader_LoadAsync_SpecializesChain_Registered` |
| Resolved supertype records edge | `WorkspaceLoader_LoadAsync_ResolvedSupertype_RecordsSupertypeEdge` |
| Resolved feature typing records edge | `WorkspaceLoader_LoadAsync_ResolvedFeatureTyping_RecordsTypingEdge` |
| Unresolved typing — Warning, no edge | `WorkspaceLoader_LoadAsync_UnresolvedFeatureTyping_ProducesWarningNoEdge` |
| Wildcard import records edge | `WorkspaceLoader_LoadAsync_WildcardImport_RecordsImportEdge` |
| Named import records edge | `WorkspaceLoader_LoadAsync_NamedImport_RecordsImportEdge` |
| Unresolved import — Warning, no crash | `WorkspaceLoader_LoadAsync_UnresolvedImport_ProducesWarningNoCrash` |
| RenderTargetName captured raw | `WorkspaceLoader_LoadAsync_ViewRenderTarget_CapturedRawNeverResolvedNoDiagnostic` |
| View definition typing edge | `WorkspaceLoader_LoadAsync_ViewConditionsAndViewDefinition_Captured` |
| Resolved expose name records edge | `WorkspaceLoader_LoadAsync_ViewUsageWithExpose_RecordsExposeEdge` |
| E2E diagnostic visibility | `RenderSubsystem_ViewsWithDistinctExposeTargets_ProduceDifferingOutputsAndDiagnostic` |
| Resolved redefinition records edge | `WorkspaceLoader_LoadAsync_ResolvedRedefinition_RecordsRedefinitionEdge` |
| Unresolved redefinition — no edge | `WorkspaceLoader_LoadAsync_UnresolvedRedefinition_ProducesWarningNoEdge` |
| Bare-name feature | `WorkspaceLoader_LoadAsync_BareRedefinitionOfInheritedFeature_RecordsRedefinitionEdgeNoWarning` |
| Out-of-order | `WorkspaceLoader_LoadAsync_OutOfOrderRedefinitionChain_RecordsRedefinitionEdgeNoWarning` |
| Cross-file | `WorkspaceLoader_LoadAsync_CrossFileOutOfOrderRedefinitionChain_RecordsRedefinitionEdgeNoWarning` |
| Later-file ancestor | `WorkspaceLoader_LoadAsync_BareRedefinitionOfInheritedFeatureInLaterFile_Resolves` |
| Never to self | `WorkspaceLoader_LoadAsync_RedefinitionQualifiedTargetOrOnlySelf_ResolvesLenientlyNeverToSelf` |
| Private not redefined | `WorkspaceLoader_LoadAsync_RedefinitionOfPrivateInheritedMember_NoEdgeAndWarning` |
| Private direct member | `WorkspaceLoader_LoadAsync_RedefinitionOfPrivateDirectSupertypeMember_NoEdgeAndWarning` |
| Resolver private | `NameResolver_ResolveRedefinedFeature_PrivateMemberOfDirectGeneral_DoesNotResolve` |
| Chain to private/protected member | `WorkspaceLoader_LoadAsync_FeatureChainToNonPublicMember_NoEdgeAndWarning` |
| Chain to private inline member | `WorkspaceLoader_LoadAsync_FeatureChainToPrivateInlineMember_NoEdgeAndWarning` |
| Chain public ok | `WorkspaceLoader_LoadAsync_FeatureChainToPublicMembersAndPrivateFirstSegment_RecordsConnectEdges` |
| Private qualified | `WorkspaceLoader_LoadAsync_PrivateMemberQualifiedFromOtherPackage_NoEdgeAndWarning` |
| Private short name and alias | `WorkspaceLoader_LoadAsync_PrivateShortNameAndAlias_NotResolvableFromOtherPackage` |
| Protected member, specialization only | `WorkspaceLoader_LoadAsync_ProtectedMember_VisibleToSpecializationOnly` |
| `import all` and own-package access | `WorkspaceLoader_LoadAsync_ImportAllAndOwnPackage_ResolvePrivateMember` |
| Expose of a private member | `WorkspaceLoader_LoadAsync_ExposePrivateMember_NoExposeEdgeAndWarning` |
| Filtered import rejects candidate | `WorkspaceLoader_LoadAsync_FilteredImport_RejectsUnannotatedCandidate` |
| Filtered recursive import | `WorkspaceLoader_LoadAsync_FilteredRecursiveImport_SkipsRejectedFindsDeeperMatch` |
| Later-file metadata | `WorkspaceLoader_LoadAsync_FilteredImportWithMetadataInLaterFile_MatchesQualifiedAnnotation` |
| Metaclass-kind filter | `WorkspaceLoader_LoadAsync_FilteredImportWithMetaclassKind_AdmitsMatchingKindOnly` |
| Import scoped to its package | `WorkspaceLoader_LoadAsync_ImportInSiblingPackage_DoesNotApplyToOtherPackage` |
| Outer import seen by nested package | `WorkspaceLoader_LoadAsync_ImportInOuterPackage_AppliesToNestedPackage` |
| File-root import scoped to its file | `WorkspaceLoader_LoadAsync_FileRootImport_AppliesToItsOwnFileOnly` |
| Top-level supertype via root import | `WorkspaceLoader_LoadAsync_TopLevelSupertypeViaRootImport_ResolvesMembers` |
| Split package, own imports | `WorkspaceLoader_LoadAsync_SameTopLevelNameInTwoFiles_EachUsesItsOwnRootImports` |
| Split package, general types | `WorkspaceLoader_LoadAsync_SameTopLevelNameInTwoFiles_GeneralTypesUseOwnRootImports` |
| Private import not re-exported | `WorkspaceLoader_LoadAsync_PrivateImport_IsNotReExported` |
| OMG `RedefinitionExample` | `WorkspaceLoader_LoadAsync_RedefinitionExampleFixture_NoUnresolvedReferenceWarnings` |
| OMG PartsTree fixture | `WorkspaceLoader_LoadAsync_1cPartsTreeRedefinitionFixture_NoUnresolvedReferenceWarnings` |
| Dependency binary ends | `WorkspaceLoader_LoadAsync_DependencyBinaryEnds_RecordsDependencyEdge` |
| Unresolved + alias | `WorkspaceLoader_LoadAsync_SupertypeUnresolvedBeforeAlias_OnlyAliasEdgeCarriesRawReference` |
| Single-reference edges record the reference text | `WorkspaceLoader_LoadAsync_ResolvedEdges_RecordRawReferenceText` |
| Import edge keeps wildcard markers | `WorkspaceLoader_LoadAsync_ImportForms_RawReferenceKeepsWrittenWildcardMarkers` |
| Expose edge keeps wildcard markers | `WorkspaceLoader_LoadAsync_ExposeForms_RawReferenceKeepsWrittenWildcardMarkers` |
| Raw text falls back to the name | `ReferenceResolver_ImportAndExposeWithoutRawText_FallBackToName` |
| Two-ended edges have no raw reference | `WorkspaceLoader_LoadAsync_TwoEndedEdges_HaveNoRawReference` |
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
| Inherited transition source | `WorkspaceLoader_LoadAsync_TransitionSourceStartFeature_ResolvesToStdlibActionMember` |
| OMG Transitions corpus fixture | `Transition_OmgCorpusFixture_ResolvesAllStatesAndTransitions` |
