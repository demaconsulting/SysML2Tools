#### ReferenceResolver

##### Overview

`ReferenceResolver` performs two analyses over the loaded files:

1. **Import graph cycle detection** — builds a directed graph of import relationships between
   files and uses depth-first search to detect cycles.
2. **Supertype reference resolution** — checks each `SupertypeName` in all AST nodes against
   the symbol table and emits a Warning for any name not found.

##### Import Graph

`BuildImportGraph` iterates all file roots, collecting `SysmlImportNode.ImportedNamespace`
values into a `HashSet<string>` per file. The result is a `Dictionary<string, HashSet<string>>`
from file path to imported names.

`DetectCircularImports` runs a DFS over the import graph keys. A cycle is detected when a
node in the current DFS stack is encountered again. The Warning message names the file and
the imported namespace that completes the cycle.

##### Supertype Resolution

`ResolveNode` traverses each AST node's `SupertypeNames`. For each name not found in the
symbol table (and not already reported in this file), a Warning diagnostic is emitted. The
`resolvedInFile` set prevents duplicate warnings for the same name within a file.

##### Error Handling

All issues are reported as `Warning`-severity `SysmlDiagnostic` entries added to the shared
`_diagnostics` list. No exceptions are thrown; the resolver completes even when cycles or
unresolved names are present.

##### Dependencies

- `SymbolTable` — `Contains` method used to check whether a supertype name is registered.
- `SysmlNode` hierarchy — traversed to collect `SupertypeNames` and `ImportedNames`.
- `SysmlDiagnostic`, `DiagnosticSeverity` — used to construct and emit Warning diagnostics.

##### Callers

`WorkspaceLoader.LoadAsync` constructs a `ReferenceResolver` with the shared `SymbolTable` and
diagnostics list, then calls `ResolveAll` with all stdlib and user file AST roots.
