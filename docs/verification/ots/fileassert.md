## FileAssert Verification

This document provides the verification evidence for the FileAssert OTS software item. Requirements
for this OTS item are defined in the FileAssert OTS Software Requirements document.

### Required Functionality

DemaConsulting.FileAssert validates HTML and PDF documents produced during the build, asserting that
each document exists, has a non-trivial size, is structurally valid, and contains expected content.
It provides OTS evidence for Pandoc and WeasyPrint and independently confirms file assertion is
functioning. Self-validation proves the tool itself is operational before ReqStream consumes the
results.

### Verification Approach

FileAssert is verified by two complementary layers of evidence. First, the CI pipeline runs
`fileassert --validate --results artifacts/fileassert-self-validation.trx` after all documents
have been generated, exercising FileAssert's built-in self-validation suite and recording
functional test results for ReqStream.

Second, FileAssert is used throughout the pipeline to validate every generated document before
ReqStream runs — Build Notes, Code Quality, Review Plan, Review Report, Design, Verification,
and User Guide. If FileAssert were non-functional, these validation steps would fail or produce
incorrect results, causing `reqstream --enforce` to report missing test coverage and fail the
build. A passing CI build therefore constitutes transitive evidence that FileAssert correctly
asserted document content at each stage of the pipeline.

### Test Scenarios

#### FileAssert_Results

**Scenario**: FileAssert self-validation exercises the `--results` option, generating TRX test
results containing both passing and failing outcomes.

**Expected**: Writes a TRX results file with correctly classified pass and fail entries.

**Requirement coverage**: `Template-OTS-FileAssert`.

#### FileAssert_File

**Scenario**: FileAssert self-validation exercises a test configuration using a glob pattern to
assert file existence.

**Expected**: Passes when the specified file is present.

**Requirement coverage**: `Template-OTS-FileAssert`.

#### FileAssert_Text

**Scenario**: FileAssert self-validation exercises a test configuration using a `contains` assertion
to verify file text content.

**Expected**: Passes when the specified content is present.

**Requirement coverage**: `Template-OTS-FileAssert`.

#### FileAssert_Html

**Scenario**: FileAssert self-validation exercises a test configuration using an XPath query to
assert HTML document structure.

**Expected**: Passes when the specified HTML nodes are present.

**Requirement coverage**: `Template-OTS-FileAssert`.

#### FileAssert_Pdf

**Scenario**: FileAssert self-validation exercises a test configuration asserting PDF metadata
fields and body text content.

**Expected**: Passes when the PDF contains the expected metadata and text.

**Requirement coverage**: `Template-OTS-FileAssert`.
