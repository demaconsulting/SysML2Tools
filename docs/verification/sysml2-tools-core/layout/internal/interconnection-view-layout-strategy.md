#### InterconnectionViewLayoutStrategy Verification

##### Verification Approach

`InterconnectionViewLayoutStrategy` is verified through unit tests in
`InterconnectionViewLayoutStrategyTests` that construct a synthetic `SysmlWorkspace` containing a
part definition with nested parts and connections, invoke `BuildLayout`, and assert on the
returned `LayoutTree`. Assertions count the container box, rounded part boxes, port nodes, and
connector lines, and a geometric helper confirms that no two part boxes overlap. Nested-layout
tests build a two-level workspace (a part typed by a definition with its own internal parts) and
assert on the container box's nested `Children`. Parallel-connection tests assert that multiple
connections between the same two parts produce pairwise-distinct routed waypoints (not a shared
route), and port-labeling tests assert `LayoutPort.ExternalLabel` reflects the real SysML port-name
segment from a dotted endpoint reference, including the cross-boundary (label-only) case. No
mocking is required; the strategy depends only on the in-memory model, `LayeredPlacement`, and
render options.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `InterconnectionViewLayoutStrategyTests` pass with zero failures across all three target frameworks.
- A part definition with nested parts and connections yields a container box, one rounded box per
  part, one port per connection endpoint, and one connector line per connection.
- No two part boxes overlap.
- Two distinct connections between the same two parts render as two independently-routed
  connectors with genuinely distinct waypoints (parallel-edge preservation), not one shared route.
- Three distinct connections between the same two parts (mirroring a real 3-axis-gantry wiring
  model) render as three pairwise-distinct connectors, each with its own labeled port pair.
- A connection endpoint with a dotted port segment (e.g. `StepperMotorX.encoder`) produces a
  `LayoutPort` whose `ExternalLabel` is the real SysML port name, not `null`.
- A connection endpoint referencing a nested/cross-boundary path (e.g. `board.cpu`) still
  terminates its connector at the containing part's own boundary (the documented remaining
  limitation), but its port label reflects the true nested target name.
- An empty workspace yields a canvas with no nodes.
- A part typed by a definition with its own internal parts is rendered as a container box whose
  nested children lie inside its bounds, below its title area.
- A container box is sized to bound its nested children together with its title area and insets.
- Nested children are emitted at absolute coordinates offset from the container origin.
- A flat model (no nested internal structure) produces only leaf part boxes with no children.
- A self-referential part type terminates (cycle guard) and is rendered as a leaf box.
- A `null` `ViewContext.ViewNode` selects the pre-scoping heuristic root and renders every nested
  part, unchanged from before this feature — the critical `--auto`/no-expose regression guard.
- A view whose resolved `Expose` edge names a definition other than the heuristic root selects
  that definition as the root instead.
- A view whose resolved `Expose` edge names an inner part of a non-heuristic-root definition
  selects that definition's own root, narrowing its parts and dropping the connection to the
  excluded part.
- A view whose resolved `Expose` edge names a definition unrelated to any candidate root selects
  no root, producing the minimal empty canvas.
- A view whose resolved `Expose` edge names a single part narrows the container to that part.
- A view with an `expose` statement naming two separate parts unions both their containment
  subtrees, keeping the connection between them.
- A view whose resolved `Expose` edge names a feature usage (not a definition) still resolves to
  the usage's type as the root, via the shared usage-to-type fallback.
- A view whose resolved `Expose` edge names an inner part of a definition genuinely nested inside
  another eligible root candidate selects the nested definition, not the ancestor, even though the
  ancestor has more connections/parts and would win the old pure-score tie-break.
- When two same-depth sibling root candidates are both made scope-relevant by their own `expose`
  edges, the connections/parts score heuristic breaks the tie, selecting the candidate with the
  better score even when its qualified name is shorter — proving the tie-break is depth-based, not
  a raw qualified-name-length comparison.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `InterconnectionView_BuildLayout_PartsAndConnections_ProducesBoxesPortsAndLines` | Box, parts, ports, and lines |
| `InterconnectionView_BuildLayout_TwoConnectionsSamePair_ProducesTwoConnectorsWithoutException` | Distinct waypoints |
| `InterconnectionView_BuildLayout_ThreeParallelConnections_ProducesThreeDistinctConnectors` | Six labeled ports |
| `InterconnectionView_BuildLayout_ConnectionEndpointWithPortSegment_PortLabelReflectsSysmlPortName` | Port label |
| `InterconnectionView_BuildLayout_CrossBoundaryEndpoint_LabelReflectsNestedTarget` | Nested label, boundary connector |
| `InterconnectionView_BuildLayout_PartBoxes_DoNotOverlap` | No two rounded part boxes overlap |
| `InterconnectionView_BuildLayout_EmptyWorkspace_ReturnsMinimalCanvas` | Canvas with no nodes |
| `InterconnectionView_BuildLayout_NestedContainer_PlacesChildrenInsideContainerBox` | Children nested inside the box |
| `InterconnectionView_BuildLayout_ContainerSize_BoundsChildrenAndTitle` | Size bounds children, title, insets |
| `InterconnectionView_BuildLayout_NestedChildren_RenderedAtAbsoluteCoordinates` | Children at absolute coordinates |
| `InterconnectionView_BuildLayout_NoNesting_ProducesFlatLeafBoxes` | Flat model yields only leaf boxes (no children) |
| `InterconnectionView_BuildLayout_SelfReferentialType_TreatedAsLeaf` | Self-referential type renders as leaf |
| `InterconnectionView_BuildLayout_NullViewNode_PicksHeuristicRootUnchanged` | Null `ViewNode` renders unchanged |
| `InterconnectionView_BuildLayout_ExposeNonHeuristicRoot_SelectsExposedRoot` | Non-heuristic root is selected |
| `InterconnectionView_BuildLayout_ExposeInnerChildOfNonHeuristicRoot_SelectsItsRoot` | Inner child selects root |
| `InterconnectionView_BuildLayout_ExposeUnrelatedDefinition_NoRootSelected_ReturnsMinimalCanvas` | Unrelated def |
| `InterconnectionView_BuildLayout_ExposeSinglePart_NarrowsToThatPart` | Single exposed part narrows the container |
| `InterconnectionView_BuildLayout_ExposeMultipleParts_UnionsBothSubtrees` | Two exposed parts union both subtrees |
| `InterconnectionView_BuildLayout_ExposedUsage_ResolvesThroughTypingToRoot` | Usage resolves via `Typing` to root |
| `InterconnectionView_BuildLayout_ExposeInnerPartOfNestedDefinition_SelectsNestedDefinitionNotAncestor` | Nested wins |
| `InterconnectionView_BuildLayout_ExposeBothSameDepthSiblings_ScoreBreaksTieNotLength` | Score breaks the tie |
