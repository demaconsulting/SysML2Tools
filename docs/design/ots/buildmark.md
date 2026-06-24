## BuildMark

This document describes the integration and usage design for the `BuildMark` OTS software item.

### Purpose

BuildMark (`DemaConsulting.BuildMark`) is chosen to capture GitHub Actions workflow run metadata —
Git history, issues, and pull requests — and render it as a markdown build-notes document. It
provides automated, repeatable build-notes generation for the release artifacts so that the
project does not maintain release notes by hand.

### Features Used

- Workflow run metadata capture (Git tags, commit history, issues, pull requests)
- Markdown build-notes document rendering with section routing rules
- Built-in self-validation suite (`--validate`)

### Integration Pattern

BuildMark is consumed as a dotnet tool restored from the tool manifest. In the CI documentation
build it is invoked with live GitHub Actions metadata via `dotnet buildmark ... --output` to
produce `docs/build_notes/generated/build_notes.md`, which Pandoc then converts to HTML. Tool
qualification evidence is produced by `dotnet buildmark --validate --results
artifacts/buildmark-self-validation.trx`, whose results are consumed by ReqStream. No
initialization or disposal beyond a single process invocation is required.
