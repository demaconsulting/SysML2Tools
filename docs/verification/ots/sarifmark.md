## SarifMark Verification

This document provides the verification evidence for the SarifMark OTS software item. Requirements
for this OTS item are defined in the SarifMark OTS Software Requirements document.

### Required Functionality

DemaConsulting.SarifMark reads the SARIF output produced by CodeQL code scanning and renders it as
a human-readable markdown document included in the release artifacts. It runs in the same CI
pipeline that produces the TRX test results, so a successful pipeline run is evidence that SarifMark
executed without error.

### Verification Approach

SarifMark is verified by two complementary layers of evidence. First, the CI pipeline runs
`sarifmark --validate --results artifacts/sarifmark-self-validation.trx`, which exercises
SarifMark's built-in self-validation suite against mock SARIF data and records results for
ReqStream.

Second, the pipeline invokes SarifMark with the CodeQL SARIF output to generate
`docs/code_quality/generated/codeql-quality.md`. Pandoc converts this file (along with the
SonarMark output) to HTML; if the file were absent or malformed, Pandoc would fail. WeasyPrint
renders the result to PDF and FileAssert asserts the PDF contains expected content
(`WeasyPrint_CodeQualityPdf`). A CI build failure at any step is evidence that SarifMark did
not produce the required output.

### Test Scenarios

#### SarifMark_SarifReading

**Scenario**: SarifMark is invoked with a CodeQL SARIF results file as input.

**Expected**: Exits 0 and successfully reads the SARIF content.

**Requirement coverage**: `Template-OTS-SarifMark`.

#### SarifMark_MarkdownReportGeneration

**Scenario**: SarifMark renders the SARIF input as a markdown report included in the release
artifacts.

**Expected**: Exits 0 and produces a non-empty markdown report.

**Requirement coverage**: `Template-OTS-SarifMark`.

#### SarifMark_Enforcement

**Scenario**: SarifMark self-validation processes a SARIF file containing known issues in
enforcement mode.

**Expected**: Returns a non-zero exit code when issues are found.

**Requirement coverage**: `Template-OTS-SarifMark-Enforcement`.
