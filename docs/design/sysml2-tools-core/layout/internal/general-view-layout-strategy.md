#### GeneralViewLayoutStrategy

##### Purpose

`GeneralViewLayoutStrategy` implements `ILayoutStrategy` to produce a General View diagram. It
renders every user-defined definition (part, port, interface, requirement, action, and so on) and
— since Phase 2d (see `ROADMAP.md`'s "View `filter [<expr>];` expression evaluation" section) —
every named usage-level declaration too, as a keyword-labeled box, groups the boxes that belong to
a bare package (one with no admitted definition as its own owner) inside a folder-shaped
container when the view is unscoped — drawing a package nested in another package inside its
owner's folder — nests a definition that owns nested definitions recursively
inside that definition's own box rather than as a duplicate sibling container, lists each
definition's owned usages in compartments, and draws specialization,
membership, attribute-typing, redefinition, subsetting, connect, allocate, dependency, and
binding edges orthogonally between the boxes, including edges whose endpoints lie in different
packages or at different nesting depths. The whole diagram — package
folders, definition-as-container boxes, and plain definitions alike — is expressed as a single
`DemaConsulting.Rendering` `LayoutGraph`
and placed with one `LayoutEngine.Layout` call via the `"auto"` meta-algorithm
(`AutoLayoutAlgorithm`): the root scope classifies package folders and top-level definitions by
connectivity, routing connected groups through the bundled layered algorithm
(`LayeredLayoutAlgorithm`) and packing disconnected/singleton nodes by reading order
(`ContainmentLayoutAlgorithm`), while each container's own contents (a folder's or a
nested-owning-definition's) are always ordered by their intra-container edges with the layered
algorithm. Because the engine places a folder's contents without knowing where the folder's
relationships to the outside lead, the strategy orders each folder's definitions by the vertical
position of those relationships' far ends in a first layout and lays out once more (see
`ComputeItemOrder`), so relationships leaving a folder are not forced to cross by declaration order.
All box sizing (title bands, compartment rows) remains this strategy's
responsibility, since the layout stage in `DemaConsulting.Rendering.Layout` is theme-agnostic;
box title and folder-tab geometry come from `BoxMetrics` in `DemaConsulting.Rendering.Abstractions`.

##### Data Model

