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

###### Data Model

`BrandesKopfPlacer` is stateless; it holds no fields and operates on the shared `LayeredGraph`
passed to `Apply`:

- Reads: `LayeredGraph.AugNodes` (each node's `Width`, `IsDummy`, and `Layer`), `LayeredGraph.Groups`
  (the per-layer ordering produced by `CrossingMinimizer`), and `LayeredGraph.AugEdges`.
- Writes: `LayeredGraph.AugX` and `LayeredGraph.AugY` (one absolute coordinate per augmented node),
  `LayeredGraph.ColumnX` (the left edge of each layer column), and `LayeredGraph.MaxColWidth` (the
  maximum real-node width per layer column). The coordinate arrays are sized to the augmented-node
  count; the column arrays are sized to the layer count.

###### Key Methods

- `Apply(LayeredGraph graph)` — the `ILayoutStage` entry point. Rejects a null graph, computes the four
  output arrays, and stores them on the graph. Precondition: `CrossingMinimizer` has populated `Groups`.
  Postcondition: every augmented node has a finite X and Y and column left edges strictly increase in
  layer order.
- `AssignCoordinatesAug` (private) — computes each column's maximum real-node width and corridor width,
  derives the column left edges, then assigns X (real nodes left-aligned to their column, dummies
  centered) and delegates Y to the Brandes-Köpf routine.
- `BkAssignYCoordinates` (private) — runs the four balanced vertical layouts (DOWN/UP × RIGHT/LEFT),
  compacts each, and averages the two middle results per node.
- Supporting private helpers `BkPreprocess`, `BkMarkConflicts`, `BkVerticalAlignment`,
  `BkHorizontalCompaction`, `BkInsideBlockShift`, and `BkBalancedLayout` implement the standard
  Brandes-Köpf conflict-marking, alignment, compaction, and balancing phases.

###### Error Handling

A null graph throws `ArgumentNullException`.

###### Dependencies

- `LayeredGraph` (Layered) — the shared state read from and written to.
- `CrossingMinimizer` (Layered) — must run first to populate the ordered layer groups.
- `LayeredLayoutMetrics` (Layered) — supplies the spacing, clearance, and padding constants.

###### Callers

- `LayeredLayoutPipeline` (Layered) — adds `BrandesKopfPlacer` as the fifth stage, immediately after
  `CrossingMinimizer`, when it assembles its ordered stage list.
- `ComponentPacker` (Layered) — constructs the same stage sequence, including `BrandesKopfPlacer`, to
  lay out each connected component independently.
