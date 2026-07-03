### DemaConsulting.SysML2Tools.Tool — Help Subsystem Verification

#### Verification Approach

The Help subsystem is verified using unit/integration tests in two files under
`test/DemaConsulting.SysML2Tools.Tool.Tests/Help/` (`HelpArgumentParserTests.cs`,
`HelpSubsystemTests.cs`), plus help-specific parsing tests in
`test/DemaConsulting.SysML2Tools.Tool.Tests/Cli/ContextTests.cs`, and regression tests in
`test/DemaConsulting.SysML2Tools.Tool.Tests/Lint/LintSubsystemTests.cs`,
`test/DemaConsulting.SysML2Tools.Tool.Tests/Render/RenderSubsystemTests.cs`, and
`test/DemaConsulting.SysML2Tools.Tool.Tests/ProgramTests.cs`. Tests invoke
`HelpArgumentParser.Parse` directly (parser-only tests), and `Context.Create` +
`Program.RunAsync` (end-to-end tests), asserting on captured console output, exit code, and
byte-for-byte parity between `help <command>[<verb>]` and `<command> [<verb>] --help`. Tests
run against all three target frameworks.

#### Test Environment

- Framework: xUnit v3
- Target frameworks: net8.0, net9.0, net10.0
- Test project: `DemaConsulting.SysML2Tools.Tool.Tests`
- Dependencies: `DemaConsulting.SysML2Tools.Tool` (internal access via `InternalsVisibleTo`)

#### Acceptance Criteria

- Bare `help` produces identical output to bare `--help`/`-h`/`-?`, and does not fall through
  to "No command specified".
- `help lint`/`help render` produce output identical to `lint --help`/`render --help`
  respectively, each containing that command's distinguishing usage text.
- `help query` (no verb) produces output identical to `query --help`, listing all 11 verbs.
- `help query <verb>` produces output identical to `query <verb> --help`, for every one of
  the 11 verbs, each mentioning that verb's real flags (e.g., `--depth` for `impact`,
  `--direction` for `hierarchy`, `--kind`/`--name` for `list`/`find`, `--element` for every
  other verb).
- `help <unknown-command>` and `help query <unknown-verb>` throw a graceful
  `ArgumentException` naming the bad token — not a crash — consistent with the existing
  `ArgumentException`/`InvalidOperationException` handling in `Program.Main`.
- `--silent help query` (and `help lint --silent`) suppress all console output, consistent
  with `Context.WriteLine`'s existing unconditional `Silent` suppression (the same behavior
  already applies to, e.g., `--silent --version`).
- Existing `-h`/`-?`/`--help` regression tests (`ProgramTests.cs`) continue to pass
  unmodified, confirming no regression in the pre-existing global-flag behavior.
- `HelpArgumentParser.Parse` in isolation: no arguments → both fields null; `lint`/`render`/
  `query` (no verb) → `TargetCommand` set; `query` + each of the 11 verbs → `TargetVerb` set;
  unknown target/verb/extra token → `ArgumentException`.

#### Test Scenarios

##### HelpArgumentParserTests.cs

**`HelpArgumentParser_Parse_NoArguments_ReturnsBothFieldsNull`**: Bare `help` (no args)
leaves `TargetCommand`/`TargetVerb` both null.

**`HelpArgumentParser_Parse_Lint_SetsTargetCommandLint`** /
**`HelpArgumentParser_Parse_Render_SetsTargetCommandRender`** /
**`HelpArgumentParser_Parse_QueryNoVerb_SetsTargetCommandQueryOnly`**: Each of the three
recognized target commands sets `TargetCommand` accordingly with `TargetVerb` null.

**`HelpArgumentParser_Parse_QueryWithVerb_SetsTargetVerb`** (theory, 11 cases): For each of
the 11 recognized query verb tokens, `help query <verb>` sets both `TargetCommand` (`"query"`)
and `TargetVerb` (the verb token).

**`HelpArgumentParser_Parse_UnknownTargetCommand_ThrowsArgumentException`**: An unrecognized
target command throws `ArgumentException` naming the bad token and listing all three valid
targets.

