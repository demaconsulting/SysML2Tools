---
title: query list verb
summary: List the elements of the workspace, optionally filtered by kind and name.
tags:
  - reference
order: 11
see-also:
  - commands/query/find
  - model-queries
examples:
  - name: run
    kind: command
    language: shell
    description: List the part definitions of the model.
    content: '{{tool}} query list --kind "part def" vehicle.sysml'
    expect: |
      - 6 element(s) match the filter.
      | Vehicle::Car | part def |
---
`list` enumerates the elements of the workspace and needs no element:

```text
{{tool}} query list [options] <files...>
```

| Option | Meaning |
| --- | --- |
| `--kind <kind>` | Keep the elements whose kind contains this text. |
| `--name <text>` | Keep the elements whose name contains this text. |

Both filters ignore case. `--kind` compares with the kind shown in the
`Kind` column, such as `part def`, so `--kind requirement` also keeps
`requirement def`. `--name` also matches the qualified name. Without
filters, every element is listed. The Markdown report has a
`**Matching Elements**` table, as the command that [[#run]] prints
shows. The options of [[commands/query]] apply.
