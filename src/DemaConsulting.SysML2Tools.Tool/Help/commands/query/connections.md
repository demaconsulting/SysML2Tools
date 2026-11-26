---
title: query connections verb
summary: List the far ends of the connections that touch an element or its features.
tags:
  - reference
order: 9
see-also:
  - commands/query/interface
  - commands/query/impact
examples:
  - name: run
    kind: command
    language: shell
    description: List what the engine of a car is connected to.
    content: '{{tool}} query connections -e Vehicle::Car::engine vehicle.sysml'
    expect: '| Vehicle::Car::tank::fuelOut | connection | B |'
---
`connections` lists the connectors that end at an element or at a
feature inside it, including ends written as dotted feature chains:

```text
{{tool}} query connections --element <name> [options] <files...>
```

Each row of the `**Connection Endpoints**` table is the other end of
one connector. `Kind` is the connector keyword, such as `connection`,
`connect`, or `bind`, and `Detail` is `A` when the element is at the
first end and `B` when it is at the second; see the command that
[[#run]] prints.
The options of [[commands/query]] apply.
