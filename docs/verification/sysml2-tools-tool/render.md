## DemaConsulting.SysML2Tools.Tool — Render Subsystem Verification

### Verification Approach

The Render subsystem is verified using unit tests in
`test/DemaConsulting.SysML2Tools.Tool.Tests/Render/RenderSubsystemTests.cs`.
Tests invoke `Program.RunAsync` with controlled `Context` instances and assert
on context output and exit code. File-writing scenarios use a temporary directory
(`Path.GetTempPath()`). Tests run against all three target frameworks.

### Test Environment

- Framework: xUnit v3
- Target frameworks: net8.0, net9.0, net10.0
- Test project: `DemaConsulting.SysML2Tools.Tool.Tests`
- Dependencies: `DemaConsulting.SysML2Tools.Tool` (internal access via `InternalsVisibleTo`)

### Acceptance Criteria

- No files supplied: `context.WriteError` is called with a message containing "no input files
  specified", and the method returns without loading
- One or more file patterns supplied but none resolve to any file on disk: `context.WriteError`
  is called with a message containing "no files matched", and the method returns without
  loading
- A glob pattern (e.g. `*.sysml`) resolves to every matching file in the target directory via
  the shared `GlobFileCollector` (see `docs/verification/sysml2-tools-core/io.md` for the
  underlying glob-semantics verification) and the workspace loads all of them
- Workspace loads without errors for a valid SysML model file
- SVG output produced for `--format svg` (or default)
- PNG output produced for `--format png`
- Output files written to `--output` directory
- No output files written when workspace has no views
- Informational message written when workspace has no views
- `--depth 1` produces SVG output containing the ellipsis character `"…"`
- Multiple views without `--view` renders every declared view, producing one output file per
  view, exit code 0
- Two views whose display names sanitize to the same output file name (declared in different
  packages) yield exit code 1, an error message naming both colliding qualified views and the
  shared file name, and no output files written
- `--view <name>` with a multi-view workspace renders exactly one file
- `--view <name>` naming a view that does not exist yields exit code 1 and an error message
  listing available view names
- Unsupported `--format` value throws `ArgumentException` when `RunAsync` executes (not at
  `Context.Create` parse time)
- `render --help` prints render-specific usage and options (not the generic top-level command
  list), and is identical to `help render`'s output (see the Help subsystem verification
  document).
- A workspace with two views — one with an `expose <target>;` statement naming a resolvable
  target, one with a bogus `expose thisIdentifierDoesNotExistAnywhere;` statement — produces
  two output files whose content DIFFERS, and the bogus view's unresolved exposed name is
  visible as a diagnostic in the captured log output.
- Rendering the real OMG corpus fixture `11b-SafetyAndSecurityFeatureViews.sysml` with no
  `--view` filter produces exactly 5 output files (2 `view def`s plus 3 named `view` usages),
  regression-guarding the `VisitViewUsage` capability addition, with no false
  "Unresolved reference" diagnostic for its `render asTreeDiagram;`/`render asElementTable;`
  rendering-style members.

### Test Scenarios

#### RenderSubsystem_NoFiles_ReportsError

Verifies that invoking the render command with zero file patterns results in an error message
written to the context containing the "no input files specified" diagnostic text, and no
workspace loading.

#### RenderSubsystem_PatternMatchesNoFiles_ReportsError

Verifies that invoking the render command with a file pattern that matches no file on disk
results in an error message written to the context containing "no files matched", and no
workspace loading.

#### RenderSubsystem_GlobPattern_ResolvesMultipleFiles

