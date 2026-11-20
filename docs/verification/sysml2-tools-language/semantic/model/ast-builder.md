#### AstBuilder Verification

##### Verification Approach

`AstBuilder` is an internal class with no public surface and is verified indirectly through
`WorkspaceLoaderTests` plus the focused `AstBuilderMetadataTests`. Tests call
`WorkspaceLoader.LoadAsync` with controlled `.sysml` source files and assert that the returned
`SysmlLoadResult.Workspace.Declarations` and nested node data contain the expected names,
metadata annotations, and raw filter text.

##### Test Environment

Tests run via `dotnet test` against all four target frameworks: net8.0, net9.0, net10.0, and net11.0.
Temporary `.sysml` files are created in `Path.GetTempPath()` and deleted after each test. No
external services or additional configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- Every named KerML classifier kind (`classifier`, `datatype`, `class`, `struct`, `assoc`,
  `assoc struct`, `metaclass`, `behavior`, `interaction`, `type`, `function`, `predicate`) and
  feature kind (`feature`, `step`, `expr`, `bool`, `inv`, `connector`, `binding`, `succession`,
  `flow`, `succession flow`) declared in a package or nested in a classifier or function body is
  registered under its qualified name with the matching keyword, its supertypes or typing, its
  visibility, short name, and multiplicity; function parameters and `return` features are
  registered under the function; members declared without a name register no symbol; and a SysML
  model resolves references to the standard library's KerML declarations without diagnostics.

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
  the corresponding `SysmlViewNode`, and leaves both null for a view with an empty body. Each
  `render` target, a transition's trigger/guard/effect, an `accept`/`send` node's payload and
  operands, a state action's declaration, and a constraint expression also have their references
  located from the syntax tree and recorded with token positions on `ReferenceOccurrences`,
  excluding operators, literals, `self`, library functions, selected members, named-argument names
  and the names the construct declares for itself (a payload name, a body's parameters, bare `in`
  parameters), but keeping the SysML2Tools extension `render` selectors (`asGeneralDiagram` and the
  other four) so the resolver can report their portability Warning; a `rendering` usage is
  registered as a declaration, and a view's own `import`
  statements are kept as its children.
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
- Each `import` and `expose` member's written reference text is captured on
  `SysmlImportNode.RawReferenceText`/`ExposeMember.RawReferenceText` with the `::*`/`::**` markers
  kept and any bracketed filter and inter-token whitespace excluded (`P`, `P::*`, `P::**`,
  `P::*::**`, `P :: *` as `P::*`, `P::**[@Tag]` as `P::**`), while `ImportedNamespace`/
  `ImportedNames`/`QualifiedName` stay the stripped name used for resolution.
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
  Warning with no edge for an unresolvable name). A declared name (`dependency link from a to b;`,
  with an optional short name) is recorded as the node's name and registers it under its qualified
  name, while an unnamed dependency registers nothing.
- `VisitBindingConnectorAsUsage` captures a `bind A = B;` binding connector's two endpoints on a
  `SysmlConnectionNode` with `ConnectionKeyword == "binding"`, resolved via the same
  dotted-feature-chain walk as `connect`, producing a `Binding` edge when both sides resolve (or
  a Warning with no edge otherwise).
- Connector ends capture their optional role name (`name ::> reference`) and cross multiplicity
  (`[2] reference`) on `SysmlConnectionNode` for connection, binding, and interface usages, and leave
  them null for an end that declares neither.
- `VisitInterfaceUsage` builds a `"interface"` connection node and `VisitFlowUsage` a `"flow"` node
  (two ends, plus the `of` payload text for a flow); `VisitMessage` captures its `of` payload. None of
  them adds a reference-resolution edge or an error diagnostic.
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
- A body element's leading `abstract`/`variation`/`individual`/`variant`/`ref` and
  `in`/`out`/`inout` keywords are applied to its definition or feature node in definition, state,
  action, and requirement bodies (skipping visibility and a leading `then`), never to a
  synthesized transition; a bare direction-only parameter builds a `ref` feature carrying its
  direction (`return` is `Out`), flagged excluded from diagrams.
- `ordered`/`nonunique`, the value clause (`ValueText`, operator and whitespace preserved),
  `::>`/`references` targets, `~` conjugated typing, state sub-action declaration text, and
  `parallel` are captured as raw, unresolved data; `::>` targets stay out of `SupertypeNames`
  and a conjugated typing leaves `FeatureTyping` null, so no edge or diagnostic is added. The same
  data is captured on specialized usages (action, state, requirement, concern, constraint) through
  the shared `ApplyFeatureNotation` helper, and action control nodes never receive `IsParallel`.
