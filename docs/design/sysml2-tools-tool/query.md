### DemaConsulting.SysML2Tools.Tool — Query Subsystem

#### Overview

The Query subsystem implements the `query` CLI command: a model-analysis interface exposing
12 verbs (`uses`, `used-by`, `dependencies`, `impact`, `describe`, `hierarchy`,
`requirements`, `interface`, `connections`, `states`, `list`, `find`) over a SysML v2
workspace. It provides five
cooperating types:

- `QueryVerb` — an enum identifying which of the 12 operations was requested, plus a
  `QueryVerbParsing` helper that converts between kebab-case command-line tokens
  (e.g., `used-by`) and enum values, and reports which verbs require a target element.
- `QueryOptions` — an immutable record capturing every verb-specific option (`Element`,
  `Format`, `WalkDepth`, `Direction`, `Kind`, `NameFilter`, `IncludeStdlib`, `Heading`,
  `Files`) parsed by `Context.Create` for one `query` invocation.
- `QueryCommand` — the entry-point dispatcher, mirroring `LintCommand`/`RenderCommand`'s
  `internal static class` shape with a `RunAsync(Context)` method, plus `PrintGeneralHelp`
  and `PrintVerbHelp` for `--help` rendering. Loads the workspace, resolves the target
  element, dispatches to `QueryEngine`, and renders the result via `QueryResultRenderer`.
- `QueryEngine` — a stateless static class with one public method per verb, each taking
  `(SysmlWorkspace workspace, SysmlNode element, QueryOptions options)` (for `list`/`find`,
  `element` is an unused placeholder since those verbs operate workspace-wide) and returning
  a `QueryResult`.
- `QueryResult`/`QueryResultEntry`/`QueryResultRenderer`/`QueryResultSerializerContext` — the
  shared, verb-agnostic output model and rendering layer (see **Output Model** below).

#### Verb Semantics

Every verb reads the semantic model built by the `Semantic`/`Semantic.Model` types
(`SemanticIndex`, `SysmlNode.ResolvedEdges`, `SysmlNode.Children`) rather than re-parsing or
re-resolving anything; `QueryEngine` is a pure read-only consumer of the workspace built by
`WorkspaceLoader.LoadAsync`.

| Verb | Element required | Data source |
| --- | --- | --- |
| `uses` | yes | `Index.GetOutgoingEdges(qn)` |
| `used-by` | yes | `Index.GetIncomingEdges(qn)` |
| `dependencies` | yes | Merges `QueryEngine.Uses` + `QueryEngine.UsedBy` (no separate traversal) |
| `impact` | yes | Breadth-first transitive closure over `used-by`, cycle-guarded, bounded by `--walk-depth` |
| `describe` | yes | Node's own kind, resolved supertypes, typing, annotations, applied metadata, children |
| `hierarchy` | yes | Recursive `Supertype` edge walk, direction-controlled, cycle-guarded |
| `requirements` | yes | `Satisfy`/`Verify`/`Allocate` edges where the element is source or target |
| `interface` | yes | Direct `Children` that are ports or have a non-null `FeatureTyping` |
| `connections` | yes | `QueryEngine.CollectConnectEdges` node-walk (see below) |
| `states` | yes | Recursive descendant walk (`QueryEngine.CollectStates`, see below) |
| `list` | no | `workspace.Declarations`, filtered by `--kind`/`--name` |
| `find` | no | Same as `list`, but requires at least one of `--kind`/`--name` |

Entry shapes, one row per verb:

- `uses`/`used-by`: other-side qn, `Kind` = edge kind label, `Detail` = other side's kind.
- `dependencies`: the merged `uses` (outgoing) + `used-by` (incoming) entries, each tagged
  with `Direction` (`Outgoing`/`Incoming`); no `Summary` lines (the prose rendering supplies
  its own intro sentences instead — see **Output Model** below).
