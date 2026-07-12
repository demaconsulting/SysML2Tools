### DemaConsulting.SysML2Tools.Tool — Export Subsystem

#### Overview

The Export subsystem implements the `export` CLI command: a single-purpose command that
loads a workspace and dumps its resolved semantic model — every declaration, every semantic
edge, and every diagnostic — as JSON or JSON Lines (JSONL). Unlike `query`, which summarizes
a targeted analysis question into a small `QueryResult`, `export` is a lossless, faithful
dump of the whole model intended for offline/AI-assisted bulk analysis. It provides six
cooperating types:

- `ExportOptions` — an immutable record capturing `Format`, `Output`, `IncludeStdlib`,
  `Target`, `FilterExpression`, and `Files`, parsed by `Context.Create` for one `export`
  invocation.
- `ExportArgumentParser` — parses `--format`, `--output`, `--include-stdlib`, `--target`,
  `--filter`, and positional file globs, rejecting any other `-`-prefixed token, mirroring
  `LintArgumentParser`/`RenderArgumentParser`'s shape.
- `ExportCommand` — the entry-point dispatcher, mirroring `LintCommand`/`RenderCommand`'s
  `internal static class` shape with a `RunAsync(Context)` method and a `PrintHelp` method.
  Loads the workspace, applies the `--include-stdlib` filter, then `--target` subtree scoping
  and `--filter` expression narrowing (see **Target Scoping** and **Filter Narrowing**
  below), builds an `ExportResult`, and renders it as JSON or JSONL to `--output` or stdout.
- `ExportResult` — the envelope record (`Declarations`, `Edges`, `Diagnostics`) reusing the
  existing `SysmlNode`/`SysmlEdge`/`SysmlDiagnostic` model types directly rather than
  introducing a fourth parallel result shape.
- `ExportDeclarationLine` / `ExportEdgeLine` / `ExportDiagnosticLine` — small per-line wrapper
  records used only by the JSONL rendering path, each carrying a `"Kind"` discriminator
  (`"declaration"`/`"edge"`/`"diagnostic"`) plus the wrapped payload.
- `ExportResultSerializerContext` / `ExportLineSerializerContext` — two source-generated
  `JsonSerializerContext` classes (see **Serialization Contexts** below).

#### Command Semantics

`ExportCommand.RunAsync` performs the same file-resolution and workspace-loading sequence as
`query`/`render` (`GlobFileCollector.Collect`, `StdlibProvider.GetSymbolTable()`,
`WorkspaceLoader.LoadAsync`), then:

