##### LayeredLayoutPipeline

###### Purpose

`LayeredLayoutPipeline` is an ordered sequence of `ILayoutStage` instances that, when run,
transforms a `LayeredGraph` from raw nodes and edges into a fully placed and routed layout. It is the
assembly point of the layered subsystem and the only type a caller needs to build and execute the
ELK-style algorithm. This chapter also documents the small supporting types co-located with the
pipeline: the stage interface, the two configuration enums, and the shared metrics.

###### Data Model

A pipeline is immutable once built: it holds its `Direction`, its `Hierarchy` mode, and the ordered
stage array. It is constructed through the nested fluent `PipelineBuilder` returned by `Builder()`.
The builder exposes `Direction(LayoutDirection)`, `Hierarchy(HierarchyHandling)`,
`AddStage(ILayoutStage)`, `AddDefaultStages()`, and `Build()`. `AddDefaultStages` appends the nine
default stages in order: `CycleBreaker`, `LayerAssigner`, `LongEdgeSplitter`, `CrossingMinimizer`,
`BrandesKopfPlacer`, `PortDistributor`, `OrthogonalRouter`, `LongEdgeJoiner`, and `AxisTransform`.

###### Key Methods

`Run(LayeredGraph graph)` iterates the stages in order and calls `Apply` on each, mutating the graph
in place. `Build()` validates the configuration and returns the immutable pipeline.

###### Supporting Types

- `ILayoutStage` — the single composable phase interface, `void Apply(LayeredGraph graph)`. Stages
  are stateless and may be shared across pipelines.
- `LayoutDirection` — enum `{ Right, Down, Left, Up }`. `Right` is the abstract identity direction;
  the other values are reserved scaffolding handled by `AxisTransform`.
- `HierarchyHandling` — enum `{ Flat, Recursive }`. Only `Flat` is implemented; `Recursive` is
  reserved for a later phase.
- `LayeredLayoutMetrics` — the shared spacing, clearance, padding, sweep-count, and tolerance
  constants, intentionally identical to the constants used by the previous monolithic engine.

###### Error Handling

`Build()` throws `NotSupportedException` when recursive hierarchy handling is requested.
`AddStage` throws `ArgumentNullException` for a null stage, and `Run` throws `ArgumentNullException`
for a null graph.

###### Dependencies

- `LayeredGraph` (Layered) — the shared state mutated by `Run`.
- The nine default stage units (Layered) — appended by `AddDefaultStages`.

###### Callers

`InterconnectionLayoutEngine.Place` builds a default pipeline (right direction, flat hierarchy) and
runs it to obtain the placed coordinates and connector waypoints.
