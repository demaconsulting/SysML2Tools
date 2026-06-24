## VersionMark Verification

This document provides the verification evidence for the `VersionMark` OTS software item.

### Required Functionality

DemaConsulting.VersionMark reads version metadata for each dotnet tool used in the pipeline and
writes a versions markdown document included in the release artifacts. It runs in the same CI
pipeline that produces the TRX test results, so a successful pipeline run is evidence that
VersionMark executed without error.

### Verification Approach

VersionMark is verified by two complementary layers of evidence. First, the CI pipeline runs
`versionmark --validate --results *.trx` in each build job, exercising VersionMark's built-in
self-validation suite and recording results for ReqStream.

Second, each CI job runs `versionmark --capture` to collect tool-version JSON files, and the
build-docs job runs `versionmark --publish` to produce
`docs/build_notes/generated/versions.md`. This file is included in the Build Notes document
compiled by Pandoc. If VersionMark failed to produce the versions document, the Build Notes
compilation would be incomplete. WeasyPrint renders the result to PDF and FileAssert asserts
its content (`WeasyPrint_BuildNotesPdf`). A CI build failure at any step is evidence that
VersionMark did not execute correctly.

### Test Scenarios

#### VersionMark_CapturesVersions

**Scenario**: VersionMark reads version metadata for each dotnet tool defined in the pipeline.

**Expected**: Exits 0 and captures version data for every tool.

**Requirement coverage**: `Template-OTS-VersionMark`.

#### VersionMark_GeneratesMarkdownReport

**Scenario**: VersionMark writes a versions markdown document to the release artifacts.

**Expected**: Exits 0 and produces a non-empty versions markdown file.

**Requirement coverage**: `Template-OTS-VersionMark`.

#### VersionMark_LintPassesForValidConfig

**Scenario**: VersionMark self-validation exercises lint mode against a valid `.versionmark.yaml`
config.

**Expected**: Exits 0 and reports no errors.

**Requirement coverage**: `Template-OTS-VersionMark`.

#### VersionMark_LintReportsErrorsForInvalidConfig

**Scenario**: VersionMark self-validation exercises lint mode against a malformed config with
deliberate errors.

**Expected**: Correctly identifies and reports the configuration errors.

**Requirement coverage**: `Template-OTS-VersionMark`.
