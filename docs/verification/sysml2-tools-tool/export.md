### DemaConsulting.SysML2Tools.Tool — Export Subsystem Verification

#### Verification Approach

The Export subsystem is verified using unit/integration tests in two files under
`test/DemaConsulting.SysML2Tools.Tool.Tests/Export/` (`ExportSubsystemTests.cs`,
`ExportRenderingTests.cs`), plus export-specific parsing tests in
`test/DemaConsulting.SysML2Tools.Tool.Tests/Cli/ContextTests.cs` and a self-test coverage
test in `test/DemaConsulting.SysML2Tools.Tool.Tests/SelfTest/ValidationTests.cs`. Tests
invoke `Context.Create`, `ExportCommand.RunAsync`, `Program.RunAsync`, and (for the
end-to-end scenario) the built tool as a real process via `Runner.Run`, asserting on
captured console output, exit code, produced file content, and deserialized JSON/JSONL
shape. Tests run against all three target frameworks.

#### Test Environment

- Framework: xUnit v3
- Target frameworks: net8.0, net9.0, net10.0
- Test project: `DemaConsulting.SysML2Tools.Tool.Tests`
- Dependencies: `DemaConsulting.SysML2Tools.Tool` (internal access via `InternalsVisibleTo`);
  OMG reference models under `test/SysMLModels/OMG/` (locatable via a repo-root search
  upward from the test assembly's output directory).

#### Acceptance Criteria

- `--format json` (and the default, no `--format`) and `--format jsonl` both parse and
  dispatch without error; an unrecognized `--format` value produces an `ArgumentException`.
- `--output <file>` writes the rendered document to the named file instead of stdout.
- `--include-stdlib` toggles whether stdlib-seeded declarations/edges appear in the export;
  omitting it excludes them by default, matching `query`'s convention.
- Diagnostics are always present in the export output regardless of `--include-stdlib`.
- Unrecognized `-`-prefixed flags are rejected with an `ArgumentException`.
- No input files produces the "no input files" error and exit code 1; one or more patterns
  matching zero files produces the "no files matched" error and exit code 1.
- JSON output is a single indented document with a qualified-name-keyed `Declarations`
  object, an `Edges` array, and a `Diagnostics` array; declaration nodes carry their
  polymorphic `$type` discriminator and round-trip through `SysmlNode`'s own converter.
- JSONL output is one compact JSON object per line, each carrying a `"Kind"` discriminator
  (`"declaration"`/`"edge"`/`"diagnostic"`), with declarations, then edges, then diagnostics.
- A full CLI invocation (`export --format json`/`export --format jsonl --output <path>`)
  against a real `test/SysMLModels` fixture produces valid, deserializable JSON/JSONL
  containing non-empty declarations, edges, and diagnostics, with stdlib content correctly
  included/excluded per `--include-stdlib`.
- `export --help` prints usage/help text without throwing, including an explicit note that
  `--output` names a file (not a directory, unlike `render`'s `--output`).
- The `SysML2Tools_ExportSelfTest` self-test (part of `--validate`) passes.
- Existing `lint`/`render`/`query` test suites continue to pass unmodified, confirming no
  regression.

#### Test Scenarios

##### ExportSubsystemTests.cs

**`ExportArgumentParser_NoFlags_ProducesDefaults`**: Verifies that parsing with only file
globs produces `Format = null`, `Output = null`, `IncludeStdlib = false`.

**`ExportArgumentParser_FormatFlag_CapturesRawValue`**: Verifies `--format jsonl` populates
`Format`.

**`ExportArgumentParser_OutputFlag_CapturesValue`**: Verifies `--output out.json` populates
`Output`.

**`ExportArgumentParser_IncludeStdlibFlag_SetsTrue`**: Verifies `--include-stdlib` sets
`IncludeStdlib` to `true`.

**`ExportArgumentParser_UnrecognizedFlag_ThrowsArgumentException`**: Verifies an unknown
`-`-prefixed token throws `ArgumentException`.

**`ExportArgumentParser_FormatFlagMissingValue_ThrowsArgumentException`** /
**`ExportArgumentParser_OutputFlagMissingValue_ThrowsArgumentException`**: Verifies a
trailing `--format`/`--output` with no following value throws `ArgumentException`.

**`ExportSubsystem_FormatJson_DispatchesAndPrintsJson`** /
**`ExportSubsystem_NoFormat_DefaultsToJson`** /
**`ExportSubsystem_FormatJsonl_DispatchesAndPrintsJsonLines`**: Verifies each accepted
`--format` value (and the default) dispatches successfully and produces output in the
expected shape.

**`ExportSubsystem_InvalidFormat_ThrowsArgumentException`**: Verifies an unsupported
`--format` value throws `ArgumentException` naming the bad value.

**`ExportSubsystem_OutputFlag_WritesToFileInsteadOfStdout`**: Verifies `--output <path>`
writes the rendered document to the named file and that stdout does not contain the JSON
body.

**`ExportSubsystem_NoIncludeStdlib_ExcludesStdlib`** /
**`ExportSubsystem_IncludeStdlib_IncludesStdlibAndIncreasesSize`**: Verifies stdlib
declarations/edges are excluded by default and included (with measurably larger output) when
`--include-stdlib` is supplied.

**`ExportSubsystem_NoFiles_ReportsNoInputFilesError`** /
**`ExportSubsystem_NoMatchingFiles_ReportsNoFilesMatchedError`**: Verifies the two
file-resolution error paths report the expected message and exit code 1.

**`ExportSubsystem_ExportHelp_PrintsHelpWithoutThrowing`**: Verifies `export --help` prints
help text (including the `--output` file-vs-directory clarification) and exits with code 0.

##### ExportRenderingTests.cs

**`ExportResultSerializerContext_DefinitionNode_RoundTripsTypeDiscriminator`** /
**`ExportResultSerializerContext_FeatureNode_UsesFeatureTypeDiscriminator`**: Verifies JSON
output for definition/feature declarations carries the correct `$type` discriminator and
deserializes back to an equivalent node.

**`ExportResultSerializerContext_EdgeKind_RoundTripsExactly`**: Verifies each
`SysmlEdgeKind` value serializes/deserializes to the same enum value in JSON output.

**`ExportResultSerializerContext_Diagnostic_AllFieldsPresent`**: Verifies diagnostic entries
in JSON output carry their expected fields (message, severity, location).

**`ExportLineSerializerContext_DeclarationLine_HasKindDiscriminatorAndIsCompact`** /
**`ExportLineSerializerContext_EdgeLine_HasKindDiscriminatorAndIsCompact`** /
**`ExportLineSerializerContext_DiagnosticLine_HasKindDiscriminatorAndIsCompact`**: Verifies
each JSONL line-wrapper type serializes with the correct `"Kind"` value and is a single
compact (non-indented, single-line) JSON object.

**`ExportIntegration_RealFixture_ProducesValidJsonAndJsonl`**: A full CLI end-to-end test
(via `Runner.Run`, mirroring `IntegrationTests.cs`'s process-invocation style) against
`test/SysMLModels/OMG/examples/VehicleExample/VehicleDefinitions.sysml`: runs
`export --format json` and asserts the captured output contains a valid, deserializable JSON
document with non-empty `Declarations`/`Edges`/`Diagnostics`; runs
`export --format jsonl --output <path>` and asserts the file contains one JSON object per
line, each parseable and carrying the expected `"Kind"` discriminator. Satisfies
`SysML2Tools-Tool-Export-StdlibFilter`, `SysML2Tools-Tool-Export-JsonEnvelope`, and
`SysML2Tools-Tool-Export-JsonlEnvelope`.

##### Validation_RunExportSelfTest_ValidModel_Passes (ValidationTests.cs)

Verifies that `Validation.RunExportSelfTestAsync` passes against the built-in self-test
model, reporting a passing `TestResult` named `SysML2Tools_ExportSelfTest`. Satisfies
`SysML2Tools-Tool-Export-SelfTest`.
