---
name: SysML2 Modeling
description: Follow these standards when creating or modifying the SysML2 architecture
  model, its rendered views, or the software structure it feeds into design documentation.
globs: ["docs/sysml2/**/*.sysml"]
---

# SysML2 Modeling Standard

## Required Standards

Read these standards first before applying this standard:

- **`software-items.md`** - Software categorization (System/Subsystem/Unit/OTS/Shared Package)
- **`design-documentation.md`** - Design document structure that embeds the diagrams this
  standard produces

## Purpose

The repository's software structure is modeled in SysML2 under `docs/sysml2/` rather than
hand-maintained as prose or an ASCII tree. The model is the authoritative, machine-queryable
source of structure; rendered diagrams and `docs/design/introduction.md`'s narrative are
generated/derived artifacts. AI agents should query the model (see `sysml2tools-query`
skill) before deep-diving into source code to understand what a piece of the system is,
why it exists, and what it depends on.

## Repository Structure

```text
docs/sysml2/
├── common/
│   └── software-items.sysml           # shared SoftwareItemMetadata::SoftwareInfo metadata def
├── model/
│   ├── {system-name}.sysml            # package {SystemName}; system-level part def only (no subsystems/units inline)
│   ├── {system-name}/
│   │   ├── {subsystem-name}.sysml     # package {SystemName}_{SubsystemName}Subsystem; nests further for sub-subsystems
│   │   └── {subsystem-name}/
│   │       └── {unit-name}.sysml      # package {SystemName}_{UnitName}; one file per unit
│   ├── ots.sysml                      # package OtsDependencies: OTS parts (optional; if OTS items exist)
│   └── shared.sysml                   # package SharedPackages: Shared Package parts (optional; if Shared Packages exist)
└── views/
    └── design-views.sysml             # package {RepoName}DesignViews; named view usages rendered for the design document
```

`docs/sysml2/common/software-items.sysml` defines the `SoftwareItemMetadata::SoftwareInfo`
`metadata def` used by every System/Subsystem/Unit for artifact-location traceability (see
Artifact-Location Metadata below). It is intentionally self-contained (no repo-specific
imports) so it can be copied/synced verbatim across repositories without modification — do not
edit it beyond keeping it in sync with the canonical version.

`docs/sysml2/model/` mirrors the same folder shape as `docs/design/`, `docs/reqstream/`, and
`docs/verification/` — one `.sysml` file per System/Subsystem/Unit, at the same nesting depth
as that item's companion design/requirements/verification files. This keeps each file small
and keeps PRs that touch one unit's model from producing unrelated diffs in unrelated units.
`docs/sysml2/views/` is a sibling of `model/`, not nested inside it, so a single
`docs/sysml2/model/**/*.sysml` glob never needs to exclude view files (`sysml2tools` has no
exclude-glob syntax).

There is no separate "stable entry point" file — a repository may contain multiple systems,
so no single alias name would generalize. Agents discover the system(s) present by running
`query list --kind "part def"` or `query find` (see the `sysml2tools-query` skill) over
`docs/sysml2/**/*.sysml`, not by assuming a fixed name.

Always pass the full set of `.sysml` files (or an equivalent glob) to every `sysml2tools`
invocation — the model spans multiple files and cross-references between them, and a
reference only resolves when the file declaring its target is loaded (see Package Structure
below for how files reference each other). As of `sysml2tools` 0.1.0-beta.5,
`lint` and `render` both expand `*`/`**` glob patterns internally (recursing correctly into
subdirectories), so pass a single **quoted** glob string (e.g. `'docs/sysml2/**/*.sysml'`)
and let the tool resolve it — do not rely on the shell to expand it, and do not quote-strip
the pattern before it reaches the tool. Quoting keeps the behavior identical across
PowerShell and bash, since neither shell needs to (or reliably does) expand `**` itself.

## Package Structure

SysML v2 has no package merging: each file is its own root namespace, and two root packages
with the same name are duplicate members of the global namespace (the OMG Pilot
Implementation reports "Duplicate of other owned member name" for each, and references across
the halves do not resolve). A model that only loads because a tool is lenient is not portable,
so:

