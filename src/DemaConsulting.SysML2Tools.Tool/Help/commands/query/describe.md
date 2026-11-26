---
title: query describe verb
summary: 'Describe one element: its kind, supertypes, documentation, metadata, and children.'
tags:
  - reference
order: 5
see-also:
  - model-queries
  - commands/query/find
examples:
  - name: run
    kind: command
    language: shell
    description: Describe the part definition Car.
    content: '{{tool}} query describe -e Vehicle::Car vehicle.sysml'
    expect: |
      - Kind: part def
      - Supertypes: Vehicle::Machine
      - Documentation: A car with an engine, a tank, and four wheels.
      - Metadata SoftwareInfo.owner: drivetrain team
  - name: json
    kind: command
    language: shell
    description: Get the same facts as JSON.
    content: '{{tool}} query describe -e Vehicle::Car --format json vehicle.sysml'
    expect: '"Metadata SoftwareInfo.owner: drivetrain team"'
---
`describe` gives a fact sheet of one element:

```text
{{tool}} query describe --element <name> [options] <files...>
```

The summary bullets name the kind, the qualified name, the supertypes,
the type, the `doc` text, and one `Metadata <def>.<attribute>: <value>`
line for each attribute of the metadata applied to it; see the
command that [[#run]] prints. A `**Children**` table lists the
members that the element owns, or `_No children._`.

When the name was found through a `public import` or an alias rather
than where it is declared, the report adds a `Looked up as: <name>`
bullet and shows the declared name. `--format json` puts the bullets
in `Summary`, as in the command that [[#json]] prints.
The options of [[commands/query]] apply.