Regression test for the glob-expansion bug fix: verifies that a glob pattern such as
`*.sysml` (previously treated as a literal, never-matching file name, causing a "Failed to
read file" diagnostic and zero rendered views) now resolves to every matching `.sysml` file
in the target directory via the shared `GlobFileCollector`, and that the workspace loads and
renders successfully from all of them.

#### RenderSubsystem_WithFiles_LoadsWorkspace

Verifies that supplying a valid SysML model file loads without errors,
producing a non-null workspace in the context.

#### RenderSubsystem_FormatSvg_UsesSvgRenderer

Verifies that `--format svg` routes to the SVG renderer by confirming output file
extension is `.svg`.

#### RenderSubsystem_FormatPng_UsesPngRenderer

Verifies that `--format png` routes to the PNG renderer by confirming output file
extension is `.png`.

#### RenderSubsystem_NoOutputDir_UsesCurrentDirectory

Verifies that omitting `--output` causes files to be written to the current working
directory.

#### RenderSubsystem_NoViews_ReportsNoOutput

Verifies that a model with no view declarations produces no output files and an
informational message.

#### RenderSubsystem_WithDepth_LimitsNesting

Verifies that `--depth 1` causes the SVG output to contain the ellipsis character `"…"`,
confirming that child part-def boxes were replaced by the depth-limit indicator.

#### RenderSubsystem_MultipleViews_NoViewFlag_RendersAllViews

Verifies that rendering a workspace with two views and no `--view` flag renders every
declared view: exit code 0, and exactly two `.svg` output files produced (one per view).

#### RenderSubsystem_DuplicateViewFileNames_ReportsCollisionError

Verifies that rendering a workspace containing two views with the same simple name
(`SharedView`), declared in different packages (`PkgA`, `PkgB`), with no `--view` flag: exit
code 1, an error message naming both colliding qualified views (`PkgA::SharedView`,
`PkgB::SharedView`) and the shared output file name (`SharedView.svg`), and no output directory
or files are created.

#### RenderSubsystem_UnknownViewFlag_ReportsErrorWithAvailableViews

Verifies that `--view <nonexistent-name>` yields exit code 1 and an error message listing
the available view names (`ViewAlpha` and `ViewBeta`).

#### RenderSubsystem_MultipleViews_WithViewFlag_RendersSelectedView

Verifies that `--view ViewAlpha` selects exactly one view from a two-view workspace and
produces a single `.svg` output file.

#### RenderSubsystem_UnsupportedFormat_ThrowsArgumentException

Verifies that `--format xml` (an unsupported value) does not throw when `Context.Create` parses
the arguments, but throws `ArgumentException` naming the bad value once `Program.RunAsync`
actually runs the render command — mirroring the timing of the `query` command's `--format`
validation.

#### RenderSubsystem_Help_PrintsRenderSpecificUsage

Verifies that `render --help` prints the render-specific usage line and its `--output`/
`--auto` flags, and does not print the generic top-level `"Commands:"` section — a
regression-proofing test added alongside the `help` command's command-aware `--help` dispatch
(see `docs/design/sysml2-tools-tool/help.md`).

#### RenderSubsystem_ViewsWithDistinctExposeTargets_ProduceDifferingOutputsAndDiagnostic

End-to-end regression test: a workspace declares two `view` usages — one with `expose TargetA;`
(resolving to a `part def` with a nested child), one with `expose
thisIdentifierDoesNotExistAnywhere;` (an unresolvable target). Verifies that rendering both
without `--view` produces `ViewValid.svg` and `ViewBogus.svg` whose content DIFFERS (a view with
no resolved `Expose` edges renders the full workspace, while the valid view's sole `Expose` entry
scopes to that target's subtree), and that the captured `--log` output contains
`"thisIdentifierDoesNotExistAnywhere"` — the unresolved-reference diagnostic surfaced by
`ReferenceResolver` for the bogus exposed name. `render <target>;` plays no role in scoping —
only `expose` does, per the corrected semantics.

#### RenderSubsystem_OmgSafetyFeatureViewsCorpus_RendersAllNamedViewUsages

Regression guard for the `AstBuilder.VisitViewUsage` capability addition: loads and renders the
real OMG corpus fixture
`test/SysMLModels/OMG/validation/11-ViewAndViewpoint/11b-SafetyAndSecurityFeatureViews.sysml`
with no `--view` filter, asserting exactly 5 output files are produced — the 2 `view def`
declarations (`SafetyFeatureView`, `SafetyOrSecurityFeatureView`) plus the 3 named `view` usages
(`vehicleSafetyFeatureView`, `vehicleMandatorySafetyFeatureView`,
`vehicleMandatorySafetyFeatureViewStandalone`), not just the 2 `view def`s that were the only
renderable declarations before `VisitViewUsage` was added. Also asserts the captured `--log`
output contains no `"asTreeDiagram"`/`"asElementTable"` text, confirming those rendering-style
`render` members never surface a false unresolved-reference diagnostic.

#### ResxResource_EveryKey_ResolvesToNonEmptyText / ResxResource_KeysAndAccessorProperties_AreInBidirectionalParity (ResxResourceTests.cs)

For the `RenderStrings` resource base name/accessor pair (one of four covered by these theory
tests), every key discovered in `Render/RenderStrings.resx`'s invariant-culture resource set
resolves to non-null/non-empty text via `ResourceManager`, and every such key has a matching
`public static string` property on `RenderStrings` (and vice versa). Satisfies
`SysML2Tools-Tool-Render-LocalizableHelpText`.
