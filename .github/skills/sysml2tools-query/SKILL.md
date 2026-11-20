---
name: sysml2tools-query
description: Query this repository's SysML2 architecture model (docs/sysml2/) to understand software structure, purpose, and relationships before deep-diving into source code. Use this when asked to understand the codebase, find which unit implements something, assess the impact of a change, or trace requirements to code.
---

# SysML2Tools Query Skill

This repository models its software structure in SysML2 under `docs/sysml2/`. Prefer
querying this model over grepping/reading source files when you need to understand
*what* a piece of the system is, *why* it exists, or *what it depends on*.

## Prerequisites

The `sysml2tools` CLI is a local .NET tool pinned in `.config/dotnet-tools.json`.
Restore it once per session if not already available:

```pwsh
dotnet tool restore
```

## Model files

- `docs/sysml2/model/{system-name}.sysml` — system-level `part def` only (no subsystems/units
  inline). A repository may contain more than one system.
- `docs/sysml2/model/{system-name}/{subsystem-name}.sysml` and
  `docs/sysml2/model/{system-name}/{subsystem-name}/{unit-name}.sysml` — one file per
  Subsystem/Unit, nested to mirror the same folder depth as that item's companion
  `docs/design/`, `docs/reqstream/`, and `docs/verification/` files.
- `docs/sysml2/model/ots.sysml` — off-the-shelf (OTS) dependency parts (present when OTS items
  exist).
- `docs/sysml2/model/shared.sysml` — Shared Package parts (present when Shared Package items
  exist).
- `docs/sysml2/views/design-views.sysml` — named views rendered for the design document; a
  sibling of `model/`, not nested inside it. Not usually needed for query workflows, but
  useful to see how diagrams are scoped.

Always pass all `.sysml` files (or a glob covering them) to every `sysml2tools`
invocation — the model spans multiple files and cross-references between them; files that
reopen the same `package Name { ... }` from different files merge into one namespace
automatically, no `import` required. As of `sysml2tools` 0.1.0-beta.5, every subcommand
(`lint`, `render`, `query`) expands `*`/`**` glob patterns internally and recurses
correctly — pass a single **quoted** glob string and let the tool resolve it, rather than
relying on the shell (quoting works identically in both PowerShell and bash):

```pwsh
dotnet sysml2tools query list 'docs/sysml2/**/*.sysml'
```

## Artifact-location metadata

Every System/Subsystem/Unit `part def` applies a single `metadata info : SoftwareInfo { ... }`
annotation (defined once in `docs/sysml2/common/software-items.sysml`) pointing at its
companion artifacts (source, test, design, verification, and — for repositories that still
use ReqStream — requirements file). As of `sysml2tools` 0.1.0-beta.13+, `query describe`
surfaces every applied metadata attribute value as a `Metadata SoftwareInfo.{attribute}: ...`
summary line, so one query gets you every artifact location for that item without grepping
the source tree:

```pwsh
dotnet sysml2tools query describe -e {SystemName}::{UnitName} 'docs/sysml2/**/*.sysml'
```

```text
- Documentation: One-line purpose statement for the unit.
- Metadata SoftwareInfo.sourceFiles: ("src/{Project}/{Subsystem}/{UnitName}.cs")
- Metadata SoftwareInfo.testFiles: ("test/{Project}.Tests/{Subsystem}/{UnitName}Tests.cs")
- Metadata SoftwareInfo.designFiles: ("docs/design/{system-name}/{subsystem-name}/{unit-name}.md")
- Metadata SoftwareInfo.verificationFiles: ("docs/verification/{system-name}/{subsystem-name}/{unit-name}.md")
- Metadata SoftwareInfo.requirementFiles: ("docs/reqstream/{system-name}/{subsystem-name}/{unit-name}.yaml")
```

Not every item sets all five attributes — Systems/Subsystems have no `sourceFiles`, and units
without a dedicated companion doc point at their parent's doc instead (see the
Artifact-Location Metadata section of `sysml2-modeling.md` for the full rules). Prefer these
paths over guessing a file location from the unit name; fall back to `grep`/`glob` only when
the metadata is missing or the model is stale. Note: repositories still on a `sysml2tools`
version older than 0.1.0-beta.13, or that haven't yet migrated from the older `comment`-based
convention, won't show `Metadata ...` lines here — check `.config/dotnet-tools.json`'s pinned
version and fall back to reading `comment sourceRef`/`testRef`/`designRef`/`verificationRef`/
`reqRef` elements (rendered as `Comment: ...` lines) if present instead.

## Recommended workflow

1. **Discover the system(s) present** — do not assume a fixed name:

   ```pwsh
   dotnet sysml2tools query list --kind "part def" 'docs/sysml2/**/*.sysml'
   ```

   Look for the top-level part def with no containing part (or the one with the most
   children) to identify each system's qualified name.

2. **Get the full hierarchy** (subsystems and units) for a system found above:

   ```pwsh
   dotnet sysml2tools query describe -e <QualifiedName> 'docs/sysml2/**/*.sysml'
   ```

   `describe` lists direct children and every artifact-location metadata attribute; repeat on
   each child to walk deeper, or use `query list` to see the full flat inventory.

3. **Understand a specific unit's purpose and find its files** before opening its source
   file — `describe` returns both the purpose `doc` and every `Metadata SoftwareInfo...`
   artifact-location line in one call (see Artifact-location metadata above):

   ```pwsh
   dotnet sysml2tools query describe -e <QualifiedName> 'docs/sysml2/**/*.sysml'
   ```

4. **Assess impact before editing a unit, and see what it depends on** — use `dependencies`
   for the combined view, or `uses`/`used-by`/`impact` individually:

   ```pwsh
   dotnet sysml2tools query dependencies -e <QualifiedName> 'docs/sysml2/**/*.sysml'
   dotnet sysml2tools query impact -e <QualifiedName> 'docs/sysml2/**/*.sysml'
   ```

   `dependencies`/`uses`/`used-by` return both structural (typing/containment) references
   *and* explicit `dependency A to B;` statements (see `sysml2-modeling.md`'s Dependency
   Relationships section) — repositories that model real collaborators this way get accurate
   usage-impact results directly from the query, not just structural references. If a
   repository hasn't adopted explicit `dependency` modeling yet, these verbs only surface
   structural references; confirm actual usage in source before making impact-sensitive
   changes in that case.

5. **Trace requirements** linked to a unit, if modeled:

   ```pwsh
   dotnet sysml2tools query requirements -e <QualifiedName> 'docs/sysml2/**/*.sysml'
   ```

6. **Search by name or kind** when the qualified name is unknown:

   ```pwsh
   dotnet sysml2tools query find --name <PartialOrFullName> 'docs/sysml2/**/*.sysml'
   dotnet sysml2tools query list --kind "part def" 'docs/sysml2/**/*.sysml'
   ```

Use `--format json` on any query verb for machine-parsed output.

## Fallback

If the model is stale, doesn't yet cover a unit, or a query returns nothing useful, fall
back to `grep`/`glob`/reading source directly. When you finish work that adds or changes a
Unit/Subsystem, update the corresponding `.sysml` model file (including its artifact-location
metadata) in the same change (mirrors the existing requirement to keep `docs/design/*.md` and
`docs/reqstream/*.yaml` companion artifacts in sync). See the `sysml2-modeling.md`
standard for full modeling and view-authoring conventions.

## Validating changes to the model

Before committing changes to any `.sysml` file, lint it:

```pwsh
dotnet sysml2tools lint 'docs/sysml2/**/*.sysml'
```
