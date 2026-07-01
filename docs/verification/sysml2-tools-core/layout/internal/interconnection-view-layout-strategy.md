#### InterconnectionViewLayoutStrategy Verification

##### Verification Approach

`InterconnectionViewLayoutStrategy` is verified through unit tests in
`InterconnectionViewLayoutStrategyTests` that construct a synthetic `SysmlWorkspace` containing a
part definition with nested parts and connections, invoke `BuildLayout`, and assert on the
returned `LayoutTree`. Assertions count the container box, rounded part boxes, port nodes, and
connector lines, and a geometric helper confirms that no two part boxes overlap. Nested-layout
tests build a two-level workspace (a part typed by a definition with its own internal parts) and
assert on the container box's nested `Children`. No mocking is required; the strategy depends only
on the in-memory model, the layered placement engine, and the theme.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `InterconnectionViewLayoutStrategyTests` pass with zero failures across all three target frameworks.
- A part definition with nested parts and connections yields a container box, one rounded box per
  part, one port per connection endpoint, and one connector line per connection.
- No two part boxes overlap.
- An empty workspace yields a canvas with no nodes.
- A part typed by a definition with its own internal parts is rendered as a container box whose
  nested children lie inside its bounds, below its title area.
- A container box is sized to bound its nested children together with its title area and insets.
- Nested children are emitted at absolute coordinates offset from the container origin.
- A flat model (no nested internal structure) produces only leaf part boxes with no children.
- A self-referential part type terminates (cycle guard) and is rendered as a leaf box.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `InterconnectionView_BuildLayout_PartsAndConnections_ProducesBoxesPortsAndLines` | Box, parts, ports, and lines |
| `InterconnectionView_BuildLayout_PartBoxes_DoNotOverlap` | No two rounded part boxes overlap |
| `InterconnectionView_BuildLayout_EmptyWorkspace_ReturnsMinimalCanvas` | Canvas with no nodes |
| `InterconnectionView_BuildLayout_NestedContainer_PlacesChildrenInsideContainerBox` | Children nested inside the box |
| `InterconnectionView_BuildLayout_ContainerSize_BoundsChildrenAndTitle` | Size bounds children, title, insets |
| `InterconnectionView_BuildLayout_NestedChildren_RenderedAtAbsoluteCoordinates` | Children at absolute coordinates |
| `InterconnectionView_BuildLayout_NoNesting_ProducesFlatLeafBoxes` | Flat model yields only leaf boxes (no children) |
| `InterconnectionView_BuildLayout_SelfReferentialType_TreatedAsLeaf` | Self-referential type renders as leaf |
