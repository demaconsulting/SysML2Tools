## Program

![Tool Structure](ToolView.svg)

### Purpose

`Program` is the entry point and execution orchestrator for the SysML2 Tools. Its single
responsibility is to create a `Context` from the command-line arguments, dispatch to the
appropriate handler based on the parsed flags, and return the exit code.

### Data Model

**Version**: `string` (static property) — The tool version read from
`AssemblyInformationalVersionAttribute` on every access, falling back to `AssemblyVersion`, then
`"0.0.0"`. No caching is applied; callers that need the value more than once should store it
locally.

### Key Methods

**Main**: Entry point for the tool process.

- *Parameters*: `string[] args` — command-line arguments from the host environment.
- *Returns*: `int` — exit code; 0 for success, 1 for expected errors, or the exit code of the
  help command (see *Help Command* in the system design).
- *Preconditions*: None.
- *Postconditions*: Exit code reflects whether any errors were reported during execution.

First offers the arguments to `HelpCommand.Default.TryRun`; when the first argument is `help`, the help command answers
from the help cards and `Main` returns its exit code without creating a `Context`, so `--silent` and `--log` do not
apply to it. Otherwise creates a `Context` using `Context.Create(args)`, calls `Run(context)`, and returns
`context.ExitCode`. Catches `ArgumentException` and `InvalidOperationException` — writes `"Error: {message}"` to stderr
and returns 1. Catches any other `Exception` — writes `"Unexpected error: {message}"` to stderr and re-throws so the
runtime can record it.

**Run**: Dispatches execution based on parsed flags.

- *Parameters*: `Context context` — the parsed context.
- *Returns*: `void`.
- *Preconditions*: `context` is not null.
- *Postconditions*: Exactly one handler has been called.

Inspects flags in priority order: (1) if `context.Version` is true, calls
`context.WriteLine(Version)` and returns; (2) calls `PrintBanner`; (3) help dispatch (see
below) and returns if triggered; (4) if `context.Validate` is true, calls
`Validation.Run(context)`; (5) otherwise calls `RunToolLogic(context)`.

**Help dispatch** (within `Run`/`RunAsync`, not a separate method): if `context.Help` is true,
dispatch is command-aware: `SysmlCommand.Lint` → `LintCommand.PrintHelp`;
`SysmlCommand.Render` → `RenderCommand.PrintHelp`; `SysmlCommand.Query` →
`QueryCommand.PrintGeneralHelp`/`PrintVerbHelp` (depending on whether a verb was parsed);
`SysmlCommand.Export` → `ExportCommand.PrintHelp`; otherwise → `PrintTopLevelHelp`. The
`help` command itself never reaches `Run`: `Main` answers it from the help cards before the
options are parsed.

**PrintBanner**: Writes the tool name, version, and copyright line to `context`.

- *Parameters*: `Context context` — output target.
- *Returns*: `void`.

**PrintTopLevelHelp**: Writes the top-level usage synopsis, the `sysml2tools help [topic]` usage
line (`TopLevel_UsageHelp`), the command list, and the global options table to `context`, and
ends with a blank line and `HelpCommand.Default.Footer`, the pointer to the help command. The
`lint` command line reads "Parse files and report syntax errors and unresolved references", and the
options list ends with `--strict-unresolved` (reports unresolved references as errors, not
warnings; for `lint`, `render`, `query`, and `export`), sourced from the
`TopLevel_CommandLint` and `TopLevel_OptionStrictUnresolved` resource strings.

- *Parameters*: `Context context` — output target.
- *Returns*: `void`.

**RunToolLogic**: Placeholder for main tool logic; writes a demo message to `context`.
Downstream projects replace this method body with actual tool behavior.

- *Parameters*: `Context context` — output target.
- *Returns*: `void`.

### Error Handling

`Main` handles errors at two levels. Expected errors (`ArgumentException` and
`InvalidOperationException`) are written to stderr as `"Error: {message}"` and cause exit
code 1 without a stack trace. Unexpected errors (`Exception`) are written to stderr as
`"Unexpected error: {message}"` and re-thrown so the runtime can record them in event logs.
`Run`, `PrintBanner`, `PrintHelp`, and `RunToolLogic` do not catch exceptions; all errors
propagate to `Main`.

### Dependencies

- **Context** — `Program` reads parsed flags from `Context` and calls `Context.WriteLine` and
  `Context.WriteError` for all output.
- **Validation** — `Program.Run` calls `Validation.Run(context)` when the `--validate` flag is
  set.
- **HelpCommand** — `Program.Main` calls `HelpCommand.Default.TryRun` before `Context.Create`,
  and `PrintTopLevelHelp` ends with `HelpCommand.Default.Footer`.

### Localization / Resource Strings

`PrintBanner`/`PrintTopLevelHelp` and the "No command specified" message in
`RunToolLogicAsync` no longer contain literal string content: every line is sourced from
`ProgramStrings`, a hand-written, culture-aware accessor over `ProgramStrings.resx`. The
accessor is hand-written (a thin static class with one `ResourceManager` field and one
`public static string` property per resx key) rather than generated by the Visual Studio
"ResXFileCodeGenerator" custom tool, because that generator only runs inside Visual Studio —
a hand-written class builds identically under `dotnet build`/CI on every target framework
(net10.0), with or without an IDE. Adding a future locale (e.g., French)
requires zero code changes: adding `ProgramStrings.fr.resx` with the same key names is
enough — the .NET SDK's default resx-embedding behavior produces a satellite assembly that
`ResourceManager` discovers automatically based on the current UI culture, falling back to
this invariant (English) resource set otherwise. `Lint`, `Render`, and `Query` follow the
identical pattern for their own help text (`LintStrings`, `RenderStrings`, `QueryStrings`);
see each subsystem's own design document for details.

### Callers

N/A - entry point, called by the host environment.
