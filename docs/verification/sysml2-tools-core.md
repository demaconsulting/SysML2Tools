# DemaConsulting.SysML2Tools

## Verification Approach

System-level verification for the `DemaConsulting.SysML2Tools` core library uses unit tests
in `DemaConsulting.SysML2Tools.Tests`. Tests exercise the public `WorkspaceParser` and
`WorkspaceLoader` APIs and validate that the embedded stdlib parses without errors and that
the semantic workspace is populated correctly. The xUnit v3 framework discovers and runs all
test methods; results are captured in TRX files consumed by ReqStream.

## Test Environment

Tests run via `dotnet test` against all three target frameworks: net8.0, net9.0, and net10.0.
No external services, files, or environment configuration are required beyond a standard .NET
SDK installation.

## Acceptance Criteria

- All unit tests pass with zero failures across all three target frameworks.
- All 94 embedded stdlib files are included in parse results; KerML parse errors are downgraded
  to Warnings so they do not affect `HasErrors`.
- `WorkspaceParser` correctly propagates the caller-supplied file path in all diagnostics.
- Invalid SysML syntax produces at least one `Error`-severity diagnostic.
- `WorkspaceLoader` correctly registers qualified names from SysML packages and definitions.
- Unresolved supertype references produce `Warning`-severity diagnostics.

## Test Scenarios

See *Parser Verification Design* and *Semantic Verification Design* for the full list of
test scenarios. Primary acceptance evidence is provided by:

- `Parse_StdlibOnly_NoErrors` — parses all 94 stdlib files, asserts `HasErrors` is false.
- `WorkspaceLoader_LoadAsync_StdlibDeclarations_Registered` — loads all stdlib files, asserts
  `HasErrors` is false and `Declarations` is non-empty.
