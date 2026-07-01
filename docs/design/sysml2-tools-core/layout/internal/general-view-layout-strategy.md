#### GeneralViewLayoutStrategy

##### Purpose

`GeneralViewLayoutStrategy` implements `ILayoutStrategy` to produce a General View diagram. It
renders every user-defined definition (part, port, interface, requirement, action, and so on) as
a keyword-labelled box, groups the boxes that belong to a package inside a folder-shaped
container, lists each definition's owned usages in compartments, and draws specialization,
membership, and attribute-typing edges orthogonally between the boxes. Box placement and
intra-package edge routing are
delegated to the reusable layered pipeline (`LayeredLayoutPipeline` with a `ComponentPacker` stage),
so definitions and their relationships are arranged the same way as ELK's layered algorithm.

##### Data Model

`GeneralViewLayoutStrategy` has no instance state; all input arrives through the `BuildLayout`
parameters. Layout constants (`MinBoxWidth`, `CharWidthFactor`, `EdgeClearance`) are declared as
`private const` fields. Private records carry intermediate data: `DefBox` (a user definition with its
computed size, keyword, supertype names, memberships, and compartments), `PlacedBox` (a definition
with absolute coordinates, used as a cross-package edge anchor), `IntraEdge` (a package-local edge in
local node indices plus its target end marker and edge kind), `CrossEdge` (an edge between definitions
in different packages, with its target end marker and edge kind), `GroupLayout` (the package-local
placement of one group's definitions and routed edges), and `BlockPlan` (the plan for one top-level
block — a package folder or the frameless top-level block). The private `EdgeKind` enumeration
classifies each edge as `Specialization`, `Membership`, or `Typing`; the `LineStyleForKind` helper
maps this kind to a rendered line style (dashed for `Typing`, solid for the others), so an
attribute-typing dependency is visually distinct from the structural relationships.

##### Key Methods

###### `BuildLayout(ViewContext context, RenderOptions options)`

Entry point. Calls `CollectDefinitions` to gather user definitions; returns a minimal 200×100 empty
`LayoutTree` when none are found. Otherwise groups the definitions by package with `GroupByPackage`,
resolves the edge set into intra-package and cross-package edges with `BuildEdges`, places the
package folders (and the frameless top-level block) across the canvas with `PlaceGroups`, then
appends the pipeline-routed intra-package edge lines and the `ChannelRouter`-routed cross-package
edge lines. The assembled `LayoutTree` carries no warnings; the layered pipeline always produces a
valid placement.

###### `CollectDefinitions(workspace, theme)`

Iterates `workspace.Declarations`, keeping each `SysmlDefinitionNode` that is not a
standard-library element (per `StdlibFilter.IsStdlibElement`). For each kept definition it builds
the compartments from the owned usage features (grouped by keyword, each formatted as a
`name : Type [n]` row), collects the typed memberships, and computes the box size from the title and
the longest compartment row.

###### `GroupByPackage(defs)`

Groups definitions by the qualified-name prefix before the last `::`, preserving first-seen order.
Top-level definitions (no package prefix) form a single frameless block laid out together.

###### `BuildEdges(groups)`

Resolves every specialization (subtype → supertype), structural membership (member-type → owner), and
attribute-typing (owner → attribute-type) relationship into either an `IntraEdge` (both endpoints in
the same package group, laid out together by the layered pipeline) or a `CrossEdge` (endpoints in
different groups, routed around the folders).
Specialization edges carry an open triangular end marker at the supertype; `part`/`port` memberships
carry a filled diamond and `ref` memberships a hollow diamond at the owner; other memberships are not
drawn. In addition, each `attribute` (or `enum`-typed attribute) feature whose type resolves to a
definition in the view contributes a **typing** edge from the owner to the attribute-type definition,
carrying an open chevron at the type end and rendered as a dashed line. Attribute typing is a
usage-type dependency, not composition, so it uses the OMG dependency notation (dashed line with an
open arrowhead) rather than a membership diamond, and it connects otherwise-disconnected attribute and
enumeration definitions into the cluster near the definitions that reference them. Unresolved types and
self-references are skipped, exactly as for specialization and membership edges. Feature-less
definitions (such as standalone interface, requirement, or unreferenced attribute defs) legitimately
contribute no edges.

###### `PlaceGroups(groups, intraByGroup, theme, depthLimit, hGap, vGap)`

For each package group, calls `LayoutGroup` to lay out the group's definitions and intra-group edges,
sizes the folder to the content bounding box plus its title area, and records a `BlockPlan`. When the
depth limit forbids the nested level, a folder's contents are replaced with a single ellipsis
indicator. The blocks are then packed across the canvas with `ContainmentPacker` so that folders
never overlap, and `PlaceBlock` emits each block's nodes and edge lines at the packed offset.

###### `LayoutGroup(items, intraEdges)`

Builds a `LayeredGraph` from the group's definitions (as `LayerNode`s) and intra-group edges (as
`LayerEdge`s), runs `LayeredLayoutPipeline` left-to-right (`Direction(Right)`, `Hierarchy(Flat)`) with
a `ComponentPacker` stage so disconnected definitions are packed beside the connected core, then reads
back each definition's top-left from `graph.AugX`/`AugY` and each edge polyline from `graph.Waypoints`,
normalized against the group's content bounding box.

###### `PlaceBlock(block, rect, …)`

Emits the layout nodes for one placed block: a package folder with its child definition boxes, the
frameless top-level definitions, or a truncated folder with an ellipsis indicator. Translates the
pipeline-routed intra-group edge polylines into absolute canvas coordinates — preserving each edge's
recorded end marker and line style (dashed for typing dependencies, solid otherwise) — and records
each rendered definition's absolute placement for cross-package routing.

###### `RouteCrossEdges(crossEdges, placed)`

Routes the rare cross-package edges around the placed folders with `ChannelRouter`, cost-neutrally,
placing the recorded end marker at the target end with the recorded line style (dashed for typing
dependencies, solid otherwise). Edges touching a truncated (unrendered) definition are skipped.

##### Error Handling

Null `context` or `options` arguments throw `ArgumentNullException`. A workspace with no user
definitions is not an error: the method returns the minimal empty canvas. The layered pipeline always
produces a valid placement, so no crossing warnings are emitted.

##### Dependencies

- `ILayoutStrategy`, `ViewContext`, `RenderOptions`, `Theme` (Rendering subsystem) — the strategy contract and inputs.
- `LayeredLayoutPipeline`, `ComponentPacker`, `LayerNode`, `LayerEdge`, `LayeredGraph` (Layout Engine
  Layered subsystem) — box placement and intra-package edge routing.
- `ContainmentPacker` and `ChannelRouter` (Layout Engine subsystem) — folder packing and cross-package edge routing.
- `StdlibFilter` (Rendering Internal subsystem) — standard-library exclusion.
- `SysmlWorkspace`, `SysmlDefinitionNode`, `SysmlFeatureNode` (Semantic subsystem) — model input.
- The `LayoutTree`, `LayoutBox`, `LayoutCompartment`, `LayoutLine`, and `Point2D` data types (Layout subsystem).
- `FeatureMembership` (private record) — carries the keyword and type reference of one owned feature.

##### Callers

The Rendering subsystem selects `GeneralViewLayoutStrategy` when rendering a General View. No
other unit calls it directly.
