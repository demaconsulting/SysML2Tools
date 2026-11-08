---
name: canvasnet
description: Create, render, and visually verify diagrams (SVG, OpenSCAD, VegaLite) with the CanvasNet tool. Use this when asked to draw, chart, or illustrate something for documentation, or when a diagram source in the repository needs to be updated or re-rendered.
---

# CanvasNet Skill

CanvasNet renders SVG, OpenSCAD (`.scad`), and VegaLite (`.vl.json`) sources to PNG so
an agent can create a diagram, look at the result, and refine it. For *when* a diagram
belongs in a document, follow `.github/standards/diagram-documentation.md`. This skill
covers only how to use the tool.

## Prerequisites

`canvasnet` is a local .NET tool (`DemaConsulting.CanvasNet.Tool`) pinned in
`.config/dotnet-tools.json`; add it to the manifest if it is missing. It is only available
from inside the repository:

```pwsh
dotnet tool restore
dotnet canvasnet --help
```

## Learn the tool from its own help

The tool documents itself and the help always matches the pinned version. Do not rely on
memory or on this skill for format details:

```pwsh
dotnet canvasnet formats            # supported inputs and outputs
dotnet canvasnet help svg           # supported SVG subset, embedding rules, limits
dotnet canvasnet help scad          # supported OpenSCAD features
dotnet canvasnet help vega-lite     # supported VegaLite features
dotnet canvasnet help <topic> --examples
```

Read the topic help before authoring in a format, particularly the unsupported-feature
lists, and look at an example if the format is unfamiliar.

## Workflow

1. Write the source file next to where it will be used (normally `docs/images/`).
2. Render it, using `--strict` so unsupported constructs fail instead of being silently
   skipped:

   ```pwsh
   dotnet canvasnet render docs/images/name.svg -o docs/images/name.png --strict
   ```

3. **View the PNG** and critique it: clipped or overlapping text, uneven spacing,
   misaligned elements, unreadable labels, poor contrast. Fix the source and re-render.
   Repeat until it is right; the first render is rarely the final one.
4. Check the result on both a light and a dark background when the PNG will be
   published on a page of unknown theme:

   ```pwsh
   dotnet canvasnet render docs/images/name.svg -o preview.png --background "#0d1117" --strict
   ```

   Delete previews and other scratch output when done.

## Habits

- **Transparent output.** Rendering flattens vector sources over white by default. Pass
  `--background transparent` when the diagram supplies its own card or background.
- **Rounded card.** For a diagram that must look right in light and dark themes, draw a
  rounded white `<rect>` with a thin border behind the content and render transparent.
- **Solid colors, not opacity.** The PDF/A pipeline (WeasyPrint) ignores `opacity` and
  `fill-opacity` on SVG shapes, so a translucent shadow turns solid black. Use solid
  colors for anything that must appear in a generated PDF. VegaLite marks may have
  their own default opacity (circles are translucent); set it explicitly if needed.
- **Resolution.** Render PNGs wide enough to stay sharp when scaled (for example
  `--width 1800` for a diagram displayed at about 900 px).
- **Keep the data with the chart.** VegaLite data files and OpenSCAD includes must live in
  the source's folder or below; do not reference remote data.
- **Derive, don't invent.** Take facts shown in a diagram (names, relationships, values)
  from the source code or the SysML2 model, not from memory.

## SVG as a layout engine

An SVG may embed other sources with `<image href="...">`: rasters, other SVGs, OpenSCAD
models, VegaLite charts, and documents (PDF, PPTX, VSDX). This lets you compose panels,
labels, and arrows around charts and models in a single figure. Constraints:

- Only CanvasNet can render such an SVG. Browsers and GitHub cannot resolve the embedded
  `.scad`, `.vl.json`, or document references. Always render the composed SVG to PNG and
  publish the PNG.
- Embedded files must be in the SVG's folder or below.
- For how to select a page of a PDF, PPTX, or VSDX document, and for the file-count,
  nesting, and pixel limits, run `dotnet canvasnet help svg`.

## Troubleshooting

- *Tool not found*: run from inside the repository after `dotnet tool restore`.
- *A feature fails under `--strict`*: it is not supported; check the topic's help for the
  supported alternative rather than dropping `--strict`.
- *Render looks wrong but exits 0*: you rendered without `--strict`; re-run with it.
