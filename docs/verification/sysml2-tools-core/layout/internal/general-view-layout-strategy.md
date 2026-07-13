#### GeneralViewLayoutStrategy Verification

##### Verification Approach

`GeneralViewLayoutStrategy` is verified through unit tests in `GeneralViewLayoutStrategyTests`
that construct a synthetic `SysmlWorkspace` of definitions, invoke `BuildLayout`, and assert on
the returned `LayoutTree`. A recursive helper collects boxes from the (possibly nested) node tree
so assertions can confirm box keywords, folder shapes, compartments, and specialization, membership,
attribute-typing, redefinition, subsetting, connect, allocate, dependency, and binding lines. No
mocking is required; the strategy depends only on the in-memory model, `LayeredPlacement`, and
render options, all constructed directly by the tests. (Phase 2a) Bracket-form `expose`
filter-narrowing behavior is delegated to `ExposeScopeResolver` and is verified directly by
`ExposeScopeResolverTests` (see `expose-scope-resolver.md`) plus an end-to-end
`RenderIntegrationTests` case exercising the full rendering pipeline; both are cross-referenced
here since the observable effect on `GeneralViewLayoutStrategy`'s rendered output is part of this
unit's requirement coverage.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `GeneralViewLayoutStrategyTests` pass with zero failures across all three target frameworks.
- Each user definition appears as a box carrying its definition keyword.
- When the view has no resolved `expose` scope, a bare package's definitions appear inside a
  folder-shaped box labelled with the package name.
- A definition that owns nested definitions appears as exactly one box, with its nested
  definitions placed inside that box (as its own `Children`) rather than as a duplicate sibling
  container — regardless of whether the view is scoped or unscoped.
- When the view has a resolved `expose` scope, a bare package is never rendered as a wrapping
  folder merely because it is an ancestor of admitted content; the package's admitted items are
  promoted directly to the diagram's root instead.
- A definition's owned usages appear as compartment rows formatted `name : Type`.
- A specialization yields a line with an open end marker at the supertype end.
- A `part`-feature yields a line with a filled-diamond end marker at the owner end.
- A `port`-feature yields a line with a filled-diamond end marker at the owner end.
- A `ref`-feature yields a dashed line with an open-chevron end marker at the referenced type,
  sharing the same rendering as a standalone Dependency edge (no hollow-diamond marker).
- An `attribute`-feature does NOT yield any diamond end marker edge.
- An `attribute`-feature whose type resolves to a definition in the view yields a dashed line with an
  open-chevron end marker at the attribute-type definition end.
- An `attribute`-feature whose type is an `enum def` in the view yields a dashed open-chevron line to
  the enumeration definition.
- An `attribute`-feature whose type does not resolve to a definition in the view yields no typing edge.
- A dense model with many part edges produces a layout in which no two definition boxes overlap,
  confirming `LayeredPlacement` keeps boxes separated.
- A connected model (many cross-referencing part edges) produces a layout in which all definition
  boxes remain mutually non-overlapping.
- A sparse model (two boxes and one edge) produces a compact canvas with no warnings, confirming the
  delegated layout does not over-pad sparse diagrams.
- Standard-library-only input (by prefix or by seed set) yields a minimal empty canvas.
- An empty workspace yields a 200×100 canvas with no nodes.
- A view whose `ViewContext.ViewNode` carries a resolved `Expose` edge scopes the diagram to that
  target's containment subtree, excluding unrelated sibling definitions and producing fewer boxes
  than an unscoped (no-`ViewNode`) rendering of the same workspace.
- A view whose `ViewContext.ViewNode` carries a `RenderTargetName` but no resolved `Expose` edges
  renders the full workspace, byte-identical to a view with a `null` `ViewNode` — proving
  `RenderTargetName` never affects scope, regardless of `FilterExpressionText`.
- A view whose resolved `Expose` edge names a feature usage (not a definition) still renders that
  usage's type's containment subtree, by additionally resolving the usage's own `Typing` edge —
  the fix for the usage-vs-definition containment gap.
- (Phase 2a) A view whose `expose <path>::**[<expr>];` entry carries a bracket-filter expression
  that parses and evaluates successfully narrows that entry's contribution to only the descendant
  definitions the expression matches, rather than that entry's whole containment subtree; end-to-end
  rendering of such a view produces SVG output containing only the matched definitions, with no
  "could not be evaluated" warning.
