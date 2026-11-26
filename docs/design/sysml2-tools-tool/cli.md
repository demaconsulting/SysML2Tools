## Cli

![Tool Cli Structure](ToolCliView.svg)

### Overview

The `Cli` subsystem is the boundary between the host environment's command-line invocation and
the tool's internal logic. It owns argument parsing, output channel management, and exit-code
tracking. All other subsystems receive a `Context` object from this subsystem to read parsed
flags and write output. The `Cli` subsystem contains two units: `Context` and `HelpCommand`,
which answers the `help` command from the built-in help cards.

### Interfaces

**Context.Create**: Factory method that constructs a `Context` from a command-line argument array.

- *Type*: In-process .NET static method.
- *Role*: Provider.
- *Contract*: Parses `string[] args` into flag properties and opens the log file if `--log` is
  present. Returns a fully initialized `Context`. Accepts `--result` as a legacy alias for
  `--results`.
- *Constraints*: Throws `ArgumentException` for unknown or malformed arguments; throws
  `InvalidOperationException` when the log file cannot be opened.

**Context.WriteLine**: Writes a message to stdout and to the log file.

- *Type*: In-process .NET instance method.
- *Role*: Provider.
- *Contract*: Writes `message` to `Console.Out` and to the log file if one is open. Stdout
  output is suppressed when `Silent` is true; the log file always receives the message.
- *Constraints*: None.

**Context.WriteError**: Writes an error message and sets the error exit code.

- *Type*: In-process .NET instance method.
- *Role*: Provider.
- *Contract*: Sets `_hasErrors` to true, writes `message` in red to `Console.Error`, and writes
  to the log file if one is open. Stderr output is suppressed when `Silent` is true, but
  `ExitCode` is set to 1 regardless.
- *Constraints*: Once set, `ExitCode` cannot return to 0 within the same invocation.

**Context.ExitCode**: Derived property returning 0 or 1.

- *Type*: In-process .NET property.
- *Role*: Provider.
- *Contract*: Returns 1 if `WriteError` has been called at least once; returns 0 otherwise.
- *Constraints*: Read-only.

**HelpCommand.TryRun**: Answers the `help` command from the help cards embedded in the assembly.

- *Type*: In-process .NET instance method.
- *Role*: Provider.
- *Contract*: Returns `true` and the exit code of the help command when the first argument is
  `help`; returns `false` without any work otherwise. `help <command>` and
  `help query <verb>` print the card of that command or verb.
- *Constraints*: Called before `Context.Create`, so `--silent` and `--log` do not apply.

**HelpCommand.Footer**: The one-line pointer to the help command that ends the usage text.

- *Type*: In-process .NET property.
- *Role*: Provider.
- *Contract*: Returns the sentence without building the card catalog.
- *Constraints*: None.

**Context.Dispose**: Releases the log file `StreamWriter`.

- *Type*: In-process .NET method (`IDisposable`).
- *Role*: Provider.
- *Contract*: Disposes `_logWriter` and sets it to null; flushes any buffered content. Callers
  must use a `using` statement to guarantee disposal.
- *Constraints*: Safe to call multiple times (idempotent after first call).

### Design

The `Cli` subsystem contains the `Context` and `HelpCommand` units; there is no subsystem-level
code of its own. The `Program` unit first offers the arguments to `HelpCommand`, then creates a
`Context` at the start of each other invocation and passes it to all other units that produce
output.

`Context` has no dependencies on other tool subsystems; it uses only .NET BCL types
(`Console`, `StreamWriter`). `HelpCommand` is the only unit that uses the HelpCards OTS
library, and it reads the command names from `SysmlCommand` so that `help <command>` stays in
step with the commands that `Context` accepts.

Error handling flows from `Context.Create` to `Program.Main`: argument parsing errors propagate
as `ArgumentException`; log-file errors propagate as `InvalidOperationException`. Both are
caught and handled in `Program.Main`, which writes the error to stderr and returns exit code 1.

When `--log` is active, `Context` holds an open `StreamWriter` for the duration of the
invocation. The `Program.Main` call site wraps the `Context` in a `using` statement to ensure
the file handle is released and buffered content is flushed on exit.
