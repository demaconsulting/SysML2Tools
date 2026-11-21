### WorkspaceParser Verification

#### Verification Approach

`WorkspaceParser` is verified by unit tests in `WorkspaceParserTests` in the
`DemaConsulting.SysML2Tools.Tests` project. Tests call `WorkspaceParser.ParseSource` directly with
controlled inputs and assert on the returned `IReadOnlyList<SysmlDiagnostic>`. No mocking is
required; the unit's public method is exercised end to end.

#### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, net10.0, and net11.0. Source text is supplied inline as
string literals; no files on disk, network access, or additional configuration are required beyond
a standard .NET SDK installation.

#### Acceptance Criteria

- An empty source string produces zero diagnostics.
- A minimal valid package declaration produces zero diagnostics.
- A valid part definition with an attribute declaration produces zero diagnostics.
- Invalid SysML syntax produces at least one `Error`-severity diagnostic.
- The file path supplied to `ParseSource` appears verbatim in every returned diagnostic.
- Every KerML package member production (classifier kinds, relationship statements, feature
  elements, and `step`/`expr`/`bool`/`inv` without a declaration) parses with zero diagnostics in a
  package body and at the root of a file.
- SysML usages named like KerML keywords, and the words `entry`/`do`/`exit`/`accept`/`frame` in
  state, action, and requirement bodies, keep their SysML parse.
- A classification test used as the bare operand of `not`, `-`, `+`, or `~` (`filter not @X;`,
  `... and not @X`, `[not @X]`) produces exactly one `Error` diagnostic at the `@` token's line and
  column; the parenthesized form (`not (@X)`) and a classification test that is an operand of
  `and`/`or` parse with zero diagnostics.

#### Test Scenarios

| Test | Assertion |
| --- | --- |
| `ParseSource_EmptyFile_NoErrors` | Empty source yields an empty diagnostic list |
| `ParseSource_MinimalPackage_NoErrors` | `package MyPackage {}` yields no diagnostics |
| `ParseSource_PartDef_NoErrors` | A package with a `part def` block yields no diagnostics |
| `ParseSource_InvalidSyntax_ReportsError` | Invalid source yields an Error-severity diagnostic |
| `ParseSource_ErrorPath_MatchesSuppliedPath` | Every diagnostic carries the supplied file path |
| `ParseSource_KermlNamespaceMemberAtPackageLevel_NoErrors` | Each KerML member form parses cleanly |
| `ParseSource_KermlMembersAtRootNamespace_NoErrors` | KerML members at the file root parse cleanly |
| `ParseSource_SysmlUsagesNamedLikeKermlKeywords_NoErrors` | SysML usages named like KerML keywords parse |
| `ParseSource_SysmlKeywordsAlsoUsedAsKermlNames_KeepSysmlMeaning` | `entry`/`do`/`exit`/`accept`/`frame` parse |
| `ParseSource_UnaryOperatorWithBareClassificationTest_ReportsSyntaxErrorAtAtSign` | `not @X`, `-@X`: error at `@` |
| `ParseSource_ExposeFilterWithNotBareClassificationTest_ReportsSyntaxError` | `[not @X]` gives an Error diagnostic |
| `ParseSource_UnaryOperatorWithParenthesizedClassificationTest_NoErrors` | `not (@X)` forms parse cleanly |
