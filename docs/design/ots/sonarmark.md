## SonarMark

This document describes the integration and usage design for the `SonarMark` OTS software item.

### Purpose

SonarMark (`DemaConsulting.SonarMark`) is chosen to retrieve quality-gate and metrics data from
SonarCloud and render a markdown code-quality report. It provides automated inclusion of SonarCloud
quality results in the project's documentation artifacts.

### Features Used

- SonarCloud quality-gate retrieval
- SonarCloud issues and hot-spots retrieval
- Markdown code-quality report rendering
- Built-in self-validation suite (`--validate`)

### Integration Pattern

SonarMark is consumed as a dotnet tool restored from the tool manifest. In the CI pipeline it
retrieves quality-gate and metrics data from SonarCloud and renders the markdown code-quality
report that Pandoc compiles. Tool qualification evidence is produced by `dotnet sonarmark
--validate --results artifacts/sonarmark-self-validation.trx`. Each invocation is a single process
call with no persistent state.
