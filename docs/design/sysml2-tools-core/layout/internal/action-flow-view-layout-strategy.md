#### ActionFlowViewLayoutStrategy

##### Purpose

`ActionFlowViewLayoutStrategy` implements `ILayoutStrategy` to produce an Action Flow View
diagram. It renders action usages as rounded boxes arranged top-to-bottom in layers, with a start
marker entering the actions that have no predecessor, a done marker leaving the actions that have
no successor, and successions drawn as downward flow arrows.

##### Data Model

`ActionFlowViewLayoutStrategy` has no instance state; all input arrives through the `BuildLayout`
parameters. Layout constants (`MinActionWidth`, `CharWidthFactor`, `MarkerSize`, `MarkerBand`,
`FlowClearance`) are declared as `private const double` fields. A private `ActionItem` record
carries each action with its computed box size; successions are carried as `(int From, int To)`
index pairs.

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

Action boxes are positioned by `LayeredLayoutEngine.Place`, which assigns top-to-bottom layers so
a target sits below its source, then shifted down to leave a marker band above the first layer.
`AddSuccessionEdges` routes each succession with `ChannelRouter.RouteWithStatus` from the bottom
of the source to the top of the target, keeping `FlowClearance` from unrelated boxes. Each
succession is emitted as a dashed `LayoutLine` with an open end marker at the target, matching
SysML v2 succession notation. The method returns the number of edges that had to cross a box.
`AddStartAndDone` places a filled-circle start marker
centred over the actions with no incoming edge and a bullseye done marker centred under the actions
with no outgoing edge, joining each with a straight flow line.

##### Error Handling

Null `context` or `options` arguments throw `ArgumentNullException`. The absence of an eligible
action definition or of actions is not an error: the method returns the minimal empty canvas.
Successions that cannot be routed cleanly are still drawn and counted as crossings, which are
surfaced through `LayoutWarnings`.

##### Dependencies

- `ILayoutStrategy`, `ViewContext`, `RenderOptions`, `Theme` (Rendering subsystem) — the strategy contract and inputs.
- `LayeredLayoutEngine`, `ChannelRouter`, `BoxMetrics` (Layout Engine subsystem) — layered placement and routing.
- `StdlibFilter` (Rendering Internal subsystem) — standard-library exclusion.
- `SysmlWorkspace`, `SysmlDefinitionNode`, `SysmlFeatureNode`, `SysmlTransitionNode` (Semantic subsystem) — model input.
- `LayoutWarnings` (Layout Internal subsystem) — crossing-warning construction.
- The `LayoutTree`, `LayoutBox`, `LayoutBadge`, and `LayoutLine` data types (Layout subsystem).

##### Callers

The Rendering subsystem selects `ActionFlowViewLayoutStrategy` when rendering an Action Flow View.
No other unit calls it directly.