- `impact`: qn of every element transitively affected by a change to the target.
- `describe`: direct `Children` as entries (child qn, `Kind` = child's kind); the node's
  own kind/supertypes/typing/annotations/applied-metadata/child-count appear in `Summary`,
  not `Entries`. Supertypes are resolved via outgoing `Supertype` edges, falling back to raw
  `SupertypeNames` only when no resolved edge exists. Applied `metadata` annotations (captured
  as `SysmlMetadataNode` entries in `element.Children`, per `SysmlNode.cs`) are rendered as one
  `"Metadata {TypeReference}"` line per bare annotation (no attributes), or one
  `"Metadata {TypeReference}.{Attribute}: {value}"` line per attribute otherwise; boolean values
  render as `true`/`false`, numbers via invariant-culture formatting, strings unquoted, and any
  non-scalar (list/expression) value falls back to its verbatim raw source text so the value is
  never silently dropped.
- `hierarchy`: qn, `Kind` = `"supertype"` or `"subtype"`; `--direction up` walks outgoing
  `Supertype` edges, `down` walks incoming, `both` unions them.
- `requirements`: other-side qn, `Kind` = edge kind label, `Detail` = direction arrow.
- `interface`: feature qn, `Kind` = `FeatureKeyword`, `Detail` = typing + multiplicity.
- `connections`: `Connect` edges are **not** exposed via `SemanticIndex.AllEdges`
  (feature-chain resolution only mutates the originating connector node's own
  `ResolvedEdges`, per `ReferenceResolver`'s design), so `QueryEngine.CollectConnectEdges`
  walks every node reachable from `Declarations`, collecting each node's own resolved
  `Connect` edges together with its connector keyword (`connect`/`connection`/`message`).
  Entries: other-endpoint qn, `Kind` = connector keyword, `Detail` = `"A"`/`"B"` role.
- `states`: a recursive descendant walk (`QueryEngine.CollectStates`) collects
  `SysmlFeatureNode` entries with `FeatureKeyword == "state"` and `SysmlTransitionNode`
  children (using the transition's own resolved `Transition` edge when present, else its
  raw `Source`/`Target` text). States: qn, `Kind` = `"state"`. Transitions: target qn,
  `Kind` = `"transition"`, `Detail` = `"{source} -> {target}"` (+ `" if {guard}"`).
- `list`/`find`: qn, `Kind` = element's kind.

Every verb applies the `--include-stdlib` filter identically via `IsVisible` (checks
`workspace.StdlibNames.Contains(qualifiedName)`), and every entry is emitted unsorted by
`QueryEngine` — deterministic alphabetical-by-qualified-name ordering is applied exactly
once, downstream, in `QueryResultRenderer` (see **Output Model**).

##### Known Model Gaps

- **State-usage bodies with `accept X then Y` trigger-shorthand transitions**: a state usage
  body item consisting of an accept-triggered transition (`accept SomeSignal then target;`,
  with no explicit `first`/`transition` keyword) can, per the current ANTLR grammar/AST
  builder, silently absorb a preceding sibling `state` usage instead of producing its own
  `SysmlTransitionNode`. Plain `state x;` declarations and explicit
  `transition first x if guard then y;` successions are unaffected and fully supported. This
  is a pre-existing gap in the grammar/`AstBuilder` (predates this unit) that the `states`
  verb inherits; it is out of scope for this unit to fix (see this unit's completion report
  for the reproduction and analysis).

#### Output Model

##### QueryResult / QueryResultEntry Purpose

A single, uniform result shape used by all 12 verbs so that `QueryResultRenderer` never
needs verb-specific rendering logic (except the one `dependencies`-only branch documented
below), and so Markdown/JSON output are always structurally identical.

##### QueryResult / QueryResultEntry Data Model

- `QueryResult`: `Verb` (string token), `Element` (qualified name, or `null` for
  `list`/`find`), `Summary` (`IReadOnlyList<string>`, free-form header lines), `Entries`
  (`IReadOnlyList<QueryResultEntry>`).
- `QueryResultEntry`: `QualifiedName`, `Kind`, `Detail` (`string?`), `Notes`
  (`IReadOnlyList<string>`, currently unused by any verb but reserved for future
  multi-line annotations), `Direction` (`QueryEntryDirection?`, `[JsonIgnore(Condition =
  JsonIgnoreCondition.WhenWritingNull)]`) — populated only by `dependencies`
  (`Outgoing`/`Incoming`); `null` for every other verb, and omitted from JSON output
  entirely when `null` (rather than serialized as `"Direction": null`), so adding this
  field does not change any other verb's JSON output shape.
- `QueryEntryDirection`: a plain enum, `Outgoing`/`Incoming`, defined alongside
  `QueryResult`/`QueryResultEntry`.

##### QueryResultRenderer Purpose

The single point of Markdown/JSON rendering and the single point of deterministic ordering,
so no verb implementation can accidentally produce out-of-order or format-inconsistent
output.

##### QueryResultRenderer Key Methods

**`RenderMarkdown(QueryResult, int depth = 1, string? heading = null)`**:
Returns `IReadOnlyList<string>` lines — a `new string('#', depth)`-prefixed heading
(default one `#`) whose text is either `heading` (when supplied, verbatim, with no
merging of verb/element information) or the auto-generated `query <verb>[: <element>]` text,
followed by the `Summary` lines as a bullet list, then either `_No entries._` or a Markdown
table (`| Qualified Name | Kind | Detail |`) of `SortEntries`' output. `depth` is sourced from
the global `--depth` flag (`Context.HeadingDepth`, shared with the `--validate` self-report
heading; pre-validated to the range 1-6 by `GlobalArgumentParser`); `heading` is sourced from
`query`'s own `--heading` flag (parsed by `QueryArgumentParser`). Both are Markdown output
only; no effect on `RenderJson`.

**`dependencies`-only rendering path**: after the heading (and empty `Summary`), when
`result.Verb == "dependencies"`, `RenderMarkdown` first builds the combined pool
`[Element] + sortedEntries.Select(QualifiedName)` and calls
`Utilities.QualifiedNameShortener.Shorten` on it, producing a map from each original qualified
name to its shortened form (see `docs/design/sysml2-tools-tool/utilities/qualified-name-shortener.md`
for the stripping algorithm). It then branches to a private
`RenderDependenciesBody(sortedEntries, element, shortened)` helper instead of the table logic
above — the only per-verb branch in an otherwise verb-agnostic method. It splits the (already
`SortEntries`-sorted) entries by `Direction`, preserving each group's ordinal order, and emits
(using the shortened form of `Element` and every entry's `QualifiedName`):

```text
{Element} references the following elements:

- Depends on **{QualifiedName}** ({Kind})
...

The following elements reference {Element}:

- Used by **{QualifiedName}** ({Kind})
...
```

When the outgoing group is empty, the intro sentence and bullet list are replaced by a
single `"{Element} has no outgoing references."` line; when the incoming group is empty,
by a single `"No elements reference {Element}."` line. No table, and no `"_No entries._"`
fallback, are ever emitted for `dependencies`. `RenderJson` needs no special-casing for
`dependencies`: the `Direction` field already round-trips through the existing
`QueryResultSerializerContext` serialization once the property exists on
`QueryResultEntry`, and `RenderJson` never calls `QualifiedNameShortener` — its
`QualifiedName`/`Element` values remain fully qualified for every verb, including
`dependencies`.

**`RenderJson(QueryResult)`**: Returns an indented JSON string via
`JsonSerializer.Serialize(sortedResult, QueryResultSerializerContext.Default.QueryResult)`,
where `sortedResult` is a copy of the input with `Entries` replaced by `SortEntries`' output
— guaranteeing the same ordering as `RenderMarkdown` for the same `QueryResult`.

**`SortEntries(IReadOnlyList<QueryResultEntry>)`** (private): `OrderBy(e => e.QualifiedName,
StringComparer.Ordinal)` — the single, shared sort used by both render methods.

##### QueryResultSerializerContext Purpose

Mirrors `AstSerializerContext`'s AOT-safe `System.Text.Json` source-generation pattern for
the Tool assembly: `[JsonSerializable(typeof(QueryResult))]` with
`[JsonSourceGenerationOptions(WriteIndented = true)]`.

#### QueryVerb / QueryVerbParsing

##### QueryVerb Purpose

Defines the fixed, ROADMAP-specified vocabulary of 12 query verbs and centralizes the
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

A single, flat, immutable record carrying every option relevant to any of the 12 verbs.
A shared shape (rather than one record per verb) keeps `Context.Create`'s construction
logic simple, since all 12 verbs share the same CLI grammar (verb token, then options,
then file globs) and differ only in which options are meaningful.

##### QueryOptions Data Model

- `Verb` (`QueryVerb`, required) — which operation was requested.
- `Element` (`string?`) — target qualified name from `--element`/`-e`; required for every
  verb except `list`/`find`.
- `Format` (`string?`) — `"markdown"` (default) or `"json"` from `--format`; reuses the same
  flag name as `render`'s `--format` (which instead accepts `svg`/`png`) — the two commands
  interpret the raw string independently.
- `WalkDepth` (`int?`) — impact-walk depth bound from `--walk-depth`, meaningful only for
  `impact`; command-scoped (parsed by `QueryArgumentParser`, unrelated to `render`'s own
  `--walk-depth` flag).
- `Direction` (`string?`) — `up`/`down`/`both` from `--direction`, meaningful only for
  `hierarchy`.
- `Kind` (`string?`) — element-kind filter from `--kind`, meaningful only for `list`/`find`.
- `NameFilter` (`string?`) — name substring filter from `--name`, meaningful only for
  `list`/`find`.
- `IncludeStdlib` (`bool`) — from `--include-stdlib`; applies to every verb.
- `Heading` (`string?`) — custom Markdown heading text from `--heading`, replacing the
  auto-generated `query <verb>[: <element>]` text; `null` means the auto-generated text is
  used. Markdown output only; no effect on `--format json`. The Markdown heading *depth*
  (number of leading `#` characters) is a separate concept, read directly from the global
  `Context.HeadingDepth` (`--depth`) by `QueryCommand.RunAsync`, not stored on `QueryOptions`.
- `Files` (`IReadOnlyList<string>`) — file glob patterns supplied after the verb token; kept
  separate from `Context.Lint`/`Context.Render`'s file lists (used by `lint`/`render`) so
  query's file handling cannot affect the other commands.

#### QueryCommand

##### QueryCommand Purpose

`QueryCommand.RunAsync` validates that a verb was successfully parsed and that
`--element` was supplied when required, loads the workspace, resolves the target element,
dispatches to `QueryEngine`, and renders the result via `QueryResultRenderer`. It also
exposes `PrintGeneralHelp`/`PrintVerbHelp` for `Program`'s help handling.

##### QueryCommand Key Methods

**`RunAsync(Context context)`**

1. Throws `ArgumentException` if `context.Query` is `null` (defensive; unreachable when
   `Program` dispatches correctly, since `Context.Create` only sets `Command = Query` and
   `Query = null` together when `--help` was requested without a verb).
2. Throws `ArgumentException` naming the verb when `QueryVerbParsing.RequiresElement` is
   `true` for the verb and `Element` is null/whitespace.
3. Throws `ArgumentException` when `Verb == Find` and neither `Kind` nor `NameFilter` is
   supplied (mirrors the `--element`-required validation style).
4. Throws `ArgumentException` when `Format` is neither `null`, `"markdown"`, nor `"json"`
   (case-insensitive).
5. `WriteError`s "no input files" and returns (exit code 1) when `options.Files` is empty —
   matching `lint`/`render`'s convention.
6. Resolves `options.Files` to concrete file paths via `GlobFileCollector.Collect(options.Files,
   [".sysml", ".kerml"], Directory.GetCurrentDirectory())` (`DemaConsulting.SysML2Tools.Io`, Core
   `Io` subsystem) — the same shared resolver used by `lint`/`render`. `WriteError`s
   `"query {token}: no files matched the given pattern(s)."` and returns when the pattern list
   resolved to zero files.
7. Loads the workspace via `StdlibProvider.GetSymbolTable()` +
   `WorkspaceLoader.LoadAsync(files, stdlibTable)`, reporting diagnostics via
   `WriteLine`/`WriteError` exactly like `RenderCommand`; `WriteError`s "workspace loading
   failed" and returns if `Workspace` is `null`.
8. For verbs requiring an element, looks up `workspace.Declarations.TryGetValue(element,
   out node)`; `WriteError`s `"query {token}: element '{element}' not found in the
   workspace."` and returns (exit code 1) when missing.
9. Dispatches via an explicit 12-arm `switch` on `options.Verb` — one arm per verb, not a
   loop or dictionary — to the matching `QueryEngine` method.
10. Renders the resulting `QueryResult` via `QueryResultRenderer.RenderMarkdown`/`RenderJson`
    and writes each line/the JSON string via `context.WriteLine`.

**`PrintGeneralHelp(Context context)`**: Lists all 12 verbs with a one-line description and
the shared option set; used for `query --help` with no verb. Also prints a "typical
workflow" note recommending `list`/`find` first to discover exact qualified names before
using an element-scoped verb, since 10 of the 12 verbs require `--element`.

**`PrintVerbHelp(Context context, QueryVerb verb)`**: Prints a verb-specific usage line and
only the options relevant to that verb, followed by one real example invocation (drawn from
the bundled `test/SysMLModels/OMG/examples/VehicleExample/*.sysml` fixture, or explicitly
marked "illustrative" for the two verbs — `requirements`, `states` — that fixture has no
matching content for) and the shared Markdown/JSON output-shape schema hint (identical for
every verb, since all 12 share one `QueryResult`/`QueryResultRenderer`); used for
`query <verb> --help`.

#### Localization / Resource Strings

Every line printed by `PrintGeneralHelp`/`PrintVerbHelp` (including the verb list, options,
the "typical workflow" note, the per-verb example invocations, and the schema hints) is
sourced from `QueryStrings`, a hand-written, culture-aware `ResourceManager` accessor over
`Query/QueryStrings.resx` — see `docs/design/sysml2-tools-tool/program.md`'s "Localization /
Resource Strings" section for the rationale and the zero-code-change future-locale story,
which applies identically here. The 12 per-verb example-invocation keys
(`Query_Example_Uses` … `Query_Example_Find`) are each backed by their own
`public static string` property (so the resx-key/accessor-property parity check in
`ResxResourceTests` needs no special-casing), and are additionally exposed through a single
`QueryStrings.GetExample(QueryVerb)` switch-expression helper so `PrintVerbHelp` only needs
one call site instead of a 12-arm switch of its own.

#### Error Handling

- `context.Query is null`: `ArgumentException` (defensive; should not occur via `Program`).
- `--element` required but missing: `ArgumentException` naming the verb token.
- `find` with neither `--kind` nor `--name`: `ArgumentException`.
- Unsupported `--format` value: `ArgumentException` naming the bad value.
- Unrecognized verb token: `ArgumentException` (thrown by `QueryVerbParsing.Parse`, called
  from `Cli.QueryArgumentParser`) listing all valid tokens.
- No input files: `context.WriteError`; `Context.ExitCode` becomes 1.
- Patterns given but none matched any files: `context.WriteError` reports
  `"query {token}: no files matched the given pattern(s)."`; `Context.ExitCode` becomes 1.
- Workspace failed to load: `context.WriteError`; `Context.ExitCode` becomes 1.
- Target element not found in the workspace: `context.WriteError` naming the element;
  `Context.ExitCode` becomes 1.

#### Dependencies

- `Context`, `SysmlCommand` (in `DemaConsulting.SysML2Tools.Cli`) — reads `Query` options;
  writes output.
- `GlobFileCollector` (in `DemaConsulting.SysML2Tools.Io`) — resolves `options.Files` glob
  patterns to concrete file paths before loading the workspace.
- `WorkspaceLoader`, `StdlibProvider`, `SysmlWorkspace`, `SemanticIndex`, `SysmlNode` and
  derived types (in `DemaConsulting.SysML2Tools.Semantic`/`.Internal`) — workspace loading
  and the model read by `QueryEngine`.
- `Utilities.QualifiedNameShortener` (in `DemaConsulting.SysML2Tools.Utilities`) —
  `QueryResultRenderer.RenderMarkdown`'s `dependencies`-only branch calls `Shorten` on the
  combined subject-plus-entries pool before rendering.

#### Callers

- `Program.RunToolLogicAsync` — dispatches to `QueryCommand.RunAsync` when
  `context.Command == SysmlCommand.Query`.
- `Program.RunAsync` — dispatches to `QueryCommand.PrintGeneralHelp`/`PrintVerbHelp` for the
  `--help` case.

#### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| SysML2Tools-Tool-Query-VerbGrammar | `Cli.QueryArgumentParser` verb-first parsing; `QueryVerbParsing.Parse` |
| SysML2Tools-Tool-Context-QueryVerbRequired | `Cli.QueryArgumentParser`'s required-verb check |
| SysML2Tools-Tool-Query-UnknownVerb | `QueryVerbParsing.Parse`'s `ArgumentException` path |
| SysML2Tools-Tool-Query-ElementRequired | Element-required check at the start of `QueryCommand.RunAsync` |
| SysML2Tools-Tool-Query-Format | `QueryOptions.Format`; `QueryResultRenderer.RenderMarkdown`/`RenderJson` |
| SysML2Tools-Tool-Query-NoFilesMatched | Zero-resolved-files guard in `QueryCommand.RunAsync` |
| SysML2Tools-Tool-Query-Help | `QueryCommand.PrintGeneralHelp`/`PrintVerbHelp`, called from `Program.RunAsync` |
| SysML2Tools-Tool-Query-Uses | `QueryEngine.Uses` |
| SysML2Tools-Tool-Query-UsedBy | `QueryEngine.UsedBy` |
| SysML2Tools-Tool-Query-Dependencies | `QueryEngine.Dependencies`, `QueryResultRenderer`'s `dependencies` branch |
| SysML2Tools-Tool-Query-DependenciesNameShortening | `Utilities.QualifiedNameShortener.Shorten` |
| SysML2Tools-Tool-Query-Impact | `QueryEngine.Impact` |
| SysML2Tools-Tool-Query-Describe | `QueryEngine.Describe` |
| SysML2Tools-Tool-Query-Hierarchy | `QueryEngine.Hierarchy` |
| SysML2Tools-Tool-Query-Requirements | `QueryEngine.Requirements` |
| SysML2Tools-Tool-Query-Interface | `QueryEngine.Interface` |
| SysML2Tools-Tool-Query-Connections | `QueryEngine.Connections`, `QueryEngine.CollectConnectEdges` |
| SysML2Tools-Tool-Query-States | `QueryEngine.States`, `QueryEngine.CollectStates` |
| SysML2Tools-Tool-Query-List | `QueryEngine.List` |
| SysML2Tools-Tool-Query-Find | `QueryEngine.Find` |
| SysML2Tools-Tool-Query-StdlibFilter | `QueryEngine.IsVisible` |
| SysML2Tools-Tool-Query-ElementNotFound | Element-lookup check in `QueryCommand.RunAsync` |
| SysML2Tools-Tool-Query-OutputFormat | `QueryResultRenderer.RenderMarkdown`/`RenderJson`/`SortEntries` |
| SysML2Tools-Tool-Query-LocalizableHelpText | `QueryStrings` accessor, used by `PrintGeneralHelp`/`PrintVerbHelp` |
| SysML2Tools-Tool-Query-HelpEnrichment | `QueryStrings.GetExample`/`SchemaHint_*`, used by `PrintVerbHelp` |
| SysML2Tools-Tool-Query-ReportHeading | `QueryOptions.Heading`, `Cli.Context.HeadingDepth`, `RenderMarkdown` |
