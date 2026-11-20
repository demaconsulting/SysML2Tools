### WorkspaceLoader

![Language Semantic Structure](LanguageSemanticView.svg)

#### Purpose

`WorkspaceLoader` is the public entry point for the Semantic subsystem. It orchestrates parsing,
AST building, symbol registration, reference resolution, and supertype walking into a single
`SysmlLoadResult`.

#### Data Model

`WorkspaceLoader` is a static class with no instance state. It holds no shared fields; each
call to `LoadAsync` creates an independent `SymbolTable`.

#### Key Methods

##### `LoadAsync(IEnumerable<string> filePaths, SymbolTable? seedSymbolTable = null)`

The original public signature, retained so that consumers compiled against earlier releases remain
binary compatible (adding an optional parameter to an existing public method would change its
metadata signature and cause `MissingMethodException` for those consumers). It is a non-async
forwarder equivalent to
`LoadAsync(filePaths, seedSymbolTable, strictUnresolved: false)`, so unresolved references are
reported as Warnings. The `strictUnresolved` parameter of the three-parameter overload has no
default so that the two overloads never overlap and `LoadAsync(files)` and
`LoadAsync(files, seed)` bind unambiguously to this overload.

##### `LoadAsync(IEnumerable<string> filePaths, SymbolTable? seedSymbolTable, bool strictUnresolved)`

1. Creates a `SymbolTable` seeded from `seedSymbolTable` via the copy constructor
   `new SymbolTable(seedSymbolTable.Symbols)` when a seed is provided, or creates an
   empty `SymbolTable` when `seedSymbolTable` is `null`.
2. Dispatches all user file paths to `ParseUserFileAsync` in parallel via `Task.WhenAll`, then
   orders the results canonically: a stable ordinal sort on each file's full path with directory
   separators normalized to `/` (`CanonicalPathKey`). The order of the supplied paths is
   therefore irrelevant, and so is the platform's directory enumeration order.
3. Collects all AST roots, in that canonical order, and registers them into a shared
   `SymbolTable`. The table keeps the first registration of a qualified name (as does the
   file-root-import registration in `NameResolver`), so when several files declare the same
   top-level name (KerML leaves which one is meant unspecified) the declaration in the
   canonically first file wins, deterministically. The `SysmlWorkspace.Files` list and the
   diagnostics follow the same order.
4. Runs `ReferenceResolver.ResolveAll` on all user file roots, constructing the resolver with the
   unresolved severity — `DiagnosticSeverity.Error` when `strictUnresolved` is `true`, otherwise
   `WorkspaceLoader.DefaultUserUnresolvedSeverity` (`Warning`) —
   and handing it the seed symbol table's
   names as the standard library names, which filtered-import evaluation needs, capturing the
   returned `SemanticIndex`.
5. Runs `SupertypeWalker.WalkAll` on the populated symbol table.
6. Constructs a `SysmlWorkspace` from the file list, symbol table, and the `SemanticIndex`
   from step 4 (assigned to `SysmlWorkspace.Index`).
7. Returns a `SysmlLoadResult(workspace, allDiagnostics)`.

##### `ParseUserFileAsync(string filePath)`

Reads the file via `File.ReadAllTextAsync`, calls `WorkspaceParser.ParseSourceToCst`, builds
an AST root via `AstBuilder.Build`, and returns `(path, root, diagnostics)`. File I/O failures
are caught and converted to a single Error diagnostic.

#### Error Handling

- `ParseUserFileAsync` catches any `Exception` from `File.ReadAllTextAsync` and returns an
  `Error`-severity `SysmlDiagnostic` rather than propagating the exception.
- Unresolved references reported by `ReferenceResolver` are `Warning` severity by default and do
  not cause `HasErrors` to be set on the returned `SysmlLoadResult`; with `strictUnresolved: true`
  they are `Error` severity and do. The standard library is a pre-resolved seed that is never
  re-resolved here, so it contributes no unresolved-reference diagnostics.
- Circular-import (`ReferenceResolver`), cyclic-specialization (`SupertypeWalker`), and
  tool-render-selector portability diagnostics are always `Warning` severity and never set
  `HasErrors`, independent of `strictUnresolved`.

#### Unresolved-Reference Severity Policy

The severity of an unresolved reference in a user file is chosen once, in `LoadAsync`:

- `WorkspaceLoader.DefaultUserUnresolvedSeverity` (an internal constant, currently
  `DiagnosticSeverity.Warning`) is the single place that defines the default.
- The `strictUnresolved` parameter promotes user-file unresolved references to
  `DiagnosticSeverity.Error` for callers that opt in (the `--strict-unresolved` CLI option).
- Standard library diagnostics are always `Warning`: `StdlibGen` passes `Warning` to the
  `ReferenceResolver` explicitly, and the compiled library is never re-resolved on a user load.
- The portability Warning for a SysML2Tools extension `render` selector is independent of this
  policy and stays a `Warning` either way.

The default is `Warning` while the OMG corpus still reports strict-mode errors, all of them members
`AstBuilder` does not record rather than name-resolution gaps (see the `ReferenceResolver`
design): the OMG example corpus reports 48 strict-mode errors in 18 of its 251
files, down from 94 in 36 before feature chains were resolved. These are tracked by issue #129
(implicit specialization, #93, is modeled for lookup). The
plan is: once the OMG example corpus produces close to zero strict-mode errors, flip
`DefaultUserUnresolvedSeverity` to `Error` and replace the `--strict-unresolved` option with an
opt-out (for example, a flag that downgrades unresolved references to warnings).

#### Dependencies

- **WorkspaceParser** (`ParseSourceToCst`) — parses source text into an ANTLR4 CST.
- **AstBuilder** (`Build`) — transforms CST roots into typed AST nodes.
- **SymbolTable** (`RegisterAll`, `Symbols`) — stores and exposes all named declarations.
- **ReferenceResolver** (`ResolveAll`) — resolves supertype, typing, and import references (and the
  other reference kinds) by KerML name resolution, and checks
  import cycles; returns a `SemanticIndex` over all resolved edges.
- **SupertypeWalker** (`WalkAll`) — detects cyclic specialization chains.

#### Callers

`WorkspaceLoader.LoadAsync` is a public static method consumed by:

- Tests in `DemaConsulting.SysML2Tools.Tests` (`WorkspaceLoaderTests`).
- Future rendering and tooling layers that require a populated semantic workspace.
