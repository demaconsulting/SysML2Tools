### Context

#### Purpose

`Context` handles command-line argument parsing and program output for one tool invocation. Its
single responsibility is to parse the argument list, expose the parsed flags as read-only
properties, own the two output channels (console and log file), and derive the exit code from
whether any errors were reported.

Argument parsing is split into two stages so that each command rejects flags outside its own
grammar, rather than sharing one mega-switch across `lint`/`render`/`query`:

1. **`GlobalArgumentParser`** parses the cross-cutting options that apply regardless of which
   command (or no command) is selected, and identifies the selected command token
   (`lint`/`render`/`query`/`help`). Everything else is collected, in original order, into
   `GlobalArguments.CommandArgs` for stage 2.
2. Exactly one **per-command parser** — `LintArgumentParser`, `RenderArgumentParser`,
   `QueryArgumentParser`, or `HelpArgumentParser` — interprets `CommandArgs` according to that
   command's own grammar, rejecting any flag it does not recognize with an `ArgumentException`
   naming both the flag and the command.

**Design decision — walk/nesting depth is per-command-scoped:** `render`'s diagram nesting
depth and `query`'s `impact`-walk depth are both parsed locally, as `--walk-depth`, by
`RenderArgumentParser`/`QueryArgumentParser` respectively — an ordinary command-scoped flag
like any other, with no special-cased global plumbing. The global `--depth` flag is a
separate, unrelated concept: Markdown heading depth, feeding `Context.HeadingDepth`. It must
work with **no command at all** (`sysml2tools --validate --depth 2`, which adjusts
`HeadingDepth` for the self-validation report), and `query` also reads it directly
(`Context.HeadingDepth`) for its own Markdown report heading depth — this is the only
cross-command reuse of `--depth`, and it is a direct read of the already-parsed global value,
not a second parse.

**Design decision — `--heading` is intentionally query-scoped, not global:** unlike `--depth`,
the `--heading <text>` override (custom Markdown heading text) is parsed entirely within
`QueryArgumentParser` and stored on `QueryOptions.Heading`; it is **not** a `Context`/
`GlobalArgumentParser` concept and has no effect on `--validate`. This is deliberate: `--depth`
needed global scope because `--validate`'s self-validation report predates `query` and already
used the shared `Context.HeadingDepth` flag, but a custom heading *title* is only meaningful for
`query`'s report-style output (e.g. pasting a `query dependencies` result into a design doc under
a caller-chosen heading) — `--validate`'s self-validation summary has no equivalent "custom title"
use case, so promoting `--heading` into the shared global layer would add unused surface area to
`lint`/`render`/`--validate` for no current benefit.

#### Data Model

**_logWriter**: `StreamWriter?` — Log file writer; `null` when logging is not active.

**_hasErrors**: `bool` — Set to `true` on the first `WriteError` call; once set, cannot return
to `false` within the same invocation.

**Version**: `bool` — `true` when `-v` or `--version` was present in the argument list.

**Help**: `bool` — `true` when `-?`, `-h`, or `--help` was present in the argument list.

**Silent**: `bool` — `true` when `--silent` was present in the argument list.

**Validate**: `bool` — `true` when `--validate` was present in the argument list.

**ResultsFile**: `string?` — Path supplied after `--results` or `--result`, or `null` if
neither flag was present.

