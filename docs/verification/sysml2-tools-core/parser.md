## Parser

### Verification Approach

The `Parser` subsystem is verified by unit tests in `WorkspaceParserTests.cs` and integration
tests in `OmgModelsTests.cs` in the `DemaConsulting.SysML2Tools.Tests` project. Tests call
`WorkspaceParser.ParseAsync` and `WorkspaceParser.ParseSource` directly with controlled inputs
and assert on the returned `Task<WorkspaceParseResult>` and `IReadOnlyList<SysmlDiagnostic>`
values. No mocking is required; `SysmlDiagnosticListener` and `StdlibLoader` are exercised
through the public API.

### Test Environment

The `OmgModelsTests` class requires the `test/SysMLModels/OMG/` directory to be present in the
repository checkout. The directory contains 251 OMG reference `.sysml` files checked into the
repository under a MIT-compatible license. No external network access or services are required.
The test assembly locates the directory by walking up from `AppContext.BaseDirectory` to the
repository root.

### Acceptance Criteria

- All nine unit tests pass with zero failures across net8.0, net9.0, and net10.0.
- An empty source string produces zero diagnostics.
- A valid SysML package declaration produces zero diagnostics.
- A valid SysML part definition produces zero diagnostics.
- Invalid SysML syntax produces at least one `Error`-severity diagnostic.
- The file path supplied to `ParseSource` appears verbatim in all returned diagnostics.
- All 58 embedded `.sysml` stdlib files parse without any error-severity diagnostic.
- `WorkspaceParser.ParseAsync([])` returns a `Files` list containing at least 58 entries.
- All 251 OMG reference model files in `test/SysMLModels/OMG/` parse without any error-severity
  diagnostic.
- The `test/SysMLModels/OMG/` directory contains at least 251 `.sysml` files.

### Test Scenarios

**ParseSource_EmptyFile_NoErrors**: `WorkspaceParser.ParseSource` is called with virtual path
`"<test>"` and an empty string; the returned diagnostic list is empty. This scenario is tested
by `ParseSource_EmptyFile_NoErrors`.

**ParseSource_MinimalPackage_NoErrors**: `WorkspaceParser.ParseSource` is called with
`"package MyPackage {}"` as the source; the returned diagnostic list is empty, confirming that
a minimal valid package declaration parses without errors. This scenario is tested by
`ParseSource_MinimalPackage_NoErrors`.

**ParseSource_PartDef_NoErrors**: `WorkspaceParser.ParseSource` is called with a multi-line
source containing a package with a `part def` block including an attribute declaration; the
returned diagnostic list is empty, confirming that valid nested SysML constructs parse
correctly. This scenario is tested by `ParseSource_PartDef_NoErrors`.

**ParseSource_InvalidSyntax_ReportsError**: `WorkspaceParser.ParseSource` is called with
`"@@@ NOT VALID SYSML @@@"`; the returned diagnostic list is non-empty and contains at least
one diagnostic with `Severity == DiagnosticSeverity.Error`, confirming that lexer errors are
surfaced as `Error`-severity diagnostics. This scenario is tested by
`ParseSource_InvalidSyntax_ReportsError`.

**ParseSource_ErrorPath_MatchesSuppliedPath**: `WorkspaceParser.ParseSource` is called with
path `"my-model.sysml"` and invalid source `"@@@"`; every diagnostic in the returned list has
`FilePath == "my-model.sysml"`, confirming that the virtual path is propagated correctly. This
scenario is tested by `ParseSource_ErrorPath_MatchesSuppliedPath`.

**Parse_StdlibOnly_NoErrors**: `WorkspaceParser.ParseAsync` is called with an empty file collection;
`result.HasErrors` is false, confirming that all 58 embedded `.sysml` stdlib files parse
without error. Any failures print the full diagnostic list to the assertion message. This
scenario is tested by `Parse_StdlibOnly_NoErrors`.

**Parse_FilesCount_IncludesStdlib**: `WorkspaceParser.ParseAsync` is called with an empty file
collection; `result.Files.Count` is at least 58, confirming that the stdlib loader enumerates
all `.sysml` resources. This scenario is tested by `Parse_FilesCount_IncludesStdlib`.

**Parse_OmgModels_NoSyntaxErrors**: All 251 OMG reference `.sysml` files are discovered from
`test/SysMLModels/OMG/` and passed to `WorkspaceParser.ParseAsync`; the returned result
contains zero `Error`-severity diagnostics. Any failures print the full list of erroring files,
lines, and messages. This is the Phase 1 architecture gate. This scenario is tested by
`Parse_OmgModels_NoSyntaxErrors`.

**OmgModels_FileCount_IsExpected**: The `test/SysMLModels/OMG/` directory tree is scanned for
`.sysml` files; the count must be at least 251, confirming the full OMG model set is present
in the repository. This scenario is tested by `OmgModels_FileCount_IsExpected`.
