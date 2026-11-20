# Introduction

This document provides the detailed design for the SysML2 Tools, a free, open-source .NET CLI
tool and library that parses SysML v2 textual model files and renders them as professional nested
block diagrams suitable for architecture documentation, CI/CD pipelines, and AI-assisted modeling
workflows.

## Purpose

The purpose of this document is to describe the internal design of each software unit that
comprises the SysML2 Tools. It captures data models, algorithms, key methods, and
inter-unit interactions at a level of detail sufficient for formal code review, compliance
verification, and future maintenance. The document does not restate requirements; it explains
how they are realized.

## Scope

This document defines the design for each software item in SysML2 Tools —
full architectural and detailed design for local items (systems, subsystems,
and units), and integration/usage design for OTS software items. A reviewer
should be able to understand how each item satisfies its requirements without
reading source code.

The following topics are out of scope:

- Design documents are not produced for the test projects or build pipeline CI configuration.
- The internal design of OTS software items is excluded; only integration and usage design is documented.
- **`src/Tools/StdlibGen`** is a standalone pre-compiler tool that parses the stdlib source
  files and writes `stdlib.json.gz` (invoked as a plain, sequential step by `build.ps1`, not
  part of the MSBuild graph). Like the build pipeline, it is not part of the delivered
  software and is therefore excluded from the software-items requirements, design, verification,
  and SysML2 model tree. The absence of requirements, design, verification, and model artifacts
  for it is intentional, not a decomposition gap.

The OTS software items whose integration design is covered are ANTLR4, BuildMark,
DemaConsulting.Rendering, FileAssert, Pandoc, ReqStream, ReviewMark, SarifMark, SonarMark,
SysML2Tools, VersionMark, WeasyPrint, and xUnit.

## Software Structure

The SysML2 Tools software structure is modeled in SysML v2 under `docs/sysml2/`. The model is the
authoritative, machine-queryable source for every software item: its purpose, its dependencies, and the
location of its source, test, design, verification, and requirements artifacts. It has one SysML
package per system (`SysML2ToolsLanguage`, `SysML2ToolsStdlib`, `SysML2ToolsCore`, and
`SysML2ToolsTool`) plus the shared `OtsDependencies` package, and is organized as follows:

- `docs/sysml2/common/` — the shared `SoftwareInfo` metadata definition
- `docs/sysml2/model/` — one file per system, subsystem, and unit, mirroring the design tree, plus
  `ots.sysml` for the OTS items
- `docs/sysml2/views/` — the named views rendered into this document

The diagram below is rendered from the model and shows the four systems, their subsystems, and the OTS
items. Every system and subsystem chapter that follows opens with the diagram of its own system or
subsystem, and every unit chapter with the diagram of its parent. To learn what a software item is,
why it exists, and what it depends on, query the model (for example `sysml2tools query describe` or
`sysml2tools query dependencies` over `'docs/sysml2/**/*.sysml'`) before reading the source code.

![Software Structure](SoftwareStructureView.svg)

The four systems are:

- **`DemaConsulting.SysML2Tools.Language`** — language library: SysML v2 parsing engine, AST node
  types, semantic analysis, and AST serialization and deserialization
- **`DemaConsulting.SysML2Tools.Stdlib`** — stdlib library: the pre-compiled SysML v2 standard
  library JSON embedded as a gzip-compressed managed resource
- **`DemaConsulting.SysML2Tools.Core`** — core library: layout strategies, rendering
  orchestration, file collection, and the model-analysis query API
- **`DemaConsulting.SysML2Tools.Tool`** — dotnet tool: thin command-line wrapper and
  orchestration

`src/Tools/StdlibGen` is deliberately not part of the model (see *Scope*).

The diagram below complements the structure model by showing how the systems relate at run
time: the Tool's commands call into the Core library, which builds on the Language library
seeded with the pre-compiled Stdlib, to turn `.sysml` model files into diagnostics, diagrams,
query results, and model JSON.

![SysML2Tools architecture: model files flow from the Tool commands through the Core library
and the Language library, which is seeded by the Stdlib, to produce diagnostics, diagrams,
query results, and model JSON](architecture.svg)

## Folder Layout

The source code folder structure mirrors the top-level system breakdown above, giving
reviewers an explicit navigation aid from design to code:

