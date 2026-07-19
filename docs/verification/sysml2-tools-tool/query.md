### DemaConsulting.SysML2Tools.Tool — Query Subsystem Verification

#### Verification Approach

The Query subsystem is verified using unit/integration tests in five files under
`test/DemaConsulting.SysML2Tools.Tool.Tests/Query/` (`QuerySubsystemTests.cs`,
`QueryVerbsTests.cs`, `QueryRenderingTests.cs`, `QueryOmgFixtureTests.cs`,
`QueryErrorPathTests.cs`), plus query-specific parsing tests in
`test/DemaConsulting.SysML2Tools.Tool.Tests/Cli/ContextTests.cs`. Tests invoke
`Context.Create`, `QueryCommand.RunAsync`, `Program.RunAsync`, and (for a subset of
real-workspace scenarios) `QueryEngine`'s verb methods directly, asserting on captured
console output, exit code, and `QueryResult` shape. Tests run against the tool's target
framework.

#### Test Environment

- Framework: xUnit v3
- Target framework: net10.0
- Test project: `DemaConsulting.SysML2Tools.Tool.Tests`
- Dependencies: `DemaConsulting.SysML2Tools.Tool` (internal access via `InternalsVisibleTo`);
  OMG reference models under `test/SysMLModels/OMG/` (locatable via a repo-root search
  upward from the test assembly's output directory).

#### Acceptance Criteria

- All 12 verb tokens parse to the matching `QueryVerb` and dispatch to real `QueryEngine`
  logic, producing exit code 0 for valid input.
- An unrecognized verb token produces an `ArgumentException` naming the bad token.
- `--element`/`-e` is required for every verb except `list`/`find`; omitting it for a verb
  that requires it produces an `ArgumentException`.
- `find` without `--kind`/`--name` produces an `ArgumentException`.
- `list`/`find` succeed without `--element`.
- `--format markdown` and `--format json` both parse and render without error; an
  unrecognized `--format` value produces an `ArgumentException`.
- `--depth` (global flag, 1-6, default 1) and `--heading` (default: auto-generated text)
  control the Markdown output's top heading depth/text without affecting `--format json`
  output (byte-identical JSON with/without the flags).
- Each verb's output correctly reflects the underlying model for representative inline
  fixtures: `uses` reports outgoing supertype/typing/import edges; `used-by` reports the
  reverse; `dependencies` combines `uses`/`used-by` for one element into a single prose
  (non-tabular) Markdown result, with the merged entries' `Direction` field
  (`Outgoing`/`Incoming`) populated only for this verb; `impact` respects `--walk-depth`
  bounding and computes the full transitive closure
  when unbounded; `describe` reports kind, resolved supertypes, and child count;
  `hierarchy` respects `--direction up`/`down`/`both`; `requirements` reports
  satisfy/verify/allocate edges; `interface` reports ports/typed features and excludes
  plain attributes; `connections` reports resolved feature-chain endpoints with the
  connector's keyword; `states` reports states and guarded transitions; `list`/`find`
  respect `--kind`/`--name` filtering.
- `dependencies` reports the merged `uses`/`used-by` result as bullet-prose Markdown (not a
  table), with entries' `Direction` field populated (`Outgoing`/`Incoming`) only for this
  verb; JSON output for every other verb is unaffected (no `Direction` key present when
  `null`).
- `dependencies`'s Markdown output shortens every name (the subject element plus every entry's
  qualified name) by the longest shared leading `::`-segment prefix across that combined pool,
  via `Utilities.QualifiedNameShortener.Shorten`, applied identically to the subject sentence
  and both bullet groups; when the pool shares no common prefix, names remain fully qualified.
  JSON output for `dependencies` (and every other verb) is unaffected — `QualifiedName`/
  `Element` values remain fully qualified in JSON regardless of this Markdown-only behavior.
- Markdown and JSON renderings of the same `QueryResult` contain the same qualified names
  in the same (alphabetical, ordinal) order.
- `--include-stdlib` toggles whether stdlib-seeded elements appear in results.
- Error paths are covered: element not found, a file that fails to parse
  (best-effort/graceful degradation), `find` without a filter, unsupported `--format`, no
  input files, one or more patterns supplied but none matching any file on disk.
- A glob pattern (e.g. `*.sysml`) resolves to every matching file in the target directory via
  the shared `GlobFileCollector` (see `docs/verification/sysml2-tools-core/io.md` for the
  underlying glob-semantics verification) and the workspace loads all of them.
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

**`QuerySubsystem_AnyVerb_WithValidInput_DispatchesToRealLogic`** (theory, 12 cases):
Verifies that each verb, given `--element` when required (and `--kind` for `find`), against
a small shared fixture covering every verb's target element, produces exit code 0 and
output containing `query {verb}`.

