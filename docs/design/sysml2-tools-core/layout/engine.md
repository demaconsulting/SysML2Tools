### Layout Engine Subsystem

#### Overview

The Engine subsystem provides the reusable geometric layout engines used by the per-view
layout strategies. Each engine solves one well-defined placement or routing problem from
plain geometric input — box sizes, connection pairs, anchor points, and obstacle
rectangles — and returns computed geometry. No engine references the SysML semantic model,
so every engine is reusable across view strategies and testable in isolation.

The subsystem contains the following units:

| Unit | Responsibility |
| --- | --- |
| `ChannelRouter` | Routes an orthogonal connector between two anchors, avoiding obstacles |
| `ForceDirectedEngine` | Places connected nodes using attraction/repulsion relaxation |
| `LayeredLayoutEngine` | Places a directed graph in top-to-bottom layers |
| `PortAssigner` | Assigns ports to box sides and distributes them along each edge |
| `ContainmentPacker` | Packs sized boxes within a bounded container region |
| `ConnectivityAnalyzer` | Computes sparse adjacency, layer hints, and community assignments |
| `GravityCompressor` | Separates overlapping boxes to a minimum clearance |
| `GridQuantizer` | Snaps boxes to a grid and unifies aligned column widths/row heights |

#### Interfaces

Each engine exposes a single static entry point that accepts plain geometric records
(sizes, edges, anchors, obstacle rectangles) and returns computed geometry (placed
rectangles, ordered waypoints, or port placements). The engines consume and produce the
geometric value types declared in the Layout subsystem (for example `Point2D` and the
internal `Rect`); they do not consume `SysmlNode` or any semantic type.

#### Design

The view strategies (see *Layout Internal Subsystem*) own the mapping from the semantic
model to geometric input and back. They call the engines to obtain geometry and then build
the `LayoutTree`. This separation keeps the engines small, single-purpose, and independently
verifiable, and lets a strategy combine several engines (for example, force-directed
placement followed by orthogonal routing). The detailed algorithm of each engine is
described in its own unit chapter.
