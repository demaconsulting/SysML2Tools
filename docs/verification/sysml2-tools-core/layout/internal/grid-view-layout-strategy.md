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

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `GridView_BuildLayout_Specialization_ProducesMarkedMatrix` | Grid has a header row and one specialization mark |
| `BrowserAndGrid_BuildLayout_EmptyWorkspace_ReturnMinimalCanvas` | Empty workspace yields no nodes |
