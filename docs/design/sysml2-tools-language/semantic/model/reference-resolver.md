#### ReferenceResolver

##### Overview

`ReferenceResolver` performs four analyses over the loaded files:

1. **Import graph cycle detection** — builds a directed graph of import relationships between
   files and uses depth-first search to detect cycles.
2. **Metadata-type resolution (pass 0)** — resolves each `SysmlMetadataNode.TypeReference` into a
   `MetadataType` edge first, with an import-filter-less `NameResolver`, so that a filtered import
   (`import P::*[@X];`) evaluated in pass 1 can read the resolved metadata type of any candidate,
   wherever the candidate is declared (see "Filtered Imports" below).
3. **Reference resolution (pass 1)** — checks each `SupertypeName`, `SysmlFeatureNode.FeatureTyping`,
   `SysmlFeatureNode.RedefinedFeatureName`, `ImportedName`, `VerifiedRequirementNames` entry,
   `SysmlSatisfyNode` subject/requirement, `SysmlConnectionNode`
   (`ConnectionKeyword == "allocation"`) endpoint, `SysmlDependencyNode` from/to name lists, and
   `SysmlViewNode` expose entries and view definition in all AST nodes by KerML 8.2.3.5 name
   resolution (`NameResolver`), emitting an unresolved-reference diagnostic for any name not found
   (a Warning by default, an Error under `strictUnresolved`; see "Severity and Origin" below) and recording a
   `SysmlEdge` for any name (or pair of names) that resolves. The same pass resolves each node's
   `ReferenceOccurrences` (see "Expression References" below), reporting without recording an edge;
   a view's `render` target that names a SysML2Tools extension selector gets a portability Warning
   instead (see "Tool Render Selectors (Portability Warning)" below).
4. **Feature-chain resolution (pass 2)** — after pass 1 has completed for every file root,
   resolves dotted feature chains (e.g. `engine.fuelPort`) referenced by `SysmlConnectionNode`
   (`ConnectionKeyword == "connection"`, `"message"`, or `"binding"`) endpoints and
   `SysmlTransitionNode` `Source`/`Target` into `Connect`/`Binding`/`Transition`-kind edges.

##### Import Graph

`BuildImportGraph` iterates all file roots, collecting `SysmlImportNode.ImportedNamespace`
values into a `HashSet<string>` per file. The result is a `Dictionary<string, HashSet<string>>`
from file path to imported names.

`DetectCircularImports` runs a DFS over the import graph keys. A cycle is detected when a
node in the current DFS stack is encountered again. This diagnostic is always a Warning,
whatever the unresolved-reference severity. The Warning message names the namespace
that imports and the imported namespace that completes the cycle. `BuildImportGraph` also records,
for each `(importing namespace, imported namespace)` pair, the file and source position of the
first import statement that writes it (the `ReferencePositions` entry of the import node, else
its declaration position), so the Warning is located at that import statement and names the file
that contains it.

##### Unresolved-Reference Positions

An unresolved-reference diagnostic (Warning by default; see "Severity and Origin") is located at the
position of the reference text as written, not
at `(0,0)`. `ResolveOrWarn`/`ReportUnresolved` (and the feature-chain and transition-source
helpers) take the *owning node* of the reference and look the reference text up in the owner's
`SysmlNode.ReferencePositions` (see the `AstBuilder` design): the first token of the reference.
When the owner has no entry for the text (a synthesized name that matches no token) the diagnostic
falls back to the owner's declaration position (`SourceLine`/`SourceColumn`), and to `(0,0)` only
for a node with no position at all (a hand-built node). The reference kinds that get an exact
token position are supertype, subsetting, typing, redefinition, import, `expose`, view
definition, connector (`connect`/`bind`/message) endpoints, transition source and target,
`satisfy`, `verify`, allocation endpoints, `dependency`, and metadata-type references; the
owner-position fallback applies only to texts with no token. Deduplication is unchanged: the
diagnostic is emitted once per distinct name per file, at the first owner that references it, so the
count of diagnostics is identical to before positions were added. References inside expressions
(`ReferenceOccurrences`, see "Expression References") carry their own exact token position and are
reported per occurrence rather than once per name. `NameResolver` emits no
diagnostics and is unchanged.

##### Reference Resolution

