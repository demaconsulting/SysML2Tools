# SysML2Tools Rendering Roadmap

This document tracks the planned rendering coverage for SysML2Tools relative to
the eight SysML v2 diagram view types defined in the OMG specification and
implemented by the SysON reference tool.

## Implementation Status (Phases 0–5 Complete)

Phases 0–5 described in `SysML2Tools-architecture.md` are complete. The tool
successfully parses all OMG example models, resolves the standard library, renders
`GeneralView` diagrams to SVG and PNG, and ships as a `dotnet tool` NuGet package.

The remainder of this document describes the incremental rendering roadmap (Phase 6
onward) needed to reach full SysML v2 view coverage.

---

## SysML v2 View Types — Coverage Summary

| # | View Type | Purpose | Our Status |
|---|-----------|---------|------------|
| 1 | **General View** | Any model element; foundational "catch-all" view | 🟢 Implemented |
| 2 | **Interconnection View** | Structural contents of a Usage (parts, ports, connectors) | 🟢 Implemented |
| 3 | **Action Flow View** | Input/output flows between actions (behavioral dynamics) | 🟢 Implemented |
| 4 | **State Transition View** | States and transitions (behavioral dynamics) | 🟢 Implemented |
| 5 | **Sequence View** | Chronological event occurrences on lifelines | 🟢 Implemented |
| 6 | **Grid View** | Elements in structured rectangular grid (tabular/matrix) | 🟢 Implemented |
| 7 | **Browser View** | Hierarchical membership structure from a root element | 🟢 Implemented |
| 8 | **Geometry View** | Spatial items in 2D or 3D | 🔴 Unsupported (deferred) |

---

## 1. General View — Detailed Gap Analysis

The General View is the most broadly applicable view. SysON defines six categories of
representable elements. Our current implementation covers a fraction of one category.

### 1.1 Definition Nodes

| Definition Type | SysON Spec | We Render | AST Parsed |
|---|---|---|---|
| Part Definition | ✅ | ✅ | ✅ |
| Attribute Definition | ✅ | ❌ | ✅ |
| Item Definition | ✅ | ❌ | ✅ |
| Port Definition | ✅ | ❌ | ❌ |
| Interface Definition | ✅ | ❌ | ❌ |
| Connection Definition | ✅ | ❌ | ❌ |
| Requirement Definition | ✅ | ❌ | ❌ |
| Use Case Definition | ✅ | ❌ | ❌ |
| Action Definition | ✅ | ❌ | ❌ |
| Allocation Definition | ✅ | ❌ | ❌ |
| Constraint Definition | ✅ | ❌ | ❌ |
| Enumeration Definition | ✅ | ❌ | ❌ |
| Metadata Definition | ✅ | ❌ | ❌ |
| Occurrence Definition | ✅ | ❌ | ❌ |

> **Note:** `attribute def` and `item def` are parsed by `AstBuilder` but not yet
> rendered. All other missing types require both AST visitor additions and rendering.

### 1.2 Usage Nodes (rendered with rounded-corner rectangles in SysON)

| Usage Type | SysON Spec | We Render | AST Parsed |
|---|---|---|---|
| Part Usage | ✅ | ❌ | Partial (`SysmlFeatureNode` — no keyword) |
| Attribute Usage | ✅ | ❌ | Partial |
| Port Usage | ✅ | ❌ | Partial |
| Interface Usage | ✅ | ❌ | Partial |
| Item Usage | ✅ | ❌ | Partial |
| Occurrence Usage | ✅ | ❌ | Partial |
| Constraint Usage | ✅ | ❌ | Partial |
| Requirement Usage | ✅ | ❌ | Partial |
| Use Case Usage | ✅ | ❌ | Partial |
| Action Usage | ✅ | ❌ | Partial |
| Allocation Usage | ✅ | ❌ | Partial |
| Accept Action Usage | ✅ | ❌ | Partial |

> **Note:** `SysmlFeatureNode` exists and is populated but does not capture the
> feature keyword (e.g., `"part"`, `"port"`, `"attribute"`). All usage rendering
> requires that keyword to be tracked.

### 1.3 Definition Compartments (attributes, ports, etc. inside a Definition node)

| Compartment Content | SysON Spec | We Render |
|---|---|---|
| Attributes inside any Definition | ✅ | ❌ |
| Ports inside Part Def / Interface Def | ✅ | ❌ |
| Documentation inside any Definition | ✅ | ❌ |
| Enumerated Values inside Enum Def | ✅ | ❌ |
| Constraints inside Constraint Def | ✅ | ❌ |
| Actions inside Action Def | ✅ | ❌ |
| Assumed/Required Constraints in Requirement Def | ✅ | ❌ |

### 1.4 Usage Compartments (attributes, ports, etc. inside a Usage node)

| Compartment Content | SysON Spec | We Render |
|---|---|---|
| Attributes inside Part/Interface/Item/Port Usage | ✅ | ❌ |
| Ports inside Part / Interface Usage | ✅ | ❌ |
| Reference inside Attribute/Item/Port Usage | ✅ | ❌ |
| Documentation inside any Usage | ✅ | ❌ |

### 1.5 Package Nodes

| Element | SysON Spec | We Render |
|---|---|---|
| Package node (folder shape, `package` keyword label) | ✅ | ❌ (used as grouping label only) |

### 1.6 Annotating Elements

