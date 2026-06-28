#### BrowserViewLayoutStrategy Verification

##### Verification Approach

`BrowserViewLayoutStrategy` is verified through unit tests in `BrowserAndGridViewLayoutStrategyTests`
that build a `SysmlWorkspace` with a nested membership hierarchy, run `BuildLayout`, and assert on
the returned `LayoutTree`. The strategy is pure and deterministic, so no mocking is required; real
workspace and rendering-option values are constructed directly.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- The browser-view tests in `BrowserAndGridViewLayoutStrategyTests` pass with zero failures across
  all target frameworks.
- A nested element's box is indented further than its ancestor's box.
- A workspace with no user-defined elements yields an empty diagram.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `BrowserView_BuildLayout_NestedElements_AreIndentedByDepth` | Nested element box has larger X than its ancestor box |
| `BrowserAndGrid_BuildLayout_EmptyWorkspace_ReturnMinimalCanvas` | Empty workspace yields no nodes |
