### WorkspaceParser

#### Purpose

`WorkspaceParser` is the public API of the Parser subsystem. It parses SysML v2 source text into
an ANTLR4 concrete syntax tree (CST) and returns the syntax diagnostics collected during parsing.
It performs no semantic analysis; the CST it produces is consumed by the Semantic subsystem.

#### Data Model

`WorkspaceParser` is a static class with no instance state. Each call operates only on the
arguments it is given and a locally created diagnostic list, so calls are independent and safe to
run concurrently.

#### Key Methods

##### `ParseSource(string filePath, string content)`

Creates a local diagnostic list, invokes the internal `ParseSourceToCst`, and returns the
collected `IReadOnlyList<SysmlDiagnostic>`. The CST is discarded because only diagnostics are of
interest to public callers.

##### `ParseSourceToCst(string filePath, string content, List<SysmlDiagnostic> diagnostics)`

Internal method that constructs an `AntlrInputStream`, a `SysMLv2Lexer`, a `CommonTokenStream`,
and a `SysMLv2Parser`. It removes the default ANTLR4 error listeners from both the lexer and the
parser and installs a shared `SysmlDiagnosticListener` so that lexer and parser errors are
collected uniformly. It returns the `rootNamespace` CST context for callers in the Semantic
subsystem.

#### Dependencies

- **SysmlDiagnosticListener** — receives lexer and parser syntax errors and appends them to the
  supplied diagnostic list.
- **Antlr4.Runtime.Standard** — provides the input stream, token stream, and the generated
  `SysMLv2Lexer`/`SysMLv2Parser`.

#### Callers

- `WorkspaceLoader` in the Semantic subsystem calls `ParseSourceToCst` for each user file.
- Tests call `ParseSource` directly with controlled inputs.
