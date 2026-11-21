## SysML2Tools

This document describes the integration and usage design for the `SysML2Tools` OTS software item.

### Purpose

SysML2Tools (`DemaConsulting.SysML2Tools.Tool`) lints, renders, and queries the repository's SysML2
architecture model under `docs/sysml2/`. It is the reference implementation for the SysML v2 subset
used by the Continuous Compliance methodology, and it integrates directly with the same
`dotnet tool restore` and CI pipeline used by the other pipeline tools. The pinned, published release
is a different artifact from the in-repository build that this project develops and tests, so the
model is always checked by a stable, independent copy of the tool.

### Features Used

- **Lint** (`lint --strict-unresolved 'docs/sysml2/**/*.sysml'`) — reports syntax errors and, with
  `--strict-unresolved`, unresolved references as errors, and other problems (such as duplicate root
  packages) as warnings, without producing output files. Warnings do not change its exit code, so
  `lint.ps1` also captures the output and fails on any `warning:` line (see Integration Pattern).
- **Render** (`render --strict-unresolved --format svg`) — renders every view declared in
  `docs/sysml2/views/design-views.sysml` to an SVG file named after the view (for example
  `SoftwareStructureView.svg`). A general view draws an exposed part definition's part usages as
  diamond-linked part nodes and each `dependency` as a dashed `«depend»` edge, which the views are
  scoped to keep readable at document width.
- **Query** (`query list`, `query describe`, `query dependencies`) — answers structure questions
  directly from the model, so agents and reviewers can navigate the software items without reading
  source code.
- **Self-validation** (`--validate --results`) — runs a lint self-test and a render self-test
  against built-in model fixtures and writes a TRX result.

### Integration Pattern

SysML2Tools is installed as a .NET local tool via `.config/dotnet-tools.json` and invoked as
`dotnet sysml2tools` from two places: `lint.ps1` runs
`dotnet sysml2tools lint --strict-unresolved 'docs/sysml2/**/*.sysml'` as an early-detection lint gate
(failing on a non-zero exit code, which includes unresolved references, and on any remaining `warning:`
line in the output, so the model must lint with zero errors and zero warnings), and the
design-document section of `.github/workflows/build.yaml` runs the following command

```pwsh
dotnet sysml2tools render --strict-unresolved `
  --output docs/design/generated --format svg `
  'docs/sysml2/common/**/*.sysml' `
  'docs/sysml2/model/**/*.sysml' `
  'docs/sysml2/views/design-views.sysml'
```

immediately before Pandoc compiles the Design document. FileAssert then asserts that the rendered
`SoftwareStructureView.svg` file exists in `docs/design/generated/` and is well-formed XML, before
Pandoc embeds it in the compiled document. Each invocation is a stateless CLI run with no
configuration object beyond its command-line arguments. The published SysML2Tools tool is not
referenced by the delivered SysML2Tools packages.
