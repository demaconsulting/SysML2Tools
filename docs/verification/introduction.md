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

This document describes how each software item in SysML2 Tools is verified —
local items (systems, subsystems, and units), and OTS software items. For each
item it names the test scenarios that verify its requirements. A reviewer should
be able to confirm coverage completeness without reading test code.

The following topics are out of scope:

- Verification documents are not produced for the test projects themselves — they are the
  means of verification, not subjects of it.
- Build pipeline CI configuration is excluded.
- The internal implementation of OTS software items is excluded; only integration and usage
  are verified.

## Folder Layout

The test folder structure mirrors the source subsystem breakdown across the Language and core
systems:

- **test/** — test projects
  - **DemaConsulting.SysML2Tools.Tests/** — unit tests for the Language and core systems
    - **Parser/** — Language system: parsing subsystem tests
    - **Semantic/** — Language system: semantic model subsystem tests
    - **Layout/** — core system: layout subsystem tests
    - **Rendering/** — core system: rendering subsystem tests
  - **DemaConsulting.SysML2Tools.Svg.Tests/** — SVG renderer tests
  - **DemaConsulting.SysML2Tools.Png.Tests/** — PNG renderer tests
  - **DemaConsulting.SysML2Tools.Tool.Tests/** — dotnet tool unit and integration tests

## Companion Artifact Structure

Local items have parallel artifacts in:

- Requirements: `docs/reqstream/{system-name}.yaml`, `docs/reqstream/{system-name}[/{subsystem-name}...]/{item}.yaml`
- Design: `docs/design/{system-name}.md`, `docs/design/{system-name}[/{subsystem-name}...]/{item}.md`
- Verification: `docs/verification/{system-name}.md`, `docs/verification/{system-name}[/{subsystem-name}...]/{item}.md`
- Source: `src/{SystemName}[/{SubsystemName}...]/{Item}.cs`
- Tests: `test/{SystemName}.Tests[/{SubsystemName}...]/{Item}Tests.cs`

OTS items have parallel artifacts in:

- Requirements: `docs/reqstream/ots/{ots-name}.yaml`
- Design: `docs/design/ots/{ots-name}.md`
- Verification: `docs/verification/ots/{ots-name}.md`

Review-sets: defined in `.reviewmark.yaml`

## References

- SysML2 Tools Software Design Document
- SysML2 Tools releases (<https://github.com/demaconsulting/SysML2Tools/releases>)
