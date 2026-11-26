---
title: export command
summary: Write the whole resolved model as JSON or JSON Lines, for scripts and other tools.
tags:
  - reference
see-also:
  - commands
  - model-queries
  - workspaces
examples:
  - name: json
    kind: command
    language: shell
    description: Print the model as one JSON document.
    content: '{{tool}} export vehicle.sysml'
    expect: |
      "Declarations": {
      "Edges": [
      "Diagnostics": []
  - name: jsonl
    kind: command
    language: shell
    description: Print one JSON object per line instead.
    content: '{{tool}} export vehicle.sysml --format jsonl'
    expect: '{"Kind":"declaration","QualifiedName":"Vehicle::Car",'
  - name: file
    kind: command
    language: shell
    description: Write the JSON to a file.
    content: '{{tool}} export vehicle.sysml --output model.json'
    expect: "to 'model.json'."
  - name: target
    kind: command
    language: shell
    description: Export only the subtree of one element.
    content: '{{tool}} export vehicle.sysml --target Vehicle::Car --output car.json'
    expect: "to 'car.json'."
---
`export` loads the files as one workspace and writes every declaration,
every resolved relationship, and every diagnostic. Use it to analyze a
whole model with another tool; use [[commands/query]] for one question.

```text
{{tool}} export [options] <files...>
```

| Option | Meaning |
| --- | --- |
| `--format <format>` | `json` (default) or `jsonl` (JSON Lines). |
| `--output <file>` | Write to this file instead of the console. |
| `--include-stdlib` | Also export the standard library elements. |
| `--target <name>` | Export only the subtree of this element. |
| `--filter <expr>` | Keep only the elements that match; see [[views]]. |
| `--strict-unresolved` | Report unresolved references as errors. |

**JSON** is one indented document with `Declarations`, an object keyed
by qualified name, and the arrays `Edges` and `Diagnostics`, as the
command that [[#json]] prints shows. An edge has
`SourceQualifiedName`, `TargetQualifiedName`, a numeric `Kind`, and,
for most kinds, the `RawReference` as written. A diagnostic has
`FilePath`, `Line`, `Column`, `Severity` (1 warning, 2 error), and
`Message`.

**JSON Lines** writes one compact object per line, first the
declarations, then the edges, then the diagnostics; its `Kind` is
`declaration`, `edge`, or `diagnostic` ([[#jsonl]]).

**Files.** `--output` names a file, not a folder, and the file holds
only the export ([[#file]]); the tool then prints how many
declarations, edges, and diagnostics it wrote. On the console, the
export follows the banner and the loading messages.

**Narrowing.** `--target` keeps the containment subtree of one
element ([[#target]]); for a usage, the subtree of its type too. A
target that is not found is an error and exits 1. `--filter` is
applied after `--target`; an expression that cannot be evaluated adds
a warning and leaves the export unfiltered.

**Diagnostics.** Model problems are reported only in the output, so
the exit code stays 0. With `--strict-unresolved`, an unresolved
reference has `Severity` 2 in the JSON diagnostics (exit code
unaffected).
