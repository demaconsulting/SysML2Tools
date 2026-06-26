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

### Key Methods

**`Render(LayoutTree layout, RenderOptions options, Stream output)`**

Entry point. Validates arguments. Computes bitmap width and height as
`(int)Math.Ceiling(layout.Width * options.Scale)`, clamped to a minimum of 1×1 pixels
to prevent SkiaSharp allocation errors on empty trees. Creates `SKBitmap(w, h, Rgba8888, Premul)`,
fills background with `SKColors.White`, calls `RenderNode` for each top-level node,
then encodes via `SKImage.FromBitmap(bitmap).Encode(Png, 100)` and saves to `output`.

**`RenderNode(SKCanvas canvas, LayoutNode node, RenderOptions options)`**

Dispatches by concrete node type: `LayoutBox` → `RenderBox`, `LayoutLine` → `RenderLine`,
`LayoutLabel` → `RenderLabel`. Unknown subtypes are skipped.

**`RenderBox(SKCanvas canvas, LayoutBox box, RenderOptions options)`**

Draws a filled rectangle using the fill color from `theme.DepthFillColors[depth % count]`
(parsed via `SKColor.Parse`). Draws a stroke rectangle using `theme.StrokeColor` and
`theme.StrokeWidth`. Draws centered text using `SKPaint.TextAlign = Center` when
`box.Label` is non-null. Recursively calls `RenderNode` for all `box.Children`.

**`RenderLine(SKCanvas canvas, LayoutLine line, RenderOptions options)`**

Draws each segment between consecutive waypoints using `canvas.DrawLine`. Configures
`SKPathEffect.CreateDash` for `Dashed` and `Dotted` line styles. Lines with fewer
than two waypoints are skipped.

**`RenderLabel(SKCanvas canvas, LayoutLabel label, RenderOptions options)`**

Draws text using `SKPaint.TextAlign` mapped from `label.Align` (Left/Center/Right).

### Error Handling

`Render` throws `ArgumentNullException` when `layout`, `options`, or `output` is null.
`SKColor.Parse` throws `ArgumentException` for malformed hex strings; this is a programming
error in the theme definition, not a user input error.

### Dependencies

- `DemaConsulting.SysML2Tools` — provides `IRenderer`, `LayoutTree`, `LayoutBox`,
  `LayoutLine`, `LayoutLabel`, `RenderOptions`, `Theme`
- `SkiaSharp` (OTS) — provides `SKBitmap`, `SKCanvas`, `SKImage`, `SKPaint`, `SKColor`

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
