## ReviewMark Verification

This document provides the verification evidence for the ReviewMark OTS software item. Requirements
for this OTS item are defined in the ReviewMark OTS Software Requirements document.

### Required Functionality

DemaConsulting.ReviewMark reads the `.reviewmark.yaml` configuration and the review evidence store
to produce a review plan and review report documenting file review coverage and currency. It runs in
the same CI pipeline that produces the TRX test results, so a successful pipeline run is evidence
that ReviewMark executed without error.

### Verification Approach

ReviewMark is verified by two complementary layers of evidence. First, the CI pipeline runs
`reviewmark --validate --results artifacts/reviewmark-self-validation.trx`, which exercises
ReviewMark's built-in self-validation suite against test review configurations and records
results for ReqStream.

Second, the pipeline invokes ReviewMark to generate
`docs/code_review_plan/generated/plan.md` and `docs/code_review_report/generated/report.md`.
Pandoc converts each to HTML; if either file were absent or malformed, Pandoc would fail.
WeasyPrint renders both to PDF and FileAssert asserts their content
(`WeasyPrint_ReviewPlanPdf`, `WeasyPrint_ReviewReportPdf`). A CI build failure at any step is
evidence that ReviewMark did not produce the required review documents.

### Test Scenarios

#### ReviewMark_ReviewPlanGeneration

**Scenario**: ReviewMark self-validation uses `--definition` and `--plan` to generate a review plan
from a test configuration.

**Expected**: Exits 0 and produces a non-empty review plan markdown file.

**Requirement coverage**: `Template-OTS-ReviewMark`.

#### ReviewMark_ReviewReportGeneration

**Scenario**: ReviewMark self-validation uses `--definition` and `--report` to generate a review
report from a test configuration and evidence store.

**Expected**: Exits 0 and produces a non-empty review report.

**Requirement coverage**: `Template-OTS-ReviewMark`.

#### ReviewMark_Enforce

**Scenario**: ReviewMark self-validation uses `--enforce` against a configuration with review
issues.

**Expected**: Exits with a non-zero exit code when review issues are present.

**Requirement coverage**: `Template-OTS-ReviewMark-Enforce`.

#### ReviewMark_Elaborate

**Scenario**: ReviewMark self-validation uses `--elaborate` to print a Markdown elaboration of a
named review set.

**Expected**: Exits 0 and prints the review-set ID, fingerprint, and file list.

**Requirement coverage**: `Template-OTS-ReviewMark-Elaborate`.

#### ReviewMark_Lint

**Scenario**: ReviewMark self-validation uses `--lint` to validate a definition file and report
issues.

**Expected**: Correctly reports structural and semantic issues found in the test definition.

**Requirement coverage**: `Template-OTS-ReviewMark-Lint`.
