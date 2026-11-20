#### BrowserViewLayoutStrategy Verification

##### Verification Approach

`BrowserViewLayoutStrategy` is verified through unit tests in `BrowserAndGridViewLayoutStrategyTests`
that build a `SysmlWorkspace` with a nested membership hierarchy, run `BuildLayout`, and assert on
the returned `LayoutTree`. The strategy is pure and deterministic, so no mocking is required; real
workspace and rendering-option values are constructed directly.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, net10.0, and net11.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- The browser-view tests in `BrowserAndGridViewLayoutStrategyTests` pass with zero failures across
  all target frameworks.
- A nested element's box is indented further than its ancestor's box.
- Each row's text is the element's keyword in guillemet marks followed by its name line: a package
  reads `«package» Name`, and a feature row also shows its short name, typing, and multiplicity.
- Every row box has depth 0, regardless of its depth in the tree.
- A workspace with no user-defined elements yields an empty diagram.
- A view whose `ViewContext.ViewNode` carries a resolved `Expose` edge scopes the tree to that
  target's containment subtree, excluding unrelated sibling elements and producing fewer boxes than
  an unscoped (no-`ViewNode`) rendering of the same workspace.
- A view with a `null` `ViewContext.ViewNode` renders the full membership forest, unchanged from
  before this feature — the critical `--auto`/no-expose regression guard.
- A view whose resolved `Expose` edge names a feature usage (not a definition) still renders that
  usage's type's containment subtree, via the shared usage-to-type fallback.
- A view with an `expose` statement naming two separate definitions unions both their containment
  subtrees into the forest.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `BrowserView_BuildLayout_NestedElements_AreIndentedByDepth` | Nested element box has larger X than its ancestor box |
| `BrowserView_BuildLayout_RowLabels_UseGuillemetKeyword` | Rows read `«keyword» name`, with feature typing |
| `BrowserView_BuildLayout_RowBoxes_HaveDepthZero` | All rows of a three-level hierarchy have `Depth == 0` |
| `BrowserAndGrid_BuildLayout_EmptyWorkspace_ReturnMinimalCanvas` | Empty workspace yields no nodes |
| `BrowserView_BuildLayout_ExposedName_UnionsAdditionalSubtree` | Resolved `Expose` scopes to fewer boxes |
| `BrowserView_BuildLayout_NullViewNode_RendersFullWorkspaceUnchanged` | Null `ViewNode` renders full forest unchanged |
| `BrowserView_BuildLayout_ExposedUsage_ResolvesThroughTypingToDefinitionSubtree` | Usage resolves via `Typing` |
| `BrowserView_BuildLayout_ExposeMultipleTargets_UnionsBothSubtrees` | Two `expose` targets union both subtrees |
| `BrowserView_BuildLayout_ExcludedMembers_RenderIdenticallyToWorkspaceWithout` | Hidden declarations not listed |
