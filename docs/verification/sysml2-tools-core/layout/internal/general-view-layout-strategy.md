#### GeneralViewLayoutStrategy Verification

##### Verification Approach

`GeneralViewLayoutStrategy` is verified through unit tests in `GeneralViewLayoutStrategyTests`
that construct a synthetic `SysmlWorkspace` of definitions, invoke `BuildLayout`, and assert on
the returned `LayoutTree`. A recursive helper collects boxes from the (possibly nested) node tree
so assertions can confirm box keywords, folder shapes, compartments, and specialization lines. No
mocking is required; the strategy depends only on the in-memory model, the geometric engines, and
the theme, all constructed directly by the tests.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `GeneralViewLayoutStrategyTests` pass with zero failures across all three target frameworks.
- Each user definition appears as a box carrying its definition keyword.
- A package's definitions appear inside a folder-shaped box labelled with the package name.
- A definition's owned usages appear as compartment rows formatted `name : Type`.
- A specialization yields a line with an open arrowhead at the supertype end.
- A `part`-feature yields a line with a filled-diamond arrowhead at the owner end.
- A `port`-feature yields a line with a filled-diamond arrowhead at the owner end.
- A `ref`-feature does NOT yield any diamond arrowhead edge.
- An `attribute`-feature does NOT yield any diamond arrowhead edge.
- A dense model with many part edges produces a canvas with area at least as large as a sparse model, confirming adaptive gap widening.
- Standard-library-only input (by prefix or by seed set) yields a minimal empty canvas.
- An empty workspace yields a 200×100 canvas with no nodes.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `GeneralViewLayoutStrategy_BuildLayout_EmptyWorkspace_ReturnsMinimalCanvas` | 200×100 canvas with no nodes |
| `GeneralViewLayoutStrategy_BuildLayout_StdlibOnlyWorkspace_ReturnsMinimalCanvas` | Stdlib defs excluded; no nodes |
| `GeneralViewLayoutStrategy_BuildLayout_OneUserPartDef_ProducesLayoutBox` | A user part def produces at least one box |
| `GeneralViewLayoutStrategy_BuildLayout_MixedDefinitionKinds_RendersAllWithKeywords` | Each def carries its keyword |
| `GeneralViewLayoutStrategy_BuildLayout_PackagedDefinitions_ProducesFolderBox` | Folder box with package keyword |
| `GeneralViewLayoutStrategy_BuildLayout_Subclassification_ProducesEdge` | Line with open arrowhead at supertype |
| `GeneralViewLayoutStrategy_BuildLayout_SeedStdlibNames_AreExcluded` | Seed-listed definitions excluded; empty canvas |
| `GeneralViewLayoutStrategy_BuildLayout_DefinitionWithUsages_ProducesCompartments` | Attribute and port compartments |
| `GeneralViewLayoutStrategy_BuildLayout_CompositeMembership_ProducesFilledDiamondEdge` | Filled-diamond at owner for `part` feature |
| `GeneralViewLayoutStrategy_BuildLayout_PortFeature_ProducesFilledDiamondEdge` | Filled-diamond at owner for `port` feature |
| `GeneralViewLayoutStrategy_BuildLayout_ReferenceMembership_DoesNotProduceEdge` | No diamond edge for `ref` feature |
| `GeneralViewLayoutStrategy_BuildLayout_AttributeFeature_DoesNotProduceDiamondEdge` | No diamond edge for `attribute` feature |
| `GeneralViewLayoutStrategy_BuildLayout_AdaptiveGap_DenseModelIsWiderThanSparseModel` | Dense layout canvas area ≥ sparse layout canvas area |
