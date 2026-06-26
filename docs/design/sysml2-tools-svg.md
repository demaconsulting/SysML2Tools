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

### Key Methods

**`Render(LayoutTree layout, RenderOptions options, Stream output)`**

Entry point. Validates arguments, computes canvas size clamped to a minimum of 1×1,
writes the SVG root element with `xmlns`, `width`, `height`, and `viewBox` attributes,
then calls `WriteArrowheadDefs` followed by recursive `RenderNode` calls for every
top-level node. Encodes the completed `StringBuilder` as UTF-8 and writes all bytes
to `output` in a single `Write` call.

**`RenderNode(StringBuilder sb, LayoutNode node, Theme theme, double scale)`**

Dispatches by concrete node type: `LayoutBox` → `RenderBox`, `LayoutLine` → `RenderLine`,
`LayoutLabel` → `RenderLabel`. Unknown subtypes are silently skipped for forward
compatibility.

**`RenderBox(StringBuilder sb, LayoutBox box, Theme theme, double scale)`**

Writes a `<rect>` element using fill color from `theme.DepthFillColors[box.Depth % count]`.
Writes a `<text>` element centered horizontally and positioned in the title area when
`box.Label` is non-null. Recursively calls `RenderNode` for all `box.Children`.

**`RenderLine(StringBuilder sb, LayoutLine line, Theme theme, double scale)`**

Writes a `<path>` element with `M`/`L` commands for each waypoint segment. Adds
`marker-start` or `marker-end` attributes for `Open` and `Filled` arrowhead styles.
Adds `stroke-dasharray` for `Dashed` and `Dotted` line styles. Lines with fewer than
two waypoints are skipped.

**`RenderLabel(StringBuilder sb, LayoutLabel label, Theme theme, double scale)`**

Writes a `<text>` element with `text-anchor` derived from `label.Align`.

**`WriteArrowheadDefs(StringBuilder sb, Theme theme)`**

Writes the SVG `<defs>` block containing two named marker elements: `arrowhead-open`
(hollow polygon) and `arrowhead-filled` (filled polygon). Both use `theme.StrokeColor`
and `theme.StrokeWidth`.

### Error Handling

`Render` throws `ArgumentNullException` when `layout`, `options`, or `output` is null.
No other exceptions are expected under normal operation. XML special characters in labels
are escaped via `EscapeXml` (replaces `&`, `<`, `>` with XML entities) to prevent
malformed SVG output.

### Dependencies

- `DemaConsulting.SysML2Tools` — provides `IRenderer`, `LayoutTree`, `LayoutBox`,
  `LayoutLine`, `LayoutLabel`, `RenderOptions`, `Theme`

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
