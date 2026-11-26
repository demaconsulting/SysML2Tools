### HelpCommand

#### Verification Approach

`HelpCommand` is verified with unit tests defined in `HelpCommandTests.cs`. Each test creates a
help command with `HelpCommand.Create`, which writes to in-memory writers instead of the console.
No mocking is applied: the tests use the real help cards embedded in the tool assembly and the
real HelpCards library, a documented dependency of the unit.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The help verb prints the topic index.
- `help <command>`, `help commands <command>`, and `help query <verb>` print the card of every
  command and query verb that the tool accepts.
- The shipped catalog passes validation with warnings treated as errors.
- Every card prints in lines of at most 120 characters.

#### Test Scenarios

**HelpCommand_TryRun_HelpVerb_PrintsTopicIndexAndReturnsZero**: `TryRun` is called with
`["help"]`; it answers with exit code 0 and the output lists the command of every root topic.
This scenario is tested by `HelpCommand_TryRun_HelpVerb_PrintsTopicIndexAndReturnsZero`.

**HelpCommand_TryRun_EveryRegisteredCommand_PrintsCommandCard**: For every name in
`HelpCommand.CommandNames`, `TryRun` is called with `["help", <command>]` and with
`["help", "commands", <command>]`; each answers with exit code 0 and prints the card titled
`<command> command`. This scenario is tested by
`HelpCommand_TryRun_EveryRegisteredCommand_PrintsCommandCard`.

**HelpCommand_TryRun_EveryQueryVerb_PrintsVerbCard**: For every verb token that the query command
accepts, `TryRun` is called with `["help", "query", <verb>]`; each answers with exit code 0 and
prints the card whose title starts with `query <verb>`. This scenario is tested by
`HelpCommand_TryRun_EveryQueryVerb_PrintsVerbCard`.

**HelpCommand_TryRun_CommandNameAfterValueOption_PrintsCommandCard**: `TryRun` is called with
`["help", "--width", "100", "lint"]`; the value of `--width` is not taken for a topic, so the
command answers with exit code 0 and the `lint` card rather than the commands overview. This
scenario is tested by `HelpCommand_TryRun_CommandNameAfterValueOption_PrintsCommandCard`.

**HelpCommand_CreateOptions_ShippedCatalog_PassesValidation**: The catalog configuration from
`CreateOptions` is validated with warnings treated as errors; no defect is reported, which proves
that the root cards are exactly `RootCardIds`, that every card carries one purpose tag, and that
every command example parses as a command line of the tool. This scenario is tested by
`HelpCommand_CreateOptions_ShippedCatalog_PassesValidation`.

**HelpCommand_TryRun_EveryCard_FitsMaximumLineWidth**: Every card of the shipped catalog is
printed with its links and tokens expanded and a fixed, long prerelease version in place of the
build's version; each exits with code 0 and has no line longer than 120 characters. This scenario
is tested by `HelpCommand_TryRun_EveryCard_FitsMaximumLineWidth`.
