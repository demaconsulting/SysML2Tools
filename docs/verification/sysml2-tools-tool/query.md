### DemaConsulting.SysML2Tools.Tool — Query Subsystem Verification

#### Verification Approach

The Query subsystem is verified using unit/integration tests in five files under
`test/DemaConsulting.SysML2Tools.Tool.Tests/Query/` (`QuerySubsystemTests.cs`,
`QueryVerbsTests.cs`, `QueryRenderingTests.cs`, `QueryOmgFixtureTests.cs`,
`QueryErrorPathTests.cs`), plus query-specific parsing tests in
`test/DemaConsulting.SysML2Tools.Tool.Tests/Cli/ContextTests.cs`. Tests invoke
`Context.Create`, `QueryCommand.RunAsync`, `Program.RunAsync`, and (for a subset of
real-workspace scenarios) `QueryEngine`'s verb methods directly, asserting on captured
console output, exit code, and `QueryResult` shape. Tests run against all three target
frameworks.

#### Test Environment

- Framework: xUnit v3
- Target frameworks: net8.0, net9.0, net10.0
- Test project: `DemaConsulting.SysML2Tools.Tool.Tests`
- Dependencies: `DemaConsulting.SysML2Tools.Tool` (internal access via `InternalsVisibleTo`);
  OMG reference models under `test/SysMLModels/OMG/` (locatable via a repo-root search
  upward from the test assembly's output directory).

#### Acceptance Criteria

- All 11 verb tokens parse to the matching `QueryVerb` and dispatch to real `QueryEngine`
  logic, producing exit code 0 for valid input.
- An unrecognized verb token produces an `ArgumentException` naming the bad token.
- `--element`/`-e` is required for every verb except `list`/`find`; omitting it for a verb
  that requires it produces an `ArgumentException`.
- `find` without `--kind`/`--name` produces an `ArgumentException`.
- `list`/`find` succeed without `--element`.
- `--format markdown` and `--format json` both parse and render without error; an
  unrecognized `--format` value produces an `ArgumentException`.
- Each verb's output correctly reflects the underlying model for representative inline
  fixtures: `uses` reports outgoing supertype/typing/import edges; `used-by` reports the
  reverse; `impact` respects `--depth` bounding and computes the full transitive closure
  when unbounded; `describe` reports kind, resolved supertypes, and child count;
  `hierarchy` respects `--direction up`/`down`/`both`; `requirements` reports
  satisfy/verify/allocate edges; `interface` reports ports/typed features and excludes
  plain attributes; `connections` reports resolved feature-chain endpoints with the
  connector's keyword; `states` reports states and guarded transitions; `list`/`find`
  respect `--kind`/`--name` filtering.
- Markdown and JSON renderings of the same `QueryResult` contain the same qualified names
  in the same (alphabetical, ordinal) order.
- `--include-stdlib` toggles whether stdlib-seeded elements appear in results.
- Error paths are covered: element not found, a file that fails to parse
  (best-effort/graceful degradation), `find` without a filter, unsupported `--format`, no
  input files.
- A representative sample of real-world OMG training/example fixtures
  (`RequirementSatisfaction.sysml`, `ConnectionsExample.sysml`, `StateDecomposition-1.sysml`,
  `GeneralizationExample.sysml`, `Comments.sysml`) produce non-empty, sensible results for
  their respective verbs, without asserting brittle exact counts.
- Existing `lint`/`render` test suites continue to pass unmodified, confirming no
  regression.
- `query <verb> --help` includes a real example invocation for the verb and the shared
  Markdown/JSON output-shape schema hint; `query --help` (no verb) includes a "typical
  workflow" note recommending `list`/`find` before element-scoped verbs.
- All four subsystems' resx-backed help text (`ProgramStrings`, `LintStrings`,
  `RenderStrings`, `QueryStrings`) resolve every key to non-empty text and stay in
  bidirectional parity with their accessor classes.

#### Test Scenarios

##### QuerySubsystemTests.cs

