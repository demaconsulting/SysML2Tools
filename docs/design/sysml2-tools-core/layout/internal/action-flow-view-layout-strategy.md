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

Entry point. Resolves the view's `expose` scope via `ExposeScopeResolver.ResolveExposedScope`,
selects the root definition via `FindRoot(workspace, scope)`, collects its actions via
`CollectActions(root, theme, scope)`, resolves its successions, lays the actions out in layers,
adds the succession edges and the start/done markers, and assembles the tree. Returns a minimal
200×100 empty `LayoutTree` when no root or no actions are found.

###### `FindRoot(workspace, scope)` and `CollectActions(root, theme, scope)`

`FindRoot` chooses the non-standard-library definition that scores highest on successions (then
actions), restricted — when a scope is resolved — to candidates for which
`ExposeScopeResolver.IsRootRelevantToScope` returns `true`. `CollectActions` gathers the declared
`action` usages, excluding — when a scope is resolved — any declared action feature whose
qualified name fails `ExposeScopeResolver.IsInSubjectScope`; it then adds any additional action
named only by a succession endpoint **unconditionally** (this second pass has no independent
qualified name of its own to scope against, since it exists solely because a succession names it),
building a name → index lookup.

###### `ResolveSuccessions(root, index)`

Maps each succession's source and target — by their last `::`-separated name segment — to action
indices, keeping only distinct, resolvable pairs.

###### Placement and routing

Action boxes are positioned by calling `LayeredPlacement.Place` with a top-to-bottom flow
direction: each action becomes a sized node and each succession a directed edge, so the flow reads
top-to-bottom. `LayeredPlacement` delegates to the off-the-shelf `DemaConsulting.Rendering.Layout`
layered algorithm, which returns placed rectangles for the actions and routed orthogonal polylines
for the successions, each already oriented source-to-target because the algorithm reverses back
edges internally. (The previous internal engine reserved a custom straight approach for the
open-chevron marker on reversed edges; that knob has no public equivalent, so a reversed
succession's final approach may differ by about a pixel — a purely cosmetic change.) The placed
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

##### Expose Scoping

Because this strategy renders exactly one selected root's actions, scoping restricts **which
root is selected** and then narrows **which of that root's actions are shown**, mirroring
`StateTransitionViewLayoutStrategy`'s approach. `FindRoot` only considers candidates
`ExposeScopeResolver.IsRootRelevantToScope` accepts, so exposing the current heuristic root
itself, an inner action of it, or a definition that itself contains the heuristic default all
correctly select a root, while exposing an unrelated definition yields no root and thus the
minimal empty canvas. `CollectActions` then narrows the selected root's own **declared** action
features to those within the resolved scope; however, any declared-but-excluded action that is
still referenced by an in-scope succession is transparently re-added by the unconditional
succession-endpoint pass, since that pass has no independent qualified name to filter against — so
expose-scoping only reliably drops an action that is genuinely isolated (never referenced by any
succession of the selected root). `ResolveSuccessions`'s existing name-lookup approach naturally
omits any succession whose endpoint action was never added — no new edge-side logic was required.
A view with no `expose` statement (including the synthesized `--auto` view, whose `ViewNode` is
`null`) resolves no scope, so `FindRoot` considers every candidate and `CollectActions` keeps
every action, unchanged from the pre-scoping behavior.

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
- `ExposeScopeResolver` (Layout Internal subsystem) — `ResolveExposedScope`,
  `IsRootRelevantToScope`, and `IsInSubjectScope` supply the shared `expose`-scoping used by
  `BuildLayout`, `FindRoot`, and `CollectActions`.
- `SysmlWorkspace`, `SysmlDefinitionNode`, `SysmlFeatureNode`, `SysmlTransitionNode` (Semantic subsystem) — model input.
- `LayoutWarnings` (Layout Internal subsystem) — crossing-warning construction.
- The `LayoutTree`, `LayoutBox`, `LayoutBadge`, and `LayoutLine` data types
  (`DemaConsulting.Rendering`).

##### Callers

The Rendering subsystem selects `ActionFlowViewLayoutStrategy` when rendering an Action Flow View.
No other unit calls it directly.
