#### AstBuilder Verification

##### Verification Approach

`AstBuilder` is an internal class with no public surface and is verified indirectly through
`WorkspaceLoaderTests` plus the focused `AstBuilderMetadataTests`. Tests call
`WorkspaceLoader.LoadAsync` with controlled `.sysml` source files and assert that the returned
`SysmlLoadResult.Workspace.Declarations` and nested node data contain the expected names,
metadata annotations, and raw filter text.

##### Test Environment

Tests run via `dotnet test` against all three target frameworks: net8.0, net9.0, and net10.0.
Temporary `.sysml` files are created in `Path.GetTempPath()` and deleted after each test. No
external services or additional configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- A single package `package Foo {}` registers `"Foo"` in `Declarations`.
- Nested packages `package Foo { package Bar {} }` register both `"Foo"` and `"Foo::Bar"`.
- A part definition `part def MyPart {}` inside `Foo` registers `"Foo::MyPart"`.
- An element with only a short name `< shortName >` (no declared name) is not registered.
- A definition with `specializes KnownType` produces a `SupertypeNames` entry that resolves
  without a Warning when `KnownType` is registered.
- A usage/feature's own usage-level `subsets`/`:>` clause (distinct from a definition's
  `specializes`/`:>` supertype clause) directly populates that feature node's `SupertypeNames`
  with the expected target name.
- A metadata annotation in an element body is captured as a `SysmlMetadataNode` child with its
  raw type reference and any supported literal attribute values.
- `VisitViewDefinition` captures `render <target>;` and `filter [<expr>];` members' raw text on
  the corresponding `SysmlViewNode`, and leaves both null for a view with an empty body.
- `VisitViewUsage` (a named `view` usage, not a `view def` definition) captures the same
  render/filter members plus `expose <name>;` members, producing a `SysmlViewNode` with
  populated `ExposeMembers` (each entry pairing its qualified-name reference text, its own
  bracket-filter expression text (or null when absent), and its `ExposeRecursionKind`
  classification). This also makes every named `view` usage its own renderable declaration, an
  intentional capability addition beyond `expose` capture alone (see the ast-builder design
  doc).
- Each `expose` member is classified into the correct `ExposeRecursionKind` per its grammar form
  and recursion setting: a bare `expose X;` as `MembershipExact`, a recursive `expose X::**;` as
  `MembershipRecursive`, a bare namespace `expose X::*;` as `NamespaceDirectChildren`, and a
  recursive namespace `expose X::*::**;` as `NamespaceRecursive` — fixing the prior defect where
  the namespace-import branch's recursion bit was hard-coded away instead of checking for a
  trailing `::**`.
- `BuildUsageNode` captures a feature's redefinition reference on `RedefinedFeatureName` for both
  the `redefines` keyword form and the `:>>` operator form, for both a bare simple name and a
  qualified `Owner::feature` form (captured verbatim, unresolved), and leaves it null for a
  feature that declares no redefinition.
- An implicitly-named redefining usage (e.g. `port redefines fuelTankPort { item redefines
  fuelSupply; }`, with no explicit declared name) is assigned an `effectiveName`/`QualifiedName`
  derived from the redefined feature's simple name, making it resolvable by later references
  (including `bind` connector ends) instead of remaining anonymous. When the `redefines`
  reference itself is a dot-chained feature path (e.g. `tank.fuelTankPort`), only the trailing
  segment is used, not the whole dotted reference text.
- `VisitDependency` splits a `dependency A, B to C, D;` declaration's flat qualified-name list
  into `FromNames`/`ToNames` correctly, including the FROM-keyword-omitted shape
  (`dependency z to x, y;`), and resolves to the expected cross-product `Dependency` edges (or a
  Warning with no edge for an unresolvable name).
- `VisitBindingConnectorAsUsage` captures a `bind A = B;` binding connector's two endpoints on a
  `SysmlConnectionNode` with `ConnectionKeyword == "binding"`, resolved via the same
  dotted-feature-chain walk as `connect`, producing a `Binding` edge when both sides resolve (or
  a Warning with no edge otherwise).