- (Phase 2a) When one `expose` entry in a view carries a successfully-evaluated bracket filter and
  another entry in the same view carries none, each entry narrows independently — the unfiltered
  entry still contributes its whole containment subtree while the bracket-filtered entry
  contributes only its matched definitions.
- A view whose `ViewContext.ViewNode` carries a supported `FilterExpressionText` narrows the
  already expose-scoped candidate definitions to the matched subset, including the empty-set case.
- A view whose `ViewContext.ViewNode` carries an unsupported or malformed `FilterExpressionText`
  emits a "could not be evaluated" warning through `LayoutTree.Warnings`, while still rendering
  the resolved (unfiltered) scope.
- A view with a `null` `ViewContext.ViewNode` (the `--auto` synthesized-view path, and the
  pre-scoping-change 2-argument `ViewContext` construction used throughout the rest of this test
  file) renders every non-stdlib definition in the workspace, unchanged from before this feature —
  the critical regression guard confirming full backward compatibility.
- A subtype feature that redefines a bare-named inherited feature (declared on a resolved
  supertype in the view) yields a solid line with a hollow-triangle-crossbar end marker at the
  supertype that declares the redefined feature.
- A subtype feature that redefines a bare-named feature declared two or more supertype hops away
  yields a hollow-triangle-crossbar edge targeting the actual declaring ancestor, not the
  immediate supertype, proving the bare-name walk is transitive.
- A subtype feature that redefines a qualified `Owner::feature` reference yields a
  hollow-triangle-crossbar edge directly to the named owner, without needing the owner to be an
  immediate or transitive supertype.
- An unresolvable redefinition reference (neither a qualified owner nor a bare name found
  anywhere in the supertype chain) produces no edge, and layout completes without throwing.
- A genuinely self-referential redefinition (a feature's `redefines` target resolves back to its
  own owning definition, via a self-referential supertype cycle) produces no edge, and layout
  completes without throwing.
- A resolved `Connect` edge whose endpoints map (via `ResolveOwningBox`) to two distinct rendered
  boxes yields a solid line with no end marker between them — including the dominant real-world
  shape (two sibling features declared directly in their owning `part def`s, resolved via
  `ReferenceResolver`'s instance-path-preserving type-hierarchy fallback), verified end to end
  with the real `WorkspaceLoader` and `BuildLayout`, not just a hand-built fixture.
- A resolved `Connect` edge whose endpoints both map to the same rendered box (a genuine self-loop
  — e.g. two sibling features of the same enclosing definition with no distinguishing owner)
  yields no edge, and the drop is surfaced as a `Connect`-kind warning via
  `LayoutWarnings.ForDroppedRelationshipEdges`.
- A resolved `Allocate` edge yields a dashed line with an open-chevron end marker at the target
  and a `«allocate»` midpoint label.
- A resolved `Dependency` edge yields a dashed line with an open-chevron end marker at the target
  and no midpoint label — the same rendering as the `ref`-fix.
- A resolved `Binding` edge whose endpoints map to two distinct rendered boxes yields a solid line
  with no end marker and an `=` midpoint label.
- A subtype feature that subsets a bare-named or qualified inherited feature yields a dashed line
  with a hollow-triangle end marker at the owning definition of the subsetted feature.
- A subsetting reference that resolves back to the subtype's own owning definition (a
  self-referential same-definition shape) produces no edge.
- Any `Connect`/`Allocate`/`Dependency`/`Binding` edge whose endpoint fails to resolve to a
  rendered box, or whose endpoints resolve to the same box, is surfaced as a warning in
  `LayoutTree.Warnings` (defense-in-depth diagnostic) — except an unresolved-endpoint drop caused
  solely by the endpoint falling outside an active `expose` scope narrowing, which is expected
  behavior and produces no warning.
- Every definition with one or more `Documentation`/`Comment` annotations gets exactly one
  `BoxShape.Note` box, connected to the definition's own box by a plain solid line with no end
  marker; a definition with no annotations gets no note box.
- A `"subject"`/`"assume constraint"`/`"require constraint"`/`"constraint"`-keyword feature
  compartment is titled with the guillemet-wrapped stereotype form (e.g. `«subject»`), not the
  generic pluralized-keyword default.
- A constraint-kind feature (non-null `ExpressionText`) renders its raw expression text in place
  of the generic `name : Type [multiplicity]` row shape.
- An `"enum value"`-keyword feature compartment is titled `"enum values"`.
- `CollectDefinitions` admits named usage-level (`SysmlFeatureNode`) candidates alongside
  definitions (Phase 2d), so a bare or `SysML::`-qualified metaclass-kind filter
  (`filter @PartUsage;`/`filter @SysML::PartUsage;`) renders only the matching usage-kind boxes,
  reproducing the OMG `42.Views/ViewsExample.sysml` pattern, instead of an empty canvas.