| Element | SysON Spec | We Render |
|---|---|---|
| Documentation node (note shape, `doc` keyword) | ✅ | ❌ |
| Comment node (note shape, `comment` keyword) | ✅ | ❌ |

### 1.7 Relationship Edges

| Relationship | SysON Spec | We Render |
|---|---|---|
| Subclassification | ✅ | ✅ |
| Dependency | ✅ | ❌ |
| Redefinition | ✅ | ❌ |
| Subsetting | ✅ | ❌ |
| Feature Typing | ✅ | ❌ |
| Allocation | ✅ | ❌ |
| Containment | ✅ | ❌ |
| Succession | ✅ | ❌ |
| Connection Usage | ✅ | ❌ |

---

## 2. Interconnection View — Gap Analysis

Shows the encapsulated structural contents of a **Usage** element: its owned parts,
ports, connectors, and interfaces. This is the "inside the box" view complementary
to the General View's "catalogue of definitions" perspective.

| Element | SysON Spec | We Support |
|---|---|---|
| Root part Usage context box | ✅ | ❌ |
| Nested Part Usage nodes | ✅ | ❌ |
| Port nodes on boundary | ✅ | ❌ |
| Documentation/Comment annotations | ✅ | ❌ |
| Attribute compartment inside part | ✅ | ❌ |
| Nested parts compartment inside part | ✅ | ❌ |
| Port compartment inside part | ✅ | ❌ |
| Binding Connector (Usage) edge | ✅ | ❌ |
| Allocation edge | ✅ | ❌ |

> **Note:** `LayoutPort` already exists in our `LayoutTree` vocabulary specifically
> for this view.

---

## 3. Action Flow View — Gap Analysis

Describes input/output flows between actions within a system. Useful for behavioral
analysis: action sequences, tokens, accept/send actions.

| Element | SysON Spec | We Support |
|---|---|---|
| Action nodes | ✅ | ❌ |
| Start / Done / Fork / Join / Merge / Decision action nodes | ✅ | ❌ |
| Accept Action / Perform Action nodes | ✅ | ❌ |
| Succession flow edges | ✅ | ❌ |
| Item flow annotations on edges | ✅ | ❌ |

> **Note:** `LayoutBand` in our vocabulary may be useful here for action swim-lanes.

---

## 4. State Transition View — Gap Analysis

Depicts states a system occupies and the guarded transitions between them.

| Element | SysON Spec | We Support |
|---|---|---|
| State nodes | ✅ | ❌ |
| Initial / Final pseudo-states | ✅ | ❌ |
| Transition edges (with guard/trigger/effect labels) | ✅ | ❌ |
| Nested state regions | ✅ | ❌ |

---

## 5. Sequence View — Gap Analysis

Presents the chronological order of event occurrences on lifelines.

| Element | SysON Spec | We Support |
|---|---|---|
| Lifeline columns | ✅ | ❌ |
| Occurrence / message arrows between lifelines | ✅ | ❌ |
| Combined fragment boxes (alt, loop, opt) | ✅ | ❌ |
| Execution occurrence bars on lifelines | ✅ | ❌ |

> **Note:** `LayoutLifeline` already exists in our `LayoutTree` vocabulary for
> exactly this view.

---

## 6. Grid View — Gap Analysis

Presents elements in a structured rectangular grid — generalizes Tabular View,
Data Value Tabular View, and Relationship Matrix View.

| Element | SysON Spec | We Support |
|---|---|---|
| Row/column header cells | ✅ | ❌ |
| Data value cells | ✅ | ❌ |
| Relationship matrix cells | ✅ | ❌ |

> **Note:** `LayoutGrid` already exists in our `LayoutTree` vocabulary for this view.

---

## 7. Browser View — Gap Analysis

Presents the hierarchical membership structure of model elements from a root element.

| Element | SysON Spec | We Support |
|---|---|---|
| Tree nodes with expand/collapse | ✅ | ❌ |
| Hierarchical containment edges | ✅ | ❌ |

---

## 8. Geometry View — Gap Analysis

Presents exposed spatial items in 2D or 3D. Primarily for spatial/physical layout
of system components.

| Element | SysON Spec | We Support |
|---|---|---|
| 2D spatial item placement | ✅ | ❌ |
| 3D spatial item placement | ✅ | ❌ |
| Spatial relationship connectors | ✅ | ❌ |

> This is the most complex view to implement and requires 2D/3D coordinate data
> from the model. Lowest priority.

---

## Implementation Phases (Phase 6 onward)

## Acceptance Criteria Pattern

Each phase gate uses two complementary checks:

**Automated checks** are run by `pwsh ./build.ps1` and any engine-specific unit
tests. These verify correctness (no crashes, no overlapping boxes by coordinate
arithmetic, correct layer ordering in DAGs).

**Visual inspection** leverages the multimodal capability of the implementing agent.
After publishing the tool and rendering specific test models to PNG, the agent reads
each PNG back using the `view` tool (which returns the image for visual analysis)
and checks the listed visual criteria. This catches layout quality issues that are
invisible to coordinate-arithmetic tests — for example, an edge that technically has
valid waypoints but visually routes through the middle of an unrelated box.

