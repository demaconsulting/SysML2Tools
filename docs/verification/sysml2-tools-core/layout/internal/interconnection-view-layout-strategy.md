#### InterconnectionViewLayoutStrategy Verification

##### Verification Approach

`InterconnectionViewLayoutStrategy` is verified through unit tests in
`InterconnectionViewLayoutStrategyTests` that construct a synthetic `SysmlWorkspace` containing a
part definition with nested parts and connections, invoke `BuildLayout`, and assert on the
returned `LayoutTree`. Because the root container box nests all interior content as its own
`Children`, `LayoutTree.Nodes`/`.Children` are read through recursive `CollectBoxes`/`CollectPorts`/
`CollectLines` helpers (mirroring `GeneralViewLayoutStrategyTests`'s established pattern) that walk
every nested `LayoutBox.Children` rather than reading `.Nodes.OfType<T>()` directly. Assertions
count the container box, rounded part boxes, port nodes, and connector lines, and a geometric
helper confirms that no two part boxes overlap. Nested-layout tests build a two-level workspace (a
part typed by a definition with its own internal parts) and assert on the container box's nested
`Children`. Parallel-connection tests assert that multiple connections between the same two parts
produce pairwise-distinct routed waypoints (not a shared route), and port-labeling tests assert
`LayoutPort.ExternalLabel` reflects the real SysML port-name segment from a dotted endpoint
reference, including the cross-boundary (label-only) case. A dedicated test asserts the root-box
nesting invariant directly (`LayoutTree.Nodes` has exactly one element, and that element's
`Children` hold the interior content), and another exercises a high-connection-degree part to
confirm boxes remain non-overlapping and every incident connection still yields a labeled port,
now that box sizing/port spacing is fully delegated to the layered engine (via
`LayeredPlacement.PlaceWithPorts`) instead of the removed `MinPortSlot`/`ConnectorClearance`
heuristic. A further test confirms every left/right port sits below its own box's title area
(`BoxMetrics.TitleAreaHeight`) — the label-collision defect fixed by flagging every part node
`HasLabel: true, HasKeyword: true` so the layered algorithm's automatic title-vs-side-port
reservation activates for it. No mocking is required; the strategy depends only on the in-memory
model, `LayeredPlacement`, and render options.

A dedicated set of tests exercises the no-single-root scoped fallback: a workspace shaped exactly
like the reported bug (a namespace-like `part def` whose only nested content is a single `part`
feature usage, exposed via `expose Namespace::*;`) asserts the previously-empty canvas now renders
that feature directly, with no frame for the enclosing namespace anywhere in the tree; a two-sibling
variant asserts two independent top-level parts render as two non-overlapping top-level boxes; a
container-typed top-level feature variant asserts the recursion into its own nested parts still
fires (proving `BuildPartItem` is shared, not duplicated); a connection-between-top-level-parts
variant asserts a connector is drawn when the connection's own qualified name is itself in scope;
and a nested-qualified-name variant asserts a matched feature nested under another matched
feature's own qualified name is excluded from the top-level set (not duplicated). A final
regression test pins down the preserved empty-canvas fallback when the scope matches no `part`
feature at all.

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
- The root container box nests its interior content (part boxes, ports, and connector lines) as its
  own `Children` rather than as flat top-level siblings: `LayoutTree.Nodes` contains exactly one
  element (the root box).
- A part with a high connection degree still produces non-overlapping boxes and a labeled port for
  every incident connection, now that box sizing and port spacing are fully delegated to the
  layered engine instead of the removed `MinPortSlot`/`ConnectorClearance` heuristic.
- No left/right port's centre falls within its owning part box's own title area, even under a high
  connection count — the layered algorithm's automatic title-vs-side-port reservation, activated by
  flagging every part node `HasLabel: true, HasKeyword: true`, keeps ports clear of the box's own
  "«keyword» / name : type" header row.
- When `FindRoot` selects no root but the resolved `expose` scope directly includes a top-level
  `part` feature usage (e.g. `expose Namespace::*;` where `Namespace` is only a `part def` with one
  nested `part`), that feature renders directly as a boxless node — `LayoutTree.Nodes` holds exactly
  one box, with no frame labeled for the enclosing namespace anywhere in the tree.
- Two independent top-level scoped parts render as two non-overlapping top-level boxes with no
  wrapping frame.
- A top-level scoped feature that is itself typed by a container definition still recurses into its
  own nested parts, which appear as that top-level box's own `Children`.
- A connection between two top-level scoped parts is drawn as a connector line when the connection's
  own qualified name is itself within the resolved scope.
- A matched top-level feature whose qualified name is nested under another matched feature's own
  qualified name is excluded from the top-level set (not duplicated as its own separate node).
- When the resolved `expose` scope matches no `part` feature usage at all, the pre-existing minimal
  empty canvas is returned unchanged.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `InterconnectionView_BuildLayout_PartsAndConnections_ProducesBoxesPortsAndLines` | Box, parts, ports, and lines |
| `InterconnectionView_BuildLayout_RootContent_IsNestedAsRootBoxChildren` | Root box nesting invariant |
| `InterconnectionView_BuildLayout_TwoConnectionsSamePair_ProducesTwoConnectorsWithoutException` | Distinct waypoints |
| `InterconnectionView_BuildLayout_ThreeParallelConnections_ProducesThreeDistinctConnectors` | Six labeled ports |
| `InterconnectionView_BuildLayout_HighConnectionDegreePart_BoxesDoNotOverlapAndPortsAreLabeled` | No overlap |
| `InterconnectionView_BuildLayout_PartWithPorts_PortsNeverOverlapBoxTitleArea` | Ports clear of title area |
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
| `InterconnectionView_BuildLayout_ExposeNamespaceDirectChildren_NoRootDef_RendersTopLevelFeatureWithoutFrame` | Boxless single top-level feature, no frame |
| `InterconnectionView_BuildLayout_ExposeNamespaceDirectChildren_TwoTopLevelParts_ArrangesSideBySideNoFrame` | Two non-overlapping top-level boxes, no frame |
| `InterconnectionView_BuildLayout_ExposeNamespaceDirectChildren_TopLevelFeatureIsContainer_RecursesInterior` | Container recursion reused for top-level fallback |
| `InterconnectionView_BuildLayout_ExposeNamespaceDirectChildren_NoMatchingFeature_ReturnsMinimalCanvas` | Empty-canvas fallback preserved |
| `InterconnectionView_BuildLayout_ExposeNamespaceDirectChildren_ConnectionBetweenTopLevelFeatures_DrawsEdge` | Connector between top-level parts |
| `InterconnectionView_BuildLayout_ExposeNamespaceDirectChildren_NestedTopLevelFeatureExcluded_NotDuplicated` | Nested match excluded from top-level set |
