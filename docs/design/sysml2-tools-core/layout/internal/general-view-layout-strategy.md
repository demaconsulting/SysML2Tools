#### GeneralViewLayoutStrategy

##### Purpose

`GeneralViewLayoutStrategy` implements `ILayoutStrategy` to produce a General View diagram. It
renders every user-defined definition (part, port, interface, requirement, action, and so on) and
— since Phase 2d (see `ROADMAP.md`'s "View `filter [<expr>];` expression evaluation" section) —
every named usage-level declaration too, as a keyword-labeled box, groups the boxes that belong to
a package inside a folder-shaped
container, lists each definition's owned usages in compartments, and draws specialization,
membership, attribute-typing, redefinition, subsetting, connect, allocate, dependency, and
binding edges orthogonally between the boxes. The whole diagram — package
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
size, keyword, supertype names, memberships, compartments, and `Annotations` — the definition's
own `SysmlNode.Annotations` list, threaded through by `CollectDefinitions` so `AddAnnotationNote`
(below) can render them without a second workspace lookup), `ModelEdge` (a resolved
specialization/membership/attribute-typing/redefinition/subsetting/connect/allocate/dependency/
binding relationship expressed by qualified name, together with its target end marker, edge kind,
and an optional midpoint label), `Location` (a located definition's
graph node and owning package, used to resolve and scope edges), and `TruncatedFolder` (a
depth-truncated package folder's leaf graph node and hidden-definition count, used to stamp its
ellipsis label onto the placed box after layout). `DroppedRelationshipEdge` (a private record)
carries the short kind label, raw source/target reference, and human-readable reason for one
`Connect`/`Allocate`/`Dependency`/`Binding` edge `BuildModelEdges` could not map to two distinct
rendered boxes, feeding `LayoutWarnings.ForDroppedRelationshipEdges`. `FeatureMembership` (a private record) carries
each owned feature's keyword, raw type reference (`TypeName`, nullable — a feature may declare a
redefinition with no explicit type annotation), simple `Name`, raw
`RedefinedFeatureName` reference, and the raw `SubsettedFeatureNames` list (populated verbatim
from `SysmlFeatureNode.SupertypeNames` — a feature's `subsets`/`:>` targets, not a new AST field).
`FeatureMembership` does not itself carry an
`ExpressionText` field — constraint-kind features' expression text (non-null only for
`"require constraint"`/`"assume constraint"`/`"constraint"` keywords) is read directly from the
owning `SysmlFeatureNode.ExpressionText` where needed (`BuildCompartments`), not copied into
`FeatureMembership`. `CollectMemberships` includes a feature when `TypeName`,
`RedefinedFeatureName`, or a non-empty `SubsettedFeatureNames` is present. The private `EdgeKind`
enumeration classifies each edge as
`Specialization`, `Membership`, `Typing`, `Redefinition`, `Subsetting`, `Connect`, `Allocate`,
`Dependency`, or `Binding`; `Subsetting` is a purely view-layer classification — it does not
correspond to a public `SysmlEdgeKind` — reusing `SupertypeNames` and the same owner-resolution
walk (`ResolveRedefinitionOwner`) already used for `Redefinition`. The `LineStyleForKind` helper
maps each kind to a rendered line style (dashed for `Typing`, `Allocate`, `Dependency`, and
`Subsetting`; solid for all others), so an attribute-typing/allocate/dependency/subsetting
dependency is visually distinct from the structural relationships.

##### Key Methods

###### `BuildLayout(ViewContext context, RenderOptions options)`

