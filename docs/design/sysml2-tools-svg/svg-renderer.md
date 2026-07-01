### SvgRenderer

#### Purpose

`SvgRenderer` is the single unit of the `DemaConsulting.SysML2Tools.Svg` system. It implements the
`IRenderer` interface and translates a `LayoutTree` into a self-contained SVG 1.1 document written
to a caller-supplied stream. It performs no layout; it draws the already-placed nodes a layout
strategy produced.

#### Data Model

`SvgRenderer` is a stateless class. Each `Render` call operates only on its arguments and a locally
built `StringBuilder`, so calls are independent. Intrinsic marker geometry is not stored on the
renderer; it is read from the shared `NotationMetrics` so that SVG and PNG markers are identical.

#### Key Methods

##### `MediaType` / `DefaultExtension`

Report `"image/svg+xml"` and `".svg"` so the diagram renderer can select SVG output and callers can
name and serve the output correctly.

##### `Render(LayoutTree tree, RenderOptions options, Stream output)`

Writes the SVG root element and a `defs` block of end markers, then walks the layout tree emitting an
element per node: `rect` for boxes (with `rx` for rounded corners and inner lines/text for
compartments), `text` for labels (with bold/italic styling and XML escaping of model text), `path`
for connector lines (with arcs for rounded corners, dash arrays for dashed styling, midpoint labels,
and `marker-end` references), and the notation-appropriate elements for port, badge, band, lifeline,
activation, and grid nodes.

#### Dependencies

- **Core `IRenderer` / `LayoutTree` / `RenderOptions`** — the rendering contract and intermediate
  representation the renderer consumes.
- **`NotationMetrics`** — supplies the intrinsic end-marker geometry so markers match the PNG output.

#### Callers

- `DiagramRenderer` (Core) invokes `SvgRenderer` once per view when SVG output is requested.
- Tests construct `SvgRenderer` directly and assert on the emitted SVG document.