- `VisitStateBodyItem` synthesizes an attached `SysmlTransitionNode` for the
  `behaviorUsageMember (targetTransitionUsageMember)*` shape (e.g. `state off; accept Signal
  then starting;`), including the repeated (`*`) case, and for the
  `entryActionMember (entryTransitionMember)*` shape (e.g. `entry action initial; then off;`),
  in each case in addition to building the preceding usage/entry-action node itself.
- A named entry action (the OMG Annex A.7-preferred separate `entry action initial; ...
  transition initial then off;` form) registers a resolvable feature — no "Unresolved reference:
  'initial'" diagnostic is produced.
- An unnamed entry-action reference form (e.g. `entry performSelfTest{...}`) produces a feature
  node with `Name == null` and does not crash.
- `VisitStateUsage` populates `FeatureTyping` from an explicit `state usage : Type { ... }` form,
  recording the expected `Typing` edge (previously always dropped for state usages).
- A transition source named `"start"` with no locally-declared `start` feature resolves against
  the standard library's `Actions::Action` via the new inherited-pseudostate-feature fallback,
  producing a `Transition` edge and no unresolved-reference diagnostic.
- The real OMG corpus fixture `training/25.Transitions/TransitionActions.sysml` parses with no
  unresolved-reference diagnostics for `start`/`off`/`starting`/`on`, and produces the exact
  expected declared-state and resolved-transition counts, including entry/do/exit action feature
  nodes on the `on` state.
- `VisitEnumeratedValue` captures each `enum def` literal (bare, value-assignment, and
  redefinition-body forms) as an `"enum value"`-keyword feature, in source order, via the
  dedicated `CollectEnumerationBodyChildren` helper.
- `VisitRequirementDefinition`/`VisitConcernDefinition` capture `subject`/`actor`/`stakeholder`/
  `require constraint`/`assume constraint` members as `Children`; `VisitRequirementUsage`/
  `VisitConcernUsage` capture the same members when nested in a requirement/concern *usage*
  instead of a definition (the dominant real-corpus idiom).
- A `require constraint`/`assume constraint` member's `ExpressionText` captures its raw
  calculation-body expression text, for both the reference form and the inline form.
- `VisitConstraintUsage` (previously entirely absent) captures a top-level `constraint { expr }`
  usage's raw expression text; `VisitConstraintDefinition` synthesizes one child feature node
  carrying its own calculation-body expression text.
- A `verify`/`frame` member's own nested `requirementBody` content is not spuriously hoisted onto
  the enclosing requirement/concern's `Children`, while the verify target itself remains captured
  via `VerifiedRequirementNames` — a regression guard for the null-suppression safeguard.
- The real OMG corpus fixtures `training/06.EnumerationDefinitions/EnumerationDefinitions-{1,2}.
  sysml`, `training/32.Requirements/RequirementDefinitions.sysml` and `RequirementUsages.sysml`,
  and `examples/CommentExamples/Comments.sysml`/`training/01.Packages/DocumentationExample.sysml`
  parse with no error diagnostics and produce the expected enum-value/subject/constraint/
  annotation captures.

##### Test Scenarios

