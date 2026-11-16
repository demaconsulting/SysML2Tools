#### ActionFlowViewLayoutStrategy

##### Purpose

`ActionFlowViewLayoutStrategy` implements `ILayoutStrategy` to produce an Action Flow View
diagram. It renders action usages as rounded boxes placed top-to-bottom through `LayeredPlacement`,
with a start marker entering the actions named by an explicit `first start then X;` (or, when none
is declared, the actions with no predecessor), a done marker reached from the actions that declare
`then done;` (or, when none is declared, the actions with no successor), and successions (including
the start and done connectors) drawn as dashed downward flow arrows with open arrowheads. Fork and
join nodes are thick bars, decisions and merges are diamonds, a `terminate` node is a crossed circle,
accept and send actions are boxes with the specification's corner icon and their payload text, and a
succession leaving a decision is labeled `[guard]` or `else`.

##### Data Model

`ActionFlowViewLayoutStrategy` has no instance state; all input arrives through the `BuildLayout`
parameters. Layout constants (`MinActionWidth`, `CharWidthFactor`, `MarkerSize`, `MinBarLength`,
`BarLengthPerConnection`, `DiamondLengthPerConnection`, `ControlNodeBadgeSize`, `MarkerLayerGap`) and
the reserved names `StartName` and `DoneName` are declared as `private const` fields. Three private
records carry intermediate data: `ActionItem` (an action with its computed size, a `Kind` —
`"action"` by default, or `"merge"`/`"decide"`/`"join"`/`"fork"`/`"accept"`/`"send"`/`"terminate"` —
its displayed `NameLine`, and its accept/send `PayloadRows`), `SuccessionItem` (a resolved succession
as `From`/`To` indices and an optional `[guard]`/`else` label), and `FlowMarkers` (the actions the
explicit start and done successions name).

##### Key Methods

###### `BuildLayout(ViewContext context, RenderOptions options)`

Entry point. Resolves the view's `expose` scope via `ExposeScopeResolver.ResolveExposedScope`,
selects the root definition via `FindRoot(workspace, scope)`, collects its actions via
`CollectActions(root, theme, scope)`, resolves its successions and explicit start/done markers,
sizes the control nodes from their fan-out (`SizeBars`), lays the actions out in layers, adds the
succession edges and the start/done markers, and assembles the tree. Returns a minimal 200×100 empty
`LayoutTree` when no root or no actions are found.

###### `FindRoot(workspace, scope)` and `CollectActions(root, theme, scope)`

`FindRoot` chooses the non-standard-library definition that scores highest on successions (then
actions), restricted — when a scope is resolved — to candidates for which
`ExposeScopeResolver.IsRootRelevantToScope` returns `true`. When multiple candidates are relevant
to a non-null scope (possible because a nested definition and its ancestor can both be relevant),
the most specific (deepest/longest qualified name) relevant candidate is preferred via
`ExposeScopeResolver.IsMoreSpecificCandidate`, with the succession/action score used only to break
ties among equally specific candidates; this ordering does not apply when `scope` is `null`. The
zero-successions-and-zero-actions exclusion guard (`successions > 0 || actions > 0`) is unaffected
by this change — it is applied regardless of specificity. `CollectActions` gathers the declared
`action` usages, excluding — when a scope is resolved — any declared action feature whose
qualified name fails `ExposeScopeResolver.IsInSubjectScope`; it then adds any additional action
named only by a succession endpoint **unconditionally** (this second pass has no independent
qualified name of its own to scope against, since it exists solely because a succession names it),
building a name → index lookup. The reserved names `start` and `done` are never added: they are
pseudo-nodes drawn as markers, not actions. Both the primary loop and `FindRoot`'s succession/action
scoring recognize an 8-way `IsActionFlowKeyword` allow-list (`"action"`, `"merge"`, `"decide"`,
`"join"`, `"fork"`, `"accept"`, `"send"`, `"terminate"`) rather than only `"action"`, so
control-node-heavy action defs are still favored as root candidates; each collected item records its
real `Kind` alongside its name.

###### Node content and size

`BuildActionItem` builds the displayed name line with `GeneralViewLayoutStrategy.FormatNameLine`
(`name : Type [multiplicity]` for a typed action) and, for an accept or send node, one payload row
holding the node's `ActionText` (`accept Signal`, `send report to carrier`). `ComputeActionSize` sizes
each kind: a decision/merge is a `ControlNodeBadgeSize` square and a terminate node a `MarkerSize`
circle (both independent of the name); a box is wide enough for its name line and payload row and
tall enough for the title area plus the payload; an accept/send box also reserves the corner icon's
width and inset on both sides of its centered name so the icon never touches the text.

