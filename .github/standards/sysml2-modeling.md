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
│   ├── {system-name}.sysml            # package {SystemName} (facade); system-level part def only (no subsystems/units inline)
│   ├── {system-name}/
│   │   ├── {subsystem-name}.sysml     # package {SystemName}_{SubsystemName}Subsystem (facade); nests further for sub-subsystems
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

SysML v2 has no package merging: each file is its own root namespace, and the top-level names
of all loaded files share one flat global scope (KerML 1.0 §7.2.5.3, §10.2–10.3; SysML 2.0
§7.5.5). Folders do not create namespaces. Two files that declare the same top-level name
leave the resolution unspecified (KerML §8.2.3.5.4); the OMG Pilot Implementation reports
"Duplicate of other owned member name", and references into the "other half" do not resolve.
A model that only loads because a tool is lenient is not portable, so top-level package names
must be unique **repository-wide** and a package is never declared (or "reopened") in more
than one file.

The model uses one top-level package per file, with **facade packages** for the system and
each subsystem. A facade is a package whose `public import` re-exports the packages of the
items it owns, so a consumer can write `{SystemName}::{AnyItem}` (or `import {SystemName}::*;`)
without knowing which file declares the item. `public import` makes the imported members
visible members of the importing package (KerML §8.2.3.5.3; SysML §7.5.3). A facade must be a
package, because imports owned directly by a file's root namespace must be private (KerML
§8.3.2.4.2, `validateImportTopLevelVisibility`).

| Item | File | Package | Imports |
| --- | --- | --- | --- |
| System | `{system}.sysml` | `{SystemName}` | `public import` of direct items |
| Subsystem | `{system}/{subsystem}.sysml` | `{SystemName}_{SubsystemName}Subsystem` | `public import` of owned items |
| Unit | `.../{unit}.sysml` | `{SystemName}_{UnitName}` | `private import` of what it references |
| OTS | `ots.sysml` | `OtsDependencies` | none |
| Shared Packages | `shared.sysml` | `SharedPackages` | none |
| Views | `views/design-views.sysml` | `{RepoName}DesignViews` | `private import` of what it exposes |

In this table `{system}`, `{subsystem}`, and `{unit}` are the kebab-case names from Repository
Structure above. Consumers use the `{SystemName}` package. `ots.sysml`, `shared.sysml`, and
the views file are one small file each, and the views package is one per repository, not per
system. A unit imports exactly what it references, no more. A system or subsystem file also
uses `private import` for anything else it references (for example `OtsDependencies`). Every
file that applies `SoftwareInfo` also has `private import SoftwareItemMetadata::*;`.

Rules:

- Name each package from its place in the hierarchy, joined with an underscore, as in the table
  (a nested sub-subsystem is not prefixed with its parent subsystem). The system prefix keeps
  names unique across systems.
- The short forms in the table are the default. When one would collide (two `Storage`
  subsystems in different branches, or two units named `Cache` in different subsystems),
  qualify it with the owning subsystem path, for example
  `{SystemName}_{SubsystemName}_{UnitName}` (`Acme_Ingest_Cache` and `Acme_Query_Cache`).
- `public import` goes **downward only**, following ownership (system to subsystem to unit).
  The chain of re-exports is what makes `{SystemName}::{AnyItem}` resolve.
- An **upward or sideways** reference (a unit using another unit, a subsystem, or the system)
  uses a `private import` of the specific item's package, **never** of a facade. A facade
  re-exports the item that imports it, so importing it back forms an import cycle. Cycles are
  legal (KerML §8.2.3.5.1) and SysML2Tools 0.3.0-beta.5 and later no longer warns about
  them, but the OMG Pilot can no longer load the files one at a time because every file in the
  cycle needs another one loaded first. A reference to a facade's own part def (the system or a subsystem)
  from below therefore uses the qualified name, with no import.
- Where two items genuinely depend on each other, import in one direction only and use the
  qualified name in the other, e.g. `dependency {UnitName} to {SystemName}_{OtherUnit}::{OtherUnit};`.
  The same applies to a one-off reference into another system. Qualified references do not
  create import edges.
- Never name a package after a standard library package. A package named `Views` shadows the
  library's `Views`, and `GeneralView` then fails with `Unresolved reference`. This is why the
  views package is `{RepoName}DesignViews`.
- A small cohesive namespace (a handful of definitions that change together) may be a single
  file with a single package, instead of one file per definition.

Rationale: this is the pattern the SysML v2 libraries and published models use. The OMG
standard libraries re-export their parts through facades (`ISQ` re-exports the 12 `ISQ*`
packages and `SI` re-exports `ISQ`); Sensmetry's smart-home-hub (distinguishable, prefixed names
such as `DETECT_*`, enforced by the Syside rule `global-namespace-distinguishability`), the
Airbus Apollo-11 model, and DE4SDV all use one top-level package per file, unique and often
prefixed names, `private import` for dependencies, and facades with `public import`.

