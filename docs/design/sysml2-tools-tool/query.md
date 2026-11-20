## DemaConsulting.SysML2Tools.Tool — Query Subsystem

![Tool Query Structure](ToolQueryView.svg)

### Overview

The Tool Query subsystem is now a thin CLI adapter over Core's public Query API. It owns only
CLI-specific concerns: parsing `query` command tokens, extracting the Tool-only `--output` and
`--strict-unresolved` flags, resolving file globs, loading the workspace, resolving `--element`,
writing stdout or a
single output file, and printing localized help text. Verb semantics, the shared `QueryResult`
output model, deterministic Markdown/JSON rendering, JSON serialization, and the file-writing
helpers themselves live in `docs/design/sysml2-tools-core/query.md`.

This subsystem contains three cooperating types:

- `QueryCliArgumentParser` — pre-scans for the Tool-only `--output <file>` and boolean
  `--strict-unresolved` flags, removes those tokens, then delegates the remaining grammar to Core's
  `QueryArgumentParser`.
- `QueryCommand` — the CLI entry point. It validates CLI-only rules, resolves globs, loads the
  workspace, looks up the target element, delegates execution to `QueryEngine.Execute`, and then
  either writes rendered output to stdout or to `Context.QueryOutput`.
- `QueryStrings` — the culture-aware `.resx` accessor used by `PrintGeneralHelp` and
  `PrintVerbHelp`.

### Interfaces

```mermaid
flowchart TD
    Context --> QueryCliArgumentParser
    QueryCliArgumentParser --> QueryArgumentParser
    Context --> QueryCommand
    QueryCommand --> GlobFileCollector
    QueryCommand --> WorkspaceLoader
    QueryCommand --> QueryEngine
    QueryCommand --> QueryResultRenderer
    QueryCommand --> QueryResultExporter
    QueryCommand --> QueryStrings
```

**`QueryCliArgumentParser`**: Tool-only wrapper around the Core parser.

- *Type*: Static class.
- *Role*: Provider.
- *Contract*: `Parse(IReadOnlyList<string> commandArgs, bool helpRequested)` returns the 4-tuple
  `(Options, Files, Output, StrictUnresolved)`: the parsed `QueryOptions`, trailing file-glob
  patterns, the optional output-file path, and whether `--strict-unresolved` was supplied.

**`QueryCommand`**: CLI command orchestrator.

- *Type*: Static class.
- *Role*: Provider.
- *Contract*: `RunAsync(Context)` executes the command; `PrintGeneralHelp` and `PrintVerbHelp`
  render localized help.

**`QueryStrings`**: Localized help-text accessor.

- *Type*: Static class.
- *Role*: Provider.
- *Contract*: `ResourceManager`-backed properties expose every general-help line,
  verb-specific option line, example invocation, and schema hint printed by `QueryCommand`.

### Design

1. `Context.Create` delegates the `query` command's remaining tokens to
   `QueryCliArgumentParser.Parse`. `QueryCliArgumentParser` strips the Tool-only
   `--output <file>` pair and the `--strict-unresolved` flag, delegates the rest to Core's
   `QueryArgumentParser`, and stores the
   results on `Context.Query`, `Context.QueryFiles`, `Context.QueryOutput`, and
   `Context.QueryStrictUnresolved`.
2. `QueryCommand.RunAsync` performs the CLI-only validation that remains outside Core:
   `--element` is required for element-scoped verbs, `find` requires at least one of
   `--kind`/`--name`, and `--format` must be `markdown` or `json`.
3. The command resolves `Context.QueryFiles` through `GlobFileCollector.Collect`, then loads the
   semantic workspace with `StdlibProvider.GetSymbolTable()` and
   `WorkspaceLoader.LoadAsync(files, stdlibTable, context.QueryStrictUnresolved)`.
   Each load diagnostic is formatted by the shared `DiagnosticFormatter.Format` — the same
   `file(line,col): severity: message` line `lint` and `render` print, with no indentation — and
   written via `context.WriteError` (errors) or `context.WriteLine` (others). Load errors are
   reported and processing continues, so the query still runs and writes its output, but the exit
   code is 1 when any error was reported; unresolved references are warnings by default and
   errors with `--strict-unresolved`.
4. For verbs that require a target element, `QueryCommand` resolves `QueryOptions.Element`
   through `workspace.Declarations.TryGetValue`. Missing elements are reported as a CLI error;
   Core never receives a `null` element from this path.
5. The command delegates verb execution to `QueryEngine.Execute`, so the CLI and every library
   caller share the same public Core dispatcher and verb implementations.
6. `QueryCommand` renders results through `QueryResultRenderer.RenderMarkdown` or
   `RenderJson`. Markdown heading depth still comes from the global `Context.HeadingDepth`
   option, and custom heading text still comes from `QueryOptions.Heading`.
7. `PrintGeneralHelp` and `PrintVerbHelp` remain the single source of truth for `query --help`
   and `query <verb> --help`. Every printed line is sourced from `QueryStrings`, including the
   `--output` help text, the workflow note, and the per-verb example/schema-hint enrichment.

#### Pass-Through Options

- `QueryCliArgumentParser` pre-extracts only `--output` and `--strict-unresolved`; every other token
  is forwarded verbatim
  to Core's `QueryArgumentParser`, and `Context` stores the resulting `QueryOptions` whole.
  Adding a new Core option therefore requires no parsing change in this subsystem.
