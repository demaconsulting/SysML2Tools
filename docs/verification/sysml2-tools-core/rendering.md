## DemaConsulting.SysML2Tools — Rendering Subsystem Verification

### Verification Approach

The Rendering subsystem is verified by unit tests in `DemaConsulting.SysML2Tools.Tests`.
Tests assert that stub implementations (`SvgRenderer`, `PngRenderer`, `DiagramRenderer`)
throw `NotImplementedException` as documented, that the `Themes` static properties are
non-null and correctly initialized, and that `RenderOptions` default parameter values
match their documented defaults. No I/O or filesystem access is required.

### Test Environment

Tests run via `dotnet test` against all three target frameworks: net8.0, net9.0, and net10.0.
All test inputs are constructed inline. No external network access or services are required.

### Acceptance Criteria

- All unit tests pass with zero failures across all three target frameworks.
- `SvgRenderer.MediaType` returns `"image/svg+xml"`.
- `SvgRenderer.DefaultExtension` returns `".svg"`.
- `SvgRenderer.Render` throws `NotImplementedException` with message containing "Phase 4".
- `PngRenderer.MediaType` returns `"image/png"`.
- `PngRenderer.DefaultExtension` returns `".png"`.
- `PngRenderer.Render` throws `NotImplementedException` with message containing "Phase 4".
- `DiagramRenderer.RenderWorkspace` throws `NotImplementedException` with message containing "Phase 4".
- `Themes.Light` is non-null and has `DepthFillColors.Count >= 1` and non-empty `StrokeColor`.
- `Themes.Dark` is non-null and has `DepthFillColors.Count >= 1` and non-empty `StrokeColor`.
- `Themes.Print` is non-null and has `DepthFillColors.Count >= 1` and non-empty `StrokeColor`.
- `new RenderOptions(Themes.Light)` has `Scale == 1.0`, `Dpi == 96.0`, `DepthLimit == 0`.
- `RenderOutput` stores `SuggestedFileName`, `MediaType`, and `Data` as supplied.

### Test Scenarios

**SvgRenderer_MediaType_IsImageSvgXml**: A `SvgRenderer` instance is constructed; the
`MediaType` property is asserted to equal `"image/svg+xml"`. This confirms the renderer
identifies itself with the correct MIME type for SVG output.

**SvgRenderer_DefaultExtension_IsDotSvg**: A `SvgRenderer` instance is constructed; the
`DefaultExtension` property is asserted to equal `".svg"`. This confirms the renderer
provides a correct default extension for output file naming.

**SvgRenderer_Render_ThrowsNotImplemented**: A `SvgRenderer` instance is constructed and
`Render` is called with an empty `LayoutTree`, default `RenderOptions`, and a
`MemoryStream`; a `NotImplementedException` is expected. This confirms the Phase 4
deferral stub behavior.

**PngRenderer_MediaType_IsImagePng**: A `PngRenderer` instance is constructed; the
`MediaType` property is asserted to equal `"image/png"`.

**PngRenderer_DefaultExtension_IsDotPng**: A `PngRenderer` instance is constructed; the
`DefaultExtension` property is asserted to equal `".png"`.

**PngRenderer_Render_ThrowsNotImplemented**: A `PngRenderer` instance is constructed and
`Render` is called with an empty `LayoutTree`, default `RenderOptions`, and a
`MemoryStream`; a `NotImplementedException` is expected.

**DiagramRenderer_RenderWorkspace_ThrowsNotImplemented**: A `DiagramRenderer` instance is
constructed and `RenderWorkspace` is called with a non-null `SysmlWorkspace`, a
`SvgRenderer`, and default `RenderOptions`; a `NotImplementedException` is expected.
This confirms the Phase 4 deferral stub behavior.

**Themes_Light_IsInitialized**: `Themes.Light` is accessed; the result is asserted to be
non-null, `DepthFillColors` to have at least one element, and `StrokeColor` to be
non-empty. This confirms that static property initialization does not throw.

**Themes_Dark_IsInitialized**: `Themes.Dark` is accessed; the result is asserted to be
non-null, `DepthFillColors` to have at least one element, and `StrokeColor` to be
non-empty.

**Themes_Print_IsInitialized**: `Themes.Print` is accessed; the result is asserted to be
non-null, `DepthFillColors` to have at least one element, and `StrokeColor` to be
non-empty.

**RenderOptions_DefaultScale_IsOne**: A `RenderOptions` is constructed with only the
required `Theme` parameter; the `Scale` property is asserted to equal `1.0`.

**RenderOptions_DefaultDpi_Is96**: A `RenderOptions` is constructed with only the required
`Theme` parameter; the `Dpi` property is asserted to equal `96.0`.

**RenderOptions_DefaultDepthLimit_IsZero**: A `RenderOptions` is constructed with only the
required `Theme` parameter; the `DepthLimit` property is asserted to equal `0`, confirming
the unlimited rendering sentinel value.

**RenderOutput_Construction_StoresAllFields**: A `RenderOutput` is constructed with
`SuggestedFileName = "diagram.svg"`, `MediaType = "image/svg+xml"`, and a `MemoryStream`;
all three properties are asserted to equal the supplied values.

**ILayoutStrategy_BuildLayout_ReturnsLayoutTree**: This scenario is deferred to Phase 4
when a concrete `ILayoutStrategy` implementation is available. The scenario will construct
a `ViewContext` with a minimal `SysmlWorkspace` and assert that `BuildLayout` returns a
non-null `LayoutTree` with non-negative `Width` and `Height`.
