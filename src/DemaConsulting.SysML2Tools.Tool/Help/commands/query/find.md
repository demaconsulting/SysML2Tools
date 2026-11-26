---
title: query find verb
summary: Search the workspace for the elements that match a kind or a name.
tags:
  - reference
order: 12
see-also:
  - commands/query/list
  - model-queries
examples:
  - name: run
    kind: command
    language: shell
    description: Find every element whose name contains Car.
    content: '{{tool}} query find --name Car vehicle.sysml'
    expect: '| Vehicle::Car::engine | part |'
  - name: no-filter
    kind: command
    language: shell
    description: find needs a filter. Exits 1.
    content: '{{tool}} query find vehicle.sysml'
    exit: 1
    expect: 'query find: at least one of --kind or --name is required.'
---
`find` searches the workspace and needs no element:

```text
{{tool}} query find [options] <files...>
```

It takes the filters of [[commands/query/list]], `--kind <kind>` and
`--name <text>`, and needs at least one of them ([[#no-filter]]).
Use it to learn the exact qualified name of an element before passing
it to another verb with `--element`, as the command that [[#run]]
prints shows. The options of [[commands/query]] apply.
