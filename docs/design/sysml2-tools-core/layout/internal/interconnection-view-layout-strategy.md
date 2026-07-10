#### InterconnectionViewLayoutStrategy

##### Purpose

`InterconnectionViewLayoutStrategy` implements `ILayoutStrategy` to produce an Interconnection
View diagram. It shows the internal structure of a single part definition: its nested part usages
as boxes placed through `LayeredPlacement`, ports on the box boundaries for the incident connections,
and the connection usages routed as orthogonal connector lines between the ports, all enclosed by a
container box for the host definition.

##### Data Model

`InterconnectionViewLayoutStrategy` has no instance state; all input arrives through the
`BuildLayout` parameters. Layout constants (`MinPartWidth`, `CharWidthFactor`) are declared as
`private const double` fields. Three private records carry intermediate data: `PartItem` (a nested
part usage with its computed box size, typing, and — when the part is a container — its
pre-laid-out `InnerContent`), `ConnPair` (a resolved binary connection between two nested-part
indices, together with the optional port-name label for each end), and `InteriorLayout` (the full
container size and content produced by laying out one definition's interior).

##### Key Methods

###### `BuildLayout(ViewContext context, RenderOptions options)`

Entry point. Resolves the view's `expose` scope via `ExposeScopeResolver.ResolveExposedScope`,
selects the root part definition via `FindRoot(workspace, scope)`, builds the container-definition
index via `BuildDefinitionIndex`, lays out the root's interior via `LayOutInterior` (threading
`scope` through every recursive call), and assembles the root container box with the interior
content nested as that box's own `Children` (mirroring the nesting `MakePartBox` already uses for a
container part, so the root box is never a bare sibling of its own content) into the `LayoutTree`.
Returns a minimal 200×100 empty `LayoutTree` when no root or no parts are found.

###### Recursive nested layout (`LayOutInterior`, `CollectParts`, `BuildDefinitionIndex`)

The strategy supports genuine two-level (and deeper) nested block diagrams using a **recursive
bottom-up** scheme equivalent to ELK's `SEPARATE_CHILDREN` hierarchy mode: inner structure is laid
out first through flat layered placement, and each container is then treated as an atomic fixed-size
node by its parent, which is laid out with the **same** flat placement.

- **Container detection.** `BuildDefinitionIndex` builds a `Dictionary<string, SysmlDefinitionNode>`
  of candidate containers — non-standard-library `part def`s that have at least one nested `part`
  usage — keyed by both qualified and simple name (qualified preferred). For each part,
  `CollectParts` resolves its `FeatureTyping` against that index by qualified-then-simple name
  (`TryResolveContainer`); a part whose type resolves to a container, and whose type is not already
  on the recursion path, is a container, and every other part is a leaf.
- **Recursion.** A container part is laid out by calling `LayOutInterior` on its type definition at
  `depth + 1`, with the type's qualified name added to a `visited` set. The returned interior size
  becomes the part's atomic box size, and the returned interior content becomes its
  `InnerContent`. A `visited` qualified-name set guards against self- or mutually-referential types
  (cycle parts are treated as leaves), guaranteeing termination.
- **Sizing.** Each level reserves the same title area and insets used by the root:
  `offsetX = LabelPadding × 2`, `offsetY = TitleAreaHeight(hasLabel, hasKeyword) + LabelPadding × 2`,
  `containerWidth = TotalWidth + offsetX × 2`, and
  `containerHeight = TotalHeight + offsetY + LabelPadding × 2`. A container box therefore bounds its
  laid-out children plus its title and insets, and the parent treats that size as atomic. Box
  height itself is no longer scaled by hand from a per-port minimum-slot heuristic: each part's
  ports are modeled as named `LayoutGraphPort`s on `LayeredPlacement.PlaceWithPorts`'s input graph,
  so the layered algorithm itself grows a box, when needed, to keep every incident connection's
  port visually distinct. Every part is also passed to `PlaceWithPorts` with `HasLabel: true,
  HasKeyword: true` (every `PartItem` always carries a non-empty name and a `"part"` keyword,
  mirroring the `hasLabel: true, hasKeyword: true` convention already used for `TitleAreaHeight`
  above), which activates the engine's automatic title-vs-side-port reservation so no port is ever
  placed across the box's own title band.