`SizeBars` then sizes the control nodes from their fan-out, because the layout engine spreads the
connections on a face at least 16 px apart and widens a node that is too narrow for them. A
fork/join becomes a bar `max(MinBarLength, (fan + 1) × BarLengthPerConnection)` long and
`NotationMetrics.ThickBarThickness` thick, so its connections stay on its long face; a decision/merge
becomes a square diamond `max(ControlNodeBadgeSize, fan × DiamondLengthPerConnection)` on a side, so
the drawn diamond is the size of the slot the engine routes to.

###### `MakeActionNode(item, rect)`

A router. `"merge"`/`"decide"` map their placed rectangle's center to a
`LayoutBadge(CentreX, CentreY, Size, BadgeShape.Diamond, Label)`; `"fork"`/`"join"` map to a
`LayoutBadge(..., BadgeShape.ThickHorizontalBar, null)` with **no label** (the notation draws the bar
alone); `"terminate"` maps to a `BadgeShape.CrossedCircle` badge; `"accept"`/`"send"` map to a
`LayoutBox(BoxShape.RoundedRectangle, ...)` with no keyword, an untitled compartment holding the
payload row, and `new BoxAppearance(CornerIcon: BoxCornerIcon.Accept or Send)` — the corner icon
carries the node kind that the `«accept»`/`«send»` keyword line carried before; ordinary `"action"`
items map to a rounded box with the `action` keyword and the name line
(`«action» name : Type [m]`). A label derived from an AstBuilder-synthesized `$`-prefixed internal
name (anonymous nodes) is never shown.

###### `ResolveSuccessions(root, index)`

