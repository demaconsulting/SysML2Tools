#### GeneralViewLayoutStrategy

![Core Layout Internal Structure](CoreLayoutInternalView.svg)

##### Purpose

`GeneralViewLayoutStrategy` implements `ILayoutStrategy` to produce a General View diagram in the
notation of the SysML v2 graphical BNF (clause 8.2.3) and of the OMG Pilot implementation. It
renders every user-defined definition (part, port, interface, requirement, action, and so on) as a
square-cornered, keyword-labeled box, and every named usage that the node policy (see *Data Model*)
selects as a rounded box of its own, placed beside its owner. By default the selected usages are the
occurrence-kind usages (part, item, occurrence, action, state, requirement, concern, and so on) and
each port that owns nested features. An exposed definition or usage is drawn as a part tree: the
usages it owns are nodes joined to it, whatever `expose` form exposed it (see `CollectDefinitions`).

A box's name line shows the declared short name and name, followed by the specialization (`:>`) of a
definition or by the typing, multiplicity, and subsetting/redefinition of a usage. Its keyword carries
the prefix words (`abstract`, `variation`, `individual`, `variant`, `ref`). A reference usage has a
dashed outline and an abstract definition an italic name. The features that stay rows (attributes,
plain references, plain ports, enumeration values, constraints) are listed in compartments with
centered italic titles. Boxes that belong to a package are grouped inside a folder-shaped container
with the package name in its tab; a package nested in another package is drawn inside its owner's
folder, and a definition that owns nested definitions is a container of those definitions.

The unit draws the relationship glyphs of the notation orthogonally between the boxes —
specialization, feature membership (filled diamond for a composite usage, hollow diamond for a
referential one), feature typing, subsetting, reference subsetting, redefinition, satisfy, verify,
import, connect, allocate, dependency, and binding — including edges whose endpoints lie in different
packages or at different nesting depths.

The whole diagram — package folders, definition-as-container boxes, definitions, and usages — is
expressed as a single `DemaConsulting.Rendering` `LayoutGraph` and placed with one
`LayoutEngine.Layout` call via the `"auto"` meta-algorithm (`AutoLayoutAlgorithm`): the root scope and
every folder classify their contents by connectivity, routing connected groups through the bundled
layered algorithm (`LayeredLayoutAlgorithm`) and packing disconnected or singleton nodes by reading
order (`ContainmentLayoutAlgorithm`). The graph flows top-to-bottom (`LayoutFlowDirection.Down`), with
the owner and the general element on top, as in the Pilot implementation; see *Direction and edge
orientation* below. The engine orders a folder's contents by the relationships that leave the folder, so
relationships leaving a folder are not forced to cross by declaration order.
All box sizing (title bands, compartment rows) remains this strategy's responsibility, since the layout
stage in `DemaConsulting.Rendering.Layout` is theme-agnostic; box title and folder-tab geometry come
from `BoxMetrics` in `DemaConsulting.Rendering.Abstractions`.

##### Data Model

`GeneralViewLayoutStrategy` has no mutable instance state: all input arrives through the `BuildLayout`
parameters, and the only instance field is the immutable node policy chosen at construction. Layout
constants (`MinBoxWidth`, `CharWidthFactor`, `CanvasMargin`) are declared as `private const` fields.

**Node policy.** `NodePolicy` (an `internal` nested class with an `internal` `UsageNodeMode` enumeration)
decides, by usage keyword only, whether a usage nested in a rendered box is a node of its own or a
compartment row of its owner. Whether a usage is a reference usage changes how its node looks (dashed
outline, hollow diamond), never whether it is a node. The strategy has two constructors: the default
one uses `NodePolicy.Pilot`, the other takes an explicit policy so tests can flip the decision with one
argument. The policy is deliberately not exposed through `RenderOptions`, the command line, or the
public API: those would need new options, help text, requirements, and user documentation, and the
Pilot default is the intended presentation (see `ROADMAP.md` for the planned public option).

- `Pilot` (the default) draws as nodes the `part`, `item`, `occurrence`, `action`, `state`, `requirement`,
  `concern`, `analysis`, `verification`, `use case`, and `case` usages, and a `port` only when it owns at
  least one named nested feature. `attribute`, plain `ref`, `enum value`, `subject`, `actor`,
  `stakeholder`, constraints, and a port without nested features stay rows.
- `AllUsages` draws every named usage with a keyword as a node, except constraints and `enum value`
  (which stay rows).
- `RowsOnly` draws no usage as a node: every usage is a row and no usage relationship edge is drawn.

