---
title: query used-by verb
summary: List the elements that use a given element, the reverse of uses.
tags:
  - reference
order: 2
see-also:
  - commands/query/uses
  - commands/query/impact
examples:
  - name: run
    kind: command
    language: shell
    description: List what uses the part definition Engine.
    content: '{{tool}} query used-by -e Vehicle::Engine vehicle.sysml'
    expect: '| Vehicle::Car::engine | typing | part |'
---
`used-by` follows the incoming references of one element:

```text
{{tool}} query used-by --element <name> [options] <files...>
```

It answers "what depends on this element?" with the elements that
reference it directly, such as subtypes, features typed by it, and the
sources of `dependency` statements. Only direct references count; use
[[commands/query/impact]] for everything a change reaches.

The Markdown report has a `**Used By**` table; `Kind` is the kind of
the reference and `Detail` the kind of the user; see the command that
[[#run]] prints.
The options of [[commands/query]] apply.
