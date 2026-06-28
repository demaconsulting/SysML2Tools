#### GridViewLayoutStrategy

##### Purpose

`GridViewLayoutStrategy` lays out a Grid View as a specialization relationship matrix: the
workspace's user-defined definitions form both the rows and the columns, and a cell is marked where
the row definition specializes the column definition. Its single responsibility is to turn the
definitions and their supertype references into a positioned `LayoutTree`.

##### Data Model

The strategy is a stateless `ILayoutStrategy`. Inputs are a `ViewContext` (carrying the
`SysmlWorkspace`) and `RenderOptions` (carrying the `Theme`). It uses a private `DefRow` record
holding a definition's name and its supertype references. Output is a `LayoutTree` containing a
single `LayoutGrid` of `LayoutGridRow` and `LayoutGridCell` values.

##### Key Methods

###### `BuildLayout(context, options)`

Builds the matrix:

1. **Definition collection.** `CollectDefinitions` gathers the non-stdlib definitions in
   deterministic (ordinal qualified-name) order. An index map from simple name to column is built
   from them.
2. **Sizing.** Row height derives from the body font size and label padding; the header column width
   and the data column width derive from `MaxLabelWidth`, the widest definition label.
3. **Header row.** An empty corner cell is followed by one centered header cell per definition.
4. **Data rows.** For each row definition, a left-aligned header cell carries its name, then one
   cell per column carries the mark where `ResolveSupertypeIndices` reports that the row definition
   specializes the column definition (matching supertype references to columns by simple name) and
   an empty cell otherwise.
5. **Assembly.** The rows are wrapped in a `LayoutGrid` positioned with a small padding offset, and
   the overall canvas width and height are computed from the column counts and sizes.

When there are no user-defined definitions, a minimal empty `LayoutTree` with no nodes is returned.

##### Error Handling

Null `context` or `options` arguments throw `ArgumentNullException`. An empty workspace does not
throw: the strategy returns an empty diagram rather than failing.

##### Dependencies

- `LayoutTree`, `LayoutGrid`, `LayoutGridRow`, `LayoutGridCell`, `TextAlign` (Layout subsystem).
- `ViewContext`, `RenderOptions`, `Theme` (Rendering subsystem).
- `SysmlWorkspace`, `SysmlDefinitionNode`, and `StdlibFilter` (Semantic subsystem).

##### Callers

The layout strategy registry selects `GridViewLayoutStrategy` when a Grid View is requested; it is
not called directly by other units.