- `--include-connections` (connection-aware `impact` analysis, Core's
  `QueryOptions.IncludeConnections`) is exactly such an option: the only Tool-side work is help
  text, printed from four resx keys. `PrintGeneralHelp` writes the single line
  `Query_GeneralOptionIncludeConnections` immediately after the `--include-stdlib` line:

  ```text
    --include-connections        Also follow connect/bind edges ('impact' verb only)
  ```

  `PrintVerbHelp`'s `QueryVerb.Impact` arm writes the three-line block
  `Query_OptionIncludeConnectionsImpact1`, `Query_OptionIncludeConnectionsImpact2`, and
  `Query_OptionIncludeConnectionsImpact3` immediately after `Query_OptionWalkDepthImpact`:

  ```text
    --include-connections         Also follow connect/bind edges, undirected
                                  (default: reference edges only). Distance
                                  is bounded by --walk-depth like any edge.
  ```

  All four keys live in `QueryStrings.resx` with matching `QueryStrings` accessor properties, as
  the resx-parity tests require.
- Because the Tool does not parse the flag itself, keeping this help text in lockstep with
  Core's accepted grammar is a Tool-subsystem responsibility, verified by one general-help test
  and one `impact`-verb-help test; a Core-only option with no Tool help line would be silently
  undiscoverable.

#### Strict-Unresolved Option

- `--strict-unresolved` is a Tool-only boolean flag, extracted by `QueryCliArgumentParser` and stored
  on `Context.QueryStrictUnresolved`; Core's public `QueryArgumentParser` and `QueryOptions` are
  unchanged and unaware of it, because it controls workspace loading, not query semantics.
- `PrintGeneralHelp` writes `Query_GeneralOptionStrictUnresolved1` and
  `Query_GeneralOptionStrictUnresolved2` (the second line states exit code 1 for models that have
  unresolved references) after the `--output` lines, and
  every `PrintVerbHelp` arm writes the same two keys after its `--output` line, so every `query` help
  screen lists the flag.

#### Output File Option

- `Context.QueryOutput` is a **file path** (not a directory) from `--output`; when omitted,
  output goes to stdout. This differs in meaning from `render`'s `--output`, which names an
  output *directory* for per-view files — the flag name is reused deliberately (avoiding a
  differently-named flag for "where does output go") but the meaning is documented explicitly
  in both the resource text and help output to prevent confusion between the two commands.
- When `Context.QueryOutput` is non-null, `QueryCommand.RunAsync` creates the parent directory,
  then calls `QueryResultExporter.WriteMarkdownAsync` or `WriteJsonAsync` instead of writing to
  stdout.
- Write failures are caught at the CLI boundary. `IOException`, `UnauthorizedAccessException`,
  and `NotSupportedException` are translated to
  `query <verb>: failed to write output file '<path>': <message>`.
- On success, the command reports `query <verb>: wrote output to '<path>'.`.

### Design Constraints

- The Tool subsystem shall not re-implement verb semantics, result rendering, JSON
  serialization, or Markdown-only name shortening; those are single-source-of-truth Core
  responsibilities documented in `docs/design/sysml2-tools-core/query.md`.
- `QueryCliArgumentParser` is intentionally aware of `--output` and `--strict-unresolved`; Core's
  public `QueryArgumentParser` is intentionally unaware of them.
- Workspace loading, glob resolution, element lookup, and console/log/file reporting remain in
  the Tool layer because they depend on `Context`, filesystem conventions, and CLI error
  phrasing.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| SysML2Tools-Tool-Query-VerbGrammar | `QueryCliArgumentParser.Parse`; Core `QueryArgumentParser.Parse` |
| SysML2Tools-Tool-Query-UnknownVerb | Core `QueryVerbParsing.Parse`, surfaced by `QueryCliArgumentParser.Parse` |
| SysML2Tools-Tool-Query-ElementRequired | Element-required check at the start of `QueryCommand.RunAsync` |
| SysML2Tools-Tool-Query-Format | `QueryCommand.RunAsync` format validation |
| SysML2Tools-Tool-Query-NoFilesMatched | Zero-resolved-files guard in `QueryCommand.RunAsync` |
| SysML2Tools-Tool-Query-OutputFormat | `QueryCommand.RunAsync`; `QueryResultRenderer.RenderMarkdown`/`RenderJson` |
| SysML2Tools-Tool-Query-ElementNotFound | Element-lookup check in `QueryCommand.RunAsync` |
| SysML2Tools-Tool-Query-Find | `QueryCommand.RunAsync`'s `find`-filter validation; Core `QueryEngine.Find` dispatch |
| SysML2Tools-Tool-Query-Help | `QueryCommand.PrintGeneralHelp`; `QueryCommand.PrintVerbHelp` |
| SysML2Tools-Tool-Query-LocalizableHelpText | `QueryStrings` accessor, used by `PrintGeneralHelp` and `PrintVerbHelp` |
| SysML2Tools-Tool-Query-ReportHeading | `HeadingDepth`; `QueryOptions.Heading`; `QueryResultRenderer.RenderMarkdown` |
| SysML2Tools-Tool-Query-HelpEnrichment | `QueryStrings.GetExample`/`Query_SchemaHint_*`; workflow-note lines |
| SysML2Tools-Tool-Query-OutputFile | `QueryCliArgumentParser.Parse`; `QueryCommand.RunAsync`; `QueryResultExporter` |
| SysML2Tools-Tool-Query-IncludeConnections | `QueryCliArgumentParser.Parse` pass-through to Core; `Context.Query` |
| SysML2Tools-Tool-Query-IncludeConnectionsHelp | `QueryStrings` keys via `PrintGeneralHelp` and `PrintVerbHelp` |
| SysML2Tools-Tool-Query-LoadDiagnosticFormat | Load-diagnostic reporting in `QueryCommand.RunAsync` |
