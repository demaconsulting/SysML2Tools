### DemaConsulting.SysML2Tools.Tool — Query Subsystem

#### Overview

The Query subsystem implements the `query` CLI command: a model-analysis interface exposing
11 verbs (`uses`, `used-by`, `impact`, `describe`, `hierarchy`, `requirements`, `interface`,
`connections`, `states`, `list`, `find`) over a SysML v2 workspace. It provides three
cooperating types:

- `QueryVerb` — an enum identifying which of the 11 operations was requested, plus a
  `QueryVerbParsing` helper that converts between kebab-case command-line tokens
  (e.g., `used-by`) and enum values, and reports which verbs require a target element.
- `QueryOptions` — an immutable record capturing every verb-specific option (`Element`,
  `Format`, `Depth`, `Direction`, `Kind`, `NameFilter`, `IncludeStdlib`, `Files`) parsed by
  `Context.Create` for one `query` invocation.
- `QueryCommand` — the entry-point dispatcher, mirroring `LintCommand`/`RenderCommand`'s
  `internal static class` shape with a `RunAsync(Context)` method, plus `PrintGeneralHelp`
  and `PrintVerbHelp` for `--help` rendering.

As of this release, every verb dispatches to a shared "not yet implemented" stub; real
verb logic is added incrementally in future releases (see **Stub Contract** below).

#### QueryVerb / QueryVerbParsing

##### QueryVerb Purpose

Defines the fixed, ROADMAP-specified vocabulary of 11 query verbs and centralizes the
mapping between command-line tokens and the enum, so no other type re-implements token
parsing or formatting.

##### QueryVerb Data Model

`QueryVerb` is a plain enum with no instance state. `QueryVerbParsing` is a static class
exposing `AllTokens` (the ordered list of valid tokens, used in error messages and help
text).

##### QueryVerb Key Methods

**`Parse(string token)`**: Converts a kebab-case token to a `QueryVerb`; throws
`ArgumentException` listing all valid tokens when the token is unrecognized.

**`ToToken(QueryVerb verb)`**: Converts a `QueryVerb` back to its kebab-case token, used by
`QueryCommand`'s stub message and help text.

**`RequiresElement(QueryVerb verb)`**: Returns `true` for every verb except `List` and
`Find`, which operate over the whole workspace rather than a single target element.

#### QueryOptions

##### QueryOptions Purpose

A single, flat, immutable record carrying every option relevant to any of the 11 verbs.
A shared shape (rather than one record per verb) keeps `Context.Create`'s construction
logic simple, since all 11 verbs share the same CLI grammar (verb token, then options,
then file globs) and differ only in which options are meaningful.

##### QueryOptions Data Model

- `Verb` (`QueryVerb`, required) — which operation was requested.
- `Element` (`string?`) — target qualified name from `--element`/`-e`; required for every
  verb except `list`/`find`.
- `Format` (`string?`) — `"markdown"` (default) or `"json"` from `--format`; reuses the same
  flag name as `render`'s `--format` (which instead accepts `svg`/`png`) — the two commands
  interpret the raw string independently.
- `Depth` (`int?`) — impact-walk depth bound from `--depth`, meaningful only for `impact`;
  reuses the same flag as `render`'s diagram-nesting `--depth`.
- `Direction` (`string?`) — `up`/`down`/`both` from `--direction`, meaningful only for
  `hierarchy`.
- `Kind` (`string?`) — element-kind filter from `--kind`, meaningful only for `list`/`find`.
- `NameFilter` (`string?`) — name substring filter from `--name`, meaningful only for
  `list`/`find`.
- `IncludeStdlib` (`bool`) — from `--include-stdlib`; applies to every verb.
- `Files` (`IReadOnlyList<string>`) — file glob patterns supplied after the verb token; kept
  separate from `Context.Files` (used by `lint`/`render`) so query's file handling cannot
  affect the other commands.

#### QueryCommand

##### QueryCommand Purpose

`QueryCommand.RunAsync` validates that a verb was successfully parsed and that
`--element` was supplied when required, then dispatches to the verb's stub. It also
exposes `PrintGeneralHelp`/`PrintVerbHelp` for `Program`'s help handling.

##### QueryCommand Key Methods

**`RunAsync(Context context)`**

1. Throws `ArgumentException` if `context.Query` is `null` (defensive; unreachable when
   `Program` dispatches correctly, since `Context.Create` only sets `Command = Query` and
   `Query = null` together when `--help` was requested without a verb).
2. Throws `ArgumentException` naming the verb when `QueryVerbParsing.RequiresElement` is
   `true` for the verb and `Element` is null/whitespace.
3. Dispatches via an explicit 11-arm `switch` on `options.Verb` — one arm per verb, not a
   loop — to `NotImplementedAsync`.

**`NotImplementedAsync(Context context, QueryVerb verb)`**: Calls
`context.WriteError($"query {token}: not yet implemented. ...")` and returns a completed
task. Uses `WriteError` (not throwing `NotImplementedException`) so that `Program.Main`'s
top-level handler does not treat the stub as an unexpected crash — matching the existing
`lint`/`render` convention for reporting "not ready" conditions.

**`PrintGeneralHelp(Context context)`**: Lists all 11 verbs with a one-line description and
the shared option set; used for `query --help` with no verb.

**`PrintVerbHelp(Context context, QueryVerb verb)`**: Prints a verb-specific usage line and
only the options relevant to that verb; used for `query <verb> --help`.

#### Stub Contract (for future releases)

Each of the 11 `switch` arms in `RunAsync` currently calls `NotImplementedAsync`. A future
release implements one verb at a time by replacing that verb's single arm with a call to
real analysis logic — no other arm, and no part of the validation logic above it, needs to
change. This is a deliberate design choice: the `switch` is written with 11 explicit arms
(not a dictionary/loop keyed by `QueryVerb`) specifically so that a diff implementing one
verb touches only one arm.

#### Error Handling

- `context.Query is null`: `ArgumentException` (defensive; should not occur via `Program`).
- `--element` required but missing: `ArgumentException` naming the verb token.
- Unrecognized verb token: `ArgumentException` (thrown by `QueryVerbParsing.Parse`, called
  from `Context`'s `ArgumentParser`) listing all valid tokens.
- All 11 verbs (given valid input): `context.WriteError` reporting "not yet implemented";
  `Context.ExitCode` becomes 1.

#### Dependencies

- `Context`, `SysmlCommand` (in `DemaConsulting.SysML2Tools.Cli`) — reads `Query` options;
  writes output.

#### Callers

- `Program.RunToolLogicAsync` — dispatches to `QueryCommand.RunAsync` when
  `context.Command == SysmlCommand.Query`.
- `Program.RunAsync` — dispatches to `QueryCommand.PrintGeneralHelp`/`PrintVerbHelp` for the
  `--help` case.

#### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| SysML2Tools-Tool-Query-VerbGrammar | `query` verb handling in `Context`'s `ArgumentParser`; `QueryVerbParsing.Parse` |
| SysML2Tools-Tool-Query-UnknownVerb | `QueryVerbParsing.Parse`'s `ArgumentException` path |
| SysML2Tools-Tool-Query-ElementRequired | Element-required check at the start of `QueryCommand.RunAsync` |
| SysML2Tools-Tool-Query-Format | `--format` reused from render's `RendererFormat` field; `QueryOptions.Format` |
| SysML2Tools-Tool-Query-NotImplementedStub | `NotImplementedAsync` called from each `switch` arm in `RunAsync` |
| SysML2Tools-Tool-Query-Help | `QueryCommand.PrintGeneralHelp`/`PrintVerbHelp`, called from `Program.RunAsync` |
