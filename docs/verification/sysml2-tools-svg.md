# DemaConsulting.SysML2Tools.Svg Verification

## Verification Approach

The SVG renderer is verified using unit tests in
`test/DemaConsulting.SysML2Tools.Svg.Tests/SvgRendererTests.cs`. Tests construct
`LayoutTree` inputs directly, invoke `SvgRenderer.Render`, and inspect the output
stream for expected SVG elements. No filesystem access is required; all I/O uses
`MemoryStream`. Tests run against all three target frameworks (net8.0, net9.0, net10.0).

## Test Environment

- Framework: xUnit v3
- Target frameworks: net8.0, net9.0, net10.0
- Test project: `DemaConsulting.SysML2Tools.Svg.Tests`
- Dependencies: `DemaConsulting.SysML2Tools.Svg`, `DemaConsulting.SysML2Tools`

## Acceptance Criteria

- `SvgRenderer.MediaType` returns `"image/svg+xml"`
- `SvgRenderer.DefaultExtension` returns `".svg"`
- `Render` with any `LayoutTree` produces a non-empty stream containing `<svg` and `</svg>`
- `Render` with a tree containing a `LayoutBox` produces a stream containing `<rect`
- `Render` with a tree containing a `LayoutLabel` produces a stream containing `<text`
- `Render` with a tree containing a `LayoutLine` produces a stream containing `<path`
- `Render` with a tree containing a `LayoutLine` using the open-with-crossbar arrowhead
  style produces a stream containing `arrowhead-open-crossbar`

## Test Scenarios

### SvgRenderer_Render_EmptyTree_ProducesSvgDocument

Verifies that an empty `LayoutTree` produces a non-empty SVG document with `<svg` and
`</svg>` tags. Confirms basic document structure for the trivial case.

### SvgRenderer_Render_SingleBox_ProducesRectElement

Verifies that a `LayoutTree` containing one `LayoutBox` produces SVG output containing
a `<rect` element. Confirms that the box-to-rectangle translation is applied.

### SvgRenderer_Render_SingleLabel_ProducesTextElement

Verifies that a `LayoutTree` containing one `LayoutLabel` produces SVG output containing
a `<text` element and the label text. Confirms that standalone labels are translated
to SVG text nodes.

### SvgRenderer_Render_SingleLine_ProducesPathElement

Verifies that a `LayoutTree` containing one `LayoutLine` produces SVG output containing
a `<path` element. Confirms that lines are translated to SVG path elements.

### SvgRenderer_Render_SingleLine_WithOpenCrossbarArrowhead_ProducesOpenCrossbarMarker

Verifies that a `LayoutTree` containing a `LayoutLine` with `TargetArrowhead` set to
`ArrowheadStyle.OpenWithCrossbar` produces SVG output containing the string
`arrowhead-open-crossbar`. Confirms that the open-with-crossbar marker is defined in the
defs block and referenced by the line path element.
