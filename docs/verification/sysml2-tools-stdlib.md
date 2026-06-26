# DemaConsulting.SysML2Tools.Stdlib

## Verification Approach

System-level verification for the `DemaConsulting.SysML2Tools.Stdlib` library uses unit tests
in `DemaConsulting.SysML2Tools.Tests`. Tests exercise the `StdlibProvider.GetSymbolTable()` API
and validate that the embedded stdlib is correctly deserialized and cached. The xUnit v3 framework
discovers and runs all test methods; results are captured in TRX files consumed by ReqStream.

## Test Environment

Tests run via `dotnet test` against all three target frameworks: net8.0, net9.0, and net10.0.
No external services, files, or environment configuration are required beyond a standard .NET
SDK installation. The stdlib binary is embedded in the Stdlib assembly and available at test time
without any additional setup.

## Acceptance Criteria

- All unit tests pass with zero failures across all three target frameworks.
- `StdlibProvider.GetSymbolTable()` returns a non-empty `SymbolTable`.
- Two successive calls to `GetSymbolTable()` return the same cached instance (same reference).
- The second call to `GetSymbolTable()` completes in under 50 milliseconds (cached access).
- The stdlib `SymbolTable` contains at least 50 standard declarations.
- The diagnostics returned by `GetSymbolTable()` are not null.

## Test Scenarios

Primary acceptance evidence is provided by:

- `StdlibProviderTests.GetSymbolTable_ReturnsNonEmpty` — stdlib table is not empty.
- `StdlibProviderTests.GetSymbolTable_IsCached` — two calls return the same instance.
- `StdlibProviderTests.GetSymbolTable_FastOnSubsequentCalls` — cached call completes in < 50ms.
- `StdlibProviderTests.GetSymbolTable_ContainsKnownStdlibTypes` — table has > 50 declarations.
- `StdlibProviderTests.GetSymbolTable_DiagnosticsNotNull` — diagnostics list is not null.
