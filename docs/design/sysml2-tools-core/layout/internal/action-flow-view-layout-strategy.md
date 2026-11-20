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
`BarLengthPerConnection`, `DiamondLengthPerConnection`, `ControlNodeBadgeSize`, `MarkerLayerGap`,
`JogClearance`) and
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
`LabelRightExtent`). A label is drawn centered on its connector's run, so a long guard on a branch
that runs left of every box can overhang the left margin; `BuildLayout` measures that
(`LabelLeftExtent`) and, when a label overhangs, rebuilds the content shifted right by the overhang
so every label stays inside the canvas.

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

*Decision branch labels.* The label placer centers a label on the longest segment of its connector.
A decision's branch leaves the diamond, runs down a corridor-width gap, across to the target's column,
and drops to the target (down, across, down); the engine puts the sideways run just above the target,
so the sideways run is the longest and a wide `[guard]` would sit on it, hiding the run and the start
of the connector at the diamond. `MoveLabeledJogsTowardsSource` moves the sideways run of the branches
that leave one source up to `JogClearance` (5 px: the 4 px corner radius of the rounded bends plus
1 px) below the connector's start, so the final vertical drop is the longest segment and carries the
label with the run and the start visible. All the down-across-down branches of one source move by the
same amount (the smallest first run less `JogClearance`), which keeps the engine's spacing between
sibling lanes, so two branches never share a sideways run. The move is made only for a source that has
a labeled branch and only when every labeled branch's final drop would then be longer than its sideways
run; when a branch's sideways run is the longer (for example two very wide target boxes) the engine's
route is kept, since moving the jog could not make the drop the longest segment.

*Known limitation: hooks at the diamond.* The engine attaches a decision's outgoing connectors to the
bounding box of its slot, not to the diamond outline, so each branch starts on a slanted lower edge
(after `SnapEndsToBadges`) and turns sideways after `JogClearance`; with the 4 px corner radius this
reads as a small hook at the start of the connector (visible at 8x zoom). Two mitigations were tried
and rejected. Leaving from the side vertices (an L route) clears the hook, but the badge's own name
label (`smokeDecision`) is drawn beside the right vertex by the renderer and the right-hand connector
then runs through it. Lengthening the first run to 12 px makes the stub visible, but on the Pipeline
model the sideways run (73.4 px) is then longer than the final drop (68 px), so a branch label falls
on the sideways run, which `MoveLabeledJogsTowardsSource` exists to prevent. The 5 px clearance is kept
until the engine attaches connectors to the badge outline (or lets a badge label be placed above or
below the shape).

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

##### Members Excluded from Diagrams

Root selection ignores a declaration flagged `IsExcludedFromDiagrams`, and every count and
enumeration of the root's children (`DiagramChildren`) skips the children flagged so: the actions and
successions of a calculation, constraint or case body, built for name resolution only, are neither
drawn nor a reason to choose their owner as the root.

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
