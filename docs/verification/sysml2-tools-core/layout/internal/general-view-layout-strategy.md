#### GeneralViewLayoutStrategy Verification

##### Verification Approach

`GeneralViewLayoutStrategy` is verified through unit tests in `GeneralViewLayoutStrategyTests`
that construct a synthetic `SysmlWorkspace` of definitions, invoke `BuildLayout`, and assert on
the returned `LayoutTree`. A recursive helper collects boxes from the (possibly nested) node tree
so assertions can confirm box keywords, folder shapes, compartments, and specialization, membership,
attribute-typing, and redefinition lines. No
mocking is required; the strategy depends only on the in-memory model, `LayeredPlacement`, and
render options, all constructed directly by the tests.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `GeneralViewLayoutStrategyTests` pass with zero failures across all three target frameworks.
- Each user definition appears as a box carrying its definition keyword.
- A package's definitions appear inside a folder-shaped box labelled with the package name.
- A definition's owned usages appear as compartment rows formatted `name : Type`.
- A specialization yields a line with an open end marker at the supertype end.
- A `part`-feature yields a line with a filled-diamond end marker at the owner end.
- A `port`-feature yields a line with a filled-diamond end marker at the owner end.
- A `ref`-feature yields a line with a hollow-diamond end marker at the owner end.
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
- `GeneralViewLayoutStrategy_BuildLayout_ReferenceMembership_ProducesHollowDiamondEdge`:
  Hollow-diamond at owner for `ref` feature
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
