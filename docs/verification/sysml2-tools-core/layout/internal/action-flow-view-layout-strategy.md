#### ActionFlowViewLayoutStrategy Verification

##### Verification Approach

`ActionFlowViewLayoutStrategy` is verified through unit tests in
`ActionFlowViewLayoutStrategyTests` that construct a synthetic `SysmlWorkspace` containing an
action definition with actions and successions, invoke `BuildLayout`, and assert on the returned
`LayoutTree`. Assertions count the action boxes, confirm the start (filled-circle) and done
(bullseye) markers and the flow lines, compare action box `Y` coordinates to confirm
top-to-bottom ordering, verify that succession polylines are orthogonal and that action boxes do
not overlap, and exercise branch-and-join and cyclic flows. No mocking is required; the strategy
depends only on the in-memory model, `LayeredPlacement`, and render options.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, net10.0, and net11.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `ActionFlowViewLayoutStrategyTests` pass with zero failures across all four target frameworks.
- An action definition yields one box per action, a start marker, a done marker, and flow lines.
- A succession's target action is positioned below its source action.
- Each succession flow edge is a dashed line with an open end marker at the target action.
- The connector from the start marker to an action and from an action to the done marker is drawn
  in the same style as a succession: a dashed line with an open end marker at its target.
- A forward chain of successions flows top-to-bottom with orthogonal succession polylines and no
  overlapping action boxes.
- A branch-and-join flow renders every action, attaches the start marker only to source actions and
  the done marker only to sink actions, and a cyclic flow still emits each succession with an open
  marker at its true target.
- An empty workspace yields a canvas with no nodes.
- Fork and join nodes are unlabeled thick bars sized to their fan-out, with every connection on the
  long face at least 16 px apart and every connector touching the drawn bar; decision and merge
  nodes are diamonds that connectors touch at the outline.
- A guarded succession is labeled `[guard]` and a succession declared with `else` is labeled
  `else`.
- A terminate node is a crossed circle that only receives successions and is not an inferred end:
  no done marker follows it.
- Accept and send nodes are rounded boxes with the corner icon and no keyword, listing their payload.
- An explicit `first start` / `then done` succession decides where the start and done markers
  attach, with no start or done box; without them the markers follow the topology, unchanged.
- A typed action reads `name : Type [m]`.
- Every succession, a back edge included, ends in an open chevron on an approach at least as long as
  the marker, and the gallery pipeline model shows the whole notation.
- The guard or `else` label of a decision branch (Pipeline and Order gallery models) sits on a
  vertical run; the diamond end, the sideways run, other connectors, and the arrowhead stay
  visible. A branch whose sideways run is longer than its drop keeps the engine's route, and
  sibling branches keep distinct sideways runs.
- A missing gallery model fails its test rather than passing vacuously.
- A `null` `ViewContext.ViewNode` selects the pre-scoping heuristic root and renders every action,
  unchanged from before this feature — the critical `--auto`/no-expose regression guard.
- A view whose resolved `Expose` edge names a definition other than the heuristic root selects
  that definition as the root instead.
- A view whose resolved `Expose` edge names an inner action of a non-heuristic-root definition
  selects that definition's own root.
- A view whose resolved `Expose` edge names a definition unrelated to any candidate root selects
  no root, producing the minimal empty canvas.
- A view whose resolved `Expose` edge names a single action drops a genuinely isolated
  out-of-scope action while still rendering any excluded action re-referenced by an in-scope
  succession.
- A view whose resolved `Expose` edge names a feature usage (not a definition) still resolves to
  the usage's type as the root, via the shared usage-to-type fallback.
- A view whose resolved `Expose` edge names an inner action of a definition genuinely nested
  inside another eligible root candidate selects the nested definition, not the ancestor, even
  though the ancestor has a higher succession/action score and would win the old pure-score
  tie-break.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `ActionFlowView_BuildLayout_ActionsAndSuccessions_ProducesBoxesMarkersAndFlows` | Action boxes, markers, and flows |
