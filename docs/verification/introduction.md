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

- **DemaConsulting.SysML2Tools**: system, subsystem, and unit verification.
- **DemaConsulting.SysML2Tools.Svg**: system verification (Phase 0 stub).
- **DemaConsulting.SysML2Tools.Png**: system verification (Phase 0 stub).
- **DemaConsulting.SysML2Tools.Tool**: system, subsystem, and unit verification.

**OTS items:**

- **ANTLR4**: integration and usage verification.
- **BuildMark**: integration and usage verification.
- **FileAssert**: integration and usage verification.
- **Pandoc**: integration and usage verification.
- **ReqStream**: integration and usage verification.
- **ReviewMark**: integration and usage verification.
- **SarifMark**: integration and usage verification.
- **SonarMark**: integration and usage verification.
- **VersionMark**: integration and usage verification.
- **WeasyPrint**: integration and usage verification.
- **xUnit**: integration and usage verification.

The following topics are out of scope:

- Verification documents are not produced for the test projects themselves — they are the
  means of verification, not subjects of it.
- Build pipeline CI configuration is excluded.
- The internal implementation of OTS software items is excluded; only integration and usage
  are verified.

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

- Requirements: `docs/reqstream/ots/{ots-name}.yaml`
- Design: `docs/design/ots/{ots-name}.md`
- Verification: `docs/verification/ots/{ots-name}.md`

Review-sets: defined in `.reviewmark.yaml`

## References

- SysML2 Tools Software Design Document
- SysML2 Tools releases (<https://github.com/demaconsulting/SysML2Tools/releases>)
