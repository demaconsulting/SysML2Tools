# DemaConsulting.SysML2Tools.Language

## Verification Approach

System-level verification for the `DemaConsulting.SysML2Tools.Language` library uses unit tests
in `DemaConsulting.SysML2Tools.Tests`. Tests exercise the public `WorkspaceParser`, `WorkspaceLoader`,
`AstSerializer`, and `AstDeserializer` APIs. The xUnit v3 framework discovers and runs all
test methods; results are captured in TRX files consumed by ReqStream.

## Test Environment

Tests run via `dotnet test` against all three target frameworks: net8.0, net9.0, and net10.0.
No external services, files, or environment configuration are required beyond a standard .NET
SDK installation.

## Acceptance Criteria

- All unit tests pass with zero failures across all three target frameworks.
- `WorkspaceParser` correctly propagates the caller-supplied file path in all diagnostics.
- Invalid SysML syntax produces at least one `Error`-severity diagnostic.
- `WorkspaceLoader` correctly registers qualified names from SysML packages and definitions.
- Unresolved supertype references produce `Warning`-severity diagnostics.
- Circular imports between two files produce at least one `Warning`-severity diagnostic.
- `AstSerializer.Serialize` followed by `AstDeserializer.Deserialize` round-trips all six
  node types, node properties, and diagnostics without loss.
- `WorkspaceLoader.LoadAsync` with a `seedSymbolTable` correctly incorporates seed symbols
  into the resolved workspace.

## Test Scenarios

Primary acceptance evidence is provided by:

- `WorkspaceLoader_LoadAsync_StdlibDeclarations_Registered` — loads stdlib seed, asserts
  `HasErrors` is false and `Declarations` is non-empty.
- `WorkspaceLoader_LoadAsync_ParseError_ReturnsError` — malformed SysML produces Error diagnostic.
- `WorkspaceLoader_LoadAsync_CircularImport_ProducesWarningNoInfiniteLoop` — two files with
  circular imports; asserts cycle detection terminates and emits Warning.
- `AstSerializerTests.Serialize_EmptyTable_RoundTrips` — empty table serializes to empty table.
- `AstSerializerTests.Serialize_AllNodeTypes_RoundTrip` — all six node types round-trip correctly.
- `AstSerializerTests.Serialize_Diagnostics_RoundTrip` — diagnostics round-trip with severity preserved.
- `AstSerializerTests.Serialize_SupertypeAndImportedNames_Preserved` — supertype and import
  name lists round-trip without loss.
