# DemaConsulting.SysML2Tools

## Verification Approach

System-level verification for the `DemaConsulting.SysML2Tools` core library uses unit tests
in `DemaConsulting.SysML2Tools.Tests`. Tests exercise the Layout and Rendering pipeline via
`DiagramRenderer` and `GeneralViewLayoutStrategy`. The xUnit v3 framework discovers and runs all
test methods; results are captured in TRX files consumed by ReqStream.

## Test Environment

Tests run via `dotnet test` against all three target frameworks: net8.0, net9.0, and net10.0.
No external services, files, or environment configuration are required beyond a standard .NET
SDK installation.

## Acceptance Criteria

- All unit tests pass with zero failures across all three target frameworks.
- `DiagramRenderer.RenderWorkspace` correctly renders views declared in a `SysmlWorkspace`.
- `GeneralViewLayoutStrategy` produces a valid `LayoutTree` for a given `ViewContext`.

## Test Scenarios

Primary acceptance evidence is provided by:

- `RenderIntegrationTests` — end-to-end rendering tests with stdlib seed workspace.
- `GeneralViewLayoutStrategyTests` — layout algorithm tests for general view diagrams.
