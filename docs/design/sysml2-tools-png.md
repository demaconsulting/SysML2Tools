# DemaConsulting.SysML2Tools.Png

## Overview

`DemaConsulting.SysML2Tools.Png` is the PNG renderer system for SysML2 Tools. It provides
a single public class, `PngRenderer`, that uses SkiaSharp to rasterize a `LayoutTree`
into a PNG image written to an output stream.

## PngRenderer

### Purpose

`PngRenderer` implements the `IRenderer` interface to produce PNG diagram output from a
`LayoutTree` intermediate representation. Each call to `Render` allocates an `SKBitmap`,
draws all layout nodes onto an `SKCanvas`, encodes the result as PNG via `SKImage.Encode`,
and writes the bytes to the output stream. All SkiaSharp resources are disposed before
the method returns. The renderer is pure and stateless.

### Data Model

`PngRenderer` has no instance state. All inputs are supplied through `Render` parameters.

- `LayoutTree` — read-only input; canvas dimensions and node list
- `RenderOptions` — read-only input; `Theme` for colors/fonts, `Scale` for bitmap size
- `Stream output` — write-only output; receives PNG bytes; caller owns lifetime

### Embedded Font

`PngRenderer` embeds the four static Noto Sans TTF files as assembly resources to guarantee
pixel-identical output across all platforms regardless of which fonts are installed on the
host system:

| Resource | Typeface |
| --- | --- |
| `Fonts/NotoSans-Regular.ttf` | Regular weight, normal style |
| `Fonts/NotoSans-Bold.ttf` | Bold weight, normal style |
| `Fonts/NotoSans-Italic.ttf` | Regular weight, italic style |
| `Fonts/NotoSans-BoldItalic.ttf` | Bold weight, italic style |

The four static `Lazy<SKTypeface>` fields (`RegularTypeface`, `BoldTypeface`,
`ItalicTypeface`, `BoldItalicTypeface`) load each font once on first use via the
`LoadTypeface(string fileName)` helper. `LoadTypeface` locates the resource by matching
the file name suffix (case-insensitive) against the assembly manifest resource names and
falls back to `SKTypeface.Default` if the resource is not found, so the renderer
remains functional even when the font files are absent.

The SIL Open Font License 1.1 attribution file for Noto Sans is included at
`Fonts/OFL.txt` and is also embedded as an assembly resource.

### Font Weight and Style Per Node Type

Each node type uses a fixed weight and style:

| Node Type | Weight | Style |
| --- | --- | --- |
| `LayoutBox` label | Bold | Normal |
| `LayoutBoxCompartment` title | Bold | Italic |
| `LayoutBoxCompartment` rows | Regular | Normal |
| `LayoutLine` midpoint label | Regular | Normal |
| `LayoutLabel` | Per `FontWeight` field | Per `FontStyle` field |
| `LayoutPort` label | Regular | Normal |
| `LayoutBadge` label | Regular | Normal |
| `LayoutBand` label | Regular | Normal |
| `LayoutLifeline` label | Bold | Normal |
| `LayoutGrid` header cells | Bold | Normal |
| `LayoutGrid` body cells | Regular | Normal |

### LayoutLabel Font Styling Fields

`LayoutLabel` carries three explicit font styling fields added in Phase 4:

- `Weight` (`FontWeight`) — `Regular` or `Bold`; selects the typeface variant.
- `Style` (`FontStyle`) — `Normal` or `Italic`; selects the typeface variant.
- `FontSize` (double) — Font size in logical pixels, independent of the theme body size.

### Key Methods

**`Render(LayoutTree layout, RenderOptions options, Stream output)`**

Entry point. Validates arguments. Computes bitmap width and height as
`(int)Math.Ceiling(layout.Width * options.Scale)`, clamped to a minimum of 1×1 pixels
to prevent SkiaSharp allocation errors on empty trees. Creates `SKBitmap(w, h, Rgba8888, Premul)`,
fills background with `SKColors.White`, calls `RenderNode` for each top-level node,
then encodes via `SKImage.FromBitmap(bitmap).Encode(Png, 100)` and saves to `output`.

**`RenderNode(SKCanvas canvas, LayoutNode node, RenderOptions options)`**

