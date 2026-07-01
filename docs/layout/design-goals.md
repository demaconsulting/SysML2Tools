# Design Goals

The layout pipeline aims to produce diagrams with the following properties. Several of these
goals are in tension, and optimizing them jointly is NP-hard, so the pipeline approximates
them through a sequence of well-understood, single-responsibility stages rather than a single
global optimization.

- The flow direction is immediately obvious — layers read consistently in the diagram's
  primary direction (left-to-right for structure, top-to-bottom for behavior).
- Nodes are organized into discrete layers (ranks) so that every edge advances from one layer
  to the next; the depth of a node in the flow is readable at a glance.
- Connectors cross as little as possible — the within-layer ordering is chosen to reduce edge
  crossings using an established heuristic, not by discovery order.
- Connectors are orthogonal polylines with the minimum practical number of bends; a connector
  whose endpoints already align runs straight with no bends.
- Directly connected nodes tend to align across a layer boundary so their connector is short
  and straight.
- Parallel connectors sharing the channel between two layers are distributed into distinct
  routing slots so they never overlap or share a segment.
- The canvas is compact and reasonably balanced — layers are spaced only as far apart as the
  connectors between them require, and within-layer positions are compacted toward alignment.
- Disconnected parts of a diagram are laid out independently and packed together compactly
  rather than being forced to share the same layers.
- Nested structure is preserved — a container is sized to bound its children, and its interior
  is laid out with the same algorithm as the top level.
- Labels are placed clear of nodes and connectors.
- The layout is deterministic — the same model always produces the same diagram, so renders
  are reproducible and straightforward to compare.

These goals describe a **layered (Sugiyama-style) orthogonal drawing**. The remainder of this
document describes the pipeline that realizes them.

---
