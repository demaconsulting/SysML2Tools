---
title: render command
summary: Draw the views of a SysML v2 model as SVG or PNG diagrams.
tags:
  - reference
see-also:
  - views
  - commands
  - workspaces
examples:
  - name: svg
    kind: command
    language: shell
    description: Render every view of the model as SVG into the folder out.
    content: '{{tool}} render vehicle.sysml --output out'
    expect: |
      VehicleView.svg
      Rendered 1 view(s).
  - name: png
    kind: command
    language: shell
    description: Render the views as PNG images instead.
    content: '{{tool}} render vehicle.sysml --output out --format png'
    expect: 'VehicleView.png'
  - name: view
    kind: command
    language: shell
    description: Render only the view with the given name.
    content: '{{tool}} render vehicle.sysml --view VehicleView --output out'
    expect: 'Rendered 1 view(s).'
  - name: missing-view
    kind: command
    language: shell
    description: A view name that the model lacks is an error. Exits 1.
    content: '{{tool}} render vehicle.sysml --view Nope --output out'
    exit: 1
    expect: "error: view 'Nope' not found"
---
`render` loads the files as one workspace and writes one diagram per
view:

```text
{{tool}} render [options] <files...>
```

| Option | Meaning |
| --- | --- |
| `--output <dir>` | Folder for the diagrams; default the current folder. |
| `--format <format>` | `svg` (default) or `png`. |
| `--view <name>` | Render only the named view. |
| `--auto` | Draw the main part def when the model declares no view. |
| `--view-type <kind>` | The kind of a view made on demand; see [[views]]. |
| `--view-target <name>` | The element that view draws. |
| `--filter <expr>` | Narrow that view to the elements that match. |
| `--walk-depth <n>` | Limit the nesting depth that is drawn. |
| `--strict-unresolved` | Report unresolved references as errors. |

**Output.** Each diagram is named after its view, such as
`VehicleView.svg`, as the command that [[#svg]] prints shows. The
tool prints `Writing <path>` for each file and then
`Rendered <n> view(s).`. The folder is created when it is missing, and
existing files are replaced.

**Selection.** Without `--view`, every declared view is drawn; with it,
only that view, and a name that the model lacks is an error that lists
the views it has ([[#missing-view]]). A model with no view writes
nothing and prints `No view declarations found in the workspace`,
unless `--auto` is given. [[views]] explains view kinds, `expose`, and
the views made on demand with `--view-type` and `--view-target`.

**Diagnostics.** The problems that [[commands/lint]] reports are
printed the same way, and the views are still drawn. The command exits
1 when any is an error: a syntax error, or an unresolved reference with
`--strict-unresolved` (exit code 1 for models that have any).
