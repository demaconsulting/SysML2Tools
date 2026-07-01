### WorkspaceParser Verification

#### Verification Approach

`WorkspaceParser` is verified by unit tests in `WorkspaceParserTests` in the
`DemaConsulting.SysML2Tools.Tests` project. Tests call `WorkspaceParser.ParseSource` directly with
controlled inputs and assert on the returned `IReadOnlyList<SysmlDiagnostic>`. No mocking is
required; the unit's public method is exercised end to end.

#### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. Source text is supplied inline as
string literals; no files on disk, network access, or additional configuration are required beyond
a standard .NET SDK installation.

#### Acceptance Criteria

- An empty source string produces zero diagnostics.
- A minimal valid package declaration produces zero diagnostics.
- A valid part definition with an attribute declaration produces zero diagnostics.
- Invalid SysML syntax produces at least one `Error`-severity diagnostic.
- The file path supplied to `ParseSource` appears verbatim in every returned diagnostic.

#### Test Scenarios

| Test | Assertion |
| --- | --- |
| `ParseSource_EmptyFile_NoErrors` | Empty source yields an empty diagnostic list |
| `ParseSource_MinimalPackage_NoErrors` | `package MyPackage {}` yields no diagnostics |
| `ParseSource_PartDef_NoErrors` | A package with a `part def` block yields no diagnostics |
| `ParseSource_InvalidSyntax_ReportsError` | Invalid source yields an Error-severity diagnostic |
| `ParseSource_ErrorPath_MatchesSuppliedPath` | Every diagnostic carries the supplied file path |
