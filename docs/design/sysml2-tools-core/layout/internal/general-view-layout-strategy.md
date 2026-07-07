#### GeneralViewLayoutStrategy

##### Purpose

`GeneralViewLayoutStrategy` implements `ILayoutStrategy` to produce a General View diagram. It
renders every user-defined definition (part, port, interface, requirement, action, and so on) as
a keyword-labeled box, groups the boxes that belong to a package inside a folder-shaped
container, lists each definition's owned usages in compartments, and draws specialization,
membership, and attribute-typing edges orthogonally between the boxes. The whole diagram — package
folders and definitions alike — is expressed as a single `DemaConsulting.Rendering` `LayoutGraph`
and placed with one `HierarchicalLayoutAlgorithm.Apply` call: the root scope packs package folders
and top-level definitions by reading order (`ContainmentLayoutAlgorithm`), while each folder's own
contents are ordered by their intra-package edges with the bundled layered algorithm
(`LayeredLayoutAlgorithm`). All box sizing (title bands, compartment rows) remains this strategy's
responsibility, since the layout stage in `DemaConsulting.Rendering.Layout` is theme-agnostic;
box title and folder-tab geometry come from `BoxMetrics` in `DemaConsulting.Rendering.Abstractions`.

##### Data Model

`GeneralViewLayoutStrategy` has no instance state; all input arrives through the `BuildLayout`
parameters. Layout constants (`MinBoxWidth`, `CharWidthFactor`) are declared as `private const`
fields. Private records carry intermediate data: `DefBox` (a user definition with its computed
size, keyword, supertype names, memberships, and compartments), `ModelEdge` (a resolved
specialization/membership/attribute-typing relationship expressed by qualified name, together with
its target end marker and edge kind), `Location` (a located definition's graph node and owning
package, used to resolve and scope edges), and `TruncatedFolder` (a depth-truncated package folder's
leaf graph node and hidden-definition count, used to stamp its ellipsis label onto the placed box
after layout). The private `EdgeKind` enumeration classifies each edge as `Specialization`,
`Membership`, or `Typing`; the `LineStyleForKind` helper maps this kind to a rendered line style
(dashed for `Typing`, solid for the others), so an attribute-typing dependency is visually distinct
from the structural relationships.

##### Key Methods

###### `BuildLayout(ViewContext context, RenderOptions options)`

Entry point. First resolves the view's exposed-name scope via the shared
`ExposeScopeResolver.ResolveExposedScope` (see the *ExposeScopeResolver* unit chapter), then calls
`CollectDefinitions` to gather user definitions restricted to that scope (or every definition when
no scope applies); returns a minimal 200×100 empty `LayoutTree` when none are found. Otherwise
groups the definitions by package with `GroupByPackage`, resolves the specialization/membership/
attribute-typing relationships into qualified-name edges with `BuildModelEdges`, builds the single
input `LayoutGraph` with `BuildGraph`, and places the whole graph with one
`HierarchicalLayoutAlgorithm().Apply(graph, LayoutOptions.ForAlgorithm("containment"))`
call — passing the desired root-scope leaf algorithm through the options parameter (not
`graph.Set(CoreOptions.Algorithm, …)`) so a caller going through `LayoutEngine.Layout(graph)` later
is never misled into skipping the hierarchical engine. When any package folder was depth-truncated,
`DecorateTruncatedFolders` stamps each truncated folder's "+N more…" ellipsis label onto its placed
box. Finally, when `context.ViewNode?.FilterExpressionText` is non-null, attaches the
"parsed but not yet evaluated" warning (from `LayoutWarnings.ForUnevaluatedFilter`) to the returned
tree's `Warnings` via the `LayoutTree with { Warnings = … }` record-copy idiom, leaving the
resolved (unfiltered) scope's content unchanged.

###### `CollectDefinitions(workspace, theme, scope)`

Iterates `workspace.Declarations`, keeping each `SysmlDefinitionNode` that is not a
standard-library element (per `StdlibFilter.IsStdlibElement`) and, when `scope` is non-null, is
within `scope` per `ExposeScopeResolver.IsInSubjectScope`. For each kept definition it builds the
compartments from the owned usage features (grouped by keyword, each formatted as a
`name : Type [n]` row), collects the typed memberships, and computes the box size from the title
and the longest compartment row.

###### `GroupByPackage(defs)`

Groups definitions by the qualified-name prefix before the last `::`, preserving first-seen order.
Top-level definitions (no package prefix) become plain leaves directly on the root graph.

###### `BuildModelEdges(defs)`

