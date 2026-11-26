---
title: Workspaces and file patterns
summary: How the commands choose the model files, and the form of the diagnostics.
aliases:
  - globs
  - files
  - patterns
  - diagnostics
tags:
  - reference
order: 4
see-also:
  - commands
  - sysml-models
  - troubleshooting
examples:
  - name: single
    kind: command
    language: shell
    description: Load one file.
    content: '{{tool}} lint vehicle.sysml'
    expect: 'Linting 1 file(s)...'
  - name: recursive
    kind: command
    language: shell
    description: Load every model file below the current folder.
    content: '{{tool}} lint "**/*.sysml"'
    expect: 'Linting 1 file(s)...'
  - name: exclude
    kind: command
    language: shell
    description: Load every model file except those below the folder out.
    content: '{{tool}} lint "**/*.sysml" "!out/**"'
    expect: 'Linting 1 file(s)...'
---
A **workspace** is the set of files that one command loads together.
The SysML v2 standard library is always part of it, so a model can use
its packages without listing any library file.

**Patterns.** Give one or more paths or patterns; a relative one starts
at the current folder. A path must name a `.sysml` or `.kerml` file, and a
pattern that ends in a bare `*`, as in `"src/**/*"`, keeps only those
files.

- `*` matches any part of one file or folder name, as in `*.sysml`.
- `**` matches any number of folders, as in `"**/*.sysml"`
  ([[#recursive]]).
- A pattern that starts with `!` removes files that the other patterns
  matched, as in `"!out/**"` ([[#exclude]]).

Put each pattern in quotes so that the tool, not the shell, expands
it. Double quotes work the same in bash, PowerShell, and cmd; single
quotes also work in bash and PowerShell.

**One model.** Every file of the workspace is one model, so a file can
use the names that another file declares. Each root-level name, such as
a package, may be declared only once in a workspace: a package cannot
be split across files. A later declaration of the same name is ignored
with a warning.

**Diagnostics.** `lint`, `render`, and `query` print each problem as
one line, in the form that compilers, editors, and CI annotations use:

```text
path(line,col): severity: message
```

`line` and `col` start at 1, and `severity` is `error`, `warning`, or
`info`. A message that is not tied to a file has no `path(line,col):`
part. `export` puts the same facts in its JSON; see [[commands]].
