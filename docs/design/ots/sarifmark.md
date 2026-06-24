## SarifMark

This document describes the integration and usage design for the `SarifMark` OTS software item.

### Purpose

SarifMark (`DemaConsulting.SarifMark`) is chosen to read CodeQL SARIF output and render a
human-readable markdown code-quality report. It provides automated conversion of static-analysis
findings into a reviewable document and can fail the build when issues are detected.

### Features Used

- SARIF report reading
- Markdown code-quality report rendering
- Enforcement mode (`--enforce`) that fails the build when SARIF issues are detected
- Built-in self-validation suite (`--validate`)

### Integration Pattern

SarifMark is consumed as a dotnet tool restored from the tool manifest. In the CI pipeline it reads
the CodeQL SARIF output and renders the markdown code-quality report that Pandoc compiles, running
with `--enforce` so that detected issues break the build. Tool qualification evidence is produced
by `dotnet sarifmark --validate --results artifacts/sarifmark-self-validation.trx`. Each invocation
is a single process call with no persistent state.
