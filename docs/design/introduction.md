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

## Software Structure

The following list shows how the SysML2 Tools software items are organized across the
system, subsystem, and unit levels:

- **DemaConsulting.SysML2Tools.Language** (System) — language library: SysML v2 parsing engine,
  AST node types, semantic analysis, and AST serialization/deserialization
  - **Parser** (Subsystem) — SysML v2 parsing engine
    - **WorkspaceParser** (Unit) — public API: parses source strings; exposes internal `ParseSourceToCst`
    - **Internal** (Subsystem) — internal implementation details
      - **SysmlDiagnosticListener** (Unit) — collects ANTLR4 syntax errors as SysmlDiagnostic records
  - **Semantic** (Subsystem) — SysML/KerML semantic model: symbol table, reference resolution, supertype walking
    - **WorkspaceLoader** (Unit) — public API: loads SysML/KerML files into a semantic workspace with optional seed
    - **AstSerializer** (Unit) — serializes SymbolTable + diagnostics to UTF-8 JSON bytes
    - **AstDeserializer** (Unit) — deserializes bytes back to SymbolTable + diagnostics
    - **Internal** (Subsystem) — internal semantic implementation
      - **SysmlNode** (Unit) — public AST node hierarchy: six types with JSON polymorphism
      - **AstBuilder** (Unit) — builds AST from ANTLR4 CST with qualified names and supertype lists
      - **SymbolTable** (Unit) — registry mapping qualified names to declaration nodes
      - **ReferenceResolver** (Unit) — resolves supertype references; detects circular imports
      - **SupertypeWalker** (Unit) — walks specialization chains; detects cyclic specialization
      - **SerializedStdlib** (Unit) — DTO for stdlib binary serialization
      - **AstSerializerContext** (Unit) — source-generated JSON context for AOT-safe serialization
- **DemaConsulting.SysML2Tools.Stdlib** (System) — stdlib library: pre-compiled SysML v2 standard
  library binary embedded as a managed resource
  - **StdlibProvider** (Unit) — lazy-cached GetSymbolTable() deserialized from embedded stdlib.bin
- **StdlibGen** (Build-time tool) — console tool that parses stdlib source files and writes stdlib.bin
  - **Program** (Unit) — entry point: parses stdlib, runs resolution, serializes to stdlib.bin
- **DemaConsulting.SysML2Tools** (System) — core library: layout, rendering interfaces, and DiagramRenderer
  - **Layout** (Subsystem) — LayoutTree intermediate representation (node types covering all SysML
    diagram elements), reusable layout engines, and per-view layout strategies
    - **Engine** (Subsystem) — reusable, model-independent geometric layout engines
      - **ChannelRouter** (Unit) — orthogonal connector routing with obstacle avoidance and clearance
      - **ContainmentPacker** (Unit) — packs sized boxes within a bounded container region
      - **InterconnectionLayoutEngine** (Unit) — façade that assembles and runs the layered
        pipeline for the interconnection view, preserving its public placement API
      - **Layered** (Subsystem) — reusable, ELK-style layered layout pipeline of single-responsibility stages
        - **LayeredGraph** (Unit) — mutable shared state threaded through the pipeline stages
        - **LayeredLayoutPipeline** (Unit) — builds and runs an ordered sequence of layout stages
        - **CycleBreaker** (Unit) — reverses back edges to produce an acyclic edge set
        - **LayerAssigner** (Unit) — assigns nodes to layers by longest-path ranking
        - **LongEdgeSplitter** (Unit) — inserts dummy nodes so every edge spans one layer
        - **CrossingMinimizer** (Unit) — orders nodes within layers to reduce edge crossings
        - **BrandesKopfPlacer** (Unit) — assigns node coordinates with the Brandes-Köpf algorithm
        - **PortDistributor** (Unit) — distributes edge ports along node faces
        - **OrthogonalRouter** (Unit) — routes edges as orthogonal segments between layers
        - **LongEdgeJoiner** (Unit) — rejoins split sub-edges into original edge polylines
        - **AxisTransform** (Unit) — maps pipeline coordinates to the requested layout direction
        - **ComponentPacker** (Unit) — lays out each connected component independently and packs them without overlap
    - **Internal** (Subsystem) — per-view layout strategies
      - **GeneralViewLayoutStrategy** (Unit) — general view: package-grouped definitions placed by the
        layered pipeline with orthogonal specialization and membership edges
      - **InterconnectionViewLayoutStrategy** (Unit) — internal structure: nested parts, ports, connectors
      - **StateTransitionViewLayoutStrategy** (Unit) — state machine: states and guarded transitions
        placed top-to-bottom by the layered pipeline (DOWN direction) with orthogonal transitions
      - **ActionFlowViewLayoutStrategy** (Unit) — action flow with start/done markers, placed
        top-to-bottom by the layered pipeline (DOWN direction) with orthogonal successions
      - **SequenceViewLayoutStrategy** (Unit) — lifelines and ordered messages
      - **GridViewLayoutStrategy** (Unit) — specialization/relationship matrix
      - **BrowserViewLayoutStrategy** (Unit) — indented membership tree
      - **LayoutWarnings** (Unit) — builder for layout diagnostic warning messages
    - **ConnectorLabelPlacer** (Unit) — collision-aware placement of connector midpoint labels
  - **Rendering** (Subsystem) — rendering pipeline: the `IRenderer`/`ILayoutStrategy` interfaces,
    `Theme`, `RenderOptions`, `RenderOutput`, the `DiagramRenderer` orchestrator, and the
    `StdlibFilter` helper that excludes standard-library elements from diagrams
    - **Internal** (Subsystem) — internal rendering implementation
      - **DiagramTypeRouter** (Unit) — selects a layout strategy from a view's resolved kind
