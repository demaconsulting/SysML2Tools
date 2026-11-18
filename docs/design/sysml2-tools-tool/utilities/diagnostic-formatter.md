### DiagnosticFormatter

#### Purpose

`DiagnosticFormatter` is a static utility class that formats a `SysmlDiagnostic` as a single
console line. It is the one formatter shared by the `lint`, `render`, and `query` commands, so a
diagnostic prints identically whichever command reports it. Its single responsibility is the
text shape of one diagnostic; deciding which output stream a line goes to (standard output for
warnings and info, standard error for errors) remains with each command.

#### Data Model

`DiagnosticFormatter` holds no instance state. The class is `internal static` with no fields or
properties.

#### Key Methods

**Format**: Formats one diagnostic as a console line.

- *Parameters*: `SysmlDiagnostic diagnostic` — the diagnostic to format.
- *Returns*: `string` — the single-line text, without a trailing newline or indentation.
- *Preconditions*: `diagnostic` is non-null.
- *Postconditions*: When `diagnostic.FilePath` is non-empty the result is
  `{FilePath}({Line},{Column}): {severity}: {Message}`; otherwise it is `{severity}: {Message}`.
  `{severity}` is the lowercase name of `DiagnosticSeverity` (`info`, `warning`, or `error`).
  `Line` and `Column` are both one-based (the first character of a line is column 1) — the
  convention of MSBuild, GCC, editor problem matchers, and GitHub annotations — and are printed
  exactly as carried by the diagnostic, which stores one-based values (see `SysmlDiagnostic`).

The location is omitted for a diagnostic that is not tied to a file (an empty `FilePath`)
because `(0,0)` would suggest a real position. The `path(line,col): severity: message` shape is
the compiler-style form recognized by IDEs and CI problem matchers.

#### Error Handling

`Format` throws `ArgumentNullException` for a null diagnostic. It performs no other validation:
any `FilePath`, position, severity, and message that the diagnostic carries are rendered as-is.

#### Dependencies

- **SysmlDiagnostic / DiagnosticSeverity** — the Language library's diagnostic record and
  severity enumeration (read-only).

#### Callers

- **LintCommand** — formats each workspace-load diagnostic.
- **RenderCommand** — formats each workspace-load diagnostic and each render-time diagnostic
  (for example the empty-view warning).
- **QueryCommand** — formats each workspace-load diagnostic.
