### Context

#### Purpose

`Context` handles command-line argument parsing and program output for one tool invocation. Its
single responsibility is to parse the argument list, expose the parsed flags as read-only
properties, own the two output channels (console and log file), and derive the exit code from
whether any errors were reported.

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
supplied via `--depth`.

**MaxRenderDepth**: `int?` — Raw diagram render depth supplied via `--depth`; not clamped
to 6. `null` when `--depth` was not specified. Used by the render command as the
`DepthLimit` in `RenderOptions`; 0 is interpreted as unlimited.

**ViewName**: `string?` — View display name supplied via `--view`, or `null` if the option
was absent. Used by the render command to filter which view is rendered.

**Command**: `SysmlCommand` — `SysmlCommand.Lint` when `lint` is the first positional
argument; `SysmlCommand.Render` when `render` is the first positional argument;
`SysmlCommand.Query` when `query` is the first positional argument; `SysmlCommand.None`
otherwise.

**Files**: `IReadOnlyList<string>` — file glob patterns collected from positional arguments
after the command token. Not populated for the `query` command — see `Query.Files` instead.

**OutputDirectory**: `string?` — path supplied after `--output`, or `null` if the option
was absent. Used by the render command as the output directory for diagram files.

**RendererFormat**: `string?` — value supplied after `--format` (e.g., `"svg"` or `"png"`),
or `null` if the option was absent. Used by the render command to select the output format.
The same raw value is also exposed as `Query.Format` for the `query` command, which
interprets it as `"markdown"`/`"json"` instead — the flag name is shared but its meaning is
command-specific.

**Query**: `QueryOptions?` — populated only when `Command` is `SysmlCommand.Query` and a
recognized verb token was captured; `null` when `query` was supplied without a verb (e.g.,
`query --help`) or when a different command was selected. Carries `--element`/`-e`,
`--direction`, `--kind`, `--name`, `--include-stdlib`, plus the reused `--format`/`--depth`
values and the query-specific `Files` list.

**ExitCode**: `int` (derived) — Returns 1 if `_hasErrors` is true; returns 0 otherwise.

#### Key Methods

**Create**: Factory method that parses arguments and returns a fully initialized `Context`.

- *Parameters*: `string[] args` — raw command-line argument array.
- *Returns*: `Context` — a new instance with all flags set.
- *Preconditions*: `args` is not null.
- *Postconditions*: All flag properties reflect the parsed argument state; the log file is open
  if `--log` was supplied.

Delegates to the private `ArgumentParser` helper to parse flags, then opens the log file by
calling `OpenLogFile` if `--log` was present. For `--depth`, the raw value is stored as
`MaxRenderDepth` without clamping and `HeadingDepth` is set to `Math.Clamp(depth, 1, 6)`.
The `--view` flag stores its value in `ViewName`.
When the `query` command is selected, the first bare word following it is parsed as a verb
token via `QueryVerbParsing.Parse` (throwing `ArgumentException` listing all valid verbs on
failure) instead of being collected as a file pattern; once a verb is captured, `Create`
builds a `QueryOptions` from the parser's query-specific fields and the same `--format`/
`--depth` values already parsed for `render`, and assigns it to `Query`. This positional
verb-capture guard only activates when `Command == SysmlCommand.Query`, a value no
`lint`/`render` invocation can produce, so `lint`/`render` positional-file parsing is
unaffected.
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
or missing required value. It throws `InvalidOperationException`
("Failed to open log file '{path}': {detail}") when the `--log` file cannot be opened. Both
exceptions propagate to `Program.Main`.

`WriteLine` and `WriteError` do not throw; they write to whichever output channels are
available.

`Dispose` does not throw; any disposal errors are silently ignored.

#### Dependencies

- **.NET BCL** — `Console`, `StreamWriter`, and `Path` are the only dependencies. No other
  tool units are used.

#### Callers

- **Program** — creates `Context` via `Context.Create` and calls `WriteLine` and `WriteError`.
- **Validation** — receives `Context` from `Program` and calls `WriteLine` and `WriteError`.
- **RenderCommand** — reads `Files`, `RendererFormat`, `OutputDirectory`, `ViewName`, and
  `MaxRenderDepth`; calls `WriteLine` and `WriteError`.
- **QueryCommand** — reads `Query` (`QueryOptions`); calls `WriteLine` and `WriteError`.