Entry point. First resolves the view's exposed-name scope via the shared
`ExposeScopeResolver.ResolveExposedScope` (see the *ExposeScopeResolver* unit chapter) — which
(Phase 2a) already narrows any bracket-filtered `expose` entry to its matched definitions and
records any bracket-filter parse/evaluation failures on `scope.Failures` — then calls
`CollectDefinitions` to gather user definitions restricted to that scope (or every definition when
no scope applies); returns a minimal 200×100 empty `LayoutTree` when none are found. If the view
carries standalone `FilterExpressionText`, the method next parses it with
`FilterExpressionParser.Parse`; a successful parse is evaluated with
`FilterExpressionEvaluator.Evaluate` over the already expose-scoped candidate definitions, narrowing
`defs` to the matched subset and returning the same minimal empty canvas when that subset is empty.
A parse failure or unsupported Phase 1 construct does not abort layout: the first parser
diagnostic message is remembered as the warning reason and the method continues rendering the
unfiltered resolved scope. Once scope and standalone-filter narrowing are both applied, the method
calls `RemoveRedundantNestedUsages(defs)` (see below), which drops any usage-level box whose
nearest still-rendered ancestor is also present in the final `defs` set — this must run after both
narrowing steps, not before or inside `CollectDefinitions`, so a usage whose parent was excluded by
a filter (rather than genuinely absent from scope) survives as its own standalone box. The method returns
the same minimal empty canvas if the dedup empties `defs`. The remaining pipeline is unchanged:
definitions are grouped by package
with `GroupByPackage`, the specialization/membership/attribute-typing/redefinition/subsetting/
connect/allocate/dependency/binding relationships are
resolved into qualified-name edges (plus any dropped-edge diagnostics) with `BuildModelEdges`, the
single input `LayoutGraph` is built
with `BuildGraph`, and the whole graph is placed with one
`HierarchicalLayoutAlgorithm().Apply(graph, LayoutOptions.ForAlgorithm("containment"))` call —
passing the desired root-scope leaf algorithm through the options parameter (not
`graph.Set(CoreOptions.Algorithm, …)`) so a caller going through `LayoutEngine.Layout(graph)` later
is never misled into skipping the hierarchical engine. When any package folder was depth-truncated,
`DecorateTruncatedFolders` stamps each truncated folder's "+N more…" ellipsis label onto its placed
box. Finally, the returned tree's `Warnings` concatenates
`LayoutWarnings.ForUnevaluatedFilter` (only when standalone filter parsing/evaluation failed),
`LayoutWarnings.ForUnevaluatedExposeBracketFilter(context.ViewName, scope?.Failures ?? [])` (Phase
2a: one warning per bracket-filter expression that failed to parse or evaluate — a
successfully-evaluated bracket filter now has real narrowing effect and produces no warning), and
`LayoutWarnings.ForDroppedRelationshipEdges(context.ViewName, dropped)` (one warning per
`Connect`/`Allocate`/`Dependency`/`Binding` edge `BuildModelEdges` could not map to two distinct
rendered boxes — see `BuildModelEdges` below) via
the `LayoutTree with { Warnings = … }` record-copy idiom.

###### `CollectDefinitions(workspace, theme, scope)`

Iterates `workspace.Declarations`, keeping each `SysmlDefinitionNode` **or named**
`SysmlFeatureNode` (Phase 2d — a metaclass-kind classification test like
`filter @SysML::PartUsage;` is inherently about usages, not definitions, and this method doubles
as both the filter-candidate source and the box-rendering function for the whole diagram, so
usages must be admitted here for such a filter to have anything meaningful to narrow; an unnamed
feature is excluded, since it has no stable qualified name to key a box on) that is not a
standard-library element (per `StdlibFilter.IsStdlibElement`) and, when `scope` is non-null, is
within `scope` per `ExposeScopeResolver.IsInSubjectScope`. This admission is unconditional of
nesting depth — it is deliberately a superset used as the filter-candidate pool, including usages
nested inside an already-admitted definition/usage. `BuildLayout` narrows this superset down to
the final rendered set in two later steps: the standalone `filter [<expr>];` narrowing, then
`RemoveRedundantNestedUsages` (Retry 1 fix, cascading depth-ordered pass hardened in Retry 2 —
see below), which removes exactly the nested usages
that would duplicate an already-rendered immediate parent's compartment row. For each kept
declaration it builds the
compartments from the owned usage features (grouped by keyword, each formatted via
`FormatFeatureRow`), collects the typed memberships, and computes the box size from the title
and the longest compartment row. `BuildCompartments`/`CollectMemberships` take the common
`SysmlNode` base type (not `SysmlDefinitionNode`) precisely so a usage-level candidate's owned
features are collected identically to a definition's.