An unnamed usage, a usage without a keyword, and a usage that carries an expression are never nodes,
in any mode: there is nothing to key or label a box with. A usage that has no rendered owner (its owner
is outside the view's scope, for example) stays a standalone box whatever the policy says. The policy
decides *whether* a usage is a node; *whether the owner's usages are admitted into a scoped view at
all* is the admission rule of `CollectDefinitions` (part trees), which uses the policy to pick the usages
it admits.

**Private records.**

- `DefBox` — a definition or named usage with its qualified name, simple name (the identity used for
  grouping, ordering, and edge endpoints), displayed name line `Label`, keyword including prefix words,
  specialization supertypes (definitions only), compartments, computed size, the element's visible
  `comment` annotations, the `Declaration` it represents (so `Feature` and `IsUsage` are derived from
  it), whether its documentation is visible, an optional `BoxAppearance`, and the qualified name of the
  rendered owner box for a usage node (`OwnerQualifiedName`).
- `ModelEdge` — one relationship between two boxes by qualified name: source, target, the end marker
  drawn at the target, the `EdgeKind`, and an optional midpoint label. A package-folder endpoint is
  keyed `folder:<package qualified name>`.
- `Location` (a located node and the `ScopeInfo` holding it), `ScopeInfo` (one container scope of the
  graph — the root graph, a package folder's `Children` graph, or a definition container's `Children`
  graph — with a `Parent` link and the `Owner` container node, so `LowestCommonScope` can find the scope
  of an edge), and `GraphBuild` (the result of one `BuildGraph` pass: the graph, the truncated
  containers, the `Located` map including folder keys, the folder plans, and the usage nodes placed
  beside each owner).
- `FolderPlan` (key, nearest enclosing folder, directly owned boxes, ordered child-folder keys, nesting
  level, and the hidden-element count of a truncated folder), `TruncatedContainer` (a depth-truncated
  folder's placeholder node, hidden count — retained for diagnostics only — and title-band offset,
  used by `DecorateTruncated`), and `DroppedRelationshipEdge` (kind, raw source and target, reason; one
  per `Connect`/`Allocate`/`Dependency`/`Binding` edge that could not be mapped to two distinct boxes,
  feeding `LayoutWarnings.ForDroppedRelationshipEdges`).
- `EdgeAnswer` — the semantic model's answer for one raw reference (`Known`, `Target`), built by
  `TryGetResolvedTarget`.

**Edge kinds and glyphs.** The private `EdgeKind` enumeration is `Specialization`, `Membership`,
`FeatureTyping`, `Redefinition`, `Subsetting`, `ReferenceSubsetting`, `Satisfy`, `Verify`, `Import`,
`Connect`, `Allocate`, `Dependency`, and `Binding`. The source of a `ModelEdge` is always the dependent,
specific, or owned element, and the end marker is at the target (the general, owning, or depended-upon
element). `LineStyleForKind` draws `Dependency` and `Import` dashed and every other kind solid
(including `Allocate`, which the specification draws as a solid line with an open arrowhead).

| Relationship | Source → target | Marker at target | Line | Label |
| --- | --- | --- | --- | --- |
| Specialization | definition → supertype | hollow triangle | solid | none |
| Feature membership, composite | usage → owner | filled diamond | solid | none |
| Feature membership, referential | usage → owner | hollow diamond | solid | none |
| Feature typing | usage → definition | hollow triangle with two dots | solid | none |
| Subsetting | usage → subsetted usage | hollow triangle | solid | none |
| Reference subsetting (`::>`) | usage → referenced usage | hollow triangle with four dots | solid | none |
| Redefinition | usage → redefined usage | hollow triangle with crossbar | solid | none |
| Satisfy | satisfier → requirement | open arrowhead | solid | `«satisfy»` |
| Verify | verifier → requirement | open arrowhead | solid | `«verify»` |
| Import | importing namespace → imported namespace | open arrowhead | dashed | `«import»`, `«import»*`, `«import»**` |
| Connect | box → box | none | solid | none |
| Allocate | source → target | open arrowhead | solid | `«allocate»` |
| Dependency | client → supplier | open arrowhead | dashed | the declared name, else `«depend»` |
| Binding | box → box | none | solid | `=` |
| Comment link | element → note | none | dashed | none |

A usage is referential (hollow diamond) when it is a reference usage or when its keyword is
`attribute`, `ref`, or `enum`; every other usage is composite. The distinction is visible only for a
usage that is a node: under the Pilot policy attribute and plain reference usages are rows, so it
matters for reference parts and under the `AllUsages` policy.

**Appearance.** A usage that `IsReference` gets `BoxAppearance(OutlineStyle: Dashed)`. An abstract
definition gets an italic name through the constant `AbstractDefinitionNameStyle`; an abstract
*usage* keeps the default name style (regular weight, applied to every unstyled name by
`DiagramRenderer`), because Table 4 of the graphical notation sets only the name of
an abstract definition in italics (the usage is marked by its `abstract` keyword prefix). A package
folder with content gets `BoxAppearance(FolderTitle: Tab)` and no `TitleHeight`, so the engine reserves
only the tab. An empty exposed package and a depth-truncated folder keep the name inside the body, as
the notation shows a package without visible content. Every compartment is created by
`MakeCompartment` with `TitleAlign = Center` and `TitleStyle = TextStyle.Italic`.

##### Key Methods

###### `BuildLayout(ViewContext context, RenderOptions options)`

Entry point. First resolves the view's exposed-name scope via the shared
`ExposeScopeResolver.ResolveExposedScope` (see the *ExposeScopeResolver* unit chapter), which also
narrows any bracket-filtered `expose` entry to its matched definitions and records bracket-filter
failures on `scope.Failures`. It gathers the view's conditions with
`ExposeScopeResolver.CollectViewConditions` (see *View conditions and annotation visibility*) and
calls `CollectDefinitions` to gather the user definitions and usages restricted to that scope (or
all of them when no scope applies). It returns a minimal 200×100 empty `LayoutTree` when none are
found (and no package is exposed).

Each view condition is then evaluated with `FilterExpressionEvaluator.Evaluate` over the admitted
boxes and exposed packages, narrowing them to the matched subset; an empty result returns the same
minimal canvas. A condition that fails to parse does not abort the layout: it is reported with
`LayoutWarnings.ForUnevaluatedFilter` and skipped, so the unfiltered resolved scope is rendered.

Once scope and filter narrowing are applied, `RemoveRowUsages` drops every usage that is shown as a
row of its rendered owner and records the owner of every usage that stays a node (see below); this
must run after both narrowing steps so a usage whose owner was excluded by a filter survives as a
standalone box. `FinalizeBoxes` then builds each box's compartments and size, now that the final set
of boxes is known. The surviving boxes are partitioned into three disjoint sets: boxes whose
immediate parent (the qualified-name prefix before the last `::`) is itself an admitted box are
nested definitions (`childrenByParent`); usages with a rendered owner (`usagesByOwner`) are placed
beside their owner; everything else (`packageOrRootItems`) is grouped by package with
`GroupByPackage`. This partitioning is what prevents a definition that owns nested definitions from
being rendered a second time as a duplicate sibling folder.

The relationships are resolved into qualified-name edges (plus dropped-edge diagnostics) with
`BuildModelEdges`, the single `LayoutGraph` is built with `BuildGraph`, and the whole graph is placed
with one `LayoutEngine.Layout(graph)` call (the `LayOut` helper, which first sets
`CoreOptions.Algorithm` to `"auto"`). The engine orders a folder's contents by the relationships that
leave the folder (cross-hierarchy crossing minimization, accepted only when the routed connectors cross
less), so no caller-side ordering pass is needed. A second, conditional layout then places the
definitions that nothing refers to in the
free space of the drawing (`TryTuckUnconnected`, see below); it only runs when such definitions
exist next to connected content.

The placed tree is then post-processed in a fixed sequence: `DecorateTruncated` stamps the bare `…`
label onto each truncated folder's box; `TryTuckUnconnected` replaces the tree by the tucked one when it
accepts (it decorates its own second layout the same way); `FitConnectorsToCanvas` grows and shifts the
canvas so every connector lies inside it (see below); `OrientEdges` turns back the connectors that
were declared reversed (see *Direction and edge orientation*); and `ClearEndDecorations` moves the bends
of the oriented connectors whose end decoration would reach into their last bend (see below). Finally the
returned tree's `Warnings` concatenates
`ForUnevaluatedFilter` (view conditions that failed), `ForUnevaluatedExposeBracketFilter` (bracket
filters that failed), `ForDroppedRelationshipEdges`, and finally the warnings the layout engine itself
put on the placed tree (for example the "work limit" hint that a scope too large for the full routing
effort was routed with a reduced effort), via the `LayoutTree with { Warnings = … }` record-copy idiom;
the engine's warnings are kept after the strategy's own, never replaced by them. They are those of the
tree that is finally returned: the tucked layout when `TryTuckUnconnected` accepts, the engine's first
layout otherwise. The other strategies lay out flat graphs, for which the engine reports no warnings.

###### `CollectDefinitions(workspace, scope, annotationVisible, policy)` and `CreateBox`

Visits `workspace.Declarations` and, recursively, the named usages in each declaration's `Children`
(each qualified name admitted once): a real workspace registers every nested usage in `Declarations`,
whereas a hand-built one may list nested usages only as children. A `SysmlDefinitionNode` or a named
`SysmlFeatureNode` that is not a standard-library element (`StdlibFilter.IsStdlibElement`, an
origin test against the workspace's seed-derived `StdlibNames`, never a name-prefix test) and is
within `scope` (`ExposeScopeResolver.IsInSubjectScope`) becomes a box through `CreateBox`. An unnamed
usage is excluded, since it has no stable qualified name to key a box on; an unnamed definition is
admitted under its qualified name. The admission is deliberately a superset used as the
filter-candidate pool, so a metaclass filter such as `filter @SysML::PartUsage;` has usages to narrow;
`RemoveRowUsages` later reduces it to the rendered set.

**Part trees: the depth rule.** In a scoped view, a definition or node-kind usage (`NodePolicy.IsNode`)
that is in scope through an `expose` *subject* also admits the node-kind usages it owns, and every
node-kind usage admitted that way admits the node-kind usages it owns in turn. The visit threads an
"owner is exposed" flag down the `Children` recursion, and the visit record remembers the flag per
qualified name, so a usage first reached directly from `Declarations` is reconsidered when its exposed
owner reaches it later: the result does not depend on the order of `Declarations`. The rule is:

- *Closure over ownership.* Inline nesting (`part a { part b; }`) is followed to any depth, because a
  usage that is a node needs a box in which its own parts can hang; the diagram stays bounded by the
  usages the model's author wrote under the exposed element.
- *Never over typing.* A definition that an owned usage is typed by is not admitted: the usage's typing
  edge (hollow triangle, two dots) is drawn only when the view exposes that definition itself. Pulling
  typed definitions in recursively would make `expose Vehicle;` draw the whole transitive model, and it
  is what the OMG Pilot does not do either: the Pilot's tree follows the owned members of each element
  and draws a typing relation only to an element that is already in the diagram.
- *Never definitions or rows.* A nested definition is admitted only by a recursive expose, as before;
  attributes, plain references, plain ports, enumeration values, and constraints stay rows of their
  owner (`NodePolicy.IsNode` is false for them, so they are not admitted, and `BuildCompartments` lists
  them).
- *Nothing twice.* The admission test only widens *which* qualified names are admitted; each name is
  still added to the result once (an `admitted` set), so `expose V::**;`, `expose V; expose V::**;`, and
  `expose P::**;` draw each usage once, as a node, and never also as a row (`FinalizeBoxes` omits a
  usage that is a box from its owner's compartments, and a usage that is not admitted stays a row).
- *Bracket filters select, they do not widen.* The test of "in scope through a subject" ignores
  `ExposedScope.ExplicitMembers` (it uses a copy of the scope that has only the subjects), so a
  definition selected by `expose P::**[expr];` keeps its usages as rows.
- *A typing target is not exposed.* `expose myVehicle;` also puts `Vehicle` in scope (the resolver adds
  the usage's typing target as an `ExposeSubject` with `IsTypingTarget`), so the definition is drawn as a
  box; the copy of the scope used for the owner test leaves those subjects out, so `Vehicle`'s own parts
  stay rows. Only what the view wrote in an `expose` statement owns part-tree nodes.
- *Later narrowing still applies.* The standalone view `filter` conditions and the depth limit run
  after admission. A usage node that fails a condition is removed, and `RemoveRowUsages` and
  `FinalizeBoxes` then list it as a row of its owner; the depth limit applies to part trees exactly as
  to unscoped views (see *Depth limit* below).
- *Annotations are unchanged.* A usage admitted by this rule is exposed by its owner, not as a member of
  an expose: its `doc` and `comment` are drawn only when an expose also exposes the owner's members
  (`::*`, `::**`), as `ExposeScopeResolver.IsAnnotationExposed` decides for any element.

The dynamic views (`--view-type`/`--view-target`) are unaffected: `DynamicViewSynthesizer` synthesizes a
recursive expose of the target, which already reaches every owned usage.

**No per-element cap.** The closure is bounded by the model text, so the strategy imposes no cap on
the number of nodes an exposed element draws. A view that exposes many elements, such as an overview of a
whole system, grows with the usages they own; the remedy is to expose fewer elements (the per-system and
per-subsystem views exist for that) or to apply the depth limit, and a cap would be a separate decision.

**Depth limit.** A usage node is one level below its owner, so the owner is a leaf with a trailing `…`
row while `RenderOptions.DepthLimit` does not permit that level (`PlaceDef`). For an exposed definition at
the root, limits 1 and 2 show the `…` row and limit 3 shows the part nodes; each enclosing package folder
shifts this by one, exactly as in an unscoped view.

`CreateBox` builds the not-yet-sized box:

- the keyword — `DefinitionKeyword` writes the prefix words `abstract`, `variation`, `individual`
  before the definition keyword, and `UsageKeyword` writes `abstract`, `variation`, `individual`,
  `variant`, and `ref` before the usage keyword (a plain `ref` usage is not prefixed twice); the engine
  itself adds the guillemet marks, so raw words are passed;
- the name line — `FormatNameLine(shortName, name, supertypes)` for a definition (specialization
  after the name) and `FormatUsageNameLine` for a usage (typing, a conjugated typing with a leading
  `~`, multiplicity, `ordered`/`nonunique`, then `:> subsets`, `:>> redefines`, `::> references`);
- the appearance (dashed for a reference usage, italic name for an abstract definition);
- the visible `comment` annotations and whether documentation is visible, from the `annotationVisible`
  callback (see *View conditions and annotation visibility*).

###### `RemoveRowUsages(defs, policy)`

Removes the usage boxes that the policy says are rows of their rendered owner, and records the owner
of every nested usage that stays a node. Every qualified name is a strict `parent::child` chain, so a
usage's immediate parent is recovered by stripping the last `::` segment, and every ancestor has fewer
`::` segments than its descendants. Boxes are processed shallowest-first while an `excluded` set grows,
so by the time a usage is tested its owner's own decision is final. A usage whose immediate parent is
present and not excluded is excluded when `NodePolicy.IsNode` is false (the owner's compartment shows
it as a row) and otherwise kept with `OwnerQualifiedName` set. A usage whose parent is absent (outside
the scope or removed by a filter) or was itself excluded is not touched, so a usage nested several
levels deep is never silently lost because an intermediate usage became a row: it survives as a
standalone box. A top-level usage and every definition are never removed by this rule.

###### `FinalizeBoxes(defs, theme)`, `BuildCompartments`, and `FormatFeatureRow`

`FinalizeBoxes` builds, for every box, the compartments of its owned features that are not nodes of
their own (a feature that is a node of the box is listed in `drawnAsNodes`, so a node-kind feature that
is not rendered — depth-truncated, removed by a view condition, outside the scope, or owned by an element
that a bracket filter selected — stays a row), prepends the `doc` compartment when
documentation is visible, and computes the box size with `ComputeBoxSize`.

`BuildCompartments` groups the owned features by keyword and emits the groups in a fixed order: the
canonical keywords `attribute`, `enum value`, `part`, `port` (the private `CanonicalCompartmentKeywords`
array, the stacking order of the specification's compartment list) and then every other keyword in the
order first seen; the `doc` compartment is prepended by `FinalizeBoxes`. This makes every box read the
same way regardless of member declaration order. Each title comes from `Pluralize(keyword)`, which
special-cases `subject`, `assume constraint`, `require constraint`, and `constraint` to a
guillemet-wrapped stereotype form, `ref` to `references`, and `enum value` to `enums` (the
specification's title), and otherwise appends `s`.

`FormatFeatureRow` writes a row in the specification's textual notation: an optional direction keyword
(`in`, `out`, `inout`), the name line from `FormatNameLine` (name, typing with a conjugated typing as
`~Type`, multiplicity, `ordered`/`nonunique`, subsetting, redefinition, and reference-subsetting
clauses), and the value clause (which already carries its operator). A feature with a non-empty
`ExpressionText` (constraint kinds) is written as its raw expression, prefixed `name:` when named.

###### `FormatNameLine(shortName, name, supertypes, typing, multiplicity, subsets, redefines, referenceSubsets, modifiers)`

An `internal static` helper shared by the General, Interconnection, and Browser views so a given element
reads the same way in every view. It formats the name line in the order of the graphical BNF name
compartment — `<shortName> name :> Super1, Super2 : Type [2] ordered :> subsetted :>> redefined
::> referenced` — omitting every piece that is `null` or empty. The trailing optional parameters
(`subsets`, `redefines`, `referenceSubsets`, `modifiers`) are appended after the multiplicity, so
callers that do not pass them are unaffected. Supertypes and typing are passed through exactly as
written (possibly qualified); nothing is resolved here.

###### `GroupByPackage(defs, workspace)` and `PlacementPackage`

Groups boxes by the qualified-name prefix before the last `::`, preserving first-seen order; top-level
boxes (no package prefix) become plain leaves on the root graph. For a usage, `PlacementPackage` walks
the prefix outward while it names a declared definition or usage, so a usage whose owner is not rendered
is grouped by its nearest enclosing package and never by its owner's own qualified name (which would
draw a folder for a definition). The buckets stay flat: package nesting is decided in `BuildGraph`.

###### `CollectExposedPackages(workspace, scope)` and `CollectPackageNames(workspace)`

`CollectExposedPackages` returns the non-stdlib `SysmlPackageNode` declarations that
`ExposeScopeResolver.IsInSubjectScope` admits (empty for an unscoped view); `BuildLayout` applies the
view conditions to these packages like to definitions and keeps the view non-empty when only packages
are exposed. `CollectPackageNames` returns every non-stdlib package, exposed or not, so `BuildGraph` can
tell which qualified-name prefixes are packages and draw an enclosing package's folder even when it
owns no boxes directly.

###### `BuildModelEdges(defs, workspace, isScoped, policy)`

Resolves every relationship into a flat list of qualified-name `ModelEdge`s and the list of dropped
`Connect`/`Allocate`/`Dependency`/`Binding` edges. Per box:

- a definition contributes a **specialization** edge to each resolved supertype, resolved by
  `TypeResolver.TryResolveSupertype` (the edge answer matched to the raw text, else name resolution)
  and skipped when it resolves to the box itself;
- a usage node contributes, through `AddUsageEdges` and unless the policy is `RowsOnly`, its
  **feature membership** edge to the rendered owner (filled or hollow diamond by `IsReferential`), its
  **typing** edge (`ResolveTyping`, a conjugated typing connecting to the underlying port definition),
  one **subsetting** edge per entry of `SupertypeNames` (`ResolveSubsetting`), a **reference
  subsetting** edge (`ResolveReferenceSubsetting`), and a **redefinition** edge
  (`ResolveRedefinition`). Every target is the *box* of the typing definition or related usage, never the
  owning definition. An unresolved reference, an unrendered target, and a self-reference draw nothing.
  A feature that is a row has no box and therefore no edge: an attribute's type is visible in its row.

Then `workspace.Index.AllEdges` is walked once:

- `Connect`, `Allocate`, `Dependency`, and `Binding` map each endpoint to its owning rendered box with
  `ResolveOwningBox` and are emitted only when both endpoints resolve to distinct boxes; a same-box
  result is a genuine self-loop and is dropped. Every dropped edge is recorded as a
  `DroppedRelationshipEdge`, except that an unresolved-endpoint drop is not reported in an `expose`-scoped
  view, where an endpoint outside the exposed subtree legitimately has no box; a self-loop is always
  reported.
- A `Dependency` edge is labeled by `DependencyLabel`: the declared name of the dependency when it is
  named (`dependency link from A to B;` shows `link`), else the keyword label `«depend»` — one
  expression, so showing both or only the keyword is a one-line change. The label comes from the edge
  itself: `ReferenceResolver` records the declaration's name on each `Dependency` edge it creates
  (`SysmlEdge.DeclaredName`), so no matching by client and supplier is involved and a named and an
  unnamed dependency between the same two elements each keep their own label in either declaration
  order. An edge with no declared name — an unnamed dependency, or an edge of a hand-built workspace —
  gets `«depend»`. The name replaces the
  keyword rather than accompanying it, and the keyword spelling follows the OMG Pilot.
- `Satisfy` and `Verify` map each endpoint to the *nearest rendered box* with `ResolveNearestBox`
  (the name itself, else the longest rendered `::` prefix), because the relating element is often a
  nested declaration such as an objective or a subject. The edge is added once per source, target,
  kind, and label (`AddOnce`; a `satisfy` and a `verify` of the same pair are two edges) with the label
  `«satisfy»` or `«verify»`; an unrendered endpoint or a same-box
  result draws nothing, silently, as the relationships are an overlay on content a view may scope out.
- `AddImportEdges` finds the imports by walking the `Children` of every declaration for
  `SysmlImportNode` (an import node has no qualified name of its own, so it carries no usable source in
  `AllEdges`), reading its resolved `Import` edges, with the declaration as the importing namespace. A
  package endpoint is its folder (`folder:<name>`), a definition or usage endpoint its box. An
  unrendered endpoint (including the standard library), an import of itself, and an import between a
  namespace and a namespace nested in it draw nothing. The label is `«import»`, `«import»*` for a
  namespace import, and `«import»**` for a recursive one. Edges are added with `AddOnce`, keyed by
  source, target, kind, and label, so imports of different kinds that reach the same endpoints (such as
  `import A::*;` with `import A::**;`) each keep their edge and label, while an exact repeat draws one
  edge; no visibility indicator is drawn, because the
  default (private) visibility cannot be told from an explicit one.

**Resolving a reference to a box (`TypeResolver`).** Because packages may reuse a simple name for
distinct definitions, every reference is resolved in this order: (1) the semantic model's
already-resolved target — the usage's own `Typing` edge for typing, the `Supertype` edge matched to the
raw reference by its `RawReference` (`TryGetResolvedTarget`) for specialization and subsetting, the
`Redefinition` edge for redefinition — used as resolved, since the semantic resolver already applies
KerML name resolution (member visibility, filtered imports, import scoping, and imports written at the
root of a file); otherwise (2) KerML 8.2.3.5 name resolution through the Language `NameResolver`
(created by `FilterExpressionEvaluator.CreateNameResolver`, so filtered imports honor their
conditions): owned, imported, and inherited memberships — including aliases and short names — of the
local namespace, then outward to the global namespace, later qualified-name segments by visible
(public-only) resolution. The local namespace follows KerML 8.2.3.5.2: a usage's type and a subsetted
usage resolve relative to the usage's owner; a definition's supertype relative to the definition's
owning namespace; a redefined usage against each general type of the owning type (KerML 8.2.3.5.1,
then ordinary resolution from the owning type). A reference `::>` is resolved only by step (2), because
the syntax tree carries its raw text and the semantic model produces no edge for it. (3) A reference
neither step resolves is **unresolved**: no edge is drawn and the layout emits no warning for it. A
resolved target is used only when it is a rendered box (`RenderedOrNull`); a redefinition resolves to
the usage of the nearest ancestor of the owner that declares the name, never to the owning definition.

**Why the `NameResolver` step is kept.** `SysmlWorkspace` is a public type that programmatic callers
(and many layout tests) build by hand with no `ResolvedEdges`, or with edges that carry no
`RawReference`, where step (2) is the only resolution available. Where the semantic model did produce
identified edges it is authoritative even for the references it left unmatched: step (2) is not
consulted, so the layout cannot resurrect a reference the load already reported as unresolved.

**Pairing references with edges by identity (`TryGetResolvedTarget`).** The semantic `Supertype`
edges follow the declared reference order but omit unresolved references, so they cannot be paired with
the references by position. Each edge therefore carries the reference text it was resolved from
(`SysmlEdge.RawReference`), and `TryGetResolvedTarget(declaration, kind, rawReference, out target)`
pairs by exact (ordinal) text equality, returning the target of the first edge of that kind whose
`RawReference` equals the reference. When the declaration carries at least one such edge but none
matches, it reports a known answer with no target (the reference is semantically unresolved and draws
nothing); when it carries none, the answer is not known and the caller falls back to name resolution.
No name-suffix logic exists, so an unresolved reference can never take the target another reference
resolved to — for example `:> X, A`, where `X` is unresolved and the alias `A` resolves to `P::X`,
draws one edge. The helper is shared with `GridViewLayoutStrategy`.

**Decision: no simple-name fallback (issue #91).** A reference that the KerML rules reject — a private
member, a candidate a filtered import excludes, a name reachable only through another namespace's
import — is not matched to any same-simple-name element: an unresolved reference renders as
unresolved. The diagnostic channel is the load-time `Unresolved reference` diagnostic (a Warning
by default) from `ReferenceResolver`, the single report; the layouts emit no additional warning
because it would
duplicate that message. Relationship endpoints from `workspace.Index` are already qualified, so they are
mapped only to the box with that exact qualified name or through `ResolveOwningBox` — never by simple
name.

###### `ResolveOwningBox(qualifiedName, workspace, byQualified, usageBoxes)`

Resolves the rendered box that "owns" a `Connect`/`Binding` endpoint (also used for
`Allocate`/`Dependency` endpoints that already name a box). A reference that names a rendered box
resolves directly. Otherwise the reference is a feature-chain qualified name such as
`Drone::controller::power` (always `::`-joined), and the **outermost** rendered *usage* box on the
path wins: shortest prefix first, so `connect controller.power to battery.output;` written inside
`Drone` joins the `controller` and `battery` nodes instead of resolving both sides to `Drone` and
dropping a false self-loop. Last, for an endpoint nested in a feature that is a row rather than a node,
the method walks successively shorter prefixes, longest first, looking for a `SysmlFeatureNode` whose
resolved `Typing` edge targets a rendered box; the shortest matching prefix wins. A genuine same-box
result now arises only when both endpoints are features of the same usage node (two features nested in
one part) — a rarer, legitimately self-referential shape.

###### `BuildGraph(groups, childrenByParent, usagesByOwner, modelEdges, theme, depthLimit, ...)`

Builds the single input `LayoutGraph` and returns it as a `GraphBuild`. Its remaining parameters are
`exposedPackages` (the exposed packages of a scoped view), `packageNames` (every package of the
workspace), and the optional `deferred` set. A folder's own boxes are added in declaration order: the
engine orders a container's interior by its cross-hierarchy relationships (see
`SysML2Tools-OTS-DemaRendering-ContainerOrderFollowsCrossHierarchyEdges`), so the strategy supplies no
order of its own. The optional `deferred` set (qualified names)
names leaf definitions held directly by a folder or the root that are left out of the graph; it is
used only by `TryTuckUnconnected`.

The root graph is configured once, and every `CoreOptions` property cascades to the nested folder and
container scopes unless a scope overrides it: `MergeParallelEdges = false` (every distinct model
relationship stays its own routed edge, pinned by the `ParallelEdgesIn…_AreNotMerged` tests) and
`Direction = Down` (see below).

*Folder planning (`PlanFolders`).* A group becomes a folder seed when its package key is non-empty
and, in a scoped view, the package is itself exposed: per SysML 2.1 8.3.26.3/8.3.26.4 (and KerML
8.3.2.4.4) a MembershipExpose (`expose P;`, `expose P::**;`) exposes the package P itself, while a
NamespaceExpose (`expose P::*;`) exposes only P's members. Groups with an empty key and, in a scoped
view, groups of non-exposed packages are not folders; their boxes are placed directly on the root scope
(a package that is merely an ancestor of admitted content is not drawn). Every enclosing package of a
seed that is eligible also becomes a folder, planned before the first folder nested in it, even when it
owns no boxes directly. Each folder is nested in its nearest enclosing planned folder, or on the root.
The plan records each folder's nesting level and its subtree count (its own boxes plus, for each nested
folder, one for the folder and its own subtree count), the number a truncated folder records as hidden
(diagnostics only; the rendered marker is a bare `…`). `PlanFolders` returns the ordered
materialization steps — a folder key or the index of a group placed directly on the root — so root-level
items keep their relative order.

*Materialization.* Folders are materialized in plan order, so a folder's parent scope exists first; a
folder whose parent was collapsed by the depth limit is skipped. A folder with no boxes and no nested
folders is drawn as a leaf package node (`Shape = Folder`, name inside, sized to its label) — checked
before depth truncation, so a depth limit never decorates an empty package with an ellipsis. Any other
folder becomes a container node (`Shape = Folder`, no `Keyword`, since a package is drawn with its name
only) whose appearance asks for the name in the tab (`FolderTitlePlacement.Tab`). No `TitleHeight` is
set for such a folder: the engine reserves the tab height itself, and a title height would add a body
band back. The folder's own `CoreOptions.Algorithm` is set to `AutoLayoutAlgorithm.AlgorithmId`
(see *Direction and edge orientation* for why it is not the layered algorithm). The folder's boxes
are placed with `PlaceDef` under `folder.Children` and a new `ScopeInfo`. When the depth limit forbids a
folder's contents, none of its boxes or nested folders is added: the folder becomes a single leaf node
(its `Children` graph is never touched, so the engine keeps this caller-computed placeholder size)
sized by `ComputeEllipsisSize(theme)`, showing its name and a bare `…` label, and is recorded as a
`TruncatedContainer` for later decoration.

*Depth.* The contents of a top-level folder, and the top-level or scope-promoted items placed on the
root, are at depth 1; each enclosing folder adds one level, as does each level of nested-definition
containment and each usage node below its owner. A folder is collapsed when
`depthLimit > 0 && depthLimit <= 1 + nesting level`: with a limit of 1 only the top-level folders are
shown, collapsed; with 2 each top-level folder's own boxes are shown and its nested folders collapsed;
and so on.

*Edges.* Every located box's node and holding `ScopeInfo` is recorded in the `located` map (folders
under `folder:<name>` keys too, so import edges can end at a folder). Each model edge whose endpoints
both have a node is added to the scope returned by `LowestCommonScope(source.Scope, target.Scope)` — the
innermost scope on both endpoints' parent chains — and references the (possibly deeply nested) endpoint
nodes directly. One lookup therefore handles sibling boxes in one scope, siblings of a common parent,
a nested package versus its owner, boxes several packages deep in different top-level packages (the
root), and mixtures of package and definition nesting. The layout engine routes an edge declared on an
ancestor scope between the endpoint nodes themselves, not between the containers holding them. An edge
between a container and one of its own descendants is not drawn by the engine (the containment is
already visible) and does not throw. An edge touching a depth-truncated (unrendered) box has no node
in `located` and is silently dropped.

###### Direction and edge orientation

The graph flows top-to-bottom (`CoreOptions.Direction = LayoutFlowDirection.Down`, set once on the
root), so the owner, the supertype, and the general element read on top, as in the Pilot implementation
and the SysML v2 training material, and the definition typing a usage hangs below the usage. The
bundled layered algorithm assigns layers by longest path from the sources, with no option to tighten
the layering. If every edge were declared in notation direction (usage → owner, usage → definition,
specific → general) with an upward flow, the owner and every typing definition would share one tier
above all usages, giving two rigid tiers and a drawing several thousand pixels wide for the gallery
drone model. Instead every layout edge runs from the element drawn above to the element drawn below:
owner → owned usage, general → specific, subsetted or redefined → subsetting or redefining,
requirement → satisfier, imported → importer, and usage → typing definition. Each element then hangs
directly below the top-most element that reaches it, and the owner, its usages, and the definitions form
a short three-tier tree.

An edge whose notation direction points up is therefore *declared reversed* (`IsDeclaredReversed` is
true for every kind except `FeatureTyping`, `Connect`, and `Binding`): `BuildGraph` adds it from its
drawn target to its drawn source with the end marker still on the layout target. After layout,
`OrientEdges` reverses the waypoints of every connector whose `TargetEnd` is neither `None` nor
`HollowTriangleTwoDots`, so the marker ends up at the drawn target. A connector declared in its drawn
direction carries either no marker (connect, bind, the comment link) or the typing marker, which no
reversed edge uses, so the marker identifies the connectors to reverse. The comment link is declared
element → note so the note hangs directly below its element. The graph cannot express a source-end
marker, which is why the reversal is done on the placed connectors (see `ROADMAP.md`).

For a reader and for the diagram-quality checklist, the rule that a marker sits "on the right face of
the source box and the left face of the target" is specific to a left-to-right flow. For this
top-to-bottom layout it reads as follows: the connector of a relationship toward the owner or the
general element starts at the *top* face of the lower (owned or specific) box and ends, with its
marker, at the *bottom* face of the upper box; the typing connector starts at the bottom face of the
usage and ends, with its marker, at the top face of the definition below it.

Folders are not pinned to the layered algorithm but to `"auto"`: a folder pinned to `LayeredLayoutAlgorithm`
with a vertical direction was observed to stack several unconnected nodes of one layer on top of each
other, whereas the automatic selection places connected groups with the layered algorithm and the
singletons with the containment algorithm; the regression test
`TopDown_UnconnectedDefinitionsDoNotOverlap` guards this. The automatic selection places the
unconnected-box bucket as a shelf beside or below the connected component, which can leave an empty
region in a wide drawing; no option controls the bucket's shape, so the strategy places those boxes
itself (see `TryTuckUnconnected`).

###### `TryTuckUnconnected(tree, build, rebuild, theme)`

The automatic layout packs every childless box without an edge into one block beside the connected
part of a scope. In a wide tree this block sits beside the tree while the lower part of the drawing
stays empty, so the canvas is much larger than its content (diagram-quality B4/B5: the quadcopter model
was 1959×1308 px with a 460 px empty square and 17.6 % of the canvas covered by blocks).
`TryTuckUnconnected` places those boxes in the free space instead:

1. `FindUnconnectedDefinitions` selects, per scope, the leaf boxes that no edge of the graph touches
   (a definition with a comment note, a definition that owns usages or nested definitions, and a box that
   is the end of any relationship are connected, so they are not selected), only for the root and
   package-folder scopes, and only when the scope also holds connected
   content. A scope of isolated boxes only keeps the engine's packing.
2. The graph is built again through `rebuild` with these names deferred (`BuildGraph(…, deferred)`) and
   laid out again, so the connected part keeps the engine's placement and the folder and the canvas
   shrink to it. The result is decorated like the first layout. If its canvas is not strictly smaller
   than the canvas of the first layout the method returns null.
3. For each scope, `TuckScope` takes the deferred boxes from the first layout (with their compartments
   and appearance) and calls `PlaceInFreeSpace` with the content bounds of the scope in the new layout
   (the bounds of its boxes and connector waypoints, so no folder is resized) and the obstacles: the
   boxes placed directly in the scope, every connector segment of the tree, and every connector label
   (placed by `ConnectorLabelPlacer`).
4. `PlaceInFreeSpace` rasterizes the bounds into cells of `TuckClearance` (12 px) and marks the cells that
   an obstacle grown by that clearance touches. It tries the boxes, in placement order, as an arrangement
   of one to six columns (`ArrangeInColumns`, a gap of `TuckGap` = 16 px) at every third cell, using
   prefix sums to test that the footprint is free, and picks the position that leaves the smallest empty
   square (`MeasureEmptySquares`, a dynamic-programming pass), then the fewest empty squares of any size,
   then the position nearest the bottom-right corner; ties keep the first position tried, so the result is
   deterministic. It returns null when no arrangement fits inside the bounds.
5. A scope whose boxes do not fit is dropped from the set, and the whole attempt is repeated with that
   scope's boxes in the engine's packing (the set shrinks every round, so the loop terminates). When no
   scope remains the method returns null and the caller keeps the engine's layout unchanged.

Nothing is moved outside a scope's own content bounds, so no sibling can overlap and no nested folder needs
resizing. The cost is one more engine pass plus a few thousand cell evaluations (a few milliseconds for the
quadcopter model); a layout without isolated boxes beside connected content does no extra work. The
pass compensates for the missing bucket-shape control of the engine and can be retired when the engine
offers one (see `ROADMAP.md`).

###### `FitConnectorsToCanvas(tree)`

The engine can route a connector joining two root-level folders through a channel outside the drawing
(negative coordinates) and reports the node bounds only, so a renderer would clip it. When any waypoint
lies outside the canvas, the whole tree is translated so the leftmost and topmost waypoint sits
`CanvasMargin` (8) inside the canvas, and the canvas is widened and heightened to hold the rightmost and
lowest waypoint; a tree whose connectors are all inside is returned unchanged. Labels outside the
canvas are not covered (see `ROADMAP.md`).

###### `ClearEndDecorations(tree, theme)`

The engine guarantees a clean straight final leg (the end marker plus one corner radius, see
`Theme.CleanLegMargin`) only at the *target* end of an edge. The General View declares most edges
reversed, so after `OrientEdges` the owner-end decoration (a diamond or a triangle) sits at the
engine's *source* end, where only the connector stub is reserved: the first lane out of an owner face is
a fixed 10 px from the box, which is less than the length of a diamond plus the bend radius, so the
connector would leave sideways from the decoration's tip (diagram-quality C10). `ClearEndDecorations`
runs after `OrientEdges`, when
the marker is on the last waypoint, and applies to every connector with four or more axis-aligned
waypoints whose last leg, the lane before it, and the leg before the lane form a Z shape. A connector
with a single bend (three waypoints) has no lane and is left alone.

The Z-shaped connectors are grouped by the box face their last waypoint lies on (the box whose outline
contains it, and the outward direction of the last leg). The required approach of a connector is
`NotationMetrics.RenderedAlongLineLength(TargetEnd, theme) + Theme.LineCornerRadius +
Theme.CleanLegMargin` (zero without a marker). The group's shift is the largest shortfall of its
members, and every member's last two bend points move away from the box by that one distance, so the
lane order and the lane pitch of the face are preserved (moving only the short lane would collapse it
onto the next one). A group is left unchanged, as the engine routed it, when any of these guards fail:

- the leg that starts at the other box would shrink below two corner radii plus the margin;
- a moved segment would pass through the inside of a leaf box;
- the moved set would add a conflict between connectors: a crossing of perpendicular segments, or
  parallel segments closer than 8 px (`LanePitch`) whose extents overlap or are less than 8 px apart.
  Conflicts that exist before the move are not counted.

Each face is decided once, in the order the connectors were first seen, with no iteration, so the pass is
deterministic and terminates; a tree without a violation is returned as the same instance. It is a
post-pass of this unit rather than an engine option because the engine exposes no source-end marker
and no first-lane distance (see `ROADMAP.md`); it can be retired when the engine reserves a clean leg
at the source end too.

###### `PlaceDef(def, scope, childrenByParent, usagesByOwner, located, theme, depth, depthLimit)`

Places a single box's node into `scope.Graph` via `MakeDefNode`, records its `Location`, and emits its
annotation note via `AddAnnotationNote`. `depth` is the box's own nesting depth; its nested children
sit at `depth + 1`. When the box owns nested definitions or usage nodes and `depthLimit` is positive and
does not permit that depth (`depthLimit <= depth + 1`), the children are not rendered: the box is
replaced by `WithTruncationRow(def, theme)`, which appends one untitled `LayoutCompartment` whose
single row is `…` (the specification's marker that contents are not shown) and recomputes the size,
and the box stays a leaf. No child scope is created and the hidden children receive no `located` entry,
so every edge touching one is dropped.

Otherwise the box's usage nodes (`usagesByOwner`) are placed in the **same scope as the box**, beside
it and recursively at `depth + 1`, never inside the owner's `Children`, because an edge between a
container and its own descendant is not drawn by the engine. Nested definitions (`childrenByParent`)
are placed inside the box: its `TitleHeight` is set to its own `Height`, which already includes the
title band and every compartment row, reserving that band above the nested content, and a new
`ScopeInfo` (enclosed by `scope`) is created for the child scope, so edges between nested definitions
resolve to their lowest common scope. `LayoutGraphNode.Children` and `TitleHeight` are shape-agnostic,
so a rectangle-shaped definition can be a container with no engine-side change.

###### `MakeDefNode(scope, def)`

Adds one box as a leaf node carrying its `Label`, `Keyword`, `Compartments`, and `Appearance`. The
`Shape` is `RoundedRectangle` for a usage and `Rectangle` for a definition (graphical BNF, clause
8.2.3).

###### `ComputeEllipsisSize(theme)` and `WithTruncationRow(def, theme)`

`ComputeEllipsisSize` computes the size of a truncated folder's placeholder: the minimum box width and a
height of three title lines plus margins, so the folder's name and the ellipsis drawn below it stay
clear of each other; it does not depend on how many elements were hidden. A truncated definition or
usage is sized by `WithTruncationRow` instead.

###### View conditions and annotation visibility

`BuildLayout` gathers the view's conditions with `ExposeScopeResolver.CollectViewConditions` — every
`filter` the view owns followed by those inherited from its view definition and its general view
definitions (SysML 2.1 7.26.2; 8.3.26.11 `viewCondition`/`includeAsExposed`) — and parses each with
`FilterExpressionParser.Parse`. Every exposed definition or usage must satisfy all parsed conditions
(`FilterExpressionEvaluator.Evaluate`, with abstract-syntax metaclass tests such as
`@SysML::PartUsage`, SysML 7.5.4). An owned `doc` or `comment` is visible when
`ExposeScopeResolver.IsAnnotationExposed` reports it exposed (including through an expose filter,
`expose P::**[expr];`) and it satisfies every condition for its metaclass (`Documentation` or `Comment`)
via `FilterExpressionEvaluator.EvaluateForMetaclass` — so `filter not (@SysML::Documentation);` hides
documentation while keeping comments, and a positive condition such as `filter @SysML::PartDefinition;`
hides both. These decisions are made per element, so a nested usage that is a node carries its own
documentation and comments.

###### `BuildDocCompartment(annotations, boxWidth, theme)`

Called by `FinalizeBoxes` when the element's documentation is visible: it is exposed with its owner's
members (KerML 7.2.4.2 — documentation is an owned member of its element) and satisfies every view
condition. It draws the SysML 2.1 8.2.3.4 `documentation-compartment` notation. It returns `null` when
the element has no non-empty `doc` annotation; otherwise it builds a `LayoutCompartment` titled `doc`
(centered and italic like every compartment) that `FinalizeBoxes` places first among the box's
compartments before re-running `ComputeBoxSize`. `SplitParagraphs` normalizes each annotation's raw text
(trimming lines, dropping a leading `*` continuation marker, joining consecutive lines, and splitting
paragraphs on blank lines), and `WrapText` greedily word-wraps each paragraph to the number of
characters that fit the box width computed without the doc compartment — using the same
`FontSizeBody * CharWidthFactor` estimate and `3 * LabelPadding` inset as `ComputeBoxSize`, so a wrapped
row never widens the box — but never fewer than `MinDocWrapChars` (36) characters, so a narrow box
widens to a readable column instead of wrapping into a tall sliver. A word longer than the limit is
hard-broken. Documentation is never drawn as a separate note. Only an element that is rendered as a box
reaches `FinalizeBoxes`, so the documentation of a usage that stays a compartment row under the default
`NodePolicy` (an attribute, a plain reference, or a port without nested features) is not drawn, matching
the OMG Pilot implementation.

###### `AddAnnotationNote(scope, def, defNode, theme)`

Called immediately after `MakeDefNode` for every box (definition or usage node), this does nothing when
`def.Annotations` is empty; otherwise it adds one `BoxShape.Note` node (id `note:{qualifiedName}`) to
the same `scope` as the box, plus a plain dashed edge with no end marker (id
`note-edge:{qualifiedName}`, `LineStyle.Dashed`, `TargetEnd = EndMarkerStyle.None`) from the box to the
note. `def.Annotations` holds only the element's visible `comment` annotations, so this draws the SysML
8.2.3.4 `comment-node` with its `annotation-link`; `doc` annotations are never drawn as notes.
`BuildNoteText` concatenates every comment's trimmed text with a blank-line separator, so **one note
renders per commented element**, never one per `comment`. `ComputeNoteBoxSize` sizes the note from the
longest line and the line count using `BoxMetrics.TitleAreaHeight(theme, hasLabel: false, hasKeyword:
false)` for the title-less top band, deliberately simpler than the full `ComputeBoxSize`. The note's
single `LayoutCompartment` carries `Title = null` and one row per line; no `Label` or `Keyword` is set.

The note is an ordinary `LayoutGraph` node and edge added during the same single-pass `BuildGraph`
traversal that builds each box, a deliberate deviation from the post-layout marker decoration used by
`StateTransitionViewLayoutStrategy.AddInitialMarker`: this strategy builds its entire graph in one pass
with no separate decoration phase, so adding the note up front lets the engine route and position it
like any other element. A usage that stays a compartment row has no box, so its comments are not
drawn either (the specification permits a dashed link from a note to the row; see `ROADMAP.md`).
Comment keyword, name, locale, and `about` are not shown, because
`SysmlAnnotation` carries only the kind and the text.

###### `DecorateTruncated(tree, graph, truncated, theme)`

Replaces each truncated package folder's placed box with one carrying a bare ellipsis (`…`) label,
centered horizontally in the box's now-known absolute placement and anchored at its bottom, below the
folder's name and never above the title band that each `TruncatedContainer`'s own `TitleOffset`
reserves. A recursive worker overload walks the tree scope by scope: at every compound-graph scope
the engine emits one box per node of that scope, possibly interleaved with the lines of the scope's own
routed edges, and not necessarily in node order. `LayoutBox` carries no `Id`, so each placed box is
paired with the graph node of that same scope it came from — by the shared `PairBoxesWithNodes`
enumerator — through the identity signature the
strategy itself set on the node and the engine copies onto the box: `Label`, `Keyword`, and `Shape`. Each
box takes the first not-yet-paired node with an equal signature, so the pairing does not depend on box
order; a box with no signature match falls back to the first unpaired node. The worker decorates any box
whose paired graph node is a recorded truncation and otherwise recurses into the box's own `Children`
against the graph node's own children, so a truncated folder at any depth is found.

##### Members Excluded from Diagrams

A member flagged `IsExcludedFromDiagrams` — a bare parameter, a `perform`/`exhibit`/`include`/`assert`
usage, a portion usage, a transition's `effect`, or anything in the body of an action, calculation,
constraint or case usage or definition — is modeled for name resolution only and never drawn.
`CollectDefinitions` skips it (and so everything nested in it), `BuildCompartments` skips it so it
adds no compartment row, and `NodePolicy.HasNestedContent` ignores it, so a port whose only members
are excluded stays a compartment row instead of becoming a box.

##### Error Handling

Null `context` or `options` arguments throw `ArgumentNullException`, as does a null policy passed to the
policy constructor. A workspace with no user definitions is not an error: the method returns the
minimal empty canvas. A reference that cannot be resolved, an unrendered endpoint, and a self-reference
draw no edge and raise no exception. A view condition that cannot be parsed is reported as a layout
warning and skipped. Delegated placement produces valid geometry, so no crossing warnings are emitted;
the one geometry defect of the engine that the strategy corrects itself is a connector outside the
canvas (`FitConnectorsToCanvas`), together with the owner-end decoration that is too close to its bend
(`ClearEndDecorations`).

##### Dependencies

- `ILayoutStrategy` and `ViewContext` (Rendering subsystem) — the strategy contract and view input.
- `RenderOptions` and `Theme` (`DemaConsulting.Rendering.Abstractions`) — render options and sizing
  inputs.
- `LayoutGraph`, `LayoutGraphNode`, `LayoutGraphEdge`, `CoreOptions`, `LayoutFlowDirection`,
  `BoxAppearance`, `TextStyle`, and `EndMarkerStyle` (`DemaConsulting.Rendering`) — the input graph
  model, its well-known cascading options, and the notation styling carried to the placed tree.
- `AutoLayoutAlgorithm`, `ContainmentLayoutAlgorithm`, `LayeredLayoutAlgorithm`, and `LayoutEngine`
  (`DemaConsulting.Rendering.Layout`) — the layout engine and the bundled leaf algorithms it delegates
  to per scope.
- `BoxMetrics` (`DemaConsulting.Rendering.Abstractions`) — box title-area and folder-tab geometry.
- `StdlibFilter` (Rendering Internal subsystem) — standard-library exclusion.
- `SysmlWorkspace`, `SysmlDefinitionNode`, `SysmlFeatureNode`, `SysmlPackageNode`, and
  `SysmlImportNode` (Semantic subsystem) — model input; `SysmlWorkspace.Index.AllEdges` and
  `SysmlEdgeKind.Connect`/`Allocate`/`Dependency`/`Binding`/`Satisfy`/`Verify` are consumed by
  `BuildModelEdges`, and each import node's resolved `Import` edges by `AddImportEdges`.
- `ExposeScopeResolver` (Layout Internal subsystem) — `ResolveExposedScope`, `IsInSubjectScope`, and
  the annotation-exposure checks supply the shared `expose` scoping.
- `FilterExpressionParser`, `FilterExpressionEvaluator`, and `NameResolver` (Filtering and Language
  subsystems) — parse and evaluate view conditions and resolve references by KerML name resolution.
- `LayoutWarnings` (Layout Internal subsystem) — `ForUnevaluatedFilter`,
  `ForUnevaluatedExposeBracketFilter`, and `ForDroppedRelationshipEdges` supply the warning text.
- The `LayoutTree`, `LayoutBox`, `LayoutCompartment`, `LayoutLine`, `LayoutLabel`, and `Point2D` data
  types (`DemaConsulting.Rendering`).

##### Callers

The Rendering subsystem selects `GeneralViewLayoutStrategy` when rendering a General View. No other
unit calls it directly; `FormatNameLine` and `TryGetResolvedTarget` are additionally called by the
Interconnection, Browser, and Grid view layout strategies, and the `internal static` label and
compartment helpers `UsageKeyword`, `DefinitionKeyword`, `FormatUsageNameLine`, `FormatTyping`,
`BuildCompartments` (with its optional keyword filter), `BuildDocCompartment`, `MakeCompartment`,
`ComputeBoxSize`, and `ComputeCompartmentHeight` are called by the Interconnection view layout strategy,
so an element reads the same way in every view.
