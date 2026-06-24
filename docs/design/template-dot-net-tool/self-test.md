## SelfTest

### Overview

The `SelfTest` subsystem provides the self-validation framework for the Template DotNet Tool.
It is invoked when the user passes `--validate` on the command line. The subsystem runs a
built-in suite of tests that exercise the tool's own capabilities, prints a pass/fail summary,
and optionally writes the results to a TRX or JUnit XML file for CI/CD integration. The
`SelfTest` subsystem contains one unit: `Validation`.

### Interfaces

**Validation.Run**: Runs all self-validation tests, prints a summary, and optionally writes a
results file.

- *Type*: In-process .NET static method.
- *Role*: Provider.
- *Contract*: Accepts a `Context` argument. Prints a Markdown-formatted heading (depth
  controlled by `context.HeadingDepth`) and a table of environment metadata, executes each test
  runner (`RunVersionTest`, `RunHelpTest`), prints aggregate totals (`Total Tests:`, `Passed:`,
  `Failed:`), and writes a results file if `context.ResultsFile` is set. Calls
  `context.WriteError` for each failed test and for unsupported results file extensions,
  causing `context.ExitCode` to return 1.
- *Constraints*: Throws `ArgumentNullException` if `context` is null. Each test runner wraps
  its execution in a broad `catch (Exception)` handler so that one test failure does not prevent
  remaining tests from running.

### Design

The `SelfTest` subsystem contains only the `Validation` unit. When `Program.Run` detects the
`--validate` flag, it calls `Validation.Run(context)`. The flow within `Validation.Run` is:

1. `PrintValidationHeader` writes a Markdown heading and a table containing tool version,
   machine name, OS description, .NET runtime description, and timestamp.
2. A `DemaConsulting.TestResults.TestResults` object is constructed to accumulate results.
3. Each test runner (`RunVersionTest`, `RunHelpTest`) creates a `TemporaryDirectory`, constructs
   a log file path via `PathHelpers.SafePathCombine`, invokes `Program.Run` with controlled
   arguments (capturing output to the log file via `--log`), reads the log, and asserts the
   expected content is present. Pass or fail is recorded; any exception is caught and recorded
   via `HandleTestException` so execution continues with the next test.
4. Totals are printed; `WriteError` is used for the failed count if any tests failed.
5. If `context.ResultsFile` is set, `WriteResultsFile` serializes the results. An unsupported
   file extension causes `WriteError` to be called and no file is written.

The `TemporaryDirectory` nested class manages temporary directory creation and deletion. Its
constructor wraps `IOException`, `UnauthorizedAccessException`, and `ArgumentException` in
`InvalidOperationException`. Its `Dispose` method attempts best-effort deletion; `IOException`
and `UnauthorizedAccessException` during cleanup are silently ignored.