- **Positioning.** `MakePartBox` builds a leaf box with empty `Children` (unchanged), and a
  container box whose `Children` are its `InnerContent` translated from the child's local origin
  `(0, 0)` to the box's absolute top-left by `TranslateNodes`, which recursively shifts box
  positions (and their nested children), port centres, and connector waypoints. The interior was
  laid out reserving its own title area, so the inner part boxes land below the container's
  "name : Type" title, inside its border. Box `Depth` increases by one per level (the renderer
  indexes `DepthFillColors` by modulo, so any depth is safe). The root container box mirrors this
  same pattern one level up: `BuildLayout` nests the root's own interior content as the root box's
  `Children` rather than as flat top-level siblings of the root box — the root sits at the same
  origin `(0, 0)` its interior content is already positioned relative to, so no translation is
  needed there.
- **No-op invariant.** When no part is a container, every `PartItem.InnerContent` is `null`,
  `MakePartBox` emits exactly the non-recursive leaf box with empty `Children`, and the placement
  call, offsets, ports, and lines are identical to the single-level layout — single-level output is
  byte-identical.
- **Cross-boundary resolution.** `ResolveEndpoint` resolves the **full** dotted endpoint reference,
  not just its head segment: the head segment still identifies the parent-level part index, and any
  remaining dotted segment(s) (e.g. `cpu` in `board.cpu`, or `encoder` in `StepperMotorX.encoder`) is
  captured as that endpoint's port-name label and flows through `ConnPair.LabelA`/`LabelB` into the
  emitted `LayoutPort.ExternalLabel`. This means a reference into a nested part (`connect psu to
  board.cpu`) now shows the true target name (`cpu`) on the port, rather than the pre-fix behavior of
  silently discarding it. The connector itself is still **routed only to the container's own
  boundary** — it does not continue into the container's interior to physically terminate on the
  inner `cpu` box. Achieving that (a genuine boundary/delegation-port anchor shared between the
  outer connector and an inner one, via the companion library's `HierarchyHandling.Recursive`
  support) would require restructuring `LayOutInterior`'s per-level independent
  `LayeredPlacement.PlaceWithPorts` calls into one connected nested `LayoutGraph`/
  `LayoutGraphNode.Children` for the affected subtree, which is a materially larger architectural
  change than this feature makes; it remains a known, documented limitation. `ResolveEndpoint`
  captures **everything** after the first dotted segment as the label, however many levels deep
  (e.g. `board.sub.cpu` yields the label `sub.cpu`, not just `sub`); only the _routing_ — never the
  label text — stops at the container's boundary regardless of path depth.

##### Port Labeling

Each connection endpoint that resolves to a nested part is requested as a named
`LayeredPlacement.EdgePortRef` on that endpoint's `PortEdge`, carrying the real SysML port-name
segment (from `ConnPair.LabelA`/`LabelB`; see _Cross-boundary resolution_ above and
`ResolveEndpoint` below) as the port's `ExternalLabel`. `LayeredPlacement.PlaceWithPorts` creates
the corresponding `LayoutGraphPort` and returns the engine-placed `LayoutPort` with that label
already attached — the strategy only translates the returned port's `CentreX`/`CentreY` by the
container offset before adding it to the interior's content. A bare endpoint reference with no
dotted port segment (e.g. `connect psu to board`) yields a `null` label (a port is still created
and placed; only its label is absent), preserving the pre-fix rendering (no label shown) for that
connector end.

##### Parallel Connection Preservation

`LayOutInterior` calls `LayeredPlacement.PlaceWithPorts` (which unconditionally disables
parallel-edge merging — see `docs/design/sysml2-tools-core/layout/internal/layered-placement.md`),
so multiple distinct SysML connections between the same two parts (e.g. separate
`power`/`encoder`/`sensor` connections wired between the same controller and motor, as seen in a
real 3-axis-gantry wiring model) are never collapsed onto one shared routed polyline: each
connection keeps its own independently-routed connector with distinct waypoints (a separate
parallel lane), and its own pair of labeled ports.

###### `FindRoot(workspace, scope)`

Chooses the non-standard-library `part def` with the most connection usages (breaking ties by the
most part usages) as the definition whose interior to render, restricted — when a scope is
resolved — to candidates for which `ExposeScopeResolver.IsRootRelevantToScope` returns `true`;
returns `null` when no candidate is relevant to a non-null scope (an empty canvas results), and
falls back to considering every candidate when `scope` is `null`. When multiple candidates are
relevant to a non-null scope (possible because a nested definition and its ancestor can both be
relevant), the most specific (deepest/longest qualified name) relevant candidate is preferred via
`ExposeScopeResolver.IsMoreSpecificCandidate`, with the connections/parts tie-break used only to
break ties among equally specific candidates; this ordering does not apply when `scope` is `null`.

###### `CollectParts(root, theme)` and `ResolveConnections(root, partIndex)`

