---
title: query impact verb
summary: Report every element that a change to a given element can affect, with its distance.
tags:
  - reference
order: 4
see-also:
  - commands/query/used-by
  - commands/query/connections
examples:
  - name: run
    kind: command
    language: shell
    description: Report what a change to Engine affects.
    content: '{{tool}} query impact -e Vehicle::Engine vehicle.sysml'
    expect: '| Vehicle::Car::engine | part | depth 1 |'
  - name: connected
    kind: command
    language: shell
    description: Also follow the connections between parts.
    content: '{{tool}} query impact -e Vehicle::Engine --include-connections vehicle.sysml'
    expect: '| Vehicle::Car::tank | part | depth 2;'
  - name: bounded
    kind: command
    language: shell
    description: Stop after one relationship.
    content: '{{tool}} query impact -e Vehicle::Engine --walk-depth 1 vehicle.sysml'
    expect: 'depth 1'
---
`impact` walks the incoming references of an element again and again,
so it reports everything that a change can reach:

```text
{{tool}} query impact --element <name> [options] <files...>
```

| Option | Meaning |
| --- | --- |
| `--walk-depth <n>` | Stop after n relationships; default unlimited. |
| `--include-connections` | Also follow `connect` and `bind`, both ways. |

The Markdown report has an `**Impacted Elements**` table, and `Detail`
holds the shortest distance, such as `depth 1` ([[#run]]).

**Connections.** By default only references count, so a part that is
joined to the others only by connectors impacts nothing. With
`--include-connections`, a connector is followed in both directions
([[#connected]]), and a port at its end is reported as the part
that owns it, with the connector in `Detail`. `--walk-depth` counts every
relationship the same, so `--walk-depth 1` gives the direct
neighbors ([[#bounded]]). The JSON entries add `Depth`,
`Relation`, and `ViaQualifiedName`.
The options of [[commands/query]] apply.
