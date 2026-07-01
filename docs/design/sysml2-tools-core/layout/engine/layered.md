#### Layout Engine Layered Subsystem

##### Overview

The Layered subsystem is a reusable, composable layered-layout pipeline that reproduces ELK's
layered (Sugiyama-style) algorithm. It replaces the single monolithic placement method that the
interconnection engine previously contained with an ordered sequence of small, single-purpose
stages. Each stage reads the state produced by earlier stages and writes the state it owns, so the
pipeline can be extended, reordered, and unit-tested one stage at a time.

The subsystem was produced by a behavior-preserving extraction: for every input it produces exactly
the same rectangles, totals, layer assignments, and connector waypoints as the previous
implementation, verified byte for byte by the pipeline-equivalence tests.

The subsystem contains the following units:

| Unit | Responsibility |
| --- | --- |
| `LayeredGraph` | Mutable shared state threaded through every stage |
| `LayeredLayoutPipeline` | Runs ordered stages via a fluent builder; hosts the stage interface, enums, and metrics |
| `CycleBreaker` | Reverses cycle-causing edges to produce an acyclic edge set |
| `LayerAssigner` | Assigns each node a longest-path layer index |
| `LongEdgeSplitter` | Splits multi-layer edges with one dummy node per intermediate layer |
| `CrossingMinimizer` | Orders nodes within each layer to reduce edge crossings |
| `BrandesKopfPlacer` | Assigns absolute X and Y coordinates to every augmented node |
| `PortDistributor` | Distributes connector ports along each box face |
| `OrthogonalRouter` | Assigns routing slots and emits orthogonal bend points per corridor |
| `LongEdgeJoiner` | Concatenates sub-edge bend points into one polyline per original edge |
| `AxisTransform` | Maps abstract along/cross coordinates onto screen coordinates |
| `ComponentPacker` | Lays out each connected component independently and packs them without overlap |

##### Interfaces

Each stage implements the `ILayoutStage` interface (`void Apply(LayeredGraph graph)`) and mutates
the shared `LayeredGraph` in place. A pipeline is assembled through the fluent
`LayeredLayoutPipeline.PipelineBuilder` (`Direction`, `Hierarchy`, `AddStage`, `AddDefaultStages`,
`Build`) and executed with `Run`. The default stage sequence is, in order: `CycleBreaker`,
`LayerAssigner`, `LongEdgeSplitter`, `CrossingMinimizer`, `BrandesKopfPlacer`, `PortDistributor`,
`OrthogonalRouter`, `LongEdgeJoiner`, and `AxisTransform`. `ComponentPacker` is an optional composite
stage that wraps an inner stage sequence: it is added explicitly by callers that lay out potentially
disconnected graphs (such as the General view), where it splits the graph into connected components,
runs the inner stages on each, and packs the results without overlap. All types are `internal` and
consume only the geometric value types of the Layout subsystem (`Point2D`, `Rect`) plus the internal
`LayerNode` and `LayerEdge` records; no stage references the SysML semantic model.

##### Design

The `LayeredLayoutMetrics` constants (spacing, clearances, padding, sweep count, tolerance) are
shared by every stage and are intentionally identical to the constants embedded in the previous
monolithic engine, so the pipeline reproduces the legacy output exactly. The deleted
`ReversedEdgeApproach` magic constant is replaced by the per-graph `LayeredGraph.BackEdgeEntryApproach`
parameter, which the `OrthogonalRouter` reads to size a reversed edge's final straight approach; it
defaults to `ConnectorClearance`, so the default pipeline stays byte-identical, while a
decoration-aware caller can raise it to clear an end marker's along-line length. The `LayoutDirection`
and `HierarchyHandling` enums select flow direction and nested-node handling. The left-to-right direction
is the abstract identity (its output is byte-identical to the original engine); the down, left, and up
directions are mapped by `AxisTransform`, which also normalizes the input node axes (swapping width and
height for the down/up directions) at the start of `Run` so layer spacing is driven by the correct
extent. Flat hierarchy handling is supported; the recursive mode is reserved and fails fast with a
clear error. The detailed algorithm of each stage is described in its own
unit chapter.
