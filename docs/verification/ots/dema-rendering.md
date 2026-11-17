## DemaConsulting.Rendering Verification

This document provides the verification evidence for the DemaConsulting.Rendering OTS software
item. Requirements for this OTS item are defined in the DemaConsulting.Rendering OTS Software
Requirements document.

### Required Functionality

`DemaConsulting.Rendering` is a family of SysML-agnostic layout and rendering packages consumed by
SysML2Tools:

- `DemaConsulting.Rendering` — the layout intermediate representation (`LayoutTree`, `LayoutBox`,
  `LayoutLine`, and related nodes), the layout property system, `LayoutGraph`, and the notation
  styling carried on graph nodes and compartments (`BoxAppearance`, compartment title alignment and
  style, and the extended `EndMarkerStyle` values).
- `DemaConsulting.Rendering.Abstractions` — the `IRenderer` contract, `Theme`, `RenderOptions`,
  `RenderOutput`, `NotationMetrics`, `BoxMetrics`, and `ConnectorLabelPlacer`.
- `DemaConsulting.Rendering.Layout` — the `LayeredLayoutAlgorithm`, `LayoutEngine`, containment
  layout, the automatic algorithm selection, and connector routing.
- `DemaConsulting.Rendering.Svg` — the `SvgRenderer`.
- `DemaConsulting.Rendering.CanvasNet` — the CanvasNet-based `PngRenderer` (and `JpegRenderer`).

SysML2Tools relies on the family to lay a graph of sized nodes and directed edges out into placed
boxes and routed orthogonal connectors, honor the requested layout direction (including a
top-to-bottom flow, cascaded to every nested container scope), pack container regions, route
connectors around the boxes it places, and render a layout tree to both SVG and PNG. It also relies
on the following features introduced by version 0.3.0-beta.3 of the family, in addition to the
cross-hierarchy edges of version 0.3.0-beta.2 (an edge declared on an ancestor graph scope is routed
between the nested endpoint nodes themselves) and the insertion-order stacking of the unconnected
groups of a layered container's contents:

- the per-node `BoxAppearance` (outline style, name style, folder-title placement) carried from the
  graph node to the placed box and drawn by both renderers (dashed reference usages, italic abstract
  definition names, package names in the folder tab);
- the compartment title alignment and title style (centered italic compartment titles);
- the `HollowTriangleTwoDots` and `HollowTriangleFourDots` end markers (feature typing and reference
  subsetting) in addition to the existing hollow-triangle, hollow-triangle-with-crossbar, diamond, and
  open-arrowhead markers;
- the layout direction set on the root graph cascading to nested folders and containers, and per-node
  algorithm overrides that survive the `auto` meta-algorithm's split into connected and unconnected
  groups.

Version 0.3.0-beta.3 of the family adds the SysML v2 notation primitives that the State Transition
and Action Flow views rely on: thick fork and join bars (`BadgeShape.ThickHorizontalBar`), the
crossed-circle terminate badge (`BadgeShape.CrossedCircle`), the send and accept corner icons on a
box (`BoxCornerIcon`), obstacle-aware connector label placement (`ConnectorLabelPlacer`), and the
`LayoutGraphEdge.TargetEnd` end decoration that sizes the final approach of a back edge. It also adds the
rounded, keyword-carrying lifeline header (`LayoutLifeline.Keyword`, `HeaderShape`, and `HeaderAppearance`,
with `BoxMetrics.LifelineHeaderHeight`) that the Sequence view relies on.

Version 0.3.0-beta.4 of the family (the minimum version SysML2Tools builds against) adds the SysML v2
port and connector end notation that the Interconnection view relies on: the
`PortAppearance` of a graph port (direction arrow, outlined glyph, dashed reference outline, and outward
`name : Type` label placement) and `LineEndLabel` end labels (role name above and multiplicity below the
line end) on graph edges and layout lines. It also
aligns the ports that several edges share on one box face straight, so the connectors between such
boxes no longer end with a small bend.