Each compartment's title comes from `Pluralize(keyword)`, which special-cases a small set of
individually-significant single-member keywords to a guillemet-wrapped stereotype form instead of
the generic pluralized-keyword default: `"subject"` → `"«subject»"`, `"assume constraint"` →
`"«assume constraint»"`, `"require constraint"` → `"«require constraint»"`, and `"constraint"` →
`"«constraint»"`. This reuses the exact guillemet convention already established elsewhere in this
file for the `«allocate»` edge midpoint label (see `BuildModelEdges`), rather than inventing a new
one. Every other keyword introduced alongside this work — `"enum value"` (rendered as the
compartment title `"enum values"`), `"actor"`, `"stakeholder"` — falls through to the generic
pluralized-keyword default, matching the OMG spec figures' own compartment-heading conventions for
those kinds.

`FormatFeatureRow` renders a feature with a non-null, non-empty `ExpressionText` (constraint-kind
features only) as its raw expression text alone (unnamed) or as `"{name}: {expr}"` (named)
instead of the generic `"name : Type [multiplicity]"` shape used by every other feature kind,
since a constraint's defining content is its expression, not a type/multiplicity pair.

###### `RemoveRedundantNestedUsages(defs)` (Retry 1 fix; cascading depth-ordered pass hardened in Retry 2)

Removes usage-level (`DefBox.IsUsage`) boxes from `defs` whose nearest still-rendered ancestor is
also present in `defs` — i.e., a usage nested (directly, or transitively through one or more other
excluded usages) inside a box that already shows it as a compartment row. Since every node's
qualified name is a strict `parent::child` containment chain, every ancestor is strictly shallower
(fewer `::`-separated segments) than its descendants, and a usage's immediate-parent qualified name
is recovered by stripping the qualified name's last `::`-separated segment (a top-level usage, with
no `::` in its qualified name, is never excluded). Boxes are processed in ascending depth order
(fewest `::` occurrences first) while incrementally building an `excluded` set, so that by the time
a usage is tested, every one of its ancestors has already been finally decided. A usage is excluded
only when its immediate parent is present in `defs` **and has not itself already been excluded**
earlier in this same pass — i.e., only when the immediate parent is itself still rendered, and
therefore already shows the usage as one of the parent's compartment rows (`BuildCompartments`
renders a definition/usage's direct `SysmlNode.Children` only — it never reaches into
grandchildren). Conversely, when the immediate parent has itself just been excluded in this same
pass, it no longer renders anywhere, so its own compartment can no longer show this usage — the
usage is therefore not excluded, and survives as its own standalone box. This cascades correctly
through any nesting depth: two, three, or more levels of usage-in-usage nesting all resolve
correctly in this single ordered pass, so a deeper-nested usage is never silently dropped merely
because an intermediate ancestor between it and the nearest rendered box was itself excluded.
Definitions (`DefBox.IsUsage` is `false`) are never excluded by this rule, preserving all
pre-Phase-2d definition-rendering behavior.

This exists because `CollectDefinitions`'s usage-level widening (Phase 2d) admits every named
usage regardless of nesting depth — necessary so a metaclass-kind filter like
`filter @SysML::PartUsage;` has a complete candidate pool to narrow — but that superset, if
rendered unfiltered, duplicates every nested usage already shown as a compartment row inside its
owning definition's box (empirically, 21 → 47 rendered boxes for
`docs/gallery/models/01-drone-general.sysml`'s `DroneGeneralView`, a real regression found by
quality re-validation). The initial Retry 1 fix computed the "which parents are present" set once
from the pre-dedup `defs` list and tested every usage against that single fixed snapshot; a
subsequent quality re-validation found this silently dropped a usage nested **two or more levels**
deep (e.g. `part def A { part b { part c; } }`), since `c`'s immediate parent `b` was present in
the pre-removal snapshot even though `b` was itself simultaneously being excluded — `c` vanished
from the diagram entirely, shown neither as its own box nor inside any surviving compartment. The
current depth-ordered, incrementally-shrinking-`excluded`-set pass (Retry 2) fixes this: `b` is
excluded before `c` is considered, so `c`'s immediate parent is no longer treated as present by the
time `c` is tested, and `c` correctly survives as its own standalone box. `BuildLayout` calls this
method **after** the standalone `filter [<expr>];` narrowing and **before** `GroupByPackage`, so
the dedup sees only the truly final, fully scope-and-filter-narrowed set: a nested usage whose
immediate parent was excluded by scope, by that filter, or by this same dedup pass (rather than
genuinely still present and rendered) is correctly kept as its own standalone box, since it either
duplicates a still-rendered ancestor's compartment row (and is correctly excluded) or is never
shown anywhere else and must render standalone (and is therefore never silently lost).

