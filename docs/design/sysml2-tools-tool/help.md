### DemaConsulting.SysML2Tools.Tool — Help Subsystem

#### Overview

The Help subsystem implements the `help` CLI command: `sysml2tools help [command] [verb]`.
It provides three cooperating types:

- `HelpOptions` — an immutable record capturing the raw target command token (`TargetCommand`,
  one of `"lint"`/`"render"`/`"query"`, or `null` for bare `help`) and the raw target verb token
  (`TargetVerb`, meaningful only when `TargetCommand` is `"query"`) parsed by `Context.Create`
  for one `help` invocation. Mirrors the flat-immutable-record style of `QueryOptions`.
- `HelpArgumentParser` — parses the arguments following the `help` command token: an optional
  first token naming the target command, followed — only when the target is `query` — by an
  optional second token naming the verb, re-validated via `QueryVerbParsing.Parse` rather than
  duplicating the 11-verb vocabulary.
- `HelpCommand` — pure dispatch. `Run(Context)` never authors help text itself; it delegates to
  the single source of truth for each command's help text: `Program.PrintTopLevelHelp`,
  `LintCommand.PrintHelp`, `RenderCommand.PrintHelp`, or
  `QueryCommand.PrintGeneralHelp`/`PrintVerbHelp`.

#### Design Rationale — Single Source of Truth

Before this subsystem, `lint`/`render` had no command-specific `--help` output (both fell
through to the generic top-level help printed by `Program`), while `query` alone had a
`PrintGeneralHelp`/`PrintVerbHelp` pair invoked only from `Program.RunAsync`'s `--help` branch.
Introducing `help` as a genuine top-level command created a second potential place to author
help text for `lint`/`render`. To avoid duplicating help text across two entry points
(`<command> --help` and `help <command>`), this unit:

1. Adds `LintCommand.PrintHelp(Context)` and `RenderCommand.PrintHelp(Context)`, mirroring the
   existing `QueryCommand.PrintGeneralHelp`/`PrintVerbHelp` shape.
2. Makes `Program.RunAsync`'s `--help` branch command-aware for every command (not just
   `query`), so `lint --help`/`render --help` now show command-specific detail instead of the
   generic top-level help.
3. Makes `HelpCommand.Run` call the exact same four methods, so `help lint` and `lint --help`
   (and likewise for `render`/`query`) are guaranteed to produce byte-identical output — neither
   path maintains its own copy of the help text.

This is a deliberate, minimal scope expansion beyond "add a `help` command": without it, `help
lint`/`help render` would have nothing command-specific to show, defeating the purpose of a
per-command help target.

#### HelpOptions

##### HelpOptions Purpose