Every reference is resolved by `NameResolver` in its internal *resolving* mode (see
`name-resolver.md`) through `TryResolve(name, scope, rootImports, out resolvedName)`, which calls
`NameResolver.Resolve(name, scope, rootImports)` and outputs the exact qualified name that matched.
`scope` is the **local namespace** of the reference: the qualified name of the nearest enclosing
named package, definition, or usage (`ChildScope`; null at the file root) — for a node's own
references that is its owning namespace, which is the local namespace KerML 8.2.3.5.2 specifies
for a Specialization, FeatureTyping, Subsetting, Import (the import's owning namespace), metadata
typing, and the other reference kinds this unit resolves. Resolution then follows KerML 8.2.3.5:

- **Local resolution** in the scope namespace: owned memberships (matching a member's name, short
  name, or an alias, followed to its target), the memberships brought in by the namespace's *own*
  imports, and — for a type — the non-private memberships it inherits from its general types.
- **Full resolution** outward through the owning namespaces, then the imports written at the root
  of the referencing file, then the global namespace (so a local `Base` is not shadowed by the
  standard library's top-level `Base` package).
- **Visible resolution** for the later segments of a qualified name: only public memberships
  (and, through `import all`, the private ones the import names) are reachable.

`ResolveNode` traverses each AST node's `SupertypeNames`, the node's `FeatureTyping` and
`RedefinedFeatureName` (both only when the node is a `SysmlFeatureNode`), and `ImportedNames`
uniformly. For each name that resolves, a `SysmlEdge` is appended to a per-node list, tagged
with `SysmlEdgeKind.Supertype`, `SysmlEdgeKind.Typing`, `SysmlEdgeKind.Redefinition`, or
`SysmlEdgeKind.Import` respectively; `Source` is the current node's
`QualifiedName` (`null` for anonymous nodes such as import statements) and `Target` is
`resolvedName`. Each of these single-reference edges (and the `Verify`, `Expose`, and `MetadataType`
edges, and a view usage's `Typing` edge) is also tagged with the reference text as written
(`SysmlEdge.RawReference`), which is the edge's identity: the supertype edge list omits
unresolved references, so a consumer pairs a raw reference with its edge by that text rather than
by position or by name suffix. For an `Import` edge the text is `SysmlImportNode.RawReferenceText`
(wildcard/recursive markers kept — `P`, `P::*`, `P::**` — and the bracket filter excluded), falling
back to the `ImportedNames` entry when it is `null` (hand-built nodes, legacy serialized data);
the name that is resolved, and reported in the unresolved-reference diagnostic, remains the stripped
`ImportedNames` entry. The two-ended kinds (`Satisfy`, `Allocate`, `Dependency`, `Connect`,
`Binding`, `Transition`) leave it `null`. Any non-empty per-node edge list is attached to `node.ResolvedEdges` and
appended to the aggregate edge list returned by `ResolveAll`.

For each name that does not resolve (and is not already reported in this file), an
`Unresolved reference: '<name>'` diagnostic is emitted at the resolver's unresolved severity (see
"Severity and Origin"). The `resolvedInFile` set prevents duplicate diagnostics for the same name
within a file; `ResolveOrWarn` may resolve an already-reported name again (a minor,
correctness-preserving redundancy — see Error Handling), but no duplicate diagnostic is
produced. A reference that KerML name resolution rejects — a private member reached through a
qualified name, a candidate a filtered import excludes, a name reachable only through another
namespace's import — is reported exactly like any other unresolved reference (the same
`Unresolved reference: '<name>'` diagnostic, with no edge), so the layouts and the `lint` command
need no new diagnostic channel.

A `SysmlViewNode`'s `ViewDefinitionName` (`view V : Def`) is resolved the same way into a
`SysmlEdgeKind.Typing` edge, or reported with the same unresolved-reference diagnostic; callers use
the edge to find the view definition whose view conditions and rendering the usage inherits
(SysML 2.1 Beta 2 7.26.2).

`ResolveAll` returns a `SemanticIndex` built from the aggregate edge list once all file roots
have been traversed.

##### Member Visibility

A qualified name's later segments are resolved by *visible* resolution, so a member whose
`SysmlNode.Visibility` is `Private` (or `Protected`) is not reachable as `P::Hidden` from another
package: no `Typing`/`Supertype`/... edge is recorded and the unresolved-reference diagnostic is
emitted. The same rule applies on the short-name and alias paths (`P::<k>` of a private member,
a private alias) and to `expose P::Hidden;`. A protected member is visible to a specialization of
its owner (inheritance admits everything but private members), `import all P::*;` brings in the
private members as well, and an unqualified reference to a private member from inside its own
package resolves normally (local resolution sees every owned membership).

##### Filtered Imports

A filtered import (`import P::*[@X];` / `import P::**[@X];`) admits a candidate only if its filter
expression accepts it. `ReferenceResolver` builds the `NameResolver` with
`FilterExpressionEvaluator.CreateImportFilter(symbolTable.Symbols, stdlibNames)`, which evaluates
the import's `BracketFilterExpressionText` per candidate (the parsed expression is cached per
import node). Because the filter reads each candidate's `MetadataType` edge, **pass 0** resolves
every metadata annotation first — otherwise a candidate declared later (or in a later file) would
be tested against a still-unresolved annotation and wrongly rejected. Pass 0 uses a resolver
without import filters: a metadata type reachable *only* through a filtered import is resolved as
if the filter admitted it (this avoids a circular dependency between a filter and the metadata it
tests). A recursive filtered import skips a rejected candidate and keeps searching, so a deeper
match can still be found. When no import in the loaded files carries a filter, pass 0's resolver
(with its warm cache) is simply reused for pass 1.

