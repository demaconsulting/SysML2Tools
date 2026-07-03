### DemaConsulting.SysML2Tools.Tool — Render Subsystem

#### Overview

The Render subsystem implements the `render` CLI verb. It provides a single internal
static class, `RenderCommand`, with one entry-point method `RunAsync`. The subsystem
coordinates workspace loading, renderer selection, and file output for the Phase 4
diagram-rendering feature.

#### RenderCommand

##### Purpose

`RenderCommand.RunAsync` translates the user's CLI intent — expressed as file glob
patterns, a format option, and an output directory — into rendered diagram files on
disk. It delegates workspace loading to `WorkspaceLoader`, renderer instantiation to
a simple string comparison, and rendering orchestration to `DiagramRenderer`.

##### Data Model

No instance state. All data flows through the `Context` parameter and local variables.

- Input: `Context.Render` (a `RenderCommandOptions` populated by `RenderArgumentParser`) —
  file patterns (`Files`), format (`Format`), output path (`OutputDirectory`), view filter
  (`ViewName`), auto-view flag (`AutoView`); plus `Context.MaxRenderDepth` (render depth,
  parsed globally — see `docs/design/sysml2-tools-tool/cli/context.md`)
- Intermediate: `SysmlLoadResult` — workspace and diagnostics from `WorkspaceLoader`
- Output: files written to `OutputDirectory` via `File.Create`

##### Key Methods

**`RunAsync(Context context)`**

Entry point for the render command. Steps:

1. Reads `context.Render` (throwing `ArgumentException` if somehow null — defensive only, since
   `Program` only reaches here when `Context.Create` has already populated it) and validates
   that `options.Files` is non-empty; calls `context.WriteError` and returns when no patterns
   are supplied.
2. Calls `WorkspaceLoader.LoadAsync(options.Files)` to load the workspace.
3. Reports all diagnostics from `loadResult.Diagnostics`, writing errors via
   `context.WriteError` and other messages via `context.WriteLine`.
4. Calls `DiagramRenderer.GetViewNames(workspace)` to enumerate renderable views.
5. When `viewNames.Count > 1` and `options.ViewName` is null, calls `context.WriteError`
   with a message listing the available names and returns early.
6. Resolves `format = options.Format ?? "svg"` and eagerly rejects any value other than
   `"svg"`/`"png"` (case-insensitive) with `ArgumentException` naming the bad value — mirroring
   the `query` command's `--format` validation style. This is validated here, in `RunAsync`, not
   inside `RenderArgumentParser`, so an invalid `--format` value (e.g., `render --format xml`)
   throws only once the command actually runs. Selects `PngRenderer` when `format` equals
   `"png"`; `SvgRenderer` otherwise.
7. Calls `DiagramRenderer.RenderWorkspace` passing
   `new RenderOptions(Themes.Light, DepthLimit: context.MaxRenderDepth ?? 0)` and
   `viewFilter: options.ViewName`.
8. Writes a "No views found" message and returns when `outputs` is empty.
9. Resolves the output directory (defaults to `Directory.GetCurrentDirectory()`), creates
   it via `Directory.CreateDirectory`, and writes each `RenderOutput.Data` stream to a
   file named `RenderOutput.SuggestedFileName`.

**`PrintHelp(Context context)`**: Prints `render`'s usage line and its four flags (`--output`,
`--format`, `--view`, `--auto`), plus a note about the shared global `--depth` option. This is
the single source of truth for both `render --help` (dispatched from `Program.RunAsync`'s
command-aware help block) and `help render` (dispatched from `Help.HelpCommand.Run`); neither
entry point duplicates the help text. Every line printed by `PrintHelp` is sourced from
`RenderStrings`, a hand-written, culture-aware `ResourceManager` accessor over
`Render/RenderStrings.resx` — see `docs/design/sysml2-tools-tool/program.md`'s
"Localization / Resource Strings" section for the rationale and the zero-code-change
future-locale story, which applies identically here.

##### Error Handling

- Missing file patterns: `context.WriteError` is called and the method returns early.
- Load diagnostics: reported to the context; non-fatal; rendering proceeds regardless.
- Multiple views without `--view`: `context.WriteError` lists available view names and
  returns early.
- Unsupported `--format` value: `ArgumentException` is thrown naming the bad value and the
  valid values (`svg`, `png`); propagates to `Program.Main`'s expected-exception handler.
- No view declarations: informational message; no output files written; returns normally.
- File system errors (e.g., permission denied): propagate as `IOException`; handled by
  `Program.Main`'s outer exception handler.

##### Dependencies

- `WorkspaceLoader` (in `DemaConsulting.SysML2Tools.Semantic`) — loads workspace
- `DiagramRenderer` (in `DemaConsulting.SysML2Tools.Rendering`) — renders views
- `SvgRenderer` (in `DemaConsulting.Rendering.Svg`) — produces SVG output
- `PngRenderer` (in `DemaConsulting.Rendering.Skia`) — produces PNG output
- `Themes.Light` (in `DemaConsulting.Rendering.Abstractions`) — default theme
- `Context`/`RenderCommandOptions`/`RenderArgumentParser` (in `DemaConsulting.SysML2Tools.Cli`
  and `DemaConsulting.SysML2Tools.Render`) — reads arguments; writes output

##### Callers

- `Program.RunToolLogicAsync` — dispatches to `RenderCommand.RunAsync` when
  `context.Command == SysmlCommand.Render`

#### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| SysML2Tools-Tool-Render-Patterns | Input validation at start of `RunAsync` |
| SysML2Tools-Tool-Render-Load | `WorkspaceLoader.LoadAsync` call in `RunAsync` |
| SysML2Tools-Tool-Render-Format | Renderer selection switch in `RunAsync` |
| SysML2Tools-Tool-Render-Output | Output directory resolution in `RunAsync` |
| SysML2Tools-Tool-Render-Empty | Empty-outputs message in `RunAsync` |
| SysML2Tools-Tool-Render-DepthLimit | `DepthLimit` passed to `RenderOptions` in `RunAsync` |
| SysML2Tools-Tool-Render-MultipleViewError | Multi-view guard using `GetViewNames` in `RunAsync` |
| SysML2Tools-Tool-Render-ViewSelection | `viewFilter` passed to `RenderWorkspace` in `RunAsync` |
| SysML2Tools-Tool-Render-FormatValidation | Eager `--format` value guard in `RunAsync` |
