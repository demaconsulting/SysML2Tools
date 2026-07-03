### DemaConsulting.SysML2Tools.Tool — Query Subsystem Verification

#### Verification Approach

The Query subsystem is verified using unit tests in
`test/DemaConsulting.SysML2Tools.Tool.Tests/Query/QuerySubsystemTests.cs`, plus
query-specific parsing tests appended to
`test/DemaConsulting.SysML2Tools.Tool.Tests/Cli/ContextTests.cs`. Tests invoke
`Context.Create`, `QueryCommand.RunAsync`, and `Program.RunAsync` with controlled argument
arrays and assert on the resulting `Context` properties, captured console output, and exit
code. Tests run against all three target frameworks.

#### Test Environment

- Framework: xUnit v3
- Target frameworks: net8.0, net9.0, net10.0
- Test project: `DemaConsulting.SysML2Tools.Tool.Tests`
- Dependencies: `DemaConsulting.SysML2Tools.Tool` (internal access via `InternalsVisibleTo`)

#### Acceptance Criteria

- All 11 verb tokens (`uses`, `used-by`, `impact`, `describe`, `hierarchy`, `requirements`,
  `interface`, `connections`, `states`, `list`, `find`) parse to the matching `QueryVerb` and
  dispatch to the "not yet implemented" stub, producing exit code 1.
- An unrecognized verb token produces an `ArgumentException` naming the bad token.
- `--element`/`-e` is required for every verb except `list`/`find`; omitting it for a verb
  that requires it produces an `ArgumentException`.
- `list`/`find` succeed without `--element`.
- `--format markdown` and `--format json` both parse without error.
- `query --help` (no verb) and `query <verb> --help` both render help text without throwing
  and without requiring `--element`.
- Query's positional file globs populate `Query.Files`, not the top-level `Context.Files`
  used by `lint`/`render`.
- Existing `lint`/`render` test suites continue to pass unmodified, confirming no regression.

#### Test Scenarios

##### QuerySubsystem_AnyVerb_WithElement_ReportsNotImplementedStub

Verifies that each of the 11 verbs, supplied with `--element` when required, produces a
stderr message containing the verb token and "not yet implemented", and exit code 1.

##### QuerySubsystem_ElementRequiredVerb_MissingElement_ThrowsArgumentException

Verifies that omitting `--element` for any of the 9 verbs that require it (all except
`list`/`find`) throws an `ArgumentException` mentioning `--element`.

##### QuerySubsystem_ListVerb_NoElement_DispatchesToStub

Verifies that `list` without `--element` dispatches to its stub rather than throwing.

##### QuerySubsystem_FindVerb_NoElement_DispatchesToStub

Verifies that `find` without `--element` dispatches to its stub rather than throwing.

##### QuerySubsystem_FormatMarkdown_DispatchesWithoutError / QuerySubsystem_FormatJson_DispatchesWithoutError

Verifies that both accepted `--format` values parse and reach the stub without a parsing
error.

##### QuerySubsystem_UnknownVerb_ThrowsArgumentException

Verifies that `Context.Create(["query", "bogus"])` throws an `ArgumentException` naming
`bogus`.

##### QuerySubsystem_QueryHelp_NoVerb_PrintsGeneralHelpWithoutThrowing

Verifies that `query --help` prints general help (listing the verbs) and returns exit code 0.

##### QuerySubsystem_QueryVerbHelp_WithVerb_PrintsVerbHelpWithoutThrowing

Verifies that `query uses --help` prints verb-specific help and returns exit code 0, without
requiring `--element`.

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

Verifies that `--format markdown`/`--format json` populate `Query.Format`, and that the
`markdown` case does not disturb `RendererFormat`'s existing value (both fields share the
same raw parsed string by design).

##### Context_Create_QueryCommand_WithDepthFlag_SetsQueryDepth (ContextTests.cs)

Verifies that `--depth 3` populates both `Query.Depth` and `MaxRenderDepth` (the same
underlying parsed value, interpreted independently by `query` vs `render`).

##### Context_Create_QueryCommand_WithFiles_SetsQueryFilesNotTopLevelFiles (ContextTests.cs)

Verifies that file globs supplied after the verb token populate `Query.Files` while
`Context.Files` remains empty.
