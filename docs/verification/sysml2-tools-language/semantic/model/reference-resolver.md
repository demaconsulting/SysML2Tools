#### ReferenceResolver Verification

##### Verification Approach

`ReferenceResolver` is an internal class verified indirectly through `WorkspaceLoaderTests`.
Tests construct files with deliberate unresolved supertype, feature-typing, and import
references and circular import declarations (which must stay silent), then call `WorkspaceLoader.LoadAsync` and assert
that the returned diagnostics contain the expected Warning entries (Error entries with
`strictUnresolved`) and that
`SysmlWorkspace.Index` contains the expected resolved `SysmlEdge` entries. The absence of an
infinite loop is verified implicitly by test completion within the xUnit v3 timeout.

##### Test Environment

Tests run via `dotnet test` against all four target frameworks: net8.0, net9.0, net10.0, and net11.0.
Temporary `.sysml` files are created in `Path.GetTempPath()` and deleted after each test. No
external services or additional configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- An unresolved supertype name produces exactly one `Warning`-severity diagnostic per file
  containing that name; with `strictUnresolved: true` the same diagnostic (same message and
  position) is an `Error`. The default severity is `Warning`
  (`WorkspaceLoader.DefaultUserUnresolvedSeverity`). The standard library seed is never re-resolved
  at load time and adds no diagnostics, and the StdlibGen tool, which compiles the library,
  reports its unresolved references as Warnings.
- A cyclic specialization remains a `Warning` with `strictUnresolved` either way, and a circular
  import produces no diagnostic at all.
- Packages that `public import` each other (two packages, or a cycle of three) produce no
  diagnostic, `LoadAsync` returns (does not hang), and a type declared in any package of the cycle
  resolves from every other one, in both directions around the cycle.
- A name reachable only through a cycle resolves for every package that imports a member of the
  cycle whichever of those packages is resolved first (a lookup cut short by the cycle is not
  remembered as a miss); a name that exists nowhere is reported once as unresolved.
- A resolved supertype name (registered in `SymbolTable`) produces no Warning diagnostic and
  is recorded as a `Supertype`-kind `SysmlEdge`.
- A resolved feature typing reference is recorded as a `Typing`-kind `SysmlEdge`; an
  unresolved one produces a Warning diagnostic and no edge.
- A resolved import reference (wildcard or named) is recorded as an `Import`-kind `SysmlEdge`;
  an unresolved import reference produces a Warning diagnostic without crashing.
- A view usage's resolved `expose <name>;` reference is recorded as an `Expose`-kind `SysmlEdge`;
  an unresolved `expose` reference produces a Warning diagnostic naming the unresolved
  identifier and no edge. A view's `render <target>;` member (a rendering style/format selector
  per the SysML v2 grammar, never content) is captured on `SysmlViewNode.RenderTargetName` and
  produces no edge; the reference it writes is resolved in the view's own namespace as one of the
  view's `ReferenceOccurrences`, so `render asTreeDiagram;` with `import Views::*;` (anywhere in
  the view's namespace chain, including the view's own body), or `render Views::asTreeDiagram;`,
  is silent, while an unqualified target with no import produces a Warning at the target token.