##### Import Scoping

An import applies only to the namespace that declares it and the namespaces nested in it: a
`NameResolver` lookup consults the imports among a namespace's own children, and full resolution
visits only the namespaces enclosing the reference. `PkgB`'s `part q : X;` therefore no longer
resolves through `PkgA`'s `import A::*;`. The imports written at the root of a source file
(outside any package — `private import …::*;` before the first package) apply to every
declaration in that file and to no other file: `ResolveAll` collects each file root's direct
`SysmlImportNode` children and registers them (`NameResolver.RegisterRootImports`) against the
qualified name of every declaration in that file (keyed by declaration, not top-level name, so a
package of the same name split across two files keeps each file's imports separate), and they are
consulted after the owning namespaces and before the global namespace.

##### Redefinition and Subsetting

A `redefines`/`:>>` target is resolved by `NameResolver.ResolveRedefinition(name, owningType,
redefiningFeature, rootImports)`: per KerML 8.2.3.5.1 the basic resolution is repeated with each
general type of the redefining feature's owning type as the local namespace, in turn. If that
yields nothing it falls back — a documented, deliberately lenient deviation — to ordinary
resolution from the owning type, so a qualified target such as `:>> ISQ::mass` still resolves in a
type with no resolvable general types (it remains subject to member visibility and import
scoping). The redefining feature itself is never an acceptable target: a bare `:>> mass;` inside
`part x` creates `x::mass`, which ordinary local resolution would otherwise find, giving a
meaningless self-edge. A usage-level `subsets`/`:>` supertype and a definition-level supertype
use plain `Resolve(name, scope)`, whose local resolution in the owning namespace already includes
the memberships the owner inherits.

Because `NameResolver`'s resolving mode derives every type's general types from the type's *raw
references* (supertype names, typing, view definition, redefined feature) rather than from
`ResolvedEdges`, an inherited member resolves in this single pass wherever its ancestor is
declared — later in the same file, or in another file. The earlier deferred "pending bare
redefinition" pass is therefore gone.

##### Out of Scope (Known Limitations)

- **Package-level `filter` statements** (a filter that applies to all of a package's imports) are
  not captured by `AstBuilder` — it records only a view's `elementFilterMember` and an import's own
  bracket filter — so they are not honored by the resolver; supporting them needs grammar/AST
  changes.
- **Implicit specialization** (for example a feature's implicit specialization of `Base::things`)
  is not modeled, so a redefinition of a member that exists only on an implicit general type is
  reported as unresolved; before this unit used KerML name resolution such a redefinition silently
  resolved to itself.
- The layouts' `NameResolver` (public mode) does not see file-root imports; they rely on the
  semantic edges this unit produces, which do.

##### Feature-Chain Resolution

After pass 1 (`ResolveNode`) has run to completion over **every** file root, `ResolveAll` runs a
second traversal, `ResolveFeatureChains`, over every file root again. This ordering is required
because a chain walk depends on `Typing`/`Supertype` edges attached to `node.ResolvedEdges` by
pass 1 — and a chain in file A may reference a type declared (and typed/specialized) in file B, or
reference a node that pass 1 has not yet visited within the same file (a forward reference in
document order). Running feature-chain resolution as a strictly later, whole-workspace second pass
guarantees every `Typing`/`Supertype` edge needed by the walk already exists.

`ResolveFeatureChains` computes the local namespace of each node with the same `ChildScope`
function as `ResolveNode` (the nearest enclosing named package, definition, or usage), so that
segment-0 resolution scope cannot silently diverge between the two passes. For each
`SysmlConnectionNode` with `ConnectionKeyword` `"connection"`, `"message"`, or `"binding"` (the
`"allocation"` variant is excluded — it keeps its existing, unit-3, single-segment-only
behavior), and for each
`SysmlTransitionNode`, both sides (`EndpointA`/`EndpointB` or `Source`/`Target`) are resolved via
`TryResolveFeatureChain`, and a `Connect`/`Binding`/`Transition` edge is emitted only when **both**
sides
resolve — mirroring the existing Satisfy/Allocate both-sides-must-resolve contract. New edges are
appended to `node.ResolvedEdges` (pass-1 edges, if any, are preserved) and to the aggregate edge
list.

For a `SysmlTransitionNode`, both the `Source` and the `Target` side are resolved via
`ResolveTransitionEnd` rather than the plain `TryResolveFeatureChain` call: it tries
`TryResolveFeatureChain` first, and only when that fails, falls back to
`TryResolveInheritedActionMember` (see "Inherited Pseudostate Feature Resolution" below) before
the pair is given up on as unresolved. The fallback covers an inherited `start`/`done` on either
end — `first start then off;` (source) and `then done;` / `transition first x then done;`
(target) — and is not given to connection or binding endpoints or any other reference kind.

`TryResolveFeatureChain(chain, scope, rootImports, out resolvedName)` splits the raw
reference text on `.`. Segment 0 is resolved via `TryResolve` (so it
participates in the same KerML scope/import resolution as any other single-name reference); a
single-segment "chain" is handled by the remaining-segment loop simply never executing. Each
subsequent segment is resolved relative to the previous segment's node (looked up via
`SymbolTable.Lookup`) using `FindFeatureMember`. Two parallel accumulators are tracked across the
loop: `current` (the real declared node's qualified name, which continues to drive the *next*
segment's `SymbolTable.Lookup` — this part of the walk is unchanged) and `instancePath` (the value
ultimately returned as `resolvedName`). For a segment resolved via `FindFeatureMember`'s
direct-child branch, `instancePath` is simply set to the member's own (already instance-relative)
qualified name — unchanged from before. For a segment resolved via the type-hierarchy fallback
branch (`viaTypeFallback == true`, see `FindFeatureMember` below), `instancePath` is instead set to
`{previous instancePath}::{raw segment text}` — preserving the instance-relative path rather than
collapsing to the type's own declared qualified name. This distinction matters for the dominant
real-world `connect`/`bind` shape: two sibling features (e.g. ports) declared directly in their
owning `part def`s, referenced from an enclosing part via bare usages with no per-instance nested
redeclaration — every such reference resolves via the fallback branch, and without this
`instancePath` accumulator the returned qualified name would collapse to the shared port type's own
declared path (e.g. `PowerPort`'s owning type's own `power`/`output` declaration), causing
`GeneralViewLayoutStrategy.ResolveOwningBox` to (incorrectly) map both endpoints to the same box.

`FindFeatureMember(node, name, out viaTypeFallback)` tries `node`'s own direct children first — an
inline nested usage or redefinition shadows a same-named definition-level member (confirmed by the
OMG fixture `2c-PartsInterconnection-MultipleDecompositions.sysml`'s `port :>> pe = c1.pb`
pattern) — setting `viaTypeFallback = false` and returning immediately when found. Only when no
direct child matches does it fall back to the member's `Typing`-edge target's own hierarchy,
setting `viaTypeFallback = true` for that branch; the returned node's own `QualifiedName` in this
case is the *type's own* declared path, not instance-relative — it is the caller
(`TryResolveFeatureChain`) that reconstructs the instance-relative path using `viaTypeFallback`, as
described above. Every segment after the first is a *visible resolution* in the previous feature
(KerML 8.2.3.5), so both branches admit **public members only** (the `MemberAccess.Public` limit,
matching `NameResolver`'s `Scope.Visible`): a private or protected child, or a private or
protected member inherited through the type's supertypes, is never found. Segment 0 is unaffected
(it is a local/full resolution through `TryResolve`, which sees the namespace's own private
members).

`FindMemberInTypeHierarchy(typeNode, name, access, visited)` finds a member in `typeNode`'s own direct
children, or — recursively — in any `Supertype`-edge ancestor's direct children, walking the
supertype chain until a match is found or the chain is exhausted, admitting only the memberships
`access` allows at every level (`Public` for feature chains; `NonPrivate` — what a specialization
inherits — for `TryResolveInheritedActionMember`). A `HashSet<string>` keyed on
qualified type name guards against supertype cycles (e.g. a malformed `A :> B :> A` model), so the
walk always terminates. `FindMemberInAncestorChain` likewise searches the enclosing node's own
children without restriction but its ancestors only for non-private members.

##### Scope Boundary (Feature-Chain Resolution)

- **`SysmlSatisfyNode` dotted subjects remain unresolved** — unchanged from unit 3; chain
  resolution is not currently extended to `satisfy` subjects.
- **`"allocation"`-keyword endpoints remain single-segment-only** — unchanged from unit 3.
- **Redefinition/subsetting compatibility is not validated** — a chain segment is matched by
  `Name` only; `:>>`/`:>`/`subsets` compatibility between the redefining and redefined feature is
  not checked (matching by name is safe because SysML redefinitions preserve or narrow, never
  change, the feature's identity for chain-traversal purposes).
- **Indexed/sequence access syntax (`#(n)`) is not supported** — a segment containing `#(...)`
  simply fails to match any child `Name` and gracefully degrades to unresolved (diagnostic, no
  edge, no crash).
- **Imported/wildcard-imported feature members are not merged into type member lookup** — only
  `Children` and `Supertype`-chain `Children` are searched.
- **An implied/omitted `SysmlTransitionNode.Source` produces no edge, except for the narrow
  `start`/`done` inherited-pseudostate fallback described below** — there is nothing to walk a
  chain from in the general case, so no partial/misleading edge is emitted; this remains a
  documented limitation for every other implicit-source shape.

##### Inherited Pseudostate Feature Resolution

A transition's source or target commonly names a pseudostate feature (`start`/`done`) that the
enclosing state/action **inherits** from its supertype rather than declares itself — e.g.
`first start then off;` inside a `state usage : VehicleStates { ... }` with no local `start`
feature declared, the synthesized `Source` of an attached transition following an
`entryActionMember`, or the completion target in `then done;` and
`transition first idle then done;`. `TryResolveFeatureChain` cannot see inherited members (it
only resolves via namespace/import scope), so without a dedicated fallback every such transition
produced a false "Unresolved reference" diagnostic and no `Transition` edge, even though the
model is semantically valid per the SysML v2 spec (the Systems Library declares `start` and
`done` as features of `Actions::Action`, which every action and state inherits).

**Why a fallback and not implicit specialization (#93).** The spec-faithful general mechanism is
the implicit specialization of every action to `Actions::Action` and of every state to
`States::StateAction` (itself an `Action`), after which ordinary inherited-member lookup finds
`start`/`done`. Modeling it is a cross-cutting change (new `Supertype` edges on every definition
and usage, with consequences for `query hierarchy`, grid and general views, and the stdlib
re-resolution) tracked separately as #93. The minimal change that is faithful to the spec's
*result* is to resolve exactly those two library-defined names against `Actions::Action`, as
below; it yields the same targets implicit specialization would, and #93 can remove the step-4
special case later without changing any observable result.

`TryResolveInheritedActionMember(scope, name, out resolvedName)` is tried only after
`TryResolveFeatureChain` fails, and only for a `SysmlTransitionNode`'s `Source` or `Target`. On the
`Target` side (`pseudoFeaturesOnly`) it is restricted up front to exactly the names `start`/`done`
and only when the enclosing node is a state or action (definition or usage); no other inherited
member is looked up for a target, and `done` in any other kind of body (for example a part
definition) is left to ordinary name resolution. The `Source` side keeps the full sequence below:

1. Looks up the enclosing node via `_symbolTable.Lookup(scope)` — since at the
   point a `SysmlTransitionNode` is visited, `scope` is the enclosing state/usage
   (transitions have no `Name` of their own to push).
2. If the enclosing node has a resolved `Typing` edge, follows it to the type node and searches
   via `FindMemberInTypeHierarchy` (the same supertype-chain walk `TryResolveFeatureChain`'s
   type-fallback branch uses).
3. Also tries `FindMemberInAncestorChain` directly on the enclosing node itself (a walk of the
   node's own children and its `Supertype`/`Redefinition` resolved edges).
4. **Narrow, hardcoded last resort:** only when `name` is exactly `"start"` or `"done"`, and only
   when the enclosing node has a state/action keyword (a `SysmlFeatureNode` with
   `FeatureKeyword` `"state"`/`"action"`, or a `SysmlDefinitionNode` with `DefinitionKeyword`
   `"state def"`/`"action def"`), looks up the standard library's `Actions::Action` type node's
   direct children by name. This covers the common case of a `state def`/`state usage` with no
   explicit `:>` supertype clause at all — whose implicit ultimate supertype, per the SysML v2
   standard library, is `Actions::Action` — since this codebase has no general
   implicit-generalization/default-supertype inference mechanism (confirmed absent by inspection)
   to derive that relationship automatically.

This fallback is **intentionally narrow and not a general inherited-member resolver**: it is
scoped to Transition `Source` and `Target` resolution only (step 4 additionally scoped to the two
well-known pseudostate names), and a name that is neither declared, imported, inherited nor one
of those two still produces the "Unresolved reference" diagnostic. A future
model using a different, custom state-machine base type that also needs `start`/`done`-like
inherited pseudostate features would not resolve via step 4 (only via steps 1–3, if it declares
an explicit supertype chain) — this is a known, accepted limitation, not a defect to fix here.

##### Requirement-Trace Edge Resolution

Three additional resolution blocks in `ResolveNode` produce the `Satisfy`/`Verify`/`Allocate`
edge kinds, all following the same graceful-degradation contract as the uniform loops above
(no exception is ever thrown for an unresolvable name; unresolved names produce an
unresolved-reference diagnostic and no edge):

- **`VerifiedRequirementNames` (Verify)** — resolved via the same uniform loop pattern used for
  `SupertypeNames`/`ImportedNames`: each entry in `node.VerifiedRequirementNames` that resolves
  produces one `SysmlEdgeKind.Verify` edge sourced from `node.QualifiedName` (the verifying
  case/requirement) to the resolved requirement; an unresolvable entry produces a diagnostic and no
  edge, independently per entry.
- **`SysmlSatisfyNode` (Satisfy)** — resolves `SubjectName` and `RequirementName`
  independently (each optional/absent side is simply skipped). A `SysmlEdgeKind.Satisfy` edge
  (`Source` = resolved subject, `Target` = resolved requirement) is emitted **only when both
  sides resolve** — unlike the uniform loops, this is a two-sided reference, so a single
  unresolvable side suppresses the edge entirely rather than emitting a partial/misleading edge.
  Dotted feature-chain subjects (e.g. `a.b`) are out of scope for this unit and simply fail to
  resolve as a single symbol name, which degrades gracefully (diagnostic, no edge, no crash).
- **`SysmlConnectionNode` with `ConnectionKeyword == "allocation"` (Allocate)** — resolves
  `EndpointA` and `EndpointB` independently, emitting a `SysmlEdgeKind.Allocate` edge (`Source`
  = resolved first end, `Target` = resolved second end) only when both ends resolve, using the
  identical both-sides-must-resolve contract as `Satisfy`. Regular `"connection"`/`"message"`
  keyword variants remain intentionally unresolved (out of scope for this unit).
- **`SysmlDependencyNode` (Dependency)** — resolves every entry in `FromNames` and every entry in
  `ToNames` independently (each unresolvable name produces its own diagnostic, per the
  `resolvedInFile` de-duplication rule), then emits one `SysmlEdgeKind.Dependency` edge per
  resolved (from, to) pair — a cross product, per the grammar's list-of-lists shape (e.g.
  `dependency a, b to c, d;` resolves to up to 4 edges). Unlike the two-sided
  Satisfy/Allocate/Connect contract, a `Dependency` edge is emitted per-pair rather than
  all-or-nothing: an unresolvable name on one side does not suppress edges for the other
  resolvable names.
- **`SysmlViewNode` (Expose)** — resolves each `GetExposedNames()` entry (the `QualifiedName` of
  each `ExposeMember`) into its own `SysmlEdgeKind.Expose` edge, or the standard
  unresolved-reference diagnostic when it does not resolve. The edge's `RawReference` is the
  member's `RawReferenceText` (the reference as written, wildcard/recursive markers kept, bracket
  filter excluded), falling back to `QualifiedName` when it is `null` (hand-built members,
  `DynamicViewSynthesizer`, legacy serialized data); resolution and diagnostics always use the
  stripped `QualifiedName`. An `ExposeMember`'s own
  `BracketFilterExpressionText` (when present) is never inspected here — evaluating it is
  `ExposeScopeResolver`'s responsibility (Phase 2a), given the per-entry containment-subtree
  candidate set only that unit can compute. `RenderTargetName` names a rendering style/format
  (e.g. `asTreeDiagram`, `asElementTable`) per the SysML v2 grammar — never model content — so
  it produces no `Expose` edge and no scoping; the *reference* it writes is resolved as one of the
  view's `ReferenceOccurrences` (see "Expression References"). `FilterExpressionText` (raw source
  text, not a reference) is never inspected here.

##### Expression References

References that the AST keeps only as raw text — a view's `render` target, a transition's trigger
payload, guard and effect, an action-flow `accept`/`send` node's payload and `via`/`to` operands,
an action-flow decision guard, a state `entry`/`do`/`exit` action's declaration, and a constraint
or requirement-constraint expression — are located by `AstBuilder` from the syntax tree into
`SysmlNode.ReferenceOccurrences` (name plus token position; see the `AstBuilder` design).
`ResolveReferenceOccurrences`, called from `ResolveNode` for every node, resolves each occurrence
with `TryResolve` and, when it does not resolve, adds the standard `Unresolved reference: '<name>'`
diagnostic (at the resolver's unresolved severity) at the occurrence's own `(line, column)`.

- **Local namespace.** A `render` target is resolved in the *view's own* namespace (its qualified
  name), so an `import Views::*;` written in the view's body applies (`AstBuilder` keeps a view's
  `import` statements as its children for this reason); every other occurrence is resolved in the
  namespace that owns the node (`scope` of `ResolveNode`), exactly like the node's typing.
  `Views` is not implicitly imported: `render asTreeDiagram;` without `import Views::*;` or the
  qualified `Views::asTreeDiagram` is unresolved, as in the OMG Pilot Implementation. The five
  SysML2Tools extension selectors are not filtered out of the occurrences; a view's occurrence that
  names one and does not resolve gets the portability Warning instead of the unresolved-reference
  diagnostic (see "Tool Render Selectors (Portability Warning)").
- **Reported per occurrence.** Unlike the name-keyed deduplication of the other reference kinds,
  every unresolved occurrence is reported: the same undeclared guard name used in two transitions
  is two places to fix, and the Pilot reports both.
- **No edge.** The occurrences have no single edge kind (a feature, a type, or a rendering,
  depending on the context), so only the resolved/unresolved outcome is produced. Edges are a
  follow-up.
- **Severity.** The resolver's unresolved severity (Warning by default; see "Severity and Origin").
  The standard library has no
  `ReferenceOccurrences` after deserialization (`[JsonIgnore]`); it is checked when it is
  compiled by `StdlibGen`, which adds two justified warnings to the standard library's own
  diagnostics: `Anything` (`Base::Anything` is a KerML declaration the SysML grammar does not
  model, the cause of the existing `Base::Anything` warnings) and `receiver` (inherited from a
  KerML-declared supertype; implicit specialization, #93).
- **Not covered (follow-ups).** Value expressions (`= expr`, `:= expr`) of usages — enabling them
  reported valid OMG corpus models because of unrelated modeling gaps (a quoted name such as `'mi'`
  not matching its short name, usages declared with a metadata prefix, KerML
  library features such as `TrigFunctions::pi`, `::` access into variants); the guard of the
  `first … if … then …` succession form (no AST node is built for it); `if`/`while`/`for` action
  nodes; invocation function names; members selected through `.`; redefinitions; edge creation.

##### Severity and Origin

Every "Unresolved reference" diagnostic — all the kinds listed under "Unresolved-Reference
Positions" and every `ReferenceOccurrences` entry — is emitted at one severity, the required
`unresolvedSeverity` constructor parameter (the third, before the optional `stdlibNames`). The
severity is decided by *which resolver invocation produced the diagnostic*, not by a per-node flag:

- **User files — Warning by default.** `WorkspaceLoader.LoadAsync` passes
  `WorkspaceLoader.DefaultUserUnresolvedSeverity` (`DiagnosticSeverity.Warning`), so a model with an
  unresolved reference still lints with exit code 0. Message, position, and de-duplication rules
  are unchanged, and no edge is recorded.
- **User files — Error when strict.** `LoadAsync(..., strictUnresolved: true)` (the
  `--strict-unresolved` CLI option) passes `DiagnosticSeverity.Error`, so the model fails `lint` and
  is reported as an error by the other commands.
- **Standard library — always Warning.** `StdlibGen` constructs the resolver with
  `DiagnosticSeverity.Warning` explicitly, so the diagnostics produced while compiling the standard
  library stay Warnings (the compile reports 0 errors and 1964 warnings). The compiled library is a
  pre-resolved seed symbol table that is never re-resolved at load time, so a user load produces no
  standard-library-origin diagnostics and the model, serialization, and `SysmlDiagnostic` carry no
  origin marker.
- **Unaffected diagnostics.** The circular-import Warning of this unit and the cyclic-specialization
  Warning of `SupertypeWalker` do not use the severity parameter and are always Warnings.
  The portability Warning below is likewise always a Warning.

**Unresolved-reference severity policy.** The default is Warning, not Error, because name
resolution still has known gaps (dotted feature chains and kernel/library features), tracked by
issue #93 and #129; promoting those to errors by default would reject
valid models. `WorkspaceLoader.DefaultUserUnresolvedSeverity` is the single place to change the
default. Once the OMG corpus produces close to zero strict-mode errors, the constant is flipped to
`Error` and the `--strict-unresolved` flag is replaced (for example with an opt-out). See the
`WorkspaceLoader` design for the full policy.

##### Tool Render Selectors (Portability Warning)

The diagram type router accepts five SysML2Tools extension `render` selectors — `asGeneralDiagram`,
`asStateTransitionDiagram`, `asActionFlowDiagram`, `asSequenceDiagram`, and `asGridDiagram` — that
no standard library declares, so a standard SysML v2 tool cannot resolve them. The internal table
`ReferenceResolver.ToolRenderSelectors` is the single copy of the list in this library and maps each
token to the standard view definition a portable model should use (`GeneralView`,
`StateTransitionView`, `ActionFlowView`, `SequenceView`, `GridView`, declared in the
`StandardViewDefinitions` standard-library package, not in `Views`). The `DiagramTypeRouter` in the
Core library, which this library cannot reference, recognizes the same five tokens; that is the
second copy.

In `ResolveReferenceOccurrences`, a view's occurrence is first resolved normally (so a user-declared
`rendering asGridDiagram`, or one reached through an import, resolves with no diagnostic). Only when
it does not resolve and its name is in `ToolRenderSelectors` is **one Warning** added at the token,
per occurrence, instead of the unresolved-reference diagnostic:

```text
'asGeneralDiagram' is a SysML2Tools extension, not a standard SysML v2 rendering; the model will
not validate in other tools. Use a standard view definition instead (e.g. 'view v :
StandardViewDefinitions::GeneralView').
```

- **Never an Error.** The severity is a Warning whatever `unresolvedSeverity` is, so
  `--strict-unresolved` does not change it, and the documented tool selectors keep working in `lint`.
- **Rendering is unchanged.** The router is untouched; a dynamic `--view-type` view is synthesized
  without reference occurrences and so gets no warning.
- **Standard selectors are unaffected.** `Views::asTreeDiagram`, `Views::asInterconnectionDiagram`,
  the bare forms imported through `import Views::*;`, `asElementTable`, and `asTextualNotation`
  resolve and produce no diagnostic.
- **Qualified extension names are not accepted.** `Views::asGeneralDiagram` is not a member of
  `Views`, so it is an ordinary unresolved-reference diagnostic (a Warning by default).

##### Local Namespace of a Reference

The local namespace passed to `NameResolver` is the qualified name of the nearest enclosing
`SysmlPackageNode`, `SysmlDefinitionNode`, or `SysmlFeatureNode` that has a `Name` (`ChildScope`
prefers the node's own `QualifiedName`, falling back to the joined names). Named feature usages
count because real-world OMG models nest `requirement`/`satisfy`/`verify` usages inside named
`part`/feature usages (e.g. `part 'X Context' { requirement 'Y' {...} satisfy 'Y' by ...; }`),
and `AstBuilder.BuildUsageNode` already scopes such named usages the same way when it computes
child `QualifiedName`s. The nested-namespace import resolution (`import LogicalModel::*;` where
`LogicalModel` is nested inside `'12b-Allocation'`) falls out of `NameResolver`'s resolution of
the import's namespace from the import's own local namespace; the earlier bespoke
`ResolveNamespaceName` helper and the pending bare-redefinition pass are gone.

`FindMemberInAncestorChain` remains for `TryResolveInheritedActionMember`: it walks a node's own
children and — recursively, cycle-guarded — any ancestor reachable via its
`SysmlEdgeKind.Supertype` **and** `SysmlEdgeKind.Redefinition` resolved edges. Both edge kinds
are followed, not supertype-only, because `1c-PartsTreeRedefinition.sysml`'s nested
`frontAxle_c1 redefines frontAxle` has an owner (`frontAxleAssembly_c1`) with no
`SupertypeNames` at all — the only path to the inherited member is via the owner's own
`Redefinition` edge.

##### Error Handling

Issues are reported as `SysmlDiagnostic` entries added to the shared `_diagnostics` list:
unresolved references at the configured `unresolvedSeverity` (Warning by default for user files),
and circular imports and the tool-selector portability notice always as `Warning`. No exceptions
are thrown; the resolver completes even when cycles or unresolved names are present.

##### Dependencies

- `SymbolTable` — `Symbols` is the declaration dictionary `NameResolver` resolves against;
  `Lookup` is used by feature-chain resolution and `FindMemberInAncestorChain` to walk from a
  resolved qualified name back to its node; also used by `TryResolveInheritedActionMember` to look
  up the enclosing node and (in its narrow last-resort branch) the stdlib `Actions::Action` type
  node.
- `NameResolver` — resolves every reference in its internal resolving mode (see `name-resolver.md`).
- `FilterExpressionEvaluator.CreateImportFilter` (Language Filtering subsystem) — the predicate
  that evaluates filtered imports.
- `SysmlNode` hierarchy — traversed to collect `SupertypeNames`, `ImportedNames`, and
  `VerifiedRequirementNames`; checks for `SysmlFeatureNode.FeatureTyping` and
  `SysmlFeatureNode.RedefinedFeatureName`, `SysmlSatisfyNode`
  (`SubjectName`/`RequirementName`), `SysmlDependencyNode` (`FromNames`/`ToNames`),
  `SysmlConnectionNode` with `ConnectionKeyword ==
  "allocation"` (`EndpointA`/`EndpointB`), `SysmlConnectionNode` with `ConnectionKeyword ==
  "connection"`, `"message"`, or `"binding"`, `SysmlTransitionNode` (`Source`/`Target`), and
  `SysmlViewNode`
  (`GetExposedNames()`; `RenderTargetName`/`FilterExpressionText`/each `ExposeMember`'s
  `BracketFilterExpressionText` are never read); reads
  `ResolvedEdges` (`Typing`/`Supertype` kinds during feature-chain resolution; `Supertype`
  **and** `Redefinition` kinds during `FindMemberInAncestorChain`'s ancestor walk).
- `SysmlEdge`, `SemanticIndex` — resolved references are recorded as `SysmlEdge` instances and
  aggregated into the returned `SemanticIndex`.
- `SysmlDiagnostic`, `DiagnosticSeverity` — used to construct and emit diagnostics at the configured
  severity (and the always-Warning import-cycle and portability diagnostics).

##### Callers

`WorkspaceLoader.LoadAsync` constructs a `ReferenceResolver` with the shared `SymbolTable`,
diagnostics list, the unresolved severity (`Error` when `strictUnresolved` is set, otherwise
`DefaultUserUnresolvedSeverity`, `Warning`), and
the seed symbol table's names (the stdlib names), then calls `ResolveAll`
with all user file AST roots (stdlib roots are
registered into the `SymbolTable` directly and are never passed through `ResolveAll`), capturing
the returned `SemanticIndex` into `SysmlWorkspace.Index`. `StdlibGen` constructs one with
`DiagnosticSeverity.Warning`, no stdlib names, and passes every stdlib file root.
