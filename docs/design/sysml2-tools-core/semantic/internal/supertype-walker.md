#### SupertypeWalker

##### Overview

`SupertypeWalker` traverses the specialization chains of all symbols registered in the
`SymbolTable` to detect cyclic specialization (e.g., A specializes B, B specializes A).

##### Algorithm

`WalkAll` iterates over all symbols and for each unvisited symbol calls `WalkNode`. `WalkNode`
maintains two sets:

- `chainVisited` — names in the current DFS path (used to detect cycles in this chain).
- `globalVisited` — all visited names across all chains (used to avoid redundant processing).

For each supertype name in the current node:

- If the name is in `chainVisited`, a cyclic specialization warning is emitted and the loop
  continues (the cycle is not traversed further).
- If the supertype is registered in the symbol table and not yet globally visited, `WalkNode`
  is called recursively with a copy of `chainVisited`.

##### Warning Format

Cyclic specialization warnings use `DiagnosticSeverity.Warning` with an empty `FilePath`
(since the cycle spans multiple potential source files).

##### Error Handling

Cyclic specialization is detected and reported as a `Warning`-severity `SysmlDiagnostic`.
No exceptions are thrown. Supertype names not present in the symbol table are silently skipped
(reference-resolution warnings are already emitted by `ReferenceResolver`).

##### Dependencies

- `SymbolTable` — `Symbols` property (all registered names), `Lookup` (supertype node retrieval).
- `SysmlNode` — `SupertypeNames` property traversed on each node.
- `SysmlDiagnostic`, `DiagnosticSeverity` — used to construct and emit Warning diagnostics.

##### Callers

`WorkspaceLoader.LoadAsync` constructs a `SupertypeWalker` with the shared `SymbolTable` and
diagnostics list, then calls `WalkAll` after `ReferenceResolver.ResolveAll` completes.
