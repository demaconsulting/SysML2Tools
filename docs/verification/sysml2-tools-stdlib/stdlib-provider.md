### StdlibProvider Verification

#### Verification Approach

`StdlibProvider` is verified by unit tests in `StdlibProviderTests` (in the
`DemaConsulting.SysML2Tools.Tests` project). Tests call `StdlibProvider.GetSymbolTable` directly and
assert on the returned `SymbolTable`, its cached identity, its lookup performance, and its diagnostics
list. No mocking is required; the unit's public method is exercised against the embedded binary.

#### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. The standard library binary is
embedded in the assembly; no files on disk, network access, or additional configuration are required
beyond a standard .NET SDK installation.

#### Acceptance Criteria

- `GetSymbolTable` returns a non-empty `SymbolTable` containing known standard library types.
- Repeated calls return the same cached `SymbolTable` instance.
- After the first call, a subsequent call returns in under 50 milliseconds.
- A non-null diagnostics list is returned alongside the `SymbolTable`.

#### Test Scenarios

| Test | Assertion |
| --- | --- |
| `GetSymbolTable_ReturnsNonEmpty` | The returned symbol table is non-empty |
| `GetSymbolTable_ContainsKnownStdlibTypes` | Known standard library types are present |
| `GetSymbolTable_IsCached` | Repeated calls return the same instance |
| `GetSymbolTable_FastOnSubsequentCalls` | A cached call completes in under 50 ms |
| `GetSymbolTable_DiagnosticsNotNull` | A non-null diagnostics list is returned |