Dispatches by concrete node type to the appropriate typed render method. All nine
`LayoutNode` subtypes are handled: `LayoutBox` → `RenderBox`, `LayoutLine` → `RenderLine`,
`LayoutLabel` → `RenderLabel`, `LayoutPort` → `RenderPort`, `LayoutBadge` → `RenderBadge`,
`LayoutBand` → `RenderBand`, `LayoutLifeline` → `RenderLifeline`,
`LayoutActivation` → `RenderActivation`, `LayoutGrid` → `RenderGrid`. Unknown subtypes
are silently skipped for forward compatibility.

**`LoadTypeface(string fileName)`**

Locates the embedded assembly resource whose name ends with `fileName`
(case-insensitive). Returns an `SKTypeface` decoded from the resource stream, or
`SKTypeface.Default` if no matching resource is found.

**`CreateTextPaint(SKColor color, float fontSize, bool bold, bool italic)`**

Selects the appropriate `Lazy<SKTypeface>` variant based on the `(bold, italic)` tuple,
then returns a new `SKPaint` configured with the color, font size, anti-aliasing, and
typeface. The caller is responsible for disposing the returned paint.

**`FitFontSize(SKPaint paint, string text, float availableWidth, float maxFontSize)`**

Measures the text width at `maxFontSize`. Returns `maxFontSize` unchanged when the text
fits within `availableWidth` or when `availableWidth` is zero or negative. Otherwise
scales the font size proportionally: `maxFontSize * (availableWidth / measuredWidth)`.
This ensures long labels do not overflow their bounding box.

**`RenderBox(SKCanvas canvas, LayoutBox box, RenderOptions options)`**

Draws a filled rectangle (plain or rounded via `DrawRoundRect` when
`BoxShape.RoundedRectangle` and `LineCornerRadius > 0`) using the fill color from
`theme.DepthFillColors[depth % count]`. Draws a matching stroke rectangle. Draws centered
bold title text when `box.Label` is non-null, calling `FitFontSize` to prevent overflow.
Calls `RenderBoxCompartments` for any compartments, then recursively calls `RenderNode`
for all `box.Children`.

**`RenderBoxCompartments(SKCanvas canvas, LayoutBox box, RenderOptions options, SKColor strokeColor)`**

Draws a full-width horizontal divider line at the start of each compartment, followed by
an optional bold-italic title row and zero or more left-aligned regular-weight body-font
text rows. Tracks a running Y offset starting below the label area.

**`RenderLine(SKCanvas canvas, LayoutLine line, RenderOptions options)`**

Builds a single `SKPath` from all waypoints. Applies `SKPathEffect.CreateCorner` when
`LineCornerRadius > 0`. When dashing is also active, composes effects with
`SKPathEffect.CreateCompose(dash, corner)` so the dash pattern follows the rounded path.
When either end carries a marker, the corner radius is clamped (via `NeedsEndCornerClamp`
and `BuildClampedLinePath`) so the rounded corner completes at least the marker's
along-line length before the endpoint, keeping the curve out of the end-marker zone.
After drawing the path, calls `DrawEndMarker` for non-None `SourceEnd` and `TargetEnd`
styles, then `RenderLineMidpointLabel` when `MidpointLabel` is non-null.

**`DrawEndMarker(SKCanvas, tipX, tipY, dx, dy, EndMarkerStyle, EndMarkerPaint)`**

Draws the line-end marker at the given tip using a normalized direction vector. Supports all
nine `EndMarkerStyle` values: `None` (no-op), `OpenChevron` (open chevron — two strokes that
meet at the apex with no closing base edge), `HollowTriangle` (hollow triangle, closed),
`HollowTriangleCrossbar` (hollow triangle with perpendicular crossbar), `FilledArrow` (solid
triangle), `HollowDiamond` (hollow four-point polygon, closed), `FilledDiamond` (solid
four-point polygon, closed), `Circle` (open circle), and `Bar` (perpendicular stroke). Every
vertex comes from the shared `NotationMetrics` geometry (for example `TriangleVertices()`,
`DiamondVertices()`, `EndMarkerRefX`, `CrossbarX`, `CircleRadius`, `BarAlong`/`BarAcross`), so
the PNG markers are geometrically identical to the SVG markers built from the same source.