A minimal, flat, immutable record carrying the two fields needed to dispatch `help` — no
enum is defined for `TargetCommand` because `lint`/`render` have no enum of their own (unlike
`query`'s `QueryVerb`); reusing raw string tokens keeps `HelpOptions` symmetric with how
`GlobalArguments.Command` itself is parsed as a token before becoming a `SysmlCommand` enum
value.

##### HelpOptions Data Model

- `TargetCommand` (`string?`) — `"lint"`, `"render"`, `"query"`, or `null` for bare `help`.
- `TargetVerb` (`string?`) — a raw query verb token; only meaningful when `TargetCommand` is
  `"query"`; `null` otherwise (including when `TargetCommand` is `"query"` but no verb was
  supplied, i.e., `help query`).

#### HelpArgumentParser

##### HelpArgumentParser Purpose

Parses the `help` command's positional grammar, eagerly validating both the target command and
(when applicable) the target verb, rather than silently accepting an unrecognized token.

##### HelpArgumentParser Key Methods

**`Parse(IReadOnlyList<string> commandArgs)`**:

1. No tokens → returns `new HelpOptions()` (both fields `null`) — bare `help`.
2. First token must be one of `lint`/`render`/`query`; otherwise throws `ArgumentException`
   listing the three valid targets.
3. When the target is `query` and a second token is present, it is validated via
   `QueryVerbParsing.Parse` (reusing that method's existing error message and valid-token list —
   no duplicate vocabulary maintained here) and stored as `TargetVerb`.
4. Any further token (beyond the target command, and — for `query` — the verb) throws
   `ArgumentException($"Unsupported argument '{arg}' for the 'help' command.")`, matching the
   rejection convention shared by `LintArgumentParser`/`RenderArgumentParser`/
   `QueryArgumentParser`.

#### HelpCommand

##### HelpCommand Purpose

Pure dispatch from the parsed `HelpOptions` to the single source of truth for each command's
help text. Authors no help text of its own.

##### HelpCommand Key Methods

**`Run(Context context)`**:

- `TargetCommand == null` → `Program.PrintTopLevelHelp(context)`.
- `TargetCommand == "lint"` → `LintCommand.PrintHelp(context)`.
- `TargetCommand == "render"` → `RenderCommand.PrintHelp(context)`.
- `TargetCommand == "query"`, `TargetVerb == null` → `QueryCommand.PrintGeneralHelp(context)`.
- `TargetCommand == "query"`, `TargetVerb` set → `QueryCommand.PrintVerbHelp(context,
  QueryVerbParsing.Parse(TargetVerb))`.

#### Error Handling

- Unrecognized target command (`help bogus`): `ArgumentException` (thrown by
  `HelpArgumentParser.Parse`) listing the three valid targets.
- Unrecognized query verb (`help query bogus`): `ArgumentException` (thrown by
  `QueryVerbParsing.Parse`, reused as-is) listing all 11 valid verb tokens.
- Extra/`-`-prefixed trailing token: `ArgumentException` naming the bad token and the `help`
  command.
- All three cases propagate to `Program.Main`'s existing `ArgumentException` handler, which
  writes the message to stderr and returns exit code 1 — consistent with every other command's
  error handling; `help` introduces no new exception-handling path.

#### Interaction with `--silent`

`--silent` suppresses `help`'s output exactly as it suppresses every other command's output,
because `Context.WriteLine` unconditionally suppresses stdout when `Silent` is set, regardless
of the reason for the write — this already applies to, e.g., `--silent --version`, which
suppresses its own explicitly requested output. `HelpCommand` intentionally does not special-case
`Silent` to bypass this; doing so would make `Silent`'s behavior inconsistent and harder to
reason about across commands. A caller that needs help text programmatically while also
capturing other output via `--log` will still receive the help text in the log file, since
`WriteLine` writes to the log file unconditionally.

#### Interaction with the Global `--help` Flag

`Program.RunAsync`'s `--help` handling is unchanged in spirit but restructured to be
command-aware for every command (see **Design Rationale** above) and to check
`Command == SysmlCommand.Help` first — the `help` command always prints help and returns,
**regardless of the `context.Help` flag**, so bare `sysml2tools help` works without also
requiring `--help`. When `Command != SysmlCommand.Help` and `context.Help` is `true`, dispatch is
command-aware: `Lint`/`Render`/`Query`/`None` each print their own help via the same
single-source-of-truth methods `HelpCommand.Run` uses.

#### Dependencies

- `Context`, `SysmlCommand` (in `DemaConsulting.SysML2Tools.Cli`) — reads `HelpCommand`
  options.
- `Program.PrintTopLevelHelp`, `LintCommand.PrintHelp`, `RenderCommand.PrintHelp`,
  `QueryCommand.PrintGeneralHelp`/`PrintVerbHelp`, `QueryVerbParsing.Parse` — the single sources
  of truth this subsystem dispatches to.

#### Callers

- `Program.RunAsync` — dispatches to `HelpCommand.Run` when `context.Command ==
  SysmlCommand.Help`.

#### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| SysML2Tools-Tool-Help-BareHelp | `HelpCommand.Run`'s `null`-target branch → `Program.PrintTopLevelHelp` |
| SysML2Tools-Tool-Help-CommandHelp | `HelpCommand.Run`'s `lint`/`render` branches → respective `PrintHelp` methods |
| SysML2Tools-Tool-Help-QueryOverview | `HelpCommand.Run`'s `query` (no verb) branch → `QueryCommand.PrintGeneralHelp` |
| SysML2Tools-Tool-Help-QueryVerbHelp | `HelpCommand.Run`'s `"query"` + verb branch → `QueryCommand.PrintVerbHelp` |
| SysML2Tools-Tool-Help-UnknownTarget | `HelpArgumentParser.Parse`'s target check; reused `QueryVerbParsing.Parse` |
| SysML2Tools-Tool-Help-SilentConsistency | `Context.WriteLine`'s `Silent` suppression; no bypass in `HelpCommand` |
| SysML2Tools-Tool-Help-Grammar | `HelpArgumentParser.Parse`'s positional/extra-token validation |
| SysML2Tools-Tool-Context-HelpDispatch | `Context.Create`'s `Help` dispatch arm → `HelpArgumentParser.Parse` |
