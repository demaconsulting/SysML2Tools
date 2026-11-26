---
title: Troubleshooting
summary: Common errors and warnings, what they mean, and how to fix them.
aliases:
  - errors
  - problems
  - faq
tags:
  - troubleshooting
order: 9
see-also:
  - command-line
  - workspaces
  - commands/lint
examples:
  - name: broken
    kind: source
    language: sysml
    save-as: broken.sysml
    description: A model whose part refers to an undeclared definition.
    content: |
      part def Car {
          part engine : Engine;
      }
  - name: warning
    kind: command
    language: shell
    description: An unresolved reference is a warning by default.
    content: '{{tool}} lint broken.sysml'
    expect: |
      warning: Unresolved reference: 'Engine'
      lint: no errors found.
  - name: strict
    kind: command
    language: shell
    description: With --strict-unresolved it is an error. Exits 1.
    content: '{{tool}} lint --strict-unresolved broken.sysml'
    exit: 1
    expect: "error: Unresolved reference: 'Engine'"
  - name: missing-element
    kind: command
    language: shell
    description: Most query verbs need an element. Exits 1.
    content: '{{tool}} query describe vehicle.sysml'
    exit: 1
    expect: 'query describe: --element (-e) is required for this verb.'
  - name: unknown-element
    kind: command
    language: shell
    description: A name that the model lacks. Exits 1.
    content: '{{tool}} query describe -e Vehicle::Truck vehicle.sysml'
    exit: 1
    expect: "element 'Vehicle::Truck' not found in the workspace."
  - name: unknown-topic
    kind: command
    language: shell
    description: An unknown help topic exits 2.
    content: '{{tool}} help no-such-topic'
    exit: 2
    expect: "Error: Unknown help card 'no-such-topic'."
---
**`warning: Unresolved reference: '<name>'`.** A name does not resolve
from where it is used, as in the model that [[#broken]] prints: it is
not declared, it is `private` in another package, or it needs an
`import` in the package that uses it. Declare or import it, or qualify
it. The command still succeeds ([[#warning]]); `--strict-unresolved`
makes it an error and exit code 1 ([[#strict]]).

**`error: mismatched input ...`.** A syntax error. The line and column
show where the parser stopped; the command exits 1.

**`no files matched the given pattern(s).`** Check the folder you run
from, quote each pattern, and use `**` for subfolders; a path must name
a `.sysml` or `.kerml` file. See [[workspaces]].

**`--element (-e) is required for this verb.`** Every query verb except
`list` and `find` needs `-e <name>` ([[#missing-element]]).

**`element '<name>' not found in the workspace.`** Use the qualified
name that `query find --name <text>` shows, and load every file of the
model ([[#unknown-element]]).

**`Error: Unsupported argument '...'`.** An option that the command does
not take, or a misspelled one; it exits 1 and is printed even with
`--silent`. `{{tool}} <command> --help` lists the options of a command.

**`No view declarations found in the workspace`.** The model declares
no `view`. Add one, or use `--auto` or `--view-type`; see [[views]].

**`declares a filter expression that could not be evaluated`.** The view
is drawn unfiltered. Qualify the metadata name, as in
`@Vehicle::SoftwareInfo`, or write the filter as `not (@T)`.

**Unknown help topic.** `{{tool}} help <topic>` exits 2 and lists
the topics ([[#unknown-topic]]). `{{tool}} help` lists them with
their summaries, and `--search <words>` finds a topic.

**The tool does not start.** It needs the .NET 10 runtime; a machine
with only a newer .NET needs the .NET 10 runtime too.
