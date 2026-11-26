---
title: query requirements verb
summary: List the satisfy, verify, and allocate relationships that involve an element.
tags:
  - reference
order: 7
see-also:
  - model-queries
examples:
  - name: run
    kind: command
    language: shell
    description: List the requirements that Car satisfies.
    content: '{{tool}} query requirements -e Vehicle::Car vehicle.sysml'
    expect: '| Vehicle::carMass | satisfy | satisfies |'
---
`requirements` traces an element to its requirements, or a requirement
to the elements that satisfy, verify, or are allocated to it:

```text
{{tool}} query requirements --element <name> [options] <files...>
```

The Markdown report has a `**Requirement Relationships**` table;
`Kind` is `satisfy`, `verify`, or `allocate`, and `Detail` the
direction, such as `satisfies`; see the command that
[[#run]] prints.
The options of [[commands/query]] apply.
