# DemaConsulting.SysML2Tools

## Verification Approach

System-level verification for the `DemaConsulting.SysML2Tools` core library uses unit tests
in `DemaConsulting.SysML2Tools.Tests`. Tests exercise the public `WorkspaceParser` API and
validate that the embedded stdlib parses without errors. The xUnit v3 framework discovers
and runs all test methods; results are captured in TRX files consumed by ReqStream.

## Test Environment

Tests run via `dotnet test` against all three target frameworks: net8.0, net9.0, and net10.0.
No external services, files, or environment configuration are required beyond a standard .NET
SDK installation.

## Acceptance Criteria

- All unit tests pass with zero failures across all three target frameworks.
- All 58 embedded `.sysml` stdlib files parse without producing any error-severity diagnostics.
- `WorkspaceParser` correctly propagates the caller-supplied file path in all diagnostics.
- Invalid SysML syntax produces at least one `Error`-severity diagnostic.

## Test Scenarios

See *Parser Verification Design* for the full list of test scenarios. Primary acceptance
evidence is provided by `Parse_StdlibOnly_NoErrors`, which parses all 58 embedded stdlib
`.sysml` files and asserts `HasErrors` is false.
