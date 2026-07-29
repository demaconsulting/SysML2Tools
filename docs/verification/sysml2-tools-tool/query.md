## DemaConsulting.SysML2Tools.Tool — Query Subsystem Verification

### Verification Approach

The Tool Query subsystem is verified using unit/integration tests in
`test/DemaConsulting.SysML2Tools.Tool.Tests/Query/QuerySubsystemTests.cs`,
`QueryVerbsTests.cs`, and `QueryErrorPathTests.cs`, plus query-specific parsing tests in
`test/DemaConsulting.SysML2Tools.Tool.Tests/Cli/ContextTests.cs` and shared resx-parity tests
in `test/DemaConsulting.SysML2Tools.Tool.Tests/Resources/ResxResourceTests.cs`. These tests
invoke `Context.Create`, `QueryCommand.RunAsync`, and `Program.RunAsync`, asserting on parsed
state, captured console output, exit code, and file-output behavior. Detailed verb semantics,
renderer behavior, and Core-side file-export helper behavior are verified separately in
`docs/verification/sysml2-tools-core/query.md`.

### Test Environment

- Framework: xUnit v3
- Target framework: net10.0
- Test project: `DemaConsulting.SysML2Tools.Tool.Tests`
- Dependencies: `DemaConsulting.SysML2Tools.Tool` (internal access via `InternalsVisibleTo`);
  inline temporary `.sysml` fixtures in the Query test helpers; OMG reference models only where
  explicitly used by the Core Query subsystem tests.

### Acceptance Criteria

- All 12 verb tokens parse to the matching `QueryVerb` and dispatch through `QueryCommand` to
  the shared Core Query API, producing exit code 0 for valid input.
- An unrecognized verb token produces an `ArgumentException` naming the bad token.
- `--element`/`-e` is required for every verb except `list`/`find`; omitting it for a verb that
  requires it produces an `ArgumentException`.
- `find` without `--kind`/`--name` produces an `ArgumentException`.
- `--format markdown` and `--format json` both parse and render without error; an
  unrecognized `--format` value produces an `ArgumentException`.
- `--depth` and `--heading` affect Markdown output only and leave `--format json` output
  unchanged.
- `--output <file>` writes the rendered document to the named file instead of stdout.
- Help output remains localized through `QueryStrings`, including the workflow note, example
  invocations, schema hints, and the `--output` help text.
