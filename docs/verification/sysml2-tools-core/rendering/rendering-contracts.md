### RenderingContracts Verification

#### Verification Approach

The `RenderingContracts` unit is verified through unit tests in `RenderingTests` that exercise the
shipped `IRenderer` implementations (media type, extension, and empty-tree byte output), construct
`RenderOptions` and `RenderOutput` and assert their fields and defaults, and construct a `ViewContext`
for the `ILayoutStrategy` contract. `StdlibFilter` is verified indirectly through the integration
render of a user model, which produces output only for the user's own views.

#### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. The renderer tests use in-memory
streams; no external services, files, or configuration are required beyond a standard .NET SDK
installation.

#### Acceptance Criteria

- All `RenderingTests` pass with zero failures across all target frameworks.
- The SVG and PNG renderers report the correct media type and extension and write valid bytes for an
  empty tree.
- `RenderOptions` defaults are Scale 1.0, Dpi 96, and DepthLimit 0.
- `RenderOutput` and `ViewContext` store their fields unchanged.
- A user-model render contains only user-defined views, confirming stdlib filtering.

#### Test Scenarios

| Test | Assertion |
| --- | --- |
| `SvgRenderer_MediaType_IsImageSvgXml` | The SVG renderer reports `image/svg+xml` |
| `SvgRenderer_DefaultExtension_IsDotSvg` | The SVG renderer reports `.svg` |
| `SvgRenderer_Render_EmptyTree_WritesValidSvg` | An empty tree renders to a valid SVG document |
| `PngRenderer_MediaType_IsImagePng` | The PNG renderer reports `image/png` |
| `PngRenderer_DefaultExtension_IsDotPng` | The PNG renderer reports `.png` |
| `PngRenderer_Render_EmptyTree_WritesPngBytes` | An empty tree renders to PNG signature bytes |
| `ViewContext_Construction_StoresAllFields` | A ViewContext stores its view name and workspace |
| `RenderOptions_DefaultScale_IsOne` | RenderOptions defaults Scale to 1.0 |
| `RenderOptions_DefaultDpi_Is96` | RenderOptions defaults Dpi to 96 |
| `RenderOptions_DefaultDepthLimit_IsZero` | RenderOptions defaults DepthLimit to 0 |
| `RenderOutput_Construction_StoresAllFields` | A RenderOutput stores its three fields |
| `DiagramRenderer_RenderWorkspace_SoftwareStructureModel_ReturnsSvgOutput` | StdlibFilter limits output to user views |