1. Filters `workspace.Declarations` down to entries whose qualified-name key is visible per
   `IsVisible` (see **Stdlib Filtering** below), producing the `ExportResult.Declarations`
   dictionary (still keyed by qualified name, so the JSON `Declarations` object round-trips
   the workspace's own lookup key, not just an implicit array index).
2. Filters `workspace.Index.AllEdges` down to edges whose `Source` and `Target` are both
   visible per `IsVisible`, producing `ExportResult.Edges`.
3. When `--target` is supplied: resolves its containment-subtree "subject" qualified names
   (see **Target Scoping** below), reporting a clean "not found" error and returning (no
   export produced) when the target does not resolve to a visible declaration; otherwise
   narrows `Declarations`/`Edges` (from steps 1–2) to that subtree, requiring — for edges —
   both endpoints (when the source is non-null) to lie within it.
4. When `--filter` is supplied: parses it via `FilterExpressionParser.Parse`; on success,
   narrows the (possibly `--target`-scoped) `Declarations` to the matched subset via
   `FilterExpressionEvaluator.Evaluate`, and re-derives `Edges` to keep only edges whose
   endpoints (when the source is non-null) both survive in the narrowed declaration set (see
   **Filter Narrowing** below); on parse failure, appends a synthetic warning
   `SysmlDiagnostic` to the diagnostics list and prints a matching console warning, falling
   back to the unfiltered (but still `--target`-scoped, if applicable) result.
5. Copies `workspace.Diagnostics` (plus any synthetic `--filter`-failure diagnostic from step
   4) into `ExportResult.Diagnostics` — diagnostics are never stdlib-filtered (see **Stdlib
   Filtering** below).
6. Renders the resulting `ExportResult` as an indented JSON document (`--format json`,
   default) or as JSONL (`--format jsonl`) — see **Output Model** below.
7. Writes the rendered text to the file named by `--output` (via `File.WriteAllText`) when
   present, or to stdout via `context.WriteLine` otherwise.

##### Stdlib Filtering

`ExportCommand` defines its own `private static bool IsVisible(string qualifiedName,
SysmlWorkspace workspace, bool includeStdlib) => includeStdlib ||
!workspace.StdlibNames.Contains(qualifiedName);` — a verbatim copy of the Tool project's
`Query.QueryEngine.IsVisible` logic. It cannot be shared directly because
`QueryEngine.IsVisible` is `private`, and because the Tool project cannot reference Core's
internal `StdlibFilter` type either; duplicating this one-line predicate locally is simpler
and lower-risk than exposing a new shared internal surface for a single boolean check.

Diagnostics are deliberately **not** filtered by this predicate: `WorkspaceLoader`
diagnostics are only ever produced while parsing/resolving the user's own supplied files —
the stdlib symbol table returned by `StdlibProvider.GetSymbolTable()` is a pre-resolved seed
consumed as-is by `WorkspaceLoader`, not re-parsed, so it can never itself be a diagnostic
source.

##### Target Scoping

`--target <qualified-name>` restricts export output to the containment subtree rooted at the
named element. `ExportCommand.ResolveTargetSubtreeSubjects` resolves the target: it returns
`null` when the name is absent from `workspace.Declarations`, or is present but fails
`IsVisible` (a standard-library declaration without `--include-stdlib`) — both cases are
reported by the caller as the same "was not found in the workspace" error, matching
`IsVisible`'s existing exclude semantics used elsewhere in this class. When the target
resolves, the "subject" set starts as `{ target }`; if the target's declaration is a
`SysmlFeatureNode` (a usage, e.g. `part myVehicle : Vehicle;`), the usage's own resolved
`SysmlEdgeKind.Typing` edge target (its type) is added to the subject set too, so a usage
target still yields useful subtree content instead of a near-empty result. A qualified name
is then "in scope" when it equals a subject or has a subject as a `"{subject}::"` prefix
(`ExportCommand.IsInTargetSubtree`).

This mirrors Core's internal `DemaConsulting.SysML2Tools.Layout.Internal.ExposeScopeResolver`
— specifically its `AddWholeSubtreeSubject` usage-to-type expansion — which the Tool project
cannot reference directly (it is `internal` to Core, and Core's `InternalsVisibleTo` list
only grants Core's own test project, not this Tool project). `ExportCommand` therefore
duplicates a small, purpose-built subset of that logic locally, following the exact same
duplication precedent already established by `IsVisible` above: `--target` only ever has a
single target and no per-target bracket filter (the standalone `--filter` option already
covers narrowing), so `ExposeScopeResolver`'s multi-target/bracket-filter/`Failures`
machinery is deliberately omitted from this smaller copy. Future changes to
`ExposeScopeResolver`'s whole-subtree-subject behavior should prompt a deliberate check of
whether this duplicate needs a matching update.

##### Filter Narrowing

`--filter <expr>` narrows the exported declarations/edges using the same Phase 1
filter-expression subset `render`'s dynamic-view `--filter` uses, reusing the public
`DemaConsulting.SysML2Tools.Filtering.FilterExpressionParser`/`FilterExpressionEvaluator`
types directly (both are `public`, so — unlike the target-subtree logic above — no
duplication is needed here). `ExportCommand.RunAsync` parses `options.FilterExpression` via
`FilterExpressionParser.Parse`; on success, it evaluates the parsed expression against the
current (possibly `--target`-scoped) declaration keys via
`FilterExpressionEvaluator.Evaluate`, narrows `Declarations` to the matched subset, and
re-derives `Edges` to keep only edges whose endpoints (when the source is non-null) both
remain in the matched set.

A parse failure does not abort the export: mirroring `GeneralViewLayoutStrategy`'s own
graceful degradation for a non-evaluatable `filter [<expr>];` statement, `ExportCommand` falls
back to the unfiltered (but still `--target`-scoped, if applicable) result, and surfaces the
failure via two channels — (a) a synthetic `SysmlDiagnostic` appended to
`ExportResult.Diagnostics`, with `FilePath = "<--filter>"` (following the same `[stdlib]…`
virtual-path convention already used for non-ordinary `FilePath` values — see **Stdlib
Filtering** above), `Line = 0`, `Column = 0`, `Severity = DiagnosticSeverity.Warning`, and a
`Message` describing the failure, so the failure is visible to offline/agent JSON/JSONL
consumers; and (b) a matching `context.WriteLine` console warning, mirroring `render`'s own
`--filter` warning-surfacing convention, for interactive visibility.
`FilterExpressionEvaluator.Evaluate` itself never fails (its `Diagnostics` list is always
empty by design), so the single failure point handled here is the parse step.

Composition order is deliberate: `--target` scopes first, `--filter` narrows second — mirroring
`GeneralViewLayoutStrategy`'s `expose`-then-`filter` pipeline exactly. With no `--target`,
`--filter` narrows the whole (stdlib-filtered) workspace, matching a view with a `filter`
statement but no `expose` statement.

#### Output Model

##### ExportResult Data Model

- `Declarations` (`IReadOnlyDictionary<string, SysmlNode>`) — every visible declaration,
  keyed by its qualified name.
- `Edges` (`IReadOnlyList<SysmlEdge>`) — every visible semantic edge.
- `Diagnostics` (`IReadOnlyList<SysmlDiagnostic>`) — every diagnostic produced while loading
  the workspace, unfiltered.

##### JSON Rendering (`--format json`, default)

A single indented JSON document is produced via
`JsonSerializer.Serialize(result, ExportResultSerializerContext.Default.ExportResult)`. The
`Declarations` property serializes as a qualified-name-keyed JSON object (not an array),
`Edges`/`Diagnostics` as JSON arrays. Each `SysmlNode` serializes using its own
pre-existing `[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]` /
`[JsonDerivedType]` attributes — no export-specific converter is required.

##### JSONL Rendering (`--format jsonl`)

`ExportCommand.RenderJsonLines` emits one compact (non-indented) JSON object per line: one
line per `Declarations` entry wrapped in `ExportDeclarationLine` (`Kind = "declaration"`),
one line per `Edges` entry wrapped in `ExportEdgeLine` (`Kind = "edge"`), one line per
`Diagnostics` entry wrapped in `ExportDiagnosticLine` (`Kind = "diagnostic"`) — declarations
first, then edges, then diagnostics, each serialized independently via
`JsonSerializer.Serialize(line, ExportLineSerializerContext.Default.ExportXxxLine)` and
joined with `\n`. JSONL's entire value proposition (line-oriented, grep/tail-friendly
streaming) requires one complete JSON value per line; a single envelope-per-line would defeat
that, hence three small dedicated wrapper types rather than one `ExportResult`-per-line.

##### Serialization Contexts

Source-generated `JsonSerializerContext` types apply their `WriteIndented` setting at the
context level only — there is no supported per-`Serialize`-call override in AOT-safe
source-generation mode. Since JSON output must be indented and JSONL output must be compact,
this rules out sharing one context between both paths. `ExportResultSerializerContext`
(`WriteIndented = true`, covering `ExportResult`/`SysmlNode`/`SysmlEdge`/`SysmlDiagnostic`)
and `ExportLineSerializerContext` (`WriteIndented = false`, covering the three line-wrapper
types) are therefore two separate, purpose-built contexts — the concrete resolution
recommended by this unit's planning report for the single-vs-dual-context design question,
implemented directly without re-spiking.

#### ExportOptions

- `Format` (`string?`) — `"json"` (default) or `"jsonl"` from `--format`.
- `Output` (`string?`) — a **file path** (not a directory) from `--output`; when omitted,
  output goes to stdout. This differs in meaning from `render`'s `--output`, which names an
  output *directory* for per-view files — the flag name is reused deliberately (avoiding a
  differently-named flag for "where does output go") but the meaning is documented explicitly
  in both the XML doc comment and `export --help` text to prevent confusion between the two
  commands.
- `IncludeStdlib` (`bool`) — from `--include-stdlib`; mirrors `query`'s exact convention.
- `Target` (`string?`) — a qualified name from `--target`, restricting output to that
  element's containment subtree; `null` means no target scoping (the whole stdlib-filtered
  workspace). See **Target Scoping** above.
