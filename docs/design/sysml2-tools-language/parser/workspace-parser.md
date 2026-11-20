### WorkspaceParser

![Language Parser Structure](LanguageParserView.svg)

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

#### Accepted Syntax

The parser accepts SysML v2 textual notation and, through the grammar's Local Patch 4, the KerML
package members used by the standard library's `.kerml` files directly in a package body or at the
root of a file: `classifier`, `metaclass`, `behavior`, `interaction`, `type`, `multiplicity`,
`namespace`, the relationship statements (`subclassifier`, `subset`, `redefinition`, ...), and
every feature element (`feature`, `step`, `expr`, `bool`, `inv`, `connector`, `binding`,
`succession`, `flow`, `succession flow`). `step`, `expr`, `bool`, and `inv` may omit their
declaration (`inv { ... }`, `step;`), as in the KerML grammar. The KerML alternatives are tried
after the SysML ones, so SysML models — including usages named `feature`, `type`, `behavior`, or
`step` — parse as before. See `Grammar/README.md` for the exact productions.

#### Dependencies

- **SysmlDiagnosticListener** — receives lexer and parser syntax errors and appends them to the
  supplied diagnostic list.
- **Antlr4.Runtime.Standard** — provides the input stream, token stream, and the generated
  `SysMLv2Lexer`/`SysMLv2Parser`.

#### Callers

- `WorkspaceLoader` in the Semantic subsystem calls `ParseSourceToCst` for each user file.
- Tests call `ParseSource` directly with controlled inputs.
