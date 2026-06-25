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

- **DemaConsulting.SysML2Tools** (System — core library)
  - **Parser** (Subsystem) — SysML v2 parsing engine
    - **WorkspaceParser** (Unit) — public parsing API
    - **Internal** (Subsystem) — internal implementation
      - **SysmlDiagnosticListener** (Unit) — ANTLR4 error listener
      - **StdlibLoader** (Unit) — embedded stdlib loader
- **DemaConsulting.SysML2Tools.Svg** (System — SVG renderer, Phase 0 stub)
- **DemaConsulting.SysML2Tools.Png** (System — PNG renderer, Phase 0 stub)
- **DemaConsulting.SysML2Tools.Tool** (System — dotnet tool)
  - **Program** (Unit) — entry point and execution orchestrator
  - **Cli** (Subsystem) — command-line argument parsing and I/O
    - **Context** (Unit) — argument parser and I/O owner
  - **Lint** (Subsystem) — lint command implementation
    - **LintCommand** (Unit) — lint subcommand handler
  - **SelfTest** (Subsystem) — self-validation test runner
    - **Validation** (Unit) — self-validation test runner
  - **Utilities** (Subsystem) — shared utilities
    - **PathHelpers** (Unit) — safe path combination utilities

**OTS items:**

- **ANTLR4** — ANTLR4 runtime (Antlr4.Runtime.Standard)
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

## Folder Layout

The test folder structure mirrors the source subsystem breakdown:

- **test/** — test projects
  - **DemaConsulting.SysML2Tools.Tests/** — core library unit tests
    - **Parser/** — WorkspaceParser and related tests
  - **DemaConsulting.SysML2Tools.Svg.Tests/** — TODO: SVG renderer tests (Phase 4+)
  - **DemaConsulting.SysML2Tools.Png.Tests/** — TODO: PNG renderer tests (Phase 4+)
  - **DemaConsulting.SysML2Tools.Tool.Tests/** — dotnet tool unit and integration tests

## Companion Artifact Structure

Local items have parallel artifacts in:

- Requirements: `docs/reqstream/{system-name}.yaml`, `docs/reqstream/{system-name}[/{subsystem-name}...]/{item}.yaml`
- Design: `docs/design/{system-name}.md`, `docs/design/{system-name}[/{subsystem-name}...]/{item}.md`
- Verification: `docs/verification/{system-name}.md`, `docs/verification/{system-name}[/{subsystem-name}...]/{item}.md`
- Source: `src/{SystemName}[/{SubsystemName}...]/{Item}.cs`
- Tests: `test/{SystemName}.Tests[/{SubsystemName}...]/{Item}Tests.cs`

OTS items have parallel artifacts in:

- Requirements: `docs/reqstream/ots/{ots-name}.yaml` (kebab-case)
- Verification: `docs/verification/ots/{ots-name}.md` (kebab-case)

Review-sets: defined in `.reviewmark.yaml`

## References

- SysML2 Tools Software Design Document
- SysML2 Tools releases (<https://github.com/demaconsulting/SysML2Tools/releases>)