- With no filter present at all, usage-level candidates render as boxes too, but only when they
  are not already shown as a compartment row of an independently-rendered nearest ancestor —
  `RemoveRedundantNestedUsages` (Retry 1 fix; hardened to a depth-ordered cascading pass in Retry 2)
  excludes a nested usage from standalone rendering when its immediate parent is also present in
  the final rendered set and has not itself already been excluded, restoring the pre-Phase-2d box
  count for models with nested usages (e.g. the gallery's `01-drone-general.sysml`) while still
  admitting genuinely top-level or filter-surviving usages as their own boxes, and — critically —
  never silently dropping a usage nested two or more levels deep whose intermediate parent was
  itself excluded (that usage instead correctly survives as its own standalone box).
- The OMG Safety feature-views fixture's exposed-vehicle-subtree scope (a whole-subtree `expose`
  of the vehicle usage, no bracket filter) now renders a non-empty scoped diagram containing the
  vehicle's part usages (e.g. `seatBelt`, `bumper`), where it previously rendered empty because
  `CollectDefinitions` admitted only definitions, not usages — while still excluding
  `Safety`/`Security` from the scoped result. These usages' immediate containing usages (`vehicle`
  and its intermediate subassemblies) are never independently admitted into the exposed scope's
  matched-member set, so `RemoveRedundantNestedUsages` does not remove them.
- A usage nested directly inside an independently-rendered definition/usage is excluded from
  standalone box rendering in the default (unfiltered, unexposed) case — the direct regression
  reproduction requested by quality re-validation (Retry 1).
- A nested usage whose immediate parent is excluded from the final rendered set by an active
  metaclass filter (rather than by scope) still renders as its own standalone box, proving
  `RemoveRedundantNestedUsages` runs after — not before — standalone filter narrowing (Retry 1).
- The real gallery corpus model `docs/gallery/models/01-drone-general.sysml`'s `DroneGeneralView`
  renders exactly 21 boxes, matching the checked-in `docs/gallery/svg/DroneGeneralView.svg` — the
  automated regression guard for the 21 → 47 box-count defect found by quality re-validation
  (Retry 1).
- A usage nested two or more levels deep (e.g. `part def A { part b { part c; } }`) renders as its
  own standalone box (`c`) when its intermediate parent (`b`) is itself excluded as a redundant
  nested usage — the direct regression reproduction for the silent-data-loss defect found by
  quality re-validation (Retry 2): exactly 2 boxes render (`A` and `c`), `b` never appears as its
  own box, and `c` is never silently dropped merely because its immediate parent was itself
  excluded in the same pass.

##### Test Scenarios

- `GeneralViewLayoutStrategy_BuildLayout_EmptyWorkspace_ReturnsMinimalCanvas`:
  200×100 canvas with no nodes
- `GeneralViewLayoutStrategy_BuildLayout_StdlibOnlyWorkspace_ReturnsMinimalCanvas`:
  Stdlib defs excluded; no nodes
- `GeneralViewLayoutStrategy_BuildLayout_OneUserPartDef_ProducesLayoutBox`:
  A user part def produces at least one box
- `GeneralViewLayoutStrategy_BuildLayout_MixedDefinitionKinds_RendersAllWithKeywords`:
  Each def carries its keyword
- `GeneralViewLayoutStrategy_BuildLayout_PackagedDefinitions_ProducesFolderBox`:
  Folder box with package keyword
- `GeneralViewLayoutStrategy_BuildLayout_Subclassification_ProducesEdge`:
  Line with open end marker at supertype
- `GeneralViewLayoutStrategy_BuildLayout_SeedStdlibNames_AreExcluded`:
  Seed-listed definitions excluded; empty canvas
- `GeneralViewLayoutStrategy_BuildLayout_DefinitionWithUsages_ProducesCompartments`:
  Attribute and port compartments
- `GeneralViewLayoutStrategy_BuildLayout_CompositeMembership_ProducesFilledDiamondEdge`:
  Filled-diamond at owner for `part` feature
- `GeneralViewLayoutStrategy_BuildLayout_PortFeature_ProducesFilledDiamondEdge`:
  Filled-diamond at owner for `port` feature
- `GeneralViewLayoutStrategy_BuildLayout_ReferenceMembership_ProducesDependencyEdge`:
  Dashed open-chevron Dependency-style edge for `ref` feature (no hollow-diamond marker)
