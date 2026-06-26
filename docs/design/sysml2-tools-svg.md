# DemaConsulting.SysML2Tools.Svg

## Overview

`DemaConsulting.SysML2Tools.Svg` is the SVG renderer system for SysML2 Tools. It provides
a single public class, `SvgRenderer`, that translates a `LayoutTree` into a self-contained
SVG 1.1 document written to an output stream. The renderer has zero external runtime
dependencies beyond the .NET base class library.

## SvgRenderer

### Purpose

`SvgRenderer` implements the `IRenderer` interface to produce SVG 1.1 diagram output
from a `LayoutTree` intermediate representation. Each call to `Render` builds a complete
SVG document in a `StringBuilder` and writes it to the supplied stream in UTF-8 encoding.
The renderer is pure and stateless; no fields are mutated between calls.

### Data Model

`SvgRenderer` has no instance state. All inputs are supplied through `Render` parameters.

- `LayoutTree` — read-only input; canvas dimensions and node list
- `RenderOptions` — read-only input; `Theme` for visual parameters, `Scale` for sizing
- `Stream output` — write-only output; receives UTF-8 SVG bytes; caller owns lifetime

### Font Family

All text elements use `font-family="Noto Sans, sans-serif"`. The `Noto Sans` family is
specified first so that browsers and renderers with Noto Sans installed use it; `sans-serif`
is the CSS generic fallback. This replaced the earlier `Segoe UI, sans-serif` default to
provide consistent rendering across all platforms.

### Font Weight and Style Per Node Type

Each node type uses a fixed font weight and style as SVG attributes:

| Node Type | `font-weight` | `font-style` |
| --- | --- | --- |
| `LayoutBox` label | `bold` | (default) |
| `LayoutBoxCompartment` title | `bold` | `italic` |
| `LayoutBoxCompartment` rows | (default) | (default) |
| `LayoutLine` midpoint label | (default) | (default) |
| `LayoutLabel` | Per `FontWeight` field | Per `FontStyle` field |
| `LayoutPort` label | (default) | (default) |
| `LayoutBadge` label | (default) | (default) |
| `LayoutBand` label | (default) | (default) |
| `LayoutLifeline` label | `bold` | (default) |
| `LayoutGrid` header cells | `bold` | (default) |
| `LayoutGrid` body cells | (default) | (default) |

### LayoutLabel Font Styling Fields

`LayoutLabel` carries three explicit font styling fields added in Phase 4:

- `Weight` (`FontWeight`) — `Regular` maps to `font-weight="normal"`; `Bold` maps to `font-weight="bold"`.
- `Style` (`FontStyle`) — `Normal` maps to `font-style="normal"`; `Italic` maps to `font-style="italic"`.
- `FontSize` (double) — Font size in logical pixels, used as `font-size` instead of the theme body size.

### Text Length Shrink-to-Fit

`LayoutBox` labels include `textLength` and `lengthAdjust="spacingAndGlyphs"` attributes
set to `(box.Width - 2 * theme.LabelPadding) * scale`. This instructs SVG renderers to
compress or stretch glyph spacing so the text fills (or shrinks into) the available title
area without overflow.

`LayoutLabel` nodes with `MaxWidth > 0` similarly include
`textLength="{MaxWidth * scale}" lengthAdjust="spacingAndGlyphs"`.

### Key Methods

**`Render(LayoutTree layout, RenderOptions options, Stream output)`**

Entry point. Validates arguments, computes canvas size clamped to a minimum of 1×1,
writes the SVG root element with `xmlns`, `width`, `height`, and `viewBox` attributes,
then calls `WriteArrowheadDefs` followed by recursive `RenderNode` calls for every
top-level node. Encodes the completed `StringBuilder` as UTF-8 and writes all bytes
to `output` in a single `Write` call.

**`WriteArrowheadDefs(StringBuilder sb, Theme theme)`**

Writes the SVG `<defs>` block containing six named marker elements: `arrowhead-open`
(hollow triangle), `arrowhead-filled` (solid triangle), `arrowhead-diamond` (hollow
four-point polygon), `arrowhead-filled-diamond` (solid four-point polygon),
`arrowhead-circle` (open circle), and `arrowhead-bar` (perpendicular line). All markers
use `theme.StrokeColor` and `theme.StrokeWidth`.

**`RenderNode(StringBuilder sb, LayoutNode node, Theme theme, double scale)`**

Dispatches by concrete node type to the appropriate typed render method. All nine
`LayoutNode` subtypes are handled: `LayoutBox` → `RenderBox`, `LayoutLine` → `RenderLine`,
`LayoutLabel` → `RenderLabel`, `LayoutPort` → `RenderPort`, `LayoutBadge` → `RenderBadge`,
`LayoutBand` → `RenderBand`, `LayoutLifeline` → `RenderLifeline`,
`LayoutActivation` → `RenderActivation`, `LayoutGrid` → `RenderGrid`. Unknown subtypes
are silently skipped for forward compatibility.

**`RenderBox(StringBuilder sb, LayoutBox box, Theme theme, double scale)`**

Writes a `<rect>` element using fill color from `theme.DepthFillColors[box.Depth % count]`.
Adds `rx`/`ry` attributes when `BoxShape.RoundedRectangle` and `LineCornerRadius > 0`.
Writes a bold `<text>` element with `textLength` in the title area when `box.Label` is
non-null. Calls `RenderBoxCompartments` for any compartments, then recursively calls
`RenderNode` for all `box.Children`.

