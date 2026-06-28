#### ForceDirectedEngine

##### Purpose

`ForceDirectedEngine` arranges a set of nodes connected by undirected edges into a spread-out,
non-overlapping placement. It produces the organic, balanced arrangement used for general block
and interconnection views where there is no inherent top-to-bottom flow. The result is a region
size and one rectangle per input node, translated so the region origin is `(0, 0)` plus a uniform
padding margin.

##### Data Model

`ForceDirectedEngine` is a static class with no instance state. Inputs are a list of `ForceNode`
records (each carrying a `Width` and `Height`), a list of `ForceEdge` records (each an undirected
pair of node indices), a `spacing` distance, and a `padding` margin. The result is a `ForceResult`
record carrying the region `Width`, `Height`, and the ordered list of placed `PackedRect`
rectangles, one per input node in input order.

##### Key Methods

###### `Place(nodes, edges, spacing, padding)`

Computes the placement. The algorithm is:

1. **Degenerate cases.** An empty node list returns a region of `2 * padding` on each axis with no
   rectangles. A single node is placed at `(padding, padding)` and the region is sized to that node
   plus padding on each side.
2. **Deterministic seed.** Initial node centres are laid out on a golden-angle spiral (radius
   growing with the square root of the index) centred on the origin. Seeding from a fixed spiral
   rather than a random number generator is what makes the result reproducible across runs and
   platforms.
3. **Force simulation.** A Fruchterman-Reingold simulation runs for a fixed number of cooling
   iterations. Every pair of nodes contributes a repulsive force inversely proportional to the
   distance between them, and every edge contributes an attractive spring force proportional to the
   square of the distance, both scaled by an optimal-distance constant `k` derived from `spacing`.
   Each iteration displaces nodes by the summed force, capped by a temperature that cools linearly
   to zero, so early iterations make large moves and later iterations fine-tune.
4. **Overlap removal.** A final pass repeatedly separates any two node bounding boxes that still
   overlap (with a small margin derived from `spacing`), pushing each pair apart along its axis of
   least penetration until no overlaps remain or an iteration cap is reached. This pass is what
   guarantees the non-overlap postcondition regardless of how the force simulation settled.
5. **Finalize.** Node centres are converted to top-left rectangles, the whole arrangement is
   translated so its minimum corner sits at the padding offset, and the region width and height are
   computed from the arrangement extent plus padding on each side.

##### Error Handling

Null `nodes` or `edges` arguments throw `ArgumentNullException`. All other inputs are handled
without throwing: empty and single-node inputs return well-formed degenerate results, and the
overlap-removal pass is bounded by an iteration cap so it always terminates.

##### Dependencies

- `PackedRect` (Layout subsystem) — the placed-rectangle value type returned in the result.

##### Callers

View layout strategies that arrange loosely structured graphs without an inherent directional
flow, where an organic balanced placement reads better than a layered or packed one.
