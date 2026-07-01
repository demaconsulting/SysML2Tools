# Per-View Analysis

Each SysML view maps to the layout algorithms differently. The node-link views (General,
Interconnection, State Transition, Action Flow) use the layered pipeline with a view-appropriate
flow direction and wrappers. The remaining views (Sequence, Grid, Browser) are not node-link
graph-drawing problems and use bespoke structured layouts.

| View | Layout approach |
| --- | --- |
| General | Layered pipeline, `RIGHT`, flat + component packing |
| Interconnection | Layered pipeline, `RIGHT`, flat + recursive nesting |
| State Transition | Layered pipeline, `DOWN`, flat |
| Action Flow | Layered pipeline, `DOWN`, flat |
| Sequence | Bespoke timeline (arithmetic) |
| Grid | Bespoke relationship matrix (arithmetic) |
| Browser | Bespoke indented tree (arithmetic) |

## General View

**What this view shows**: all user-defined SysML definitions (part def, port def, attribute def,
action def, interface def, and so on) as labelled boxes with a stereotype keyword; specialization
and membership edges between them; optional grouping of definitions into package folders.

**How the algorithm applies**: definitions and their relationship edges are laid out by the layered
pipeline running left-to-right (`RIGHT`), wrapped by `ComponentPacker` so that unrelated definitions
(distinct connected components) are laid out independently and packed compactly rather than stacked
into one tall column. When definitions are grouped by package, each package group is laid out on its
own and the resulting folder boxes are arranged with `ContainmentPacker` so they never overlap. The
occasional edge whose endpoints fall in different package folders is routed around the placed folders
with `ChannelRouter`, which finds an orthogonal, obstacle-avoiding path.

## Interconnection View

**What this view shows**: the internal structure of a part definition — its nested part usages as
boxes, `port` usages on the box boundaries, and `connection` usages routed as orthogonal connectors
between the ports, all enclosed by a container box for the host definition.

**How the algorithm applies**: placement and routing are delegated to `InterconnectionLayoutEngine`,
the façade over the layered pipeline (running left-to-right and flat). Each box's height is scaled up
if needed to guarantee a minimum vertical slot per port, so connectors on a shared face stay
distinct. When a nested part is itself typed by a definition that has its own internal parts, the
strategy lays that inner structure out **recursively, bottom-up**: the inner definition is laid out
first with the same flat engine, the container part is then treated as a fixed-size atomic node by
its parent, and the inner content is nested as the container box's children. A single-level model —
one with no part typed by a definition that has internal parts — never triggers the recursion and is
laid out exactly as the non-recursive case. This recursion is driven at the strategy level because
detecting containers is a semantic-model concern the model-independent engine cannot see; the
pipeline's reserved `Recursive` hierarchy mode is not currently wired.

## State Transition View

**What this view shows**: state boxes (with entry/do/exit compartments); directed transition edges
with guard labels; an initial pseudo-state; optionally a final state.

**How the algorithm applies**: states and transitions are laid out by the layered pipeline running
top-to-bottom (`DOWN`), so execution order reads downward. Cycle breaking reverses loop-back
transitions to keep the graph acyclic, layer assignment orders states by their longest path from the
sources, crossing minimization reduces transition crossings, and orthogonal routing draws each
transition as a slotted orthogonal polyline. Because the `DOWN` direction is produced by the Axis
Transform over the same canonical layout, the state layout uses identical placement and routing logic
to the structural views.

## Action Flow View

**What this view shows**: action boxes in execution order; succession edges; fork/join bars;
decision/merge diamonds; start and done markers.

**How the algorithm applies**: actions and successions are laid out by the layered pipeline running
top-to-bottom (`DOWN`). Layer assignment places each action beyond its predecessors, so the flow
reads in execution order; long-edge splitting inserts dummy nodes on successions that span multiple
layers (common when fork/join branches have unequal length) so they route cleanly through the
intermediate layers instead of cutting across them; crossing minimization keeps parallel branches
from tangling; and orthogonal routing separates concurrent successions into distinct slots.

## Sequence View

**What this view shows**: lifelines as vertical stems with labelled head boxes arranged horizontally;
messages as horizontal arrows between lifelines, ordered top-to-bottom.

**How the algorithm applies**: this is a hard-notation layout and does not use the graph-drawing
pipeline. Layout is pure arithmetic: each lifeline's horizontal position is its column index times a
pitch (with the pitch at least as wide as the widest head label), and each message's vertical
position is its ordinal times a row pitch. The result is a regular timeline grid.

## Grid View

**What this view shows**: a relationship matrix whose rows and columns are definition names; a cell
is marked where the row definition specializes the column definition.

**How the algorithm applies**: this is an arithmetic layout with no placement algorithm. Column
widths are sized to fit the widest cell and the header row and column are styled distinctly, using
the shared grid helper. There is no node-link routing to perform.

## Browser View

**What this view shows**: the membership hierarchy of the workspace's user-defined elements as an
indented tree of rows, with connector stubs from each parent to its children.

**How the algorithm applies**: this is an arithmetic layout with no placement algorithm. The tree is
derived from the qualified-name hierarchy — element `A::B::C` is a child of `A::B` — and each row is
a small box indented by its depth. Row positions follow directly from tree order and depth.

---
