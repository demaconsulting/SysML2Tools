---
title: query command
summary: Answer questions about a SysML v2 model with one of twelve verbs, as Markdown or JSON.
tags:
  - reference
see-also:
  - model-queries
  - commands
  - workspaces
examples:
  - name: list
    kind: command
    language: shell
    description: List every part definition of the model.
    content: '{{tool}} query list --kind "part def" vehicle.sysml'
    expect: '| Vehicle::Car | part def |'
  - name: heading
    kind: command
    language: shell
    description: Start the report with a level-2 heading of your own.
    content: '{{tool}} query describe -e Vehicle::Car --depth 2 --heading "Car Report" vehicle.sysml'
    expect: '## Car Report'
  - name: output
    kind: command
    language: shell
    description: Write the report to a file instead of the console.
    content: '{{tool}} query list --kind "part def" --output parts.md vehicle.sysml'
    expect: "query list: wrote output to 'parts.md'."
  - name: verb-card
    kind: command
    language: shell
    description: Print the card of one verb, here describe.
    content: '{{tool}} help query describe'
    expect: '{{tool}} query describe --element <name> [options] <files...>'
---
`query` loads the files as one workspace, resolves the model, and
answers one question, chosen by its verb. The command is a preview.

```text
{{tool}} query <verb> [options] <files...>
```

| Verb | Answers |
| --- | --- |
| `uses` | The elements a given element uses. |
| `used-by` | The elements that use a given element. |
| `dependencies` | Both of the above, as prose. |
| `impact` | The elements a change to a given element affects. |
| `describe` | One element in detail. |
| `hierarchy` | The supertypes and subtypes of an element. |
| `requirements` | The satisfy, verify, and allocate links of an element. |
| `interface` | The ports and typed features of an element. |
| `connections` | The connections attached to an element. |
| `states` | The states and transitions of a state machine. |
| `list` | The elements of the workspace, optionally filtered. |
| `find` | The elements that match a filter. |

Each verb has its own card, listed below; print one with
`{{tool}} help query <verb>`, as the command that [[#verb-card]]
prints shows. `{{tool}} query <verb> --help` prints its short usage.

| Option | Meaning |
| --- | --- |
| `--element <name>`, `-e` | The element; all verbs except `list` and `find`. |
| `--format <format>` | `markdown` (default) or `json`. |
| `--output <file>` | Write to this file instead of the console. |
| `--depth <n>` | First Markdown heading level, 1 to 6; default 1. |
| `--heading <text>` | Markdown heading text instead of the default. |
| `--include-stdlib` | Also report standard library elements. |
| `--strict-unresolved` | Report unresolved references as errors. |

The verb cards list the options that only one verb takes: `--walk-depth`
and `--include-connections` (`impact`), `--direction` (`hierarchy`), and
`--kind` and `--name` (`list` and `find`).

**Workflow.** Run `list` or `find` first to learn the exact qualified
names of the workspace, as the command that [[#list]] prints shows,
then pass them to the other verbs with `--element`; see
[[model-queries]].

**Markdown output** starts with a `# query <verb>: <element>` heading,
then summary bullets, a bold label such as `**Children**`, and a
`| Qualified Name | Kind | Detail |` table; an empty result prints a
line such as `_No children._` instead. `--depth` and `--heading`
change the heading, so a report fits into a larger document
([[#heading]]).

**JSON output** is one object with `Verb`, `Element`, `Summary` (the
bullets), and `Entries`; each entry has `QualifiedName`, `Kind`,
`Detail`, and `Notes`. Both formats sort the entries by qualified name;
`dependencies` lists the outgoing entries first.

**Files.** `--output` names a file, not a folder ([[#output]]), and
the file holds only the report. On the console, the report follows the
banner and the loading messages, and the global `--silent` hides it,
so add `--output` when you use `--silent`; see [[command-line]]. The
command exits 1 when the model has
an error, as [[commands/lint]] does, and when the element is not found.
