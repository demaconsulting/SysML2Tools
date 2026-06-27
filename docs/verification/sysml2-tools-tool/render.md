### DemaConsulting.SysML2Tools.Tool — Render Subsystem Verification

#### Verification Approach

The Render subsystem is verified using unit tests in
`test/DemaConsulting.SysML2Tools.Tool.Tests/Render/RenderSubsystemTests.cs`.
Tests invoke `Program.RunAsync` with controlled `Context` instances and assert
on context output and exit code. File-writing scenarios use a temporary directory
(`Path.GetTempPath()`). Tests run against all three target frameworks.

#### Test Environment

- Framework: xUnit v3
- Target frameworks: net8.0, net9.0, net10.0
- Test project: `DemaConsulting.SysML2Tools.Tool.Tests`
- Dependencies: `DemaConsulting.SysML2Tools.Tool` (internal access via `InternalsVisibleTo`)

#### Acceptance Criteria

- No files supplied: `context.WriteError` is called and method returns without loading
- Workspace loads without errors for a valid SysML model file
- SVG output produced for `--format svg` (or default)
- PNG output produced for `--format png`
- Output files written to `--output` directory
- No output files written when workspace has no views
- Informational message written when workspace has no views
- `--depth 1` produces SVG output containing the ellipsis character `"…"`
- Multiple views without `--view` yields exit code 1 and an error message
- `--view <name>` with a multi-view workspace renders exactly one file

#### Test Scenarios

##### RenderSubsystem_NoFiles_ReportsError

Verifies that invoking the render command with zero file patterns results in an
error message written to the context and no workspace loading.

##### RenderSubsystem_WithFiles_LoadsWorkspace

Verifies that supplying a valid SysML model file loads without errors,
producing a non-null workspace in the context.

##### RenderSubsystem_FormatSvg_UsesSvgRenderer

Verifies that `--format svg` routes to the SVG renderer by confirming output file
extension is `.svg`.

##### RenderSubsystem_FormatPng_UsesPngRenderer

Verifies that `--format png` routes to the PNG renderer by confirming output file
extension is `.png`.

##### RenderSubsystem_NoOutputDir_UsesCurrentDirectory

Verifies that omitting `--output` causes files to be written to the current working
directory.

##### RenderSubsystem_NoViews_ReportsNoOutput

Verifies that a model with no view declarations produces no output files and an
informational message.

##### RenderSubsystem_WithDepth_LimitsNesting

Verifies that `--depth 1` causes the SVG output to contain the ellipsis character `"…"`,
confirming that child part-def boxes were replaced by the depth-limit indicator.

##### RenderSubsystem_MultipleViews_NoViewFlag_ReportsError

Verifies that rendering a workspace with two views and no `--view` flag yields exit code 1.

##### RenderSubsystem_MultipleViews_NoViewFlag_ListsAvailableViews

Verifies that the multi-view error path writes an error containing the available view names
to the log.

##### RenderSubsystem_MultipleViews_WithViewFlag_RendersSelectedView

Verifies that `--view ViewAlpha` selects exactly one view from a two-view workspace and
produces a single `.svg` output file.
