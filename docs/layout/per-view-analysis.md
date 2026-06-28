# Per-View Analysis

## General View

**What this view shows**: all user-defined SysML definitions (part def, port def,
attribute def, action def, interface def, etc.) as labelled boxes with stereotype keyword;
specialization edges as hollow triangles; membership edges as filled/hollow diamonds;
optional packaging in folder nodes.

**Mode**: Free 2D.

**How the algorithm applies**:
Connectivity analysis builds affinity from specialization and membership edges, and uses
**community detection** so a specialization fan (a hub-and-spoke shape) is recognised as
one cluster — note that the older "all pairs above threshold" clique rule could not do
this, because subtype-to-subtype affinity is zero. Free 2D force-directed produces
clusters matching the model's package structure. Highway assignment bundles specialization
fans — e.g. 6 subtypes converging on one supertype share a corridor. Because all six
arrowheads coexist at the supertype face, `peak_lanes = 6` there and compression sizes that
corridor to `max(min_gap, 6·G + 2·wire_margin)`. `PortAssigner` collapses the six incoming
wires (same face, same directionality, same highway) into one merged trunk: a single shared
arrowhead contacts the supertype's target face (the SysML shared-target "tree" notation) and
a comb fans that trunk, without crossings, into six corridor lanes running on to the six
subtypes. If the face is shorter than `6·G` (`face_capacity < 6`) the bundle follows the
capacity-threshold tiers — splitting across the adjacent face, or stacking with a
`LayoutWarning` — so the fan never overflows a single face. Grid quantisation aligns all
blocks to an implicit shared grid via constraint-graph compaction.

**Common issues in prior implementation**:

| Issue | Root Cause |
|---|---|
| Massive inter-row whitespace | Broken compounding heat loop (applied to already-shifted positions) |
| Horizontal crowding ignored | Only vertical gaps were expanded; wrong axis |
| No crossing minimisation | Blocks ordered by discovery, not connectivity |
| Rows forced regardless of structure | Shelf packing cannot produce 2D clusters |
| Independent fan-out edges | No shared trunk / highway concept |
| Magic constants | `HeatThreshold` / `PerEdgeExpansion` had no principled derivation |

**How the proposal mitigates**:
Compression starts oversized and shrinks to minimum — over-expansion is impossible by
construction. Both axes compressed simultaneously. Monte Carlo + barycenter minimises
crossings. Free 2D placement allows genuine 2D clustering. Highway assignment naturally
produces shared trunks. All bounds are derived from measured wire clearances; no magic
constants.

---

## Interconnection View

**What this view shows**: `part usage` instances as boxes inside a containing part; `port`
usages on box boundaries; `connection` usages as lines between ports; optionally nested
parts.

**Mode**: Free 2D (using existing `ForceDirectedEngine` placement, supplemented by new
compression and quantisation steps).

**How the algorithm applies**:
Existing force-directed placement is retained — a port graph should not be forced into
layers — but the **shared pipeline from Step 4 onward** is adopted (highway assignment,
constraint-graph compaction, closed-form compression, post-processing) with **port-aware
anchoring**. `HighwayAssigner` identifies connector bundles (e.g. a bus between two parts)
and reserves corridor space. The highway-aware `PortAssigner` chooses each port's side to
face its corridor and orders slots to match the corridor's wire order. `GravityCompressor`
removes empty regions left by force-directed settlement. A **self/nested connection** (a
connection between two ports on the same part) carries no highway meaning; it is routed as
a small external arc and excluded from highway assignment.

**Common issues in prior implementation**:

| Issue | Root Cause |
|---|---|
| Empty regions after force-directed | No post-placement compression |
| Connector bundles not reserved | No highway concept |

**How the proposal mitigates**:
`GravityCompressor` collapses empty regions. `HighwayAssigner` reserves bundle corridors.
`GridQuantizer` aligns port slots to predictable positions.

---

## State Transition View

**What this view shows**: state boxes (with entry/do/exit compartments); directed
transition edges with guard labels; an initial pseudo-state (filled circle); optionally a
final state.

**Mode**: Directed Flow (`κ_h = 1.0`).

**How the algorithm applies**:
`LayeredLayoutEngine` assigns states to layers by longest path from the initial state.
The **initial pseudo-state is pinned to the top layer and the final state to the bottom
layer** — longest-path layering alone does not guarantee the final state is last once
back-edges are reversed. Edges spanning more than one layer receive **virtual nodes** so
ordering and routing stay clean. Monte Carlo seeds try different within-layer orderings to
minimise transition crossings. Edge types are handled distinctly:

