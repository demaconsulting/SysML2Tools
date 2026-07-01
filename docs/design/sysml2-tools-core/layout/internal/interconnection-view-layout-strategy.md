#### InterconnectionViewLayoutStrategy

##### Purpose

`InterconnectionViewLayoutStrategy` implements `ILayoutStrategy` to produce an Interconnection
View diagram. It shows the internal structure of a single part definition: its nested part usages
as boxes placed by the layered engine, ports on the box boundaries for the incident connections,
and the connection usages routed as orthogonal connector lines between the ports, all enclosed by
a container box for the host definition.

##### Data Model

`InterconnectionViewLayoutStrategy` has no instance state; all input arrives through the
`BuildLayout` parameters. Layout constants (`MinPartWidth`, `CharWidthFactor`, `MinPortSlot`,
`ConnectorClearance`) are declared as `private const double` fields. Three private records carry
intermediate data: `PartItem` (a nested part usage with its computed box size, typing, and — when
the part is a container — its pre-laid-out `InnerContent`), `ConnPair` (a resolved binary
connection between two nested-part indices), and `InteriorLayout` (the full container size and
content produced by laying out one definition's interior).

##### Key Methods

###### `BuildLayout(ViewContext context, RenderOptions options)`

Entry point. Selects the root part definition via `FindRoot`, builds the container-definition
index via `BuildDefinitionIndex`, lays out the root's interior via `LayOutInterior`, and assembles
the root container box plus the interior content into the `LayoutTree`. Returns a minimal 200×100
empty `LayoutTree` when no root or no parts are found.

###### Recursive nested layout (`LayOutInterior`, `CollectParts`, `BuildDefinitionIndex`)

The strategy supports genuine two-level (and deeper) nested block diagrams using a **recursive
bottom-up** scheme equivalent to ELK's `SEPARATE_CHILDREN` hierarchy mode: inner structure is laid
out first with the flat engine, and each container is then treated as an atomic fixed-size node by
its parent, which is laid out with the **same** flat engine.

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
  laid-out children plus its title and insets, and the parent treats that size as atomic. Because a
  node is only ever grown via `Math.Max(height, degreeMinHeight)`, the reserved interior never
  shrinks below what the children need; any extra port-slot height is empty space at the bottom of
  the container.
- **Positioning.** `MakePartBox` builds a leaf box with empty `Children` (unchanged), and a
  container box whose `Children` are its `InnerContent` translated from the child's local origin
  `(0, 0)` to the box's absolute top-left by `TranslateNodes`, which recursively shifts box
  positions (and their nested children), port centres, and connector waypoints. The interior was
  laid out reserving its own title area, so the inner part boxes land below the container's
  "name : Type" title, inside its border. Box `Depth` increases by one per level (the renderer
  indexes `DepthFillColors` by modulo, so any depth is safe).
- **No-op invariant.** When no part is a container, every `PartItem.InnerContent` is `null`,
  `MakePartBox` emits exactly the non-recursive leaf box with empty `Children`, and the engine
  call, offsets, ports, and lines are identical to the single-level layout — single-level output is
  byte-identical.
- **Reserved pipeline mode.** The `HierarchyHandling.Recursive` pipeline mode remains reserved and
  intentionally **not wired**: recursion is driven here, at the strategy level, because container
  detection is a semantic-model concern the model-independent layered engine deliberately cannot
  see.
- **Cross-boundary limitation.** Connection endpoints resolve to **parent-level** part indices via
  the head segment of the dotted reference, so a reference such as `connect psu to board.cpu`
  terminates on the `board` container boundary rather than routing to the inner `cpu` box. This is
  the known `SEPARATE_CHILDREN` limitation (no true cross-boundary routing); gallery models avoid
  relying on cross-boundary endpoints.

###### `FindRoot(workspace)`

Chooses the non-standard-library `part def` with the most connection usages (breaking ties by the
most part usages) as the definition whose interior to render.

###### `CollectParts(root, theme)` and `ResolveConnections(root, partIndex)`

`CollectParts` gathers the root's nested `part` usages, sizing each box from its `name : Type`
label. `ResolveConnections` maps each binary connection's dotted endpoint references to nested-part
indices by matching the first segment against the part names, keeping only distinct, resolvable
pairs.

###### Placement and routing

The part boxes are positioned by `LayeredPlacer.Place`, which assigns the highest-degree node to
layer 0 and BFS-assigns each neighbour to `parent_layer + 1`. Inter-layer corridor widths scale
with the number of edges crossing each gap. The `NodeLayers` output classifies each pair as
cross-layer or same-layer.

`AddPortsAndConnectors` dispatches to two routing paths:

- **Cross-layer pairs** (the common case): the lower-layer node's right face and the higher-layer
  node's left face each receive ports distributed evenly along the face. A vertical slot in the
  inter-layer corridor is assigned, spaced `EdgeSpacing` apart from other slots in the same
  corridor, so connectors never share a vertical segment. The route is a Z-path:
  `[sourcePort, (slotX, srcY), (slotX, tgtY), targetPort]`.

- **Same-layer pairs** (rare, when two connected nodes land in the same BFS layer): ports are
  assigned with `PortAssigner` and routes are computed with `ChannelRouter.RouteWithStatus`.
  An alignment pass snaps the two ports of a connection to a shared axis coordinate — but only
  when each port is the sole occupant of its (facing) edge and the boxes overlap along the
  connector axis — so a straight connector is drawn without ever moving a box.

The method returns the number of connectors that had to cross a box (same-layer path only; the
slot-based path is obstacle-free by construction).

##### Error Handling

Null `context` or `options` arguments throw `ArgumentNullException`. The absence of an eligible
part definition or of nested parts is not an error: the method returns the minimal empty canvas.
Connectors that cannot be routed cleanly are still drawn and counted as crossings, which are
surfaced through `LayoutWarnings`.

##### Dependencies

- `ILayoutStrategy`, `ViewContext`, `RenderOptions`, `Theme` (Rendering subsystem) — the strategy contract and inputs.
- `LayeredPlacer`, `PortAssigner`, `ChannelRouter`, `BoxMetrics`
  (Layout Engine subsystem) — placement, ports, and routing.
- `StdlibFilter` (Rendering Internal subsystem) — standard-library exclusion.
- `SysmlWorkspace`, `SysmlDefinitionNode`, `SysmlFeatureNode`, `SysmlConnectionNode` (Semantic subsystem) — model input.
- `LayoutWarnings` (Layout Internal subsystem) — crossing-warning construction.
- The `LayoutTree`, `LayoutBox`, `LayoutPort`, and `LayoutLine` data types (Layout subsystem).

##### Callers

The Rendering subsystem selects `InterconnectionViewLayoutStrategy` when rendering an
Interconnection View. No other unit calls it directly.