- A transition's `Trigger` and `Effect` are captured verbatim for the full and attached forms
  (null for the entry-attached and action-flow forms and for empty effects), with `Guard`
  unchanged.
- A succession declared with `else` sets `IsElse` (and has no guard); a `terminate` node produces a
  `terminate` feature (synthetic name when unnamed); an action usage captures its typing and
  multiplicity; `accept` and `send` nodes capture their payload clause as `ActionText`.
- `event occurrence name [: Type];` (with or without a leading `then`) is captured as a feature child
  with the keyword `event occurrence`, in declaration order; the referencing form `event feature;` is
  not captured.
- Bare parameters (`in x : T;`, `out y;`, `in :>> p;`, a package-level `x :> y = v;`) build `ref`
  features with direction, typing, multiplicity, value and redefinitions; `timeslice`, `snapshot` and
  `individual` usages build features whose keyword is the portion kind (`occurrence` for individual);
  `perform`, `exhibit`, `include` and `assert constraint` usages build features named by their
  declared name, first redefinition or referenced feature, under a synthetic qualified name when
  named only by the reference (so a `perform`/`assert` named like the feature it references does not
  collide with it); a named transition's `do` clause builds an `effect` child of the matching kind.
- The bodies of action, calculation, constraint, state and case usages and definitions are built
  (nested actions, parameters, `return` parameters, subjects), so feature chains through them
  resolve; every member built this way, with its descendants, is flagged `IsExcludedFromDiagrams`
  and no previously built member is; the synthetic names of anonymous action nodes inside such bodies
  do not shift those of drawn nodes.