- Each `.sysml` file declares **exactly one uniquely named package**, and a package is never
  declared (or "reopened") in more than one file.
- Name each package from its place in the hierarchy, joined with an underscore:
  `{SystemName}` for the system file, `{SystemName}_{SubsystemName}Subsystem` for a subsystem
  (including a nested sub-subsystem, which is not prefixed with its parent subsystem), and
  `{SystemName}_{UnitName}` for a unit. The system prefix keeps names unique across systems.
  The shared files use `OtsDependencies` and `SharedPackages`, and the single views file uses
  one repository-level package, `{RepoName}DesignViews` (not one per system).
- Package names must be unique **repository-wide**. The short forms above are the default;
  when one would collide (two `Storage` subsystems in different branches, or two units named
  `Cache` in different subsystems), qualify it with the owning subsystem path, for example
  `{SystemName}_{SubsystemName}_{UnitName}` (`Acme_Ingest_Cache` and `Acme_Query_Cache`).
- Reference other files with an explicit `private import {PackageName}::*;` (a system or
  subsystem file imports the package of each direct member so its `part` usages resolve; every
  file that applies `SoftwareInfo` imports `SoftwareItemMetadata::*`).
- Where an import would form a cycle (for example a unit that depends on a package that
  already imports it), or for a one-off reference into another system, use the qualified name
  instead, e.g. `dependency {UnitName} to {SystemName}_{OtherUnit}::{OtherUnit};`.
  Import cycles raise a lint warning; qualified references do not.

## Model Content

- `{system-name}.sysml` defines one `part def` for the System only, with `part` usages
  referencing its direct Subsystems/Units (defined in their own files, see below).
- Each `{subsystem-name}.sysml` / `{unit-name}.sysml` defines exactly one `part def`, with a
  `doc /* ... */` comment stating its purpose — mirroring what would otherwise be written as
  prose in `docs/design/introduction.md`'s Software Structure section. The standard SysML v2
  `doc` comment is the single purpose statement for every System/Subsystem/Unit/OTS `part def`;
  do not duplicate it in metadata. Subsystem files add
  `part` usages for their own direct children (nested subsystems or units), matching the
  Software Item Hierarchy in `software-items.md`.
- `ots.sysml` / `shared.sysml` define one `part def` per OTS item / Shared Package, referenced
  as `part` usages from the system(s) that depend on them.

## Artifact-Location Metadata

Every System/Subsystem/Unit `part def` records where its companion artifacts live, as a single
applied `metadata info : SoftwareInfo { ... }` annotation, using the shared `SoftwareInfo`
`metadata def` from `docs/sysml2/common/software-items.sysml` (see Repository Structure above).
As of `sysml2tools` 0.1.0-beta.13+, applied metadata attribute values are fully surfaced by
`sysml2tools query describe` (in both Markdown and JSON output) as `Metadata
SoftwareInfo.{attribute}: {value}` summary lines — one call gets every artifact location for a
unit without grepping the source tree. (Prior to beta.13, `query describe` silently dropped
metadata attribute values entirely; this is why an earlier version of this standard recommended
`comment` elements instead — that workaround is no longer needed and should not be used in new
work.) `SoftwareInfo` carries artifact locations only — the purpose statement lives solely in
the `doc` comment (shown by `query describe` as the `Documentation:` line). Metadata
annotations have zero effect on rendered diagrams.

```sysml
private import SoftwareItemMetadata::*;

part def {UnitName} {
    doc /* One-line purpose statement. */

    metadata info : SoftwareInfo {
        sourceFiles = ("{path to the unit's source file}");
        testFiles = ("{path to the unit's test file}");
        designFiles = ("{path to the unit's design doc}");
        verificationFiles = ("{path to the unit's verification doc}");
        requirementFiles = ("{path to the unit's reqstream file}");
    }
}
```