### Verification Approach

`DemaConsulting.Rendering` is verified by self-validation evidence from the SysML2Tools CI
pipeline. The SysML2Tools view layout strategies and renderers drive the package family end-to-end
on every test run: the `LayeredPlacement` helper builds a `LayoutGraph` and runs
`LayeredLayoutAlgorithm`, while the `Tool` and core `DiagramRenderer` render the resulting
`LayoutTree` through the package's SVG and PNG renderers. The notation styling drawn by the renderers is
additionally verified by focused tests that render small hand-built layout trees and assert on the SVG
elements and attributes and on decoded PNG pixels. A passing pipeline run for all scenarios
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

#### GeneralViewLayoutStrategy_BuildLayout_ReferenceUsage_OutlineIsDashedAndCompositeUsageIsSolid

**Scenario**: A reference usage and a composite usage are laid out as graph nodes whose box appearance
requests a dashed and a default outline.

**Expected**: The placed box of the reference usage carries the dashed outline and the composite
usage's box the solid one.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-NotationStyling`.

#### GeneralViewLayoutStrategy_BuildLayout_AbstractDefinition_NameIsItalicButAbstractUsageIsNot

**Scenario**: An abstract definition's graph node requests an italic name style.

**Expected**: The placed box carries the italic name style; the abstract usage's box carries the default.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-NotationStyling`.

#### GeneralViewLayoutStrategy_BuildLayout_PackageFolder_NameInTabOnlyWhenItHasContent

**Scenario**: A package folder with content requests the name in the tab; an empty package and a
collapsed package do not.

**Expected**: The placed folder box carries the tab placement only for the folder with content.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-NotationStyling`.

#### GeneralViewLayoutStrategy_BuildLayout_DefinitionWithUsages_ProducesCompartments

**Scenario**: A definition's compartments request centered, italic titles.

**Expected**: Every compartment of the placed box carries the centered alignment and italic style.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-NotationStyling`.

#### GeneralViewLayoutStrategy_BuildLayout_ReferenceSubsetting_ProducesHollowTriangleFourDotsEdgeOnlyWhenResolved

**Scenario**: A usage reference-subsets another; the edge requests the four-dots triangle marker.

**Expected**: The routed connector carries the four-dots triangle end marker.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-NotationStyling`.

#### GeneralViewLayoutStrategy_BuildLayout_AllUsages_AttributeUsage_IsNodeWithTypingAndHollowDiamond

**Scenario**: A typed usage and its definition are laid out; the typing edge requests the two-dots
triangle marker.

**Expected**: The routed connector carries the two-dots triangle end marker at the definition.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-NotationStyling`.

#### SvgRenderer_Render_DashedOutline_ReferenceUsageHasDashArrayAndCompositeUsageIsSolid

**Scenario**: A hand-built layout tree holds a rounded box whose appearance requests a dashed outline
(a reference usage) next to a rounded box with the default appearance (a composite usage); the
package's `SvgRenderer` renders it.

**Expected**: The outline rectangle of the dashed box carries a non-empty `stroke-dasharray`; the
outline rectangle of the default box carries none.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-NotationStyling`.

#### SvgRenderer_Render_ItalicName_AbstractDefinitionIsItalicAndConcreteIsNot

**Scenario**: A hand-built layout tree holds a box whose appearance requests the bold-italic name style
(an abstract definition) next to a default box (a concrete definition); the `SvgRenderer` renders it.

**Expected**: The abstract name text has `font-style="italic"` and stays bold; the concrete name text
has no `font-style` attribute.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-NotationStyling`.

#### SvgRenderer_Render_CompartmentTitle_CenteredAndItalicWhenRequested

**Scenario**: Three hand-built boxes carry a compartment with a centered italic title, a centered
regular-style title, and a default title; the `SvgRenderer` renders them.