**`HelpArgumentParser_Parse_QueryWithUnknownVerb_ThrowsArgumentException`**: An unrecognized
verb under `query` throws `ArgumentException` naming the bad token (reusing
`QueryVerbParsing.Parse`'s error message).

**`HelpArgumentParser_Parse_FlagAsTargetCommand_ThrowsArgumentException`**: A `-`-prefixed
token as the target command throws `ArgumentException`.

**`HelpArgumentParser_Parse_ExtraArgumentAfterCommand_ThrowsArgumentException`** /
**`HelpArgumentParser_Parse_ExtraArgumentAfterQueryVerb_ThrowsArgumentException`**: An extra
trailing token (after the command, or after the query verb) throws `ArgumentException` naming
the extra token and the `help` command.

##### HelpSubsystemTests.cs

**`HelpSubsystem_BareHelp_MatchesTopLevelHelpFlag`**: `help` (no args) produces output and
exit code identical to `--help`.

**`HelpSubsystem_BareHelp_DoesNotFallThroughToNoCommandSpecified`**: `help` (no args) prints
`"Usage:"` and never `"No command specified"`, confirming the `Command == Help` dispatch
branch returns before `RunToolLogicAsync`'s default arm could run.

**`HelpSubsystem_HelpLint_MatchesLintHelpFlag`** / **`HelpSubsystem_HelpRender_MatchesRenderHelpFlag`**:
`help lint`/`help render` produce output identical to `lint --help`/`render --help`, each
containing that command's distinguishing usage substring.

**`HelpSubsystem_HelpQuery_MatchesQueryHelpFlag`**: `help query` (no verb) produces output
identical to `query --help`, containing all 11 verb tokens.

**`HelpSubsystem_HelpQueryVerb_MatchesQueryVerbHelpFlag`** (theory, 11 cases): For each verb,
`help query <verb>` produces output identical to `query <verb> --help`, containing the verb's
usage line and its real flag(s) (`--depth` for `impact`; `--direction` for `hierarchy`;
`--kind`/`--name` for `list`/`find`; `--element` for every other verb).

**`HelpSubsystem_HelpUnknownCommand_ThrowsArgumentException`** /
**`HelpSubsystem_HelpQueryUnknownVerb_ThrowsArgumentException`**: `Context.Create(["help",
"bogus-command"])` / `Context.Create(["help", "query", "bogus-verb"])` each throw
`ArgumentException` naming the bad token.

**`HelpSubsystem_HelpUnknownCommand_ViaMain_ReturnsNonZeroExitCodeWithoutCrashing`**:
`Program.Main(["help", "bogus-command"])` returns exit code 1 and writes the error message to
stderr — via `Main`'s existing `ArgumentException` handler — without an unhandled crash.

**`HelpSubsystem_SilentHelpQuery_SuppressesOutputConsistentlyWithOtherCommands`** /
**`HelpSubsystem_HelpLintSilent_SuppressesOutput`**: `--silent help query` and `help lint
--silent` both produce empty captured stdout and exit code 0, documenting that `--silent`'s
existing unconditional suppression in `Context.WriteLine` applies equally to `help`'s output
(consistent with, e.g., `--silent --version`), with no special-case bypass for `help`.

##### Regression tests (existing files, updated)

**`LintSubsystem_Help_PrintsLintSpecificUsage`** (`LintSubsystemTests.cs`) /
**`RenderSubsystem_Help_PrintsRenderSpecificUsage`** (`RenderSubsystemTests.cs`): `lint
--help`/`render --help` now print command-specific usage (not the generic top-level
`"Commands:"` list), confirming the command-aware `--help` dispatch introduced alongside the
`help` command.

**`Program_Run_WithHelpFlag_DisplaysUsageInformation`**,
**`Program_Run_WithShortHelpFlag_DisplaysUsage`**,
**`Program_Run_WithQuestionMarkFlag_DisplaysUsage`** (`ProgramTests.cs`, pre-existing,
unmodified): confirm bare `--help`/`-h`/`-?` still print the generic top-level usage/options
text, unaffected by the command-aware dispatch restructuring.

##### Context_Create_HelpCommand_* (ContextTests.cs)

Verifies `Context.Create`'s dispatch to `HelpArgumentParser` for `Command ==
SysmlCommand.Help`: bare `help` populates an empty `HelpOptions`; `help lint`/`help render`
set `TargetCommand`; `help query uses` sets both `TargetCommand` and `TargetVerb`; unknown
command/verb tokens throw `ArgumentException`. See
`docs/verification/sysml2-tools-tool/cli/context.md` for the full scenario list.
