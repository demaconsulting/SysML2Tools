#### GridViewLayoutStrategy

##### Purpose

`GridViewLayoutStrategy` lays out a Grid View as a specialization relationship matrix: the
workspace's user-defined definitions form both the rows and the columns, and a cell is marked where
the row definition specializes the column definition. Its single responsibility is to turn the
definitions and their supertype references into a positioned `LayoutTree`.

##### Data Model

The strategy is a stateless `ILayoutStrategy`. Inputs are a `ViewContext` (carrying the
`SysmlWorkspace`) and `RenderOptions` (carrying the `Theme`). It uses a private `DefRow` record
holding a definition's qualified name, its simple name, and its supertype references. Output is a
`LayoutTree` containing a single `LayoutGrid` of `LayoutGridRow` and `LayoutGridCell` values.

##### Key Methods

###### `BuildLayout(context, options)`

Builds the matrix:

1. **Scope resolution.** `ExposeScopeResolver.ResolveExposedScope` resolves the view's `expose`
   scope once (or `null` when none applies).
2. **Definition collection.** `CollectDefinitions` gathers the non-stdlib definitions in
   deterministic (ordinal qualified-name) order, narrowed by the relation-preserving rule
   described in *Expose Scoping* below when a scope was resolved. An index map from simple name to
   column is built from the (possibly narrowed) set.
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

`CollectDefinitions` is the only place scoping applies. Unlike the four single-root strategies, the
Grid View has no root to restrict, so the resolved `expose` scope is applied directly as a
workspace-wide definition filter — but because the matrix's entire purpose is to show
specialization relationships between rows and columns, membership is decided along **two
dimensions**, and a definition is kept when **at least one** of them is in scope:

1. **Direct containment** — the definition's own qualified name is within a resolved `expose`
   target's containment subtree (`ExposeScopeResolver.IsInSubjectScope`), including, via
   `ExposeScopeResolver.ResolveExposedScope`'s usage-to-type fallback, an exposed feature usage's
   own type. Multiple `expose` targets union their subtrees, since `IsInSubjectScope` matches
   against every resolved subject.
2. **Specialization relationship** — the definition is a supertype of an in-scope definition, or
   an in-scope definition is one of its own supertypes (resolved by the same simple-name matching
   `ResolveSupertypeIndices` uses for the marked cells).

This relation-preserving rule means exposing only the specific side of a specialization (e.g. a
`Sub` that specializes an out-of-subtree `A`) still renders both `A` and `Sub` as header rows and
columns with the specialization mark between them, rather than silently dropping one side of the
relationship the matrix exists to show.

Implementation-wise, `CollectDefinitions` runs in two phases so the `scope is null` case (no
`expose` statement, including the synthesized `--auto` view whose `ViewNode` is `null`) remains a
byte-identical "return everything" fast path: it first collects every non-stdlib definition
unfiltered and builds a full simple-name index across all of them, then — only when a scope is
resolved — computes which definitions are directly in scope, resolves every definition's
supertype indices against the full index, and keeps the union of the directly-in-scope set with
any definition connected to it by a specialization edge in either direction, before returning the
kept definitions in their original deterministic order.

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
