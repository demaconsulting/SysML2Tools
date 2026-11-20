---
name: Diagram Documentation
description: Follow these standards when deciding whether, and how, to add diagrams to documentation.
globs: ["docs/**/*.md", "README.md", "docs/images/**", "!docs/**/generated/**"]
---

# Diagram Documentation Standards

A diagram is a tool for the reader, not decoration. Add one only when it explains
something faster or more accurately than prose, a table, or a code sample would. Tool
mechanics (commands, formats, flags) are in the `canvasnet` skill, not here.

# When a Diagram Earns Its Place

Add a diagram when it:

- shows **structure or flow** that is hard to follow in text (components and their
  relationships, data conversion paths, state or outcome taxonomies)
- presents **quantitative data** where shape matters (a chart or matrix)
- gives a first-time reader a **mental model** before the detail starts

Do not add a diagram when it:

- repeats what the adjacent text or table already says clearly
- is decorative (logos, banners, illustrations with no information)
- would need to be maintained in many places for little reader benefit
- depicts something that is better generated from the authoritative source (see below)

# Budget and Placement

- Prefer one or two diagrams per document. If more seem necessary, the document probably
  needs restructuring.
- Place the diagram where the reader needs it, after a lead-in sentence that says what
  to look for, and refer to it from the text.
- Keep the README lean: a single overview diagram is typical.
- Every image has meaningful alt text describing what it shows, not "diagram". Never
  rely on color alone to convey meaning.

# Choosing the Form

- **Authoritative software structure** lives in the SysML2 model under `docs/sysml2/`.
  Do not hand-draw a competing description of the same structure; a diagram derived from
  it is fine, and the model wins when they disagree.
- **Simple flows and relationships** in design or verification documents may use
  Mermaid: a fenced code block of class `mermaid`, or a `.mmd` file referenced as a picture,
  which the documentation pipeline's CanvasNet filter draws.
- **Illustrative or marketing-quality figures** (README, user guide): SVG.
- **Data charts**: VegaLite, with the data file kept beside the spec.
- **3D or physical objects**: OpenSCAD. Rare for software documentation.
- **Composite figures** (charts, models, or document pages arranged with labels): an SVG
  that embeds the other sources.

# Source and Rendered Output

- **Always commit the source** (`.svg`, `.mmd`, `.scad`, `.vl.json`, and any data it uses). The
  source is the maintainable artifact and is reviewed like code.
- **In documents built by the Pandoc pipeline, reference the source directly** (for example
  `![Flow](flow.mmd)` or `![Sales](sales.vl.json)`) and commit no PNG: the CanvasNet filter draws
  the picture during the build and caches it under `docs/generated/canvasnet/`, which is never
  committed. Keep such a source beside the document that uses it, or in `docs/images/`, with its
  embedded assets and data in that folder or below.
- **Commit the rendered PNG only when a consumer outside the Pandoc pipeline cannot use the source
  directly.** For example, the README is published in the NuGet package, which does not reliably
  render external SVG, so its image is a committed PNG.
- Composite SVGs that embed `.mmd`, `.scad`, `.vl.json`, or document files can only be drawn
  by CanvasNet: outside the Pandoc pipeline a PNG (or a flattened SVG) is always required for them.
- For a figure with a committed PNG, keep the source and the PNG together in `docs/images/` with
  matching base names (`name.svg` and `name.png`).
- In every case keep embedded assets and data in the source's folder or below, so a figure moves
  with its folder and still draws when a build restricts file access. This does not apply to
  generator-managed outputs (for example SysML2 views rendered into `docs/design/generated/`),
  which stay where their generator writes them.
- Render with `--strict` and review the result visually before committing. When a figure has a
  committed PNG, regenerate it in the same commit whenever the source changes so the two never
  disagree.
- Images referenced from a README that ships in a package use absolute URLs, per the
  README's own conventions.

# Maintenance

- A diagram that no longer matches the code is worse than no diagram. When a change
  alters what a diagram shows, update the diagram in the same change.
- Remove a diagram when it stops earning its place.
- Do not commit scratch files, previews, or test renders.
