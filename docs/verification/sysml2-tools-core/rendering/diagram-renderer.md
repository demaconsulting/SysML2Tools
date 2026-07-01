### DiagramRenderer Verification

#### Verification Approach

`DiagramRenderer` is verified through integration tests in `RenderIntegrationTests` that load a real
workspace (from a model file and from inline SysML source), run `RenderWorkspace` with both the SVG
and PNG renderers, and assert on the produced outputs — including that element names appear in the
SVG text and that the PNG output carries the PNG file signature. The empty-workspace case is verified
by a dedicated test that asserts an empty result.

#### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. The integration tests load the
embedded standard library through `StdlibProvider` and read the `test/SysMLModels` fixtures; no
external services are required.

#### Acceptance Criteria

- All `RenderIntegrationTests` pass with zero failures across all target frameworks.
- Rendering a workspace with views produces one output per view, containing the rendered elements.
- The same workspace renders successfully with both the SVG and PNG renderers.
- A workspace with no views produces an empty result.

#### Test Scenarios

| Test | Assertion |
| --- | --- |
| `DiagramRenderer_RenderWorkspace_SoftwareStructureModel_ReturnsSvgOutput` | A loaded model renders to SVG output |
| `DiagramRenderer_RenderWorkspace_SoftwareStructureModel_PngRenderer_ReturnsPngOutput` | Same model renders to PNG |
| `DiagramRenderer_RenderWorkspace_GeneralViewModel_SvgContainsElementNames` | The SVG output contains element names |
| `DiagramRenderer_RenderWorkspace_GeneralViewModel_PngProducesValidOutput` | The PNG output carries the PNG signature |
| `DiagramRenderer_RenderWorkspace_NoViews_ReturnsEmptyList` | A view-free workspace yields an empty result |
