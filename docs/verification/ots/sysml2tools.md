## SysML2Tools Verification

This document provides the verification evidence for the SysML2Tools OTS software item. Requirements
for this OTS item are defined in the SysML2Tools OTS Software Requirements document.

### Required Functionality

The published SysML2Tools tool lints the SysML2 architecture model under `docs/sysml2/`, reporting
syntax errors, and with `--strict-unresolved` unresolved references, as errors, and renders each view declared in
`docs/sysml2/views/design-views.sysml` to an SVG diagram embedded in the design documentation.
Both behaviors run in the same CI pipeline that produces the compiled Design document, so a
successful pipeline run is evidence that SysML2Tools executed without error.

### Verification Approach

SysML2Tools is verified by two complementary layers of evidence. First, the CI pipeline runs
`dotnet sysml2tools --validate --results artifacts/sysml2tools-self-validation.trx`, which runs
the published tool's built-in self-test suite and writes a TRX file consumed by ReqStream. The
self-tests prove only the following: the lint self-test loads one embedded, valid model fixture and
passes when loading reports no error-level diagnostics; the render self-test loads the same fixture,
renders it to SVG in memory, and passes when at least one non-empty output is produced. They do not
exercise an invalid model, so they are not evidence that syntax errors or unresolved references are
detected.

The in-repository build of this project also emits self-validation tests with the same names
(`SysML2Tools_LintSelfTest`, `SysML2Tools_RenderSvgSelfTest`) into its own platform-specific
`validation-*.trx` files. Those results verify the product under test, not this OTS item, so the
requirements for this OTS item use the `sysml2tools-self-validation@` source filter to accept only
the results written by the published tool.

Second, `lint.ps1` runs `dotnet sysml2tools lint --strict-unresolved 'docs/sysml2/**/*.sysml'`
against the actual SysML2Tools model and fails the build on a non-zero exit code (syntax errors and,
because of `--strict-unresolved` in the pinned release 0.3.0-beta.4, unresolved references) and on any
`warning:` line in the output. The lint rule is therefore zero errors and zero warnings: warnings such
as duplicate root packages or import cycles do not change the exit code, so the output scan in
`lint.ps1` is what makes them fail. The build-docs job runs
`dotnet sysml2tools render --strict-unresolved` to produce one SVG file per declared view under
`docs/design/generated/`. FileAssert then asserts that the overview SVG file exists, has a
non-trivial size, and is well-formed XML with an `<svg>` root element
(`SysML2Tools_SoftwareStructureViewSvg`), before Pandoc compiles the Design document, which embeds
those SVG files by filename. A CI build failure at any of these steps is evidence that SysML2Tools
did not produce the required model validation or diagrams against the real model.

### Test Scenarios

#### SysML2Tools_LintSelfTest

**Scenario**: The published SysML2Tools is invoked with `--validate`, which runs its lint self-test:
it writes one embedded, valid model fixture to a temporary file and loads it.

**Expected**: The test passes when loading the valid fixture reports no error-level diagnostics.
It does not load an invalid model and does not check how unresolved references are reported.

**Requirement coverage**: `SysML2Tools-OTS-SysML2Tools-Lint` (the tool lints a valid model without
errors; error and warning detection on the real model is evidenced by `lint.ps1`, see above).

#### SysML2Tools_RenderSvgSelfTest

**Scenario**: The published SysML2Tools is invoked with `--validate`, which runs its render
self-test: it loads the same embedded, valid model fixture and renders it to SVG in memory.

**Expected**: The test passes when the workspace loads and at least one non-empty SVG output is
produced.

**Requirement coverage**: `SysML2Tools-OTS-SysML2Tools-Render`.

#### SysML2Tools_SoftwareStructureViewSvg

**Scenario**: The build-docs CI job runs `dotnet sysml2tools render` against the real SysML2Tools
model, then FileAssert checks the resulting file at
`docs/design/generated/SoftwareStructureView.svg`.

**Expected**: Exactly one file exists at that path, it is at least 100 bytes, and it is
well-formed XML with a root element named `svg`.

**Requirement coverage**: `SysML2Tools-OTS-SysML2Tools-Render`.

### Requirements Coverage

- **`SysML2Tools-OTS-SysML2Tools-Lint`**: SysML2Tools_LintSelfTest
- **`SysML2Tools-OTS-SysML2Tools-Render`**: SysML2Tools_RenderSvgSelfTest,
  SysML2Tools_SoftwareStructureViewSvg
