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
- Error-severity diagnostics are written via `context.WriteError`; informational and
  warning-severity diagnostics are written via `context.WriteLine`.
- An unresolved reference in a user file is a Warning-severity diagnostic by default: it is
  written via `context.WriteLine` and `lint` exits 0 when nothing else is wrong; with
  `--strict-unresolved` the same diagnostic is an Error, written via `context.WriteError`, and
  `lint` exits 1. Strict mode promotes only unresolved references: other warnings such as a
  circular import stay warnings, and a reference into the standard library produces no
  diagnostic.
- A SysML2Tools extension `render` selector the model does not declare (`asGridDiagram`,
  `asGeneralDiagram`, …) produces one portability Warning at the token, never an Error and
  independent of `--strict-unresolved`; the standard selectors (`Views::asTreeDiagram`,
  `Views::asInterconnectionDiagram`) produce no diagnostic.
- Every model in `docs/gallery/models` lints with no diagnostics and exit code 0, with and
  without `--strict-unresolved`.
- `lint --help` prints lint-specific usage (`lint [--strict-unresolved] <files...>`) that
  describes the warning default and the `--strict-unresolved` option, and is
  identical to `help lint`'s output (see the Help subsystem verification document).

### Test Scenarios

#### LintSubsystem_PatternMatchesNoFiles_ReportsError (LintSubsystemTests.cs)

Verifies that invoking the lint command with a file pattern that matches no file on disk
results in an error message written to the context containing "no files matched", and a
failing exit code — distinguishing this case from "no patterns supplied at all". Satisfies
`SysML2Tools-Tool-Lint-NoFilesMatched`.

#### LintSubsystem_Report_UnresolvedReference_ReportsReferencePosition (LintSubsystemTests.cs)

Verifies that linting a model with an unresolved supertype reference (`part def X :>
DoesNotExist;`, written at line 2, column 19) writes `<file>(2,19): warning: Unresolved
reference: 'DoesNotExist'` — located at the reference text rather than at `(0,0)`. Satisfies
`SysML2Tools-Tool-Lint-Report` and `SysML2Tools-Tool-Lint-StrictUnresolved`.

#### LintSubsystem_Report_UnresolvedExpressionReferences_ReportsTokenPositionsAsWarnings (LintSubsystemTests.cs)

Verifies that an undeclared name in a transition guard, a trigger payload, and a `render`
target is written as a `warning:` at the token's position, while a declared name, an operator,
and a library-function call are not reported, and the exit code is 0. Satisfies
`SysML2Tools-Tool-Lint-Report`.

#### LintSubsystem_UnresolvedReference_ReportsWarningAndExitsZero (LintSubsystemTests.cs)

Verifies that linting a model with an unresolved supertype reference writes the `warning:`
line to the output, writes nothing containing "error" to the error stream, and sets exit code
0. Satisfies `SysML2Tools-Tool-Lint-ExitCode` and `SysML2Tools-Tool-Lint-StrictUnresolved`.

#### LintSubsystem_StrictUnresolved_UnresolvedReference_ReportsErrorAndExitsOne (LintSubsystemTests.cs)

Verifies that `lint --strict-unresolved` writes the same diagnostic as `error:` at the same
position to the error stream, writes `lint: 1 error(s) found.`, writes no warning to the
output, and exits 1. Satisfies `SysML2Tools-Tool-Lint-StrictUnresolved`.

#### LintSubsystem_StrictUnresolved_CircularImport_RemainsWarning (LintSubsystemTests.cs)

Verifies that under `--strict-unresolved` a circular import is still reported as a `warning:`
and the exit code stays 0, so only unresolved references are promoted. Satisfies
`SysML2Tools-Tool-Lint-StrictUnresolved`.

#### LintSubsystem_StrictUnresolved_StandardLibraryReference_NoDiagnostics (LintSubsystemTests.cs)

Verifies that a reference into the bundled standard library (`ScalarValues::Real`) produces no
diagnostic under `--strict-unresolved`, reports "no errors found", and exits 0. Satisfies
`SysML2Tools-Tool-Lint-StrictUnresolved`.

#### LintSubsystem_Report_ToolRenderSelector_WritesPortabilityWarningAtToken (LintSubsystemTests.cs)

Verifies that `render asGridDiagram;` without a declaration writes exactly one `warning:` at
the token (line 2, column 21) stating that the selector is a SysML2Tools extension and naming
`StandardViewDefinitions::GridView`, and the exit code is 0. Satisfies
`SysML2Tools-Tool-Lint-PortabilityWarning`.

#### LintSubsystem_StrictUnresolved_ToolRenderSelector_RemainsWarning (LintSubsystemTests.cs)

Verifies that the same portability warning is still a `warning:` under `--strict-unresolved`,
nothing is written to the error stream, and the exit code is 0. Satisfies
`SysML2Tools-Tool-Lint-PortabilityWarning`.

#### LintSubsystem_Report_StandardRenderSelectors_NoDiagnostics (LintSubsystemTests.cs)

Verifies that `Views::asTreeDiagram` and `Views::asInterconnectionDiagram` render targets
produce no diagnostic and `lint` reports "no errors found". Satisfies
`SysML2Tools-Tool-Lint-PortabilityWarning`.

#### LintSubsystem_Gallery_AllModels_NoDiagnosticsExitZero (LintSubsystemTests.cs)

Verifies that every `.sysml` model in `docs/gallery/models` lints with no diagnostic line on
either stream and exit code 0. Satisfies `SysML2Tools-Tool-Lint-ExitCode`.

#### LintSubsystem_Gallery_StrictUnresolved_AllModels_NoDiagnosticsExitZero (LintSubsystemTests.cs)

Verifies that every `.sysml` model in `docs/gallery/models` also lints with no diagnostic line
on either stream and exit code 0 under `--strict-unresolved`. Satisfies
`SysML2Tools-Tool-Lint-StrictUnresolved`.

#### LintSubsystem_Help_PrintsLintSpecificUsage (LintSubsystemTests.cs)

Verifies that `lint --help` prints the lint-specific usage line
(`Usage: sysml2tools lint [--strict-unresolved] <files...>`), lists `--strict-unresolved`, and
does not print the generic top-level `"Commands:"` section — a regression-proofing test added
alongside the `help` command's command-aware `--help` dispatch (see
`docs/design/sysml2-tools-tool/help.md`). Satisfies
`SysML2Tools-Tool-Lint-StrictUnresolved`.

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