**Expected**: The centered italic title has `text-anchor="middle"`, is positioned at the horizontal
center of its box, and has `font-style="italic"`. The regular-style title is also centered but has no
italic style, which shows that the requested style is carried through rather than a fixed default; the
default title is not middle-anchored.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-NotationStyling`.

#### SvgRenderer_Render_FolderNameInTab_NameInsideTabWithoutSecondTitleBand

**Scenario**: Two hand-built folder boxes with the same tab size are rendered; one requests the name in
the tab and the other keeps the default placement.

**Expected**: The tab-titled folder's name occurs exactly once and its position lies inside the tab
rectangle (so no second title band is drawn in the folder body); the default folder's name lies below
the tab.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-NotationStyling`.

#### SvgRenderer_Render_HollowTriangleTwoDots_MarkerHasTriangleAndTwoDots

**Scenario**: A hand-built connector ending in the `HollowTriangleTwoDots` marker is rendered by the
`SvgRenderer`.

**Expected**: The `line-end-hollow-triangle-two-dots` marker definition holds one polygon and exactly
two circles, and the connector path references it as its end marker. The assertions use the marker
identifier and the element counts, not coordinates.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-NotationStyling`.

#### SvgRenderer_Render_HollowTriangleFourDots_MarkerHasTriangleAndFourDots

**Scenario**: A hand-built connector ending in the `HollowTriangleFourDots` marker is rendered by the
`SvgRenderer`.

**Expected**: The `line-end-hollow-triangle-four-dots` marker definition holds one polygon and exactly
four circles in two distinct columns, and the connector path references it as its end marker.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-NotationStyling`.

#### SvgRenderer_Render_HollowTriangleCrossbar_MarkerHasTriangleAndBarWithoutDots

**Scenario**: A hand-built connector ending in the `HollowTriangleCrossbar` marker is rendered by the
`SvgRenderer`.

**Expected**: The `line-end-hollow-triangle-crossbar` marker definition holds one polygon and one
vertical bar line and no circles, and the connector path references it as its end marker.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-NotationStyling`.

#### PngRenderer_Render_DashedOutline_TopEdgeAlternatesDarkAndWhiteAndSolidEdgeDoesNot

**Scenario**: A hand-built layout tree holds a dashed-outline rectangle and a default rectangle; the
CanvasNet-based `PngRenderer` renders it and the PNG is decoded. A pixel probe walks the straight top
edge of each rectangle, sampling the two pixel rows the outline straddles.

**Expected**: The dashed edge alternates between dark outline and white background at least ten times;
the default edge never shows a white gap and is dark.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-NotationStyling`.

#### PngRenderer_Render_HollowTriangleFourDots_DrawsDarkDotsBehindTriangle

**Scenario**: A hand-built tree holds one connector ending in the `HollowTriangleFourDots` marker and one
ending in the plain `HollowTriangle` marker; the `PngRenderer` renders it and the PNG is decoded. A
pixel probe counts the separate dark spots in a band above and a band below each connector, just left
of the triangle.

**Expected**: Two dark spots are found in each band for the four-dots marker (four dots in total) and
none for the plain triangle. The probe depends on spot counts in a band, not on exact marker geometry.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-NotationStyling`.

#### GeneralViewLayoutStrategy_BuildLayout_TopDown_OwnerAndGeneralAboveUsageAndDefinitionBelow

**Scenario**: A definition owning a usage typed by another definition, and a specialization, are laid
out with the top-to-bottom direction set on the root graph only; the definitions sit in nested package
containers.

**Expected**: The package places the layout-edge sources above their targets, also inside the nested
containers.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-NestedDirectionAndAutoLayout`.

#### GeneralViewLayoutStrategy_BuildLayout_TopDown_UnconnectedDefinitionsDoNotOverlap

**Scenario**: A folder with twelve unconnected nodes and one connected pair is laid out with the
`auto` algorithm selection and the top-to-bottom direction, in a folder and at the root.