| `ActionFlowView_BuildLayout_Successions_FlowTopToBottom` | The source action sits above its successor |
| `ActionFlowView_BuildLayout_EmptyWorkspace_ReturnsMinimalCanvas` | Canvas with no nodes |
| `ActionFlowView_BuildLayout_SuccessionEdge_IsDashedWithOpenArrowhead` | Dashed line with open end marker |
| `ActionFlowView_BuildLayout_ForwardChain_FlowsTopToBottomOrthogonally` | Top-to-bottom orthogonal flow |
| `ActionFlowView_BuildLayout_NoOverlap` | Action boxes do not overlap |
| `ActionFlowView_BuildLayout_BranchAndJoin` | Branch/join; start/done connectors dashed, open marker |
| `ActionFlowView_BuildLayout_Cycle_IsBroken` | Cyclic flow successions keep open markers at true targets |
| `ActionFlowView_BuildLayout_NullViewNode_PicksHeuristicRootUnchanged` | Null `ViewNode` renders unchanged |
| `ActionFlowView_BuildLayout_ExposeNonHeuristicRoot_SelectsExposedRoot` | Non-heuristic root is selected |
| `ActionFlowView_BuildLayout_ExposeInnerChildOfNonHeuristicRoot_SelectsItsRoot` | Inner action selects root |
| `ActionFlowView_BuildLayout_ExposeUnrelatedDefinition_NoRootSelected_ReturnsMinimalCanvas` | Unrelated def, no root |
| `ActionFlowView_BuildLayout_ExposeSingleAction_DropsOutOfScopeAction` | Isolated action dropped; referenced kept |
| `ActionFlowView_BuildLayout_ExposedUsage_ResolvesThroughTypingToRoot` | Usage resolves via `Typing` to root |
| `ActionFlowView_BuildLayout_ExposeInnerActionOfNestedDefinition_SelectsNestedDefinitionNotAncestor` | Nested wins |
| `ActionFlowView_BuildLayout_ForkAndJoin_RenderThickBarBadges_WithoutLabel` | Fork/join render unlabeled thick bars |
| `ActionFlowView_BuildLayout_DecisionAndMerge_RenderDiamondBadges` | Decision/merge render diamond badges |
| `ActionFlowView_BuildLayout_AcceptAndSend_RenderBoxesWithCornerIcons` | Accept/send corner icons |
| `ActionFlowView_BuildLayout_SyntheticControlNodeName_RendersBlankLabel` | `$`-prefixed synthetic names render blank |
| `ActionFlowView_BuildLayout_CompactActionThenIdiom_ResolvesBothNodes` | Compact `action a; then b;` idiom resolves |
| `ActionFlowView_BuildLayout_ThickBar_LengthGrowsWithFanOut` | Bar length follows fan-out |
| `ActionFlowView_BuildLayout_ThickBar_AnchorsAtLeast16pxApart` | Connections on the bar at least 16 px apart |
| `ActionFlowView_BuildLayout_ThickBar_ConnectorsTouchTheBar` | Connectors meet the drawn bar |
| `ActionFlowView_BuildLayout_ThickBar_SingleConnection_RunsStraight` | A bar's only connection runs straight |
| `ActionFlowView_BuildLayout_Decision_ConnectorsTouchTheDiamondOutline` | Connectors meet the diamond outline |
| `ActionFlowView_BuildLayout_GuardedSuccession_LabelsEdge` | `[guard]` label |
| `ActionFlowView_BuildLayout_ElseSuccession_LabelsEdgeElse` | `else` label |
| `ActionFlowView_BuildLayout_TerminateNode_RendersCrossedCircle_NoOutgoing` | Crossed circle, receives only |
| `ActionFlowView_BuildLayout_TerminateNode_NotTreatedAsDoneSource` | Done marker skips terminate nodes |
| `ActionFlowView_BuildLayout_OnlyTerminateEnds_DrawsNoDoneMarker` | No inferred done marker |
| `ActionFlowView_BuildLayout_ExplicitStart_NoStartBox_MarkerEntersTarget` | Explicit start marker, no start box |
| `ActionFlowView_BuildLayout_ExplicitDone_ConnectsOnlyDeclaredSources` | Explicit done sources only |
| `ActionFlowView_BuildLayout_NoExplicitMarkers_HeuristicUnchanged` | Topology heuristic unchanged |
| `ActionFlowView_BuildLayout_TypedAction_LabelShowsTypeAndMultiplicity` | `name : Type [m]` label |
| `ActionFlowView_BuildLayout_AcceptPayload_RendersTextRow` | Payload row, box wide enough |
| `ActionFlowView_BuildLayout_BackEdge_OpenChevronWithFlushApproach` | Final approach at least the marker length |
| `ActionFlowView_BuildLayout_PipelineGalleryModel_ShowsActionFlowNotation` | Gallery model shows the notation |
| `ActionFlowView_BuildLayout_DecisionBranchLabels_SitOnVerticalDrop_AndKeepRunsVisible` | Labels on vertical runs |
| `ActionFlowView_BuildLayout_WideBranchBoxes_LabeledJogIsLeftUnchanged` | Long sideways run keeps engine route |
| `ActionFlowView_BuildLayout_DecisionWithManyBranches_KeepsDistinctJogLanes` | Sibling branches keep distinct runs |
| `ActionFlowView_BuildLayout_LongLeftBranchGuard_LabelIsInsideCanvas` | Left overhang moves content right |
| `ActionFlowView_BuildLayout_ExcludedBodies_RenderIdenticallyToWorkspaceWithout` | Hidden bodies are not drawn |
