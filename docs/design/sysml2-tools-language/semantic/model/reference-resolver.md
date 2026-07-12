#### ReferenceResolver

##### Overview

`ReferenceResolver` performs three analyses over the loaded files:

1. **Import graph cycle detection** — builds a directed graph of import relationships between
   files and uses depth-first search to detect cycles.
2. **Reference resolution (pass 1)** — checks each `SupertypeName`, `SysmlFeatureNode.FeatureTyping`,
   `SysmlFeatureNode.RedefinedFeatureName`, `ImportedName`, `VerifiedRequirementNames` entry,
   `SysmlSatisfyNode` subject/requirement, `SysmlConnectionNode`
   (`ConnectionKeyword == "allocation"`) endpoint, and `SysmlDependencyNode` from/to name lists
   in all AST nodes against
   the symbol table, emitting a Warning for any name not found and recording a `SysmlEdge` for
   any name (or pair of names) that resolves.
3. **Feature-chain resolution (pass 2)** — after pass 1 has completed for every file root,
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

`TryResolve(name, namespaceStack, imports, out resolvedName)` performs the four-step lookup
(direct; enclosing-namespace prefixes; wildcard imports; named imports) and, on success, also
outputs the exact qualified name that matched (`resolvedName`). Steps 3–4 resolve the imported
namespace name itself via `ResolveNamespaceName` before building the `"{ns}::{name}"` candidate
(see the "Nested-Namespace Import Resolution" deviation below).

`ResolveNode` traverses each AST node's `SupertypeNames`, the node's `FeatureTyping` and
`RedefinedFeatureName` (both only when the node is a `SysmlFeatureNode`), and `ImportedNames`
uniformly. For each name that resolves via
`TryResolve`, a `SysmlEdge` is appended to a per-node list, tagged with `SysmlEdgeKind.Supertype`,
`SysmlEdgeKind.Typing`, `SysmlEdgeKind.Redefinition`, or `SysmlEdgeKind.Import` respectively;
`Source` is the current node's
`QualifiedName` (`null` for anonymous nodes such as import statements) and `Target` is
`resolvedName`. Any non-empty per-node edge list is attached to `node.ResolvedEdges` and
appended to the aggregate edge list returned by `ResolveAll`. The `RedefinedFeatureName` block
is a line-for-line mirror of the `FeatureTyping` block immediately preceding it — same
`TryResolve` call, same `resolvedInFile` deduplication, same Warning diagnostic message format —
since both are a single raw reference captured on a `SysmlFeatureNode` with identical resolution
semantics.

For each name that does not resolve (and is not already reported in this file), a Warning
diagnostic is emitted. The `resolvedInFile` set prevents duplicate warnings for the same name
within a file; `TryResolve` may be called again for an already-warned name (a minor,
correctness-preserving redundancy — see Error Handling), but no duplicate diagnostic is
produced.

`ResolveAll` returns a `SemanticIndex` built from the aggregate edge list once all file roots
have been traversed.

##### Feature-Chain Resolution

After pass 1 (`ResolveNode`) has run to completion over **every** file root, `ResolveAll` runs a
second traversal, `ResolveFeatureChains`, over every file root again. This ordering is required
because a chain walk depends on `Typing`/`Supertype` edges attached to `node.ResolvedEdges` by
pass 1 — and a chain in file A may reference a type declared (and typed/specialized) in file B, or
reference a node that pass 1 has not yet visited within the same file (a forward reference in
document order). Running feature-chain resolution as a strictly later, whole-workspace second pass
guarantees every `Typing`/`Supertype` edge needed by the walk already exists.

`ResolveFeatureChains` mirrors `ResolveNode`'s namespace-stack push/pop condition exactly
(`(node is SysmlPackageNode or SysmlDefinitionNode or SysmlFeatureNode) && node.Name is not null`)
so that segment-0 resolution scope cannot silently diverge between the two passes. For each
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

