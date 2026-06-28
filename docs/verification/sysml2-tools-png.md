# DemaConsulting.SysML2Tools.Png Verification

## Verification Approach

The PNG renderer is verified using unit tests in
`test/DemaConsulting.SysML2Tools.Png.Tests/PngRendererTests.cs`. Tests construct
`LayoutTree` inputs directly, invoke `PngRenderer.Render`, and inspect the output
stream for PNG signature bytes and non-zero length. No filesystem access is required;
all I/O uses `MemoryStream`. Tests run against all three target frameworks.

## Test Environment

- Framework: xUnit v3
- Target frameworks: net8.0, net9.0, net10.0
- Test project: `DemaConsulting.SysML2Tools.Png.Tests`
- Dependencies: `DemaConsulting.SysML2Tools.Png`, `DemaConsulting.SysML2Tools`, SkiaSharp

## Acceptance Criteria

- `PngRenderer.MediaType` returns `"image/png"`
- `PngRenderer.DefaultExtension` returns `".png"`
- `Render` with any `LayoutTree` produces a non-empty stream whose first four bytes
  are `0x89 0x50 0x4E 0x47` (PNG magic number)
- `Render` with a tree containing a `LayoutBox` produces a non-empty PNG output
  without throwing
- `Render` with a tree containing a `LayoutLine` using the open-with-crossbar arrowhead
  style produces a non-empty PNG output without throwing

## Test Scenarios

### PngRenderer_Render_EmptyTree_WritesPngSignature

Verifies that an empty `LayoutTree` (zero canvas size) produces a stream beginning
with the four-byte PNG signature `0x89 0x50 0x4E 0x47`. Confirms that SkiaSharp
produces valid PNG output even for the minimal 1×1 bitmap case.

### PngRenderer_Render_SingleBox_ProducesNonEmptyOutput

Verifies that a `LayoutTree` containing one `LayoutBox` produces a non-empty PNG
stream with the PNG signature. Confirms that box drawing operations complete without
error and produce a rasterized output.

### PngRenderer_Render_DrawArrowhead_OpenWithCrossbar_ProducesNonEmptyOutput

Verifies that a `LayoutTree` containing a `LayoutLine` with `TargetArrowhead` set to
`ArrowheadStyle.OpenWithCrossbar` produces a non-empty PNG output stream beginning with
the PNG signature bytes. Confirms that the open-with-crossbar arrowhead style renders
without throwing and produces valid PNG output.
