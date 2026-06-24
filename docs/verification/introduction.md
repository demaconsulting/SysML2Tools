# Introduction

This document provides the verification design for the SysML2 Tools, a .NET command-line
application demonstrating best practices for DEMA Consulting DotNet Tools.

## Purpose

The purpose of this document is to describe how each requirement for the SysML2 Tools is
verified. For every software item — system, subsystem, and unit — this document names the
verification approach, identifies the test scenarios (including boundary conditions and error
paths), describes what is mocked or stubbed, and maps each requirement to at least one named
test scenario. The document does not restate design; it explains how the design is proven correct.

## Scope

This document covers the verification design for the following software items:

**Local items:**

- **SysML2Tools** — system, subsystem, and unit verification:
  - **Program** — entry point and execution orchestrator
  - **Cli** subsystem
    - **Context** — argument parser and I/O owner
  - **SelfTest** subsystem
    - **Validation** — self-validation test runner
  - **Utilities** subsystem
    - **PathHelpers** — safe path combination utilities

**OTS items:**

- **BuildMark** — build-notes documentation tool
- **FileAssert** — document assertion tool
- **Pandoc** — Markdown-to-HTML conversion tool
- **ReqStream** — requirements traceability tool
- **ReviewMark** — file review enforcement tool
- **SarifMark** — SARIF report conversion tool
- **SonarMark** — SonarCloud quality report tool
- **VersionMark** — tool-version documentation tool
- **WeasyPrint** — HTML-to-PDF conversion tool
- **xUnit** — unit-testing framework

The following topics are out of scope:

- Verification documents are not produced for the test projects themselves — they are the
  means of verification, not subjects of it
- Build pipeline CI configuration is excluded
- The internal implementation of OTS software items is excluded; only integration and usage
  are verified

## Software Structure

The following tree shows the software items covered by this document:

```text
SysML2Tools (System)
├── Program (Unit)
├── Cli (Subsystem)
│   └── Context (Unit)
├── SelfTest (Subsystem)
│   └── Validation (Unit)
└── Utilities (Subsystem)
    └── PathHelpers (Unit)

OTS Items
├── BuildMark
├── FileAssert
├── Pandoc
├── ReqStream
├── ReviewMark
├── SarifMark
├── SonarMark
├── VersionMark
├── WeasyPrint
└── xUnit
```

## Folder Layout

The test folder structure mirrors the source subsystem breakdown:

```text
test/
└── DemaConsulting.SysML2Tools.Tests/  — unit and integration tests
```

## Companion Artifact Structure

In-house items have corresponding artifacts in parallel directory trees:

- Requirements: `docs/reqstream/{system}/.../{item}.yaml` (kebab-case)
- Design docs: `docs/design/{system}/.../{item}.md` (kebab-case)
- Verification design: `docs/verification/{system}/.../{item}.md` (kebab-case)
- Source code: `src/{System}/.../{Item}.cs` (PascalCase for C#)
- Tests: `test/{System}.Tests/.../{Item}Tests.cs` (PascalCase for C#)

OTS items have parallel artifacts in:

- Requirements: `docs/reqstream/ots/{ots-name}.yaml` (kebab-case)
- Verification: `docs/verification/ots/{ots-name}.md` (kebab-case)

Review-sets: defined in `.reviewmark.yaml`

## References

- SysML2 Tools Software Design Document
- SysML2 Tools releases (<https://github.com/demaconsulting/SysML2Tools/releases>)
