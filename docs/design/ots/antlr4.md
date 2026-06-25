## ANTLR4

This document describes the integration and usage design for the `ANTLR4` OTS software item
(`Antlr4.Runtime.Standard` 4.13.1).

### Purpose

`Antlr4.Runtime.Standard` is the official ANTLR4 C# runtime library. It is used to execute the
pre-generated SysML v2 lexer and parser (`SysMLv2Lexer` and `SysMLv2Parser`) that were produced
from the hand-maintained ANTLR4 grammar files in `Grammar/`. The runtime provides the core
infrastructure for tokenizing SysML v2 source text, building concrete syntax trees, and
dispatching syntax errors to registered error listeners.

### Features Used

- `AntlrInputStream` — wraps a `string` source for character-by-character input consumption by
  the lexer.
- `CommonTokenStream` — bridges the lexer to the parser by buffering the token sequence produced
  by `SysMLv2Lexer`.
- Generated lexer class `SysMLv2Lexer` and generated parser class `SysMLv2Parser` — produced from
  the grammar files using `antlr-4.13.1-complete.jar` and committed under `Parser/Antlr/`.
- `IAntlrErrorListener<IToken>` — interface implemented by `SysmlDiagnosticListener` to receive
  parser syntax errors.
- `IAntlrErrorListener<int>` — interface implemented by `SysmlDiagnosticListener` to receive
  lexer syntax errors.

### Integration Pattern

The grammar files (`Grammar/SysMLv2Lexer.g4` and `Grammar/SysMLv2Parser.g4`) are the
authoritative source. The generated C# files are produced by running
`antlr-4.13.1-complete.jar` as documented in `Grammar/README.md` and committed to the
`Parser/Antlr/` folder. The generated files must not be manually edited.

At runtime the integration sequence is as follows:

1. A `SysmlDiagnosticListener` is constructed, bound to the current file path and a shared
   diagnostic list.
2. An `AntlrInputStream` is created from the source text string.
3. A `SysMLv2Lexer` is instantiated over the input stream. The default error listeners are
   removed and the `SysmlDiagnosticListener` is registered on the lexer.
4. A `CommonTokenStream` is created from the lexer.
5. A `SysMLv2Parser` is instantiated over the token stream. The default error listeners are
   removed and the same `SysmlDiagnosticListener` is registered on the parser.
6. The entry rule `rootNamespace()` is invoked. In Phase 1 the returned CST root is discarded.
7. Any lexer or parser errors encountered during steps 3–6 are delivered to
   `SysmlDiagnosticListener` via the `IAntlrErrorListener` interface, which appends a
   `SysmlDiagnostic` record to the shared list.

No initialization or disposal beyond the steps above is required; ANTLR4 objects are
short-lived and garbage-collected after each parse call.