- `GeneralViewLayoutStrategy_BuildLayout_AttributeFeature_DoesNotProduceDiamondEdge`:
  No diamond edge for `attribute` feature
- `GeneralViewLayoutStrategy_BuildLayout_AttributeTyping_ProducesDashedOpenChevronEdge`:
  Dashed open-chevron dependency to the attribute-type def; no diamond edge
- `GeneralViewLayoutStrategy_BuildLayout_EnumTypedAttribute_ProducesDashedOpenChevronEdge`:
  Dashed open-chevron dependency to the enum def
- `GeneralViewLayoutStrategy_BuildLayout_AttributeTyping_UnresolvedType_ProducesNoEdge`:
  No typing edge when the attribute type is unresolved
- `GeneralViewLayoutStrategy_BuildLayout_AdaptiveGap_DenseModelProducesNonOverlappingBoxes`:
  Dense model produces a layout with no overlapping definition boxes
- `GeneralViewLayoutStrategy_BuildLayout_HeatLayout_ConnectedModelKeepsBoxesSeparated`:
  Connected cross-referencing model keeps every definition box non-overlapping
- `GeneralViewLayoutStrategy_BuildLayout_HeatLayout_SparseModelProducesCompactCanvas`:
  Sparse canvas stays compact with no warnings (no over-padding)
- `GeneralViewLayoutStrategy_BuildLayout_ExposedName_UnionsAdditionalSubtree`:
  A resolved `Expose` edge scopes the diagram to the target's containment subtree, fewer boxes
  than the full workspace
- `GeneralViewLayoutStrategy_BuildLayout_RenderTargetNameOnly_NoExposeEdges_RendersFullWorkspace`:
  A `RenderTargetName` with no resolved `Expose` edges renders the full workspace unchanged
- `GeneralViewLayoutStrategy_BuildLayout_ExposedUsage_ResolvesThroughTypingToDefinitionSubtree`:
  A resolved `Expose` edge naming a feature usage resolves through the usage's `Typing` edge to
  include its type's containment subtree
- `ResolveExposedScope_BracketFilterEvaluatesSuccessfully_NarrowsToMatchedDefinitionsOnly`
  (`ExposeScopeResolverTests`):
  A bracket-filtered `expose` entry that parses and evaluates successfully narrows to only the
  matched descendant definitions instead of the whole containment subtree
- `ResolveExposedScope_MixedFilteredAndUnfilteredEntries_NarrowsIndependently`
  (`ExposeScopeResolverTests`):
  A bracket-filtered entry and an unfiltered entry on the same view narrow independently
- `DiagramRenderer_RenderWorkspace_BracketExposeMandatorySafetyView_FiltersToMandatoryPart`
  (`RenderIntegrationTests`):
  End-to-end rendering of a `expose <path>::**[<expr>];` view includes only the matched definition
  and emits no "could not be evaluated" warning for the successfully-evaluated filter
- `GeneralViewLayoutStrategy_BuildLayout_FilterExpressionPresent_EmitsNotYetEvaluatedWarning`:
  Unsupported filter text emits the "could not be evaluated" warning while the unfiltered scope
  still renders
- `GeneralViewLayoutStrategy_BuildLayout_FilterExpressionMatchesNothing_RendersEmpty`:
  A supported filter expression that matches no candidates narrows the diagram to an empty canvas
- `GeneralViewLayoutStrategy_BuildLayout_NullViewNode_RendersFullWorkspaceUnchanged`:
  A `null` `ViewNode` (`--auto`/default) renders every definition, unchanged (regression guard)
- `GeneralViewLayoutStrategy_BuildLayout_BareNameRedefinition_ProducesHollowTriangleCrossbarEdge`:
  A bare-name redefinition produces a solid hollow-triangle-crossbar edge to the supertype that
  declares the redefined feature
- `GeneralViewLayoutStrategy_BuildLayout_TransitiveBareNameRedefinition_ProducesHollowTriangleCrossbarEdgeToDeclaringAncestor`:
  A bare-name redefinition whose declaring ancestor is two supertype hops away produces a
  hollow-triangle-crossbar edge to that ancestor, not the immediate supertype
- `GeneralViewLayoutStrategy_BuildLayout_QualifiedRedefinition_ProducesHollowTriangleCrossbarEdgeToOwner`:
  A qualified `Owner::feature` redefinition produces a hollow-triangle-crossbar edge to the
  named owner
