---
title: query uses verb
summary: List the elements that a given element uses, such as its supertypes and the types of its features.
tags:
  - reference
order: 1
see-also:
  - commands/query/used-by
  - commands/query/dependencies
examples:
  - name: run
    kind: command
    language: shell
    description: List what the part definition Car uses.
    content: '{{tool}} query uses -e Vehicle::Car vehicle.sysml'
    expect: '| Vehicle::Machine | supertype | part def |'
---
`uses` follows the outgoing references of one element:

```text
{{tool}} query uses --element <name> [options] <files...>
```

It answers "what does this element depend on?" with the elements that
the element references directly: its supertypes, the types of its
features, the requirements it satisfies, and the targets of explicit
`dependency` statements. The command that [[#run]] prints shows a
supertype and a satisfied requirement.

Imports are not reported: a package that imports another, such as
`import Lib::*;`, lists no `import` entry, and the imported elements
list no user for it, here or in [[commands/query/used-by]] and
[[commands/query/dependencies]]. The types of features are reported
on the features, not on their owner: `part engine : Engine;` in `Car`
is a `typing` entry of `Car::engine`.

The Markdown report has a `**Uses**` table; `Kind` is the kind of the
reference, such as `supertype`, `typing`, or `dependency`, and `Detail`
the kind of the element used.
The options of [[commands/query]] apply.
