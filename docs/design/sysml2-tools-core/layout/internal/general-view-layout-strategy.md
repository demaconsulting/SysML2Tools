#### GeneralViewLayoutStrategy

##### Purpose

`GeneralViewLayoutStrategy` implements `ILayoutStrategy` to produce a General View diagram. It
renders every user-defined definition (part, port, interface, requirement, action, and so on) as
a keyword-labelled box, groups the boxes that belong to a package inside a folder-shaped
container, lists each definition's owned usages in compartments, and draws specialization edges
between subtypes and their supertypes.

##### Data Model

`GeneralViewLayoutStrategy` has no instance state; all input arrives through the `BuildLayout`
parameters. Layout constants (`MinBoxWidth`, `CharWidthFactor`, `EdgeClearance`, `MaxIterations`,
`MaxGapMultiplier`, `HeatThreshold`, `RowClusterTolerance`, `PerEdgeExpansion`) are declared as `private const`
fields. Two private records carry intermediate
data: `DefBox` (a user definition with its computed size, keyword, supertype names, and
compartments) and `PlacedBox` (a definition with absolute coordinates, used as an edge anchor).

##### Key Methods

###### `BuildLayout(ViewContext context, RenderOptions options)`

Entry point. Calls `CollectDefinitions` to gather user definitions; returns a minimal
200×100 empty `LayoutTree` when none are found. Otherwise groups the definitions by package
and enters a per-band heat loop (up to `MaxIterations` iterations): calls `PlaceGroups` with
the current `hGap`/`vGap` values, builds specialization and membership edges, then calls
`DetectRows` to cluster placed boxes into rows and `MeasureVerticalBandHeat` to count vertical
edge segments passing through each inter-row band. For each band whose heat exceeds
`HeatThreshold`, the required extra gap is computed as `(heat − HeatThreshold) × PerEdgeExpansion`.
The maximum extra gap across all hot bands is added to `initialVGap` as a uniform `vGap`
increase (simplified max-gap fallback), capped at `MaxGapMultiplier × initialVGap`. The loop
exits early when no band is hot, the gap cap is reached, or the gap stops growing. After the
loop the edges are appended to the node list and the assembled tree is returned with any crossing
warnings. Horizontal band heat is not yet measured (deferred as a known limitation).

###### `CollectDefinitions(workspace, theme)`

Iterates `workspace.Declarations`, keeping each `SysmlDefinitionNode` that is not a
standard-library element (per `StdlibFilter.IsStdlibElement`). For each kept definition it builds
the compartments from the owned usage features (grouped by keyword, each formatted as a
`name : Type [n]` row) and computes the box size from the title and the longest compartment row.

###### `GroupByPackage(defs)`

Groups definitions by the qualified-name prefix before the last `::`, preserving first-seen
order. Top-level definitions (no package prefix) form their own standalone blocks.

###### `PlaceGroups(groups, theme, depthLimit, hGap, vGap)`

Packs the definition boxes of each package inside a folder box using `ContainmentPacker`, then
packs the folder boxes and standalone boxes across the canvas with a second `ContainmentPacker`
pass. The `hGap` and `vGap` parameters control horizontal and vertical spacing; these are
supplied by the `BuildLayout` adaptive loop and may grow across iterations. A full title area is
reserved above each folder's contents so the package label never overlaps the first child. When
the depth limit forbids the nested level, a folder's contents are replaced with a single ellipsis
indicator.

###### `BuildSpecializationEdges(defs, placed)`

For each definition with a declared supertype present in the workspace, routes an orthogonal line
with `ChannelRouter` from the subtype box to the supertype box, keeping `EdgeClearance` from
unrelated boxes, and emits a `LayoutLine` with an open arrowhead at the supertype end. Returns
the routed lines and the count of edges that had to cross a box.

###### `BuildMembershipEdges(defs, placed)`

For each definition with typed owned features, filters to only features with keyword `part` or
`port`. For each qualifying feature, resolves its type to a placed box and routes an orthogonal
line from the member-type box to the owner box, placing a filled-diamond arrowhead at the owner
end. Features with other keywords (e.g. `ref`, `attribute`) are skipped to keep the diagram
uncluttered. Routing uses `ChannelRouter` with `EdgeClearance` from unrelated boxes. Returns the
routed lines and the count of edges that had to cross a box.

###### `DetectRows(placed)`

Clusters the placed boxes into horizontal rows by sorting them on their top-edge Y coordinate and
greedily grouping boxes whose top-edges fall within `RowClusterTolerance` of the first box in
each row. Returns the rows in ascending Y order as a list of index lists into `placed`.

###### `MeasureVerticalBandHeat(rows, placed, edges)`

Computes the inter-row band spans (bandTop = max bottom-edge of row i, bandBottom = min
top-edge of row i+1) and counts, for each band, how many vertical edge segments (|ΔX| < 1.0)
from `edges` overlap that band's Y interval. Returns one heat value per inter-row band
(length = rows.Count − 1), or an empty list when there are fewer than two rows.

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
- `FeatureMembership` (private record) — carries the keyword and type reference of one owned feature.

##### Callers

The Rendering subsystem selects `GeneralViewLayoutStrategy` when rendering a General View. No
other unit calls it directly.
