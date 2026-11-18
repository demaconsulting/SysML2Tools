### DiagnosticFormatter

#### Verification Approach

`DiagnosticFormatter` is verified with unit tests defined in `DiagnosticFormatterTests.cs`.
Because `DiagnosticFormatter` is a pure string formatter over the Language library's
`SysmlDiagnostic` record, no mocking or test doubles are required. Tests construct diagnostics
directly, call `DiagnosticFormatter.Format`, and assert on the returned text or the thrown
exception. The command-level behavior (that `lint`, `render`, and `query` print diagnostics
through it) is verified by the Lint, Render, and Query subsystem tests.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A located diagnostic is rendered exactly as `path(line,col): severity: message`.
- Each severity is rendered as its lowercase name.
- A diagnostic with no file path is rendered as `severity: message`, without a location.
- The result never contains the compiler-generated record dump (`SysmlDiagnostic { ... }`).
- A null diagnostic causes `ArgumentNullException`.

#### Test Scenarios

**DiagnosticFormatter_Format_LocatedWarning_UsesPathLineColumnSeverityMessage**: A warning with
file `model.sysml`, line 3, column 18, and message `Unresolved reference: 'DoesNotExist'` is
formatted; the result equals
`model.sysml(3,18): warning: Unresolved reference: 'DoesNotExist'`. This scenario is tested by
`DiagnosticFormatter_Format_LocatedWarning_UsesPathLineColumnSeverityMessage`.

**DiagnosticFormatter_Format_Severity_IsLowercase**: Diagnostics of severity Info, Warning, and
Error are formatted; the severity appears as `info`, `warning`, and `error` respectively. This
scenario is tested by `DiagnosticFormatter_Format_Severity_IsLowercase`.

**DiagnosticFormatter_Format_NoFilePath_OmitsLocation**: A diagnostic with an empty file path is
formatted; the result is `warning: something` with no `path(line,col):` prefix. This scenario
is tested by `DiagnosticFormatter_Format_NoFilePath_OmitsLocation`.

**DiagnosticFormatter_Format_Diagnostic_IsNotRecordDump**: A located error is formatted; the
result contains neither `SysmlDiagnostic` nor `FilePath =`, proving the raw record dump (the
defect fixed by sharing this formatter) cannot recur. This scenario is tested by
`DiagnosticFormatter_Format_Diagnostic_IsNotRecordDump`.

**DiagnosticFormatter_Format_NullDiagnostic_ThrowsArgumentNullException**: `null` is passed as
the diagnostic; an `ArgumentNullException` is thrown. This scenario is tested by
`DiagnosticFormatter_Format_NullDiagnostic_ThrowsArgumentNullException`.
