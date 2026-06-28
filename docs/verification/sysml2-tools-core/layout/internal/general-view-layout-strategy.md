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
- A composite-membership feature (non-ref) yields a line with a filled-diamond arrowhead at the owner end.
- A reference-membership feature (ref) yields a line with an open-diamond arrowhead at the owner end.
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
| `GeneralViewLayoutStrategy_BuildLayout_CompositeMembership_ProducesFilledDiamondEdge` | Filled-diamond at owner |
| `GeneralViewLayoutStrategy_BuildLayout_ReferenceMembership_ProducesDiamondEdge` | Open-diamond edge, ref→owner |