### Migrating split packages

Older models declared the same package (for example `package {SystemName}`) in several files.
To migrate:

1. Rename each file's package to its item name from the table above, so every package is
   declared once.
2. Generate each file's `private import` lines from
   `sysml2tools query dependencies --element {QualifiedName}`, run with a tool version that
   still loads the old model (0.3.0-beta.3), so the dependencies reflect the original model.
3. Make the system and subsystem files facades: turn the imports of their direct children
   (owned subsystems, sub-subsystems, and units) into `public import`, and keep `private
   import` only for references to items they do not own, so no package is imported twice.
4. Existing `{SystemName}::X` references keep working through the facades; update only
   references that named a package that no longer exists.
5. Run `sysml2tools lint --strict-unresolved` with the pinned tool version.

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
- `sysml2tools query uses`/`used-by`/`dependencies --element {QualifiedName}` remain the
  **primary** way to consume dependencies: they return every `dependency` edge (in addition to
  structural typing/containment references) directly from the model — see the
  `sysml2tools-query` skill.
- Focused dependency views are also allowed in the design docs (requires `sysml2tools`
  0.3.0-beta.4+, which draws part trees and `«depend»` edge labels, and scales oversized PNGs
  instead of clipping them). A dependency edge renders only when both ends are exposed, so each
  view must follow these rules:
  - Expose one subsystem's units plus the suppliers named in their `dependency` statements, so
    the `«depend»` edges render.
  - Keep each view to at most about 12–15 dependency edges; split a large subsystem into themed
    views.
  - Omit hub suppliers that most units depend on from unit-level views. State the omission in
    each view's `doc`; hub usage may be shown once in a small shared view.
  - No view should need PNG downscaling: beta.4 scales PNGs over 8192 px and prints a warning,
    which is a signal that the view is too big.

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
    private import {SystemName}::*;   // the system facade re-exports every subsystem and unit package

    view SoftwareStructureView : InterconnectionView {
        expose {SystemName}::{SystemName}System;  // system part def: system frame with its direct subsystem usages
    }

    view {SystemName}View {
        expose {SystemName}System;   // system def only, not expanded into subsystems
    }

    view {SubsystemName}View {
        expose {SubsystemName}Subsystem;      // one subsystem def only
    }
}
```

Views import (or qualify) every package whose members they expose, like any other file. With
the facade pattern, a private import of the system facade reaches every item beneath it; a
view of a system that is not owned by that facade imports that system's own facade as well.
Never name the views package after a library package (see Package Structure).

Type `SoftwareStructureView` by a standard view definition from the SysML v2 Views library
(`GeneralView`, `InterconnectionView`, ...; requires `sysml2tools` 0.3.0-beta.4+). The view
definition selects the diagram notation; `InterconnectionView` draws the system as nested
containment boxes, which is the clearest system-level overview.

**Critical distinction** (do not confuse these — this cost significant trial-and-error to
discover): `expose <name>;` is what scopes a rendered diagram's content (the union of the
named element's containment subtree). The view definition (or a `render <kind>;` member)
selects a rendering *style* — it has **no effect on scope**. `expose` is only legal inside a named
`view Name { ... }` **usage**; it is a syntax/semantic error inside a `view def Name { ... }`
**definition**. `expose` targets must be `::`-qualified or locally-resolvable names, not
dotted member-access chains (`expose foo.bar;` is invalid — use `expose Bar;`). A `render <kind>;`
member such as `render asInterconnectionDiagram;` needs `private import Views::*;` in the views package,
or `lint --strict-unresolved` reports it as an unresolved reference.

**Exposing a package needs a membership wildcard.** `expose {Package};` exposes only the
package element itself, which renders as an empty package symbol. To show what a package
contains, expose its members: `expose {Package}::*;` (direct members) or
`expose {Package}::**;` (recursively). Exposing a `part def` by name (as `{SystemName}View`
and `{SubsystemName}View` do) is fine: its own parts are its structure.

**Naming convention** — one `View` suffix per rendered diagram, no `System`/`Subsystem`
suffix duplication:

- `SoftwareStructureView` - system-level overview: every system as a frame with its direct
  subsystem usages (unit detail lives in the per-system and per-subsystem views)
- `{SystemName}View` - one per system, direct members only (not expanded into subsystems)
- `{SubsystemName}View` - one per subsystem (at any nesting depth), that subsystem's own
  members only
- `{SubsystemName}{Theme}View` - optional focused dependency views for a large subsystem (see
  Dependency Relationships), each exposing the units of one theme plus their suppliers
- `OtsView` - optional, the OTS items from `ots.sysml` kept apart from the system overview

When a repository has multiple systems, type `SoftwareStructureView` by
`InterconnectionView` and expose each system's part def. Each system renders as a frame with
its subsystem part usages nested inside (beta.4 draws a part def's interior). Keep OTS
dependencies out of the overview in a separate small `OtsView`, and exclude metadata defs such
as `SoftwareInfo` from overviews by not exposing the package that declares them. Use
`GeneralView` for part-tree, composition-style views and for the dependency views above;
filter out documentation so each diagram stays readable:

```sysml
package {RepoName}DesignViews {
    private import StandardViewDefinitions::*;

    view SoftwareStructureView : InterconnectionView {
        expose {SystemNameA}::{SystemNameA}System;
        expose {SystemNameB}::{SystemNameB}System;
        filter not (@SysML::Documentation);
    }

    view OtsView : GeneralView {
        expose OtsDependencies::*;
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

- `docs/design/introduction.md` — `SoftwareStructureView.svg` (the system-level overview)
- `docs/design/{system-name}.md` and every unit doc for a unit directly under the system
  (no subsystem parent) — `{SystemName}View.svg`
- `docs/design/{system-name}/{subsystem-name}.md` and every unit doc nested under that
  subsystem (at any depth) — `{SubsystemName}View.svg`

Place the image directly under the file's top-level heading, before its first prose
subsection, e.g. `![{Name} Structure]({ViewName}.svg)`.

Each System/Subsystem design doc's "Dependencies" section should be generated/maintained from
`sysml2tools query dependencies --element {QualifiedName}` output (Markdown format); it may
also embed that subsystem's focused dependency view(s) alongside the query output.

## Build and Lint Integration

- `sysml2tools lint` belongs in `lint.ps1`'s compliance-tools section, **not** as a separate
  step in the CI design-document job — `lint.ps1` gates every later job (including document
  generation) transitively, so linting the model there is both earlier and non-duplicated.
  Pass a single quoted recursive glob and `--strict-unresolved` (0.3.0-beta.4+, which fails the lint on unresolved
  references), e.g. `dotnet sysml2tools lint --strict-unresolved 'docs/sysml2/**/*.sysml'`
  — `sysml2tools` (0.1.0-beta.5+) expands and recurses this itself, so no
  `Get-ChildItem`/shell-side globbing is needed.
- The CI design-document job renders the views (see the render command above) before running Pandoc, so generated SVGs
  exist before HTML generation. Rename/stable-filename workarounds are unnecessary since view names are stable by
  definition. Pass the common, model, and views globs as separate quoted arguments (e.g.
  `'docs/sysml2/common/**/*.sysml' 'docs/sysml2/model/**/*.sysml' 'docs/sysml2/views/design-views.sysml'`) — plain
  PowerShell (the default shell) is sufficient; no `shell: bash`/`shopt -s globstar` workaround is needed.
- Add `sysml2tools` to `.config/dotnet-tools.json` (`demaconsulting.sysml2tools.tool`, minimum
  version 0.3.0-beta.6, which resolves facade paths in `query`) and to `.versionmark.yaml`'s captured tool list.

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
- [ ] Each `.sysml` file declares exactly one top-level package, unique repository-wide
  (never the same package in two files, never named after a standard library package), named
  per Package Structure
- [ ] The system and subsystem packages are facades with `public import` of the packages they
  own (downward only); units use `private import` of exactly what they reference; upward and
  sideways references import the specific item's package, never a facade, so no import cycle
  runs through a facade (lint no longer reports cycles, so check this by review)
- [ ] View names follow the `SoftwareStructureView` / `{SystemName}View` /
  `{SubsystemName}View` convention (plus optional `{SubsystemName}{Theme}View` dependency
  views and `OtsView`)
- [ ] A multi-system `SoftwareStructureView` is an `InterconnectionView` exposing the system
  part defs, with OTS items in a separate `OtsView` and no metadata defs (`SoftwareInfo`)
- [ ] Each Unit's real collaborators are captured as `dependency A to B;` statements, and each
  System/Subsystem design doc's "Dependencies" section reflects `sysml2tools query
  dependencies` output
- [ ] Each focused dependency view has at most about 12–15 dependency edges, omits hub
  suppliers (stated in its `doc`), and renders without PNG downscaling warnings
- [ ] `sysml2tools lint` passes on all `.sysml` files
- [ ] Rendered diagrams are embedded in every design doc per the Diagram Embedding rule
- [ ] `sysml2tools` is present in `lint.ps1`, `.config/dotnet-tools.json`, and
  `.versionmark.yaml`
