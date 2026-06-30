##### BrandesKopfPlacer

###### Purpose

`BrandesKopfPlacer` is the fifth pipeline stage. It assigns absolute X and Y coordinates to every
augmented node, using ELK's horizontal placement for X and the Brandes-Köpf balanced four-layout
algorithm for Y.

###### Responsibilities

For X, the stage computes the maximum real-node width per layer and derives each column's left edge
from the previous column's left edge plus that column's width plus a corridor whose width scales with
the number of sub-edges crossing the gap. Real nodes are left-aligned to their column; dummy nodes
are centered in their column. For Y, it runs four independent vertical alignments (the two scan
directions crossed with the two horizontal directions), compacts each, and averages the two middle
results per node to produce port-aligned, crossing-minimized vertical positions, adding the uniform
padding once at the end. Columns are placed left to right in layer order, so each column's left edge
is strictly greater than the previous one's.

###### Inputs and Outputs

- Reads: `LayeredGraph.AugNodes`, `LayeredGraph.Groups`, `LayeredGraph.AugEdges`.
- Writes: `LayeredGraph.AugX`, `LayeredGraph.AugY`, `LayeredGraph.ColumnX`, `LayeredGraph.MaxColWidth`.

###### Error Handling

A null graph throws `ArgumentNullException`.

###### Dependencies

- `LayeredGraph` (Layered) — the shared state read from and written to.
- `CrossingMinimizer` (Layered) — must run first to populate the ordered layer groups.
- `LayeredLayoutMetrics` (Layered) — supplies the spacing, clearance, and padding constants.