- Each reference written inside an expression — a transition's guard, trigger payload (type,
  `via` target) and effect, an action-flow `accept`/`send` node's payload and `via`/`to` operands,
  an action-flow decision guard, a state `entry`/`do`/`exit` action's declaration, and a
  constraint or requirement-constraint expression — that does not resolve produces an
  `Unresolved reference` Warning at that token's position, once per occurrence (the same name
  written twice is reported twice) and with no edge; one that resolves (a declared feature, a
  bare `in` parameter, a named `accept` payload used in its own guard, a body's own parameters)
  produces nothing. Operators, literals, `self`, library functions (`size(…)`, `isEmpty(…)`,
  `->forAll{…}`), selected members, and named-argument names are never reported.
- A view's `render` target naming a SysML2Tools extension selector (`asGeneralDiagram`,
  `asStateTransitionDiagram`, `asActionFlowDiagram`, `asSequenceDiagram`, `asGridDiagram`) that
  does not resolve to a declaration produces exactly one `Warning` at the token — never an
  `Error`, and unchanged by `strictUnresolved` — reading `'asGeneralDiagram' is a SysML2Tools
  extension, not a standard SysML v2 rendering; the model will not validate in other tools. Use
  a standard view definition instead (e.g. 'view v : StandardViewDefinitions::GeneralView').`
  Each occurrence is reported. The standard selectors (`Views::asTreeDiagram`,
  `Views::asInterconnectionDiagram`, `asElementTable`, `asTextualNotation`, in qualified,
  imported, or bare form) produce no diagnostic; a selector the model declares itself
  (`rendering asGridDiagram`) resolves normally with no warning; and the qualified
  `Views::asGeneralDiagram` is an ordinary unresolved reference because `Views` has no such
  member, an `Error` with `strictUnresolved`
  (`WorkspaceLoader_LoadAsync_ToolRenderSelectorQualifiedWithStrictUnresolved_ReportsUnresolvedErrorNotPortability`).
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
- A `bind A = B;` binding connector's endpoints resolve through the same feature-chain
  resolution used for `connect`/`message`, recording a `Binding`-kind `SysmlEdge` only when both
  endpoints resolve.