`TryResolveFeatureChain(chain, namespaceStack, imports, out resolvedName)` splits the raw
reference text on `.`. Segment 0 is resolved via the existing `TryResolve` four-step lookup (so it
participates in the same scope/import resolution as any other single-name reference); a
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
described above.

`FindMemberInTypeHierarchy(typeNode, name, visited)` finds a member in `typeNode`'s own direct
children, or — recursively — in any `Supertype`-edge ancestor's direct children, walking the
supertype chain until a match is found or the chain is exhausted. A `HashSet<string>` keyed on
qualified type name guards against supertype cycles (e.g. a malformed `A :> B :> A` model), so the
walk always terminates.

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

`TryResolveInheritedActionMember(namespaceStack, name, out resolvedName)` is tried only after
`TryResolveFeatureChain` fails, and only for a `SysmlTransitionNode`'s `Source` (never `Target`):

1. Looks up the enclosing node via `_symbolTable.Lookup(string.Join("::", namespaceStack))` —
   the same joined-namespace-stack convention `TryResolveBareRedefinition` uses, since at the
   point a `SysmlTransitionNode` is visited, `namespaceStack` reflects the enclosing state/usage
   (transitions have no `Name` of their own to push).
2. If the enclosing node has a resolved `Typing` edge, follows it to the type node and searches
   via `FindMemberInTypeHierarchy` (the same supertype-chain walk `TryResolveFeatureChain`'s
   type-fallback branch uses).
3. Also tries `FindMemberInAncestorChain` directly on the enclosing node itself (covering the
   `Supertype`/`Redefinition`-edge walk `TryResolveBareRedefinition` already performs elsewhere).
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

##### Deviations From Uniform Resolution (Behavior-Neutral Additive Fixes)

Two small resolver changes were required for the new Satisfy/Verify/Allocate edges to resolve
correctly against real-world OMG model structure; both are additive-only and cannot regress a
previously-successful resolution — they can only enable new, previously-unreachable resolution
paths:

