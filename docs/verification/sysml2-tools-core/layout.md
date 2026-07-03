## DemaConsulting.SysML2Tools — Layout Subsystem Verification

### Verification Approach

The Layout subsystem is verified by the view layout strategy tests in
`DemaConsulting.SysML2Tools.Tests`. Each test constructs a synthetic `SysmlWorkspace`, invokes a
view layout strategy, and asserts on the returned `LayoutTree`. Because the strategies build the
tree by mapping the model onto the off-the-shelf `DemaConsulting.Rendering` intermediate
representation and by delegating geometric placement and routing to the off-the-shelf
`DemaConsulting.Rendering.Layout` layered algorithm (through the `LayeredPlacement` helper), a
passing strategy layout is evidence that the mapping, placement, and routing all occurred
correctly. No mocking is used; the real intermediate representation and layered algorithm run on
every test.

### Test Environment

Tests run via `dotnet test` against all three target frameworks: net8.0, net9.0, and net10.0.
No external network access or services are required beyond a standard .NET SDK installation and
the referenced `DemaConsulting.Rendering` packages. All test inputs are constructed inline.

### Acceptance Criteria

- All view layout strategy tests pass with zero failures across all three target frameworks.
- Each view strategy maps its relevant model elements onto a populated `LayoutTree` of boxes,
  ports, lines, and markers.
- Geometric placement is delegated to the off-the-shelf layered algorithm and positions boxes so
  that they do not overlap one another.
- Connector routing is resolved before rendering, so `LayoutLine.Waypoints` holds the complete
  ordered sequence of absolute points.

### Test Scenarios

**SysML2Tools-Core-Layout-LayoutTree** is verified by
`GeneralViewLayoutStrategy_BuildLayout_OneUserPartDef_ProducesLayoutBox`,
`InterconnectionView_BuildLayout_PartsAndConnections_ProducesBoxesPortsAndLines`, and
`SequenceView_BuildLayout_Messages_ProducesLifelinesAndOrderedLines`, which confirm that each
view strategy maps the semantic model onto a populated `LayoutTree`.

**SysML2Tools-Core-Layout-DelegatedGeometry** is verified by
`InterconnectionView_BuildLayout_PartBoxes_DoNotOverlap` and
`ActionFlowView_BuildLayout_NoOverlap`, which confirm that the geometry produced by the
off-the-shelf layered algorithm places boxes without overlap.

**SysML2Tools-Core-Layout-PreRoutedLines** is verified by
`ActionFlowView_BuildLayout_ForwardChain_FlowsTopToBottomOrthogonally` and
`StateTransitionView_BuildLayout_ForwardChain_FlowsTopToBottomOrthogonally`, which confirm that
connectors are fully routed into orthogonal absolute waypoints before rendering.
