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

1. **Forest construction.** `BuildForest` takes the non-stdlib declarations in deterministic
   (ordinal qualified-name) order so parents precede children, and links each element to the parent
   identified by the prefix before its last `::` separator; elements with no known parent become
   roots.
2. **Recursive emission.** `EmitNode` lays out each row left-to-right at an X derived from its depth
   times a fixed indentation, advancing a shared Y cursor downward. Each row becomes a `LayoutBox`
   whose label combines the element keyword and simple name and whose width fits the label.
3. **Connectors.** For every non-root row a `LayoutLine` is emitted from a vertical stem dropped from
   the parent row down to the child's vertical centre and across to the child box, so the connector
   never crosses the parent's own box or text.
4. **Canvas sizing.** The overall width follows the right-most box and the height follows the final
   Y cursor.

When there are no user-defined elements, a minimal empty `LayoutTree` with no nodes is returned.

##### Error Handling

Null `context` or `options` arguments throw `ArgumentNullException`. An empty workspace does not
throw: the strategy returns an empty diagram rather than failing.

##### Dependencies

- `LayoutTree`, `LayoutBox`, `LayoutLine`, `Point2D`, `BoxShape`, `ArrowheadStyle`, `LineStyle`
  (Layout subsystem).
- `ViewContext`, `RenderOptions`, `Theme` (Rendering subsystem).
- `SysmlWorkspace`, `SysmlNode`, `SysmlPackageNode`, `SysmlDefinitionNode`, `SysmlFeatureNode`,
  `SysmlViewNode`, and `StdlibFilter` (Semantic subsystem).

##### Callers

The layout strategy registry selects `BrowserViewLayoutStrategy` when a Browser View is requested;
it is not called directly by other units.
