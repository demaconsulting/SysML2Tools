### WorkspaceLoader

#### Purpose

`WorkspaceLoader` is the public entry point for the Semantic subsystem. It orchestrates parsing,
AST building, symbol registration, reference resolution, and supertype walking into a single
`SysmlLoadResult`.

#### Data Model

`WorkspaceLoader` is a static class with no instance state. It holds no shared fields; each
call to `LoadAsync` creates an independent `SymbolTable`.

#### Key Methods

##### `LoadAsync(IEnumerable<string> filePaths, SymbolTable? seedSymbolTable = null)`

1. Creates a `SymbolTable` seeded from `seedSymbolTable` via the copy constructor
   `new SymbolTable(seedSymbolTable.Symbols)` when a seed is provided, or creates an
   empty `SymbolTable` when `seedSymbolTable` is `null`.
2. Dispatches all user file paths to `ParseUserFileAsync` in parallel via `Task.WhenAll`.
3. Collects all AST roots and registers them into a shared `SymbolTable`.
4. Runs `ReferenceResolver.ResolveAll` on all file roots.
5. Runs `SupertypeWalker.WalkAll` on the populated symbol table.
6. Constructs a `SysmlWorkspace` from the file list and symbol table.
7. Returns a `SysmlLoadResult(workspace, allDiagnostics)`.

##### `ParseUserFileAsync(string filePath)`

Reads the file via `File.ReadAllTextAsync`, calls `WorkspaceParser.ParseSourceToCst`, builds
an AST root via `AstBuilder.Build`, and returns `(path, root, diagnostics)`. File I/O failures
are caught and converted to a single Error diagnostic.

#### Error Handling

- `ParseUserFileAsync` catches any `Exception` from `File.ReadAllTextAsync` and returns an
  `Error`-severity `SysmlDiagnostic` rather than propagating the exception.
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