- **Back-edge** (transition from layer L to layer L′ < L, a loop-back): routed as an arc
  around the outside of the flow column. Nested loops are treated as nested intervals — a
  back-edge at nesting depth `k` arcs at radius `R_k = EdgeClearance × (1 + k)`, so deeper
  loops arc further out and never overlap.
- **Same-layer transition** (sibling states): routed as a short horizontal connector
  within the layer, not treated as a back-edge.
- **Self-transition** (self-loop): drawn as a small one-sided arc on the state box;
  excluded from highway assignment and back-edge treatment.

Highway assignment bundles common transition targets (e.g. many states transitioning to a
shared error state).

![Back-edge arc routing: loop-back transitions arc outside the main flow column](images/back-edge-arc.svg)

**Common issues in prior implementation**:

| Issue | Root Cause |
|---|---|
| States scatter with unclear flow direction | Force-directed without directional bias |
| No layer assignment | No concept of execution order |
| Loop-back transitions cut across forward flow | No back-edge treatment |
| No crossing minimisation | Single seed only |

**How the algorithm applies**:
`LayeredLayoutEngine` assigns states in execution order. The initial state is pinned to
the top, final to the bottom. Back-edge arc routing (nesting-depth radius), same-layer and
self-loop handling, plus virtual nodes for multi-layer edges, separate loop-backs from
forward flow. Monte Carlo reduces crossings among states in the same layer.

---

## Action Flow View

**What this view shows**: action boxes in execution order; succession edges (dashed,
open-V); fork/join thick bars; decision/merge diamonds; start (filled circle) and done
markers.

**Mode**: Directed Flow (`κ_h = 1.0`). This is the strongest case for directed flow —
execution order is the primary visual message.

**How the algorithm applies**:
`LayeredLayoutEngine` assigns actions to layers by topological sort. **Virtual/dummy nodes
are inserted on every edge that spans more than one layer** — essential here because
fork/join branches of unequal length produce long edges that would otherwise cut across
intermediate layers and confuse barycenter ordering. Fork and join nodes produce
multi-output/multi-input layers; the crossing minimiser handles the fanning over the
virtual-node chain. Decision/merge nodes are placed at layer boundaries. Highway
assignment bundles parallel paths between the same fork/join pair. Back-edge arcs (with
nesting-depth radius) handle loop-back actions.

**Common issues in prior implementation**:

| Issue | Root Cause |
|---|---|
| Parallel branches between fork/join may cross | No highway bundling, no crossing minimisation |
| Fixed inter-layer gaps | Not derived from actual wire density |

**How the proposal mitigates**:
Highway assignment identifies parallel branches and reserves their combined width.
Monte Carlo minimises crossings between branches. Gravity compression sizes each
inter-layer gap to its actual wire density.

---

## Sequence View

**What this view shows**: lifelines as vertical dashed lines with labelled head boxes
arranged horizontally; messages as horizontal arrows between lifelines; optional activation
bars; combined fragments (alt/opt/loop).

**Mode**: Hard-coded. Bypasses Steps 1–10.

**How the algorithm applies**:
Lifelines are placed in declaration order at equal horizontal spacing (minimum = label
width + margin). Messages are placed at sequential vertical positions. Grid snap (Step 10)
aligns lifeline X positions and message Y coordinates to G using an **order-preserving
snap** that keeps a minimum 1-unit (`G`) separation between consecutive messages, so two
closely-spaced messages can never collapse onto the same Y or reorder. Label collision
check (Step 11) ensures message labels do not overlap adjacent lifelines.

**Common issues in prior implementation**:

| Issue | Root Cause |
|---|---|
| Fixed lifeline spacing regardless of label width | No adaptive spacing |
| Message label overlap | No collision check |

**How the proposal mitigates**:
Minimum lifeline spacing derived from label width. Label collision check added in Step 11.

---

## Grid View

**What this view shows**: a relationship matrix — rows and columns are definition names;
cells contain a marker when a relationship exists between the row and column elements.

**Mode**: arithmetic (no placement algorithm). Grid snap (Step 10) aligns column widths to
G for visual regularity.

**Common issues in prior implementation**: none significant. This view is already
principled.

---

## Browser View

**What this view shows**: a tree of model elements reflecting package/namespace membership;
indented lines with connector stubs showing parent-child relationships.

**Mode**: tree-walk arithmetic (no placement algorithm). Grid snap aligns node Y positions
to G.

**Common issues in prior implementation**: none significant. This view is already
principled.

---
