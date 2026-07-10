# DemaConsulting.SysML2Tools

## Verification Approach

System-level verification for the `DemaConsulting.SysML2Tools` core library uses unit tests
in `DemaConsulting.SysML2Tools.Tests`. Tests exercise the Filtering, Layout, and Rendering
pipeline via `FilterExpressionParser`, `FilterExpressionEvaluator`, `DiagramRenderer`, and
`GeneralViewLayoutStrategy`, along with the shared `ExposeScopeResolver`-based expose-scoping
path exercised by all seven layout strategies. The xUnit v3 framework discovers and runs all
test methods; results are captured in TRX files consumed by ReqStream.

## Test Environment

Tests run via `dotnet test` against all three target frameworks: net8.0, net9.0, and net10.0.
No external services, files, or environment configuration are required beyond a standard .NET
SDK installation.

## Acceptance Criteria

- All unit tests pass with zero failures across all three target frameworks.
- `FilterExpressionParser` and `FilterExpressionEvaluator` correctly narrow candidate elements for
  the supported Phase 1 standalone view-filter subset and degrade safely on unsupported input.
- `DiagramRenderer.RenderWorkspace` correctly renders views declared in a `SysmlWorkspace`.
- `GeneralViewLayoutStrategy` produces a valid `LayoutTree` for a given `ViewContext`.
- Every layout strategy honors a view's resolved `expose` scope via the shared
  `ExposeScopeResolver` helper, including the no-`expose` fallback (rendering everything
  unchanged) and a multi-target `expose` union.

## Test Scenarios

Primary acceptance evidence is provided by:

- `FilterExpressionParserTests` / `FilterExpressionEvaluatorTests` — direct filtering subsystem
  tests.
- `RenderIntegrationTests` — end-to-end rendering tests with stdlib seed workspace.
- `GeneralViewLayoutStrategyTests` — layout algorithm tests for general view diagrams.