- `--include-connections` is accepted by the CLI without any Tool-side parsing (it is forwarded
  verbatim to Core's parser) and sets `QueryOptions.IncludeConnections`, and it is documented in
  both general help and `impact` verb help through `QueryStrings`.
- Error paths are covered: no input files, patterns supplied but none matching on disk, target
  element not found, invalid `--walk-depth`, invalid `--format`, and parse-error-containing
  input files that still complete best-effort.
- Detailed verb semantics, deterministic rendering rules, and Core exporter behavior are owned
  by the Core Query subsystem verification; the Tool layer proves orchestration, parsing,
  dispatch, and CLI reporting only.

### Test Scenarios

#### QuerySubsystemTests.cs

**`QuerySubsystem_AnyVerb_WithValidInput_DispatchesToRealLogic`** (theory, 12 cases): Verifies
that each verb dispatches successfully through `QueryCommand` and produces non-error output.
This is the Tool-side proof that the CLI adapter reaches the shared Core implementation.

#### QuerySubsystem_ElementRequiredVerb_MissingElement_ThrowsArgumentException

Verifies that omitting `--element` for any of the 10 element-scoped verbs throws an
`ArgumentException` mentioning `--element`.

#### QuerySubsystem_ListVerb_NoElementNoFiles_ReportsNoInputFilesError /

#### QuerySubsystem_FindVerb_NoElementNoFiles_ReportsNoInputFilesError

Verifies that `list`/`find` do not require `--element`, and that the next validation step
correctly reports the "no input files" error.

#### QuerySubsystem_FormatMarkdown_DispatchesWithoutError /

#### QuerySubsystem_FormatJson_DispatchesWithoutError

Verifies that both accepted `--format` values parse and render successfully end-to-end.

#### QuerySubsystem_FormatJson_UnaffectedByDepthOrHeading

Verifies that `--format json` output is unchanged when `--depth` and `--heading` are also
supplied, proving those flags are Markdown-only in the Tool layer as well.

#### QuerySubsystem_GlobPattern_ResolvesMultipleFiles

Verifies that a glob pattern such as `*.sysml` resolves through the shared `GlobFileCollector`
and that the resulting multi-file workspace is queried successfully.

#### QuerySubsystem_UnknownVerb_ThrowsArgumentException

Verifies that `Context.Create(["query", "bogus"])` throws an `ArgumentException` naming
`bogus`.

#### QuerySubsystem_QueryHelp_NoVerb_PrintsGeneralHelpWithoutThrowing

Verifies that `query --help` prints general help and returns exit code 0.

#### QuerySubsystem_QueryHelp_NoVerb_MentionsTypicalWorkflow

Verifies that the general-help path includes the workflow note recommending `list`/`find`
before element-scoped verbs.

#### QuerySubsystem_QueryHelp_NoVerb_MentionsIncludeConnectionsOption

Verifies that `query --help` prints the `--include-connections` line, including the
general-help-only qualifier `'impact' verb only`, so the option is discoverable from the
command's overall option list and not only from `impact` verb help.

#### QuerySubsystem_QueryVerbHelp_MentionsExampleInvocationAndSchemaHints (theory, 12 cases)

Verifies that `query <verb> --help` prints the real example invocation and shared
Markdown/JSON schema hints for every verb.

#### QuerySubsystem_QueryVerbHelp_WithVerb_PrintsVerbHelpWithoutThrowing

Verifies that `query uses --help` prints verb-specific help and returns exit code 0 without
requiring `--element`.

#### QuerySubsystem_ImpactVerbHelp_MentionsIncludeConnectionsOption

Verifies that `query impact --help` prints the `--include-connections` option line, keeping the
Tool's resx-sourced help text in lockstep with the grammar Core's parser actually accepts.

#### Dependencies_DepthAndHeadingOptions_ApplyToHeadingLikeOtherVerbs

Verifies end-to-end that the CLI passes heading-depth and heading-text options through to the
shared Markdown renderer even for the `dependencies` verb's special prose output.

#### Query_MarkdownAndJsonFormats_AgreeOnEntryContentAndOrder

Verifies through the full CLI path that Markdown and JSON output contain the same entry content
and deterministic order for the same query.

#### QuerySubsystem_OutputFlag_WritesToFileInsteadOfStdout

Verifies `--output <path>` writes the rendered document to the named file and that stdout does
not contain the query body.

#### QueryVerbsTests.cs

One or more `[Fact]` methods per verb use small inline fixtures and the full CLI path
(`Context.Create` + `Program.RunAsync`) to prove that each verb-specific command-line shape
reaches the Core Query API correctly. The detailed behavior of each verb itself is documented
and verified in `docs/verification/sysml2-tools-core/query.md`.

#### QueryErrorPathTests.cs

Covers: element not found (`context.WriteError` contains "not found in the workspace");
`find` without `--kind`/`--name`; unsupported `--format`; invalid `--walk-depth`; a file with
parse errors (diagnostics reported, command completes best-effort); no input files supplied;
and a file pattern that matches no file on disk.

#### ContextTests.cs

**`Context_Create_QueryCommand_WithVerbToken_SetsQueryVerb`**: Verifies that each verb token
parses to the matching `QueryVerb`.

**`Context_Create_QueryCommand_UnknownVerb_ThrowsArgumentException`**: Verifies that an
unrecognized verb token is rejected during parsing.

**`Context_Create_QueryCommand_NoVerbWithHelp_LeavesQueryNull`**: Verifies that `query --help`
leaves `Context.Query` null so the general-help path can run without a fake verb.

**`Context_Create_QueryCommand_WithElementFlag_SetsElement`** /
**`Context_Create_QueryCommand_WithShortElementFlag_SetsElement`**: Verify that `--element` and
`-e` populate `Query.Element`.

**`Context_Create_QueryCommand_WithDirectionFlag_SetsDirection`** /
**`Context_Create_QueryCommand_WithKindFlag_SetsKind`** /
**`Context_Create_QueryCommand_WithNameFlag_SetsNameFilter`** /
**`Context_Create_QueryCommand_WithIncludeStdlibFlag_SetsIncludeStdlibTrue`**: Verify parsing of
query-specific option fields.

**`Context_Create_QueryCommand_WithIncludeConnectionsFlag_SetsIncludeConnectionsTrue`**:
Verifies that `--include-connections` sets `Query.IncludeConnections`, proving the Tool CLI
forwards the flag to Core's parser without any Tool-side parsing of its own.

**`Context_Create_QueryCommand_WithFormatMarkdown_SetsQueryFormat`** /
**`Context_Create_QueryCommand_WithFormatJson_SetsQueryFormat`**: Verify that query's
`--format` is parsed independently of render's `--format`.

**`Context_Create_QueryCommand_WithWalkDepthFlag_SetsQueryWalkDepth`**: Verifies that
`--walk-depth` populates `Query.WalkDepth` without affecting the global heading depth.

**`Context_Create_QueryCommand_WithoutHeading_LeavesHeadingNull`** /
**`Context_Create_QueryCommand_WithHeadingFlag_SetsHeading`**: Verify heading-text parsing.

**`Context_Create_QueryCommand_WithFiles_SetsQueryFilesNotTopLevelFiles`**: Verifies that file
patterns supplied after the verb populate `Context.QueryFiles` rather than the top-level file
list used by other commands.

#### ResxResourceTests.cs

`ResxResource_EveryKey_ResolvesToNonEmptyText` and
`ResxResource_KeysAndAccessorProperties_AreInBidirectionalParity` verify that `QueryStrings`
remains a complete, culture-aware accessor over `QueryStrings.resx`, including the new
`--output` help text lines.
