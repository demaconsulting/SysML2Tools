## ReviewMark

This document describes the integration and usage design for the `ReviewMark` OTS software item.

### Purpose

ReviewMark (`DemaConsulting.ReviewMark`) is chosen to enforce file review currency based on the
`.reviewmark.yaml` configuration. It provides the formal review tracking and enforcement evidence
required for compliance, ensuring every file in every review-set has current review evidence.

### Features Used

- Review plan and report generation (`--plan` / `--report`)
- Configuration validation (`--lint`)
- Review-set elaboration for agent consumption (`--elaborate`)
- Review currency enforcement (`--enforce`)
- Built-in self-validation suite (`--validate`)

### Integration Pattern

ReviewMark is consumed as a dotnet tool restored from the tool manifest. It is used in several
contexts:

- `--plan` / `--report` generate the review plan and report documents that Pandoc compiles
- `--lint` validates `.reviewmark.yaml` and is run in `lint.ps1`
- `--elaborate` expands review-set file lists for use by the formal-review agent
- `--enforce` enforces review currency and is run in the CI build, making stale evidence a
  build-breaking condition

Tool qualification evidence is produced by `dotnet reviewmark --validate --results
artifacts/reviewmark-self-validation.trx`. Each invocation is a single process call with no
persistent state.
