---
title: query dependencies verb
summary: Describe in prose what an element depends on and what depends on it.
tags:
  - reference
order: 3
see-also:
  - commands/query/uses
  - commands/query/used-by
examples:
  - name: run
    kind: command
    language: shell
    description: Describe the dependencies of the part definition Garage.
    content: '{{tool}} query dependencies -e Vehicle::Garage vehicle.sysml'
    expect: '- Depends on **Car** (dependency)'
---
`dependencies` combines [[commands/query/uses]] and
[[commands/query/used-by]] into one section of prose:

```text
{{tool}} query dependencies --element <name> [options] <files...>
```

The Markdown report has no table. It lists the outgoing references as
`- Depends on **Name** (kind)` and the incoming ones as
`- Used by **Name** (kind)`, with the common prefix of the qualified
names removed; see the command that [[#run]] prints.
Explicit
`dependency` statements appear with the kind `dependency`; imports
do not appear in either direction.

The JSON entries keep the full qualified names, list the outgoing
entries first, and add `Direction`: 0 for outgoing, 1 for incoming.
`--walk-depth` is accepted but ignored, because only direct references
are reported.
The options of [[commands/query]] apply.