Resolves every specialization (subtype → supertype), structural membership (member-type → owner), and
attribute-typing (owner → attribute-type) relationship — across every definition, regardless of
package — into a flat list of qualified-name `ModelEdge`s. Specialization edges carry an open
triangular end marker at the supertype; `part`/`port` memberships carry a filled diamond and `ref`
memberships a hollow diamond at the owner; other memberships are not drawn. In addition, each
`attribute` (or `enum`-typed attribute) feature whose type resolves to a definition in the view
contributes a **typing** edge from the owner to the attribute-type definition, carrying an open
chevron at the type end and rendered as a dashed line. Attribute typing is a usage-type dependency,
not composition, so it uses the OMG dependency notation (dashed line with an open arrowhead) rather
than a membership diamond, and it connects otherwise-disconnected attribute and enumeration
definitions into the cluster near the definitions that reference them. Unresolved types and
self-references are skipped. Whether an edge's endpoints actually receive a graph node — i.e.,
were not depth-truncated — is decided later, in `BuildGraph`.

###### `BuildGraph(groups, modelEdges, theme, depthLimit)`

Builds the single input `LayoutGraph`. Each package becomes a folder container node
(`Shape = Folder`, `Keyword = "package"`, `Label` the simple package name, `TitleHeight` set from
`BoxMetrics.TitleAreaHeight` so the hierarchical engine reserves the exact title band the renderer
will draw) holding its definitions as leaf nodes under `folder.Children`; the folder's own
`CoreOptions.Algorithm` is set to `LayeredLayoutAlgorithm.AlgorithmId` per the established
per-container-algorithm convention (the algorithm override lives on the container node itself, while
every other `CoreOptions` property would live on its `Children` graph). Top-level (unpackaged)
definitions become plain leaves directly on the root graph. When the depth limit forbids a folder's
nested level, its definitions are never added as individual boxes at all — the folder becomes a
single leaf node (its `Children` graph is never touched, so the hierarchical engine keeps this
caller-computed ellipsis size rather than auto-sizing it as a container) sized like the previous
ellipsis-indicator formula, and recorded as a `TruncatedFolder` for later decoration. Every located
definition's node and owning package is recorded in a `located` map so model edges can be resolved
and scoped: an edge whose endpoints share a non-empty package is added to that folder's own
`Children` scope (an intra-package edge the layered algorithm can use to order the folder's
contents); every other edge — including any crossing packages — is added at the root, referencing
the (possibly nested) endpoint nodes directly, per the graph's lowest-common-ancestor edge
convention. An edge touching a depth-truncated (unrendered) definition has no node in the `located`
map to reference and is silently dropped, exactly as before.

###### `MakeDefNode(scope, def)`

Adds one definition as a leaf node to the given scope (the root graph or a folder's `Children`),
carrying its `Label`, `Shape = Rectangle`, `Keyword`, and `Compartments`.

###### `DecorateTruncatedFolders(tree, graph, truncated, theme)`

Replaces each truncated folder's placed box with one carrying its "+N more…" ellipsis label,
positioned within the box's now-known absolute placement. Because the leaf algorithm at a
compound-graph scope emits one box per node in `graph.Nodes` order, the boxes portion of the root
`LayoutTree.Nodes` aligns with `graph.Nodes` by index, so each truncated folder's placed box can be
found by index without any auxiliary identifier lookup (`LayoutBox` carries no `Id`).

##### Error Handling

Null `context` or `options` arguments throw `ArgumentNullException`. A workspace with no user
definitions is not an error: the method returns the minimal empty canvas. Delegated placement
produces valid geometry, so no crossing warnings are emitted.

##### Dependencies

- `ILayoutStrategy` and `ViewContext` (Rendering subsystem) — the strategy contract and view input.
- `RenderOptions` and `Theme` (`DemaConsulting.Rendering.Abstractions`) — render options and sizing
  inputs.
- `LayoutGraph`, `LayoutGraphNode`, `LayoutGraphEdge`, and `CoreOptions` (`DemaConsulting.Rendering`) —
  the input graph model and its well-known cascading options.
- `HierarchicalLayoutAlgorithm`, `ContainmentLayoutAlgorithm`, `LayeredLayoutAlgorithm`, and
  `LayoutOptions` (`DemaConsulting.Rendering.Layout`) — the layout engine and the bundled leaf
  algorithms it delegates to per scope.
- `BoxMetrics` (`DemaConsulting.Rendering.Abstractions`) — box title-area and folder-tab geometry.
- `StdlibFilter` (Rendering Internal subsystem) — standard-library exclusion.
- `SysmlWorkspace`, `SysmlDefinitionNode`, `SysmlFeatureNode` (Semantic subsystem) — model input.
- `ExposeScopeResolver` (Layout Internal subsystem) — `ResolveExposedScope` and
  `IsInSubjectScope` supply the shared `expose`-scoping used by `BuildLayout` and
  `CollectDefinitions`.
- `LayoutWarnings` (Layout Internal subsystem) — `ForUnevaluatedFilter` supplies the
  "parsed but not yet evaluated" filter-expression warning text.
- The `LayoutTree`, `LayoutBox`, `LayoutCompartment`, `LayoutLine`, `LayoutLabel`, and `Point2D` data
  types (`DemaConsulting.Rendering`).
- `FeatureMembership` (private record) — carries the keyword and type reference of one owned feature.

##### Callers

The Rendering subsystem selects `GeneralViewLayoutStrategy` when rendering a General View. No
other unit calls it directly.