- `FilterExpression` (`string?`) — a raw Phase 1 filter-expression text from `--filter`,
  narrowing the (possibly `--target`-scoped) declaration/edge set; `null` means no filtering.
  See **Filter Narrowing** above.
- `Files` (`IReadOnlyList<string>`) — file glob patterns.

#### ExportCommand

##### Key Methods

**`RunAsync(Context context)`**

1. Throws `ArgumentException` if `context.Export` is `null` (defensive; unreachable when
   `Program` dispatches correctly).
2. Throws `ArgumentException` naming the bad value when `Format` is neither `null`, `"json"`,
   nor `"jsonl"` (case-insensitive).
3. `WriteError`s "no input files" and returns (exit code 1) when `options.Files` is empty.
4. Resolves `options.Files` via `GlobFileCollector.Collect`; `WriteError`s "no files matched
   the given pattern(s)" and returns when the pattern list resolved to zero files.
5. Loads the workspace via `StdlibProvider.GetSymbolTable()` + `WorkspaceLoader.LoadAsync`;
   `WriteError`s "workspace loading failed" and returns if `Workspace` is `null`.
6. Builds the filtered `ExportResult` per **Command Semantics** above, including `--target`
   subtree scoping (`WriteError`s "--target '<name>' was not found in the workspace" and
   returns — no export produced — when the target does not resolve) and `--filter` narrowing
   (falling back to the unfiltered, `--target`-scoped result with a diagnostic and console
   warning on parse failure, rather than aborting).
