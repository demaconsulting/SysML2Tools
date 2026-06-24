### Validation

#### Verification Approach

`Validation` is verified with unit tests defined in `ValidationTests.cs`. Tests supply a real
`Context` object (not mocked) with controlled arguments and assert on exit codes, output
content, and result files. `Program` and `PathHelpers` also execute their real implementations;
no test doubles are introduced. Temporary directories are used for result file paths to keep
tests isolated.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- `Validation.Run` throws `ArgumentNullException` for a null context argument.
- The validation summary output contains "Total Tests:", "Passed:", and "Failed:".
- `context.ExitCode` is 0 when all sub-tests pass.
- TRX and JUnit XML result files are created with the correct XML root elements.
- An unsupported result file extension produces no file and an error message on the context.

#### Test Scenarios

**Validation_Run_NullContext_ThrowsArgumentNullException**: `Validation.Run` is called with a
null context argument; an `ArgumentNullException` is thrown, confirming the null guard at the
unit boundary. This scenario is tested by
`Validation_Run_NullContext_ThrowsArgumentNullException`.

**Validation_Run_WithSilentContext_PrintsSummary**: `Validation.Run` is called with a silent
context (output captured separately); the summary contains "Total Tests:", "Passed:", and
"Failed:", confirming the summary is always produced. This scenario is tested by
`Validation_Run_WithSilentContext_PrintsSummary`.

**Validation_Run_WithSilentContext_ExitCodeIsZero**: `Validation.Run` is called with a silent
context; `context.ExitCode` is 0 after the run, confirming all sub-tests pass in the standard
environment. This scenario is tested by `Validation_Run_WithSilentContext_ExitCodeIsZero`.

**Validation_Run_WithTrxResultsFile_WritesTrxFile**: `Validation.Run` is called with a context
whose `ResultsFile` points to a temporary `.trx` path; a file is created at the specified path
and it contains a `<TestRun` XML element. This scenario is tested by
`Validation_Run_WithTrxResultsFile_WritesTrxFile`.

**Validation_Run_WithXmlResultsFile_WritesXmlFile**: `Validation.Run` is called with a context
whose `ResultsFile` points to a temporary `.xml` path; a file is created at the specified path
and it contains a `<testsuites` XML element. This scenario is tested by
`Validation_Run_WithXmlResultsFile_WritesXmlFile`.

**Validation_Run_WithUnsupportedResultsFormat_DoesNotWriteFile**: `Validation.Run` is called
with a context whose `ResultsFile` has a `.json` extension (unsupported); no file is created,
no exception is thrown, and an error message indicating the unsupported format is written to
the context. This scenario is tested by
`Validation_Run_WithUnsupportedResultsFormat_DoesNotWriteFile`.
