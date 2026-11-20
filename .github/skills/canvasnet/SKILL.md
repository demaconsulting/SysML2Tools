---
name: canvasnet
description: Create, render, and visually verify diagrams and figures (SVG, Mermaid, Vega-Lite, OpenSCAD) with the CanvasNet tool, including framed figures with callouts and pictures in Pandoc documents. Use this when asked to draw, chart, or illustrate something for documentation, or when a diagram source in the repository needs to be updated or re-rendered.
---

# CanvasNet Skill

CanvasNet draws SVG drawings, Mermaid diagrams (`.mmd`), Vega-Lite charts (`.vl.json`), OpenSCAD
models (`.scad`), and pages of PDF, PPTX, and VSDX documents, so an agent can create a picture,
look at the result, and refine it. For *when* a diagram belongs in a document, follow
`.github/standards/diagram-documentation.md`. This skill covers how to use the tool and where its
own help is.

## Prerequisites

`canvasnet` is a local .NET tool (`DemaConsulting.CanvasNet.Tool`) pinned in
`.config/dotnet-tools.json`; add it to the manifest if it is missing. It is only available
from inside the repository:

```pwsh
dotnet tool restore
dotnet canvasnet --help
```

## Learn the tool from its own help

The help always matches the pinned version and is a tree of short cards. Each card ends with
*See also* links to the next cards worth reading. Do not rely on memory or on this skill for
format details:

```pwsh
dotnet canvasnet help topics          # every card, one line each
dotnet canvasnet help mermaid         # a format's index card; then, e.g., help mermaid flowchart
dotnet canvasnet help style           # making pictures look good, linking to each format's style card
dotnet canvasnet help composition     # figures that combine diagrams, charts, models, and pages
dotnet canvasnet help <card> --examples
```

Before authoring, read the card for the format and the diagram type, particularly what is not
supported, and follow its *See also* links to the style and composition cards.

## Workflow

1. Write the source file next to where it will be used: beside the document that references it,
   or in `docs/images/`.
2. Render it with `--strict` to a scratch preview, so that unsupported constructs fail instead of
   being silently skipped:

   ```pwsh
   dotnet canvasnet render docs/images/name.svg -o preview.png --strict
   ```

3. **View the PNG** and critique it: clipped or overlapping text, uneven spacing, misaligned
   elements, unreadable labels, poor contrast. Fix the source and render again. The first render is
   rarely the final one.
4. When the picture will be published on a page of unknown theme, check it on a dark background
   too:

   ```pwsh
   dotnet canvasnet render docs/images/name.svg -o preview-dark.png --background "#0d1117" --strict
   ```

5. Decide what is committed, following the diagram standard:
   - **A picture in a Pandoc document**: commit only the source; the document references it and
     the pipeline draws it.
   - **A figure used outside the Pandoc pipeline** (for example the README): render the final PNG
     beside the source and commit both:

     ```pwsh
     dotnet canvasnet render docs/images/name.svg -o docs/images/name.png --strict
     ```

   Delete previews and other scratch output when done.

## Framed Figures and Callouts

For a figure with a frame, a title band, captions, and callouts that point at features of an
embedded diagram, chart, or model, read `dotnet canvasnet help svg framed-figure`. To place a
callout, use these in order of preference:

1. **Position bindings** (`help svg bindings`): an attribute such as
   `cn:x2y2="#flow node:send"` makes an arrow end exactly on that feature of the embedded picture.
2. **Reported positions** (`help positions`): `render --positions <selector> --json` reports where
   Mermaid and Vega-Lite features are drawn; OpenSCAD models report their anchors.
3. **Measuring** the rendered picture with `render --grid` and `inspect pixel`.

An SVG that embeds a diagram, chart, model, or document page can only be drawn by CanvasNet.
Publish its PNG, or write a copy that any SVG reader shows with `dotnet canvasnet flatten`.
`help svg fragments` covers choosing a page, a model view, a variable, or a theme of an embedded
picture.

## In Pandoc Documents

The documentation pipeline's `docs/filters/canvasnet.lua` draws pictures with the tool, so a
document can reference the source directly: `![Flow](flow.mmd)`, `![Sales](sales.vl.json)`,
`![Part](part.scad){view=iso}`, or a fenced code block of class `mermaid`. Attributes, captions,
and the file-access variables are in `dotnet canvasnet help pandoc`. The drawn pictures are cached
in `docs/generated/canvasnet/`, which is never committed.

The filter file is a shim written by the tool: never edit it by hand. If the tool reports that
the shim's protocol changed, write it again:

```pwsh
dotnet canvasnet pandoc-filter --write-shim docs/filters/canvasnet.lua
```

## Habits

- **Transparent output.** Rendering flattens vector sources over white by default. Pass
  `--background transparent` when the diagram supplies its own card or background.
- **Rounded card.** For a diagram that must look right in light and dark themes, draw a rounded
  white `<rect>` with a thin border behind the content and render transparent.
- **Solid colors, not opacity.** The PDF/A pipeline (WeasyPrint) ignores `opacity` and
  `fill-opacity` on SVG shapes, so a translucent shadow turns solid black. Use solid colors for
  anything that must appear in a generated PDF. Vega-Lite marks may have their own default opacity
  (circles are translucent); set it explicitly if needed.
- **Resolution.** Render PNGs wide enough to stay sharp when scaled (for example `--width 1800`
  for a diagram displayed at about 900 px).
- **Keep the files together.** Keep data files, includes, and embedded pictures in the source's
  folder or below, so the figure moves with its folder and still draws when a build restricts file
  access (`help files`). Do not reference remote data.
- **Derive, don't invent.** Take facts shown in a diagram (names, relationships, values) from the
  source code or the SysML2 model, not from memory.

## Troubleshooting

- *Tool not found*: run from inside the repository after `dotnet tool restore`.
- *A feature fails under `--strict`*: it is not supported; check the card's help for the supported
  alternative rather than dropping `--strict`.
- *Render looks wrong but exits 0*: you rendered without `--strict`; render again with it.
- *The Pandoc build says the shim's protocol changed*: write the shim again (see above).
