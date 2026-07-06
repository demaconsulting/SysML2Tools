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

1. **Scope resolution.** `ExposeScopeResolver.ResolveExposedScope` resolves the view's `expose`
   scope once (or `null` when none applies).
2. **Definition collection.** `CollectDefinitions` gathers the non-stdlib definitions in
   deterministic (ordinal qualified-name) order, additionally excluding — when a scope was
   resolved — any definition whose qualified name is not within it per
   `ExposeScopeResolver.IsInSubjectScope`. An index map from simple name to column is built from
   the (possibly narrowed) set.
3. **Sizing.** Row height derives from the body font size and label padding; the header column width
   and the data column width derive from `MaxLabelWidth`, the widest definition label.
4. **Header row.** An empty corner cell is followed by one centered header cell per definition.
5. **Data rows.** For each row definition, a left-aligned header cell carries its name, then one
   cell per column carries the mark where `ResolveSupertypeIndices` reports that the row definition
   specializes the column definition (matching supertype references to columns by simple name) and
   an empty cell otherwise.
6. **Assembly.** The rows are wrapped in a `LayoutGrid` positioned with a small padding offset, and
   the overall canvas width and height are computed from the column counts and sizes.

When there are no user-defined definitions (either because the workspace is empty or because
scoping excludes every definition), a minimal empty `LayoutTree` with no nodes is returned.

##### Expose Scoping

`CollectDefinitions` is the only place scoping applies: it is a direct, workspace-wide filter with
no single-root heuristic to restrict, so a resolved `expose` scope simply narrows the matrix to the
definitions within the exposed targets' containment subtrees (plus, via
`ExposeScopeResolver.ResolveExposedScope`'s usage-to-type fallback, an exposed feature usage's own
type). Multiple `expose` targets union their subtrees, since `IsInSubjectScope` matches against
every resolved subject. A view with no `expose` statement (including the synthesized `--auto`
view, whose `ViewNode` is `null`) resolves no scope and renders every non-stdlib definition,
unchanged from the pre-scoping behavior.

##### Error Handling

Null `context` or `options` arguments throw `ArgumentNullException`. An empty workspace does not
throw: the strategy returns an empty diagram rather than failing.

##### Dependencies

- `LayoutTree`, `LayoutGrid`, `LayoutGridRow`, `LayoutGridCell`, and `TextAlign`
  (`DemaConsulting.Rendering`).
- `ViewContext` (Rendering subsystem) and `RenderOptions`, `Theme`
  (`DemaConsulting.Rendering.Abstractions`).
- `SysmlWorkspace` and `SysmlDefinitionNode` (Semantic subsystem).
- `StdlibFilter` (Rendering Internal subsystem) — standard-library exclusion.
- `ExposeScopeResolver` (Layout Internal subsystem) — `ResolveExposedScope` and
  `IsInSubjectScope` supply the shared `expose`-scoping used by `CollectDefinitions`.

##### Callers

The layout strategy registry selects `GridViewLayoutStrategy` when a Grid View is requested; it is
not called directly by other units.
