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
| `InterconnectionLayoutEngine` | Façade that assembles and runs the layered pipeline for the interconnection view |
| `ContainmentPacker` | Packs sized boxes within a bounded container region |

The subsystem also contains the nested **Layered** subsystem, which provides a reusable,
ELK-style layered layout pipeline composed of single-responsibility stages. The
`InterconnectionLayoutEngine` façade is a thin assembler over that pipeline. See the
*Layout Engine Layered Subsystem* chapter for its units and stage decomposition.

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
verifiable. Layered views drive their geometry through the reusable `LayeredLayoutPipeline`
and its single-responsibility stages (packing connected components via `ComponentPacker` and
routing edges via the pipeline's `OrthogonalRouter`), while a strategy may combine additional
engines as needed (for example, `ContainmentPacker` for package-folder grouping and
`ChannelRouter` for cross-package connector fallback). The detailed algorithm of each engine is
described in its own unit chapter.