**HeadingDepth**: `int` — Heading depth for markdown output; valid range 1–6, default 1;
supplied via `--depth`. Parsed by `GlobalArgumentParser` (see design decision above); read
directly by `SelfTest/Validation.cs` (self-validation report) and `QueryCommand.RunAsync`
(query's Markdown report heading depth).

**Command**: `SysmlCommand` — `SysmlCommand.Lint` when `lint` is the first recognized command
token; `SysmlCommand.Render` when `render` is the first recognized command token;
`SysmlCommand.Query` when `query` is the first recognized command token; `SysmlCommand.Help`
when `help` is the first recognized command token; `SysmlCommand.None` otherwise. Defined in
its own file, `Cli/SysmlCommand.cs`.

**Lint**: `LintOptions?` — populated only when `Command` is `SysmlCommand.Lint`. Carries the
`Files` glob-pattern list; `lint` recognizes no flags of its own.

**Render**: `RenderCommandOptions?` — populated only when `Command` is `SysmlCommand.Render`.
Carries `OutputDirectory`, `Format` (`"svg"`/`"png"`, validated by `RenderCommand.RunAsync` — not
at parse time), `ViewName`, `AutoView`, `WalkDepth` (diagram nesting depth limit, command-scoped),
and `Files`. Named `RenderCommandOptions` rather than
`RenderOptions` to avoid colliding with `DemaConsulting.Rendering.Abstractions.RenderOptions`,
the off-the-shelf rendering-library type already used inside `RenderCommand`.

**Query**: `QueryOptions?` — populated only when `Command` is `SysmlCommand.Query` and a
recognized verb token was captured; `null` when `query` was supplied without a verb and
`--help` was requested (e.g., `query --help`), or when a different command was selected.
Carries `--element`/`-e`, `--direction`, `--kind`, `--name`, `--include-stdlib`, the query's own
`--format` (`"markdown"`/`"json"`, a value independent of `render`'s `--format` even though the
flag name is shared), `WalkDepth` (impact-walk depth, command-scoped, parsed from `query`'s own
`--walk-depth`), `Heading` (custom Markdown heading text from `--heading`), and the
query-specific `Files` list.

**HelpCommand**: `HelpOptions?` — populated only when `Command` is `SysmlCommand.Help`. Named
`HelpCommand` (not `Help`) to avoid colliding with the existing `Help` flag property, which
reflects the global `-h`/`-?`/`--help` flag independently of which command token was recognized.
Carries `TargetCommand` (`string?` — `"lint"`/`"render"`/`"query"`, or `null` for bare `help`)
and `TargetVerb` (`string?` — meaningful only when `TargetCommand` is `"query"`).

**ExitCode**: `int` (derived) — Returns 1 if `_hasErrors` is true; returns 0 otherwise.

#### Key Methods

**Create**: Factory method that parses arguments and returns a fully initialized `Context`.

- *Parameters*: `string[] args` — raw command-line argument array.
- *Returns*: `Context` — a new instance with all flags set.
- *Preconditions*: `args` is not null.
- *Postconditions*: All flag properties reflect the parsed argument state; the log file is open
  if `--log` was supplied.

`Create` calls `GlobalArgumentParser.Parse(args)` to obtain a `GlobalArguments` instance, then
switches on `GlobalArguments.Command` to dispatch to exactly one per-command parser:

- `SysmlCommand.Lint` → `LintArgumentParser.Parse(global.CommandArgs)` → `Lint`.
- `SysmlCommand.Render` → `RenderArgumentParser.Parse(global.CommandArgs)` → `Render`.
- `SysmlCommand.Query` → `QueryArgumentParser.Parse(global.CommandArgs, global.Help)`
  → `Query`. The `query` grammar is **structural**: the first token after the `query` command
  token must be a recognized verb (validated eagerly via `QueryVerbParsing.Parse`, which lists all
  valid tokens on failure); when no verb token is present, parsing returns `null` if `--help` was
  requested, otherwise throws a clear `ArgumentException` ("query: a verb is required...") rather
  than silently leaving `Query` null.
- `SysmlCommand.None` → no per-command parser runs; any leftover `-`-prefixed token in
  `global.CommandArgs` throws `ArgumentException("Unsupported argument '{arg}'")`, preserving the
  historical bare-invocation error behavior.
- `SysmlCommand.Help` → `HelpArgumentParser.Parse(global.CommandArgs)` → `HelpCommand`. The
  `help` grammar is purely positional: an optional target command (`lint`/`render`/`query`),
  followed — only when the target is `query` — by an optional verb token, re-validated via
  `QueryVerbParsing.Parse` rather than duplicating the verb vocabulary.

Each per-command parser rejects any flag outside its own recognized set with
`ArgumentException($"Unsupported argument '{arg}' for the '{command}' command.")` — e.g.,
`lint --auto` or `render --kind foo` or `query describe --auto` all fail clearly instead of being
silently accepted or misinterpreted, which was possible under the previous single shared switch.

After dispatch, `Create` opens the log file by calling `OpenLogFile` if `--log` was present.
Throws `ArgumentException` for unknown or malformed arguments; throws
`InvalidOperationException` if the log file cannot be opened.

**WriteLine**: Writes a message to standard output and to the log file.

- *Parameters*: `string message` — the message to write.
- *Returns*: `void`.
- *Preconditions*: None.
- *Postconditions*: Message is on stdout (unless `Silent`) and in the log file (if open).

**WriteError**: Writes an error message, sets the error state, and records to the log file.

- *Parameters*: `string message` — the error message.
- *Returns*: `void`.
- *Preconditions*: None.
- *Postconditions*: `_hasErrors` is true; message is on stderr in red (unless `Silent`) and in
  the log file (if open).

**Dispose**: Disposes the log file writer.

- *Parameters*: None.
- *Returns*: `void`.
- *Preconditions*: None.
- *Postconditions*: `_logWriter` is disposed and set to null; any buffered log content is
  flushed.

#### Error Handling

`Create` throws `ArgumentException` ("Unsupported argument '{arg}'") for any unrecognized flag
at the global (no-command) scope, or one of the per-command variants
("Unsupported argument '{arg}' for the '{command}' command.") when a command-scoped flag does
not belong to the active command's grammar. It throws `InvalidOperationException`
("Failed to open log file '{path}': {detail}") when the `--log` file cannot be opened. Both
exceptions propagate to `Program.Main`.

`WriteLine` and `WriteError` do not throw; they write to whichever output channels are
available.

`Dispose` does not throw; any disposal errors are silently ignored.

#### Dependencies

- **.NET BCL** — `Console`, `StreamWriter`, and `Path` are the only dependencies.
- **`CliArgumentHelpers`** — shared value-extraction primitives (`GetRequiredStringArgument`,
  `GetRequiredIntArgument`) used by `GlobalArgumentParser` and every per-command parser. This is
  the one deliberate piece of DRY sharing across parsers; command scoping/dispatch itself is not
  shared.
- **`LintOptions`/`LintArgumentParser`**, **`RenderCommandOptions`/`RenderArgumentParser`**,
  **`QueryOptions`/`QueryArgumentParser`**, **`HelpOptions`/`HelpArgumentParser`** — the
  per-command option records and parsers dispatched to by `Create`.

#### Callers

- **Program** — creates `Context` via `Context.Create` and calls `WriteLine` and `WriteError`.
- **Validation** — receives `Context` from `Program` and calls `WriteLine` and `WriteError`.
- **LintCommand** — reads `Lint` (`LintOptions`); calls `WriteLine` and `WriteError`.
- **RenderCommand** — reads `Render` (`RenderCommandOptions`); calls
  `WriteLine` and `WriteError`.
- **QueryCommand** — reads `Query` (`QueryOptions`); calls `WriteLine` and `WriteError`.
- **HelpCommand** — reads `HelpCommand` (`HelpOptions`); dispatches to
  `Program.PrintTopLevelHelp`/`LintCommand.PrintHelp`/`RenderCommand.PrintHelp`/
  `QueryCommand.PrintGeneralHelp`/`PrintVerbHelp`, all of which call `WriteLine`.