##### QuerySubsystem_ElementRequiredVerb_MissingElement_ThrowsArgumentException

Verifies that omitting `--element` for any of the 10 verbs that require it (all except
`list`/`find`) throws an `ArgumentException` mentioning `--element`.

##### QuerySubsystem_ListVerb_NoElementNoFiles_ReportsNoInputFilesError / QuerySubsystem_FindVerb_NoElementNoFiles_ReportsNoInputFilesError

Verifies that `list`/`find` do not require `--element`, and that omitting input files (the
next validation step) produces the "no input files" error and exit code 1.

##### QuerySubsystem_FormatMarkdown_DispatchesWithoutError / QuerySubsystem_FormatJson_DispatchesWithoutError

Verifies that both accepted `--format` values parse and render successfully end-to-end.

##### QuerySubsystem_GlobPattern_ResolvesMultipleFiles

Regression test for the glob-expansion bug fix: verifies that a glob pattern such as
`*.sysml` (previously treated as a literal, never-matching file name) now resolves to every
matching `.sysml` file in the target directory via the shared `GlobFileCollector`, and that
the query dispatches successfully against the resulting multi-file workspace.

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

##### QuerySubsystem_QueryVerbHelp_MentionsExampleInvocationAndSchemaHints (theory, 12 cases)

