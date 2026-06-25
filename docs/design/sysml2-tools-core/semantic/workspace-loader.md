### WorkspaceLoader

#### Purpose

`WorkspaceLoader` is the public entry point for the Semantic subsystem. It orchestrates parsing,
AST building, symbol registration, reference resolution, and supertype walking into a single
`SysmlLoadResult`.

#### Data Model

`WorkspaceLoader` is a static class with no instance state. It holds one private static field:

- **`StdlibSemanticTask`** (`Lazy<Task<StdlibSemanticResult>>`): caches the stdlib parse and AST
  build result. The factory executes once on the first call to `LoadAsync`; all subsequent calls
  reuse the same `Task`. This ensures the 94 stdlib files are parsed at most once per process.

`StdlibSemanticResult` is a private sealed record:

- **`AstRoots`** (`IReadOnlyList<(string VirtualPath, SysmlNode? Root)>`): AST root per stdlib
  resource.
- **`Diagnostics`** (`IReadOnlyList<SysmlDiagnostic>`): Collected diagnostics for all stdlib files.

#### Key Methods

##### `LoadAsync(IEnumerable<string> filePaths)`

1. Awaits the cached stdlib semantic result (built once via `BuildStdlibSemanticAsync`).
2. Dispatches all user file paths to `ParseUserFileAsync` in parallel via `Task.WhenAll`.
3. Collects all AST roots and registers them into a shared `SymbolTable`.
4. Runs `ReferenceResolver.ResolveAll` on all file roots.
5. Runs `SupertypeWalker.WalkAll` on the populated symbol table.
6. Constructs a `SysmlWorkspace` from the file list and symbol table.
7. Returns a `SysmlLoadResult(workspace, allDiagnostics)`.

##### `BuildStdlibSemanticAsync()`

Enumerates embedded `.sysml` and `.kerml` resources, reads each to a string, calls
`WorkspaceParser.ParseSourceToCst`, downgrades KerML parse errors to Warnings, builds an AST
root via `AstBuilder.Build`, and returns all roots and diagnostics in a `StdlibSemanticResult`.

##### `ParseUserFileAsync(string filePath)`

Reads the file via `File.ReadAllTextAsync`, calls `WorkspaceParser.ParseSourceToCst`, builds
an AST root via `AstBuilder.Build`, and returns `(path, root, diagnostics)`. File I/O failures
are caught and converted to a single Error diagnostic.

##### `GetStdlibAstAsync()`

Returns `StdlibSemanticTask.Value`, triggering the `Lazy` factory on first access.

#### Error Handling

- `ParseUserFileAsync` catches any `Exception` from `File.ReadAllTextAsync` and returns an
  `Error`-severity `SysmlDiagnostic` rather than propagating the exception.
- KerML stdlib parse errors in `BuildStdlibSemanticAsync` are downgraded from `Error` to
  `Warning` severity because the SysML v2 grammar does not fully cover KerML-specific syntax.
- Reference and cycle errors from `ReferenceResolver` and `SupertypeWalker` are `Warning`
  severity and do not cause `HasErrors` to be set on the returned `SysmlLoadResult`.

#### Dependencies

- **WorkspaceParser** (`ParseSourceToCst`) — parses source text into an ANTLR4 CST.
- **AstBuilder** (`Build`) — transforms CST roots into typed AST nodes.
- **SymbolTable** (`RegisterAll`, `Symbols`) — stores and exposes all named declarations.
- **ReferenceResolver** (`ResolveAll`) — checks supertype references and import cycles.
- **SupertypeWalker** (`WalkAll`) — detects cyclic specialization chains.

#### Callers

`WorkspaceLoader.LoadAsync` is a public static method consumed by:

- Tests in `DemaConsulting.SysML2Tools.Tests` (`WorkspaceLoaderTests`).
- Future rendering and tooling layers that require a populated semantic workspace.
