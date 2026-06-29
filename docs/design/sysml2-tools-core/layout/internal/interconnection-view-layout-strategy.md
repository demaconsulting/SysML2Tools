#### InterconnectionViewLayoutStrategy

##### Purpose

`InterconnectionViewLayoutStrategy` implements `ILayoutStrategy` to produce an Interconnection
View diagram. It shows the internal structure of a single part definition: its nested part usages
as boxes placed by the layered engine, ports on the box boundaries for the incident connections,
and the connection usages routed as orthogonal connector lines between the ports, all enclosed by
a container box for the host definition.

##### Data Model

`InterconnectionViewLayoutStrategy` has no instance state; all input arrives through the
`BuildLayout` parameters. Layout constants (`MinPartWidth`, `CharWidthFactor`, `NodeSpacing`,
`MinCorridorWidth`, `EdgeSpacing`, `ConnectorClearance`) are declared as `private const double`
fields. Two private records carry intermediate data: `PartItem` (a nested part usage with its
computed box size and typing) and `ConnPair` (a resolved binary connection between two nested-part
indices).

##### Key Methods

###### `BuildLayout(ViewContext context, RenderOptions options)`

Entry point. Selects the root part definition via `FindRoot`, collects its parts, resolves its
connections, places the parts, draws ports and connectors, and assembles the container box and
tree. Returns a minimal 200×100 empty `LayoutTree` when no root or no parts are found.

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