Maps each succession's source and target — by their last `::`-separated name segment — to action
indices, keeping only resolvable, non-self pairs, and labels each with `[guard]` when it has a guard
or `else` (the specification's terminal, without brackets) when `SysmlTransitionNode.IsElse` is set.
A succession from the reserved name `start` records its target as an explicit start target, and one
to `done` records its source as an explicit done source; neither becomes an edge of the layered graph.

###### Placement and routing

Action boxes are positioned by calling `LayeredPlacement.Place` with a top-to-bottom flow
direction: each action becomes a sized node and each succession a directed edge, so the flow reads
top-to-bottom. The strategy passes `EndMarkerStyle.OpenChevron` as the placement's `edgeTargetEnd`, so
the engine sizes every succession's final approach, a back edge's included, to the open-chevron
marker. `LayeredPlacement` delegates to the off-the-shelf `DemaConsulting.Rendering.Layout`
layered algorithm, which returns placed rectangles for the actions and routed orthogonal polylines
for the successions, each already oriented source-to-target because the algorithm reverses back
edges internally. The placed coordinates are normalized so the content starts at a margin offset,
reserving a marker band of empty space at the top (for the start marker) and at the bottom (for the
done marker). The canvas is sized to the full content extent, including routed succession polylines
that can bulge beyond the box columns and the labels as the renderers place them (`ContentExtent`,
`LabelRightExtent`).

The engine clamps every node to a minimum height of 20 px, so a 4 px fork/join bar is placed in a
taller slot, and it attaches connectors to the faces of the slot's bounding box, which for a diamond
lies away from the drawn outline. `AddSuccessionEdges` therefore snaps each succession's end points
onto the drawn badge (`SnapEndsToBadges`, `BadgeInset`): a vertical connector that leaves a bar's
bottom face or enters its top face is shortened or lengthened by half the slot's slack, and one that
meets a diamond is moved by its offset from the diamond's center, so connectors touch the bar or the
diamond's slanted edge rather than stopping in empty space. The engine also centers a bar's only
connection on the bar, which kinks the connector (down, across, down) when the node at the other end is a
few pixels off-center; `StraightenThroughBar` attaches such a connection where the other end lies
(when that is within the bar, less a margin) so it runs straight.

`AddSuccessionEdges` maps each succession to the corresponding orthogonal polyline returned by
`LayeredPlacement`, preserving input-edge order and source-to-target orientation.
Each succession is emitted as a dashed `LayoutLine` with an open chevron end marker
(`EndMarkerStyle.OpenChevron`) at the target, matching SysML v2 succession notation, and carries its
`[guard]`/`else` label as `MidpointLabel`. The method counts
and returns the number of successions whose polyline crosses a non-endpoint action box (`CrossesNonEndpointBox`
/ `SegmentIntersectsRect`). `AddStartAndDone` places a filled-circle start marker
centered over the explicit start targets (the actions with no incoming edge when there are none) and
a bullseye done marker centered under the explicit done sources (the actions with no outgoing edge when
there are none, never counting a terminate node, which ends its own flow; no done marker is inferred
when every flow ends in a terminate node), joining each with a `SuccessionLine` — drawn exactly like an
action-to-action succession: a straight dashed `LayoutLine` ending in an open chevron
(`EndMarkerStyle.OpenChevron`) at the target. (In SysML v2 notation the start and done markers are
joined to actions by successions, so they share the succession style.)

###### Limitations

Nested actions, action parameters, swimlanes, flows, and control structures (`if`, `while`, `for`,
`assign`) are not drawn (tracked in the roadmap). Guard text is the grammar's token concatenation
(`a and b` loses its spaces), unchanged from before.

##### Expose Scoping

Because this strategy renders exactly one selected root's actions, scoping restricts **which
root is selected** and then narrows **which of that root's actions are shown**, mirroring
`StateTransitionViewLayoutStrategy`'s approach. `FindRoot` only considers candidates
`ExposeScopeResolver.IsRootRelevantToScope` accepts, so exposing the current heuristic root
itself, an inner action of it, or a definition that itself contains the heuristic default all
correctly select a root, while exposing an unrelated definition yields no root and thus the
minimal empty canvas. When more than one candidate is relevant (a nested definition and an
ancestor definition can both be relevant to the same exposed subject),
`ExposeScopeResolver.IsMoreSpecificCandidate` prefers the most deeply nested candidate, so
exposing an inner action of a nested definition correctly selects that nested definition rather
than its ancestor, even when the ancestor has a higher succession/action score. `CollectActions`
then narrows the selected root's own **declared** action features to those within the resolved
scope; however, any declared-but-excluded action that is still referenced by an in-scope
succession is transparently re-added by the unconditional succession-endpoint pass, since that
pass has no independent qualified name to filter against — so expose-scoping only reliably drops
an action that is genuinely isolated (never referenced by any succession of the selected root).
`ResolveSuccessions`'s existing name-lookup approach naturally omits any succession whose endpoint
action was never added — no new edge-side logic was required. A view with no `expose` statement
(including the synthesized `--auto` view, whose `ViewNode` is `null`) resolves no scope, so
`FindRoot` considers every candidate and `CollectActions` keeps every action, unchanged from the
pre-scoping behavior.

##### Error Handling

Null `context` or `options` arguments throw `ArgumentNullException`. The absence of an eligible
action definition or of actions is not an error: the method returns the minimal empty canvas.
Successions that cannot be routed cleanly are still drawn and counted as crossings, which are
surfaced through `LayoutWarnings`.

##### Dependencies

- `ILayoutStrategy` and `ViewContext` (Rendering subsystem) — the strategy contract and view input.
- `RenderOptions`, `Theme`, `BoxMetrics`, `NotationMetrics`, and `ConnectorLabelPlacer`
  (`DemaConsulting.Rendering.Abstractions`) — render options, sizing, decoration metrics, and label metrics.
- `LayeredPlacement` (Layout Internal subsystem) — top-to-bottom placement and orthogonal routing
  through `DemaConsulting.Rendering.Layout`.
- `StdlibFilter` (Rendering Internal subsystem) — standard-library exclusion.
- `ExposeScopeResolver` (Layout Internal subsystem) — `ResolveExposedScope`,
  `IsRootRelevantToScope`, and `IsInSubjectScope` supply the shared `expose`-scoping used by
  `BuildLayout`, `FindRoot`, and `CollectActions`.
- `SysmlWorkspace`, `SysmlDefinitionNode`, `SysmlFeatureNode`, `SysmlTransitionNode` (Semantic subsystem) — model input.
- `LayoutWarnings` (Layout Internal subsystem) — crossing-warning construction.
- The `LayoutTree`, `LayoutBox`, `LayoutBadge`, `LayoutCompartment`, and `LayoutLine` data types
  (`DemaConsulting.Rendering`).
- `GeneralViewLayoutStrategy.FormatNameLine` (Layout Internal subsystem) — the shared name-line formatter.

##### Callers

The Rendering subsystem selects `ActionFlowViewLayoutStrategy` when rendering an Action Flow View.
No other unit calls it directly.
