#### InterconnectionViewLayoutStrategy Verification

##### Verification Approach

`InterconnectionViewLayoutStrategy` is verified through unit tests in
`InterconnectionViewLayoutStrategyTests` that construct a synthetic `SysmlWorkspace` containing a
part definition with nested parts and connections, invoke `BuildLayout`, and assert on the
returned `LayoutTree`. Assertions count the container box, rounded part boxes, port nodes, and
connector lines, and a geometric helper confirms that no two part boxes overlap. No mocking is
required; the strategy depends only on the in-memory model, the geometric engines, and the theme.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `InterconnectionViewLayoutStrategyTests` pass with zero failures across all three target frameworks.
- A part definition with nested parts and connections yields a container box, one rounded box per
  part, one port per connection endpoint, and one connector line per connection.
- No two part boxes overlap.
- An empty workspace yields a canvas with no nodes.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `InterconnectionView_BuildLayout_PartsAndConnections_ProducesBoxesPortsAndLines` | Box, parts, ports, and lines |
| `InterconnectionView_BuildLayout_PartBoxes_DoNotOverlap` | No two rounded part boxes overlap |
| `InterconnectionView_BuildLayout_EmptyWorkspace_ReturnsMinimalCanvas` | Canvas with no nodes |
