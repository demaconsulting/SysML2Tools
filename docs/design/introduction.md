# Introduction

This document provides the detailed design for the SysML2 Tools, a .NET command-line
application demonstrating best practices for DEMA Consulting DotNet Tools.

## Purpose

The purpose of this document is to describe the internal design of each software unit that
comprises the SysML2 Tools. It captures data models, algorithms, key methods, and
inter-unit interactions at a level of detail sufficient for formal code review, compliance
verification, and future maintenance. The document does not restate requirements; it explains
how they are realized.

## Scope

This document covers the detailed design of the following software items:

**Local items:**

- **SysML2Tools** — the system as a whole
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

The following tree shows how the SysML2 Tools software items are organized across the
system, subsystem, and unit levels:

```text
SysML2Tools (System)
├── Program (Unit)
├── Cli (Subsystem)
│   └── Context (Unit)
├── SelfTest (Subsystem)
│   └── Validation (Unit)
└── Utilities (Subsystem)
    └── PathHelpers (Unit)
```

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

The source code folder structure mirrors the top-level subsystem breakdown above, giving
reviewers an explicit navigation aid from design to code:

```text
src/
└── DemaConsulting.SysML2Tools/  — main application source
    ├── Cli/                            — command-line interface subsystem
    ├── SelfTest/                       — self-validation subsystem
    └── Utilities/                      — shared utilities subsystem
```

## Document Conventions

Throughout this document:

- Class names, method names, property names, and file names appear in `monospace` font.
- The word **shall** denotes a design constraint that the implementation must satisfy.
- Section headings within each unit chapter follow a consistent structure: overview, data model,
  methods/algorithms, and interactions with other units.
- Text tables are used in preference to diagrams, which may not render in all PDF viewers.

## Companion Artifact Structure

Local software items have corresponding artifacts in parallel directory trees:

- Requirements: `docs/reqstream/{system}/.../{item}.yaml` (kebab-case)
- Design docs: `docs/design/{system}/.../{item}.md` (kebab-case)
- Verification design: `docs/verification/{system}/.../{item}.md` (kebab-case)
- Source code: `src/{System}/.../{Item}.cs` (PascalCase for C#)
- Tests: `test/{System}.Tests/.../{Item}Tests.cs` (PascalCase for C#)

OTS items have integration/usage design documentation parallel to system folders:

- Requirements: `docs/reqstream/ots/{ots-name}.yaml`
- Design: `docs/design/ots/{ots-name}.md`
- Verification: `docs/verification/ots/{ots-name}.md`

Review-sets: defined in `.reviewmark.yaml`

## References

- SysML2 Tools User Guide
- SysML2 Tools Repository (<https://github.com/demaconsulting/SysML2Tools>)
