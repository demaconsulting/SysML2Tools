#### ReferenceResolver

##### Overview

`ReferenceResolver` performs two analyses over the loaded files:

1. **Import graph cycle detection** — builds a directed graph of import relationships between
   files and uses depth-first search to detect cycles.
2. **Reference resolution** — checks each `SupertypeName`, `SysmlFeatureNode.FeatureTyping`,
   and `ImportedName` in all AST nodes against the symbol table, emitting a Warning for any
   name not found and recording a `SysmlEdge` for any name that resolves.

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
outputs the exact qualified name that matched (`resolvedName`).

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

##### Error Handling

All issues are reported as `Warning`-severity `SysmlDiagnostic` entries added to the shared
`_diagnostics` list. No exceptions are thrown; the resolver completes even when cycles or
unresolved names are present.

##### Dependencies

- `SymbolTable` — `Contains` method used to check whether a supertype, typing, or import name
  is registered.
- `SysmlNode` hierarchy — traversed to collect `SupertypeNames` and `ImportedNames`; checks for
  `SysmlFeatureNode.FeatureTyping`.
- `SysmlEdge`, `SemanticIndex` — resolved references are recorded as `SysmlEdge` instances and
  aggregated into the returned `SemanticIndex`.
- `SysmlDiagnostic`, `DiagnosticSeverity` — used to construct and emit Warning diagnostics.

##### Callers

`WorkspaceLoader.LoadAsync` constructs a `ReferenceResolver` with the shared `SymbolTable` and
diagnostics list, then calls `ResolveAll` with all user file AST roots (stdlib roots are
registered into the `SymbolTable` directly and are never passed through `ResolveAll`), capturing
the returned `SemanticIndex` into `SysmlWorkspace.Index`.
