### DiagramRenderer

#### Purpose

`DiagramRenderer` is the high-level rendering orchestrator and the single entry point for the
rendering pipeline. It iterates over every view declared in a `SysmlWorkspace`, selects a layout
strategy, builds a `LayoutTree`, and writes each view through an `IRenderer` to a `RenderOutput`. It
is the orchestrator of the Rendering subsystem and the point at which the Semantic, Layout, and
Rendering pieces are joined.

#### Data Model

`DiagramRenderer` is a `sealed class` with no instance state (a future phase will inject an
`ILayoutStrategy`). It collaborates with `ViewContext`, `RenderOptions`, `RenderOutput`, the internal
`DiagramTypeRouter`, and the internal `StdlibFilter`.

#### Key Methods

##### `RenderWorkspace(workspace, renderer, options, viewFilter = null)`

For each declaration in the workspace it skips non-view nodes and standard-library views (via
`StdlibFilter`), routes the view to an `ILayoutStrategy` (via `DiagramTypeRouter`), skips views with
no supporting strategy or that do not match `viewFilter`, builds the `LayoutTree`, renders it to an
in-memory stream, and collects a `RenderOutput` with a sanitized file name and the layout warnings.
Returns an empty list when the workspace declares no renderable views.

##### `GetViewNames(workspace)`

Returns the display names of all renderable user-defined views, mirroring the filtering applied by
`RenderWorkspace`.

##### `SynthesizeAutoView(workspace)`

Synthesizes a `SysmlViewNode` targeting the most representative top-level element (the non-stdlib
`part def` with the most children, else the first non-stdlib definition) for use with `--auto` when
no user-defined views exist. Returns `null` when there is nothing to target.

#### Error Handling

`RenderWorkspace`, `GetViewNames`, and `SynthesizeAutoView` throw `ArgumentNullException` for null
required arguments. Views whose type is unsupported by any strategy are skipped silently rather than
failing the whole render.

#### Dependencies

- `SysmlWorkspace`, `SysmlViewNode`, `SysmlDefinitionNode` (Semantic subsystem).
- `ILayoutStrategy`, `IRenderer`, `RenderOptions`, `RenderOutput`, `ViewContext` (Rendering subsystem).
- `DiagramTypeRouter` and `StdlibFilter` (Rendering Internal subsystem).

#### Callers

The `RenderCommand` in the Tool system calls `RenderWorkspace` (and `GetViewNames` /
`SynthesizeAutoView`) to produce diagram output files from a loaded workspace.

#### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| SysML2Tools-Core-Rendering-DiagramRenderer-RendersEachView | `RenderWorkspace` per-view build-and-render loop |
| SysML2Tools-Core-Rendering-DiagramRenderer-RendererAgnostic | `RenderWorkspace` using only the `IRenderer` contract |
| SysML2Tools-Core-Rendering-DiagramRenderer-NoViews | `RenderWorkspace` empty result for a view-free workspace |
