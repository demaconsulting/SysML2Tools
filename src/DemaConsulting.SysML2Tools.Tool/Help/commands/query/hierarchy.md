---
title: query hierarchy verb
summary: Report the supertypes and subtypes of an element, up, down, or both ways.
tags:
  - reference
order: 6
see-also:
  - commands/query/uses
examples:
  - name: run
    kind: command
    language: shell
    description: Show the supertypes and subtypes of Car.
    content: '{{tool}} query hierarchy -e Vehicle::Car vehicle.sysml'
    expect: '| Vehicle::Machine | supertype | depth 1 |'
  - name: down
    kind: command
    language: shell
    description: Show only the subtypes of Machine.
    content: '{{tool}} query hierarchy -e Vehicle::Machine --direction down vehicle.sysml'
    expect: '| Vehicle::Car | subtype | depth 1 |'
---
`hierarchy` walks the specialization tree of one element:

```text
{{tool}} query hierarchy --element <name> [options] <files...>
```

`--direction up` follows the supertypes, `down` the subtypes, and
`both` (the default) does both. The Markdown report has a
`**Related Types**` table; `Kind` is `supertype` or `subtype` and
`Detail` the distance. See the command that [[#run]] prints,
and for the subtypes alone, the command that [[#down]] prints.
The options of [[commands/query]] apply.
