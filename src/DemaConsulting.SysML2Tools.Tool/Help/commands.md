---
title: Commands
summary: The four commands of the tool, and the rules that they share.
aliases:
  - command
tags:
  - reference
order: 3
see-also:
  - command-line
  - workspaces
  - views
examples:
  - name: card
    kind: command
    language: shell
    description: Print the card of one command, here render.
    content: '{{tool}} help render'
    expect: '{{tool}} render [options] <files...>'
---
Run a command as `{{tool}} [options] <command> [arguments]`:

| Command | Task |
| --- | --- |
| `lint` | Check models for syntax errors and unresolved references. |
| `render` | Draw the views of a model as SVG or PNG files. |
| `query` | Answer a question about the model (preview). |
| `export` | Write the whole resolved model as JSON or JSON Lines. |

Each command has its own card, listed below. Print one with
`{{tool}} help <command>`, as the command that [[#card]] prints
shows; `{{tool}} help commands <command>` prints the same card, and
`{{tool}} help query <verb>` prints the card of one query verb.
`{{tool}} <command> --help` prints the short usage of a command.

**Files.** Every command takes one or more file patterns and loads the
files they match as one workspace, together with the SysML v2 standard
library; see [[workspaces]]. When no file matches, the command prints
a message such as `lint: no files matched the given pattern(s).` and
exits 1.

**Options.** Each command takes its own options, which may come before
or after the files, and rejects the options of the other commands. The
global options, such as `--silent` and `--log`, apply to every command;
see [[command-line]].

**Unresolved references.** By default an unresolved reference is a
warning, and the command still does its work. `--strict-unresolved`,
which every command takes, makes it an error: `lint`, `render`, and
`query` then exit 1 for models that have any, and `export` gives them
`Severity` 2 in its JSON diagnostics with the exit code unaffected.