- Every redefinition target (`:>> a :>> b`, `:>> a, b`) is recorded, the first in
  `RedefinedFeatureName` and the rest in `AdditionalRedefinedFeatureNames`, and redefinitions and
  subsettings are captured for action, requirement and the other usage kinds; an anonymous usage with
  a body gets a `$anon<n>` scope segment (null `Name`) and one without a body stays unnamed.

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
| `VisitViewDefinition` render | `WorkspaceLoader_LoadAsync_ViewRenderTarget_CapturedRawResolvedWithoutEdge` |
| Transition reference occurrences | `WorkspaceLoader_LoadAsync_TransitionExpressions_CaptureReferenceOccurrences` |
| `rendering` usage declared | `WorkspaceLoader_LoadAsync_RenderingUsage_IsDeclared` |
| Extension selector kept | `WorkspaceLoader_LoadAsync_ToolRenderSelector_ProducesPortabilityWarningAtToken` |
| `VisitViewDefinition` filter capture | `WorkspaceLoader_LoadAsync_ViewFilterExpression_CapturesTextVerbatimNoEdge` |
| View declaration source location (`view def` and `view`) | `WorkspaceLoader_LoadAsync_Views_CaptureSourceLocation` |
| Declaration and reference positions | `WorkspaceLoader_LoadAsync_UnresolvedReference_ReportsSourcePosition` |
| Own-name reference | `WorkspaceLoader_LoadAsync_ReferenceNamedLikeDeclaration_LocatedAtReferenceToken` |
| Self-specialization cycle column | `WorkspaceLoader_LoadAsync_SelfSpecialization_ReportsReferenceColumn` |
| Visibility, short name, and alias capture | `WorkspaceLoader_LoadAsync_VisibilityShortNameAndAlias_Captured` |
| View conditions and view definition | `WorkspaceLoader_LoadAsync_ViewConditionsAndViewDefinition_Captured` |
| `VisitViewUsage` expose capture | `WorkspaceLoader_LoadAsync_ViewUsageWithExpose_RecordsExposeEdge` |
| `VisitViewUsage` bracket-filter capture | `AstBuilder_ExposeBracketFilter_CapturesRawText` |
| Bracket filter paired to entry | `AstBuilder_MultipleExposeMembers_OnlyOneBracketed_PairsFilterWithCorrectPath` |
| Bare MembershipExpose classification | `AstBuilder_ExposeBareMembership_CapturesMembershipExact` |
| Recursive MembershipExpose classification | `AstBuilder_ExposeRecursiveMembership_CapturesMembershipRecursive` |
| Bare NamespaceExpose classification | `AstBuilder_ExposeNamespaceDirectChildren_CapturesNamespaceDirectChildren` |
| Recursive NamespaceExpose classification | `AstBuilder_ExposeNamespaceRecursive_CapturesNamespaceRecursive` |
| Bracket-filtered classification | `WorkspaceLoader_LoadAsync_OmgSafetyFeatureViewsFixture_ResolvesBracketedExpose` |
| Import/expose written reference text captured | `AstBuilder_ImportForms_CaptureWrittenReferenceText` |
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
| Dependency declared name | `WorkspaceLoader_LoadAsync_DependencyNamed_RecordsDeclaredName` |
| Dependency edges carry their own declared name | `WorkspaceLoader_LoadAsync_DependencyEdges_CarryOwnDeclaredName` |
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
| `abstract` modifier | `WorkspaceLoader_LoadAsync_AbstractModifier_SetsIsAbstract` |
| `variation`/`variant` modifiers | `WorkspaceLoader_LoadAsync_VariationAndVariant_SetFlags` |
| `individual` modifier | `WorkspaceLoader_LoadAsync_IndividualPrefix_SetsIsIndividual` |
| `ref` modifier | `WorkspaceLoader_LoadAsync_RefPrefix_SetsIsReference` |
| Feature direction | `WorkspaceLoader_LoadAsync_FeatureDirection_Captured` |
| Bare parameter builds a `ref` node | `WorkspaceLoader_LoadAsync_BareDirectionParameter_BuildsRefNodeWithDirection` |
| Modifiers in specialized bodies | `WorkspaceLoader_LoadAsync_ModifiersInSpecializedBodies_AppliedToUsagesOnly` |
| `ordered`/`nonunique` | `WorkspaceLoader_LoadAsync_OrderedNonunique_Captured` |
| Value clause verbatim | `WorkspaceLoader_LoadAsync_FeatureValue_CapturedVerbatim` |
| Notation data on specialized usages | `WorkspaceLoader_LoadAsync_SpecializedUsages_CaptureNotationData` |
| Leading modifiers on specialized usages | `WorkspaceLoader_LoadAsync_SpecializedUsages_CaptureLeadingModifiers` |
| Action control nodes: no parallel data | `WorkspaceLoader_LoadAsync_ActionControlNodes_NoParallelOrNotationData` |
| `::>`/`references` raw capture | `WorkspaceLoader_LoadAsync_ReferenceSubsetting_CapturedNotInSupertypeNames` |
| `~` conjugated typing raw capture | `WorkspaceLoader_LoadAsync_ConjugatedPortTyping_CapturesNameKeepsTypingNull` |
| Transition trigger/guard/effect | `WorkspaceLoader_LoadAsync_TransitionTriggerGuardEffect_Captured` |
| Attached transition trigger/guard/effect | `WorkspaceLoader_LoadAsync_AttachedTransition_CapturesTriggerGuardEffect` |
| Synthesized successions: no trigger/effect | `WorkspaceLoader_LoadAsync_SynthesizedSuccessions_TriggerEffectNull` |
| State entry/do/exit action declaration text | `WorkspaceLoader_LoadAsync_StateEntryDoExitActions_CaptureActionText` |
| Empty state entry action | `WorkspaceLoader_LoadAsync_EmptyStateEntryAction_ActionTextNull` |
| `parallel` state | `WorkspaceLoader_LoadAsync_ParallelState_SetsIsParallel` |
| `else` succession sets `IsElse` | `WorkspaceLoader_LoadAsync_ElseSuccession_SetsIsElse` |
| `terminate` node | `WorkspaceLoader_LoadAsync_TerminateNode_ProducesTerminateFeature` |
| Action usage typing and multiplicity | `WorkspaceLoader_LoadAsync_ActionUsage_CapturesTypingAndMultiplicity` |
| Typing kept per kind | `WorkspaceLoader_LoadAsync_OccurrenceKindUsage_CapturesFeatureTypingAndEdge` |
| Requirement typing, multiplicity | `WorkspaceLoader_LoadAsync_RequirementUsage_CapturesTypingAndMultiplicity` |
| Usage typing diagnostic position | `WorkspaceLoader_LoadAsync_UnresolvedUsageTyping_ReportsSourcePosition` |
| Accept/send payload text | `WorkspaceLoader_LoadAsync_AcceptSendNodes_CapturePayloadText` |
| Connection end names, multiplicities | `WorkspaceLoader_LoadAsync_ConnectionEnds_CaptureRoleNamesAndMultiplicities` |
| Binding end role names and multiplicities | `WorkspaceLoader_LoadAsync_BindingEnds_CaptureRoleNames` |
| Interface usage connection node | `WorkspaceLoader_LoadAsync_InterfaceUsage_BuildsInterfaceConnectionNode` |
| Flow usage connection node with payload | `WorkspaceLoader_LoadAsync_FlowUsage_BuildsFlowConnectionNodeWithPayload` |
| Interface and flow add no diagnostics | `WorkspaceLoader_LoadAsync_InterfaceAndFlowUsages_ProduceNoDiagnostics` |
| Message payload text | `WorkspaceLoader_LoadAsync_MessageWithPayload_CapturesPayloadText` |
| Event occurrences | `WorkspaceLoader_LoadAsync_EventOccurrences_AreCapturedInDeclarationOrder` |
| Plain model leaves new members at defaults | `WorkspaceLoader_LoadAsync_PlainModel_AllNewMembersDefault` |
| KerML classifier kinds indexed (one row per kind) | `KermlClassifierKind_AtPackageLevel_IsIndexed` |
| KerML feature kinds indexed (one row per kind) | `KermlFeatureKind_AtPackageLevel_IsIndexed` |
| KerML `flow`/`succession flow` in a classifier body | `KermlFlowKind_InClassifierBody_IsIndexed` |
| KerML classifier membership details | `KermlClassifier_WithVisibilityShortNameAndAbstract_KeepsMembershipDetails` |
| KerML feature membership details | `KermlFeature_WithVisibilityShortNameAndMultiplicity_KeepsMembershipDetails` |
| KerML subsetting and redefinition targets | `KermlFeature_WithSubsettingAndRedefinition_RecordsTargets` |
| Feature nested in a KerML classifier | `KermlClassifier_WithNestedFeature_IndexesTheFeature` |
| KerML function parameters and return feature | `KermlFunction_WithParametersAndReturn_IndexesTheMembers` |
| Unnamed KerML members register no symbol | `KermlUnnamedMembers_AreSkippedFromTheSymbolTable` |
| KerML relationship statements parse | `KermlRelationshipStatements_ParseWithoutDiagnostics` |
| SysML model resolves KerML library | `SysmlModel_ReferencingKermlLibraryDeclarations_ResolvesWithoutDiagnostics` |
| Action def params | `AstBuilder_DefaultReferenceUsage_InActionDef_BuildsRefNodesWithDirectionTypingAndMultiplicity` |
| Bare parameters in an action usage body | `AstBuilder_DefaultReferenceUsage_InActionUsageBody_BindingResolves` |
| Bare usage in a package | `AstBuilder_DefaultReferenceUsage_NoDirectionInPackage_BuildsRefNode` |
| Bare redefinition takes the redefined name | `AstBuilder_DefaultReferenceUsage_RedefinitionOnly_TakesRedefinedName` |
| Stray `;` builds nothing | `AstBuilder_DefaultReferenceUsage_WithoutDeclaration_BuildsNoNode` |
| `return` parameter is `Out` | `AstBuilder_ReturnParameter_InCalcDef_HasOutDirection` |
| Timeslice and snapshot usages | `AstBuilder_PortionUsage_TimesliceAndSnapshot_BuildNodesAndSuccessionResolves` |
| Individual usage | `AstBuilder_PortionUsage_IndividualUsage_BuildsIndividualOccurrence` |
| Nested actions and chains | `AstBuilder_ActionUsage_NestedActions_BuildsChildrenAndChainResolves` |
| Calculation parameters | `AstBuilder_CalcDef_BareParameters_ResolveThroughTypedUsage` |
| Constraint definition body | `AstBuilder_ConstraintDef_BodyMembers_ResolveThroughAssertConstraint` |
| Constraint usage body | `AstBuilder_ConstraintUsage_Body_BuildsMembers` |
| Verification definition body | `AstBuilder_VerificationDef_BodyAction_ResolvesThroughUsage` |
| Use case and case usage bodies | `AstBuilder_CaseBodies_UseCaseAndCaseUsage_BuildMembers` |
| Anonymous action names unchanged | `AstBuilder_AnonymousActionNodes_InExcludedBodies_DoNotShiftDrawnNodeNames` |
| `perform` by reference | `AstBuilder_PerformWithReference_NamedByReferencedFeature` |
| `perform action` with a body | `AstBuilder_PerformAction_WithBody_BuildsNestedSend` |
| `exhibit state` and transition effect | `AstBuilder_ExhibitState_BodyAndTransition_ResolvesAccepterAndEffect` |
| Transition without `do` | `AstBuilder_Transition_WithoutEffect_HasNoEffectChild` |
| Same name as the referenced feature | `AstBuilder_PerformAndAssertSameNameAsReferenced_DoNotCollide` |
| `include use case` | `AstBuilder_IncludeUseCase_BuildsIncludeNode` |
| `assert constraint` without name | `AstBuilder_AssertConstraint_WithoutNameOrReference_BuildsNoNode` |
| All redefinition targets recorded | `AstBuilder_MultipleRedefinitions_RecordsAllTargets` |
| Redefinitions on action/requirement usages | `AstBuilder_Redefinition_OnActionAndRequirementUsages_IsRecorded` |
| Anonymous owner scope | `AstBuilder_AnonymousUsageWithBody_GetsSyntheticScope` |
| Anonymous usage without a body | `AstBuilder_AnonymousUsageWithoutBody_HasNoQualifiedName` |