**`RenderBoxCompartments(StringBuilder sb, LayoutBox box, Theme theme, double scale)`**

Writes a `<line>` divider across the full box width at the top of each compartment,
followed by an optional `font-weight="bold" font-style="italic"` `<text>` title row and
zero or more left-aligned regular-weight body-font `<text>` rows.

**`RenderLine(StringBuilder sb, LayoutLine line, Theme theme, double scale)`**

Calls `BuildLinePath` to produce the path `d` attribute, then writes a `<path>` element
with `fill="none"`. Adds `marker-start` or `marker-end` attributes for all six non-None
`ArrowheadStyle` values. Adds `stroke-dasharray` for `Dashed` and `Dotted` line styles.
Writes an optional midpoint `<text>` element when `MidpointLabel` is non-null.

**`BuildLinePath(IReadOnlyList<Point2D> waypoints, double cornerRadius, double scale)`**

Builds the SVG path `d` string. When `cornerRadius` is zero, emits plain `M`/`L`
commands. When positive, each interior waypoint is replaced with a shortened `L` command
to the arc start point, followed by an `A` (elliptical arc) command whose sweep direction
(0 or 1) is determined from the cross product of the incoming and outgoing unit direction
vectors. The radius is clamped to half the shorter adjacent segment to prevent overshoot.

**`RenderLabel(StringBuilder sb, LayoutLabel label, Theme theme, double scale)`**

Writes a `<text>` element with `text-anchor` from `label.Align`, `font-size` from
`label.FontSize`, `font-weight` and `font-style` from `label.Weight` and `label.Style`.
When `label.MaxWidth > 0`, adds `textLength` and `lengthAdjust="spacingAndGlyphs"`.

**`RenderPort(StringBuilder sb, LayoutPort port, Theme theme, double scale)`**

Writes a filled 8×8 `<rect>` centered at `(CentreX, CentreY)`. Optional label is written
as a `<text>` element offset away from the attached `PortSide`.

**`RenderBadge(StringBuilder sb, LayoutBadge badge, Theme theme, double scale)`**

Writes shape-specific SVG elements: `<circle>` for `FilledCircle` and `Bullseye` (the
latter with an additional white inner circle), `<polygon>` for `Diamond`, and `<line>`
for `HorizontalBar` and `VerticalBar`. An optional label is written as `<text>`.

**`RenderBand(StringBuilder sb, LayoutBand band, Theme theme, double scale)`**

Writes a `<rect>` with `DepthFillColors[0]` fill. For `Horizontal` bands writes a
`<text>` with `transform="translate(...) rotate(-90)"` on the left edge; for `Vertical`
bands writes a horizontal `<text>` at the top. Children are rendered recursively.

**`RenderLifeline(StringBuilder sb, LayoutLifeline lifeline, Theme theme, double scale)`**

Writes a `<rect>` header centered at `CentreX` filled with `DepthFillColors[0]`, a
bold centered `<text>` label, and a dashed `<line>` stem (`stroke-dasharray="6 3"`) from
the bottom of the header to `BottomY`.

**`RenderActivation(StringBuilder sb, LayoutActivation activation, Theme theme, double scale)`**

Writes a `<rect>` with `fill="white"` and a stroke border. Width is `LabelPadding * 2`,
centered at `CentreX`, spanning `TopY` to `BottomY`.

**`RenderGrid(StringBuilder sb, LayoutGrid grid, Theme theme, double scale)`**

Iterates rows and cells, accumulating X/Y positions. Each cell gets a `<rect>` filled
with `DepthFillColors[1]` for header rows or `DepthFillColors[0]` for body rows, plus a
stroke border, plus a `<text>` vertically centered in the row and aligned per
`LayoutGridCell.Align`. Header cells have `font-weight="bold"` added conditionally;
body cells use the browser default.

### Error Handling

`Render` throws `ArgumentNullException` when `layout`, `options`, or `output` is null.
No other exceptions are expected under normal operation. XML special characters in labels
are escaped via `EscapeXml` (replaces `&`, `<`, `>` with XML entities) to prevent
malformed SVG output.

### Dependencies

- `DemaConsulting.SysML2Tools` — provides `IRenderer`, `LayoutTree`, all nine `LayoutNode`
  subtypes, `RenderOptions`, `Theme`

### Callers

- `DiagramRenderer.RenderWorkspace` (in `DemaConsulting.SysML2Tools`) — passes the
  renderer to `IRenderer.Render` as part of the orchestrated rendering pipeline
- `RenderCommand.RunAsync` (in `DemaConsulting.SysML2Tools.Tool`) — creates a
  `SvgRenderer` instance when `--format svg` is selected

## Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| SysML2Tools-Core-Rendering-IRenderer | `IRenderer` implementation in `SvgRenderer` |
| SysML2Tools-Core-Rendering-IRendererStateless | `SvgRenderer` is a stateless, pure class |
| SysML2Tools-Svg-MediaType | `SvgRenderer.MediaType` property |
| SysML2Tools-Svg-DefaultExtension | `SvgRenderer.DefaultExtension` property |
| SysML2Tools-Svg-Render-Document | `SvgRenderer.Render` writes SVG root element |
| SysML2Tools-Svg-Render-Box | `RenderBox` writes `<rect>` element |
| SysML2Tools-Svg-Render-Label | `RenderLabel` writes `<text>` element |
| SysML2Tools-Svg-Render-Line | `RenderLine` writes `<path>` element |
