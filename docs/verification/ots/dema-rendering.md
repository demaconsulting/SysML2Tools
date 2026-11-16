## DemaConsulting.Rendering Verification

This document provides the verification evidence for the DemaConsulting.Rendering OTS software
item. Requirements for this OTS item are defined in the DemaConsulting.Rendering OTS Software
Requirements document.

### Required Functionality

`DemaConsulting.Rendering` is a family of SysML-agnostic layout and rendering packages consumed by
SysML2Tools:

- `DemaConsulting.Rendering` — the layout intermediate representation (`LayoutTree`, `LayoutBox`,
  `LayoutLine`, and related nodes), the layout property system, and `LayoutGraph`.
- `DemaConsulting.Rendering.Abstractions` — the `IRenderer` contract, `Theme`, `RenderOptions`,
  `RenderOutput`, `NotationMetrics`, `BoxMetrics`, and `ConnectorLabelPlacer`.
- `DemaConsulting.Rendering.Layout` — the `LayeredLayoutAlgorithm`, `LayoutEngine`, containment
  layout, and connector routing.
- `DemaConsulting.Rendering.Svg` — the `SvgRenderer`.
- `DemaConsulting.Rendering.CanvasNet` — the CanvasNet-based `PngRenderer` (and `JpegRenderer`).

SysML2Tools relies on the family to lay a graph of sized nodes and directed edges out into placed
boxes and routed orthogonal connectors, honor the requested layout direction (including a
top-to-bottom flow), pack container regions, route connectors around the boxes it places, and
render a layout tree to both SVG and PNG, and route an edge declared on an ancestor graph scope
between the nested endpoint nodes themselves (cross-hierarchy edges, available since version
0.3.0-beta.2), and stack the unconnected groups of a layered container's contents in the order the
nodes were added.

Version 0.3.0-beta.3 of the family adds the SysML v2 notation primitives that the State Transition
and Action Flow views rely on: thick fork and join bars (`BadgeShape.ThickHorizontalBar`), the
crossed-circle terminate badge (`BadgeShape.CrossedCircle`), the send and accept corner icons on a
box (`BoxCornerIcon`), obstacle-aware connector label placement (`ConnectorLabelPlacer`), and the
`LayoutGraphEdge.TargetEnd` end decoration that sizes the final approach of a back edge.

### Verification Approach

`DemaConsulting.Rendering` is verified by self-validation evidence from the SysML2Tools CI
pipeline. The SysML2Tools view layout strategies and renderers drive the package family end-to-end
on every test run: the `LayeredPlacement` helper builds a `LayoutGraph` and runs
`LayeredLayoutAlgorithm`, while the `Tool` and core `DiagramRenderer` render the resulting
`LayoutTree` through the package's SVG and PNG renderers. A passing pipeline run for all scenarios
below constitutes evidence that the requirements are satisfied.

### Test Scenarios

#### ActionFlowView_BuildLayout_ActionsAndSuccessions_ProducesBoxesMarkersAndFlows

**Scenario**: An action-flow model is laid out; the strategy builds a `LayoutGraph` of action nodes
and succession edges and runs the package's layered algorithm.

**Expected**: The package returns placed action boxes and one routed connector per succession.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-LayoutGraph`.

#### StateTransitionView_BuildLayout_StatesAndTransitions_ProducesBoxesBadgeAndLines

**Scenario**: A state-machine model is laid out through the package's layered algorithm.

**Expected**: The package returns placed state boxes and routed transition lines.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-LayoutGraph`.

#### InterconnectionView_BuildLayout_PartsAndConnections_ProducesBoxesPortsAndLines

**Scenario**: An interconnection model with parts, ports, and connections is laid out through the
package's layered algorithm.

**Expected**: The package returns placed part boxes with ports and routed connection lines that
turn at right angles.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-LayoutGraph`,
`SysML2Tools-OTS-DemaRendering-Routing`.

#### ActionFlowView_BuildLayout_ForwardChain_FlowsTopToBottomOrthogonally

**Scenario**: A forward chain of actions is laid out with the top-to-bottom (DOWN) direction.

**Expected**: The package places each successor below its predecessor and routes the successions as
orthogonal segments.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-Direction`,
`SysML2Tools-OTS-DemaRendering-Routing`.

#### StateTransitionView_BuildLayout_ForwardChain_FlowsTopToBottomOrthogonally

**Scenario**: A forward chain of states is laid out with the top-to-bottom (DOWN) direction.

**Expected**: The package places each target state below its source and routes the transitions
orthogonally.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-Direction`,
`SysML2Tools-OTS-DemaRendering-Routing`.

#### ActionFlowView_BuildLayout_Successions_FlowTopToBottom

**Scenario**: A set of successions is laid out with the top-to-bottom direction.

**Expected**: Every successor action is placed below its predecessor.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-Direction`.

#### InterconnectionView_BuildLayout_NestedContainer_PlacesChildrenInsideContainerBox

**Scenario**: A part with nested children is laid out; the package packs the container region.

**Expected**: Every child box is placed inside its container box.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-Containers`.

#### InterconnectionView_BuildLayout_ContainerSize_BoundsChildrenAndTitle

**Scenario**: A container part is laid out around its children and title area.

**Expected**: The container box is sized to bound all of its children and its title.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-Containers`.

#### InterconnectionView_BuildLayout_PartBoxes_DoNotOverlap

**Scenario**: An interconnection model with multiple parts is laid out.

**Expected**: The placed part boxes do not overlap one another.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-Containers`.

#### SvgRenderer_Render_EmptyTree_WritesValidSvg

**Scenario**: The package's `SvgRenderer` renders an empty `LayoutTree`.

**Expected**: A well-formed, valid SVG document is written to the output stream.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-RenderSvg`.

