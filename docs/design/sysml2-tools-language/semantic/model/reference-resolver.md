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
   resolution (`NameResolver`), emitting a Warning for any name not found and recording a
   `SysmlEdge` for any name (or pair of names) that resolves.
4. **Feature-chain resolution (pass 2)** — after pass 1 has completed for every file root,
   resolves dotted feature chains (e.g. `engine.fuelPort`) referenced by `SysmlConnectionNode`
   (`ConnectionKeyword == "connection"`, `"message"`, or `"binding"`) endpoints and
   `SysmlTransitionNode` `Source`/`Target` into `Connect`/`Binding`/`Transition`-kind edges.

##### Import Graph

`BuildImportGraph` iterates all file roots, collecting `SysmlImportNode.ImportedNamespace`
values into a `HashSet<string>` per file. The result is a `Dictionary<string, HashSet<string>>`
from file path to imported names.

`DetectCircularImports` runs a DFS over the import graph keys. A cycle is detected when a
node in the current DFS stack is encountered again. The Warning message names the file and
the imported namespace that completes the cycle.

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
edges, and a view usage's `Typing` edge) is also tagged with the reference text exactly as written
(`SysmlEdge.RawReference`), which is the edge's identity: the supertype edge list omits
unresolved references, so a consumer pairs a raw reference with its edge by that text rather than
by position or by name suffix. The two-ended kinds (`Satisfy`, `Allocate`, `Dependency`, `Connect`,
`Binding`, `Transition`) leave it `null`. Any non-empty per-node edge list is attached to `node.ResolvedEdges` and
appended to the aggregate edge list returned by `ResolveAll`.

For each name that does not resolve (and is not already reported in this file), a Warning
diagnostic is emitted. The `resolvedInFile` set prevents duplicate warnings for the same name
within a file; `ResolveOrWarn` may resolve an already-warned name again (a minor,
correctness-preserving redundancy — see Error Handling), but no duplicate diagnostic is
produced. A reference that KerML name resolution rejects — a private member reached through a
qualified name, a candidate a filtered import excludes, a name reachable only through another
namespace's import — is reported exactly like any other unresolved reference (the same
`Unresolved reference: '<name>'` Warning, with no edge), so the layouts and the `lint` command
need no new diagnostic channel.

A `SysmlViewNode`'s `ViewDefinitionName` (`view V : Def`) is resolved the same way into a
`SysmlEdgeKind.Typing` edge, or reported with the same unresolved-reference Warning; callers use
the edge to find the view definition whose view conditions and rendering the usage inherits
(SysML 2.1 Beta 2 7.26.2).

`ResolveAll` returns a `SemanticIndex` built from the aggregate edge list once all file roots
have been traversed.

##### Member Visibility

A qualified name's later segments are resolved by *visible* resolution, so a member whose
`SysmlNode.Visibility` is `Private` (or `Protected`) is not reachable as `P::Hidden` from another
package: no `Typing`/`Supertype`/... edge is recorded and the unresolved-reference Warning is
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

For a `SysmlTransitionNode`, the `Source` side specifically is resolved via `ResolveTransitionSource`
rather than the plain `TryResolveFeatureChain` call used for `Target`: it tries
`TryResolveFeatureChain` first, and only when that fails, falls back to
`TryResolveInheritedActionMember` (see "Inherited Pseudostate Feature Resolution" below) before
the pair is given up on as unresolved. The `Target` side is never given this fallback — it is
scoped to `Source` only, matching the ROADMAP's own framing of this gap as specifically about an
implicit/inherited transition *source*.

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
  simply fails to match any child `Name` and gracefully degrades to unresolved (Warning, no edge,
  no crash).
- **Imported/wildcard-imported feature members are not merged into type member lookup** — only
  `Children` and `Supertype`-chain `Children` are searched.
- **An implied/omitted `SysmlTransitionNode.Source` produces no edge, except for the narrow
  `start`/`done` inherited-pseudostate fallback described below** — there is nothing to walk a
  chain from in the general case, so no partial/misleading edge is emitted; this remains a
  documented limitation for every other implicit-source shape.

##### Inherited Pseudostate Feature Resolution

A transition's source commonly names a pseudostate feature (`start`/`done`) that the enclosing
state/action **inherits** from its supertype rather than declares itself — e.g.
`first start then off;` inside a `state usage : VehicleStates { ... }` with no local `start`
feature declared, or the synthesized `Source` of an attached transition following an
`entryActionMember`. `TryResolveFeatureChain` cannot see inherited members (it only resolves via
namespace/import scope), so without a dedicated fallback every such transition produced a false
"Unresolved reference" diagnostic and no `Transition` edge, even though the model is semantically
valid per the SysML v2 spec.

