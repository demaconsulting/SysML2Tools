### RenderingContracts

#### Purpose

`RenderingContracts` is the grouped unit that gathers the small, cohesive contract types defining the
rendering pipeline: the `IRenderer` and `ILayoutStrategy` interfaces (with the `ViewContext` input
record), the `RenderOptions` and `RenderOutput` records, and the internal `StdlibFilter` helper.
Together they are the seam between the Layout subsystem, the `DiagramRenderer` orchestrator, and the
concrete renderer packages.

#### Grouped Unit

This unit is a **grouped contracts unit**. It bundles five cohesive contract source files because
they are small interface/record declarations with little or no logic that are versioned together as
the rendering pipeline's public and internal surface:

- `IRenderer.cs` — the low-level, stateless render interface.
- `ILayoutStrategy.cs` — the layout-computation interface, plus the `ViewContext` input record.
- `RenderOptions.cs` — per-render parameters (`Theme`, `Scale`, `Dpi`, `DepthLimit`).
- `RenderOutput.cs` — a single rendered output (`SuggestedFileName`, `MediaType`, `Data`, `Warnings`).
- `Internal/StdlibFilter.cs` — identifies standard-library elements to exclude from diagrams.

The full per-type contract is documented in the **Rendering Subsystem** chapter's *Interfaces*
section and is not duplicated here; this chapter records the grouping and the contract behaviors that
the unit tests pin.

#### Data Model

`IRenderer` and `ILayoutStrategy` are interfaces; `ViewContext`, `RenderOptions`, and `RenderOutput`
are sealed records; `StdlibFilter` is an internal static class holding the known stdlib root-package
prefixes and the `IsStdlibElement` predicates.

#### Key Behaviors

- **IRenderer** exposes `MediaType`, `DefaultExtension`, and `Render(LayoutTree, RenderOptions,
  Stream)`; implementations are pure, stateless, and do not touch the filesystem.
- **ILayoutStrategy** exposes `BuildLayout(ViewContext, RenderOptions)` returning a fully resolved
  `LayoutTree`.
- **RenderOptions** defaults `Scale` to 1.0, `Dpi` to 96, and `DepthLimit` to 0 (unlimited).
- **RenderOutput** carries a suggested file name, media type, data stream, and a `Warnings` list of
  non-fatal layout-quality warnings (empty when the layout is clean).
- **StdlibFilter.IsStdlibElement** returns `true` for qualified names in a known stdlib package (by
  seed-set membership or root-package prefix).

#### Error Handling

The records store their fields unchanged. `StdlibFilter` is a pure predicate. Renderer
implementations own the lifetime of the supplied output stream per the `IRenderer` contract.

#### Dependencies

- `LayoutTree` (Layout subsystem) — the input drawn by `IRenderer` and produced by `ILayoutStrategy`.
- `SysmlWorkspace` (Semantic subsystem) — carried by `ViewContext`.
- `Theme` (Rendering subsystem) — held by `RenderOptions`.

#### Callers

`DiagramRenderer` depends on all five types to orchestrate rendering; the SVG and PNG renderer
packages implement `IRenderer`; the layout strategies implement `ILayoutStrategy`.

#### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| SysML2Tools-Core-Rendering-RenderingContracts-IRenderer | `IRenderer` and its SVG/PNG implementations |
| SysML2Tools-Core-Rendering-RenderingContracts-ILayoutStrategy | `ILayoutStrategy` and the `ViewContext` record |
| SysML2Tools-Core-Rendering-RenderingContracts-RenderOptions | `RenderOptions` record with default values |
| SysML2Tools-Core-Rendering-RenderingContracts-RenderOutput | `RenderOutput` record |
| SysML2Tools-Core-Rendering-RenderingContracts-StdlibFilter | `StdlibFilter.IsStdlibElement` |
