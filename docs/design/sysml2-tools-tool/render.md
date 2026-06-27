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

- Input: `Context` — file patterns (`Files`), format (`RendererFormat`), output path
  (`OutputDirectory`), view filter (`ViewName`), render depth (`MaxRenderDepth`)
- Intermediate: `SysmlLoadResult` — workspace and diagnostics from `WorkspaceLoader`
- Output: files written to `OutputDirectory` via `File.Create`

##### Key Methods

**`RunAsync(Context context)`**

Entry point for the render command. Steps:

1. Validates that `context.Files` is non-empty; calls `context.WriteError` and returns
   when no patterns are supplied.
2. Calls `WorkspaceLoader.LoadAsync(context.Files)` to load the workspace.
3. Reports all diagnostics from `loadResult.Diagnostics`, writing errors via
   `context.WriteError` and other messages via `context.WriteLine`.
4. Calls `DiagramRenderer.GetViewNames(workspace)` to enumerate renderable views.
5. When `viewNames.Count > 1` and `context.ViewName` is null, calls `context.WriteError`
   with a message listing the available names and returns early.
6. Selects renderer: `PngRenderer` when `context.RendererFormat` equals `"png"`
   (case-insensitive); `SvgRenderer` otherwise.
7. Calls `DiagramRenderer.RenderWorkspace` passing
   `new RenderOptions(Themes.Light, DepthLimit: context.MaxRenderDepth ?? 0)` and
   `viewFilter: context.ViewName`.
8. Writes a "No views found" message and returns when `outputs` is empty.
9. Resolves the output directory (defaults to `Directory.GetCurrentDirectory()`), creates
   it via `Directory.CreateDirectory`, and writes each `RenderOutput.Data` stream to a
   file named `RenderOutput.SuggestedFileName`.

##### Error Handling

- Missing file patterns: `context.WriteError` is called and the method returns early.
- Load diagnostics: reported to the context; non-fatal; rendering proceeds regardless.
- Multiple views without `--view`: `context.WriteError` lists available view names and
  returns early.
- No view declarations: informational message; no output files written; returns normally.
- File system errors (e.g., permission denied): propagate as `IOException`; handled by
  `Program.Main`'s outer exception handler.

##### Dependencies

- `WorkspaceLoader` (in `DemaConsulting.SysML2Tools.Semantic`) — loads workspace
- `DiagramRenderer` (in `DemaConsulting.SysML2Tools.Rendering`) — renders views
- `SvgRenderer` (in `DemaConsulting.SysML2Tools.Svg`) — produces SVG output
- `PngRenderer` (in `DemaConsulting.SysML2Tools.Png`) — produces PNG output
- `Themes.Light` (in `DemaConsulting.SysML2Tools.Rendering`) — default theme
- `Context` (in `DemaConsulting.SysML2Tools.Cli`) — reads arguments; writes output

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
