### NotationMetrics Verification

#### Verification Approach

`NotationMetrics` is verified through unit tests in `NotationMetricsTests` that pin the canonical
notation values and prove every end-marker shape is a documented derivation of named metrics. The
tests map the tip-relative marker vertices back to the historical SVG marker-box points, confirming
that no geometry literal has drifted. The unit is pure, so no mocking is required.

#### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

#### Acceptance Criteria

- All `NotationMetricsTests` pass with zero failures across all target frameworks.
- The triangle, diamond, circle, bar, and crossbar constants match their canonical values.
- The triangle apex overshoots the endpoint and the diamond far point lands on the endpoint.
- Each end-marker style reports the documented along-line length.
- The rounded-rectangle radius equals the theme radius scaled by the documented factor.

#### Test Scenarios

| Test | Assertion |
| --- | --- |
| `TriangleFamily_HasCanonicalValues` | Triangle-family constants match the historical 10x7 refX 9 marker |
| `Diamond_HasCanonicalValues` | Diamond constants match the historical 14x8 refX 13 marker |
| `CircleAndBar_HaveCanonicalValues` | Circle (r4) and bar (4x12) constants match their canonical values |
| `Crossbar_IsDerivedFraction` | The crossbar position is the documented fraction of the marker length |
| `LabelBackground_ExtentMatchesInset` | The label-background extent is symmetric about the documented inset |
| `TriangleVertices_ReproduceSvgBoxPoints` | Triangle vertices reproduce the SVG marker-box points |
| `DiamondVertices_ReproduceSvgBoxPoints` | Diamond vertices reproduce the SVG marker-box points |
| `DiamondVertices_FarPoint_LandsOnEndpoint` | The diamond far point lands on the line endpoint |
| `TriangleVertices_Apex_OvershootsEndpoint` | The triangle apex overshoots the endpoint by the documented amount |
| `AlongLineLength_MatchesMarkerBox` | Each end-marker style reports the documented along-line length |
| `RoundedRectRadius_IsThemeRadiusTimesFactor` | The rounded-rectangle radius is the theme radius times the factor |
