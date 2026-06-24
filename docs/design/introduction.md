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

This document covers the detailed design of the following software items:

**Local items:**

- **DemaConsulting.SysML2Tools** (System — core library, Phase 0 stub)
- **DemaConsulting.SysML2Tools.Svg** (System — SVG renderer, Phase 0 stub)
- **DemaConsulting.SysML2Tools.Png** (System — PNG renderer, Phase 0 stub)
- **DemaConsulting.SysML2Tools.Tool** (System — dotnet tool)
  - **Program** — entry point and execution orchestrator
  - **Cli** subsystem
    - **Context** — command-line argument parser and I/O owner
  - **SelfTest** subsystem
    - **Validation** — self-validation test runner
  - **Utilities** subsystem
    - **PathHelpers** — safe path combination utilities

**OTS items:**

- **BuildMark** — integration and usage design
- **FileAssert** — integration and usage design
- **Pandoc** — integration and usage design
- **ReqStream** — integration and usage design
- **ReviewMark** — integration and usage design
- **SarifMark** — integration and usage design
- **SonarMark** — integration and usage design
- **VersionMark** — integration and usage design
- **WeasyPrint** — integration and usage design
- **xUnit** — integration and usage design

The following topics are out of scope:

- Design documents are not produced for the test projects or build pipeline CI configuration
- The internal design of OTS software items is excluded; only integration and usage design is documented

## Software Structure

The following list shows how the SysML2 Tools software items are organized across the
system, subsystem, and unit levels:

- **DemaConsulting.SysML2Tools** (System) — core library: SysML v2 parsing, semantic
  model, layout algorithms, and `IRenderer` interface
  - TODO: subsystems and units to be defined in Phase 1+
- **DemaConsulting.SysML2Tools.Svg** (System) — SVG renderer: renders `LayoutTree` to
  SVG output with zero external dependencies
  - TODO: subsystems and units to be defined in Phase 4+
- **DemaConsulting.SysML2Tools.Png** (System) — PNG renderer: renders `LayoutTree` to
  PNG output using SkiaSharp
  - TODO: subsystems and units to be defined in Phase 4+
- **DemaConsulting.SysML2Tools.Tool** (System) — dotnet tool: thin CLI wrapper and
  orchestration
  - **Program** (Unit) — entry point and execution orchestrator
  - **Cli** (Subsystem) — command-line argument parsing and I/O
    - **Context** (Unit) — argument parser and I/O owner
  - **SelfTest** (Subsystem) — self-validation test runner
    - **Validation** (Unit) — self-validation test runner
  - **Utilities** (Subsystem) — shared utilities
    - **PathHelpers** (Unit) — safe path combination utilities

**OTS Dependencies:**

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
  - **DemaConsulting.SysML2Tools/** — core library (Phase 0: stub)
  - **DemaConsulting.SysML2Tools.Svg/** — SVG renderer (Phase 0: stub)
  - **DemaConsulting.SysML2Tools.Png/** — PNG renderer (Phase 0: stub)
  - **DemaConsulting.SysML2Tools.Tool/** — dotnet tool CLI wrapper
    - **Cli/** — command-line interface subsystem
    - **SelfTest/** — self-validation subsystem
    - **Utilities/** — shared utilities subsystem
- **docs/design/** — design documentation
  - **sysml2-tools-core/** — TODO: core library unit/subsystem design (Phase 1+)
  - **sysml2-tools-svg/** — TODO: SVG renderer unit/subsystem design (Phase 4+)
  - **sysml2-tools-png/** — TODO: PNG renderer unit/subsystem design (Phase 4+)
  - **sysml2-tools-tool/** — DemaConsulting.SysML2Tools.Tool unit/subsystem design
    - **cli/** — Cli subsystem design
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
| `DemaConsulting.SysML2Tools` | `sysml2-tools-core` |
| `DemaConsulting.SysML2Tools.Svg` | `sysml2-tools-svg` |
| `DemaConsulting.SysML2Tools.Png` | `sysml2-tools-png` |
| `DemaConsulting.SysML2Tools.Tool` | `sysml2-tools-tool` |

OTS items have integration/usage design documentation parallel to system folders:

- Requirements: `docs/reqstream/ots/{ots-name}.yaml`
- Design: `docs/design/ots/{ots-name}.md`
- Verification: `docs/verification/ots/{ots-name}.md`

Review-sets: defined in `.reviewmark.yaml`

## References

- SysML2 Tools User Guide
- SysML2 Tools Repository (<https://github.com/demaconsulting/SysML2Tools>)