- **DemaConsulting.SysML2Tools.Svg** (System) — SVG renderer: renders `LayoutTree` to
  SVG output with zero external dependencies
  - **SvgRenderer** (Unit) — translates a `LayoutTree` to a self-contained SVG 1.1 document
- **DemaConsulting.SysML2Tools.Png** (System) — PNG renderer: renders `LayoutTree` to
  PNG output using SkiaSharp
  - **PngRenderer** (Unit) — rasterizes a `LayoutTree` to a PNG image using SkiaSharp
- **DemaConsulting.SysML2Tools.Tool** (System) — dotnet tool: thin CLI wrapper and
  orchestration
  - **Program** (Unit) — entry point and execution orchestrator
  - **Cli** (Subsystem) — command-line argument parsing and I/O
    - **Context** (Unit) — argument parser and I/O owner
  - **Lint** (Subsystem) — lint command implementation
    - **LintCommand** (Unit) — resolves glob patterns, invokes WorkspaceLoader with stdlib seed, reports diagnostics
  - **Render** (Subsystem) — render command implementation
    - **RenderCommand** (Unit) — loads workspace with stdlib seed, selects renderer, writes diagram output files
  - **SelfTest** (Subsystem) — self-validation test runner
    - **Validation** (Unit) — self-validation test runner
  - **Utilities** (Subsystem) — shared utilities
    - **PathHelpers** (Unit) — safe path combination utilities

**OTS Dependencies:**

- ANTLR4 (OTS) — ANTLR4 runtime (Antlr4.Runtime.Standard)
- BuildMark (OTS) — build-notes documentation tool
- FileAssert (OTS) — document assertion tool
- Pandoc (OTS) — Markdown-to-HTML conversion tool
- ReqStream (OTS) — requirements traceability tool
- ReviewMark (OTS) — file review enforcement tool
- SarifMark (OTS) — SARIF report conversion tool
- SonarMark (OTS) — SonarCloud quality report tool
- VersionMark (OTS) — tool-version documentation tool
- WeasyPrint (OTS) — HTML-to-PDF conversion tool
- xUnit (OTS) — unit-testing framework

