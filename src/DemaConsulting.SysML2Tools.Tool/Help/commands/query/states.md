---
title: query states verb
summary: List the states and transitions inside a state definition or state usage.
tags:
  - reference
order: 10
see-also:
  - views
examples:
  - name: run
    kind: command
    language: shell
    description: List the states of the Ignition state machine.
    content: '{{tool}} query states -e Vehicle::Ignition vehicle.sysml'
    expect: |
      | Vehicle::Ignition::off | state |
      | Vehicle::Ignition::on | state |
---
`states` lists the states and transitions nested at any depth in one
state machine:

```text
{{tool}} query states --element <name> [options] <files...>
```

The Markdown report has a `**States & Transitions**` table; `Kind` is
`state` or `transition`, and the `Detail` of a transition shows its
source and target, such as `A -> B`, as the command that [[#run]]
prints shows. The options of [[commands/query]] apply.
