### SysmlDiagnosticListener

#### Purpose

`SysmlDiagnosticListener` is the internal error listener of the Parser subsystem. It adapts the
ANTLR4 error-listener interfaces to the subsystem's structured `SysmlDiagnostic` model, capturing
every lexer and parser syntax error into a shared diagnostic list.

#### Data Model

`SysmlDiagnosticListener` is an `internal sealed` class holding two fields captured at
construction: the source `filePath` used for attribution and a reference to the caller-owned
`List<SysmlDiagnostic>` to which errors are appended. It implements both
`IAntlrErrorListener<IToken>` (parser errors) and `IAntlrErrorListener<int>` (lexer errors).

#### Key Methods

##### `SyntaxError(...)` (parser and lexer overloads)

Both `IAntlrErrorListener` overloads forward to the private `Append` helper, passing the line,
column, and message reported by ANTLR4. Handling both overloads with one code path ensures lexer
and parser errors are recorded identically.

##### `Append(int line, int column, string msg)`

Constructs a `SysmlDiagnostic` with the captured `filePath`, the reported one-based line and
zero-based column, `DiagnosticSeverity.Error`, and the ANTLR4 message, and adds it to the shared
list.

#### Dependencies

- **Antlr4.Runtime.Standard** — supplies the `IAntlrErrorListener<T>` interfaces and error-callback
  parameters.
- **SysmlDiagnostic** — the record type produced for each captured error.

#### Callers

`WorkspaceParser.ParseSourceToCst` creates one listener per parse and installs it on both the
lexer and the parser.