`CollectParts` gathers the root's nested `part` usages, sizing each box from its `name : Type`
label, additionally excluding — when a scope is resolved — any part feature whose qualified name
fails `ExposeScopeResolver.IsInSubjectScope`. `ResolveConnections` maps each binary connection's
dotted endpoint references to nested-part indices and port-name labels via `ResolveEndpoint`
(matching the first dotted segment against the (possibly narrowed) part names and capturing any
remaining segment as the port label — see _Cross-boundary resolution_ above), keeping only
distinct, resolvable pairs; a connection whose endpoint was excluded by scoping simply fails to
resolve and is dropped by this existing endpoint-lookup logic — no separate edge-side scoping is
needed.

##### Expose Scoping

Because this strategy renders exactly one selected root's interior, scoping cannot narrow a
workspace-wide collection the way `GridViewLayoutStrategy` and `BrowserViewLayoutStrategy` do;
instead it restricts **which root is selected** and then narrows **which of that root's parts are
shown**. `FindRoot` only considers candidates `ExposeScopeResolver.IsRootRelevantToScope` accepts,
so exposing the current heuristic root itself, an inner part of it, or a definition that itself
contains the heuristic default all correctly select a root, while exposing an unrelated
definition yields no root and thus the minimal empty canvas. When more than one candidate is
relevant (a nested definition and an ancestor definition can both be relevant to the same exposed
subject), `ExposeScopeResolver.IsMoreSpecificCandidate` prefers the most deeply nested candidate,
so exposing an inner part of a nested definition correctly selects that nested definition rather
than its ancestor, even when the ancestor has more connections/parts. `CollectParts` then narrows
the selected root's own part features to those within the resolved scope (via
`ExposeScopeResolver.IsInSubjectScope`), and `ResolveConnections`'s existing "skip connections
whose endpoint did not resolve" behavior transparently drops any connection touching an excluded
part — no new edge-side logic was required. A view with no `expose` statement (including the
synthesized `--auto` view, whose `ViewNode` is `null`) resolves no scope, so `FindRoot` considers
every candidate and `CollectParts` keeps every part, unchanged from the pre-scoping behavior.

###### Placement and routing

Placement and routing are delegated to `LayeredPlacement.PlaceWithPorts` with a left-to-right flow
direction; parallel-edge merging is unconditionally disabled by that method (see _Parallel
Connection Preservation_ above). Nested-container recursion is driven at the strategy level (see
_Recursive nested layout_ above); each level calls the same flat placement helper. The strategy
passes the collected part boxes and resolved connection pairs — each carrying an `EdgePortRef` for
every endpoint — as plain geometric input. `LayeredPlacement` delegates to the off-the-shelf
`DemaConsulting.Rendering.Layout` layered algorithm and returns placed rectangles, connector
waypoints, and correlated placed ports, with disconnected components packed without overlap and
port spacing/box growth resolved by the engine itself. The total canvas extent is derived from the
placed box and waypoint geometry.

The strategy then shifts the placed content to sit inside the container box and extends the
container so every connector waypoint is enclosed, without ever moving a box.

##### Error Handling

Null `context` or `options` arguments throw `ArgumentNullException`. The absence of an eligible
part definition or of nested parts is not an error: the method returns the minimal empty canvas.
Connectors that cannot be routed cleanly are still drawn; this strategy does not itself construct
`LayoutWarnings` diagnostics, so the returned `LayoutTree` carries no layout-quality warnings.

##### Dependencies

- `ILayoutStrategy` and `ViewContext` (Rendering subsystem) — the strategy contract and view input.
- `RenderOptions`, `Theme`, and `BoxMetrics` (`DemaConsulting.Rendering.Abstractions`) — render
  options and sizing metrics.
- `LayeredPlacement` (Layout Internal subsystem) — placement and routing through
  `DemaConsulting.Rendering.Layout`.
- `StdlibFilter` (Rendering Internal subsystem) — standard-library exclusion.
- `ExposeScopeResolver` (Layout Internal subsystem) — `ResolveExposedScope`,
  `IsRootRelevantToScope`, and `IsInSubjectScope` supply the shared `expose`-scoping used by
  `BuildLayout`, `FindRoot`, and `CollectParts`.
- `SysmlWorkspace`, `SysmlDefinitionNode`, `SysmlFeatureNode`, `SysmlConnectionNode` (Semantic subsystem) — model input.
- The `LayoutTree`, `LayoutBox`, `LayoutPort`, and `LayoutLine` data types
  (`DemaConsulting.Rendering`).

##### Callers

The Rendering subsystem selects `InterconnectionViewLayoutStrategy` when rendering an
Interconnection View. No other unit calls it directly.
