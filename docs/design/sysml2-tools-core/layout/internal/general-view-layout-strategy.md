#### GeneralViewLayoutStrategy

##### Purpose

`GeneralViewLayoutStrategy` implements `ILayoutStrategy` to produce a General View diagram. It
renders every user-defined definition (part, port, interface, requirement, action, and so on) as
a keyword-labelled box, groups the boxes that belong to a package inside a folder-shaped
container, lists each definition's owned usages in compartments, and draws specialization edges
between subtypes and their supertypes.

##### Data Model

`GeneralViewLayoutStrategy` has no instance state; all input arrives through the `BuildLayout`
parameters. Layout constants (`MinBoxWidth`, `CharWidthFactor`, `EdgeClearance`) are declared as
`private const double` fields. Two private records carry intermediate data: `DefBox` (a
user definition with its computed size, keyword, supertype names, and compartments) and
`PlacedBox` (a definition with absolute coordinates, used as an edge anchor).

##### Key Methods

###### `BuildLayout(ViewContext context, RenderOptions options)`

Entry point. Calls `CollectDefinitions` to gather user definitions; returns a minimal
200×100 empty `LayoutTree` when none are found. Otherwise groups the definitions by package,
places the groups, routes the specialization edges, and returns the assembled tree with any
crossing warnings attached.

###### `CollectDefinitions(workspace, theme)`

Iterates `workspace.Declarations`, keeping each `SysmlDefinitionNode` that is not a
standard-library element (per `StdlibFilter.IsStdlibElement`). For each kept definition it builds
the compartments from the owned usage features (grouped by keyword, each formatted as a
`name : Type [n]` row) and computes the box size from the title and the longest compartment row.

###### `GroupByPackage(defs)`

Groups definitions by the qualified-name prefix before the last `::`, preserving first-seen
order. Top-level definitions (no package prefix) form their own standalone blocks.

###### `PlaceGroups(groups, theme, depthLimit)`

Packs the definition boxes of each package inside a folder box using `ContainmentPacker`, then
packs the folder boxes and standalone boxes across the canvas with a second `ContainmentPacker`
pass. A full title area is reserved above each folder's contents so the package label never
overlaps the first child. When the depth limit forbids the nested level, a folder's contents are
replaced with a single ellipsis indicator.

###### `BuildSpecializationEdges(defs, placed)`

For each definition with a declared supertype present in the workspace, routes an orthogonal line
with `ChannelRouter` from the subtype box to the supertype box, keeping `EdgeClearance` from
unrelated boxes, and emits a `LayoutLine` with an open arrowhead at the supertype end. Returns
the routed lines and the count of edges that had to cross a box.

##### Error Handling

Null `context` or `options` arguments throw `ArgumentNullException`. A workspace with no user
definitions is not an error: the method returns the minimal empty canvas. Edges that cannot be
routed cleanly are still drawn and counted as crossings, which are surfaced through
`LayoutWarnings` rather than failing the layout.

##### Dependencies

- `ILayoutStrategy`, `ViewContext`, `RenderOptions`, `Theme` (Rendering subsystem) — the strategy contract and inputs.
- `ContainmentPacker` and `ChannelRouter` (Layout Engine subsystem) — box packing and edge routing.
- `StdlibFilter` (Rendering Internal subsystem) — standard-library exclusion.
- `SysmlWorkspace`, `SysmlDefinitionNode`, `SysmlFeatureNode` (Semantic subsystem) — model input.
- `LayoutWarnings` (Layout Internal subsystem) — crossing-warning construction.
- The `LayoutTree`, `LayoutBox`, `LayoutCompartment`, and `LayoutLine` data types (Layout subsystem).

##### Callers

The Rendering subsystem selects `GeneralViewLayoutStrategy` when rendering a General View. No
other unit calls it directly.
