# DemaConsulting.SysML2Tools.Tool

## Verification Approach

System-level verification uses end-to-end integration tests that invoke the tool as a real
process via the `Runner.Run` helper in `IntegrationTests.cs`. Each test exercises the full
stack — argument parsing, dispatch, execution, and output — and validates both exit code and
combined console output. The tests treat the tool as a black box and assert only on observable
outputs; no internal implementation details are assumed.

`Runner.Run` merges stdout and stderr into a single combined output string. Per-stream assertions
(e.g., "standard error is empty") are not possible at this level; all assertions are made
against the combined output.

## Test Environment

Integration tests run on .NET 8.0, .NET 9.0, and .NET 10.0 across Windows, Linux, and macOS.
All scenarios are expected to produce identical results on all supported runtime and platform
combinations. Temporary files and directories are created and cleaned up within each test.

## Acceptance Criteria

- All integration tests pass with zero failures across all supported runtimes and platforms.
- Exit code 0 is returned for all valid invocations.
- Exit code non-zero is returned for all invalid argument combinations.
- Results files are created at the specified paths when `--results` is used with `--validate`.
- Silent mode (`--silent`) produces empty combined output.

## Test Scenarios

**SysML2Tools_VersionFlag_Provided_OutputsVersion**: The `--version` flag is passed as
the sole argument; the tool outputs the version string and exits with code 0. This scenario is
tested by `SysML2Tools_VersionFlag_Provided_OutputsVersion`.

**SysML2Tools_HelpFlag_Provided_OutputsUsageInformation**: The `--help` flag is passed
as the sole argument; the combined output contains "Usage" and "Options" and the tool exits
with code 0. This scenario is tested by
`SysML2Tools_HelpFlag_Provided_OutputsUsageInformation`.

**SysML2Tools_ValidateFlag_Provided_RunsValidation**: The `--validate` flag is passed as
the sole argument; the combined output contains "Total Tests:" and the tool exits with code 0,
confirming the self-validation suite runs and completes successfully. This scenario is tested by
`SysML2Tools_ValidateFlag_Provided_RunsValidation`.

**SysML2Tools_ValidateWithTrxResults_Requested_GeneratesTrxFile**: The `--validate` flag
is combined with `--results <path>.trx`; a TRX file containing a `<TestRun` XML element is
created at the specified path and the tool exits with code 0. This scenario is tested by
`SysML2Tools_ValidateWithTrxResults_Requested_GeneratesTrxFile`.

**SysML2Tools_ValidateWithXmlResults_Requested_GeneratesJUnitFile**: The `--validate` flag
is combined with `--results <path>.xml`; a JUnit XML file containing a `<testsuites` XML element
is created at the specified path and the tool exits with code 0. This scenario is tested by
`SysML2Tools_ValidateWithXmlResults_Requested_GeneratesJUnitFile`.

**SysML2Tools_SilentFlag_Provided_SuppressesOutput**: The `--version` and `--silent`
flags are passed together; the combined output is empty or whitespace-only while the tool exits
with code 0, confirming silent mode suppresses all console output. This scenario is tested by
`SysML2Tools_SilentFlag_Provided_SuppressesOutput`.

**SysML2Tools_LogFlag_Provided_WritesOutputToFile**: The `--log <path>` flag is passed
pointing to a temporary file; the tool exits with code 0 and the log file is created containing
output that also appears in the combined console output. This scenario is tested by
`SysML2Tools_LogFlag_Provided_WritesOutputToFile`.

**SysML2Tools_UnknownArgument_Provided_ReturnsError**: An unrecognized argument
(`--unknown`) is passed; the tool exits with a non-zero code and the combined output contains
an error message identifying the unknown argument. This scenario is tested by
`SysML2Tools_UnknownArgument_Provided_ReturnsError`.

**SysML2Tools_ValidateWithDepth_DepthThree_OutputsCorrectHeadingLevel**: The `--validate`
flag is combined with `--depth 3`; the combined output contains `###` (heading at depth 3) and
the tool exits with code 0. This scenario is tested by
`SysML2Tools_ValidateWithDepth_DepthThree_OutputsCorrectHeadingLevel`.

**SysML2Tools_NoArguments_Invoked_DisplaysBanner**: The tool is invoked with no
arguments; the combined output contains the tool name and copyright notice and the exit code is
0. This scenario is tested by `SysML2Tools_NoArguments_Invoked_DisplaysBanner`.

**SysML2Tools_ResultAlias_LegacyFlag_WritesResultsFile**: The `--validate` flag is
combined with `--result <path>.trx` (the legacy alias); a TRX file is created at the specified
path and the tool exits with code 0. This scenario is tested by
`SysML2Tools_ResultAlias_LegacyFlag_WritesResultsFile`.

**SysML2Tools_ValidateWithBadExtension_ExtensionInvalid_ReturnsNonZero**: The `--validate`
flag is combined with `--results <path>.bad` (unsupported extension); the tool exits with a
non-zero code and no file is created at the specified path. This scenario is tested by
`SysML2Tools_ValidateWithBadExtension_ExtensionInvalid_ReturnsNonZero`.
