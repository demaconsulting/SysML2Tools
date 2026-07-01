### PngRenderer

#### Purpose

`PngRenderer` is the single unit of the `DemaConsulting.SysML2Tools.Png` system. It implements the
`IRenderer` interface and rasterizes a `LayoutTree` into a PNG image using SkiaSharp, written to a
caller-supplied stream. It performs no layout; it draws the already-placed nodes a layout strategy
produced.

#### Data Model

`PngRenderer` is a stateless class. Each `Render` call creates its own SkiaSharp surface, canvas, and
paints, disposing them before returning, so calls are independent. Intrinsic marker geometry is not
stored on the renderer; it is read from the shared `NotationMetrics` so that PNG and SVG markers are
identical.

#### Key Methods

##### `MediaType` / `DefaultExtension`

Report `"image/png"` and `".png"` so the diagram renderer can select PNG output and callers can name
and serve the output correctly.

##### `Render(LayoutTree tree, RenderOptions options, Stream output)`

Creates an `SKSurface` sized to the layout, clears it to white, and walks the layout tree drawing each
node with SkiaSharp: filled and stroked rectangles for boxes (fill selected from the theme depth
colours), rasterized text for labels, stroked paths for connector lines, the notation-appropriate
shapes for port, badge, band, lifeline, activation, and grid nodes, and connector end markers. It then
encodes the surface as PNG and writes the bytes to the output stream, disposing all SkiaSharp objects.

#### Dependencies

- **Core `IRenderer` / `LayoutTree` / `RenderOptions`** — the rendering contract and intermediate
  representation the renderer consumes.
- **SkiaSharp** — provides the surface, canvas, paints, and PNG encoder.
- **`NotationMetrics`** — supplies the intrinsic end-marker geometry so markers match the SVG output.

#### Callers

- `DiagramRenderer` (Core) invokes `PngRenderer` once per view when PNG output is requested.
- Tests construct `PngRenderer` directly and assert on the decoded PNG pixels and signature.
