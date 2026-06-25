## Lint

### Overview

The `Lint` subsystem implements the `lint` subcommand of the `DemaConsulting.SysML2Tools.Tool`
CLI application. It accepts one or more file glob patterns, resolves them to concrete file paths,
invokes `WorkspaceParser.Parse` from the `DemaConsulting.SysML2Tools` core library, and reports
each diagnostic to the context output in a standard `path(line,col): severity: message` format.
The subsystem contains one unit: `LintCommand`.

### Interfaces

**LintCommand.Run**: Entry point for the lint subcommand.

- *Type*: In-process .NET static method.
- *Role*: Provider.
- *Contract*: `internal static void Run(Context context)` — reads `context.Files` (the list of
  glob patterns supplied as positional CLI arguments), resolves them to file paths, calls
  `WorkspaceParser.Parse`, writes each diagnostic, and calls `context.WriteError` if any
  error-severity diagnostics were found (which sets exit code 1).
- *Constraints*: If no files are resolved from the provided patterns, writes an error message
  and returns immediately without invoking the parser.

### Design

`LintCommand` is a static class containing the public `Run` method and a private `ResolveFiles`
helper.

`ResolveFiles` iterates over the provided pattern list. For each pattern it splits the path into
a directory component and a filename glob component using `Path.GetDirectoryName` and
`Path.GetFileName`. If the directory exists it calls `Directory.GetFiles(dir, glob,
TopDirectoryOnly)` to enumerate matching files. If the directory does not exist but the pattern
itself is an existing file path, it is added directly.

`Run` checks for an empty resolved file list and emits an error if no input files were found.
Otherwise it logs a `"Linting N file(s)..."` status line, calls `WorkspaceParser.Parse(files)`,
then iterates over `result.Diagnostics`. Error-severity diagnostics are written via
`context.WriteError`; all others via `context.WriteLine`. After reporting all diagnostics it
writes either a summary error count (via `context.WriteError`) or a `"lint: no errors found."`
message (via `context.WriteLine`).

The diagnostic output format is:
`{FilePath}({Line},{Column}): {severity}: {Message}`

where `{severity}` is the lowercased `DiagnosticSeverity` enum value name.

The `Lint` subsystem depends on `DemaConsulting.SysML2Tools.Parser.WorkspaceParser` from the
core library and on `Context` from the `Cli` subsystem.
