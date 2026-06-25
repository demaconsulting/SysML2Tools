## Parser

### Overview

The `Parser` subsystem is responsible for all SysML v2 syntax parsing within the
`DemaConsulting.SysML2Tools` core library. It provides the public API consumed by other systems
(`WorkspaceParser`) and an internal subsystem (`Internal`) that encapsulates ANTLR4 runtime
interaction and embedded stdlib loading.

The subsystem contains the following items:

- **WorkspaceParser** (Unit) — public API: accepts file paths or in-memory source strings and
  returns parsed results with diagnostics.
- **Internal** (Subsystem) — internal implementation:
  - **SysmlDiagnosticListener** (Unit) — ANTLR4 error listener that maps lexer and parser
    syntax errors to `SysmlDiagnostic` records.
  - **StdlibLoader** (Unit) — enumerates and reads embedded `.sysml` stdlib resources;
    defers `.kerml` files to Phase 2.

Supporting data types declared at the `Parser` namespace level:

- **DiagnosticSeverity** (enum) — `Info`, `Warning`, `Error`.
- **SysmlDiagnostic** (sealed record) — `FilePath`, `Line`, `Column`, `Severity`, `Message`.
- **WorkspaceParseResult** (sealed class) — `Files`, `Diagnostics`, `HasErrors`.

### Interfaces

**WorkspaceParser.Parse**: Parses stdlib plus caller-supplied files.

- *Type*: In-process .NET public static method.
- *Role*: Provider.
- *Contract*: `public static WorkspaceParseResult Parse(IEnumerable<string> filePaths)` —
  loads all embedded `.sysml` stdlib files first, then reads and parses each caller-supplied
  file path. Returns a `WorkspaceParseResult` containing every parsed file path and every
  collected `SysmlDiagnostic`.
- *Constraints*: `filePaths` must not be null (`ArgumentNullException` is thrown); each path
  must be readable via `File.ReadAllText`.

**WorkspaceParser.ParseSource**: Parses a single in-memory source string.

- *Type*: In-process .NET public static method.
- *Role*: Provider.
- *Contract*: `public static IReadOnlyList<SysmlDiagnostic> ParseSource(string filePath, string content)` —
  creates a fresh `SysmlDiagnosticListener`, runs the ANTLR4 pipeline over `content`, and
  returns all diagnostics. `filePath` appears verbatim in every diagnostic produced.
- *Constraints*: None; both parameters are used as-is.

**WorkspaceParseResult.HasErrors**: Reports whether any error-severity diagnostic was collected.

- *Type*: In-process .NET public property (derived).
- *Role*: Provider.
- *Contract*: Returns `true` if and only if at least one `SysmlDiagnostic` in `Diagnostics`
  has `Severity == DiagnosticSeverity.Error`; returns `false` otherwise.

### Design

#### WorkspaceParser

`WorkspaceParser` is a static class with two public methods and one private helper. The private
`ParseSource(string filePath, string content, List<SysmlDiagnostic> diagnostics)` overload is
the central parse routine shared by both public methods:

1. Constructs a `SysmlDiagnosticListener` bound to `filePath` and the shared `diagnostics` list.
2. Creates an `AntlrInputStream` from `content`.
3. Instantiates `SysMLv2Lexer` over the input stream; removes default error listeners and
   registers the `SysmlDiagnosticListener`.
4. Wraps the lexer in a `CommonTokenStream`.
5. Instantiates `SysMLv2Parser` over the token stream; removes default error listeners and
   registers the same `SysmlDiagnosticListener`.
6. Calls `parser.rootNamespace()` to trigger full parsing; the returned CST root is discarded
   in Phase 1.

`WorkspaceParser.Parse` collects file paths and diagnostics into shared lists, then wraps them
in a `WorkspaceParseResult`. Stdlib files are always parsed first so that user-file diagnostics
appear after stdlib diagnostics in the returned list.

#### SysmlDiagnosticListener

`SysmlDiagnosticListener` implements both `IAntlrErrorListener<IToken>` (parser errors) and
`IAntlrErrorListener<int>` (lexer errors). Both interface methods delegate to the private
`Append(int line, int column, string msg)` method, which constructs a
`SysmlDiagnostic(_filePath, line, column, DiagnosticSeverity.Error, msg)` and appends it to
the shared `_diagnostics` list. All ANTLR4-reported errors are mapped to `Error` severity.

#### StdlibLoader

`StdlibLoader` is a static class with one internal method: `LoadAll()`. It enumerates
`Assembly.GetManifestResourceNames()`, filters to names that start with the computed
`ResourcePrefix` (`DemaConsulting.SysML2Tools.Stdlib.`) and end with `.sysml`
(case-insensitive). For each matching name it:

1. Derives the virtual path by replacing the resource prefix with the `[stdlib]` prefix.
2. Opens the resource stream, reads it to a string using `StreamReader`, and yields the
   `(virtualPath, content)` pair.

Resource names ending with `.kerml` are skipped; KerML parsing requires a separate grammar
and is deferred to Phase 2.

The ANTLR4-generated files in `Parser/Antlr/` (`SysMLv2Lexer.cs`, `SysMLv2Parser.cs`, and
related files) are committed to the repository and are not hand-written. They must not be
manually edited; see `Grammar/README.md` for regeneration instructions.
