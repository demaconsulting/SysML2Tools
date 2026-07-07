#### GridViewLayoutStrategy Verification

##### Verification Approach

`GridViewLayoutStrategy` is verified through unit tests in `BrowserAndGridViewLayoutStrategyTests`
that build a `SysmlWorkspace` of definitions with specialization relationships, run `BuildLayout`,
and assert on the returned `LayoutTree`. The strategy is pure and deterministic, so no mocking is
required; real workspace and rendering-option values are constructed directly.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- The grid-view tests in `BrowserAndGridViewLayoutStrategyTests` pass with zero failures across all
  target frameworks.
- Definitions with a specialization relationship yield a grid with a header row and exactly one mark
  at the specializing intersection.
- A workspace with no user-defined definitions yields an empty diagram.
- A view whose `ViewContext.ViewNode` carries a resolved `Expose` edge scopes the matrix to that
  target's containment subtree, excluding unrelated sibling definitions and producing fewer rows
  than an unscoped (no-`ViewNode`) rendering of the same workspace.
- A view with a `null` `ViewContext.ViewNode` renders every non-stdlib definition, unchanged from
  before this feature — the critical `--auto`/no-expose regression guard.
- A view whose resolved `Expose` edge names a feature usage (not a definition) still renders that
  usage's type's containment subtree, via the shared usage-to-type fallback.
- A view with an `expose` statement naming two separate definitions unions both their containment
  subtrees into the matrix.
- A view whose `expose` statement targets only the specific side of a specialization relationship
  still keeps both the specific and general side visible as header rows/columns, with the
  specialization mark between them, while an unrelated sibling definition remains excluded — the
  "at least one dimension in scope" relation-preserving rule.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `GridView_BuildLayout_Specialization_ProducesMarkedMatrix` | Grid has a header row and one specialization mark |
| `BrowserAndGrid_BuildLayout_EmptyWorkspace_ReturnMinimalCanvas` | Empty workspace yields no nodes |
| `GridView_BuildLayout_ExposedName_UnionsAdditionalSubtree` | Resolved `Expose` scopes the matrix to fewer rows |
| `GridView_BuildLayout_NullViewNode_RendersFullWorkspaceUnchanged` | Null `ViewNode` renders all defs unchanged |
| `GridView_BuildLayout_ExposedUsage_ResolvesThroughTypingToDefinitionSubtree` | Usage resolves via `Typing` |
| `GridView_BuildLayout_ExposeMultipleTargets_UnionsBothSubtrees` | Two `expose` targets union both subtrees |
| `GridView_BuildLayout_ExposeOneSideOfSpecialization_KeepsBothRowAndColumn` | Keeps `A` and `Sub`, excludes `B` |
