#### SymbolTable

##### Overview

`SymbolTable` is a registry mapping fully-qualified SysML/KerML names to their `SysmlNode`
declaration nodes. It is populated by calling `RegisterAll` on each AST root after parsing.

##### Algorithm

`RegisterAll(SysmlNode? root)` performs a depth-first traversal of the AST. For each node
with a non-null, non-empty `QualifiedName`, it calls `_symbols.TryAdd(QualifiedName, node)`.
`TryAdd` is used (not direct assignment) to silently ignore duplicate declarations.

##### Lookup

`Lookup(string qualifiedName)` returns the registered node, or null if not found.
`Contains(string qualifiedName)` returns a boolean without allocating a node reference.

##### Thread Safety

`SymbolTable` is not thread-safe. In `WorkspaceLoader`, all `RegisterAll` calls occur on a
single thread after the parallel parse tasks complete.

##### Error Handling

`RegisterAll(null)` is a no-op; no exception is thrown. Duplicate qualified names are silently
ignored via `TryAdd` — the first registration wins.

##### Dependencies

- `SysmlNode` hierarchy — the node types stored in the registry.

##### Callers

- `WorkspaceLoader.LoadAsync` — calls `RegisterAll` once per stdlib root and once per user file
  root; reads `Symbols` to build the final `SysmlWorkspace.Declarations`.
- `ReferenceResolver` — calls `Contains` to check supertype name resolution.
- `SupertypeWalker` — reads `Symbols` to iterate all registered names; calls `Lookup` to
  retrieve supertype nodes for chain traversal.