**`QuerySubsystem_AnyVerb_WithValidInput_DispatchesToRealLogic`** (theory, 11 cases):
Verifies that each verb, given `--element` when required (and `--kind` for `find`), against
a small shared fixture covering every verb's target element, produces exit code 0 and
output containing `query {verb}`.

##### QuerySubsystem_ElementRequiredVerb_MissingElement_ThrowsArgumentException

Verifies that omitting `--element` for any of the 9 verbs that require it (all except
`list`/`find`) throws an `ArgumentException` mentioning `--element`.

##### QuerySubsystem_ListVerb_NoElementNoFiles_ReportsNoInputFilesError / QuerySubsystem_FindVerb_NoElementNoFiles_ReportsNoInputFilesError

Verifies that `list`/`find` do not require `--element`, and that omitting input files (the
next validation step) produces the "no input files" error and exit code 1.

##### QuerySubsystem_FormatMarkdown_DispatchesWithoutError / QuerySubsystem_FormatJson_DispatchesWithoutError

Verifies that both accepted `--format` values parse and render successfully end-to-end.

##### QuerySubsystem_UnknownVerb_ThrowsArgumentException

Verifies that `Context.Create(["query", "bogus"])` throws an `ArgumentException` naming
`bogus`.

##### QuerySubsystem_QueryHelp_NoVerb_PrintsGeneralHelpWithoutThrowing

Verifies that `query --help` prints general help (listing the verbs) and returns exit code 0.

##### QuerySubsystem_QueryVerbHelp_WithVerb_PrintsVerbHelpWithoutThrowing

Verifies that `query uses --help` prints verb-specific help and returns exit code 0, without
requiring `--element`.

##### QuerySubsystem_QueryHelp_NoVerb_MentionsTypicalWorkflow

Verifies that `query --help` (no verb) includes the "typical workflow" note text (contains
"Typical workflow" and "--element"), confirming `PrintGeneralHelp`'s enrichment content is
actually rendered, not merely present in the resx file. Satisfies
`SysML2Tools-Tool-Query-HelpEnrichment`.

##### QuerySubsystem_QueryVerbHelp_MentionsExampleInvocationAndSchemaHints (theory, 11 cases)