**Expected**: No two placed boxes overlap.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-NestedDirectionAndAutoLayout`.

#### GeneralViewLayoutStrategy_BuildLayout_DroneLikeModel_IsCompactAndTopDown

**Scenario**: A graph whose owner reaches eight usages, each typed by its own definition, is laid out
with the top-to-bottom direction.

**Expected**: The usages share one row below the owner, each definition hangs below its usage, and the
drawing stays compact.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-NestedDirectionAndAutoLayout`.

#### LayeredPlacement_PlaceWithPorts_EdgeNotation_ReachesPlacedPortsAndLines

**Scenario**: Two nodes are placed through `LayeredPlacement.PlaceWithPorts` with one edge that requests an
outlined outward `out` port at its source, a dashed outlined outward `in` port at its target, a filled
arrowhead, a dashed line, a midpoint label, and a role name and multiplicity at each end; the package's
layered algorithm lays the graph out.

**Expected**: The placed ports carry the requested labels and `PortAppearance`, and the placed line carries
the arrowhead, line style, midpoint label, and both end labels.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-PortGlyphs`,
`SysML2Tools-OTS-DemaRendering-EndLabels`.

#### SvgRenderer_Render_PortGlyph_DirectionArrowAndDashedReference

**Scenario**: A hand-built layout tree holds a box with an `in` port and a dashed `inout` reference port on
its left face and an `out` port on its right face, all outlined with outward labels; the `SvgRenderer`
renders it and the SVG is parsed.

**Expected**: Each port is a white 12 by 12 rounded square; only the reference port has a dash array; the
`in` arrow's tip is on the part side of the left face, the `out` arrow's tip is outside the part on the right
face, and the `inout` arrow has a head at both ends; the labels end left of the left face and start right
of the right face.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-PortGlyphs`.

#### PngRenderer_Render_PortGlyph_DrawsOutlinedSquare

**Scenario**: A hand-built tree holds a box whose left border is crossed by an `in` port; the
`PngRenderer` renders it and the PNG is decoded. Pixel probes sample the border above the glyph, the
inside of the glyph, and the glyph's own outline.

**Expected**: The border above the glyph is dark, the inside of the glyph is white (the glyph is filled over
the border), and the glyph's outline is dark.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-PortGlyphs`.

#### SvgRenderer_Render_EndLabels_RoleAboveMultiplicityBelow

**Scenario**: A hand-built tree holds a horizontal line with a role name and a multiplicity at each end; the
`SvgRenderer` renders it and the SVG is parsed.

**Expected**: Each role name is drawn above the line and each multiplicity below it, near its own end of
the line.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-EndLabels`.

#### SequenceView_BuildLayout_Lifeline_HasRoundedHeaderKeywordAndTypedLabel

**Scenario**: The Sequence view lays out a part lifeline typed `Client` through the package's
`LayoutLifeline`.

**Expected**: The lifeline carries the `part` keyword, the `client : Client` name line, the rounded header
shape, and the header height the package computes for a header with a keyword.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-LifelineHeader`.

#### SequenceView_BuildLayout_ReferencePartLifeline_HasDashedHeader

**Scenario**: The Sequence view lays out a reference part lifeline next to a plain part lifeline.

**Expected**: The reference lifeline carries the `ref part` keyword and a dashed header appearance; the
plain lifeline does not.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-LifelineHeader`.

#### SvgRenderer_Render_LifelineHeader_RoundedWithKeyword

**Scenario**: A hand-built layout tree holds a solid and a dashed rounded lifeline header, each with a
keyword; the `SvgRenderer` renders it and the SVG is parsed.

**Expected**: Both headers are rounded rectangles and only the reference header has a dash array; the keyword
is drawn above the name line, both inside the header and centered on the lifeline.

**Requirement coverage**: `SysML2Tools-OTS-DemaRendering-LifelineHeader`.
