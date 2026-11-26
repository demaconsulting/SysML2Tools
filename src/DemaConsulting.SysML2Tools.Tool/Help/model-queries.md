---
title: Model queries
summary: Find elements, read their facts, and follow dependencies and requirements.
aliases:
  - architecture
  - agents
  - ai
  - navigation
  - queries
tags:
  - tutorial
order: 7
see-also:
  - commands/query
  - sysml-models
  - workspaces
examples:
  - name: discover
    kind: command
    language: shell
    description: List the part definitions to learn their names.
    content: '{{tool}} query list --kind "part def" vehicle.sysml'
    expect: '| Vehicle::Car | part def |'
  - name: find
    kind: command
    language: shell
    description: Find the elements whose name contains a word.
    content: '{{tool}} query find --name engine vehicle.sysml'
    expect: '| Vehicle::Engine | part def |'
  - name: describe
    kind: command
    language: shell
    description: Read the purpose and metadata of one element.
    content: '{{tool}} query describe -e Vehicle::Car vehicle.sysml'
    expect: |
      - Documentation: A car with an engine, a tank, and four wheels.
      - Metadata SoftwareInfo.owner: drivetrain team
  - name: dependencies
    kind: command
    language: shell
    description: See what an element depends on and what uses it.
    content: '{{tool}} query dependencies -e Vehicle::Car vehicle.sysml'
    expect: '- Used by **Garage** (dependency)'
  - name: impact
    kind: command
    language: shell
    description: See everything a change to an element can reach.
    content: '{{tool}} query impact -e Vehicle::Engine vehicle.sysml'
    expect: 'Vehicle::Car::engine'
  - name: requirements
    kind: command
    language: shell
    description: Trace an element to its requirements.
    content: '{{tool}} query requirements -e Vehicle::Car vehicle.sysml'
    expect: 'Vehicle::carMass'
  - name: json
    kind: command
    language: shell
    description: Get a result as JSON for a script or an agent.
    content: '{{tool}} query uses -e Vehicle::Car --format json vehicle.sysml'
    expect: '"QualifiedName": "Vehicle::Machine"'
  - name: repo-glob
    kind: snippet
    language: shell
    save-as: query-model.sh
    description: Query the architecture model of a repository.
    content: |
      dotnet tool restore
      dotnet {{tool}} query list --kind "part def" 'docs/sysml2/**/*.sysml'
---
A SysML v2 model can describe the structure of a system: its parts,
what each is for, where its files are, what it depends on, and which
requirements it meets. [[commands/query]] answers those questions from
the resolved model, for a person or an AI agent, without reading the
`.sysml` files. Each step below names the command that prints it.

1. **Discover the names.** `list --kind "part def"` lists the
   definitions ([[#discover]]), and `find --name <text>` searches by
   name ([[#find]]). Both show the qualified names to use next.
2. **Describe an element.** `describe -e <name>` ([[#describe]])
   prints its kind, supertypes, `doc` text, children, and one
   `Metadata <def>.<attribute>: <value>` line per attribute of the
   metadata applied to it, such as the files that implement it.
3. **Follow the dependencies.** `dependencies` ([[#dependencies]])
   lists, as prose, what the element uses and what uses it, including
   explicit `dependency A to B;` statements. `uses` and `used-by`
   give the two halves as tables, and `impact` ([[#impact]]) everything
   a change can reach.
4. **Trace the requirements.** `requirements` ([[#requirements]])
   lists the satisfy, verify, and allocate links.

**Names.** Pass the qualified name that `list` or `find` shows. A name
reached through a `public import`, such as `System::Unit` for a unit
declared in `System_Unit`, works too; the report then shows the
declared name and a `Looked up as:` line.

**Files.** Load every file of the model in each command, with one
quoted pattern such as `'docs/sysml2/**/*.sysml'`, since the files
refer to each other; [[#repo-glob]] shows it for a repository with the
tool in its manifest. See [[workspaces]] for patterns and quotes.

**Machine output.** `--format json` gives the same result as JSON for a
script or an agent ([[#json]]); `--output <file>` writes it to a file.
