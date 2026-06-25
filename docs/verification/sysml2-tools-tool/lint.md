## Lint

### Verification Approach

The `Lint` subsystem is verified through integration tests that exercise `LintCommand.Run` with
controlled `Context` instances. Tests supply synthetic glob patterns pointing to real or
temporary SysML files and assert on captured diagnostic output and exit codes. The
`WorkspaceParser` is exercised with its real implementation; no mocking is applied.

### Test Environment

N/A — standard test environment. Any tests that require `.sysml` input files create them in a
temporary directory and clean them up after each test.

### Acceptance Criteria

- `LintCommand.Run` with no resolved input files writes an error message and sets exit code 1.
- `LintCommand.Run` with valid SysML input writes a `"lint: no errors found."` message and
  returns exit code 0.
- `LintCommand.Run` with invalid SysML input writes at least one diagnostic in the
  `path(line,col): severity: message` format and sets exit code 1.
- Error-severity diagnostics are written via `context.WriteError`; informational diagnostics
  are written via `context.WriteLine`.

### Test Scenarios

N/A — integration tests for the Lint subsystem are deferred pending end-to-end CLI test
infrastructure. System-level acceptance evidence for the `lint` subcommand will be captured in
integration tests alongside other subcommands. Parser-level behavior is verified by the
`WorkspaceParser` unit tests documented in the *Parser Verification Design*.
