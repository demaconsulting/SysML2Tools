#### GeneralViewLayoutStrategy Verification

##### Verification Approach

`GeneralViewLayoutStrategy` is verified through unit tests in `GeneralViewLayoutStrategyTests`
that construct a synthetic `SysmlWorkspace` of definitions, invoke `BuildLayout`, and assert on
the returned `LayoutTree`. A recursive helper collects boxes from the (possibly nested) node tree
so assertions can confirm box keywords, folder shapes, compartments, and specialization, membership,
and attribute-typing lines. No
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
- A view whose `ViewContext.ViewNode` carries a resolved `Render` edge scopes the diagram to that
  target's containment subtree, excluding unrelated sibling definitions and producing fewer boxes
  than an unscoped (no-`ViewNode`) rendering of the same workspace.
- A view whose `ViewContext.ViewNode` additionally carries a resolved `Expose` edge additively
  includes that exposed name's containment subtree alongside the render target's subtree.
- A view whose declared render target failed to resolve (no `Render` edge present) falls back to
  rendering the full workspace, identical to a view with a `null` `ViewNode`.
- A view whose `ViewContext.ViewNode` carries a non-null `FilterExpressionText` emits the "parsed
  but not yet evaluated" warning through `LayoutTree.Warnings`, while still rendering the resolved
  (unfiltered) scope.
- A view with a `null` `ViewContext.ViewNode` (the `--auto` synthesized-view path, and the
  pre-scoping-change 2-argument `ViewContext` construction used throughout the rest of this test
  file) renders every non-stdlib definition in the workspace, unchanged from before this feature —
  the critical regression guard confirming full backward compatibility.

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
- `GeneralViewLayoutStrategy_BuildLayout_ResolvedRenderTarget_ScopesToSubtreeFewerBoxes`:
  A resolved `Render` edge scopes the diagram to the target's containment subtree, fewer boxes
  than the full workspace
- `GeneralViewLayoutStrategy_BuildLayout_ExposedName_UnionsAdditionalSubtree`:
  A resolved `Expose` edge additively includes its containment subtree
- `GeneralViewLayoutStrategy_BuildLayout_UnresolvedRenderTarget_FallsBackToFullWorkspace`:
  No `Render` edge (target failed to resolve) falls back to full-workspace rendering
- `GeneralViewLayoutStrategy_BuildLayout_FilterExpressionPresent_EmitsNotYetEvaluatedWarning`:
  A non-null `FilterExpressionText` emits the "parsed but not yet evaluated" warning
- `GeneralViewLayoutStrategy_BuildLayout_NullViewNode_RendersFullWorkspaceUnchanged`:
  A `null` `ViewNode` (`--auto`/default) renders every definition, unchanged (regression guard)
