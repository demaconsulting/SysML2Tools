## Lint

### Overview

The `Lint` subsystem implements the `lint` subcommand of the `DemaConsulting.SysML2Tools.Tool`
CLI application. It accepts one or more file glob patterns, delegates their resolution to the
shared `GlobFileCollector` (`DemaConsulting.SysML2Tools.Io`, Core `Io` subsystem), invokes
`WorkspaceLoader.LoadAsync` from the `DemaConsulting.SysML2Tools` core library, and reports each
diagnostic to the context output in a standard `path(line,col): severity: message` format. The
subsystem contains one unit: `LintCommand`.

### Interfaces

**LintCommand.RunAsync**: Entry point for the lint subcommand.

- *Type*: In-process .NET static async method.
- *Role*: Provider.
- *Contract*: `internal static async Task RunAsync(Context context)` — reads
  `context.Lint!.Files` (the list of glob patterns supplied as positional CLI arguments,
  parsed by `LintArgumentParser` and exposed via `context.Lint`), resolves them to file
  paths via `GlobFileCollector.Collect`, awaits `WorkspaceLoader.LoadAsync`, writes each
  diagnostic, and calls `context.WriteError` if any error-severity diagnostics were found
  (which sets exit code 1).
- *Constraints*: If no files are resolved from the provided patterns, writes an error message
  and returns immediately without invoking the loader.

### Design

`LintCommand` is a static class containing the public `RunAsync` method.

`RunAsync` reads its options from `context.Lint`, a `LintOptions` instance populated by
`Cli.LintArgumentParser` when `Context.Create` dispatches to the `lint` command. `lint`
recognizes no flags of its own; every argument after the `lint` command token is a file glob
pattern, and `LintArgumentParser` rejects any `-`-prefixed token (e.g., `lint --auto file.sysml`)
with `ArgumentException($"Unsupported argument '{arg}' for the 'lint' command.")` — flags that
belong to `render` or `query` are never silently accepted by `lint`.

`RunAsync` resolves `options.Files` by calling `GlobFileCollector.Collect(options.Files,
[".sysml", ".kerml"], Directory.GetCurrentDirectory())` — the same call every other command uses
(see `docs/design/sysml2-tools-core/io.md`). This single delegation replaces the subsystem's
former hand-rolled, single-directory-only resolver, and adds recursive `**` matching and `!`
exclusion support that the prior implementation lacked.

`RunAsync` checks for an empty resolved file list and emits an error if no input files were
found. Otherwise it logs a `"Linting N file(s)..."` status line, awaits
`WorkspaceLoader.LoadAsync(files, stdlibTable)`, then iterates over `result.Diagnostics`.
Error-severity diagnostics are written via `context.WriteError`; all others via
`context.WriteLine`. After reporting all diagnostics it writes either a summary error count
(via `context.WriteError`) or a `"lint: no errors found."` message (via `context.WriteLine`).

The diagnostic output format is:
`{FilePath}({Line},{Column}): {severity}: {Message}`

where `{severity}` is the lowercased `DiagnosticSeverity` enum value name.

`LintCommand.PrintHelp(Context context)` prints `lint`'s usage line and a note that it accepts
no flags of its own. This is the single source of truth for both `lint --help` (dispatched from
`Program.RunAsync`'s command-aware help block) and `help lint` (dispatched from
`Help.HelpCommand.Run`); neither entry point duplicates the help text. Every line printed by
`PrintHelp` is sourced from `LintStrings`, a hand-written, culture-aware `ResourceManager`
accessor over `Lint/LintStrings.resx` — see `docs/design/sysml2-tools-tool/program.md`'s
"Localization / Resource Strings" section for the rationale (hand-written vs. Visual
Studio-generated accessor) and the zero-code-change future-locale story, which applies
identically here.

The `Lint` subsystem depends on `DemaConsulting.SysML2Tools.Semantic.WorkspaceLoader` and
`DemaConsulting.SysML2Tools.Io.GlobFileCollector` from the core library and on
`Context`/`LintOptions`/`LintArgumentParser` from the `Cli`/`Lint` subsystems.
