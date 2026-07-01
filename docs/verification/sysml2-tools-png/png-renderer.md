### PngRenderer Verification

#### Verification Approach

`PngRenderer` is verified by unit tests in `PngRendererTests` and `PngEndMarkerTests` (in the
`DemaConsulting.SysML2Tools.Png.Tests` project) plus the renderer-contract tests in `RenderingTests`
(in `DemaConsulting.SysML2Tools.Tests`). Tests construct a `PngRenderer`, render a small `LayoutTree`,
and assert on the decoded PNG - checking the signature bytes and sampling individual pixels. No mocking
is required; the unit's public `Render` method is exercised end to end.

#### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. Layout trees are constructed inline;
no files on disk, network access, or additional configuration are required beyond a standard .NET SDK
installation and the SkiaSharp native assets restored with the package.

#### Acceptance Criteria

- `MediaType` is `"image/png"` and `DefaultExtension` is `".png"`.
- Any layout tree, including an empty one, yields a valid PNG that begins with the PNG signature and
  has a white background.
- Each box rasterizes as a filled, stroked rectangle whose fill follows the theme depth colours, and
  deeply nested boxes render without exhausting the stack.
- Label text rasterizes to a valid PNG even when it contains XML-special characters.
- Each connector line rasterizes in the theme stroke colour.
- Port, badge, band, lifeline, activation, and grid nodes each rasterize with their notation colours.
- Connector end markers rasterize with geometry derived from `NotationMetrics`.

#### Test Scenarios

| Test | Assertion |
| --- | --- |
| `PngRenderer_MediaType_IsImagePng` | MediaType is `image/png` |
| `PngRenderer_DefaultExtension_IsDotPng` | DefaultExtension is `.png` |
| `PngRenderer_Render_EmptyTree_WritesPngBytes` | Empty tree yields non-empty PNG bytes |
| `PngRenderer_Render_EmptyTree_WritesPngSignature` | Output begins with the PNG signature |
| `PngRenderer_Render_BackgroundIsWhite` | The background pixel is white |
| `PngRenderer_Render_SingleBox_ProducesNonEmptyOutput` | A box renders without error |
| `PngRenderer_Render_SingleBox_FillColorMatchesTheme` | Box fill matches the theme colour |
| `PngRenderer_Render_SingleBox_DepthOneUsesSecondColor` | Depth-one box uses the second depth colour |
| `PngRenderer_Render_DeeplyNestedBoxes_DoesNotStackOverflow` | Deep nesting renders without overflow |
| `PngRenderer_Render_LabelWithXmlSpecialCharacters_ProducesValidPng` | Special-character text yields a valid PNG |
| `PngRenderer_Render_SingleLine_PixelOnLineIsStrokeColor` | A line pixel is the stroke colour |
| `PngRenderer_Render_SinglePort_CenterPixelIsStrokeColor` | A port centre pixel is the stroke colour |
| `PngRenderer_Render_SingleBadge_FilledCircle_CenterPixelIsStrokeColor` | A badge centre pixel is the stroke colour |
| `PngRenderer_Render_SingleLifeline_StemPixelIsStrokeColor` | A lifeline stem pixel is the stroke colour |
| `PngRenderer_Render_SingleActivation_CenterPixelIsWhite` | An activation centre pixel is white |
| `PngRenderer_Render_SingleGrid_HeaderFillMatchesTheme` | A grid header fill matches the theme |
| `PngRenderer_Render_DrawArrowhead_OpenWithCrossbar_ProducesNonEmptyOutput` | Crossbar marker renders without error |
| `FilledArrow_AlongLength_MatchesNotationMetrics` | Filled arrow length matches NotationMetrics |
| `FilledArrow_BaseWidth_MatchesNotationMetrics` | Filled arrow base width matches NotationMetrics |
| `OpenChevron_HasFewerInkPixelsThanClosedTriangle` | Open chevron inks fewer pixels than a closed triangle |