7. Renders as JSON or JSONL per `options.Format`.
8. Writes to `options.Output` (file) or stdout.

**`PrintHelp(Context context)`**: Prints the `export` usage line, its five options (with an
explicit note that `--output` names a file, not a directory), and one example invocation,
sourced from `ExportStrings`.

#### Localization / Resource Strings

`export`'s help text is sourced from `ExportStrings`, a hand-written, culture-aware
`ResourceManager` accessor over `Export/ExportStrings.resx`, following the exact pattern
established by `LintStrings`/`RenderStrings`/`QueryStrings` (see
`docs/design/sysml2-tools-tool/program.md`'s "Localization / Resource Strings" section for
the rationale).

#### Error Handling

- `context.Export is null`: `ArgumentException` (defensive; should not occur via `Program`).
- Unsupported `--format` value: `ArgumentException` naming the bad value.
- No input files: `context.WriteError`; `Context.ExitCode` becomes 1.
- Patterns given but none matched any files: `context.WriteError`; `Context.ExitCode`
  becomes 1.
- Workspace failed to load: `context.WriteError`; `Context.ExitCode` becomes 1.
- `--target` does not resolve to a visible declaration (genuinely absent, or a
  standard-library declaration without `--include-stdlib`): `context.WriteError` reporting
  "--target '<name>' was not found in the workspace"; `Context.ExitCode` becomes 1; no export
  is produced. Both "absent" and "stdlib-hidden" cases intentionally share this one message —
  see **Target Scoping** above.
- `--filter` fails to parse/evaluate: not a hard error — falls back to the unfiltered
  (`--target`-scoped, if applicable) result, with a synthetic warning `SysmlDiagnostic`
  appended to the output and a matching `context.WriteLine` console warning; `Context.ExitCode`
  remains 0. See **Filter Narrowing** above.

#### Dependencies

- `Context`, `SysmlCommand` (in `DemaConsulting.SysML2Tools.Cli`) — reads `Export` options;
  writes output.
- `GlobFileCollector` (in `DemaConsulting.SysML2Tools.Io`) — resolves `options.Files` glob
  patterns to concrete file paths before loading the workspace.
- `WorkspaceLoader`, `StdlibProvider`, `SysmlWorkspace`, `SemanticIndex`, `SysmlNode`,
  `SysmlEdge`, `SysmlDiagnostic` (in `DemaConsulting.SysML2Tools.Semantic`/`.Internal`) —
  workspace loading and the model dumped by `ExportCommand`.

#### Callers

- `Program.RunToolLogicAsync` — dispatches to `ExportCommand.RunAsync` when
  `context.Command == SysmlCommand.Export`.
- `Program.RunAsync` — dispatches to `ExportCommand.PrintHelp` for the `export --help` case.

#### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| SysML2Tools-Tool-Export-Grammar | `ExportArgumentParser` |
| SysML2Tools-Tool-Export-Format | `ExportOptions.Format`; `ExportCommand.RunAsync`'s format validation |
| SysML2Tools-Tool-Export-Output | `ExportOptions.Output`; `ExportCommand.RunAsync`'s file-vs-stdout write |
| SysML2Tools-Tool-Export-StdlibFilter | `ExportCommand.IsVisible` |
| SysML2Tools-Tool-Export-NoFilesOrNoMatch | File-resolution guards in `ExportCommand.RunAsync` |
| SysML2Tools-Tool-Export-JsonEnvelope | `ExportResult`; `ExportResultSerializerContext` |
| SysML2Tools-Tool-Export-JsonlEnvelope | `Export*Line` records; `ExportLineSerializerContext` |
| SysML2Tools-Tool-Export-Help | `ExportCommand.PrintHelp`, called from `Program.RunAsync` |
| SysML2Tools-Tool-Export-Target | `ExportOptions.Target`; `ResolveTargetSubtreeSubjects`/`IsInTargetSubtree` |
| SysML2Tools-Tool-Export-Filter | `ExportOptions.FilterExpression`; `FilterExpressionParser`/`Evaluator` |
| SysML2Tools-Tool-Export-SelfTest | `Validation.RunExportSelfTestAsync` |
