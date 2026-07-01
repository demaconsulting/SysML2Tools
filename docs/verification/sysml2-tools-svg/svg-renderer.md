### SvgRenderer Verification

#### Verification Approach

`SvgRenderer` is verified by unit tests in `SvgRendererTests` and `SvgEndMarkerTests` (in the
`DemaConsulting.SysML2Tools.Svg.Tests` project) plus the renderer-contract tests in `RenderingTests`
(in `DemaConsulting.SysML2Tools.Tests`). Tests construct a `SvgRenderer`, render a small `LayoutTree`,
and assert on the emitted SVG text - parsing it as XML to confirm the output is well formed. No mocking
is required; the unit's public `Render` method is exercised end to end.

#### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. Layout trees are constructed inline;
no files on disk, network access, or additional configuration are required beyond a standard .NET SDK
installation.

#### Acceptance Criteria

- `MediaType` is `"image/svg+xml"` and `DefaultExtension` is `".svg"`.
- Any layout tree, including an empty one, yields a well-formed SVG 1.1 document with a root svg
  element.
- Each box renders as a rect (with `rx` when rounded and inner line/text for a compartment).
- Each label renders as a text element, with bold/italic styling applied and XML-special characters
  escaped so the document stays well-formed.
- Each connector line renders as a path, honouring corner radius, dashed styling, and midpoint labels.
- Port, badge, band, lifeline, activation, and grid nodes each render with their notation elements.
- Connector end markers are emitted with geometry derived from `NotationMetrics`.

#### Test Scenarios

| Test | Assertion |
| --- | --- |
| `SvgRenderer_MediaType_IsImageSvgXml` | MediaType is `image/svg+xml` |
| `SvgRenderer_DefaultExtension_IsDotSvg` | DefaultExtension is `.svg` |
| `SvgRenderer_Render_EmptyTree_ProducesSvgDocument` | Empty tree yields an svg root element |
| `SvgRenderer_Render_SingleBox_ProducesRectElement` | A box yields a rect |
| `SvgRenderer_Render_BoxRoundedRectangle_ProducesRxAttribute` | A rounded box yields an `rx` |
| `SvgRenderer_Render_BoxWithCompartment_ProducesLineAndText` | A compartment yields a line and text |
| `SvgRenderer_Render_SingleLabel_ProducesTextElement` | A label yields a text element |
| `SvgRenderer_Render_LabelWithBold_ProducesBoldAttribute` | Bold styling is applied |
| `SvgRenderer_Render_LabelWithItalic_ProducesItalicAttribute` | Italic styling is applied |
| `SvgRenderer_Render_LabelWithXmlSpecialCharacters_ProducesWellFormedEscapedSvg` | Text escaped; SVG parses |
| `SvgRenderer_Render_SingleLine_ProducesPathElement` | A line yields a path |
| `SvgRenderer_Render_SingleLine_WithCornerRadius_ProducesArcInPath` | Rounded corners yield an arc |
| `SvgRenderer_Render_SingleLine_Dashed_ProducesDashArray` | Dashed lines yield a dash array |
| `SvgRenderer_Render_LineWithMidpointLabel_ProducesTextElement` | A midpoint label yields text |
| `SvgRenderer_Render_SinglePort_ProducesRect` | A port yields a rect |
| `SvgRenderer_Render_SingleBadge_FilledCircle_ProducesCircle` | A badge yields a circle |
| `SvgRenderer_Render_SingleBand_ProducesRect` | A band yields a rect |
| `SvgRenderer_Render_SingleLifeline_ProducesRectAndLine` | A lifeline yields a rect and line |
| `SvgRenderer_Render_SingleActivation_ProducesRect` | An activation yields a rect |
| `SvgRenderer_Render_SingleGrid_ProducesRects` | A grid yields rects |
| `SvgRenderer_Render_SingleLine_WithOpenArrowhead_ProducesMarkerEnd` | Open arrowhead yields a marker-end |
| `SvgRenderer_Render_SingleLine_WithDiamondArrowhead_ProducesDiamondMarker` | Diamond yields a diamond marker |
| `SvgRenderer_Render_SingleLine_WithOpenCrossbarArrowhead_ProducesOpenCrossbarMarker` | Crossbar yields its marker |
| `OpenChevron_IsDefinedAsPolyline` | Open chevron marker is a polyline |
| `HollowTriangle_IsDefinedAsClosedPolygon` | Hollow triangle marker is a closed polygon |
| `TriangleMarker_DimensionsDeriveFromNotationMetrics` | Triangle geometry matches NotationMetrics |
| `DiamondMarker_DimensionsDeriveFromNotationMetrics` | Diamond geometry matches NotationMetrics |
| `OpenChevronLine_ReferencesOpenChevronMarker` | A chevron line references the chevron marker |