- **src/** — source projects
  - **DemaConsulting.SysML2Tools.Language/** — language library
    - **Grammar/** — ANTLR4 grammar files (hand-maintained; see Grammar/README.md)
    - **Parser/** — SysML v2 parsing subsystem
      - **Antlr/** — ANTLR4-generated C# (committed; not hand-written)
      - **Internal/** — internal implementation (SysmlDiagnosticListener)
    - **Semantic/** — semantic model subsystem
      - **Model/** — public semantic model types (SysmlNode, SysmlMetadataNode, AstBuilder,
        SymbolTable, ReferenceResolver, NameResolver, SupertypeWalker, SysmlEdge, SemanticIndex,
        SysmlAnnotation, SerializedStdlib, AstSerializerContext)
    - **Filtering/** — standalone view-filter expression AST, parser, and evaluator (namespace
      `DemaConsulting.SysML2Tools.Filtering`)
  - **DemaConsulting.SysML2Tools.Stdlib/** — stdlib library
    - **Stdlib/** — SysML v2 standard library source files (EPL-2.0; see Stdlib/README.md)
  - **DemaConsulting.SysML2Tools.Core/** — core library
    - **Layout/** — layout strategies mapping the model to the off-the-shelf `LayoutTree`
      - **Internal/** — per-view layout strategies and the `LayeredPlacement` helper
    - **Rendering/** — SysML-coupled rendering pipeline (`ILayoutStrategy`, `DiagramRenderer`)
      - **Internal/** — internal rendering implementation (`DiagramTypeRouter`,
        `DynamicViewSynthesizer`, `StdlibFilter`)
    - **Io/** — shared file glob pattern resolution used by the Tool project's
      lint/render/query commands (`GlobFileCollector`)
    - **Query/** — public, reusable model-analysis API (`QueryVerb`, `QueryOptions`,
      `QueryArgumentParser`, `QueryEngine`, `QueryResult`, `QueryResultRenderer`,
      `QueryResultSerializerContext`, `QueryResultExporter`)
    - **Utilities/** — shared helper used by the Query subsystem (`QualifiedNameShortener`)
  - **DemaConsulting.SysML2Tools.Tool/** — dotnet tool CLI wrapper
    - **Cli/** — command-line interface subsystem
    - **Lint/** — lint command subsystem
    - **Render/** — render command subsystem
    - **Help/** — help command subsystem
    - **Query/** — query command subsystem
    - **Export/** — export command subsystem
    - **SelfTest/** — self-validation subsystem
    - **Utilities/** — shared utilities subsystem
  - **Tools/StdlibGen/** — build-time stdlib pre-compiler tool
- **docs/design/** — design documentation
  - **sysml2-tools-language.md** — language library design
  - **sysml2-tools-stdlib.md** — stdlib library design
  - **sysml2-tools-core/** — core library unit/subsystem design
  - **sysml2-tools-tool/** — DemaConsulting.SysML2Tools.Tool unit/subsystem design
    - **cli/** — Cli subsystem design
    - **lint/** — Lint subsystem design
    - **render/** — Render subsystem design (render.md)
    - **help.md** — Help subsystem design
    - **query.md** — Query subsystem design
    - **export.md** — Export subsystem design
    - **self-test/** — SelfTest subsystem design
    - **utilities/** — Utilities subsystem design
- **docs/sysml2/** — SysML2 architecture model of the software structure
  - **common/** — shared `SoftwareInfo` metadata definition
  - **model/** — one `.sysml` file per system, subsystem, and unit (plus `ots.sysml`),
    mirroring the design tree
  - **views/** — named views rendered into the design document

## Document Conventions

Throughout this document:

- Class names, method names, property names, and file names appear in `monospace` font.
- The word **shall** denotes a design constraint that the implementation must satisfy.
- Section headings within each unit chapter follow a consistent structure: overview, data model,
  methods/algorithms, and interactions with other units.
- Text tables are used in preference to diagrams, which may not render in all PDF viewers.

## Companion Artifact Structure

Local software items have corresponding artifacts in parallel directory trees:

- Requirements: `docs/reqstream/{system-name}.yaml`, `docs/reqstream/{system-name}[/{subsystem-name}...]/{item}.yaml`
- Design: `docs/design/{system-name}.md`, `docs/design/{system-name}[/{subsystem-name}...]/{item}.md`
- Model: `docs/sysml2/model/{system-name}.sysml`, `docs/sysml2/model/{system-name}[/{subsystem-name}...]/{item}.sysml`,
  and `docs/sysml2/model/ots.sysml` for the OTS items
- Verification: `docs/verification/{system-name}.md`, `docs/verification/{system-name}[/{subsystem-name}...]/{item}.md`
- Source: `src/{SystemName}[/{SubsystemName}...]/{Item}.cs`
- Tests: `test/{SystemName}.Tests[/{SubsystemName}...]/{Item}Tests.cs`

The four top-level systems map to these kebab-case folder names:

| NuGet Package | kebab-case system folder |
| --- | --- |
| `DemaConsulting.SysML2Tools.Language` | `sysml2-tools-language` |
| `DemaConsulting.SysML2Tools.Stdlib` | `sysml2-tools-stdlib` |
| `DemaConsulting.SysML2Tools.Core` | `sysml2-tools-core` |
| `DemaConsulting.SysML2Tools.Tool` | `sysml2-tools-tool` |

OTS items have integration/usage design documentation parallel to system folders:

- Requirements: `docs/reqstream/ots/{ots-name}.yaml`
- Design: `docs/design/ots/{ots-name}.md`
- Verification: `docs/verification/ots/{ots-name}.md`

Review-sets: defined in `.reviewmark.yaml`

## Technology Stack

| Concern | Choice | License |
| --- | --- | --- |
| Language / runtime | C# / .NET 8, 9, 10, and 11 (libraries); .NET 10 (tool) | MIT |
| Parser generator | ANTLR4 (`Antlr4.Runtime.Standard`) | BSD-3-Clause |
| SysML v2 grammar | `antlr/grammars-v4` (official OMG KEBNF) | MIT |
| PNG rendering | CanvasNet | MIT |
| Embedded font | Noto Sans | SIL OFL 1.1 |
| Test results output | `DemaConsulting.TestResults` | — |
| Unit testing | xUnit v3 | Apache 2.0 |

No ImageSharp dependency. The off-the-shelf `DemaConsulting.Rendering.CanvasNet` PNG renderer uses
CanvasNet, a pure-managed rendering library, chosen over ImageSharp to avoid the Six Labors Split
License, which would impose licensing obligations on library consumers embedding the renderer in
commercial products.

## Architectural Decisions

**Multi-package from day one.** Separating the core library from renderer packages
allows library consumers to take a dependency on parsing and layout without pulling
in native graphics binaries. The `IRenderer` interface is the extension point.

**Multi-file workspace.** The OMG stdlib is pre-loaded from embedded resources before
any user files are parsed. Single-file input is the degenerate case of a multi-file
workspace; there is no single-file mode.

**CanvasNet over ImageSharp.** CanvasNet is MIT-licensed. ImageSharp v2+ uses the
Six Labors Split License which imposes obligations on commercial library consumers.
CanvasNet is a pure-managed library with no native asset requirement, so this
constraint no longer applies to tool or library consumers.

**Embedded Noto Sans font.** Ensures pixel-identical PNG output across all platforms.
Noto Sans is licensed SIL OFL 1.1 which explicitly permits embedding in software.
Default themes use the embedded font; user-overridden themes may use system or
external fonts at the cost of the pixel-identity guarantee.

**`IRenderer` is low-level and pure — no filesystem access.** It receives a
`LayoutTree` and writes to a caller-supplied `Stream`. Passing a `FileStream`
writes directly to disk with no intermediate buffer; passing a `MemoryStream`
keeps output in memory for testing or in-process use; passing
`Console.OpenStandardOutput()` supports stdout piping. Multi-view orchestration
(`DiagramRenderer`) lives in the core library and calls `IRenderer` once per view —
renderer packages have no concept of workspaces or view iteration.

**DiagramTypeRouter uses resolved qualified names.** The router dispatches on the
stdlib-resolved qualified name of a view's viewpoint type (e.g.,
`SystemsModelingLibrary::Views::GeneralView`), not the raw token. User aliases and
local imports therefore do not break dispatch. The router walks the supertype chain
to handle custom viewpoints that specialize stdlib viewpoints. Dispatch first checks
the view's declared `render` target for an exact match against `asTreeDiagram` or
`asInterconnectionDiagram`, which takes precedence over the qualified-name/supertype
heuristic entirely.

**Diagnostic model mirrors ReviewMark.** `SysmlDiagnostic` mirrors ReviewMark's
`LintIssue` in structure and philosophy: file/line/col location, severity enum,
human-readable message. The `lint` command makes this output useful for AI-assisted
model authoring loops.

**Geometric layout is off-the-shelf.** Non-trivial geometric layout algorithms (containment
packing, orthogonal connector routing, and the layered layout algorithm) are provided by the
off-the-shelf `DemaConsulting.Rendering.Layout` package with no dependency on the SysML semantic
model. The `LayeredPlacement` helper adapts that algorithm for the view strategies, accepting
plain geometric input and returning computed geometry.

**Theme record is a compile-time constant in v1.** Loadable theme files are deferred
to v2. The Theme record data structure is defined in v1 so that v2 loadable themes
are additive and non-breaking.

**`--auto` flag.** When a workspace has no view definitions, `--auto` renders the
general view of the top-level `part def` silently. Without `--auto`, the same
auto-render occurs but a warning is emitted advising the user to define an explicit
view. This keeps v1 immediately useful for unstructured models while encouraging
good authoring practice.

**Self-test via `--validate` flag.** Uses the same `Program.Run` pattern as
ReviewMark: invokes the full CLI pipeline against embedded test models with a test
context, then asserts expected outputs. Results are written as TRX/JUnit via
`DemaConsulting.TestResults`, consistent with the shared DEMA CI contract. This
tests the integrated tool, not just unit-level components.

**SARIF deferred.** The `SysmlDiagnostic` list is structurally compatible with SARIF.
SARIF output can be added as a formatting option on the existing infrastructure
without any breaking changes.

**Pure-managed rendering — no native assets required.** Consumers referencing the
off-the-shelf `DemaConsulting.Rendering.CanvasNet` PNG renderer have no native asset
package to add: CanvasNet is a pure-managed .NET library with no P/Invoke dependency.

**Noto Sans SIL OFL attribution.** OFL requires the copyright notice and license
text to be included in distributions. Must appear in NuGet package notices and the
tool's `--licenses` output (or equivalent).

## References

- SysML2 Tools User Guide
- SysML2 Tools Repository (<https://github.com/demaconsulting/SysML2Tools>)
- SysML2 Tools Rendering Roadmap (`ROADMAP.md`)