- **Namespace-stack push condition extended to include named `SysmlFeatureNode`.** The push
  condition in `ResolveNode`'s recursion (`(node is SysmlPackageNode or SysmlDefinitionNode or
  SysmlFeatureNode) && node.Name is not null`) was extended from `SysmlPackageNode or
  SysmlDefinitionNode` only. Real-world OMG models nest `requirement`/`satisfy`/`verify` usages
  inside named `part`/feature usages (e.g. `part 'X Context' { requirement 'Y' {...} satisfy 'Y'
  by ...; }`), and `AstBuilder.BuildUsageNode` already pushes such named usages onto its own
  namespace stack when computing child `QualifiedName`s. The resolver's original push condition
  never pushed named Feature usages onto its own enclosing-namespace lookup stack, so a name
  nested this way could never have resolved via the enclosing-scope step in the first place —
  extending the condition only adds new resolution possibilities.
- **`ResolveNamespaceName` helper (nested-namespace wildcard/explicit-import resolution fix).**
  `TryResolve`'s Steps 3–4 (wildcard and explicit named imports) previously built the
  `"{ns}::{name}"` candidate using the raw, unqualified import text directly. When the imported
  namespace itself is nested inside another package (e.g. `import LogicalModel::*;` where
  `LogicalModel` is nested inside package `'12b-Allocation'`), this built an incorrect candidate
  (`"LogicalModel::x"` instead of `"'12b-Allocation'::LogicalModel::x"`), so nested-namespace
  wildcard/explicit imports never matched. `ResolveNamespaceName(ns, namespaceStack)` fixes this
  by trying a direct symbol-table lookup of `ns` first (identical to the prior behavior for
  already-qualified namespaces), then progressively shorter enclosing-scope prefixes (mirroring
  `TryResolve`'s own Step 2), falling back to the raw name unchanged if nothing matches — so
  already-qualified or genuinely-unresolvable namespaces behave exactly as before; only
  previously-failing nested-namespace import cases gain resolution.
- **`TryResolveBareRedefinition`/`FindMemberInAncestorChain` (bare-name inherited-redefinition
  fallback).** The `RedefinedFeatureName` block previously called only `TryResolve`, which is
  namespace/import-scoped and never walks a supertype chain — but per the SysML v2 spec, a
  `redefines`/`:>>` target's entire purpose is referencing a member the redefining feature
  *inherits*, not one declared or imported into the same lexical scope. This is the dominant
  real-world shape (e.g. `RedefinitionExample.sysml`'s bare `smallEng redefines eng`, where `eng`
  is a member of `SmallVehicle`'s supertype `Vehicle`), so the original code produced a false
  unresolved-reference Warning and no edge for the standard case. `TryResolveBareRedefinition`
  looks up the immediate owner node via `_symbolTable.Lookup` on the joined `namespaceStack`
  (the owner's qualified name, since the owner always pushes its own name before its children
  are visited), then delegates to `FindMemberInAncestorChain`, which walks the owner's own
  direct children and — recursively, cycle-guarded — any ancestor reachable via its
  `SysmlEdgeKind.Supertype` **and** `SysmlEdgeKind.Redefinition` resolved edges. Both edge kinds
  must be followed, not supertype-only: `1c-PartsTreeRedefinition.sysml`'s nested `frontAxle_c1
  redefines frontAxle` has an owner (`frontAxleAssembly_c1`) with no `SupertypeNames` at all —
  the only path to the inherited `frontAxle` member is via the owner's own already-resolved
  `Redefinition` edge to `frontAxleAssembly`. This fallback is tried only after `TryResolve`
  fails, so it is additive and cannot regress a previously-successful qualified/scoped
  resolution. The same fallback is also tried for a `SysmlFeatureNode`'s own `SupertypeNames`
  entries (but not a definition's): a usage-level `subsets`/`:>` clause (captured into
  `SupertypeNames` by `AstBuilder.ExtractSubsettingTargetNames`) is, like `redefines`, commonly a
  bare reference to a member the owner inherits — e.g. `1c-PartsTreeRedefinition.sysml`'s `part
  frontWheel_1 subsets frontWheel = frontWheel#(1);`, where `frontWheel` is a member of
  `frontAxleAssembly_c1`'s redefined ancestor `frontAxleAssembly`, not of `frontAxleAssembly_c1`
  itself. A definition's own `SupertypeNames` (from `part def X :> Y`) name the supertype
  directly rather than an inherited member, so the fallback is intentionally skipped for
  non-feature nodes.

##### Error Handling

All issues are reported as `Warning`-severity `SysmlDiagnostic` entries added to the shared
`_diagnostics` list. No exceptions are thrown; the resolver completes even when cycles or
unresolved names are present.

##### Dependencies

- `SymbolTable` — `Contains` method used to check whether a supertype, typing, redefinition, or
  import name is registered; `Lookup` used by feature-chain resolution and
  `TryResolveBareRedefinition`/`FindMemberInAncestorChain` to walk from a resolved qualified name
  back to its node; also used by `TryResolveInheritedActionMember` to look up the enclosing node
  and (in its narrow last-resort branch) the stdlib `Actions::Action` type node.
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
  **and** `Redefinition` kinds during `FindMemberInAncestorChain`'s bare-redefinition ancestor
  walk).
- `SysmlEdge`, `SemanticIndex` — resolved references are recorded as `SysmlEdge` instances and
  aggregated into the returned `SemanticIndex`.
- `SysmlDiagnostic`, `DiagnosticSeverity` — used to construct and emit Warning diagnostics.

##### Callers

`WorkspaceLoader.LoadAsync` constructs a `ReferenceResolver` with the shared `SymbolTable` and
diagnostics list, then calls `ResolveAll` with all user file AST roots (stdlib roots are
registered into the `SymbolTable` directly and are never passed through `ResolveAll`), capturing
the returned `SemanticIndex` into `SysmlWorkspace.Index`.