#### DiagramRenderer_RenderWorkspace_SoftwareStructureModel_ReturnsSvgOutput

**Scenario**: A full workspace is rendered to SVG through the core `DiagramRenderer` and the
package's `SvgRenderer`.

**Expected**: An SVG render output is produced for the view.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-RenderSvg`.

#### DiagramRenderer_RenderWorkspace_GeneralViewModel_SvgContainsElementNames

**Scenario**: A general view is rendered to SVG through the package's `SvgRenderer`.

**Expected**: The SVG document contains the view's model element names.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-RenderSvg`.

#### PngRenderer_Render_EmptyTree_WritesPngBytes

**Scenario**: The package's CanvasNet-based `PngRenderer` renders an empty `LayoutTree`.

**Expected**: Valid PNG image bytes are written to the output stream.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-RenderPng`.

#### DiagramRenderer_RenderWorkspace_SoftwareStructureModel_PngRenderer_ReturnsPngOutput

**Scenario**: A full workspace is rendered to PNG through the core `DiagramRenderer` and the
package's `PngRenderer`.

**Expected**: A PNG render output is produced for the view.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-RenderPng`.

#### DiagramRenderer_RenderWorkspace_GeneralViewModel_PngProducesValidOutput

**Scenario**: A general view is rendered to PNG through the package's `PngRenderer`.

**Expected**: A valid PNG image is produced.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-RenderPng`.

#### GeneralViewLayoutStrategy_BuildLayout_CrossPackageEdge_SiblingPackages_DeclaredAtRootAndRoutedToNestedBoxes

**Scenario**: Two sibling top-level packages each own a definition, and one definition specializes
the other. The General View strategy declares the specialization edge on the root graph scope with
the two nested definition nodes as its endpoints.

**Expected**: The package returns the edge as a routed connector at the root scope whose first and
last waypoints lie on the borders of the two definition boxes, not on the package folder borders.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-CrossHierarchyEdges`.

#### GeneralViewLayoutStrategy_BuildLayout_CrossPackageEdge_NestedVersusOuter_DeclaredInOuterScope

**Scenario**: A package owns a definition and a nested package whose definition specializes the
owner's definition. The strategy declares the edge on the owner folder's own child scope with a node
inside the nested folder as one endpoint.

**Expected**: The package returns the connector inside the owner folder's children, running from
the nested definition's box to the owner's definition box.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-CrossHierarchyEdges`.

#### LayeredLayoutAlgorithm_Layout_ContainerContents_StackUnconnectedGroupsByInsertionOrder

**Scenario**: A layered package-folder container holds a specialization pair (`S` specializing `C`)
and an otherwise unconnected node `H`, and has relationships leaving it at the root scope. The nodes are
added to the container in the order `C`, `S`, `H` in one run and `H`, `C`, `S` in another.

**Expected**: The package stacks the unconnected group in insertion order: `H` is placed below `C` in the
first run and above `C` in the second. This is the behavior the General View's ordering refinement
relies on; an upgrade that changes it fails this scenario.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-ComponentOrderFollowsInsertion`.

#### ActionFlowView_BuildLayout_ForkAndJoin_RenderThickBarBadges_WithoutLabel

**Scenario**: An action flow with a fork and a join is laid out and the strategy emits thick-bar
badges for them.

**Expected**: Two `BadgeShape.ThickHorizontalBar` badges are produced, and the renderers accept them.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-NotationBadges`.

#### ActionFlowView_BuildLayout_TerminateNode_RendersCrossedCircle_NoOutgoing

**Scenario**: An action flow ending in a terminate node is laid out.

**Expected**: A `BadgeShape.CrossedCircle` badge is produced for the terminate node.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-NotationBadges`.

#### ActionFlowView_BuildLayout_AcceptAndSend_RenderBoxesWithCornerIcons

**Scenario**: An action flow with an accept and a send node is laid out.

**Expected**: The accept and send boxes request `BoxCornerIcon.Accept` and `BoxCornerIcon.Send`.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-BoxCornerIcon`.

#### ActionFlowView_BuildLayout_PipelineGalleryModel_ShowsActionFlowNotation

**Scenario**: The gallery pipeline model is laid out.

**Expected**: Its accept and send boxes carry the corner icons and its thick bars, crossed circle,
and diamond are produced.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-BoxCornerIcon`.

#### StateTransitionView_BuildLayout_ElevatorGalleryModel_LabelsDoNotOverlapStateBoxes

**Scenario**: The package's `ConnectorLabelPlacer` places the transition labels of the gallery
elevator layout with the state boxes as obstacles.

**Expected**: No placed label overlaps a state box.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-LabelAvoidance`.

#### StateTransitionView_BuildLayout_FrameWidth_AccountsForLongTransitionLabel

**Scenario**: A state with a self-transition carrying a very long label is laid out; the placer is run
over the result.

**Expected**: The state definition frame contains the placed label.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-LabelAvoidance`.

#### LayeredPlacement_Place_EdgeTargetEnd_ForwardedToGraphEdges

**Scenario**: A three-node cycle is laid out with an open-chevron `TargetEnd` declared on every edge.

**Expected**: Every returned polyline, including the back edge, ends in a straight approach at least
as long as the marker.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-BackEdgeApproach`.

#### StateTransitionView_BuildLayout_BackEdge_OpenChevronWithFlushApproach

**Scenario**: A state machine whose last transition returns to the first state is laid out.

**Expected**: Every transition ends in an open chevron on an approach at least as long as the marker.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-BackEdgeApproach`.

#### ActionFlowView_BuildLayout_BackEdge_OpenChevronWithFlushApproach

**Scenario**: An action flow whose last succession returns to the first action is laid out.

**Expected**: Every succession ends in an open chevron on an approach at least as long as the marker.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-BackEdgeApproach`.
