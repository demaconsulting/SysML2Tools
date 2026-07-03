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
- `DemaConsulting.Rendering.Skia` — the Skia-based `PngRenderer` (and JPEG/WebP renderers).

SysML2Tools relies on the family to lay a graph of sized nodes and directed edges out into placed
boxes and routed orthogonal connectors, honor the requested layout direction (including a
top-to-bottom flow), pack container regions, route connectors around the boxes it places, and
render a layout tree to both SVG and PNG.

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

**Scenario**: The package's Skia-based `PngRenderer` renders an empty `LayoutTree`.

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
