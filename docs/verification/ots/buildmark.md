## BuildMark Verification

This document provides the verification evidence for the `BuildMark` OTS software item.

### Required Functionality

DemaConsulting.BuildMark queries the GitHub API to capture workflow run details and renders them as
a markdown build-notes document included in the release artifacts. It runs as part of the same CI
pipeline that produces the TRX test results, so a successful pipeline run is evidence that BuildMark
executed without error.

### Verification Approach

BuildMark is verified by two complementary layers of evidence. First, the CI pipeline runs
`buildmark --validate --results artifacts/buildmark-self-validation.trx`, which exercises
BuildMark's built-in self-validation suite against mock data and records results for ReqStream.

Second, the pipeline invokes BuildMark with live GitHub Actions metadata to generate
`docs/build_notes/generated/build_notes.md`. Pandoc then converts this file to HTML; if the
file were absent or malformed, Pandoc would fail. WeasyPrint renders the HTML to a PDF/A
artifact, and FileAssert asserts the PDF exists, has content, and contains expected text
(`WeasyPrint_BuildNotesPdf`). A CI build failure at any step in this chain is evidence that
BuildMark did not produce the required output.

### Test Scenarios

#### BuildMark_MarkdownReportGeneration

**Scenario**: A CI pipeline run triggers BuildMark with live GitHub Actions metadata.

**Expected**: BuildMark exits without error and produces a non-empty markdown build-notes document
in the release artifacts.

**Requirement coverage**: `Template-OTS-BuildMark`.

#### BuildMark_GitIntegration

**Scenario**: BuildMark self-validation reads version tags and commits from a mock Git history.

**Expected**: Exits 0 and correctly reads version tags and commit history.

**Requirement coverage**: `Template-OTS-BuildMark`.

#### BuildMark_IssueTracking

**Scenario**: BuildMark self-validation processes mock GitHub issues and pull requests.

**Expected**: Exits 0 and correctly fetches and processes mock issues and pull requests.

**Requirement coverage**: `Template-OTS-BuildMark`.

#### BuildMark_KnownIssuesReporting

**Scenario**: BuildMark self-validation includes open bugs in the generated report when requested.

**Expected**: Exits 0 and correctly includes known issues.

**Requirement coverage**: `Template-OTS-BuildMark`.

#### BuildMark_RulesRouting

**Scenario**: BuildMark self-validation assigns mock items to report sections based on label and
type rules.

**Expected**: Exits 0 and correctly routes items to the expected sections.

**Requirement coverage**: `Template-OTS-BuildMark`.
