## VersionMark

This document describes the integration and usage design for the `VersionMark` OTS software item.

### Purpose

VersionMark (`DemaConsulting.VersionMark`) is chosen to read version metadata for each dotnet tool
used in the pipeline and write a versions markdown document. It provides the tool-version record
required for build reproducibility and compliance evidence.

### Features Used

- Per-tool version metadata capture (`--capture`)
- Versions markdown document generation (`--publish`)
- Configuration validation (`--lint`)
- Built-in self-validation suite (`--validate`)

### Integration Pattern

VersionMark is consumed as a dotnet tool restored from the tool manifest. Throughout the CI
pipeline it captures the versions of each tool used in a job, then publishes a consolidated
versions markdown document included in the build notes. `--lint` validates the configuration. Tool
qualification evidence is produced by `dotnet versionmark --validate --results
artifacts/versionmark-self-validation.trx`. Each invocation is a single process call with no
persistent state.
