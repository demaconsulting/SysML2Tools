---
title: lint command
summary: Check SysML v2 files for syntax errors, unresolved references, and other problems.
tags:
  - reference
see-also:
  - commands
  - workspaces
  - troubleshooting
examples:
  - name: check
    kind: command
    language: shell
    description: Check one model file.
    content: '{{tool}} lint vehicle.sysml'
    expect: |
      Linting 1 file(s)...
      lint: no errors found.
  - name: strict
    kind: command
    language: shell
    description: Treat unresolved references as errors.
    content: '{{tool}} lint --strict-unresolved vehicle.sysml'
    expect: 'lint: no errors found.'
  - name: glob
    kind: command
    language: shell
    description: Check every model file in the current folder.
    content: '{{tool}} lint "*.sysml"'
    expect: 'Linting 1 file(s)...'
  - name: no-match
    kind: command
    language: shell
    description: A pattern that matches no file is an error. Exits 1.
    content: '{{tool}} lint missing.sysml'
    exit: 1
    expect: 'lint: no files matched the given pattern(s).'
---
`lint` loads the files as one workspace, resolves their references, and
reports every problem it finds:

```text
{{tool}} lint [--strict-unresolved] <files...>
```

It prints `Linting <n> file(s)...`, one line per diagnostic in the form
`path(line,col): severity: message` (see [[workspaces]]), and then
`lint: no errors found.` or `lint: <n> error(s) found.`, as the command
that [[#check]] prints shows.

**Errors** are syntax errors and files that cannot be read. With at least
one error the command exits 1.

**Warnings** do not change the exit code. They are unresolved
references, cyclic specialization, a root-level name declared more than
once (the later declaration is ignored), and the render selectors that
only {{tool}} knows, such as `render asGeneralDiagram;`, which other
SysML v2 tools reject.

**Strict.** `--strict-unresolved` reports unresolved references as
errors instead of warnings (exit code 1 for models that have any), as in
the command that [[#strict]] prints. A name declared twice stays a
warning.

**Files.** Give one or more paths or quoted patterns ([[#glob]]). A
pattern that matches no file is an error ([[#no-match]]), and so is a
run with no file at all. Any option other than `--strict-unresolved`
and the global options is rejected.
