## Lint

### Verification Approach

The `Lint` subsystem is verified through integration tests that exercise `LintCommand.Run` with
controlled `Context` instances. Tests supply synthetic glob patterns pointing to real or
temporary SysML files and assert on captured diagnostic output and exit codes. Pattern
resolution is delegated to the shared `GlobFileCollector` (see
`docs/verification/sysml2-tools-core/io.md` for the underlying glob-semantics verification);
these tests confirm only the CLI-level contract — that `lint` accepts glob patterns (including
recursive `**` and `!` exclusions) and resolves them before parsing. The `WorkspaceLoader` is
exercised with its real implementation; no mocking is applied.

### Test Environment

N/A — standard test environment. Any tests that require `.sysml` input files create them in a
temporary directory and clean them up after each test.

### Acceptance Criteria

- `LintCommand.RunAsync` with no file patterns supplied at all writes a "no input files
  specified" error message and sets exit code 1.
- `LintCommand.RunAsync` with one or more file patterns that resolve to zero files writes a
  distinct "no files matched" error message and sets exit code 1.
- `LintCommand.RunAsync` with valid SysML input writes a `"lint: no errors found."` message and
  returns exit code 0.
- `LintCommand.RunAsync` with invalid SysML input writes at least one diagnostic in the
  `path(line,col): severity: message` format and sets exit code 1.
- Error-severity diagnostics are written via `context.WriteError`; informational diagnostics
  are written via `context.WriteLine`.
- `lint --help` prints lint-specific usage (not the generic top-level command list), and is
  identical to `help lint`'s output (see the Help subsystem verification document).

### Test Scenarios

#### LintSubsystem_PatternMatchesNoFiles_ReportsError (LintSubsystemTests.cs)

Verifies that invoking the lint command with a file pattern that matches no file on disk
results in an error message written to the context containing "no files matched", and a
failing exit code — distinguishing this case from "no patterns supplied at all". Satisfies
`SysML2Tools-Tool-Lint-NoFilesMatched`.

#### LintSubsystem_Help_PrintsLintSpecificUsage (LintSubsystemTests.cs)

Verifies that `lint --help` prints the lint-specific usage line and does not print the
generic top-level `"Commands:"` section — a regression-proofing test added alongside the
`help` command's command-aware `--help` dispatch (see `docs/design/sysml2-tools-tool/help.md`).

#### LintSubsystem_Patterns_RecursiveGlob_ResolvesNestedFiles (LintSubsystemTests.cs)

Verifies that a recursive `**/*.sysml` pattern resolves `.sysml` files nested in subdirectories
— a capability the prior hand-rolled, single-directory-only resolver lacked — confirming
`lint`'s delegation to the shared `GlobFileCollector`.

#### LintSubsystem_Patterns_ExclusionPattern_ExcludesMatchedFile (LintSubsystemTests.cs)

Verifies that an inclusion pattern followed by a `!`-prefixed exclusion pattern resolves every
included file except the excluded one, confirming `lint` supports exclusion patterns via the
shared `GlobFileCollector`.

#### ResxResource_EveryKey_ResolvesToNonEmptyText / ResxResource_KeysAndAccessorProperties_AreInBidirectionalParity (ResxResourceTests.cs)

For the `LintStrings` resource base name/accessor pair (one of four covered by these theory
tests), every key discovered in `Lint/LintStrings.resx`'s invariant-culture resource set
resolves to non-null/non-empty text via `ResourceManager`, and every such key has a matching
`public static string` property on `LintStrings` (and vice versa). Satisfies
`SysML2Tools-Tool-Lint-LocalizableHelpText`.
