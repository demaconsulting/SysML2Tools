#### ActionFlowViewLayoutStrategy

##### Purpose

`ActionFlowViewLayoutStrategy` implements `ILayoutStrategy` to produce an Action Flow View
diagram. It renders action usages as rounded boxes placed top-to-bottom through `LayeredPlacement`,
with a start marker entering the actions that have no predecessor, a done marker leaving the actions
that have no successor, and successions drawn as dashed downward flow arrows.

##### Data Model

`ActionFlowViewLayoutStrategy` has no instance state; all input arrives through the `BuildLayout`
parameters. Layout constants (`MinActionWidth`, `CharWidthFactor`, `MarkerSize`, `MarkerBand`) are
declared as `private const double` fields. A private `ActionItem` record carries each action with
its computed box size; successions are carried as `(int From, int To)` index pairs.

##### Key Methods

###### `BuildLayout(ViewContext context, RenderOptions options)`

Entry point. Selects the root definition via `FindRoot`, collects its actions, resolves its
successions, lays the actions out in layers, adds the succession edges and the start/done markers,
and assembles the tree. Returns a minimal 200×100 empty `LayoutTree` when no root or no actions
are found.

###### `FindRoot(workspace)` and `CollectActions(root, theme)`

`FindRoot` chooses the non-standard-library definition that scores highest on successions (then
actions). `CollectActions` gathers the declared `action` usages and any action named only by a
succession endpoint, building a name → index lookup.

###### `ResolveSuccessions(root, index)`

Maps each succession's source and target — by their last `::`-separated name segment — to action
indices, keeping only distinct, resolvable pairs.

###### Placement and routing

Action boxes are positioned by calling `LayeredPlacement.Place` with a top-to-bottom flow
direction: each action becomes a sized node and each succession a directed edge, so the flow reads
top-to-bottom. `LayeredPlacement` delegates to the off-the-shelf `DemaConsulting.Rendering.Layout`
layered algorithm, which returns placed rectangles for the actions and routed orthogonal polylines
for the successions. The strategy uses the open-chevron along-line length from `NotationMetrics` in
`DemaConsulting.Rendering.Abstractions`, plus `Theme.LineCornerRadius` and `Theme.CleanLegMargin`, so
any routed back edge has enough final
straight approach that the renderer's rounded corner never intrudes into the end marker. The placed
coordinates are normalized so the content starts at a margin offset, reserving a `MarkerBand` of
empty space at the top (for the start marker) and at the bottom (for the done marker). The canvas is
sized to the full content extent, including routed succession polylines that can bulge beyond the box
columns, via `ContentExtent`.

`AddSuccessionEdges` maps each succession to the corresponding orthogonal polyline returned by
`LayeredPlacement`, preserving input-edge order and source-to-target orientation.
Each succession is emitted as a dashed `LayoutLine` with an open chevron end marker
(`EndMarkerStyle.OpenChevron`) at the target, matching SysML v2 succession notation. The method counts
and returns the number of successions whose polyline crosses a non-endpoint action box (`CrossesNonEndpointBox`
/ `SegmentIntersectsRect`). `AddStartAndDone` places a filled-circle start marker
centred over the actions with no incoming edge and a bullseye done marker centred under the actions
with no outgoing edge, joining each with a solid filled-arrow flow line.

##### Error Handling

Null `context` or `options` arguments throw `ArgumentNullException`. The absence of an eligible
action definition or of actions is not an error: the method returns the minimal empty canvas.
Successions that cannot be routed cleanly are still drawn and counted as crossings, which are
surfaced through `LayoutWarnings`.

##### Dependencies

- `ILayoutStrategy` and `ViewContext` (Rendering subsystem) — the strategy contract and view input.
- `RenderOptions`, `Theme`, `BoxMetrics`, and `NotationMetrics`
  (`DemaConsulting.Rendering.Abstractions`) — render options, sizing, and decoration metrics.
- `LayeredPlacement` (Layout Internal subsystem) — top-to-bottom placement and orthogonal routing
  through `DemaConsulting.Rendering.Layout`.
- `StdlibFilter` (Rendering Internal subsystem) — standard-library exclusion.
- `SysmlWorkspace`, `SysmlDefinitionNode`, `SysmlFeatureNode`, `SysmlTransitionNode` (Semantic subsystem) — model input.
- `LayoutWarnings` (Layout Internal subsystem) — crossing-warning construction.
- The `LayoutTree`, `LayoutBox`, `LayoutBadge`, and `LayoutLine` data types
  (`DemaConsulting.Rendering`).

##### Callers

The Rendering subsystem selects `ActionFlowViewLayoutStrategy` when rendering an Action Flow View.
No other unit calls it directly.