| Scenario | Verified By |
| --- | --- |
| Simple name extraction | `WorkspaceLoader_LoadAsync_SinglePackage_RegistersDeclaration` |
| Qualified name from namespace stack | `WorkspaceLoader_LoadAsync_NestedPackages_RegistersQualifiedNames` |
| Definition registration | `WorkspaceLoader_LoadAsync_PartDef_RegistersDefinition` |
| Supertype extraction | `WorkspaceLoader_LoadAsync_SpecializesChain_Registered` |
| Usage-level `subsets`/`:>` capture | `WorkspaceLoader_LoadAsync_UsageLevelSubsetting_PopulatesSupertypeNames` |
| Metadata annotation capture | `AstBuilder_BareMetadataAnnotation_CapturesMetadataNode` |
| Metadata literal attribute capture | `AstBuilder_MetadataAnnotationWithBooleanAttribute_CapturesLiteralValue` |
| `VisitViewDefinition` render | `WorkspaceLoader_LoadAsync_ViewRenderTarget_CapturedRawNeverResolvedNoDiagnostic` |
| `VisitViewDefinition` filter capture | `WorkspaceLoader_LoadAsync_ViewFilterExpression_CapturesTextVerbatimNoEdge` |
| `VisitViewUsage` expose capture | `WorkspaceLoader_LoadAsync_ViewUsageWithExpose_RecordsExposeEdge` |
| `VisitViewUsage` bracket-filter capture | `AstBuilder_ExposeBracketFilter_CapturesRawText` |
| Bracket filter paired to entry | `AstBuilder_MultipleExposeMembers_OnlyOneBracketed_PairsFilterWithCorrectPath` |
| Bare MembershipExpose classification | `AstBuilder_ExposeBareMembership_CapturesMembershipExact` |
| Recursive MembershipExpose classification | `AstBuilder_ExposeRecursiveMembership_CapturesMembershipRecursive` |
| Bare NamespaceExpose classification | `AstBuilder_ExposeNamespaceDirectChildren_CapturesNamespaceDirectChildren` |
| Recursive NamespaceExpose classification | `AstBuilder_ExposeNamespaceRecursive_CapturesNamespaceRecursive` |
| Bracket-filtered classification | `WorkspaceLoader_LoadAsync_OmgSafetyFeatureViewsFixture_ResolvesBracketedExpose` |
| `VisitViewUsage` renderable declaration | `RenderSubsystem_OmgSafetyFeatureViewsCorpus_RendersAllNamedViewUsages` |
| Empty view body regression guard | `WorkspaceLoader_LoadAsync_ViewEmptyBody_AllNewFieldsNullOrEmpty` |
| Redefinition, `redefines` keyword | `WorkspaceLoader_LoadAsync_RedefinesKeyword_CapturesRedefinedFeatureName` |
| Redefinition capture, `:>>` operator | `WorkspaceLoader_LoadAsync_ColonGtGtOperator_CapturesRedefinedFeatureName` |
| Redefinition capture, qualified form | `WorkspaceLoader_LoadAsync_QualifiedRedefinition_CapturesRawText` |
| No redefinition leaves field null | `WorkspaceLoader_LoadAsync_NoRedefinition_RedefinedFeatureNameIsNull` |
| Implicit redefinition name | `WorkspaceLoader_LoadAsync_BindingViaImplicitlyNamedRedefinedUsage_RecordsBindingEdge` |
| Dotted redefinition name | `WorkspaceLoader_LoadAsync_ImplicitNameFromDottedRedefinitionChain_UsesTrailingSegment` |
| Dependency binary ends | `WorkspaceLoader_LoadAsync_DependencyBinaryEnds_RecordsDependencyEdge` |
| Dependency comma-list cross product | `WorkspaceLoader_LoadAsync_DependencyCommaLists_RecordsCrossProductEdges` |
| Dependency unresolved end | `WorkspaceLoader_LoadAsync_DependencyUnresolvedEnd_ProducesWarningNoEdge` |
| Dependency OMG corpus fixtures | `Dependency_OmgCorpusFixtures_ResolveExpectedEdges` |
| Binding dotted-chain resolution | `WorkspaceLoader_LoadAsync_BindingDottedChain_RecordsBindingEdge` |
| Binding unresolved end | `WorkspaceLoader_LoadAsync_BindingUnresolvedEnd_ProducesWarningNoEdge` |
| Binding OMG corpus fixture | `Binding_OmgCorpusFixture_ResolvesBindingEdgesViaImplicitRedefinitionNames` |
| Attached transition (self-loop) | `WorkspaceLoader_LoadAsync_AttachedTransitionAfterState_ResolvesSelfLoopEdge` |
| Multiple attached transitions | `WorkspaceLoader_LoadAsync_MultipleAttachedTransitionsAfterState_CapturesAll` |
| Entry action | `WorkspaceLoader_LoadAsync_EntryActionWithAttachedTransition_CapturesEntryFeatureAndTransition` |
| Named entry action registers feature | `WorkspaceLoader_LoadAsync_NamedEntryAction_RegistersResolvableFeature` |
| Unnamed entry-action form, no crash | `WorkspaceLoader_LoadAsync_UnnamedEntryActionReferenceForm_NoNameNoCrash` |
| State usage explicit typing | `WorkspaceLoader_LoadAsync_StateUsageWithExplicitTyping_RecordsTypingEdge` |
| Transition source resolves | `WorkspaceLoader_LoadAsync_TransitionSourceStartFeature_ResolvesToStdlibActionMember` |
| OMG corpus fixture | `Transition_OmgCorpusFixture_ResolvesAllStatesAndTransitions` |
| Compact `action a; then b;` idiom | `WorkspaceLoader_LoadAsync_CompactActionThenIdiom_ResolvesBothNodes` |
| Target successions + incoming edge | `WorkspaceLoader_LoadAsync_MultipleActionTargetSuccessions_CapturesAll` |
| Bare `first start;`, no succession | `WorkspaceLoader_LoadAsync_BareInitialNodeMember_ProducesNoSuccession` |
| Attached first-then edge | `WorkspaceLoader_LoadAsync_InitialNodeMemberWithAttachedSuccession_SynthesizesTransition` |
| Anonymous control nodes synthesize names | `WorkspaceLoader_LoadAsync_AnonymousControlNodes_SynthesizeNames` |
| Named control nodes keep declared name | `WorkspaceLoader_LoadAsync_NamedControlNodes_KeepDeclaredName` |
| Guarded/default succession | `WorkspaceLoader_LoadAsync_GuardedAndDefaultActionTargetSuccession_ExtractTargets` |
| Fork/join/decision/merge OMG fixtures + incoming | `ControlNode_OmgCorpusFixture_ResolvesForkJoinDecisionMerge` |
| Enum def bare literals | `WorkspaceLoader_LoadAsync_EnumDefinition_BareLiterals_CapturesEnumValues` |
| Enum def value-assignment form | `WorkspaceLoader_LoadAsync_EnumDefinition_ValueAssignmentForm_CapturesNames` |
| Requirement def subject/constraint | `WorkspaceLoader_LoadAsync_RequirementDefinition_CapturesSubjectAndConstraints` |
| Requirement usage subject/constraint | `WorkspaceLoader_LoadAsync_RequirementUsage_CapturesSubjectAndConstraint` |
| Requirement def actor/stakeholder | `WorkspaceLoader_LoadAsync_RequirementDefinition_CapturesActorAndStakeholder` |
| Concern def/usage subject | `WorkspaceLoader_LoadAsync_ConcernDefinitionAndUsage_CapturesSubject` |
| Standalone constraint usage | `WorkspaceLoader_LoadAsync_ConstraintUsage_CapturesExpressionText` |
| Constraint def synthesized child | `WorkspaceLoader_LoadAsync_ConstraintDefinition_SynthesizesExpressionChild` |
| Verify member no-hoist regression | `WorkspaceLoader_LoadAsync_RequirementVerifyMember_DoesNotHoistNestedContent` |
| Enum def OMG corpus fixtures | `Enumeration_OmgCorpusFixtures_CaptureAllLiteralForms` |
| Requirement OMG corpus fixtures | `Requirement_OmgCorpusFixtures_CaptureSubjectAndConstraints` |
| Comment/documentation OMG corpus fixtures | `CommentAndDocumentation_OmgCorpusFixtures_CaptureAnnotations` |
