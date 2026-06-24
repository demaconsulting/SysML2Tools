## FileAssert

This document describes the integration and usage design for the `FileAssert` OTS software item.

### Purpose

FileAssert (`DemaConsulting.FileAssert`) is chosen to assert that generated documents exist, have
non-trivial size, and contain the expected content. It provides the pipeline output assertions that
turn document generation into verifiable evidence for OTS tools that do not have their own dotnet
self-validation.

### Features Used

- File existence and size assertions
- Text content assertions
- HTML structure assertions
- PDF content assertions
- Built-in self-validation suite (`--validate`)

### Integration Pattern

FileAssert is consumed as a dotnet tool restored from the tool manifest. After each Pandoc and
WeasyPrint document-generation step in the CI pipeline, `dotnet fileassert --results <file>.trx`
asserts the generated HTML and PDF documents and records results for ReqStream. Tool qualification
evidence is produced by `dotnet fileassert --validate --results
artifacts/fileassert-self-validation.trx`. Each invocation is a single process call with no
persistent state.