**`RenderLabel(SKCanvas canvas, LayoutLabel label, RenderOptions options)`**

Draws text using `CreateTextPaint` with the weight and style from `label.Weight` and
`label.Style`, font size from `label.FontSize`, and alignment from `label.Align`. Calls
`FitFontSize` when `label.MaxWidth > 0`.

**`RenderPort(SKCanvas canvas, LayoutPort port, RenderOptions options)`**

Draws an 8×8-pixel filled square (filled with the stroke color) centered at
`(CentreX, CentreY)`. Optional label is offset away from the attached `PortSide`.

**`RenderBadge(SKCanvas canvas, LayoutBadge badge, RenderOptions options)`**

Draws the badge shape centered at `(CentreX, CentreY)` within a bounding circle of
radius `Size/2`. Shapes: `FilledCircle` (solid circle), `Bullseye` (filled circle with
white inner circle), `Diamond` (rotated open square), `HorizontalBar` (horizontal stroke),
`VerticalBar` (vertical stroke). Optional label drawn to the right.

**`RenderBand(SKCanvas canvas, LayoutBand band, RenderOptions options)`**

Draws a filled and stroked rectangle using `DepthFillColors[0]`. For `Horizontal`
orientation the label is rendered with 90° CCW rotation along the left edge using
`canvas.Save/Translate/RotateDegrees/Restore`. For `Vertical` orientation the label is
horizontal at the top. Children are rendered recursively.

**`RenderLifeline(SKCanvas canvas, LayoutLifeline lifeline, RenderOptions options)`**

Draws a header box centered at `CentreX` (top at `TopY`, size `HeaderWidth × HeaderHeight`)
filled with `DepthFillColors[0]`. Draws a bold centered label in the box. Draws a dashed
vertical stem (`SKPathEffect.CreateDash`) from the bottom of the header to `BottomY`.

**`RenderActivation(SKCanvas canvas, LayoutActivation activation, RenderOptions options)`**

Draws a white-filled rectangle of width `LabelPadding * 2` centered at `CentreX`,
spanning `TopY` to `BottomY`, with a stroke border.

**`RenderGrid(SKCanvas canvas, LayoutGrid grid, RenderOptions options)`**

Iterates rows and cells, accumulating X/Y positions. Each cell gets a filled background
(`DepthFillColors[1]` for header rows, `DepthFillColors[0]` for body rows), a stroke
border, and a text element vertically centered within the row height and horizontally
aligned per `LayoutGridCell.Align`. Header cells use bold weight; body cells use regular.

### Error Handling

`Render` throws `ArgumentNullException` when `layout`, `options`, or `output` is null.
`SKColor.Parse` throws `ArgumentException` for malformed hex strings; this is a programming
error in the theme definition, not a user input error.

### Dependencies

- `DemaConsulting.SysML2Tools` — provides `IRenderer`, `LayoutTree`, all nine `LayoutNode`
  subtypes, `RenderOptions`, `Theme`
- `SkiaSharp` (OTS) — provides `SKBitmap`, `SKCanvas`, `SKImage`, `SKPaint`, `SKColor`,
  `SKPath`, `SKPathEffect`, `SKTypeface`, `SKData`
- Noto Sans (OTS, SIL OFL 1.1) — four static TTF files embedded as assembly resources

### Callers

- `DiagramRenderer.RenderWorkspace` (in `DemaConsulting.SysML2Tools`) — passes the
  renderer to `IRenderer.Render` as part of the orchestrated rendering pipeline
- `RenderCommand.RunAsync` (in `DemaConsulting.SysML2Tools.Tool`) — creates a
  `PngRenderer` instance when `--format png` is selected

## Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| SysML2Tools-Core-Rendering-IRenderer | `IRenderer` implementation in `PngRenderer` |
| SysML2Tools-Core-Rendering-IRendererStateless | `PngRenderer` is a stateless, pure class |
| SysML2Tools-Png-MediaType | `PngRenderer.MediaType` property |
| SysML2Tools-Png-DefaultExtension | `PngRenderer.DefaultExtension` property |
| SysML2Tools-Png-Render-Signature | `PngRenderer.Render` encodes via SkiaSharp PNG |
| SysML2Tools-Png-Render-Box | `RenderBox` draws filled and stroked rectangle |