Each local unit is described in detail in its own chapter within this document.

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
      - **Internal/** — internal implementation (SysmlNode, AstBuilder, SymbolTable,
        ReferenceResolver, SupertypeWalker, SerializedStdlib, AstSerializerContext)
  - **DemaConsulting.SysML2Tools.Stdlib/** — stdlib library
    - **Stdlib/** — SysML v2 standard library source files (EPL-2.0; see Stdlib/README.md)
  - **DemaConsulting.SysML2Tools.Core/** — core library
    - **Layout/** — LayoutTree intermediate representation
      - **Internal/** — internal layout implementation (GeneralViewLayoutStrategy)
    - **Rendering/** — rendering interfaces and theme (Phase 3+)
  - **DemaConsulting.SysML2Tools.Svg/** — SVG renderer
  - **DemaConsulting.SysML2Tools.Png/** — PNG renderer
  - **DemaConsulting.SysML2Tools.Tool/** — dotnet tool CLI wrapper
    - **Cli/** — command-line interface subsystem
    - **Lint/** — lint command subsystem
    - **SelfTest/** — self-validation subsystem
    - **Utilities/** — shared utilities subsystem
  - **Tools/StdlibGen/** — build-time stdlib pre-compiler tool
- **docs/design/** — design documentation
  - **sysml2-tools-language.md** — language library design
  - **sysml2-tools-stdlib.md** — stdlib library design
  - **sysml2-tools-core/** — core library unit/subsystem design
  - **sysml2-tools-svg.md** — SVG renderer design
  - **sysml2-tools-png.md** — PNG renderer design
  - **sysml2-tools-tool/** — DemaConsulting.SysML2Tools.Tool unit/subsystem design
    - **cli/** — Cli subsystem design
    - **lint/** — Lint subsystem design
    - **render/** — Render subsystem design (render.md)
    - **self-test/** — SelfTest subsystem design
    - **utilities/** — Utilities subsystem design

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
- Verification: `docs/verification/{system-name}.md`, `docs/verification/{system-name}[/{subsystem-name}...]/{item}.md`
- Source: `src/{SystemName}[/{SubsystemName}...]/{Item}.cs`
- Tests: `test/{SystemName}.Tests[/{SubsystemName}...]/{Item}Tests.cs`

The four top-level systems map to these kebab-case folder names:

| NuGet Package | kebab-case system folder |
| --- | --- |
| `DemaConsulting.SysML2Tools.Language` | `sysml2-tools-language` |
| `DemaConsulting.SysML2Tools.Stdlib` | `sysml2-tools-stdlib` |
| `DemaConsulting.SysML2Tools` | `sysml2-tools-core` |
| `DemaConsulting.SysML2Tools.Svg` | `sysml2-tools-svg` |
| `DemaConsulting.SysML2Tools.Png` | `sysml2-tools-png` |
| `DemaConsulting.SysML2Tools.Tool` | `sysml2-tools-tool` |

OTS items have integration/usage design documentation parallel to system folders:

- Requirements: `docs/reqstream/ots/{ots-name}.yaml`
- Design: `docs/design/ots/{ots-name}.md`
- Verification: `docs/verification/ots/{ots-name}.md`

Review-sets: defined in `.reviewmark.yaml`

## Technology Stack

| Concern | Choice | License |
| --- | --- | --- |
| Language / runtime | C# / .NET 8+ | MIT |
| Parser generator | ANTLR4 (`Antlr4.Runtime.Standard`) | BSD-3-Clause |
| SysML v2 grammar | `antlr/grammars-v4` (official OMG KEBNF) | MIT |
| PNG rendering | SkiaSharp | MIT |
| Embedded font | Noto Sans | SIL OFL 1.1 |
| Test results output | `DemaConsulting.TestResults` | — |
| Unit testing | xUnit v3 | Apache 2.0 |

No ImageSharp dependency. SkiaSharp is chosen over ImageSharp to avoid the Six Labors
Split License, which would impose licensing obligations on library consumers embedding
`DemaConsulting.SysML2Tools.Png` in commercial products.

## Architectural Decisions

**Multi-package from day one.** Separating the core library from renderer packages
allows library consumers to take a dependency on parsing and layout without pulling
in native graphics binaries. The `IRenderer` interface is the extension point.

**Multi-file workspace.** The OMG stdlib is pre-loaded from embedded resources before
any user files are parsed. Single-file input is the degenerate case of a multi-file
workspace; there is no single-file mode.

**SkiaSharp over ImageSharp.** SkiaSharp is MIT-licensed. ImageSharp v2+ uses the
Six Labors Split License which imposes obligations on commercial library consumers.
SkiaSharp's native asset requirement is transparent for tool consumers (handled by
NuGet at publish time) and is a known, documented constraint for library consumers.

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
to handle custom viewpoints that specialize stdlib viewpoints.

**Diagnostic model mirrors ReviewMark.** `SysmlDiagnostic` mirrors ReviewMark's
`LintIssue` in structure and philosophy: file/line/col location, severity enum,
human-readable message. The `lint` command makes this output useful for AI-assisted
model authoring loops.

**Layout engines are independent and reusable.** Non-trivial layout algorithms
(`ContainmentPacker`, `ChannelRouter`, `InterconnectionLayoutEngine`, and the
reusable `LayeredLayoutPipeline` and its stages) live in `Layout/Engine/` with no
dependency on the SysML semantic model. Each engine accepts plain geometric input
and returns computed geometry, making them independently testable and reusable across
multiple view strategies. See `ROADMAP.md` for the phased introduction of each engine.

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

**SkiaSharp native assets for library consumers.** Consumers referencing
`DemaConsulting.SysML2Tools.Png` must ensure the appropriate
`SkiaSharp.NativeAssets.*` package is included in their publish output. This must
be documented clearly in the package README.

**Noto Sans SIL OFL attribution.** OFL requires the copyright notice and license
text to be included in distributions. Must appear in NuGet package notices and the
tool's `--licenses` output (or equivalent).

## References

- SysML2 Tools User Guide
- SysML2 Tools Repository (<https://github.com/demaconsulting/SysML2Tools>)
- SysML2 Tools Rendering Roadmap (`ROADMAP.md`)