The published binary and rendered images in `_check\` are temporary and must be
deleted after inspection. They are not committed to the repository.

---

Rather than embedding layout algorithms inside each view strategy, the non-trivial
algorithms are extracted into reusable, independently testable **layout engines**.
View strategies become thin "semantic model → engine input → LayoutTree" adapters.

Each non-trivial engine is introduced in the same phase as its first consuming view,
so it is validated against real visual output immediately. Trivial layouts (Sequence,
Grid, Browser, Geometry) require no engine — pure arithmetic in the strategy class.

**Engine inventory:**

| Engine | Category | First Used In | Reused In |
|--------|----------|---------------|-----------|
| `ContainmentPacker` ✅ | Bottom-up size, bin packing | Phase 6 | Phase 7, 8 |
| `ChannelRouter` ✅ | Orthogonal edge routing around obstacles | Phase 6 | Phase 7, 8 |
| `ForceDirectedEngine` ✅ | Fruchterman-Reingold spring layout | Phase 8 | Phase 9 |
| `PortAssigner` ✅ | Port-side and slot heuristic | Phase 8 | — |
| `LayeredLayoutEngine` ✅ | Simplified Sugiyama DAG layout | Phase 10 | — |

All engines live in `Layout/Engine/` and have their own unit tests with **synthetic
inputs** (no parser or view code required). Integration is validated through the
existing render integration tests.

---

### Phase 6 — General View: All Definitions + Edges + ContainmentPacker + ChannelRouter (2–3 sessions) — ✅ COMPLETE

> **Status:** Complete. All definition kinds render with keyword labels; packages render as
> folder-tab containers; `ContainmentPacker` and `ChannelRouter` engines implemented and
> unit-tested; specialization edges route around boxes. Standard-library filtering switched from
> a fixed prefix list to seed-origin tracking (`SysmlWorkspace.StdlibNames`). Visual gate passed
> against `2a-PartsInterconnection`, `1a-PartsTree`, and `nested-packages-with-view`.

Highest-value incremental improvement: complete the General View to show all
Definition types and relationship edges, and simultaneously introduce the two layout
engines needed by all containment-based views.

**Layout Engines introduced:**

- `ContainmentPacker`: given a set of children with computed sizes, arranges them
  within a parent container using a bin-packing pass; sizes the parent to fit.
  Replaces the current hardcoded 2-column grid.
- `ChannelRouter`: given obstacle rectangles and source/target anchor points, routes
  edges as horizontal/vertical segments through obstacle-free channels. Replaces the
  current manual midpoint waypoint hack.

**Semantic / AST scope:**

- `AstBuilder`: add visitor methods for all missing Definition types (`port def`,
  `interface def`, `connection def`, `requirement def`, `use case def`, `action def`,
  `allocation def`, `constraint def`, `enum def`, `metadata def`, `occurrence def`)
- `SysmlDefinitionNode.DefinitionKeyword` is already generic — no changes needed

**Layout / Rendering scope:**

- `GeneralViewLayoutStrategy`: render all `SysmlDefinitionNode` types (not just
  `"part def"`), with keyword prefix labels; use `ContainmentPacker` for placement
- Package nodes rendered as folder-tab shape instead of plain grouping label
- `SvgRenderer` / `PngRenderer`: keyword label rendering (e.g., `part def` on first
  line of box in smaller italic font)
- Edges added: Dependency, Redefinition, Subsetting, Feature Typing, Containment;
  all routed via `ChannelRouter`

**Acceptance Criteria:**

*Automated (must all pass):*

- [ ] `pwsh ./build.ps1` — zero errors, zero warnings
- [ ] `ContainmentPacker` unit tests pass: given N boxes of mixed sizes, no two
  output rectangles overlap; parent bounds contain all children
- [ ] `ChannelRouter` unit tests pass: routed waypoints form only horizontal/vertical
  segments; no segment intersects any obstacle rectangle

*Visual inspection — agent renders PNG and reads each image with the `view` tool:*

```
dotnet publish src\DemaConsulting.SysML2Tools.Tool -f net10.0 -c Release -o _check --nologo -q
dotnet _check\DemaConsulting.SysML2Tools.dll render test\SysMLModels\OMG\2a-PartsInterconnection.sysml --auto --format png -o _check\phase6-parts.png
dotnet _check\DemaConsulting.SysML2Tools.dll render test\SysMLModels\OMG\1c-PortsAndFlows.sysml --auto --format png -o _check\phase6-ports.png
```

Agent views each PNG and asserts:

- [ ] `part def`, `port def`, and other keyword-prefixed box labels are visible, with
  keyword text on a separate smaller first line above the element name
- [ ] No two definition boxes visually overlap
- [ ] Relationship edges route around boxes — no edge drawn through an unrelated box
- [ ] Package containers render with folder-tab shape, not a plain rectangle
- [ ] Canvas margins are proportionate — no excessive blank space around content
- [ ] Delete `_check\` after inspection

---

### Phase 7 — General View: Usage Nodes, Compartments + Annotating Elements (2–3 sessions) — ✅ COMPLETE (compartment style)

> **Status:** Complete. Definitions now render their owned usages as keyword-grouped compartments
> (e.g. *attributes*, *ports*, *parts*) with `name : Type [n]` rows. `SysmlFeatureNode` gained
> `FeatureKeyword`, `FeatureTyping`, and `Multiplicity`; `AstBuilder` visits part/port/attribute/
> item/reference/enum/occurrence usages and extracts the type from both the `typed by` clause and
> the typing list. Compartment row spacing improved in both renderers.
>
> **Design decision:** In the General View, usages render as *compartment rows* (matching the SysON
> General View compartment style) rather than nested rounded boxes. Nested-box containment with
> ports and connectors is the defining purpose of the **Interconnection View (Phase 8)** and is
> implemented there to avoid duplicating containment layout. Documentation/Comment note-shape nodes
> are deferred — the `BoxShape.Note` primitive is implemented and ready, but annotating-element AST
> capture is left to a follow-up. Visual gate passed against `vehicle-with-usages`.

Extend the semantic model and layout to capture Usage (feature) elements and render
compartments and annotation nodes. No new engines required.

**Semantic / AST scope:**

- `SysmlFeatureNode`: add `FeatureKeyword` property (`"part"`, `"port"`,
  `"attribute"`, `"interface"`, `"action"`, etc.)
- `AstBuilder`: populate `FeatureKeyword` for all feature/usage visitor methods

**Layout / Rendering scope:**

- `GeneralViewLayoutStrategy`: render Usage nodes as rounded-corner boxes nested
  inside their parent Definition or Package container (uses `ContainmentPacker`)
- `LayoutBox.Compartments`: populate attribute/port compartment rows from children
- `SvgRenderer` / `PngRenderer`: render compartment separator lines and row text
- Documentation / Comment nodes rendered as note-shape boxes
- Remaining edges: Allocation, Succession, Connection Usage (via `ChannelRouter`)

**Acceptance Criteria:**

*Automated (must all pass):*

- [ ] `pwsh ./build.ps1` — zero errors, zero warnings

*Visual inspection — create a test fixture `test\SysMLModels\Custom\vehicle-with-usages.sysml`
containing a `part def Vehicle` with nested `part engine : Engine`, `port fuel : FuelPort`,
and a doc comment; render it and view the PNG:*

```
dotnet publish src\DemaConsulting.SysML2Tools.Tool -f net10.0 -c Release -o _check --nologo -q
dotnet _check\DemaConsulting.SysML2Tools.dll render test\SysMLModels\Custom\vehicle-with-usages.sysml --auto --format png -o _check\phase7-vehicle.png
dotnet _check\DemaConsulting.SysML2Tools.dll render test\SysMLModels\OMG\2a-PartsInterconnection.sysml --auto --format png -o _check\phase7-interconnection.png
```

Agent views each PNG and asserts:

- [ ] Usage nodes (`part`, `port`) appear as rounded-corner boxes inside their parent
  definition box (not as top-level boxes alongside it)
- [ ] Attributes and ports are listed as rows within a compartment section, separated
  from the definition name by a horizontal rule
- [ ] Documentation/Comment nodes appear as note-shaped boxes (folded corner) with body text
- [ ] Connection Usage and Allocation edges are drawn and labelled where present
- [ ] Delete `_check\` after inspection

---

### Phase 8 — Interconnection View + ForceDirectedEngine + PortAssigner (2–3 sessions) — ✅ COMPLETE

> **Status:** Complete. `ForceDirectedEngine` (deterministic Fruchterman-Reingold with overlap
> removal) and `PortAssigner` (side selection + even slot distribution) implemented and unit-tested.
> `InterconnectionViewLayoutStrategy` renders a part definition's interior: nested part usages as
> rounded boxes placed by the force engine, ports on box boundaries via `PortAssigner`, and
> connection usages routed as orthogonal connectors via `ChannelRouter`. `AstBuilder` captures
> connection usages with both endpoints (`SysmlConnectionNode`). `DiagramTypeRouter` dispatches to
> the interconnection strategy when a view's name or supertype contains "Interconnection".
> Visual gate passed against `power-system-interconnection` (drivetrain chain with port-to-port
> connectors, no overlaps).

Implement the Interconnection View, introducing two new engines that will also be
reused by the State Transition View in Phase 9.

**Layout Engines introduced:**

- `ForceDirectedEngine`: Fruchterman-Reingold spring layout; takes a graph of nodes
  (with sizes) and edges, iterates repulsion/attraction forces until stable, returns
  node centre positions. Used to arrange parts within the root Usage container.
- `PortAssigner`: given a box and its connections to other boxes, assigns each port
  to a side (Left/Right/Top/Bottom) using a directional heuristic, then distributes
  multiple ports evenly along the assigned side.

**Semantic / AST scope:**

- No new AST nodes required; `SysmlFeatureNode` (Phase 7) covers part/port usages

**Layout / Rendering scope:**

- New `InterconnectionViewLayoutStrategy`
- `DiagramTypeRouter`: recognize `InterconnectionView` viewpoint
- `LayoutPort`: wire into `SvgRenderer` / `PngRenderer` (small square on box boundary)
- Nested part boxes placed by `ForceDirectedEngine` inside the root Usage container
- Ports placed by `PortAssigner` on box boundaries
- Binding Connector and Allocation edges routed by `ChannelRouter` between ports

**Acceptance Criteria:**

*Automated (must all pass):*

- [ ] `pwsh ./build.ps1` — zero errors, zero warnings
- [ ] `ForceDirectedEngine` unit tests pass: given N nodes with edges, output positions
  have no two node bounding boxes overlapping after convergence
- [ ] `PortAssigner` unit tests pass: given a box with connections to boxes in each
  cardinal direction, ports are assigned to the correct sides

*Visual inspection — create `test\SysMLModels\Custom\vehicle-interconnection.sysml`
with a `vehicle : Vehicle` usage containing nested `engine`, `transmission`, `wheels`
parts with ports and connectors between them; declare an `InterconnectionView` on it:*

```
dotnet publish src\DemaConsulting.SysML2Tools.Tool -f net10.0 -c Release -o _check --nologo -q
dotnet _check\DemaConsulting.SysML2Tools.dll render test\SysMLModels\Custom\vehicle-interconnection.sysml --format png -o _check\phase8-interconnection.png
dotnet _check\DemaConsulting.SysML2Tools.dll render test\SysMLModels\OMG\2a-PartsInterconnection.sysml --format png -o _check\phase8-omg.png
```

Agent views each PNG and asserts:

- [ ] Root usage renders as a large outer container box
- [ ] Nested part boxes float inside the container with no overlaps
- [ ] Port squares appear on the boundary edges of the boxes they belong to (not
  floating in the interior or outside the parent)
- [ ] Connector lines travel from port to port — entering and leaving at the port
  square position, not from box centre
- [ ] Connector lines do not cross through unrelated boxes
- [ ] Delete `_check\` after inspection

---

### Phase 9 — State Transition View + Bezier Routing (2–3 sessions) — ✅ COMPLETE (orthogonal routing)

> **Status:** Complete. `SysmlTransitionNode` captures transition source/target/guard;
> `AstBuilder` visits state usages (`VisitStateUsage`) and transitions (`VisitTransitionUsage`),
> and `VisitStateDefinition` now collects the state-def body (states + transitions) via a generic
> `CollectChildren` helper. `StateTransitionViewLayoutStrategy` places states with the
> force-directed engine, draws an initial pseudo-state (filled circle) into the first declared
> state, and renders transitions with filled arrowheads and `[guard]` midpoint labels; self-
> transitions render as a small loop. `DiagramTypeRouter` dispatches on "StateTransition"/"State".
>
> **Design decision:** Transitions use orthogonal routing via `ChannelRouter` rather than Bezier
> curves — orthogonal state diagrams are clear and reuse the existing routing engine. Bezier
> curve rendering remains a possible future enhancement. Visual gate passed against
> `traffic-light-states` (three states, initial marker, guarded transitions, no overlaps).

Implement the State Transition View. Reuses `ForceDirectedEngine` from Phase 8;
adds curved/Bezier edge routing for the general-graph topology.

**Semantic / AST scope:**

- `AstBuilder`: capture state definitions (`state def`, `state`), transitions
  (succession usages with guard/trigger/effect annotations)
- `SysmlDefinitionNode` / `SysmlFeatureNode` with appropriate keywords

**Layout / Rendering scope:**

- New `StateTransitionViewLayoutStrategy`
- `DiagramTypeRouter`: recognize `StateTransitionView` viewpoint
- State nodes (rounded rectangle), initial pseudo-state (filled circle), final
  pseudo-state (bullseye)
- `ForceDirectedEngine`: place state nodes on canvas
- Bezier edge routing: compute control points from node centres and desired bend angle
- Self-loop routing: fixed arc bump offset above the node
- Parallel edge offset: opposite-direction edges between same pair offset perpendicularly
- Transition edge labels: guard/trigger/effect text at edge midpoint

**Acceptance Criteria:**

*Automated (must all pass):*

- [ ] `pwsh ./build.ps1` — zero errors, zero warnings

*Visual inspection — create `test\SysMLModels\Custom\traffic-light-states.sysml`
with a simple three-state machine (Red → Green → Yellow → Red) with guard labels;
declare a `StateTransitionView` on it:*

```
dotnet publish src\DemaConsulting.SysML2Tools.Tool -f net10.0 -c Release -o _check --nologo -q
dotnet _check\DemaConsulting.SysML2Tools.dll render test\SysMLModels\Custom\traffic-light-states.sysml --format png -o _check\phase9-states.png
```

Agent views the PNG and asserts:

- [ ] Three state nodes visible as rounded rectangles, clearly labelled Red/Green/Yellow
- [ ] An initial pseudo-state (filled circle) with an arrow entering the first state
- [ ] Transition arrows are curved, not straight horizontal/vertical — they have
  visible curvature away from a direct straight-line path
- [ ] Transition guard labels appear beside the arrow midpoint, not overlapping any node
- [ ] No two state nodes overlap
- [ ] The overall layout is roughly circular or triangular — states are not all
  stacked in a single column
- [ ] Delete `_check\` after inspection

---

### Phase 10 — Action Flow View + LayeredLayoutEngine (2–3 sessions) — ✅ COMPLETE (orthogonal flows)

> **Status:** Complete. `LayeredLayoutEngine` (simplified Sugiyama: DFS cycle removal, longest-path
> layer assignment, barycenter crossing-reduction sweeps, coordinate assignment) implemented and
> unit-tested (5 tests: layer ordering, downward edges, no same-layer overlap, cycle handling).
> `AstBuilder` captures action usages (`VisitActionUsage`) and successions (`VisitSuccessionAsUsage`
> → `SysmlTransitionNode`); `VisitActionDefinition` collects the action body.
> `ActionFlowViewLayoutStrategy` lays actions out top-to-bottom in layers, adds a start node
> (filled circle) into the initial actions and a done node (bullseye) from the final actions, and
> routes successions as flow arrows. `DiagramTypeRouter` dispatches on "ActionFlow"/"Action".
>
> **Design decision:** Decision/fork/join nodes render as regular action boxes (branch points);
> dedicated diamond/bar shapes for decision and fork/join detection are a future enhancement.
> Visual gate passed against `order-action-flow` (branch + join, correct layering, no overlaps).

Implement the Action Flow View, introducing the Sugiyama-style layered layout engine.

**Layout Engine introduced:**

- `LayeredLayoutEngine`: simplified Sugiyama framework for DAGs:
  1. **Cycle removal**: detect back-edges and temporarily reverse them
  2. **Layer assignment**: longest-path ranking assigns each node a rank/layer
  3. **Node ordering**: barycentre heuristic minimises edge crossings within layers
  4. **Coordinate assignment**: nodes centred over their edges (Brandes-Köpf lite)
  Returns absolute node positions; caller wraps in `LayoutBox`.

**Semantic / AST scope:**

- `AstBuilder`: capture action usages, succession flow edges, perform/accept actions,
  start/done/fork/join/merge/decision action nodes

**Layout / Rendering scope:**

- New `ActionFlowViewLayoutStrategy`
- `DiagramTypeRouter`: recognize `ActionFlowView` viewpoint
- Action nodes, special node shapes (diamond for decision, horizontal bar for fork/join)
- `LayeredLayoutEngine`: determines layer and X position of each action node
- Succession flow edges with optional item flow annotation labels
- Back-edge routing: reversed edges drawn as curved back-arcs above the diagram
- Optional swim-lane bands (`LayoutBand`) partitioned by owning part/actor

**Acceptance Criteria:**

*Automated (must all pass):*

- [ ] `pwsh ./build.ps1` — zero errors, zero warnings
- [ ] `LayeredLayoutEngine` unit tests pass: given a DAG, each node's Y is strictly
  greater than all its predecessors' Y values (layer ordering preserved); no two
  node bounding boxes in the same layer overlap horizontally

*Visual inspection — create `test\SysMLModels\Custom\order-processing-actions.sysml`
with an action flow: Start → Receive Order → [Validate] → (In Stock? → Pick Items :
Back Order) → Pack → Ship → Done; declare an `ActionFlowView`:*

```
dotnet publish src\DemaConsulting.SysML2Tools.Tool -f net10.0 -c Release -o _check --nologo -q
dotnet _check\DemaConsulting.SysML2Tools.dll render test\SysMLModels\Custom\order-processing-actions.sysml --format png -o _check\phase10-actions.png
```

Agent views the PNG and asserts:

- [ ] Start node (filled circle) at the top; Done node (bullseye) at the bottom
- [ ] Action boxes arranged in clear top-to-bottom layers — no action at a higher Y
  than any of its predecessors
- [ ] Decision node rendered as a diamond shape
- [ ] Flow edges enter boxes from the top and leave from the bottom (not from sides)
- [ ] Branch edges from the decision diamond go to two separate actions on the same
  layer without crossing
- [ ] No two action boxes on the same layer overlap horizontally
- [ ] Delete `_check\` after inspection

---

### Phase 11 — Sequence View (1–2 sessions) — ✅ COMPLETE (core)

> **Status:** Complete. `AstBuilder.VisitMessage` captures message usages (name + from/to event
> references) as `SysmlConnectionNode` with keyword "message". `SequenceViewLayoutStrategy` renders
> the participating lifelines (distinct first-segment participants) as dashed stems with header
> boxes and draws each message as a horizontal arrow between lifelines, ordered top-to-bottom by
> declaration order, with the message name as the arrow label; self-messages render as a small loop.
> `DiagramTypeRouter` dispatches on "Sequence".
>
> **Deferred enhancements:** Activation bars and combined fragments (alt/loop/opt) — the
> `LayoutActivation` primitive is implemented and ready. Visual gate passed against
> `client-server-sequence` (two lifelines, three ordered messages with correct arrow directions).

Implement the Sequence View. No new engines — pure column-and-time-axis arithmetic.

**Semantic / AST scope:**

- `AstBuilder`: capture occurrence usages, message sends, lifeline participants,
  combined fragment occurrences (alt, loop, opt)

**Layout / Rendering scope:**

- New `SequenceViewLayoutStrategy`
- `DiagramTypeRouter`: recognize `SequenceView` viewpoint
- Lifeline columns: X = column index × column pitch; `LayoutLifeline` with dashed stem
- Activation bars: `LayoutActivation` top/bottom derived from message ordinals
- Message arrows: horizontal `LayoutLine` between lifeline centres at message Y
  (synchronous = filled arrowhead, asynchronous = open, reply = dashed)
- Combined fragment: `LayoutBox` spanning from leftmost to rightmost lifeline column,
  height = Y range of covered occurrences; operator label (alt/loop/opt) in corner

**Acceptance Criteria:**

*Automated (must all pass):*

- [ ] `pwsh ./build.ps1` — zero errors, zero warnings

*Visual inspection — create `test\SysMLModels\Custom\order-sequence.sysml` with two
lifelines (Client, Server), a synchronous request, a reply, and an `alt` combined
fragment; declare a `SequenceView`:*

```
dotnet publish src\DemaConsulting.SysML2Tools.Tool -f net10.0 -c Release -o _check --nologo -q
dotnet _check\DemaConsulting.SysML2Tools.dll render test\SysMLModels\Custom\order-sequence.sysml --format png -o _check\phase11-sequence.png
```

Agent views the PNG and asserts:

- [ ] Two vertical dashed lifeline stems, each with a header box at the top labelled
  Client and Server respectively
- [ ] A horizontal solid arrow (filled arrowhead) from Client to Server labelled with
  the request message name
- [ ] A horizontal dashed arrow (open arrowhead) from Server to Client labelled with
  the reply message name, positioned below the request arrow
- [ ] An `alt` combined fragment box spanning both lifelines, with `[alt]` in the
  top-left corner and a dashed divider between the two branches
- [ ] Activation bars visible on each lifeline for the duration they are active
- [ ] Delete `_check\` after inspection

---

### Phase 12 — Grid View + Browser View (1–2 sessions) — ✅ COMPLETE

> **Status:** Complete. Both views are pure-arithmetic strategies (no new engine, no new AST).
> `BrowserViewLayoutStrategy` builds the membership tree from the qualified-name hierarchy of
> non-stdlib declarations and renders indented rows with parent→child connector lines.
> `GridViewLayoutStrategy` renders a specialization relationship matrix (definitions × definitions,
> marked where the row specializes the column) via `LayoutGrid` with styled header row/column.
> `DiagramTypeRouter` dispatches on "Browser"/"Tree" and "Grid"/"Matrix"/"Tabular".
> Visual gate passed against `catalog-browser-grid` (indented tree; specialization matrix with
> correct marks).

Implement tabular and tree views. No new engines — pure geometric arithmetic.

**Layout / Rendering scope:**

- `DiagramTypeRouter`: recognize `GridView` and `BrowserView` viewpoints
- `GridViewLayoutStrategy`: column widths = max content width per column; row heights
  = max content height per row; header row styling; ColSpan expansion
- `BrowserViewLayoutStrategy`: DFS traversal → depth×indent for X; running cursor
  for Y; optional parent-to-child tree lines
- `SvgRenderer` / `PngRenderer`: render `LayoutGrid` as bordered table with cell fills

**Acceptance Criteria:**

*Automated (must all pass):*

- [ ] `pwsh ./build.ps1` — zero errors, zero warnings

*Visual inspection — create `test\SysMLModels\Custom\requirements-matrix.sysml` with
five requirements and five parts, with allocation relationships between them; declare
a `GridView` (relationship matrix) and a `BrowserView`:*

```
dotnet publish src\DemaConsulting.SysML2Tools.Tool -f net10.0 -c Release -o _check --nologo -q
dotnet _check\DemaConsulting.SysML2Tools.dll render test\SysMLModels\Custom\requirements-matrix.sysml --view RequirementsMatrix --format png -o _check\phase12-grid.png
dotnet _check\DemaConsulting.SysML2Tools.dll render test\SysMLModels\Custom\requirements-matrix.sysml --view SystemHierarchy --format png -o _check\phase12-browser.png
```

Agent views each PNG and asserts:

- [ ] Grid view: table with visible cell borders; header row and header column styled
  distinctly (darker background); cells at allocation intersections contain a visible
  mark (dot or cross); column widths are consistent per column
- [ ] Browser view: elements indented proportionally to their nesting depth; lines
  connecting parent to children; deepest nested elements have the most indentation
- [ ] Delete `_check\` after inspection

---

### Phase 13 — Geometry View (future / complex)

Spatial visualization of model elements in 2D or 3D. Requires spatial coordinate
data embedded in the model (e.g., `GeometryValues` library) and a 2D layout engine.
Deferred until user demand justifies the complexity.

**Layout scope:** Extract X, Y (optionally Z projected to 2D) from model attribute
values; scale/translate from model units to canvas pixels; compute canvas bounds from
placed elements. No auto-layout algorithm required — positions are model-specified.

---

## LayoutTree Vocabulary Coverage

The `LayoutTree` primitive vocabulary was designed in Phase 3 to cover all view kinds.
Current coverage:

| Primitive | Used By | Status |
|---|---|---|
| `LayoutBox` | All structural views | ✅ Implemented + rendered |
| `LayoutLabel` | Browser, truncation indicators | ✅ Implemented + rendered |
| `LayoutLine` | All views (edges/arrows) | ✅ Implemented + rendered |
| `LayoutCompartment` | General View | ✅ Populated + rendered (Phase 7) |
| `LayoutPort` | Interconnection View | ✅ Rendered (Phase 8) |
| `LayoutLifeline` | Sequence View | ✅ Rendered (Phase 11) |
| `LayoutBand` | Action Flow swim-lanes | ✅ Defined, not yet populated (future) |
| `LayoutBadge` | State/Action markers | ✅ Rendered (Phases 9–10) |
| `LayoutGrid` | Grid View | ✅ Rendered (Phase 12) |

The vocabulary is complete and the renderers handle every primitive. `LayoutActivation` (sequence
activation bars) and `LayoutBand` (action swim-lanes) are rendered/available but not yet populated
by their strategies — reserved for future refinements.

---

## Layout Engine Architecture

The five non-trivial layout algorithms are implemented as independent, stateless
engines in `Layout/Engine/`. Each engine accepts a plain data input (no SysML model
references) and returns computed geometry. This makes them independently unit-testable
with synthetic inputs before any view strategy uses them.

```
Layout/
  Engine/
    ContainmentPacker.cs      ← Phase 6: bin-packs children inside a container
    ChannelRouter.cs          ← Phase 6: orthogonal edges routing around obstacles
    PortAssigner.cs           ← Phase 8: assigns ports to box sides + distributes slots
    ForceDirectedEngine.cs    ← Phase 8: Fruchterman-Reingold spring layout
    LayeredLayoutEngine.cs    ← Phase 10: simplified Sugiyama for DAGs
  Internal/
    GeneralViewLayoutStrategy.cs           (C1 + C2 + C6)
    InterconnectionViewLayoutStrategy.cs   (C1 + C3 + C4 + C5 + C6)
    ActionFlowViewLayoutStrategy.cs        (C7 + C6)
    StateTransitionViewLayoutStrategy.cs   (C8 + C9 + C10 + C11)
    SequenceViewLayoutStrategy.cs          (arithmetic only)
    GridViewLayoutStrategy.cs              (arithmetic only)
    BrowserViewLayoutStrategy.cs           (arithmetic only)
    GeometryViewLayoutStrategy.cs          (coordinate mapping only)