###### `GroupByPackage(defs)`

Groups definitions by the qualified-name prefix before the last `::`, preserving first-seen order.
Top-level definitions (no package prefix) become plain leaves directly on the root graph.

###### `BuildModelEdges(defs, workspace, isScoped)`

Resolves every specialization (subtype → supertype), structural membership (member-type → owner),
attribute-typing (owner → attribute-type), redefinition (subtype → the owning definition of
the redefined feature), and subsetting (subtype → the owning definition of the subsetted
ancestor feature) relationship — across every definition, regardless of
package — into a flat list of qualified-name `ModelEdge`s, then appends `Connect`/`Allocate`/
`Dependency`/`Binding` edges resolved directly from `workspace.Index.AllEdges` (the semantic
model's already-resolved edges — no re-resolution needed here). Specialization edges carry an open
triangular end marker at the supertype; `part`/`port` memberships carry a filled diamond; other
memberships are not drawn (the `ref` keyword no longer contributes a hollow-diamond membership
marker — see below). In addition, each `attribute` (or `enum`-typed attribute) feature whose type
resolves to a definition in the view contributes a **typing** edge from the owner to the
attribute-type definition, carrying an open
chevron at the type end and rendered as a dashed line. Attribute typing is a usage-type dependency,
not composition, so it uses the OMG dependency notation (dashed line with an open arrowhead) rather
than a membership diamond, and it connects otherwise-disconnected attribute and enumeration
definitions into the cluster near the definitions that reference them. Unresolved types and
self-references are skipped.

Each `ref`-keyword feature (previously rendered with an obsolete hollow-diamond membership marker —
a bug fixed by this unit) instead contributes a **dependency** edge from the owner to the
referenced type, sharing the exact same rendering (dashed line, open chevron, `EdgeKind.Dependency`)
as the new public `Dependency` edge kind below — per current OMG SysML v2 notation, a `ref`
feature is a usage-type dependency, not a diamond-marked structural membership.

Each feature with a non-null `RedefinedFeatureName`
contributes a **redefinition** edge from the subtype to the owning definition of the redefined
feature, carrying a hollow-triangle-crossbar end marker at the owner and rendered as a solid line
via `ResolveRedefinitionOwner`: a qualified reference (containing `::`) strips the text before the
*last* `::` segment and resolves it directly via `TryResolveQualified`; a bare-name reference
instead walks the redefining definition's own `SupertypeNames` (resolved the same way), checking
each resolved supertype's own `Memberships` for a matching simple `Name`, and recurses
transitively up the chain (guarded by a `HashSet<string>` of visited qualified names to prevent an
infinite loop on a malformed cyclic supertype graph) when the immediate supertype does not declare
it. Neither resolving nor a self-referential result (`owner == def.QualifiedName`) produces an
edge or a diagnostic — consistent with the existing `TryResolveQualified`-failure-is-silent
convention used by the other edge kinds in this method.

Each entry in a feature's `SubsettedFeatureNames` (populated verbatim from
`SysmlFeatureNode.SupertypeNames` — see `FeatureMembership` above) contributes a **subsetting**
edge from the subtype to the owning definition of the subsetted (ancestor) feature, carrying a
hollow-triangle end marker (the same marker as `Specialization`, distinguished purely by the
dashed line style) at the owner, resolved by reusing `ResolveRedefinitionOwner` verbatim — a
subsetted-feature reference resolves identically to a redefined-feature reference, since both are
"a bare or qualified reference to a member the owner inherits" in SysML v2 semantics.
`Subsetting` is intentionally *not* a new public `SysmlEdgeKind`: it is derived entirely from
existing AST/resolver data at render time, per the project's explicit design decision to avoid
adding semantic-model surface area for a relationship that can be fully reconstructed from
`SupertypeNames` plus the existing redefinition-owner walk.

