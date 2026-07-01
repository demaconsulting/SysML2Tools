# Layout Engines

The layout subsystem is built from small, stateless engines and pipeline stages under
`Layout/Engine/`. Each accepts plain geometric input (node sizes, directed edge pairs, obstacle
rectangles) and returns computed geometry; none references the SysML semantic model. This chapter
catalogs them by role.

## Pipeline Assembly

### LayeredLayoutPipeline

Assembles an ordered sequence of stages and runs them against a `LayeredGraph`. A fluent builder
selects the flow direction and hierarchy handling and appends the default stage sequence. The
pipeline normalizes the input node axes for the requested direction, then applies each stage in
order.

### LayeredGraph

The shared mutable state threaded through the stages. It holds the input nodes and edges, the flow
direction, and every intermediate product — the acyclic edge set, layer assignments, augmented
nodes and sub-edges (with dummies), within-layer groupings, coordinates, port positions, and the
final per-edge waypoints.

### LayeredLayoutMetrics

The single source of the fixed spacing and tolerance constants shared by every stage (node
spacing, minimum corridor width, slot spacing, connector clearance, content padding, sweep count,
and straightness tolerance). Centralizing them keeps spacing consistent across stages.

## Pipeline Stages

Each stage implements the common `ILayoutStage` contract (`Apply(LayeredGraph)`) and is stateless
and reusable across pipelines. The stages, in pipeline order:

- **CycleBreaker** — reverses cycle-causing back edges (depth-first heuristic) to make the graph
  acyclic, recording which edges were reversed.
- **LayerAssigner** — assigns each node to a layer by longest-path layering.
- **LongEdgeSplitter** — inserts a zero-size dummy node per intermediate layer so every sub-edge
  spans exactly one layer.
- **CrossingMinimizer** — reorders nodes within layers with alternating barycenter sweeps to reduce
  crossings.
- **BrandesKopfPlacer** — assigns coordinates: Brandes-Köpf balanced placement on the within-layer
  axis and corridor-width-driven positions on the layer axis.
- **PortDistributor** — distributes connector ports evenly along each node face and records per
  sub-edge attachment points.
- **OrthogonalRouter** — routes each connector as an orthogonal polyline, assigning distinct routing
  slots in each inter-layer channel.
- **LongEdgeJoiner** — concatenates each original edge's sub-edge bends into one polyline and
  discards the dummies.
- **AxisTransform** — maps the canonical left-to-right layout onto the requested flow direction.

### ComponentPacker

A composite stage that wraps the default stage sequence. It partitions a graph into connected
components (union-find over the undirected edge set), lays each component out independently with the
wrapped stages, and packs the resulting bounding boxes onto shelves biased toward a target aspect
ratio. A single-component graph is a transparent pass-through, so connected graphs are unaffected.
Component and node ordering are deterministic (by lowest original index), keeping renders
reproducible.

## Standalone Geometry Engines

### InterconnectionLayoutEngine

A thin façade over the layered pipeline. It builds a `LayeredGraph` from caller-supplied nodes and
edges, runs the default stage sequence left-to-right and flat, and adapts the output into the result
shape the Interconnection View strategy consumes. It exists to preserve a stable entry point; all
placement and routing logic lives in the stages.

### ContainmentPacker

A shelf (row) bin-packing engine. It places variable-size items left to right, wrapping to a new row
when the next item would exceed the content width, and sizes the enclosing region to fit all items
plus uniform padding. It is deterministic, preserves input order, and guarantees non-overlapping
rectangles. The General View uses it to arrange package-folder groups.

### ChannelRouter

Routes an orthogonal connector between two anchors while avoiding rectangular obstacles. It builds a
sparse Hanan-style routing grid from the source, target, and obstacle-edge coordinates (offset
outward by a clearance) and runs an A\* search with a turn penalty that prefers straight runs. The
returned path begins and ends exactly at the anchors and is strictly axis-aligned. It also accepts
optional cost bands that bias the search toward preferred stripes of the canvas, and reports when it
had to fall back to a path that crosses an obstacle so callers can raise a layout warning. The
General View uses it for the occasional connector that spans two package folders.

---
