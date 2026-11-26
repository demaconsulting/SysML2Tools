## Cli

### Verification Approach

The `Cli` subsystem is verified by integration tests defined in `CliSubsystemTests.cs`. Each
test exercises `Context.Create` and `Program.Run` together, treating the pair as the observable
subsystem interface. Tests pass controlled argument arrays and assert on captured console output,
file-system side-effects, and exit codes. `Validation` (part of the `SelfTest` subsystem)
executes its real logic in scenarios that exercise the `--validate` path; no mocking is applied
at any level. The help command is exercised through `HelpCommand.TryRun` with the real help cards
embedded in the tool assembly.

### Test Environment

N/A - standard test environment.

### Acceptance Criteria

- All integration tests pass with zero failures.
- All supported flags produce the correct observable behavior when passed through `Context.Create`
  and `Program.Run`.
- Invalid arguments produce exit code 1 and an error message.
- Log files and results files are created at the specified paths when the corresponding flags
  are used.

### Test Scenarios

**CliSubsystem_VersionFlow_ContextAndProgram_DisplaysVersionAndExits**: Arguments `["--version"]`
are passed through `Context.Create` and `Program.Run`; standard output contains the version
string and exit code is 0. This scenario is tested by
`CliSubsystem_VersionFlow_ContextAndProgram_DisplaysVersionAndExits`.

**CliSubsystem_VersionFlow_ContextAndProgram_DisplaysVersionAndExits_WithShortVFlag**: Arguments
`["-v"]` are passed through `Context.Create` and `Program.Run`; standard output contains the
version string and exit code is 0. This scenario is tested by
`CliSubsystem_VersionFlow_ContextAndProgram_DisplaysVersionAndExits_WithShortVFlag`.

**CliSubsystem_HelpFlow_ContextAndProgram_DisplaysHelpAndExits**: Arguments `["--help"]` are
passed through `Context.Create` and `Program.Run`; standard output contains help text and exit
code is 0. This scenario is tested by
`CliSubsystem_HelpFlow_ContextAndProgram_DisplaysHelpAndExits`.

**CliSubsystem_HelpFlow_ContextAndProgram_DisplaysHelpAndExits_WithShortQuestionFlag**: Arguments
`["-?"]` are passed; standard output contains help text and exit code is 0. This scenario is
tested by
`CliSubsystem_HelpFlow_ContextAndProgram_DisplaysHelpAndExits_WithShortQuestionFlag`.

**CliSubsystem_HelpFlow_ContextAndProgram_DisplaysHelpAndExits_WithShortHFlag**: Arguments
`["-h"]` are passed; standard output contains help text and exit code is 0. This scenario is
tested by `CliSubsystem_HelpFlow_ContextAndProgram_DisplaysHelpAndExits_WithShortHFlag`.

**CliSubsystem_ValidateFlow_ContextAndProgram_RunsValidationAndExits**: Arguments `["--validate"]`
are passed; standard output contains `"Total Tests:"` and exit code is 0. This scenario is
tested by `CliSubsystem_ValidateFlow_ContextAndProgram_RunsValidationAndExits`.

**CliSubsystem_SilentFlow_ContextAndProgram_SuppressesOutput**: Arguments `["--version", "--silent"]`
are passed; standard output is empty and exit code is 0, confirming `--silent` suppresses all
console output. This scenario is tested by
`CliSubsystem_SilentFlow_ContextAndProgram_SuppressesOutput`.

**CliSubsystem_ResultsFlow_ContextAndProgram_WritesResultsFile**: Arguments
`["--validate", "--results", "<tmp>.trx"]` are passed; a results file is created at the
specified path and exit code is 0. This scenario is tested by
`CliSubsystem_ResultsFlow_ContextAndProgram_WritesResultsFile`.

**CliSubsystem_LogFlow_ContextAndProgram_WritesLogFile**: Arguments `["--log", "<tmp>.log"]` are
passed; a log file is created at the specified path and exit code is 0. This scenario is tested
by `CliSubsystem_LogFlow_ContextAndProgram_WritesLogFile`.

**CliSubsystem_InvalidArgs_ContextAndProgram_RejectsUnknownArgumentsAndExitsNonZero**: Arguments
`["--unknown-flag"]` are passed directly to `Program.Main`; exit code is 1 and standard error
contains an error message including the unknown flag. This scenario is tested by
`CliSubsystem_InvalidArgs_ContextAndProgram_RejectsUnknownArgumentsAndExitsNonZero`.

**CliSubsystem_ErrorOutput_ContextAndProgram_WritesErrorToStderr**: A `Context` is created with
no arguments and `WriteError` is called with a known message; standard error receives the
message and exit code is non-zero. This scenario is tested by
`CliSubsystem_ErrorOutput_ContextAndProgram_WritesErrorToStderr`.

**CliSubsystem_ResultAliasFlow_ContextAndProgram_WritesResultsFile**: Arguments
`["--validate", "--result", "<tmp>.trx"]` (legacy alias) are passed; a results file is created
at the specified path and exit code is 0. This scenario is tested by
`CliSubsystem_ResultAliasFlow_ContextAndProgram_WritesResultsFile`.

**CliSubsystem_DepthFlow_ContextAndProgram_AdjustsHeadingDepth**: Arguments `["--depth", "2"]`
are passed; the heading depth is set to 2 and exit code is 0, confirming the `--depth` flag is
correctly parsed and propagated through `Context`. This scenario is tested by
`CliSubsystem_DepthFlow_ContextAndProgram_AdjustsHeadingDepth`.

**CliSubsystem_HelpCommand_TopicRequested_PrintsTopic**: Arguments `["help", "command-line"]` are
offered to `HelpCommand.TryRun`, which answers with the command-line topic and exit code 0. This
scenario is tested by `CliSubsystem_HelpCommand_TopicRequested_PrintsTopic`.

**CliSubsystem_StrictUnresolved_IsDocumentedInEveryCommandHelp**: The top-level `--help` and the
`--help` usage of `lint`, `render`, `export`, `query`, and every query verb are printed through
`Context.Create` and `Program.RunAsync`; each lists `--strict-unresolved`, none mentions the removed
`--warn-unresolved`, and each exits with code 0. This scenario is tested by
`CliSubsystem_StrictUnresolved_IsDocumentedInEveryCommandHelp`.

**CliSubsystem_LintHelp_DescribesWarningsAndStrictUnresolved**: `lint --help` prints the usage line,
states that syntax errors give a non-zero exit code while unresolved references and tool-specific
render selectors are warnings, and describes `--strict-unresolved`. This scenario is tested by
`CliSubsystem_LintHelp_DescribesWarningsAndStrictUnresolved`.

**CliSubsystem_StrictUnresolved_WordingIsAccuratePerCommand**: The `--help` usage of `lint`,
`render`, `query`, and every query verb states exit code 1 for models with unresolved references,
the `export --help` usage states Severity 2 in the JSON diagnostics with the exit code unaffected,
and the top-level usage lists the flag for all four commands. This scenario is tested by
`CliSubsystem_StrictUnresolved_WordingIsAccuratePerCommand`.