Finally, `Connect`/`Allocate`/`Dependency`/`Binding` edges are read directly from
`workspace.Index.AllEdges`, one iteration over every `SysmlEdge` whose `Kind` matches one of the
four. Each endpoint (`SourceQualifiedName`/`TargetQualifiedName`) is mapped to its *owning rendered
box* via `ResolveOwningBox` (see below) — necessary because a `Connect`/`Binding` endpoint is
frequently a dotted feature-chain qualified name (e.g. `Drone::controller::power`), not itself a
definition, so it must be mapped up to the definition that actually owns the referenced feature
before it can become a graph edge endpoint. `Allocate` edges carry an open chevron at the target
and a `«allocate»` midpoint label; `Dependency` edges carry an open chevron with no label (matching
the `ref`-fix rendering above); `Connect` edges carry no end marker and no label; `Binding` edges
carry no end marker and an `=` midpoint label. An edge is only emitted when both endpoints resolve
to *distinct* boxes — a same-box result is a genuine self-loop and is dropped, exactly as every
other edge kind in this method already does for self-references. For the dominant real-world
corpus shape — two sibling features (e.g. ports) declared directly in their owning `part def`s,
referenced from an enclosing part via bare `part` usages with no per-instance nested redeclaration
— `ReferenceResolver`'s dotted-chain resolution now (as of the instance-path-preserving fix
documented in the `ReferenceResolver` design chapter) correctly returns an instance-relative
qualified name for each endpoint (e.g. `Drone::controller::power`, `Drone::battery::output`), which
`ResolveOwningBox`'s shortest-matching-prefix walk maps to two *distinct* boxes — this is the
normal, expected outcome for that shape, not a self-loop. A genuine same-box result now only arises
when both endpoints are truly features of the same enclosing definition (e.g. two sibling ports of
one part, with no distinguishing intermediate owner) — a rarer, legitimately self-referential
model shape.

Every edge dropped by this method — whether because an endpoint failed to resolve to any rendered
box, or because both endpoints resolved to the same box — is also recorded (subject to the scoping
rule described in the next paragraph) as a `DroppedRelationshipEdge` (kind, raw source/target
reference, and a short human-readable reason), returned as `BuildModelEdges`'s second tuple element
and surfaced by `BuildLayout` via `LayoutWarnings.ForDroppedRelationshipEdges` — a defense-in-depth
diagnostic ensuring that any residual or future resolution gap remains visible in the view's
`Warnings` rather than being silently and invisibly dropped, as it was before this diagnostic was
added. An unresolved-endpoint drop is only reported when the view carries no `expose` scope
restriction: within an `expose`-scoped view, an endpoint whose owning box falls outside the exposed
subtree legitimately fails to resolve by design, and reporting it would be misleading noise for
ordinary, intentional scope narrowing (confirmed empirically while regenerating the gallery's
`expose`-scoped `BatterySubsystemView`, whose `Allocate`/`Dependency`/`Connect` edges reaching
outside the exposed `Battery` subtree produced exactly this noise before the scoping rule was
added). A genuine self-loop (both endpoints resolve, to the same box) is always reported,
regardless of scoping, since a same-box result can never be an intended outcome of `expose`
narrowing.

Whether an edge's endpoints actually
receive a graph node — i.e.,
were not depth-truncated — is decided later, in `BuildGraph`.

###### `ResolveOwningBox(qualifiedName, workspace, byQualified, bySimple)`

Resolves the qualified name of the rendered box that "owns" a `Connect`/`Binding` endpoint
reference (also reused, degenerately, for `Allocate`/`Dependency` endpoints that are already
definition names). If the reference already names a rendered definition, it resolves directly
(the common, simple case). Otherwise, the reference is a dotted-feature-chain qualified name
(e.g. `Drone::controller::power`, produced by `ReferenceResolver`'s feature-chain walk, which
always emits `"::"`-joined segments — never the raw `.`-separated source text). The method walks
successively shorter `"::"`-split prefixes of that name, from **longest to shortest**, looking for
a `SysmlFeatureNode` declaration whose own resolved `Typing` edge targets a rendered box; each
match overwrites the running candidate (it does not break early), so the **shortest** matching
prefix — the feature immediately, directly owned by the enclosing rendered definition — wins.
This "shortest wins" rule is essential: a naive "walk to the nearest enclosing definition" (i.e.,
longest-prefix-wins) would resolve *both* sides of the dominant real-world corpus shape — e.g.
`connect controller.power to battery.output;` written inside a single enclosing `part def Drone`
— to the same box (`Drone`), producing a false self-loop where a real diagram must show two
distinct boxes (`Controller` and `Battery`) connected.

###### `BuildGraph(groups, modelEdges, theme, depthLimit)`