`GeneralViewLayoutStrategy` has no instance state; all input arrives through the `BuildLayout`
parameters. Layout constants (`MinBoxWidth`, `CharWidthFactor`) are declared as `private const`
fields. Private records carry intermediate data: `DefBox` (a user definition with its computed
size, keyword, supertype names, memberships, compartments, and `Annotations` — the definition's
own `comment` annotations from `SysmlNode.Annotations`, threaded through by `CollectDefinitions` so `AddAnnotationNote`
(below) can render them without a second workspace lookup), `ModelEdge` (a resolved
specialization/membership/attribute-typing/redefinition/subsetting/connect/allocate/dependency/
binding relationship expressed by qualified name, together with its target end marker, edge kind,
and an optional midpoint label), `Location` (a located definition's
graph node and the `ScopeInfo` that directly holds it, used to resolve and scope edges), `ScopeInfo`
(one container scope of the layout graph — the root graph, a package folder's `Children` graph, or
a definition-as-container's `Children` graph — with a `Parent` link to its enclosing scope and the
`Owner` container node whose `Children` graph it is (null for the root), so the
scopes form a tree whose lowest common ancestor `LowestCommonScope` can find), `GraphBuild` (the
result of one `BuildGraph` pass: the root graph, the truncated containers, the `located` map, and the
folder plans, which the order-refinement pass reads), `FolderPlan` (the
pre-materialization plan for one package folder: its key, nearest enclosing folder, directly owned
definitions, ordered child-folder keys, nesting level, and the hidden-element count reported if it is
truncated; see `BuildGraph`), and `TruncatedContainer` (a
depth-truncated package folder's or nested-definition-containing definition's leaf placeholder graph
node, hidden-item count, and vertical title-band offset, used to stamp its ellipsis label onto the
placed box after layout — see `DecorateTruncated` below). `DroppedRelationshipEdge` (a private record)
carries the short kind label, raw source/target reference, and human-readable reason for one
`Connect`/`Allocate`/`Dependency`/`Binding` edge `BuildModelEdges` could not map to two distinct
rendered boxes, feeding `LayoutWarnings.ForDroppedRelationshipEdges`. `FeatureMembership` (a private record) carries
each owned feature's keyword, raw type reference (`TypeName`, nullable — a feature may declare a
redefinition with no explicit type annotation), simple `Name`, raw
`RedefinedFeatureName` reference, the raw `SubsettedFeatureNames` list (populated verbatim
from `SysmlFeatureNode.SupertypeNames` — a feature's `subsets`/`:>` targets, not a new AST field),
and the semantic model's resolved answers for them: `ResolvedTypeName` (the feature's `Typing` edge
target), `ResolvedRedefinedFeatureName` (its `Redefinition` edge target), and `ResolvedSubsettedNames`
(one `EdgeAnswer` per `SubsettedFeatureNames` entry, by index — a private `(Known, Target)` struct
built by `TryGetResolvedTarget` from the feature's `Supertype` edge whose `RawReference` equals that
reference's text; the whole list `null` when the feature has no subsetted references), all populated
by `CollectMemberships`.
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
the same minimal empty canvas if the dedup empties `defs`. The remaining pipeline partitions the
surviving `defs` into `childrenByParent` (items whose immediate parent — the qualified-name prefix
before the last `"::"` — is itself an admitted definition, keyed by that parent's own qualified
name) and the rest (`packageOrRootItems`), grouping only the latter by package
with `GroupByPackage` — this partitioning is what prevents a definition that owns nested
definitions from being handed to `GroupByPackage` a second time under its own qualified name, which
is what used to render it twice (see `GroupByPackage` and `BuildGraph`/`PlaceDef` below). The
specialization/membership/attribute-typing/redefinition/subsetting/
connect/allocate/dependency/binding relationships are
resolved into qualified-name edges (plus any dropped-edge diagnostics) with `BuildModelEdges`, the
single input `LayoutGraph` is built
with `BuildGraph` (passed `childrenByParent`, whether the view has a resolved scope, the set of
every non-stdlib package in the workspace from `CollectPackageNames`, and no item order on the first
pass), and the
whole graph is placed with one `LayoutEngine.Layout(graph)` call (the `LayOut` helper, which first sets
`graph.Set(CoreOptions.Algorithm, AutoLayoutAlgorithm.AlgorithmId)`) — delegating root-scope
algorithm selection to `"auto"`, which classifies the root's own folders/top-level definitions by
connectivity (routing connected groups through the layered algorithm and packing
disconnected/singleton nodes via containment) rather than pinning every root-scope node to a single
fixed algorithm regardless of its own connectivity. The engine places a package folder's contents
without knowing where the folder's relationships to the outside lead, and stacks the unconnected
groups of those contents by the order they were added, so `BuildLayout` then calls
`ComputeItemOrder` on the first layout: when it reports a different item order for any folder, the
graph is built a second time with that order (`BuildGraph`'s `itemOrder`) and laid out again; when
it reports none (no folder has two or more items with external relationships whose order would
change) the first layout is kept unchanged. The refinement is a single pass — there is no iteration
to a fixed point, so termination is guaranteed — and it is documented as a compensation for the
engine's placement model that can be retired once the engine orders a container's interior by its
cross-hierarchy edges. When any package folder or nested-definition-containing definition was
depth-truncated, `DecorateTruncated` stamps each truncated container's "+N more…" ellipsis label onto
its placed box of the final layout. Finally, the returned tree's `Warnings` concatenates
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

Known limitation: a usage removed by this rule appears only as its parent's compartment row, so
its own exposed `doc` compartment and `comment` note are not drawn (comments were already lost
this way before view conditions applied to annotations). Documentation of such a nested usage is
visible when the usage is itself rendered as a box, for example when the view exposes it directly
or a view condition excludes its parent.

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
Top-level definitions (no package prefix) become plain leaves directly on the root graph. This
method is unchanged by the nested-definition-containment fix: `BuildLayout` now only ever calls it
with the subset of definitions whose immediate parent is *not* itself an admitted definition (see
`BuildLayout` above), so an admitted definition's own qualified name can never again become one of
its bucket keys — which is precisely what removes the duplicate-folder symptom, without any change
to this method's own flat, first-seen-order bucketing logic. The buckets stay flat: package nesting
is not decided here but in `BuildGraph`, which also receives the exposed-package and workspace-package
sets that nesting needs.

###### `CollectExposedPackages(workspace, scope)`

Returns the non-stdlib `SysmlPackageNode` declarations that `ExposeScopeResolver.IsInSubjectScope`
admits (an empty set for an unscoped view). `BuildLayout` applies the view's `filter` condition to
these packages exactly as to definitions (a package is a candidate element), keeps the view
non-empty when only packages are exposed, and passes the set to `BuildGraph`.

###### `CollectPackageNames(workspace)`

Returns the qualified names of every non-stdlib `SysmlPackageNode` in the workspace, exposed or not.
`BuildGraph` uses it to tell which qualified-name prefixes are packages, so an unscoped view draws an
enclosing package's folder even when that package owns no definitions directly.

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

**Resolving a reference to a box (`TypeResolver`).** Because packages may reuse a simple name for
distinct definitions, every reference is resolved in this order: (1) the semantic model's
already-resolved target — a membership's `FeatureMembership.ResolvedTypeName` (the feature's own
`Typing` edge) via `TryResolveMemberType`, the definition's own `Supertype` edge matched to the
raw reference by its `RawReference` (`TryGetResolvedTarget`) via `TryResolveSupertype`, a feature's
`ResolvedRedefinedFeatureName` (its `Redefinition` edge) via `ResolveRedefinitionOwner`, or a
feature's `ResolvedSubsettedNames` answer via `ResolveSubsettedOwner` — used as resolved,
since the semantic resolver already applies KerML name resolution (member visibility, filtered
imports, import scoping, and imports written at the root of a file), so its answer is
authoritative and the layout does not re-check it; otherwise (2) KerML 1.1 Beta 2
8.2.3.5 name resolution through the Language `NameResolver` (created by
`FilterExpressionEvaluator.CreateNameResolver`, so filtered imports honor their conditions):
owned, imported, and inherited memberships — including aliases and short names — of the local
namespace, then outward to the global namespace, later qualified-name segments by visible
(public-only) resolution. The local namespace follows KerML 8.2.3.5.2: a feature's type and a
subsetted feature resolve relative to the feature's owning definition; a definition's supertype
relative to the definition's owning namespace; a redefined feature against each general type of
the owning definition (`ResolveRedefinitionOwner`, KerML 8.2.3.5.1, then — as the semantic
resolver does — ordinary resolution from the owning type). (3) A reference neither step resolves
is **unresolved**: no edge is drawn (a type or supertype reference draws no edge, a part is not
expanded, a grid row is not marked) and the layout emits no warning for it.

**Why the `NameResolver` step is kept.** `SysmlWorkspace` is a public type that programmatic callers
(and many layout tests) build by hand with no `ResolvedEdges`, or with edges that carry no
`RawReference`, where step (2) is the only resolution available; it also resolves a declaration all of
whose references are semantically unresolved (such a declaration has no edges, so
`TryGetResolvedTarget` reports "not known") by the same KerML algorithm on the reference's own text — it
cannot see file-root imports, so it is strictly less permissive than the semantic pass, and this residual
case is documented rather than special-cased. Where the semantic model did produce identified edges, it is
authoritative even for the references it left unmatched: step (2) is not consulted, so the layout cannot
resurrect a reference the load already reported as unresolved. Step (2) is the same engine the semantic pass
uses, so it cannot draw a relationship KerML forbids; its only divergence is that it cannot see file-root
imports, which is why redefinition and subsetting prefer the semantic edge (step 1): without that, a target
reachable only through a file-root import would vanish once the simple-name fallback no longer rescued it.

**Pairing references with edges by identity (`TryGetResolvedTarget`).** The semantic `Supertype` edges
follow the declared reference order but omit unresolved references, so they cannot be paired with the
references by position. Each edge therefore carries the reference text it was resolved from
(`SysmlEdge.RawReference`, set by `ReferenceResolver`), and `TryGetResolvedTarget(declaration, kind,
rawReference, out target)` pairs by exact (ordinal) text equality: it returns the target of the first edge
of that kind whose `RawReference` equals the reference. When the declaration carries at least one edge of
that kind with a `RawReference` but none matches, it reports a known answer with no target (the reference is
semantically unresolved and draws nothing); when it carries none, the answer is not known and the caller
falls back to step (2). No name-suffix logic exists, so an unresolved reference can never take the target
another reference resolved to — for example `:> X, A`, where `X` is unresolved and the alias `A` resolves to
`P::X`, draws one edge. The helper is shared with `GridViewLayoutStrategy`;
`InterconnectionViewLayoutStrategy` does not use it (a part's typing is a single edge).

**Decision: no simple-name fallback (issue #91).** The earlier design matched a reference that
the KerML rules reject — a private member, a candidate a filtered import excludes, a name reachable
only through another namespace's import — to any same-simple-name element and warned. That drew
relationships the model does not contain (and the sibling Grid and Interconnection layouts did the
same), so it was removed from all three layouts, with no opt-in flag: an unresolved reference
renders as unresolved. The diagnostic channel is the load-time `Unresolved reference` warning from
`ReferenceResolver`, which is the single report (one message per unresolved name per file, printed by
`render`, `lint`, `query`, and `export`); the layouts emit no additional warning, because it would
duplicate that message. Only a view that merely does not draw an unresolved reference is silent at
layout level, by design. The `State`, `ActionFlow`, `Sequence`, and `Browser` layouts were audited
and have no such fallback: their name-index logic matches transition, succession, and message
endpoints only against elements of the already-selected root (local-scope naming within one
container), and the Browser view uses qualified names only.
Relationship endpoints from `workspace.Index` are already
qualified, so `ResolveOwningBox` maps them only to the box with that exact qualified name or via
the owning typed-feature walk — never by simple name.

Each `ref`-keyword feature (previously rendered with an obsolete hollow-diamond membership marker —
a bug fixed by this unit) instead contributes a **dependency** edge from the owner to the
referenced type, sharing the exact same rendering (dashed line, open chevron, `EdgeKind.Dependency`)
as the new public `Dependency` edge kind below — per current OMG SysML v2 notation, a `ref`
feature is a usage-type dependency, not a diamond-marked structural membership.

Each feature with a non-null `RedefinedFeatureName`
contributes a **redefinition** edge from the subtype to the owning definition of the redefined
feature, carrying a hollow-triangle-crossbar end marker at the owner and rendered as a solid line
via `ResolveRedefinitionOwner`: the feature's resolved `Redefinition` edge target
(`ResolvedRedefinedFeatureName`) when present, else the KerML 8.2.3.5.1 redefined-feature
resolution of the raw reference against each general type of the redefining feature's owning
type (so a bare `redefines name` finds the member the owner inherits, transitively), else ordinary
resolution of the reference from the owning type (a qualified `Owner::feature` target); the edge
ends at the resolved feature's owning definition when that definition is rendered. An unresolved
reference or a self-referential result (`owner == def.QualifiedName`) produces no edge and no
layout warning — consistent with the other edge kinds in this method.

Each entry in a feature's `SubsettedFeatureNames` (populated verbatim from
`SysmlFeatureNode.SupertypeNames` — see `FeatureMembership` above) contributes a **subsetting**
edge from the subtype to the owning definition of the subsetted (ancestor) feature, carrying a
hollow-triangle end marker (the same marker as `Specialization`, distinguished purely by the
dashed line style) at the owner, resolved by `ResolveSubsettedOwner`: the `Supertype` edge target
matched to the reference by its `RawReference` (the `ResolvedSubsettedNames` answer) when known, else
ordinary resolution of the
reference relative to the feature's owning definition — a subsetted-feature reference is "a bare
or qualified reference to a member the owner inherits" in SysML v2 semantics, as is a redefined one.
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

###### `BuildGraph(groups, childrenByParent, modelEdges, theme, depthLimit, exposedPackages, packageNames, itemOrder)`

Builds the single input `LayoutGraph` and returns it as a `GraphBuild` (graph, truncated containers,
`located` map, folder plans). The optional `itemOrder` (folder key to item qualified names, produced by
`ComputeItemOrder`) sets the order in which a folder's own definitions are placed (`OrderedItems`);
without it, or for a folder it does not name, declaration order is used, names the folder does not own
are ignored, and any item the order omits follows the named ones in declaration order, so a stale or
partial order can never drop a definition. Sets `CoreOptions.MergeParallelEdges` to `false` on the
root graph (every `CoreOptions` property cascades to the nested package-folder and definition-container
`Children` scopes unless a scope overrides it, so the setting holds in every scope where an edge is
declared; the `ParallelEdgesInNestedPackage_AreNotMerged` and
`ParallelEdgesInDefinitionContainer_AreNotMerged` tests pin it) so multiple distinct model
relationship edges that happen to share the same source and
target (for example two attributes of the same type, or a redefinition edge that coincides with
another edge between the same two definitions) are never collapsed by the bundled layered
algorithm's default parallel-edge merging — every distinct model relationship this strategy adds
remains its own visible, independently-routed edge, unlike `LayeredPlacement`'s helper (used by the
flat view strategies), which defaults to the algorithm's own merge-by-default behavior unless a
caller opts out (see `LayeredPlacement`'s design documentation). Every definition — whether a
top-level item, a bare package's item, or a definition nested inside another definition — is placed
through the recursive `PlaceDef` helper (below), which is what actually creates each definition's
node and recurses into its own `childrenByParent` entry, if any.

Folders are planned before any graph node exists (`PlanFolders`), then materialized parent-first.

*Planning.* A group becomes a folder seed when its `package` key is non-empty and, in a scoped view
(`exposedPackages` non-null), the package is itself exposed: per SysML 2.1 Beta 2 8.3.26.3/8.3.26.4
(and KerML 8.3.2.4.4) a MembershipExpose (`expose P;`, `expose P::**;`) exposes the Package P itself,
while a NamespaceExpose (`expose P::*;`, `expose P::*::**;`) exposes only P's members. Groups with an
empty key (top-level, unpackaged items) and, in a scoped view, groups of non-exposed packages are not
folders; their items are placed via `PlaceDef` directly on the root scope, exactly like top-level items
(a package that is merely an ancestor of admitted content is not drawn). Every enclosing package of a
seed that is eligible — an exposed package in a scoped view, otherwise any package from
`packageNames` — also becomes a folder, planned before the first folder nested in it, even when it
owns no definitions directly; a prefix that is not a package (for example a definition) is skipped.
Each folder is nested in its nearest enclosing planned folder, or sits on the root when there is none
(so in a scoped view a nested exposed package attaches to its nearest *exposed* ancestor, else the
root). The plan records each folder's nesting level (the number of enclosing folders) and its
subtree count — its own definitions plus, for each nested folder, one for the folder and its own
subtree count — which is the number a truncated folder reports as hidden.

*Materialization.* Folders are materialized in plan order, so a folder's parent scope always exists
first; a folder whose parent was collapsed by the depth limit is skipped (the parent's subtree count
already includes it). A folder with no definitions and no nested folders is drawn as a leaf package
node (`Shape = Folder`, name inside, sized to its label) — the SysML 8.2.3.5 `package-node` with no
compartments — instead of an empty canvas; this case is handled before depth truncation, so a depth
limit never decorates an empty package node with a `+0 more…` indicator. Any other folder becomes a
container node (`Shape = Folder`, `Keyword = "package"`, `Label` the simple package name,
`TitleHeight` set from `BoxMetrics.TitleAreaHeight` so the hierarchical engine reserves the exact
title band the renderer will draw) added to its parent scope — the root graph, or the enclosing
folder's own `Children` graph, which is how a nested package is drawn inside its owner — with its own
definitions placed via `PlaceDef` under `folder.Children` and a new `ScopeInfo` whose parent is the
enclosing scope. The folder's own `CoreOptions.Algorithm` is set to `LayeredLayoutAlgorithm.AlgorithmId`
per the established per-container-algorithm convention (the algorithm override lives on the container
node itself, while every other `CoreOptions` property would live on its `Children` graph). When the
depth limit forbids a folder's contents, its definitions and nested folders are never added as
individual boxes at all — the folder becomes a single leaf node (its `Children` graph is never
touched, so the hierarchical engine keeps this caller-computed ellipsis size rather than auto-sizing
it as a container) sized like the previous ellipsis-indicator formula from its subtree count, and
recorded as a `TruncatedContainer` for later decoration.

Package-folder contents sit at depth `1 + nesting level`: the contents of a top-level folder, and the
top-level/scoped-promoted items placed directly on the root graph, are at depth 1 — the pre-existing
convention (`depthLimit > 0 && depthLimit <= 1` collapses a top-level folder) — and each enclosing
folder adds one level, the same as a definition nested in a definition. A folder is therefore
collapsed when `depthLimit > 0 && depthLimit <= 1 + nesting level`: with a limit of 1 only the
top-level folders are shown, collapsed; with 2 each top-level folder's own definitions are shown and
its nested folders are collapsed; and so on. Every `PlaceDef` call `BuildGraph` makes for a folder's
items passes that folder's content depth as `def`'s own starting depth (and the root depth, 1, for
root-scope items), so `PlaceDef`'s own nested-definition-containment recursion (see below) goes one
depth deeper per level from this same baseline — a definition's immediate nested child sits one
level deeper than the definition, its grandchild two, and so on — regardless of whether that
definition sits directly on the root, inside a (possibly nested) folder, or is itself already nested
inside another definition. This is what fixes the reviewer-confirmed defect where
`RenderOptions.DepthLimit` only capped package-folder-contents nesting and had no effect at all on
nested-definition containment.

Every located definition's node and the `ScopeInfo` that directly holds it is recorded in a `located`
map so model edges can be scoped by lowest common ancestor. Each edge is added to the scope returned
by `LowestCommonScope(source.Scope, target.Scope)` — the innermost scope on both endpoints' parent
chains — and references the (possibly deeply nested) endpoint nodes directly. Because the scope
chain contains both package folders and definition-as-container scopes, one lookup handles sibling
definitions in one scope, two siblings of a common parent (their parent package or definition), a
nested package versus its owner (the owner's scope), definitions several packages deep in different
top-level packages (the root), and mixtures of package and definition nesting. The layout engine
(`DemaConsulting.Rendering.Layout` 0.3.0-beta.2 or later) routes an edge declared on an ancestor
scope between the endpoint nodes themselves, not between the containers holding them, so
relationships across packages are drawn between the actual definition boxes; earlier engine versions
dropped such edges or anchored them to package borders. An edge between a container and one of its
own descendants has the container's own enclosing scope as its lowest common ancestor; the engine
does not draw it (the containment is already visible) and does not throw. An edge touching a
depth-truncated (unrendered) definition has no node in the `located` map to reference and is silently
dropped, exactly as before.

###### `PlanFolders(groups, exposedPackages, packageNames)`

Pure planning step used by `BuildGraph` (see above): decides which packages become folders, their
nesting and order (an enclosing folder always precedes everything nested in it), the nesting level,
and the subtree count, and returns the ordered materialization steps — each either a folder key or the
index of a group whose items are placed directly on the root scope — so root-level items keep their
original relative order.

###### `LowestCommonScope(a, b)`

Returns the innermost `ScopeInfo` that encloses both arguments (a scope encloses itself) by collecting
the ancestors of `a` (by reference identity) and walking `b` upward until one of them is found; the
root scope is on both chains, so a result always exists.

###### `PlaceDef(def, scope, childrenByParent, located, theme, depth, depthLimit, truncated)`

Places a single definition's node into `scope.Graph` via `MakeDefNode`, records its `Location` in
`located` (the node and the `scope` that directly holds it, for edge scoping), and emits its
annotation note via `AddAnnotationNote` — exactly the three steps `BuildGraph` used to perform
inline for every leaf definition, now factored into one recursive helper. `depth` is `def`'s own
nesting depth, using the same numbering `BuildGraph` uses for package-folder contents (a
top-level definition is depth 1; a folder's definition is its folder's content depth, per `BuildGraph`
above); `def`'s own immediate nested children (if any) sit one depth deeper, at `depth + 1`.

When `def` owns nested definitions (a `childrenByParent` entry keyed by `def.QualifiedName`), the
definition's own node's `TitleHeight` is set to `def.Height` (which already includes the title band
and every compartment row for `def`'s own owned usages, from `ComputeBoxSize`) regardless of what
happens next, reserving exactly that band above where its nested content begins. Then, exactly one
of two things happens to that nested content:

- If `depthLimit` is positive and does not permit the children's depth (`depthLimit <= depth + 1`),
  the children are **not** recursed into or rendered as individual boxes at all — mirroring
  `BuildGraph`'s `truncateFolderContents` package-folder-contents truncation one nesting level
  deeper. A single ellipsis placeholder leaf node (sized by the same `ComputeEllipsisSize` helper
  `BuildGraph`'s folder truncation uses) is added to `def`'s own `node.Children` scope instead, and
  recorded in `truncated` with a `TitleOffset` of `0` (unlike a truncated package folder's
  placeholder, this ellipsis node is an ordinary child placed inside `def`'s own already-title
  reserved `Children` scope, not a standalone box carrying its own title/keyword band, so no further
  offset is needed once its absolute position is known). No child `ScopeInfo` is created for `def` in
  this branch (nothing beyond the single ellipsis leaf is ever placed in its scope, so no
  intra-container edge could resolve there), and none of the hidden children
  receive a `located` entry, so every edge touching one is dropped exactly like a depth-truncated
  package folder's contents.
- Otherwise, a new `ScopeInfo` (enclosed by `scope`, graph `node.Children`) is created for `def`, so
  edges between `def`'s own nested children — or between one of them and a node elsewhere — resolve to
  their lowest common scope rather than always the root, and each child is placed by a recursive call
  to `PlaceDef` with that new scope and `depth + 1` as the child's
  own depth — resolving arbitrarily deep nesting (a definition owning a definition owning a
  definition, and so on) through repeated single-level recursion, one call per level, rather than a
  multi-hop ancestor walk, with the depth-limit check re-applied at every level.

`LayoutGraphNode.Children` and `TitleHeight` are shape-agnostic in `DemaConsulting.Rendering.Layout`
(not reserved for `BoxShape.Folder`), so a `Rectangle`-shaped definition box can safely become a
container this way with no engine-side change: the `HierarchicalLayoutAlgorithm`'s cascading sizing
pass grows any container whose placed content needs more room than its initial `Width`/`Height`,
exactly as an (initially `0, 0`-sized) package folder already relied on.

###### `ComputeEllipsisSize(hiddenCount, theme)`

Computes the width/height of a "+N more…" ellipsis placeholder leaf from its hidden-item count.
Factored out of the pre-existing package-folder-contents-truncation sizing formula so both
`BuildGraph`'s folder truncation and `PlaceDef`'s nested-definition-containment truncation size
their placeholder identically.

###### `MakeDefNode(scope, def)`

Adds one definition as a leaf node to the given scope (the root graph or a folder's `Children`),
carrying its `Label`, `Shape = Rectangle`, `Keyword`, and `Compartments`.

###### View conditions and annotation visibility

`BuildLayout` gathers the view's conditions with `ExposeScopeResolver.CollectViewConditions` —
every `filter` the view owns followed by those inherited from its view definition and its general
view definitions (SysML 2.1 Beta 2 7.26.2; 8.3.26.11 `viewCondition`/`includeAsExposed`) — and
parses each with `FilterExpressionParser.Parse`; a condition that does not parse is reported with
`LayoutWarnings.ForUnevaluatedFilter` and skipped. Every exposed definition or usage must satisfy
all parsed conditions (`FilterExpressionEvaluator.Evaluate`, with abstract-syntax metaclass tests
such as `@SysML::PartUsage`, SysML 7.5.4). An owned `doc` or `comment` is visible when
`ExposeScopeResolver.IsAnnotationExposed` reports it exposed (including through an expose filter,
`expose P::**[expr];`) and it satisfies every condition for its metaclass (`Documentation` or
`Comment`) via `FilterExpressionEvaluator.EvaluateForMetaclass` — so
`filter not @SysML::Documentation;` hides documentation while keeping comments, and a positive
condition such as `filter @SysML::PartDefinition;` hides both.

###### `BuildDocCompartment(annotations, boxWidth, theme)`

Called by `CollectDefinitions` when the element's documentation is visible: it is exposed with
its owner's members (`ExposeScopeResolver.IsAnnotationExposed`, KerML 7.2.4.2 — documentation is
an owned member of its element) and satisfies every view condition
(`FilterExpressionEvaluator.EvaluateForMetaclass(…, "Documentation", …)`). It draws the SysML 2.1
Beta 2 8.2.3.4 `documentation-compartment` notation. It returns `null` when the element has no
non-empty `doc` annotation; otherwise it
builds a `LayoutCompartment` titled `doc` that `CollectDefinitions` places first among the box's
compartments before re-running `ComputeBoxSize`. `SplitParagraphs` normalizes each annotation's
raw text (trimming lines, dropping a leading `*` continuation marker, joining consecutive lines,
and splitting paragraphs on blank lines), and `WrapText` greedily word-wraps each paragraph to the
number of characters that fit the box width computed without the doc compartment — using the same
`FontSizeBody * CharWidthFactor` estimate and `3 * LabelPadding` inset as `ComputeBoxSize`, so a
wrapped row never widens the box — but never fewer than `MinDocWrapChars` (36) characters, so a
narrow box widens to a readable column instead of wrapping into a tall sliver. A word longer than
the limit is hard-broken. Documentation is never drawn as a separate note.

###### `AddAnnotationNote(scope, def, defNode, theme)`

Called immediately after `MakeDefNode` for every definition (both top-level and folder-scoped),
this no-ops when `def.Annotations` is empty; otherwise it adds one `BoxShape.Note` node (id
`note:{qualifiedName}`) to the same `scope` as `defNode`, plus a plain solid edge with no end
marker (id `note-edge:{qualifiedName}`, `TargetEnd = EndMarkerStyle.None`) from the note to the
definition. `def.Annotations` holds only the element's `comment` annotations that are visible —
exposed with the owner's members and satisfying every view condition for metaclass `Comment` —
so this draws the SysML 8.2.3.4 `comment-node` with its `annotation-link`; `doc` (Documentation)
annotations are never drawn as notes (see `BuildDocCompartment` below). `BuildNoteText`
concatenates every comment's trimmed text with a blank-line separator, so **one note box renders
per commented element**, never one box per individual `comment` — a definition with two
`comment`s still gets a single note.
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
post-layout decoration phase — its entire graph, including folder containers and truncated
placeholders, is built in one `BuildGraph` pass before layout ever runs — so adding the note as an
ordinary graph node/edge pair up front is the natural fit, and lets the layout engine route and
position it like any other element rather than requiring bespoke placement math.

###### `ComputeItemOrder(tree, build, edges)`

Decides, from the first-pass layout, how each package folder's definitions should be ordered so that
relationships leaving the folder do not have to cross one another. The layered engine lays out a
container's contents independently of the container's external edges (the engine's
`SeparateChildren` placement model) and orders the unconnected groups of those contents by their
lowest insertion index, so declaration order alone can force a crossing: in the gallery's
`VehiclePackagesGeneralView`, `Hybrid` is declared after `Chassis` and was therefore stacked below
it, although `Hybrid`'s relationship leads to the nearer `Powertrain` folder and `Chassis`'s leads
past that folder to `Car`. This unit applies the standard barycenter heuristic across the container
boundary, which the engine leaves to the caller:

1. `CollectCenters` records the absolute vertical center of every placed box against the graph node it
   was placed from (tree coordinates are absolute, so centers in different scopes compare directly).
2. For every folder plan with at least two items, `ExternalPartnerKey` computes each item's key: the
   mean center of the boxes its *external* relationships lead to. For each model edge touching the
   item (`PartnerOf`), `ExternalAnchor` takes the lowest common scope of the item's scope and the
   partner's scope; when that is the item's own (holding) scope the relationship stays inside the
   folder and contributes nothing, whether the partner is a sibling definition or sits inside a nested
   package or definition of the same folder: such an edge is declared in the folder's scope, so the
   engine sees it when it orders that scope's contents (a nested-package partner does not force a
   crossing, which the `ExternalEdges_DefinitionToSiblingNestedPackages_OrdersByPartnerFolder` test
   pins). Otherwise the partner is walked up through `ScopeInfo.Owner`/`Parent` until the scope
   reached is the common scope, and the node reached is the *anchor* — the direct member of the common
   scope that contains the partner (the partner itself when it is a direct member). Anchors are
   container boxes at the common scope, so the key does not depend on the partner folder's interior
   order, which changes between passes. A partner without a node (hidden by the depth limit) adds
   nothing.
3. `OrderByPartnerPosition` sorts the items that have a key by the rounded key (ties keep declaration
   order) into the slots those items occupied; items without a key stay where they are. It returns
   null when fewer than two items have a key or the order is unchanged.

The result maps each folder whose order changes to its wanted item order; an empty result means the
first layout stands. Scope limits, deliberately not addressed: only package-folder items are
reordered (promoted root-level items and the children of a definition-as-container keep declaration
order); the key is vertical only, matching the default left-to-right flow every folder uses; and a
single refinement pass is made. The heuristic is a crossing-reduction mitigation, not a guarantee. It
also relies on the engine's insertion-order behavior, which is pinned by the OTS requirement
`SysML2Tools-OTS-DemaRendering-ComponentOrderFollowsInsertion`.

###### `DecorateTruncated(tree, graph, truncated, theme)`

Replaces each truncated container's placed box with one carrying its "+N more…" ellipsis label,
positioned within the box's now-known absolute placement, using each `TruncatedContainer`'s own
`TitleOffset` to position the label below any title/keyword band the placeholder box itself
carries. Delegates to a recursive worker overload that walks the tree scope-by-scope: at every
compound-graph scope (the root graph, or any container node's own `Children` graph) the engine emits
one box per node of that scope, possibly interleaved with the lines of the scope's own routed
edges, and not necessarily in node order. `LayoutBox` carries no `Id`, so each placed box is paired
with the graph node of that same scope it came from — by the shared `PairBoxesWithNodes` enumerator,
which `ComputeItemOrder`'s `CollectCenters` also uses so both agree on which node a box is — through
the identity signature the strategy itself set
on the node and the engine copies verbatim onto the box — `Label`, `Keyword`, and `Shape`: each box
takes the first not-yet-paired node with an equal signature, so the pairing does not depend on box
order. Nodes with an equal signature (only duplicated, never-truncated nodes can have one; a
truncated folder's simple name is unique among its siblings, and a truncated definition's ellipsis
placeholder is the only child of its scope) pair in order, and a box with no signature match falls
back to the first unpaired node. The worker decorates any box whose paired graph node is a recorded
truncation, and otherwise — when that graph node itself owns children and the placed box has its own
nested children — recurses one level deeper into that box's own `Children` against the graph node's
own `Children.Nodes`. This is what lets a truncated nested-definition-containment placeholder or a
truncated nested package folder (potentially several levels deep — inside a folder, inside another
definition, or both) be found and decorated exactly like a root-level truncated package folder, not
just the ones directly on the root graph.

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
- `AutoLayoutAlgorithm`, `ContainmentLayoutAlgorithm`, `LayeredLayoutAlgorithm`, and `LayoutEngine`
  (`DemaConsulting.Rendering.Layout`) — the layout engine and the bundled leaf algorithms it
  delegates to per scope.
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
  name, nullable redefined-feature reference, subsetted-feature-name list, and the resolved
  typing, redefinition, and identity-matched subsetting answers of one owned feature.

##### Callers

The Rendering subsystem selects `GeneralViewLayoutStrategy` when rendering a General View. No
other unit calls it directly.
