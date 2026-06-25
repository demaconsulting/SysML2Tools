# WorkspaceLoader

## Overview

`WorkspaceLoader` is the public entry point for the Semantic subsystem. It orchestrates parsing,
AST building, symbol registration, reference resolution, and supertype walking into a single
`SysmlLoadResult`.

## Methods

### `LoadAsync(IEnumerable<string> filePaths)`

1. Awaits the cached stdlib semantic result (built once via `BuildStdlibSemanticAsync`).
2. Dispatches all user file paths to `ParseUserFileAsync` in parallel via `Task.WhenAll`.
3. Collects all AST roots and registers them into a shared `SymbolTable`.
4. Runs `ReferenceResolver.ResolveAll` on all file roots.
5. Runs `SupertypeWalker.WalkAll` on the populated symbol table.
6. Constructs a `SysmlWorkspace` from the file list and symbol table.
7. Returns a `SysmlLoadResult(workspace, allDiagnostics)`.

## Caching

The stdlib semantic result is cached via `static readonly Lazy<Task<StdlibSemanticResult>>`.
This ensures the 94 stdlib files are parsed and their ASTs registered at most once per
application lifetime, regardless of how many concurrent callers invoke `LoadAsync`.

## KerML Handling

KerML stdlib parse errors are downgraded to Warnings in `BuildStdlibSemanticAsync`. The
SysML v2 grammar does not fully cover KerML-specific syntax (e.g., `return` keyword), so
parse failures in `.kerml` files are expected and non-fatal.