- A dotted feature-chain reference (e.g. `engine.fuelPort`, `rearAxle.leftHalfAxle.axleToWheelPort`,
  `'Solve for Pressure1'.rho`) is resolved segment by segment by `NameResolver` wherever a
  reference may be written: connect/message/bind endpoints, transition ends, supertype and
  subsetting targets, redefinition targets, `satisfy`, `verify` and `allocate` ends. A segment
  may be reached through the previous feature's subsetting or redefinition, an alias, a
  conjugated port type, or an inline member. Relationship edges (`Connect`, `Binding`,
  `Transition`, `Allocate`, `Satisfy`, `Verify`) record the chain's instance-relative qualified
  name — the first segment's element then the simple name of each later segment's element,
  whether the segment was found among the feature's own members, its type's, or through
  specialization — and the type-like edges (`Supertype`, `Redefinition`) record the declared
  element. For the dominant real-world shape — two sibling features declared directly in their
  owning `part def`s, referenced from an enclosing part via bare usages with no per-instance
  nested redeclaration — the returned qualified name is still instance-relative (e.g.
  `Drone::controller::power`, not the shared port type's own declared path), so that two
  structurally distinct endpoints of the same type never collapse to the same resolved name.
  Segments after the first match public members only (visible resolution in the
  previous feature): a private or protected member of the segment's type or of an inherited
  supertype, or a private inline member, gives no edge and an unresolved-reference Warning,
  while a private feature as the *first* segment still resolves locally. A chain with an
  unresolvable segment is reported once, as the whole chain text, with no edge.
- A succession end that names an anonymous action node (`then fork;`, `decide;`, `terminate;`)
  produces no unresolved-reference diagnostic and no `Transition` edge for that end.
- A `satisfy requirement r : R by s;` usage satisfies `R` (its typing), not the name it declares.
- A `SysmlTransitionNode`'s `Source` or `Target` that names an inherited pseudostate feature
  (`"start"` or `"done"`) not declared locally on the enclosing state/action resolves via the
  `TryResolveInheritedActionMember` fallback — including the narrow last-resort case where no
  explicit supertype is declared at all, resolving against the standard library's
  `Actions::Action` direct children — producing a `Transition`-kind `SysmlEdge` and no
  unresolved-reference diagnostic (`first start then x;`, `then done;`, and
  `transition first x then done;` in action and state bodies). A name that is not `start`/`done`
  and does not resolve (`then missing;`), and `done` outside a state or action, still produce the
  warning. The target-side fallback never looks up any other inherited member (a private member
  of a supertype as a target still warns). Connection and binding endpoints never receive this
  fallback.
- Every unresolved reference — supertype, subsetting, typing, redefinition, import, `expose`,
  view definition, connector (`connect`/`bind`) endpoint, transition target, metadata type,
  dependency, allocation, `satisfy`, and `verify` — is reported at the one-based line and
  one-based column of the reference text as written (not `(0,0)`); two references on different
  lines and in different files are reported at their own positions and files; and a circular
  import is reported at the import statement that closes the cycle, in its own file.
- Every redefinition target of a feature (`:>> a :>> b`) gets its own `Redefinition` edge, an
  unresolved second target is reported once at its own token, and the members of an anonymous usage
  that has a body resolve in that usage's own scope (through the usage it subsets).
- Loading the OMG corpus in strict mode reports exactly the five explained unresolved references
  (all from user-defined keywords whose members come from semantic metadata), and the four
  root-level names that corpus files declare twice are reported as duplicate-declaration warnings
  (never errors), including the `Camera` collision between a `part def` and a `package`, whose
  package is ignored so that none of its references is reported.

##### Test Scenarios

| Scenario | Verified By |
| --- | --- |
| Position per unresolved reference kind | `WorkspaceLoader_LoadAsync_UnresolvedReference_ReportsSourcePosition` |
| Distinct positions per reference | `WorkspaceLoader_LoadAsync_UnresolvedReferences_ReportDistinctPositions` |
| Position names the referencing file | `WorkspaceLoader_LoadAsync_UnresolvedReferencesInTwoFiles_ReportEachFilePath` |
| Unresolved supertype reference | `WorkspaceLoader_LoadAsync_UnresolvedReference_ProducesWarning` |
| `strictUnresolved` — Errors | `WorkspaceLoader_LoadAsync_StrictUnresolved_UnresolvedReferenceKinds_ProduceErrors` |
| Expression Warning | `WorkspaceLoader_LoadAsync_UnresolvedExpressionReference_ProducesWarningAtToken` |
| Strict expression | `WorkspaceLoader_LoadAsync_StrictUnresolved_UnresolvedExpressionReference_ProducesErrorAtToken` |
| Default severity is Warning | `WorkspaceLoader_DefaultUnresolvedSeverity_IsWarning` |
| Warning resolver severity | `ReferenceResolver_ResolveAll_WarningSeverity_UnresolvedReferenceIsWarning` |
| Cycle diagnostics | `WorkspaceLoader_LoadAsync_CyclicSpecialization_RemainsWarning` |
| Stdlib references, no diagnostics | `WorkspaceLoader_LoadAsync_StdlibReferences_ProduceNoDiagnostics` |
| Stdlib compile stays Warning | `GetSymbolTable_Diagnostics_UnresolvedReferences_RemainWarnings` |
| Stdlib compile: no errors, warnings at most 13 | `GetSymbolTable_Diagnostics_WarningCount_DoesNotExceedCeiling` |
| Valid custom and test-case models load clean | `CustomAndTestCaseModels_AllModels_LoadWithZeroDiagnostics` |
| OMG corpus within baseline (5 errors) | `SemanticOmgModels_StrictUnresolved_DoNotExceedBaseline` |
| Selector Warning at token | `WorkspaceLoader_LoadAsync_ToolRenderSelector_ProducesPortabilityWarningAtToken` |
| Standard selectors — no diagnostic | `WorkspaceLoader_LoadAsync_StandardRenderSelectors_ProduceNoDiagnostics` |
| Selector + strict | `WorkspaceLoader_LoadAsync_ToolRenderSelectorWithStrictUnresolved_StillOnlyPortabilityWarning` |
| Tool selector used twice — each reported | `WorkspaceLoader_LoadAsync_ToolRenderSelectorTwice_ReportsEachOccurrence` |
| User-declared selector | `WorkspaceLoader_LoadAsync_ToolRenderSelectorUserDeclaredRendering_NoPortabilityWarning` |
| Qualified selector | `WorkspaceLoader_LoadAsync_ToolRenderSelectorQualified_ReportsUnresolvedNotPortability` |
| Five selectors | `WorkspaceLoader_LoadAsync_ToolRenderSelectorsDeclared_NoUnresolvedWarningOnlyPortabilityWarnings` |
| Same-named defs, own package | `WorkspaceLoader_LoadAsync_SameNamedDefsInSiblingPackages_ResolveToOwningPackage` |
| Local name before global library name | `WorkspaceLoader_LoadAsync_LocalNameShadowsGlobalLibraryName_ResolvesLocal` |
| Local short name and alias win | `WorkspaceLoader_LoadAsync_LocalShortNameAndAlias_WinOverGlobalNames` |
| Circular import — silent | `WorkspaceLoader_LoadAsync_CircularImport_ProducesNoDiagnosticsAndTerminates` |
| Mutual public import — names resolve both ways | `WorkspaceLoader_LoadAsync_MutualPublicImport_ResolvesBothWays` |
| Three-package cycle | `WorkspaceLoader_LoadAsync_ThreePackageImportCycle_ResolvesNamesAroundTheCycle` |
| Cycle, any resolution order | `WorkspaceLoader_LoadAsync_CircularImport_ResolvesThroughCycleRegardlessOfOrder` |
| Unresolved name through a cycle — once | `WorkspaceLoader_LoadAsync_CircularImport_UnresolvedNameReportedOnce` |
| Resolved reference — no Warning | `WorkspaceLoader_LoadAsync_SpecializesChain_Registered` |
| Resolved supertype records edge | `WorkspaceLoader_LoadAsync_ResolvedSupertype_RecordsSupertypeEdge` |
| Resolved feature typing records edge | `WorkspaceLoader_LoadAsync_ResolvedFeatureTyping_RecordsTypingEdge` |
| Unresolved typing — Warning, no edge | `WorkspaceLoader_LoadAsync_UnresolvedFeatureTyping_ProducesWarningNoEdge` |
| Wildcard import records edge | `WorkspaceLoader_LoadAsync_WildcardImport_RecordsImportEdge` |
| Named import records edge | `WorkspaceLoader_LoadAsync_NamedImport_RecordsImportEdge` |
| Unresolved import — Warning, no crash | `WorkspaceLoader_LoadAsync_UnresolvedImport_ProducesWarningNoCrash` |
| RenderTargetName, no edge | `WorkspaceLoader_LoadAsync_ViewRenderTarget_CapturedRawResolvedWithoutEdge` |
| Unresolved at token positions | `WorkspaceLoader_LoadAsync_UnresolvedReference_ReportsSourcePosition` |
| Declared — no Warning | `WorkspaceLoader_LoadAsync_ExpressionReferencesDeclared_NoUnresolvedWarning` |
| No false positives | `WorkspaceLoader_LoadAsync_LibraryFunctionsAndOperatorsInExpressions_NoUnresolvedWarning` |
| Reported per occurrence | `WorkspaceLoader_LoadAsync_UnresolvedExpressionReferenceTwice_ReportsEachOccurrence` |
| Pilot parity (elevator) | `WorkspaceLoader_LoadAsync_ElevatorStateModel_ReportsUndeclaredNamesAtTokens` |
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
| Chain via subsetting usage | `WorkspaceLoader_LoadAsync_ConnectionChainThroughSubsettingUsage_RecordsConnectEdge` |
| Chain ending at an alias | `WorkspaceLoader_LoadAsync_ConnectionChainToAlias_RecordsEdgeAtTargetInstancePath` |
| Chain via conjugated port | `WorkspaceLoader_LoadAsync_ConnectionChainThroughConjugatedPort_RecordsConnectEdge` |
| Quoted segment containing a dot | `WorkspaceLoader_LoadAsync_ChainWithQuotedDottedSegment_ResolvesBind` |
| Subsetting target is a chain | `WorkspaceLoader_LoadAsync_SubsettingTargetChain_ResolvesSupertypeEdge` |
| Redefinition target is a chain | `WorkspaceLoader_LoadAsync_RedefinitionTargetChain_ResolvesRedefinitionEdge` |
| Allocation ends are chains | `WorkspaceLoader_LoadAsync_AllocateChains_RecordAllocateEdge` |
| Satisfy subject is a chain | `WorkspaceLoader_LoadAsync_SatisfyFeatureChainSubject_RecordsSatisfyEdge` |
| Satisfy declares a usage | `WorkspaceLoader_LoadAsync_SatisfyDeclaredRequirementUsage_ResolvesTypingNoWarning` |
| Verify reference is a chain | `WorkspaceLoader_LoadAsync_VerifyChain_RecordsVerifyEdge` |
| Unresolvable middle segment | `WorkspaceLoader_LoadAsync_ChainUnresolvedMiddleSegment_NoEdgeAndWarning` |
| Specialization cycle through a chain | `WorkspaceLoader_LoadAsync_ChainSpecializationCycle_Terminates` |
| Anonymous fork/decide ends | `WorkspaceLoader_LoadAsync_AnonymousForkDecideTerminate_NoUnresolvedDiagnostic` |
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
| Own root imports per file | `WorkspaceLoader_LoadAsync_TwoFilesWithRootImports_EachUsesItsOwnRootImports` |
| General types from own imports | `WorkspaceLoader_LoadAsync_TwoFilesWithRootImports_GeneralTypesUseOwnRootImports` |
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
| `start` source in an action body | `WorkspaceLoader_LoadAsync_StartSource_InActionBody_ResolvesToStdlibActionMember` |
| `then done;` and `then done` transition target | `WorkspaceLoader_LoadAsync_DoneTarget_ResolvesToStdlibActionMember` |
| Unknown succession target still warns | `WorkspaceLoader_LoadAsync_UnknownSuccessionTarget_StillWarnsButDoneDoesNot` |
| `done` outside a state/action still warns | `WorkspaceLoader_LoadAsync_DoneTarget_OutsideActionOrState_StillWarns` |
| Private member as target warns | `WorkspaceLoader_LoadAsync_PrivateInheritedMemberAsSuccessionTarget_StillWarns` |
| Part def, no fallback | `WorkspaceLoader_LoadAsync_DoneTarget_InPartDefSpecializingPart_DoesNotUseActionFallback` |
| Part def, implicit `Parts::Part` | `WorkspaceLoader_LoadAsync_DoneTarget_InPartDefWithImplicitPart_Resolves` |
| State start/done keep legacy targets | `WorkspaceLoader_LoadAsync_StateStartAndDone_KeepLegacyActionTargets` |
| User-declared `start` kept | `WorkspaceLoader_LoadAsync_UserDeclaredStartState_IsNotCanonicalized` |
| Gallery edge lists equal the baseline golden files | `SemanticGalleryModels_Edges_MatchBaselineGolden` |
| OMG Transitions corpus fixture | `Transition_OmgCorpusFixture_ResolvesAllStatesAndTransitions` |
| Every redefinition target resolved | `AstBuilder_MultipleRedefinitions_ResolvesEveryTargetAndInheritsFromSecond` |
| Unresolved second target | `AstBuilder_MultipleRedefinitions_UnresolvedSecondTarget_ReportsOneDiagnostic` |
| Anonymous owner scope | `AstBuilder_AnonymousSubsettingOwner_ResolvesRedefinedMember` |
| OMG corpus strict baseline and explained errors | `SemanticOmgModels_StrictUnresolved_DoNotExceedBaseline` |
| `Camera` duplicate | `SemanticOmgModels_CameraPackage_CollidingWithPartDef_ReportsDuplicateNotUnresolved` |
| Corpus duplicates | `SemanticOmgModels_DuplicateRootNames_AreExactlyTheKnownCorpusPairs` |
| Corpus diagnostics independent of file order | `SemanticOmgModels_StrictUnresolved_IndependentOfFileOrder` |