Every `sourceFiles`/`testFiles`/`designFiles`/`verificationFiles`/`requirementFiles` attribute is
`String [0..*]` (a list), so multiple files can be listed per attribute when applicable; omit an
attribute entirely (do not assign an empty list) when it does not apply to the item:

- **Systems and Subsystems** have no `sourceFiles` (no single source file represents a whole
  system or subsystem) — set `testFiles` to the subsystem-level test file when one exists.
- **Units without a dedicated companion doc** (e.g. small data-model types documented inline
  within their subsystem's design doc rather than in their own file) point `designFiles` /
  `verificationFiles` / `requirementFiles` at the subsystem-level doc, with a short parenthetical
  note in the `doc` comment, e.g. `/* ... (documented as a supporting value type in the subsystem
  doc). */`.
- **Units without a dedicated test file** (exercised only indirectly through another unit's
  tests) omit `testFiles` entirely rather than pointing at an unrelated test file.
- **OTS items** have no `sourceFiles`/`testFiles` (no local integration code); set `designFiles`
  (when the optional design doc exists) / `verificationFiles` / `requirementFiles` only.

This is prose-searchable metadata only — it is not a substitute for the authoritative
requirements/design/verification artifacts, and it does not replace ReqStream as the
requirements source of truth. (This repository currently keeps requirements traceability in
ReqStream; a future migration to native SysML2 `requirement`/`satisfy` modeling is a separate,
not-yet-scheduled change.)

## Dependency Relationships

- Model "who uses what" as `dependency A to B;` statements, declared in the file of the
  **Unit that owns the outgoing dependency** (`from` is optional) — never centralized in a
  shared file, never at the Subsystem level (subsystems are pure containers with no code).
- Add a one-line comment above each unit's dependency block stating *why*, not just *what*.
- Source each Unit's dependency list from that Unit's own design doc, not a Subsystem-level
  summary diagram (which may omit real edges, e.g. re-entrant calls).
- Consume dependencies by **querying**, not by rendering a dedicated diagram: `sysml2tools query
  uses`/`used-by`/`dependencies --element {QualifiedName}` return every `dependency` edge (in
  addition to structural typing/containment references) directly from the model — see the
  `sysml2tools-query` skill. Do not declare a dedicated `DependencyView`/
  `{SubsystemName}DependencyView` diagram for this purpose: `render asInterconnectionDiagram`
  ignores `dependency` edges entirely (only renders `connect`), and an earlier `asGeneralDiagram`-
  based dependency-diagram approach was abandoned as unmaintainable at scale (unreadable/clipped
  canvases once a system has more than a handful of cross-unit dependencies). Model `dependency`
  statements purely as queryable facts, never as a rendering target.

## Attribute Modeling

- Add `attribute` declarations to Units whose C# type carries real data fields (records,
  DTOs, config objects), sourced directly from the C# properties/record parameters. Import
  scalar types from `ScalarValues` (`String`, `Integer`, `Boolean`, etc.). For a C# `enum`,
  declare a matching SysML2 `enum def`. Leave pure-behavior Units as empty `part def`s.
- SysML2 has no map/dictionary attribute type. For a `Dictionary<TKey,TValue>`-shaped
  field, split it into a parallel `{name}_key`/`{name}_value` attribute pair (underscore,
  not hyphen — bare identifiers disallow hyphens), with a comment stating the key
  semantics. If `TValue` is itself a modeled Unit, use a `part {name} : {ValueUnit} [0..*];`
  composition instead of an attribute pair, with the same kind of keying comment.

## Views (`docs/sysml2/views/design-views.sysml`)

Views control what gets rendered into diagrams for the design document. Use named `view`
**usages** (not `view def` **definitions**) — `expose` is only valid inside a usage:

```sysml
package {RepoName}DesignViews {
    private import StandardViewDefinitions::*;
    private import {SystemName}::*;
    private import {SystemName}_{SubsystemName}Subsystem::*;

    view SoftwareStructureView : InterconnectionView {
        expose {SystemName}::{SystemName}System::**;  // system part def, recursively: subsystems + units
    }

    view {SystemName}View {
        expose {SystemName}System;   // system def only, not expanded into subsystems
    }

    view {SubsystemName}View {
        expose {SubsystemName}Subsystem;      // one subsystem def only
    }
}
```