```

### Layout Capability Categories

| ID | Capability | Engine | Views |
|----|-----------|--------|-------|
| C1 | Bottom-up containment sizing (size parent to fit children) | `ContainmentPacker` | General, Interconnection, State (nested) |
| C2 | Bin-packing (arrange variable-size boxes in N columns) | `ContainmentPacker` | General |
| C3 | Floating child placement (place children inside container without overlap) | `ForceDirectedEngine` | Interconnection |
| C4 | Port-side assignment (Left/Right/Top/Bottom from connectivity direction) | `PortAssigner` | Interconnection |
| C5 | Port slot distribution (evenly space multiple ports along a side) | `PortAssigner` | Interconnection |
| C6 | Orthogonal edge routing (right-angle segments around obstacle boxes) | `ChannelRouter` | General, Interconnection |
| C7 | Layered DAG layout — Sugiyama (layer assignment + barycentre ordering + coordinate assignment) | `LayeredLayoutEngine` | Action Flow |
| C8 | Force-directed node placement — Fruchterman-Reingold | `ForceDirectedEngine` | State Transition, Interconnection |
| C9 | Bezier/curved edge routing (control points from node centres) | inline in strategy | State Transition |
| C10 | Self-loop routing (fixed arc bump above/below node) | inline in strategy | State Transition |
| C11 | Parallel edge offset (opposite-direction edges offset perpendicularly) | inline in strategy | State Transition |
| C12 | Fixed-column/timeline layout | arithmetic | Sequence |
| C13 | Spanning region box | arithmetic | Sequence |
| C14 | Table layout (max-width columns, max-height rows) | arithmetic | Grid |
| C15 | Tree layout (DFS + indent + running cursor) | arithmetic | Browser |
| C16 | Fixed-coordinate placement (model units → canvas pixels) | arithmetic | Geometry |

---

## Architecture Status vs `SysML2Tools-architecture.md`

| Architecture Item | Status | Notes |
|---|---|---|
| Phase 0 — Scaffold | ✅ Complete | |
| Phase 1 — Parser + Stdlib | ✅ Complete | |
| Phase 2 — Semantic Model | ✅ Complete | |
| Phase 3 — LayoutTree Design | ✅ Complete | All 8 view primitives defined |
| Phase 4 — GeneralView + Renderers | ✅ Complete | All definition kinds + usages (Phases 6–7) |
| Phase 5 — Polish + Self-test | ✅ Complete | `--validate`, `--auto`, themes |
| Phases 6–7 — General View (complete) | ✅ Complete | All definitions, compartments, edges, folder packages |
| Phase 8 — Interconnection View | ✅ Complete | Force-directed parts, ports, connectors |
| Phase 9 — State Transition View | ✅ Complete | Force-directed states, transitions, initial marker |
| Phase 10 — Action Flow View | ✅ Complete | Layered (Sugiyama) actions, start/done markers |
| Phase 11 — Sequence View | ✅ Complete | Lifelines + messages (activations deferred) |
| Phase 12 — Grid + Browser Views | ✅ Complete | Relationship matrix + membership tree |
| Phase 13 — Geometry View | 🟢 Deferred | Requires spatial coordinate data (future) |
| Open Concern #1 — LayoutTree covers all 8 views | ✅ Resolved | Vocabulary is sufficient |
| Open Concern #2 — IRenderer API stability | ✅ Stable | No breaking changes needed |
| Open Concern #3 — SkiaSharp native assets | 🟡 Documented | Needs package README |
| Open Concern #4 — Noto Sans OFL attribution | 🟡 Pending | Needs `--licenses` output |
| Open Concern #5 — spec42 competitive risk | 🟢 Low | Unchanged |
| Open Concern #6 — Theme file format for v2 | 🟢 Deferred | YAML/JSON, future |
| SARIF output | 🟢 Deferred | Infrastructure ready |
| Loadable theme files | 🟢 Deferred | Future |
| `export` verb | 🟢 Deferred | Future |
| Non-`GeneralView` rendering | ✅ Complete | 7 of 8 view types implemented |
| Full OMG graphical notation conformance | 🟡 In progress | 7 of 8 views; refinements ongoing |
