## ReqStream

This document describes the integration and usage design for the `ReqStream` OTS software item.

### Purpose

ReqStream (`DemaConsulting.ReqStream`) is chosen to process requirements YAML files and TRX test
result files to generate a requirements report, justifications document, and traceability matrix.
It provides the requirements-to-test traceability evidence required for compliance.

### Features Used

- Requirements YAML processing via the `requirements.yaml` includes chain
- TRX test result consumption for traceability
- Requirements, justifications, and trace-matrix report generation
- Enforcement mode (`--enforce`) that fails the build when a requirement lacks test evidence
- Built-in self-validation suite (`--validate`)

### Integration Pattern

ReqStream is consumed as a dotnet tool restored from the tool manifest. In the CI pipeline it runs
with `--enforce` against all previously generated TRX evidence to fail the build if any requirement
lacks passing test evidence, then generates the requirements and trace-matrix documents that Pandoc
compiles. Tool qualification evidence is produced by `dotnet reqstream --validate --results
artifacts/reqstream-self-validation.trx`. Each invocation is a single process call with no
persistent state.
