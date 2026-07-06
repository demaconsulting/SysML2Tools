#### BrowserViewLayoutStrategy

##### Purpose

`BrowserViewLayoutStrategy` lays out a Browser View: it presents the membership hierarchy of the
workspace's user-defined elements as an indented tree of rows, with connector lines from each parent
to its children. Its single responsibility is to turn the qualified-name hierarchy into a positioned
`LayoutTree`.

##### Data Model

The strategy is a stateless `ILayoutStrategy`. Inputs are a `ViewContext` (carrying the
`SysmlWorkspace`) and `RenderOptions` (carrying the `Theme`). It uses a private `TreeNode` record
holding a node's qualified name, display label, optional keyword, and child nodes. Output is a
`LayoutTree` whose nodes are `LayoutBox` rows and `LayoutLine` parent-to-child connectors.

##### Key Methods

###### `BuildLayout(context, options)`

Walks the membership forest and emits rows:

1. **Scope resolution.** `ExposeScopeResolver.ResolveExposedScope` resolves the view's `expose`
   scope once (or `null` when none applies).
2. **Forest construction.** `BuildForest` takes the non-stdlib declarations in deterministic
   (ordinal qualified-name) order so parents precede children — additionally excluding, when a
   scope was resolved, any element whose qualified name is not within it per
   `ExposeScopeResolver.IsInSubjectScope` — and links each remaining element to the parent
   identified by the prefix before its last `::` separator; elements with no known parent (whether
   because they are genuine workspace roots, or because scoping excluded their would-be parent)
   become roots.
3. **Recursive emission.** `EmitNode` lays out each row left-to-right at an X derived from its depth
   times a fixed indentation, advancing a shared Y cursor downward. Each row becomes a `LayoutBox`
   whose label combines the element keyword and simple name and whose width fits the label.
4. **Connectors.** For every non-root row a `LayoutLine` is emitted from a vertical stem dropped from
   the parent row down to the child's vertical centre and across to the child box, so the connector
   never crosses the parent's own box or text.
5. **Canvas sizing.** The overall width follows the right-most box and the height follows the final
   Y cursor.

When there are no user-defined elements (either because the workspace is empty or because scoping
excludes every element), a minimal empty `LayoutTree` with no nodes is returned.

##### Expose Scoping

`BuildForest` is the only place scoping applies: it is a direct, workspace-wide filter with no
single-root heuristic to restrict, so a resolved `expose` scope simply narrows the forest to the
elements within the exposed targets' containment subtrees (plus, via
`ExposeScopeResolver.ResolveExposedScope`'s usage-to-type fallback, an exposed feature usage's own
type). Because the parent-lookup step runs only over the narrowed set, an exposed target whose own
parent was filtered out is promoted to a forest root, so the diagram becomes one or more subtrees
rooted at the exposed target(s) rather than a single truncated tree. Multiple `expose` targets union
their subtrees, since `IsInSubjectScope` matches against every resolved subject. A view with no
`expose` statement (including the synthesized `--auto` view, whose `ViewNode` is `null`) resolves no
scope and renders the full membership forest, unchanged from the pre-scoping behavior.

##### Error Handling

Null `context` or `options` arguments throw `ArgumentNullException`. An empty workspace does not
throw: the strategy returns an empty diagram rather than failing.

##### Dependencies

- `LayoutTree`, `LayoutBox`, `LayoutLine`, `Point2D`, `BoxShape`, `EndMarkerStyle`, and
  `LineStyle` (`DemaConsulting.Rendering`).
- `ViewContext` (Rendering subsystem) and `RenderOptions`, `Theme`
  (`DemaConsulting.Rendering.Abstractions`).
- `SysmlWorkspace`, `SysmlNode`, `SysmlPackageNode`, `SysmlDefinitionNode`, `SysmlFeatureNode`, and
  `SysmlViewNode` (Semantic subsystem).
- `StdlibFilter` (Rendering Internal subsystem) — standard-library exclusion.
- `ExposeScopeResolver` (Layout Internal subsystem) — `ResolveExposedScope` and
  `IsInSubjectScope` supply the shared `expose`-scoping used by `BuildForest`.

##### Callers

The layout strategy registry selects `BrowserViewLayoutStrategy` when a Browser View is requested;
it is not called directly by other units.