Builds the single input `LayoutGraph`, setting `CoreOptions.MergeParallelEdges` to `false` on the
root graph so multiple distinct model relationship edges that happen to share the same source and
target (for example two attributes of the same type, or a redefinition edge that coincides with
another edge between the same two definitions) are never collapsed by the bundled layered
algorithm's default parallel-edge merging — every distinct model relationship this strategy adds
remains its own visible, independently-routed edge, unlike `LayeredPlacement`'s helper (used by the
flat view strategies), which defaults to the algorithm's own merge-by-default behavior unless a
caller opts out (see `LayeredPlacement`'s design documentation). Each package becomes a folder
container node
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

###### `AddAnnotationNote(scope, def, defNode, theme)`

Called immediately after `MakeDefNode` for every definition (both top-level and folder-scoped),
this no-ops when `def.Annotations` is empty; otherwise it adds one `BoxShape.Note` node (id
`note:{qualifiedName}`) to the same `scope` as `defNode`, plus a plain solid edge with no end
marker (id `note-edge:{qualifiedName}`, `TargetEnd = EndMarkerStyle.None`) from the note to the
definition. `BuildNoteText` concatenates every annotation's trimmed text with a blank-line
separator, so **one note box renders per annotated element**, never one box per individual
`comment`/`doc` — a definition with both a `doc` and two `comment`s still gets a single note.
`ComputeNoteBoxSize` sizes the note from the longest line and total line count using
`BoxMetrics.TitleAreaHeight(theme, hasLabel: false, hasKeyword: false)` for the (title-less) top
band plus per-line height and padding — deliberately simpler than the full `ComputeBoxSize` used
for definition boxes, since a note has no title/keyword band. The note's single
`LayoutCompartment` carries `Title = null` and one row per line of the concatenated text; no
`Label`/`Keyword` is set on the note node itself, matching conventional UML/SysML note rendering
(body text only).

This is implemented as ordinary `LayoutGraph` nodes and edges added during the same
single-pass `BuildGraph`/`CollectDefinitions` traversal that builds each definition's own box — a
deliberate, documented deviation from a literal "post-layout marker decoration" approach (the
pattern used by, e.g., `StateTransitionViewLayoutStrategy.AddInitialMarker`, which stamps a marker
onto an already-placed box after layout completes). This strategy has no equivalent
post-layout decoration phase — its entire graph, including folder containers and truncated-folder
placeholders, is built in one `BuildGraph` pass before layout ever runs — so adding the note as an
ordinary graph node/edge pair up front is the natural fit, and lets the layout engine route and
position it like any other element rather than requiring bespoke placement math.

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
- `SysmlWorkspace`, `SysmlDefinitionNode`, `SysmlFeatureNode` (Semantic subsystem) — model input;
  `SysmlWorkspace.Index.AllEdges` and `SysmlEdgeKind.Connect`/`Allocate`/`Dependency`/`Binding`
  additionally consumed by `BuildModelEdges`/`ResolveOwningBox`.
- `ExposeScopeResolver` (Layout Internal subsystem) — `ResolveExposedScope` and
  `IsInSubjectScope` supply the shared `expose`-scoping used by `BuildLayout` and
  `CollectDefinitions`.
- `FilterExpressionParser` and `FilterExpressionEvaluator` (Filtering subsystem) — parse and
  evaluate standalone `filter [<expr>];` statements over the already expose-scoped definition set.
- `LayoutWarnings` (Layout Internal subsystem) — `ForUnevaluatedFilter`,
  `ForUnevaluatedExposeBracketFilter`, and `ForDroppedRelationshipEdges` supply the warning text
  for standalone-filter fallback, (Phase 2a) failed expose bracket filters, and dropped
  `Connect`/`Allocate`/`Dependency`/`Binding` edges, respectively.
- The `LayoutTree`, `LayoutBox`, `LayoutCompartment`, `LayoutLine`, `LayoutLabel`, and `Point2D` data
  types (`DemaConsulting.Rendering`).
- `FeatureMembership` (private record) — carries the keyword, nullable type reference, simple
  name, nullable redefined-feature reference, and subsetted-feature-name list of one owned
  feature.

##### Callers

The Rendering subsystem selects `GeneralViewLayoutStrategy` when rendering a General View. No
other unit calls it directly.
