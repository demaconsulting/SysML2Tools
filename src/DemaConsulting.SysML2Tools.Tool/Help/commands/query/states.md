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
      | Vehicle::Ignition::on | state |  |
      | Vehicle::Ignition::on | transition | Vehicle::Ignition::off -> Vehicle::Ignition::on |
---
`states` lists the states and transitions nested at any depth in one
state machine:

```text
{{tool}} query states --element <name> [options] <files...>
```

The Markdown report has a `**States & Transitions**` table; `Kind` is
`state` or `transition`. A state row shows the qualified name of the
state. A transition row does not show the name of the transition:
its `Qualified Name` is the **target** state, and its `Detail` shows
the source and target, such as `A -> B`, followed by `if <guard>`
when the transition has a guard. So the transition `turnOn` from
`off` to `on` is a `transition` row named `Vehicle::Ignition::on`
with the detail `Vehicle::Ignition::off -> Vehicle::Ignition::on`, and
it sorts right after the state `on`, as the command that [[#run]]
prints shows.

A transition without a source, such as the initial `entry; then off;`,
or one whose names do not all resolve, shows the names as written:
here the row `off` with the detail `-> off`. The options of
[[commands/query]] apply.
