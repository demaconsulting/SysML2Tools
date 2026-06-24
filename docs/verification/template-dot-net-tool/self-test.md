## SelfTest

### Verification Approach

The `SelfTest` subsystem is verified by integration tests defined in
`SelfTestSubsystemTests.cs`. Each test exercises `Validation.Run` with a real `Context` to
confirm that the subsystem produces correct output and result files across the supported
result-format options. No mocking is applied; `Context`, `Program`, and `PathHelpers` all
execute their real implementations. Temporary directories are used for result file output to
keep tests isolated and leave no permanent file-system side-effects.

### Test Environment

N/A - standard test environment.

### Acceptance Criteria

- All integration tests pass with zero failures.
- `Validation.Run` completes successfully with exit code 0 when no result file is requested.
- TRX and JUnit XML result files are generated with the correct XML root elements when valid
  file paths are supplied.
- An unsupported result file extension produces exit code 1 and no file is created.

### Test Scenarios

**SelfTestSubsystem_ValidationWorkflow_NoResultFiles_CompletesSuccessfully**:
`Validation.Run` is called with a context that does not specify any results file; validation
completes without error, exit code is 0, and the validate flag is set. This scenario is tested
by `SelfTestSubsystem_ValidationWorkflow_NoResultFiles_CompletesSuccessfully`.

**SelfTestSubsystem_ValidationWorkflow_WithTrxFile_GeneratesResults**: `Validation.Run` is
called with a context whose `ResultsFile` points to a temporary `.trx` path; a TRX file
containing the `<TestRun` XML element is created and exit code is 0. This scenario is tested
by `SelfTestSubsystem_ValidationWorkflow_WithTrxFile_GeneratesResults`.

**SelfTestSubsystem_ValidationWorkflow_WithJUnitFile_GeneratesResults**: `Validation.Run` is
called with a context whose `ResultsFile` points to a temporary `.xml` path; a JUnit XML file
containing the `<testsuites` XML element is created and exit code is 0. This scenario is tested
by `SelfTestSubsystem_ValidationWorkflow_WithJUnitFile_GeneratesResults`.

**SelfTestSubsystem_ValidationWorkflow_WithBothResultFiles_GeneratesBothResults**: Two separate
`Validation.Run` calls are made, one targeting a `.trx` path and one targeting a `.xml` path;
both files are created with the correct root XML elements and exit code is 0 for each run. This
scenario is tested by
`SelfTestSubsystem_ValidationWorkflow_WithBothResultFiles_GeneratesBothResults`.

**SelfTestSubsystem_ValidationWorkflow_WithUnsupportedExtension_EmitsErrorAndNoFile**:
`Validation.Run` is called with a context whose `ResultsFile` has a `.bad` extension; no file
is created at the specified path, `context.ExitCode` is 1, and an error message identifying the
unsupported format is written via `context.WriteError`. This scenario is tested by
`SelfTestSubsystem_ValidationWorkflow_WithUnsupportedExtension_EmitsErrorAndNoFile`.