Views import (or qualify) every package whose members they expose, like any other file.

Type `SoftwareStructureView` by a standard view definition from the SysML v2 Views library
(`GeneralView`, `InterconnectionView`, ...; requires `sysml2tools` 0.3.0-beta.3+). The view
definition selects the diagram notation; `InterconnectionView` draws the system as nested
containment boxes, which is the clearest full-detail overview for a single system.

**Critical distinction** (do not confuse these — this cost significant trial-and-error to
discover): `expose <name>;` is what scopes a rendered diagram's content (the union of the
named element's containment subtree). The view definition (or a `render <kind>;` member)
selects a rendering *style* — it has **no effect on scope**. `expose` is only legal inside a named
`view Name { ... }` **usage**; it is a syntax/semantic error inside a `view def Name { ... }`
**definition**. `expose` targets must be `::`-qualified or locally-resolvable names, not
dotted member-access chains (`expose foo.bar;` is invalid — use `expose Bar;`).

**Exposing a package needs a membership wildcard.** `expose {Package};` exposes only the
package element itself, which renders as an empty package symbol. To show what a package
contains, expose its members: `expose {Package}::*;` (direct members) or
`expose {Package}::**;` (recursively). Exposing a `part def` by name (as `{SystemName}View`
and `{SubsystemName}View` do) is fine: its own parts are its structure.

**Naming convention** — one `View` suffix per rendered diagram, no `System`/`Subsystem`
suffix duplication:

- `SoftwareStructureView` - full detail: every system, subsystem, and unit in one diagram
- `{SystemName}View` - one per system, direct members only (not expanded into subsystems)
- `{SubsystemName}View` - one per subsystem (at any nesting depth), that subsystem's own
  members only

There is no dedicated `DependencyView`/`{SubsystemName}DependencyView` diagram convention —
`dependency` relationships are consumed via `sysml2tools query` (see Dependency Relationships
above), not rendered.

When a repository has multiple systems, type `SoftwareStructureView` by `GeneralView`,
expose each system's part def (and each subsystem's, by qualified name), and filter out
documentation so the overview stays readable — the general view draws one package frame per
system, while an interconnection view places all systems in a single row:

```sysml
package {RepoName}DesignViews {
    private import StandardViewDefinitions::*;

    view SoftwareStructureView : GeneralView {
        expose {SystemNameA}::{SystemNameA}System;
        expose {SystemNameA}_{SubsystemName}Subsystem::{SubsystemName}Subsystem;
        expose {SystemNameB}::{SystemNameB}System;
        filter not (@SysML::Documentation);
    }
}
```

Documentation (`doc`) is exposed with its owner and drawn as a `doc` compartment unless a
view filters it out; use `filter not (@SysML::Documentation);` wherever doc text would clutter
a diagram. The parentheses are required: the SysML v2 grammar does not accept the unparenthesized
`filter not @SysML::Documentation;`.

## Diagram Embedding

Render with quoted recursive globs for the shared metadata definition and the model tree, plus the views file
(`sysml2tools` 0.1.0-beta.5+ expands and recurses `**` internally, so no shell-side globbing
or explicit per-file listing is needed):

```pwsh
dotnet sysml2tools render `
  --output docs/design/generated --format svg `
  'docs/sysml2/common/**/*.sysml' `
  'docs/sysml2/model/**/*.sysml' `
  'docs/sysml2/views/design-views.sysml'
```

With multiple views declared and no `--view` flag, `sysml2tools` renders every declared view
in one invocation, one file per view, using each view's own name as the filename
(`{ViewName}.svg`) — no post-render rename step is needed.

Embed diagrams in `docs/design/` per this rule: every design doc for an item embeds the
diagram for the narrowest view that still shows that item's own immediate structure —

