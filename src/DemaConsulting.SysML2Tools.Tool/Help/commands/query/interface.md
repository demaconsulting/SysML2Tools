---
title: query interface verb
summary: List the ports and typed features that an element exposes.
tags:
  - reference
order: 8
see-also:
  - commands/query/connections
examples:
  - name: run
    kind: command
    language: shell
    description: List the ports of the part definition Engine.
    content: '{{tool}} query interface -e Vehicle::Engine vehicle.sysml'
    expect: '| Vehicle::Engine::fuelIn | port | FuelPort |'
---
`interface` lists the ports and the other typed features of one
element:

```text
{{tool}} query interface --element <name> [options] <files...>
```

The Markdown report has a `**Ports & Typed Features**` table; `Kind`
is the feature keyword, such as `port`, `part`, or `attribute`, and
`Detail` the type with its multiplicity; see the command that
[[#run]] prints.
The options of [[commands/query]] apply.