`TryResolveInheritedActionMember(scope, name, out resolvedName)` is tried only after
`TryResolveFeatureChain` fails, and only for a `SysmlTransitionNode`'s `Source` (never `Target`):

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
scoped to Transition `Source` resolution only (step 4 additionally scoped to the two
well-known pseudostate names), consistent with how the ROADMAP itself frames this gap. A future
model using a different, custom state-machine base type that also needs `start`/`done`-like
inherited pseudostate features would not resolve via step 4 (only via steps 1–3, if it declares
an explicit supertype chain) — this is a known, accepted limitation, not a defect to fix here.

##### Requirement-Trace Edge Resolution

Three additional resolution blocks in `ResolveNode` produce the `Satisfy`/`Verify`/`Allocate`
edge kinds, all following the same graceful-degradation contract as the uniform loops above
(no exception is ever thrown for an unresolvable name; unresolved names produce a Warning and
no edge):

- **`VerifiedRequirementNames` (Verify)** — resolved via the same uniform loop pattern used for
  `SupertypeNames`/`ImportedNames`: each entry in `node.VerifiedRequirementNames` that resolves
  produces one `SysmlEdgeKind.Verify` edge sourced from `node.QualifiedName` (the verifying
  case/requirement) to the resolved requirement; an unresolvable entry produces a Warning and no
  edge, independently per entry.
- **`SysmlSatisfyNode` (Satisfy)** — resolves `SubjectName` and `RequirementName`
  independently (each optional/absent side is simply skipped). A `SysmlEdgeKind.Satisfy` edge
  (`Source` = resolved subject, `Target` = resolved requirement) is emitted **only when both
  sides resolve** — unlike the uniform loops, this is a two-sided reference, so a single
  unresolvable side suppresses the edge entirely rather than emitting a partial/misleading edge.
  Dotted feature-chain subjects (e.g. `a.b`) are out of scope for this unit and simply fail to
  resolve as a single symbol name, which degrades gracefully (Warning, no edge, no crash).
- **`SysmlConnectionNode` with `ConnectionKeyword == "allocation"` (Allocate)** — resolves
  `EndpointA` and `EndpointB` independently, emitting a `SysmlEdgeKind.Allocate` edge (`Source`
  = resolved first end, `Target` = resolved second end) only when both ends resolve, using the
  identical both-sides-must-resolve contract as `Satisfy`. Regular `"connection"`/`"message"`
  keyword variants remain intentionally unresolved (out of scope for this unit).
- **`SysmlDependencyNode` (Dependency)** — resolves every entry in `FromNames` and every entry in
  `ToNames` independently (each unresolvable name produces its own Warning, per the
  `resolvedInFile` de-duplication rule), then emits one `SysmlEdgeKind.Dependency` edge per
  resolved (from, to) pair — a cross product, per the grammar's list-of-lists shape (e.g.
  `dependency a, b to c, d;` resolves to up to 4 edges). Unlike the two-sided
  Satisfy/Allocate/Connect contract, a `Dependency` edge is emitted per-pair rather than
  all-or-nothing: an unresolvable name on one side does not suppress edges for the other
  resolvable names.
- **`SysmlViewNode` (Expose)** — resolves each `GetExposedNames()` entry (the `QualifiedName` of
  each `ExposeMember`) into its own `SysmlEdgeKind.Expose` edge, or the standard
  unresolved-reference Warning diagnostic when it does not resolve. An `ExposeMember`'s own
  `BracketFilterExpressionText` (when present) is never inspected here — evaluating it is
  `ExposeScopeResolver`'s responsibility (Phase 2a), given the per-entry containment-subtree
  candidate set only that unit can compute. `RenderTargetName` names a rendering style/format
  (e.g. `asTreeDiagram`, `asElementTable`) per the SysML v2 grammar — never model content — so
  `ReferenceResolver` never inspects it: no edge is produced and no diagnostic is emitted for it,
  exactly mirroring how `FilterExpressionText` (raw source text, not a reference) is also never
  inspected here.

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

All issues are reported as `Warning`-severity `SysmlDiagnostic` entries added to the shared
`_diagnostics` list. No exceptions are thrown; the resolver completes even when cycles or
unresolved names are present.

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
- `SysmlDiagnostic`, `DiagnosticSeverity` — used to construct and emit Warning diagnostics.

##### Callers

`WorkspaceLoader.LoadAsync` constructs a `ReferenceResolver` with the shared `SymbolTable`,
diagnostics list, and the seed symbol table's names (the stdlib names), then calls `ResolveAll`
with all user file AST roots (stdlib roots are
registered into the `SymbolTable` directly and are never passed through `ResolveAll`), capturing
the returned `SemanticIndex` into `SysmlWorkspace.Index`. `StdlibGen` constructs one with no
stdlib names and passes every stdlib file root.