Verifies that `query <verb> --help`, for every one of the 11 verbs, contains that verb's
real example-invocation substring (drawn from the `VehicleExample` fixture, per the
planning report's verified enrichment content) and the shared Markdown/JSON schema-hint
substrings (`"Qualified Name"` for Markdown, `"QualifiedName"` for JSON — matching the real
`QueryResultRenderer`/`QueryResultSerializerContext` output shape, verified by direct CLI
invocation during implementation). Satisfies `SysML2Tools-Tool-Query-HelpEnrichment`.

##### ResxResource_EveryKey_ResolvesToNonEmptyText / ResxResource_KeysAndAccessorProperties_AreInBidirectionalParity (ResxResourceTests.cs)

For the `QueryStrings` resource base name/accessor pair (one of four covered by these theory
tests), every key discovered in `Query/QueryStrings.resx`'s invariant-culture resource set
resolves to non-null/non-empty text via `ResourceManager`, and every such key (including the
11 `Query_Example_*` keys, each backed by its own accessor property) has a matching
`public static string` property on `QueryStrings` (and vice versa). Satisfies
`SysML2Tools-Tool-Query-LocalizableHelpText`.

##### QueryVerbsTests.cs

One or more `[Fact]`/`[Theory]` methods per verb, each using a small inline `.sysml`
fixture written to a temp file and run end-to-end via `Context.Create` +
`Program.RunAsync`, asserting on captured stdout content: qualified names of expected
entries, edge/kind labels, `--depth`/`--direction` bounding, annotation text, and
`--include-stdlib` on/off behavior.

##### QueryRenderingTests.cs

Direct unit tests of `QueryResultRenderer.RenderMarkdown`/`RenderJson` against hand-built
`QueryResult` instances (no workspace/model involved): empty-entries rendering, sort-order
correctness, `Detail`/`Notes` rendering, and JSON round-trip via
`QueryResultSerializerContext`. Plus one end-to-end test confirming Markdown and JSON
outputs for the same query contain the same qualified names in the same order.

##### QueryOmgFixtureTests.cs

Loads real OMG training/example `.sysml` files via `WorkspaceLoader.LoadAsync` directly
(bypassing CLI argument parsing, to avoid quoting ambiguity for qualified names containing
spaces), locates the target element by qualified-name suffix match, and calls the relevant
`QueryEngine` method directly. Assertions are relaxed (non-exact-count) smoke checks:
non-empty results with entries of the expected `Kind`(s). Each test degrades gracefully
(returns without failing) if the reference model files are not present in the checkout,
consistent with prior units' precedent for OMG-fixture-dependent tests. The
`States_StateDecompositionFixture_ReportsStatesAndTransitions` test only asserts on the one
reliably-produced `"transition"`-kind entry, documenting a known, pre-existing (unit 4)
grammar/`AstBuilder` gap where `accept <Signal> then <state>;` trigger-shorthand
transitions can silently absorb an adjacent sibling `state` usage; the equivalent inline
fixture test `QueryVerbsTests.States_ReportsStatesAndGuardedTransitions` (using explicit
`transition first X if G then Y;` syntax) validates both `"state"` and `"transition"` entry
kinds together and is unaffected by the gap.

##### QueryErrorPathTests.cs

Covers: element not found (`context.WriteError` message contains "not found in the
workspace", exit code 1); `find` without `--kind`/`--name` (`ArgumentException`);
unsupported `--format` value (`ArgumentException`); a file with parse errors (diagnostics
reported, command completes best-effort); no input files supplied (exit code 1).

##### Context_Create_QueryCommand_WithVerbToken_SetsQueryVerb (ContextTests.cs)

Verifies, for each of the 11 verb tokens, that `Context.Create(["query", token, "--element",
"Pkg::Foo"])` sets `Command` to `SysmlCommand.Query` and `Query.Verb` to the matching value.

##### Context_Create_QueryCommand_UnknownVerb_ThrowsArgumentException (ContextTests.cs)

Verifies that an unrecognized verb token throws `ArgumentException` naming the token.

##### Context_Create_QueryCommand_NoVerbWithHelp_LeavesQueryNull (ContextTests.cs)

Verifies that `query --help` (no verb) leaves `Context.Query` `null`.

##### Context_Create_QueryCommand_WithElementFlag_SetsElement (ContextTests.cs)

##### Context_Create_QueryCommand_WithShortElementFlag_SetsElement (ContextTests.cs)

Verifies that both `--element` and `-e` populate `Query.Element`.

##### Context_Create_QueryCommand_WithDirectionFlag_SetsDirection (ContextTests.cs)

##### Context_Create_QueryCommand_WithKindFlag_SetsKind (ContextTests.cs)

##### Context_Create_QueryCommand_WithNameFlag_SetsNameFilter (ContextTests.cs)

##### Context_Create_QueryCommand_WithIncludeStdlibFlag_SetsIncludeStdlibTrue (ContextTests.cs)

Verifies that `--direction`, `--kind`, `--name`, and `--include-stdlib` parse into the
corresponding `QueryOptions` fields.

##### Context_Create_QueryCommand_WithFormatMarkdown_SetsQueryFormat / WithFormatJson_SetsQueryFormat (ContextTests.cs)

Verifies that `--format markdown`/`--format json` populate `Query.Format`, and that
`Context.Render` is `null` for a `query` invocation, confirming query's `--format` is
interpreted independently of render's `--format` (they are separate typed properties, not a
shared field).

##### Context_Create_QueryCommand_WithDepthFlag_SetsQueryDepth (ContextTests.cs)

Verifies that `--depth 3` populates both `Query.Depth` and `MaxRenderDepth` (the same
underlying parsed value, interpreted independently by `query` vs `render`).

##### Context_Create_QueryCommand_WithFiles_SetsQueryFilesNotTopLevelFiles (ContextTests.cs)

Verifies that file globs supplied after the verb token populate `Query.Files` while
`Context.Files` remains empty.
