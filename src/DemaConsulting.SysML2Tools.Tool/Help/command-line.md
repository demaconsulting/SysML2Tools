---
title: Command-line options
summary: The global options, the order in which a run handles them, and the exit codes.
aliases:
  - options
  - flags
  - exit-codes
tags:
  - reference
order: 2
see-also:
  - commands
  - self-validation
  - troubleshooting
examples:
  - name: usage
    kind: command
    language: shell
    description: Print the short usage text. It ends with a pointer to this help.
    content: '{{tool}} --help'
    expect: "Run '{{tool}} help' for topics and examples."
  - name: lint-usage
    kind: command
    language: shell
    description: Print the short usage text of one command.
    content: '{{tool}} lint --help'
    expect: 'Usage: {{tool}} lint [--strict-unresolved] <files...>'
  - name: silent-log
    kind: command
    language: shell
    description: Hide the console output and keep it in a log file.
    content: '{{tool}} --silent --log output.log --version'
  - name: unknown-option
    kind: command
    language: shell
    description: An unknown argument is an error, printed even with --silent. Exits 1.
    content: '{{tool}} --silent --unknown'
    exit: 1
    expect: "Error: Unsupported argument '--unknown'"
---
A run is `{{tool}} [options] <command> [files...]`; see [[commands]]
for the commands and their own options. The global options below may
come before or after the command.

| Option | Meaning |
| --- | --- |
| `-v`, `--version` | Print only the version. |
| `-?`, `-h`, `--help` | Print the short usage text. |
| `--silent` | Hide console output. Errors still set exit code 1. |
| `--log <file>` | Also write the output to a file. |
| `--validate` | Run the self-tests; see [[self-validation]]. |
| `--results <file>` | Write the self-test results as `.trx` or `.xml`. |
| `--depth <n>` | First heading level of reports, 1 to 6; default 1. |

`--result` is another name for `--results`. `--depth` applies to the
self-test report and to the Markdown output of `query`.

**Order.** `--version`, when all arguments are valid, prints only the
version and skips everything else. Without it, the tool prints a banner,
then handles the first of `--help`, `--validate`, or the command. With
no command, it prints `No command specified.` and exits 0. The command
that [[#silent-log]] prints writes the version to the log file only.

**Short usage.** `--help` alone prints the commands and options, as the
command that [[#usage]] prints shows. After a command, it prints the
usage of that command instead ([[#lint-usage]]), and
`{{tool}} query <verb> --help` prints the usage of one query verb.

**Errors.** An unknown argument, an option without its value, or an
option that the command does not take prints `Error: ...` and exits 1
([[#unknown-option]]). These errors are found
before `--silent` and `--log` take effect, so they are always printed and
never logged. Errors, including error diagnostics, go to standard
error; everything else, including warnings, goes to standard output.

| Exit code | Meaning |
| --- | --- |
| 0 | Success, also when the model has warnings. |
| 1 | An error: an argument, a model error, no matching file, a failed test. |
| 2 | `help` with an unknown topic or option. |

A model error is a syntax error, or an unresolved reference with
`--strict-unresolved`. `export` reports model errors only in its
output and exits 0; see [[commands]].

**Help.** `help` is a command, not an option, and must be the first
argument. `--silent` and `--log` do not apply to it, it prints no
banner, and an unknown topic exits 2.