- `GeneralViewLayoutStrategy_BuildLayout_UnresolvableRedefinition_ProducesNoEdge`:
  An unresolvable redefinition produces no edge and does not throw
- `GeneralViewLayoutStrategy_BuildLayout_SelfReferentialRedefinition_ProducesNoEdge`:
  A genuinely self-referential redefinition (resolving back to its own owning definition via a
  self-referential supertype cycle) produces no edge and does not throw
- `GeneralViewLayoutStrategy_BuildLayout_Connect_DifferentOwningTypes_ProducesUnmarkedSolidEdge`:
  A `Connect` edge between two distinct owning boxes produces a solid line with no end marker
- `GeneralViewLayoutStrategy_BuildLayout_Connect_SameOwningType_ProducesNoEdge`:
  A `Connect` edge whose endpoints resolve to the same owning box (self-loop) produces no edge,
  and the drop is reported as a `Connect`-kind warning in `LayoutTree.Warnings`
- `GeneralViewLayoutStrategy_BuildLayout_ConnectDominantShape_RealWorkspaceLoader_ProducesDistinctBoxes`:
  End-to-end regression guard: the real `WorkspaceLoader` + `BuildLayout` pipeline (no synthetic
  edges) renders a `Connect` edge between two distinct boxes for the dominant real-world shape,
  with no dropped-edge warning
- `GeneralViewLayoutStrategy_BuildLayout_Allocate_ProducesDashedChevronEdgeWithLabel`:
  An `Allocate` edge produces a dashed open-chevron line with a `«allocate»` midpoint label
- `GeneralViewLayoutStrategy_BuildLayout_Dependency_ProducesDashedChevronEdge`:
  A `Dependency` edge produces a dashed open-chevron line with no midpoint label
- `GeneralViewLayoutStrategy_BuildLayout_Binding_ProducesSolidEdgeWithEqualsLabel`:
  A `Binding` edge produces a solid line with no end marker and an `=` midpoint label
- `GeneralViewLayoutStrategy_BuildLayout_Subsetting_CrossesSpecializationBoundary_ProducesDashedHollowTriangleEdge`:
  A `subsets`/`:>` feature reference produces a dashed hollow-triangle edge to the owner of the
  subsetted feature
