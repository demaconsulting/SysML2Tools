#### ReferenceResolver

##### Overview

`ReferenceResolver` performs two analyses over the loaded files:

1. **Import graph cycle detection** — builds a directed graph of import relationships between
   files and uses depth-first search to detect cycles.
2. **Reference resolution** — checks each `SupertypeName`, `SysmlFeatureNode.FeatureTyping`,
   `ImportedName`, `VerifiedRequirementNames` entry, `SysmlSatisfyNode` subject/requirement, and
   `SysmlConnectionNode` (`ConnectionKeyword == "allocation"`) endpoint in all AST nodes against
   the symbol table, emitting a Warning for any name not found and recording a `SysmlEdge` for
   any name (or pair of names) that resolves.

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

`ResolveNode` traverses each AST node's `SupertypeNames`, the node's `FeatureTyping` (when the
node is a `SysmlFeatureNode`), and `ImportedNames` uniformly. For each name that resolves via
`TryResolve`, a `SysmlEdge` is appended to a per-node list, tagged with `SysmlEdgeKind.Supertype`,
`SysmlEdgeKind.Typing`, or `SysmlEdgeKind.Import` respectively; `Source` is the current node's
`QualifiedName` (`null` for anonymous nodes such as import statements) and `Target` is
`resolvedName`. Any non-empty per-node edge list is attached to `node.ResolvedEdges` and
appended to the aggregate edge list returned by `ResolveAll`.

For each name that does not resolve (and is not already reported in this file), a Warning
diagnostic is emitted. The `resolvedInFile` set prevents duplicate warnings for the same name
within a file; `TryResolve` may be called again for an already-warned name (a minor,
correctness-preserving redundancy — see Error Handling), but no duplicate diagnostic is
produced.

`ResolveAll` returns a `SemanticIndex` built from the aggregate edge list once all file roots
have been traversed.

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

##### Error Handling

All issues are reported as `Warning`-severity `SysmlDiagnostic` entries added to the shared
`_diagnostics` list. No exceptions are thrown; the resolver completes even when cycles or
unresolved names are present.

##### Dependencies

- `SymbolTable` — `Contains` method used to check whether a supertype, typing, or import name
  is registered.
- `SysmlNode` hierarchy — traversed to collect `SupertypeNames`, `ImportedNames`, and
  `VerifiedRequirementNames`; checks for `SysmlFeatureNode.FeatureTyping`, `SysmlSatisfyNode`
  (`SubjectName`/`RequirementName`), and `SysmlConnectionNode` with
  `ConnectionKeyword == "allocation"` (`EndpointA`/`EndpointB`).
- `SysmlEdge`, `SemanticIndex` — resolved references are recorded as `SysmlEdge` instances and
  aggregated into the returned `SemanticIndex`.
- `SysmlDiagnostic`, `DiagnosticSeverity` — used to construct and emit Warning diagnostics.

##### Callers

`WorkspaceLoader.LoadAsync` constructs a `ReferenceResolver` with the shared `SymbolTable` and
diagnostics list, then calls `ResolveAll` with all user file AST roots (stdlib roots are
registered into the `SymbolTable` directly and are never passed through `ResolveAll`), capturing
the returned `SemanticIndex` into `SysmlWorkspace.Index`.
