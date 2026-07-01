### SysmlDiagnosticListener Verification

#### Verification Approach

`SysmlDiagnosticListener` is an `internal` class with no public surface and is verified indirectly
through `WorkspaceParserTests`. Because `WorkspaceParser` installs the listener on both the lexer
and the parser, any test that provokes a syntax error exercises the listener's error-capture and
path-attribution behavior.

#### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. Source text is supplied inline; no
files on disk, network access, or additional configuration are required beyond a standard .NET SDK
installation.

#### Acceptance Criteria

- A source containing a syntax error yields at least one `Error`-severity diagnostic, confirming
  the listener captured the ANTLR4 error.
- Each captured diagnostic carries the file path supplied by the caller, confirming attribution.

#### Test Scenarios

| Test | Assertion |
| --- | --- |
| `ParseSource_InvalidSyntax_ReportsError` | A lexer/parser error is captured as an Error diagnostic |
| `ParseSource_ErrorPath_MatchesSuppliedPath` | The captured diagnostic carries the supplied path |