- `docs/design/introduction.md` — `SoftwareStructureView.svg` (the full-detail overview)
- `docs/design/{system-name}.md` and every unit doc for a unit directly under the system
  (no subsystem parent) — `{SystemName}View.svg`
- `docs/design/{system-name}/{subsystem-name}.md` and every unit doc nested under that
  subsystem (at any depth) — `{SubsystemName}View.svg`

Place the image directly under the file's top-level heading, before its first prose
subsection, e.g. `![{Name} Structure]({ViewName}.svg)`.

Document dependencies in prose instead of a diagram: each System/Subsystem design doc's
"Dependencies" section should be generated/maintained from `sysml2tools query dependencies
--element {QualifiedName}` output (Markdown format), not an embedded image.

## Build and Lint Integration

- `sysml2tools lint` belongs in `lint.ps1`'s compliance-tools section, **not** as a separate
  step in the CI design-document job — `lint.ps1` gates every later job (including document
  generation) transitively, so linting the model there is both earlier and non-duplicated.
  Pass a single quoted recursive glob, e.g. `dotnet sysml2tools lint 'docs/sysml2/**/*.sysml'`
  — `sysml2tools` (0.1.0-beta.5+) expands and recurses this itself, so no
  `Get-ChildItem`/shell-side globbing is needed.
- The CI design-document job renders the views (see the render command above) before running Pandoc, so generated SVGs
  exist before HTML generation. Rename/stable-filename workarounds are unnecessary since view names are stable by
  definition. Pass the common, model, and views globs as separate quoted arguments (e.g.
  `'docs/sysml2/common/**/*.sysml' 'docs/sysml2/model/**/*.sysml' 'docs/sysml2/views/design-views.sysml'`) — plain
  PowerShell (the default shell) is sufficient; no `shell: bash`/`shopt -s globstar` workaround is needed.
- Add `sysml2tools` to `.config/dotnet-tools.json` (`demaconsulting.sysml2tools.tool`) and to
  `.versionmark.yaml`'s captured tool list.

## Fallback

If the model is stale, doesn't yet cover an item, or a query returns nothing useful, fall
back to `grep`/`glob`/reading source directly. When work adds or changes a Unit/Subsystem,
update the corresponding `.sysml` model file in the same change — this mirrors the existing
requirement to keep `docs/design/*.md` and `docs/reqstream/*.yaml` companion artifacts in
sync.

## Quality Checks

- [ ] Every System/Subsystem/Unit in `docs/design/introduction.md` has a matching `part def`
  in its own file under `docs/sysml2/model/`, at the same folder depth as its companion
  design/reqstream/verification files, with a purpose `doc` comment
- [ ] Every Unit-level `part def` applies a `metadata info : SoftwareInfo { ... }` annotation
  with `sourceFiles`/`testFiles`/`designFiles`/`verificationFiles`/`requirementFiles` set where
  applicable (see Artifact-Location Metadata for documented exceptions); System/Subsystem
  `part def`s set `testFiles`/`designFiles`/`verificationFiles`/`requirementFiles` but no
  `sourceFiles`; the purpose statement appears only in the `doc` comment, never in `SoftwareInfo`
- [ ] `docs/sysml2/views/design-views.sysml` uses `view Name { expose ...; }` usages, not
  `view def` definitions
- [ ] Each `.sysml` file declares exactly one uniquely named package (never the same package
  in two files), named per Package Structure, and references other files through
  `private import` or qualified names (qualified where an import would form a cycle)
- [ ] View names follow the `SoftwareStructureView` / `{SystemName}View` /
  `{SubsystemName}View` convention (no dedicated `DependencyView`)
- [ ] Each Unit's real collaborators are captured as `dependency A to B;` statements, and each
  System/Subsystem design doc's "Dependencies" section reflects `sysml2tools query
  dependencies` output rather than an embedded diagram
- [ ] `sysml2tools lint` passes on all `.sysml` files
- [ ] Rendered diagrams are embedded in every design doc per the Diagram Embedding rule
- [ ] `sysml2tools` is present in `lint.ps1`, `.config/dotnet-tools.json`, and
  `.versionmark.yaml`
