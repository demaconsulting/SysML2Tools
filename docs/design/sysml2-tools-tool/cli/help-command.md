### HelpCommand

![Tool Cli Structure](ToolCliView.svg)

#### Purpose

`HelpCommand` answers the `sysml2tools help` command from the help cards that are embedded in the
tool assembly. It is the only unit that references the HelpCards library.

#### Data Model

**ToolName**: `string` constant — `sysml2tools`, the name of the tool as the user types it. The
usage lines, the index preamble, and the `{{tool}}` token of the cards are derived from it.

**RootCardIds**: `string[]` — the ids of the root cards, which are the topics that
`sysml2tools help` lists. It is the one list of topics; a missing or extra root card fails the
catalog.

**CommandNames**: `string[]` — the names of the commands as the user types them: every
`SysmlCommand` member except `None`, in lower case (`lint`, `render`, `query`, `export`). Deriving
them from the enumeration keeps the routing of `help <command>` in step with the commands that the
parser accepts.

**CommandsTopic**: `string` constant (private) — `commands`, the topic whose child cards describe
the commands.

**PurposeTags**: `string[]` (private) — `tutorial`, `reference`, and `troubleshooting`; the closed
tag vocabulary. Every card carries one of these tags and no other tag.

**ValueOptions**: `string[]` (private) — the HelpCards options that take a value (`--format`,
`--example`, `--search`, `--tag`, `--source`, `--width`), so that their value is not taken for a
topic name.

**Cards**: `HelpSource` (private, static) — the cards embedded in the tool assembly. Creating the
source reads nothing.

**Default**: `HelpCommand` (static) — the instance used by the tool; it writes to the console.

**_cli**: `HelpCli` — the command-line helper of HelpCards, created from `CreateOptions`. It is
immutable and keeps no state between calls.

**Help cards**: the Markdown files under `src/DemaConsulting.SysML2Tools.Tool/Help/`, embedded by
the HelpCards build targets: one root card per topic, the command cards under `commands/`, and the
query verb cards under `commands/query/`. The rules for writing them are in the *Help Cards*
section of `CONTRIBUTING.md`.

#### Key Methods

**TryRun**: Runs the help command when the arguments start with the `help` verb.

- *Parameters*: `string[] args` — the command-line arguments of the tool; `out int exitCode` —
  receives the exit code when the command was answered.
- *Returns*: `bool` — `true` when the command was answered; `false` when the tool should continue
  with its own option parser.
- *Preconditions*: None.
- *Postconditions*: When `false` is returned, nothing was written.

When the first argument is `help` (in any case), passes the remaining arguments to `HelpCli.Run`,
which builds the catalog and answers. Otherwise delegates to `HelpCli.TryRun`, which returns
`false` without building the catalog.

Before the tool used help cards, `sysml2tools help lint` and `sysml2tools help query <verb>`
printed the help of that command or verb. The command cards are children of the `commands` topic,
and HelpCards resolves the first word of a card path only against the topics, so `TryRun` inserts
`commands` before the first path word when that word is one of `CommandNames`:
`help lint` becomes `help commands lint`, and `help query describe` becomes
`help commands query describe`. The first path word is the first argument that is neither an
option nor the value of one of `ValueOptions`; an argument after `--` is always a path word.

**Footer**: Gets the one-line pointer to the help command that ends the usage text, without
building the catalog.

**CreateOptions**: Creates the options of the command-line helper.

- *Parameters*: optional `TextWriter` output and error writers (`null` for the console).
- *Returns*: `HelpCliOptions` — the tool name, the version from `Program.Version`, the catalog
  configuration (the embedded cards, `PurposeTags`, and `RootCardIds`), the usage lines (including
  `help <command>` and `help query <verb>`), the index preamble, and the writers.
- *Preconditions*: None.
- *Postconditions*: `GetCatalogConfiguration` of the options gives the tests the composition that
  the tool ships.

**Create**: Creates a help command that writes to the given writers; used by tests.

#### Error Handling

`TryRun` does not throw for user input; the library writes errors to the error writer and sets the
exit code (see *Help Command* in the system design). The validation test reports every card defect
at build time, so a defective catalog is not shipped.

The command is answered before the options are parsed, so no `Context` exists and the output and
error providers of HelpCards are not used: routing the answers through a `Context` would mean
parsing the options a second time just for help.

#### Dependencies

- **HelpCards** (OTS) — `HelpCli`, `HelpCliOptions`, `HelpSource`, and `HelpCatalogBuilder`; see
  *HelpCards*, the OTS integration design.
- **Program** — `Program.Version` supplies the version shown in the index and the `{{version}}`
  token.
- **SysmlCommand** — the members of the enumeration give `CommandNames`.

#### Callers

- **Program** — `Program.Main` calls `HelpCommand.Default.TryRun` before `Context.Create`, and
  `Program.PrintTopLevelHelp` ends the usage text with `HelpCommand.Default.Footer`.
