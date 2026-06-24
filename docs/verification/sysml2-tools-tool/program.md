## Program

### Verification Approach

`Program` is verified with unit tests defined in `ProgramTests.cs`. Because `Program` directly
instantiates `Context` from real arguments and calls `Validation.Run` when needed, no mocking
is required. Tests pass controlled argument arrays and assert on captured console output and
exit codes.

### Test Environment

N/A - standard test environment.

### Acceptance Criteria

- All unit tests pass with zero failures.
- Exit code 0 is returned for all valid flag combinations.
- Exit code 1 is returned when invalid arguments are supplied to `Program.Main`.
- `Program.Version` returns a non-empty, non-null string.

### Test Scenarios

**Program_Run_WithVersionFlag_DisplaysVersionOnly**: `Program.Run` is called with a context
created from `["--version"]`; the output contains the version string, "Copyright" does not
appear, the banner prefix does not appear, and exit code is 0, confirming version-only output
with no banner. This scenario is tested by `Program_Run_WithVersionFlag_DisplaysVersionOnly`.

**Program_Run_WithHelpFlag_DisplaysUsageInformation**: `Program.Run` is called with a context
from `["--help"]`; the output contains "Usage:", "Options:", "--version", and "--help" and exit
code is 0. This scenario is tested by `Program_Run_WithHelpFlag_DisplaysUsageInformation`.

**Program_Run_WithValidateFlag_RunsValidation**: `Program.Run` is called with a context from
`["--validate"]`; the output contains "Total Tests:" and exit code is 0, confirming the
self-validation suite is invoked and completes successfully. This scenario is tested by
`Program_Run_WithValidateFlag_RunsValidation`.

**Program_Run_NoArguments_DisplaysDefaultBehavior**: `Program.Run` is called with an empty
argument array; the output contains the tool name and copyright notice and exit code is 0.
This scenario is tested by `Program_Run_NoArguments_DisplaysDefaultBehavior`.

**Program_Version_ReturnsNonEmptyString**: The `Program.Version` static property is read; the
returned string is non-empty and non-null, confirming the version is resolvable from the
assembly attributes. This scenario is tested by `Program_Version_ReturnsNonEmptyString`.

**Program_Main_WithInvalidArgs_ReturnsNonZeroExitCode**: `Program.Main` is invoked with
`["--invalid-argument"]`; the exit code is 1, confirming that invalid arguments are caught and
handled without re-throwing. This scenario is tested by
`Program_Main_WithInvalidArgs_ReturnsNonZeroExitCode`.

**Program_Run_WithShortVersionFlag_DisplaysVersion**: `Program.Run` is called with a context
from `["-v"]`; the output contains the version string and exit code is 0. This scenario is
tested by `Program_Run_WithShortVersionFlag_DisplaysVersion`.

**Program_Run_WithShortHelpFlag_DisplaysUsage**: `Program.Run` is called with a context from
`["-h"]`; the output contains "Usage:" and "Options:" and exit code is 0. This scenario is
tested by `Program_Run_WithShortHelpFlag_DisplaysUsage`.

**Program_Run_WithQuestionMarkFlag_DisplaysUsage**: `Program.Run` is called with a context from
`["-?"]`; the output contains "Usage:" and "Options:" and exit code is 0. This scenario is
tested by `Program_Run_WithQuestionMarkFlag_DisplaysUsage`.