- `GeneralViewLayoutStrategy_BuildLayout_SelfReferentialSubsetting_ProducesNoEdge`:
  A self-referential subsetting reference (resolving back to the subtype's own owning definition)
  produces no edge
- `GeneralViewLayoutStrategy_BuildLayout_AnnotatedDefinition_EmitsNoteBox`:
  A definition with a `Documentation`/`Comment` annotation gets a `BoxShape.Note` box connected by
  a plain solid line with no end marker
- `GeneralViewLayoutStrategy_BuildLayout_UnannotatedDefinition_EmitsNoNoteBox`:
  A definition with no annotations gets no note box (regression guard)
- `GeneralViewLayoutStrategy_BuildLayout_MultipleAnnotations_ProduceOneNoteBox`:
  A definition with multiple `Documentation`/`Comment` annotations gets exactly one note box, with
  every annotation's text concatenated into it
- `GeneralViewLayoutStrategy_BuildLayout_RequirementSubject_UsesGuillemetTitle`:
  A `"subject"`-keyword feature compartment is titled `«subject»`, not the generic pluralized form
- `GeneralViewLayoutStrategy_BuildLayout_ConstraintFeatures_ShowExpressionText`:
  `"require constraint"`/`"assume constraint"` features render their raw `ExpressionText` instead
  of a `name : Type [multiplicity]` row, under `«require constraint»`/`«assume constraint»`
  compartment titles
- `GeneralViewLayoutStrategy_BuildLayout_EnumDefLiteralValues_ProducesEnumValuesCompartment`:
  `"enum value"`-keyword features are grouped under an `"enum values"` compartment title
- `GeneralViewLayoutStrategy_BuildLayout_QualifiedPartUsageFilter_RendersOnlyPartUsages`:
  A `filter @SysML::PartUsage;` expression renders only usage-level candidates whose own keyword
  maps to `PartUsage`, reproducing the OMG `42.Views/ViewsExample.sysml` regression pattern against
  a mixed part/requirement/other-usage workspace
- `GeneralViewLayoutStrategy_BuildLayout_BarePartUsageFilter_RendersOnlyPartUsages`:
  The bare `filter @PartUsage;` spelling matches identically to the qualified form
- `GeneralViewLayoutStrategy_BuildLayout_NoFilter_RendersUsageLevelCandidatesToo`:
  With no filter present, flat/top-level usage-level candidates render as boxes by default (Phase
  2d widening); this synthetic workspace has no nesting, so `RemoveRedundantNestedUsages` (Retry 1)
  has nothing to exclude here
- `GeneralViewLayoutStrategy_BuildLayout_NoFilter_ExcludesUsageNestedInsideRenderedDefinition`
  (Retry 1): a usage nested directly inside an independently-rendered definition is excluded from
  standalone box rendering in the default (unfiltered) case — the direct regression reproduction
  requested by quality re-validation
- `GeneralViewLayoutStrategy_BuildLayout_MetaclassFilter_KeepsNestedUsageWhenParentExcluded`
  (Retry 1): a nested usage whose immediate parent is excluded from the final rendered set by an
  active metaclass filter still renders as its own standalone box, proving the dedup step runs
  after — not before — standalone filter narrowing
- `GeneralViewLayoutStrategy_BuildLayout_DroneGalleryModel_RendersExactly21BoxesMatchingCheckedInSvg`
  (Retry 1): the real gallery corpus model `docs/gallery/models/01-drone-general.sysml`'s
  `DroneGeneralView` renders exactly 21 boxes, matching the checked-in
  `docs/gallery/svg/DroneGeneralView.svg` — the automated regression guard for the 21 → 47
  box-count defect
- `GeneralViewLayoutStrategy_BuildLayout_NoFilter_RendersDeeplyNestedGrandchildUsageWhenIntermediateParentExcluded`
  (Retry 2): a 3-level nested workspace (`Root::A` definition, `Root::A::b` and `Root::A::b::c`
  usages) renders exactly 2 boxes — `A` and `c` — with `b` correctly excluded as redundant but `c`
  never silently dropped, proving the depth-ordered cascading dedup pass correctly treats `b` as
  absent for `c`'s own test once `b` is itself excluded, instead of the prior single-pass,
  pre-dedup-snapshot logic that silently lost `c` entirely
- `GeneralViewLayoutStrategy_BuildLayout_OmgSafetyFeatureViewsFixture_ScopesToExposedVehicleSubtree`:
  The OMG Safety feature-views fixture's exposed-vehicle-subtree scope now renders a non-empty
  scoped diagram containing the vehicle's part usages (`seatBelt`/`bumper`), while still excluding
  `Safety`/`Security`
- `GeneralViewLayoutStrategy_BuildLayout_DefinitionOwningNestedDefinitions_RendersOneContainerBoxUnscoped`:
  A definition owning nested definitions (`OperatorConsole` owning `DisplayPanel`/`CommsHandset`,
  all inside package `Sys`, unscoped) renders exactly one `OperatorConsole` box, with
  `DisplayPanel`/`CommsHandset` nested as its own children; the `Sys` folder still exists and its
  own direct children are just the single `OperatorConsole` box — the Defect A regression guard
- `GeneralViewLayoutStrategy_BuildLayout_DefinitionOwningNestedDefinitions_RendersOneContainerBoxScoped`:
  The same nested-definition-owning fixture, scoped by an `expose Sys::OperatorConsole::**;`
  view, still renders exactly one `OperatorConsole` box with its children correctly nested, and no
  `Sys` folder appears at all — combining the Defect A and Defect B regression guards
- `GeneralViewLayoutStrategy_BuildLayout_ExposedNamespaceChildren_BarePackageAncestor_NoFolderRendered`:
  A view exposing `Sys::*` (direct-children recursion) over a bare package `Sys` with plain sibling
  definitions renders no `BoxShape.Folder` box anywhere, with the exposed definitions promoted
  directly to the root — the Defect B regression guard, isolated from any nested-definition case
- `GeneralViewLayoutStrategy_BuildLayout_Unscoped_StillRendersFullPackageFolderStructure`:
  An explicit regression guard confirming that, with no `expose`/no `ViewNode`, an ordinary
  bare-package case still renders a `Folder`-shaped box with the expected package label and
  expected (non-definition-container) children — unscoped behavior is provably unchanged
- `GeneralViewLayoutStrategy_BuildLayout_ExposedDefinitionInsideBarePackage_NoAncestorFolderRendered`:
  Mirrors the real `BatterySubsystemView` gallery scenario directly: `part def Battery` inside
  bare package `QuadcopterDrone`, exposed via `expose Battery;` (exact match) — no folder-shaped
  box exists and the `Battery` box is present directly at the root level, the primary Defect B
  correctness guard
