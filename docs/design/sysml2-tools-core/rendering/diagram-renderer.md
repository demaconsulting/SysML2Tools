### DiagramRenderer

#### Purpose

`DiagramRenderer` is the high-level rendering orchestrator and the single entry point for the
rendering pipeline. It iterates over every view declared in a `SysmlWorkspace`, selects a layout
strategy, builds a `LayoutTree`, and writes each view through an `IRenderer` to a `RenderOutput`. It
is the orchestrator of the Rendering subsystem and the point at which the Semantic, Layout, and
Rendering pieces are joined.

#### Data Model

`DiagramRenderer` is a `sealed class` with no instance state. It collaborates with the SysML-coupled
`ViewContext`, the internal `DiagramTypeRouter`, and the internal `StdlibFilter`, and with the
off-the-shelf `RenderOptions` and `RenderOutput` records from `DemaConsulting.Rendering.Abstractions`.

#### Key Methods

##### `RenderWorkspace(workspace, renderer, options, viewFilter = null)`

For each declaration in the workspace it skips non-view nodes and standard-library views (via
`StdlibFilter`), routes the view to an `ILayoutStrategy` (via `DiagramTypeRouter`), skips views with
no supporting strategy or that do not match `viewFilter`, constructs a `ViewContext` carrying the
view's own `SysmlViewNode` (so a strategy such as `GeneralViewLayoutStrategy` can read its resolved
`render`/`expose`/`filter` data), builds the `LayoutTree`, renders it to an in-memory stream, and
collects a `RenderOutput` with a sanitized file name and the layout warnings. Returns an empty list
when the workspace declares no renderable views.

##### `GetViewNames(workspace)`

Returns the display names of all renderable user-defined views, mirroring the filtering applied by
`RenderWorkspace`.

##### `SynthesizeAutoView(workspace)`

Synthesizes a `SysmlViewNode` targeting the most representative top-level element (the non-stdlib
`part def` with the most children, else the first non-stdlib definition) for use with `--auto` when
no user-defined views exist. Returns `null` when there is nothing to target. Unlike
`SynthesizeDynamicView` below, the returned node carries no `ResolvedEdges`, so
`ExposeScopeResolver` resolves a `null` scope and the rendered diagram covers the entire workspace.

##### `SynthesizeDynamicView(workspace, viewType, targetQualifiedName, filterExpressionText, out diagnostic)`

Public entry point for the dynamic (ad-hoc) view CLI feature (`render --view-type <kind>
--view-target <qualified-name> [--filter <expr>]`). Delegates directly to
`Internal.DynamicViewSynthesizer.Synthesize`, returning the synthesized `SysmlViewNode` (or
`null` with a non-null `diagnostic` on failure). Placed alongside `SynthesizeAutoView` since both
methods synthesize a view node outside the normal parse pipeline, but the two differ in scope:
`SynthesizeDynamicView`'s node is scoped to exactly the requested target via a manually populated
`Expose` edge, while `SynthesizeAutoView`'s node has no scoping edges at all (see above). See
`docs/design/sysml2-tools-core/rendering/internal/dynamic-view-synthesizer.md` for the full
per-kind compatibility rules and known limitations.

#### Error Handling

`RenderWorkspace`, `GetViewNames`, `SynthesizeAutoView`, and `SynthesizeDynamicView` throw
`ArgumentNullException` for null required arguments (`SynthesizeDynamicView`'s `filterExpressionText`
parameter excepted, since `null` is its valid "no filter" value). Views whose type is unsupported
by any strategy are skipped silently rather than failing the whole render. `SynthesizeDynamicView`
never throws for a synthesis failure (unrecognized view type, unresolved/wrong-kind/stdlib target,
failed compatibility pre-check, or name collision); it reports these via its `out diagnostic`
parameter instead.

#### Dependencies

- `SysmlWorkspace`, `SysmlViewNode`, `SysmlDefinitionNode` (Semantic subsystem).
- `ILayoutStrategy`, `ViewContext` (retained SysML-coupled Rendering subsystem contract).
- `LayoutTree`, `IRenderer`, `RenderOptions`, `RenderOutput` (off-the-shelf, from the
  `DemaConsulting.Rendering` and `DemaConsulting.Rendering.Abstractions` OTS packages).
- `DiagramTypeRouter` and `StdlibFilter` (Rendering Internal subsystem).
- `Internal.DynamicViewSynthesizer` (Rendering Internal subsystem) — `SynthesizeDynamicView`
  delegates its entire implementation to this unit.

#### Callers

The `RenderCommand` in the Tool system calls `RenderWorkspace` (and `GetViewNames` /
`SynthesizeAutoView` / `SynthesizeDynamicView`) to produce diagram output files from a loaded
workspace.

#### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| SysML2Tools-Core-Rendering-DiagramRenderer-RendersEachView | `RenderWorkspace` per-view build-and-render loop |
| SysML2Tools-Core-Rendering-DiagramRenderer-RendererAgnostic | `RenderWorkspace` using only the `IRenderer` contract |
| SysML2Tools-Core-Rendering-DiagramRenderer-NoViews | `RenderWorkspace` empty result for a view-free workspace |
| SysML2Tools-Core-Rendering-DiagramRenderer-SynthesizeDynamicView | `SynthesizeDynamicView` delegation |