Verifies that `query <verb> --help`, for every one of the 12 verbs, contains that verb's
real example-invocation substring (drawn from the `VehicleExample` fixture, per the
planning report's verified enrichment content) and the shared Markdown/JSON schema-hint
substrings (`"Qualified Name"` for Markdown, `"QualifiedName"` for JSON — matching the real
`QueryResultRenderer`/`QueryResultSerializerContext` output shape, verified by direct CLI
invocation during implementation). Satisfies `SysML2Tools-Tool-Query-HelpEnrichment`.

##### ResxResource_EveryKey_ResolvesToNonEmptyText / ResxResource_KeysAndAccessorProperties_AreInBidirectionalParity (ResxResourceTests.cs)

For the `QueryStrings` resource base name/accessor pair (one of four covered by these theory
tests), every key discovered in `Query/QueryStrings.resx`'s invariant-culture resource set
resolves to non-null/non-empty text via `ResourceManager`, and every such key (including the
12 `Query_Example_*` keys, each backed by its own accessor property) has a matching
`public static string` property on `QueryStrings` (and vice versa). Satisfies
`SysML2Tools-Tool-Query-LocalizableHelpText`.

##### QueryVerbsTests.cs

One or more `[Fact]`/`[Theory]` methods per verb, each using a small inline `.sysml`
fixture written to a temp file and run end-to-end via `Context.Create` +
`Program.RunAsync`, asserting on captured stdout content: qualified names of expected
entries, edge/kind labels, `--walk-depth`/`--direction` bounding, annotation text, and
`--include-stdlib` on/off behavior.

**`Dependencies_CombinesOutgoingAndIncoming_ReportsBothDirections`**,
**`Dependencies_NoOutgoingReferences_ReportsProseLineInsteadOfBulletList`**,
**`Dependencies_NoIncomingReferences_ReportsProseLineInsteadOfBulletList`**,
**`Dependencies_MarkdownOutput_ContainsNoTable`**: Verify, end-to-end, that `dependencies`
reports both a "Depends on" bullet (outgoing) and a "Used by" bullet (incoming) for an
element with both directions populated; that an element with no outgoing references
reports the single `"{Element} has no outgoing references."` prose line instead of a bullet
list; that an element with no incoming references reports the single `"No elements
reference {Element}."` prose line instead of a bullet list; and that the rendered Markdown
never contains the `"| Qualified Name | Kind | Detail |"` table header used by every other
verb. Satisfies `SysML2Tools-Tool-Query-Dependencies`.

##### QueryRenderingTests.cs

Direct unit tests of `QueryResultRenderer.RenderMarkdown`/`RenderJson` against hand-built
`QueryResult` instances (no workspace/model involved): empty-entries rendering, sort-order
correctness, `Detail`/`Notes` rendering, and JSON round-trip via
`QueryResultSerializerContext`. Plus one end-to-end test confirming Markdown and JSON
outputs for the same query contain the same qualified names in the same order.

**`RenderMarkdown_DefaultArguments_ProducesUnchangedTopLevelHeading`**,
**`RenderMarkdown_CustomDepth_UsesThatManyHeadingHashes`**,
**`RenderMarkdown_CustomHeading_ReplacesAutoGeneratedText`**,
**`RenderMarkdown_CustomDepthAndHeading_CombinesBothOverrides`**: Verify that
`RenderMarkdown`'s default arguments produce the unchanged single `#` heading; that a custom
`depth` changes the number of leading `#` characters; that a custom `heading`
replaces the auto-generated heading text entirely (no merging with verb/element info); and
that both overrides combine correctly when supplied together.

**`RenderMarkdown_DependenciesVerb_RendersBulletProseNotTable`**,
**`RenderMarkdown_DependenciesVerb_EmptyOutgoingAndIncoming_ReportsBothProseLines`**,
**`RenderJson_DependenciesVerb_IncludesDirectionField`**,
**`RenderJson_NonDependenciesVerb_DirectionFieldOmittedFromOutput`**,
**`Dependencies_DepthAndHeadingOptions_ApplyToHeadingLikeOtherVerbs`**: Unit-test
`dependencies`'s prose-bullet rendering directly against a hand-built, intentionally
unordered `QueryResult` (confirming per-direction ordinal sorting and the exact bullet/intro
text, now using shortened names since the fixture's subject and entries share the common
leading segment `"Model"`); confirm the both-directions-empty edge case reports both prose
lines with no bullets and no `"_No entries._"` fallback; confirm `RenderJson` includes a
populated `Direction` field (`Outgoing`/`Incoming`) for `dependencies` entries; confirm — the
critical regression test — that `RenderJson` for a non-`dependencies` verb (`uses`) never
contains the substring `"Direction"` in its JSON output, proving the `[JsonIgnore(Condition =
JsonIgnoreCondition.WhenWritingNull)]` attribute keeps every other verb's JSON output
unaffected by the new field; and confirm `--depth`/`--heading` apply to `dependencies`'s
heading line exactly like every other verb (now asserting the shortened bullet text). Satisfies
`SysML2Tools-Tool-Query-Dependencies`.

**`RenderMarkdown_DependenciesVerb_NoCommonPrefix_LeavesNamesFullyQualified`**: Confirms that
when the subject element and its entries share no common leading segment (different top-level
packages), `dependencies`'s Markdown output leaves every name fully qualified in both the
subject sentence and the bullets. Satisfies
`SysML2Tools-Tool-Query-DependenciesNameShortening`.

**`RenderJson_DependenciesVerb_NamesRemainFullyQualified`**: Explicit before/after regression
test — for a single hand-built `dependencies` `QueryResult` whose subject and entries share the
common leading segment `"Model"` (which Markdown shortens), confirms that `RenderJson`'s output
for the very same result keeps every `QualifiedName`/`Element` value fully qualified, proving
the shortening applies only to `RenderMarkdown`. Satisfies
`SysML2Tools-Tool-Query-DependenciesNameShortening`.

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

##### QuerySubsystem_FormatJson_UnaffectedByDepthOrHeading (QuerySubsystemTests.cs)

Verifies that `--format json` output is byte-identical whether or not `--depth`/
`--heading` are also supplied, confirming those two options are Markdown-output-only.

##### QueryErrorPathTests.cs

Covers: element not found (`context.WriteError` message contains "not found in the
workspace", exit code 1); `find` without `--kind`/`--name` (`ArgumentException`);
unsupported `--format` value (`ArgumentException`); a non-integer `--walk-depth` value
(`ArgumentException` naming `--walk-depth`); a file with parse errors (diagnostics
reported, command completes best-effort); no input files supplied (exit code 1); a file
pattern that matches no file on disk (`context.WriteError` message contains "no files
matched", exit code 1, regression test for the glob-expansion bug fix — see
`QuerySubsystem_GlobPattern_ResolvesMultipleFiles` in `QuerySubsystemTests.cs` for the
corresponding success-path regression proving a glob pattern such as `*.sysml` now resolves
multiple files instead of being treated as a literal, never-matching file name).

##### Context_Create_QueryCommand_WithVerbToken_SetsQueryVerb (ContextTests.cs)

Verifies, for each of the 12 verb tokens, that `Context.Create(["query", token, "--element",
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

##### Context_Create_QueryCommand_WithWalkDepthFlag_SetsQueryWalkDepth (ContextTests.cs)

Verifies that `--walk-depth 3` populates `Query.WalkDepth` without affecting the global
`Context.HeadingDepth` (which remains at its default of 1, since `--walk-depth` and
`--depth` are distinct, unrelated flags).

##### Context_Create_QueryCommand_WithoutHeading_LeavesHeadingNull (ContextTests.cs)

##### Context_Create_QueryCommand_WithHeadingFlag_SetsHeading (ContextTests.cs)

Verifies that `--heading` defaults to `null` when not supplied, and populates
`Query.Heading` when supplied.

##### Context_Create_QueryCommand_WithFiles_SetsQueryFilesNotTopLevelFiles (ContextTests.cs)

Verifies that file globs supplied after the verb token populate `Query.Files` while
`Context.Files` remains empty.
