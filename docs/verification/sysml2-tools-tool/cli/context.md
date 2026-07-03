### Context

#### Verification Approach

`Context` is verified with unit tests defined in `ContextTests.cs`. Because `Context` depends
only on .NET BCL types (`Console`, `StreamWriter`), no mocking or test doubles are required.
Tests call `Context.Create` with controlled argument arrays, inspect the resulting properties
and exit codes, and verify output written to captured streams.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- All flag properties are correctly set for each recognized argument.
- `ArgumentException` is thrown for all unknown or malformed arguments.
- `ExitCode` is 1 after `WriteError` is called and 0 otherwise.
- Silent mode suppresses console output but does not affect the log file.

#### Test Scenarios

**Context_Create_NoArguments_ReturnsDefaultContext**: `Context.Create` is called with an empty
argument array; all boolean flags are false, `ResultsFile` is null, `HeadingDepth` is 1, and
exit code is 0. This scenario is tested by `Context_Create_NoArguments_ReturnsDefaultContext`.

**Context_Create_VersionFlag_SetsVersionTrue**: `Context.Create` is called with `["--version"]`;
the `Version` property is true. This scenario is tested by
`Context_Create_VersionFlag_SetsVersionTrue`.

**Context_Create_ShortVersionFlag_SetsVersionTrue**: `Context.Create` is called with `["-v"]`;
the `Version` property is true. This scenario is tested by
`Context_Create_ShortVersionFlag_SetsVersionTrue`.

**Context_Create_HelpFlag_SetsHelpTrue**: `Context.Create` is called with `["--help"]`; the
`Help` property is true. This scenario is tested by `Context_Create_HelpFlag_SetsHelpTrue`.

**Context_Create_ShortHelpFlag_H_SetsHelpTrue**: `Context.Create` is called with `["-h"]`; the
`Help` property is true. This scenario is tested by
`Context_Create_ShortHelpFlag_H_SetsHelpTrue`.

**Context_Create_ShortHelpFlag_Question_SetsHelpTrue**: `Context.Create` is called with `["-?"]`;
the `Help` property is true. This scenario is tested by
`Context_Create_ShortHelpFlag_Question_SetsHelpTrue`.

**Context_Create_SilentFlag_SetsSilentTrue**: `Context.Create` is called with `["--silent"]`;
the `Silent` property is true. This scenario is tested by
`Context_Create_SilentFlag_SetsSilentTrue`.

**Context_Create_ValidateFlag_SetsValidateTrue**: `Context.Create` is called with
`["--validate"]`; the `Validate` property is true. This scenario is tested by
`Context_Create_ValidateFlag_SetsValidateTrue`.

**Context_Create_ResultsFlag_SetsResultsFile**: `Context.Create` is called with
`["--results", "output.trx"]`; `ResultsFile` equals `"output.trx"`. This scenario is tested by
`Context_Create_ResultsFlag_SetsResultsFile`.

**Context_Create_LogFlag_OpensLogFile**: `Context.Create` is called with
`["--log", "<tmp>.log"]` and then `WriteLine` is called with a test message; the log file is
created and contains the test message. This scenario is tested by
`Context_Create_LogFlag_OpensLogFile`.

**Context_Create_UnknownArgument_ThrowsArgumentException**: `Context.Create` is called with
`["--unknown"]`; an `ArgumentException` containing "Unsupported argument" is thrown. This
scenario is tested by `Context_Create_UnknownArgument_ThrowsArgumentException`.

**Context_Create_LogFlag_WithoutValue_ThrowsArgumentException**: `Context.Create` is called
with `["--log"]` (value missing); an `ArgumentException` is thrown. This scenario is tested by
`Context_Create_LogFlag_WithoutValue_ThrowsArgumentException`.

**Context_Create_ResultsFlag_WithoutValue_ThrowsArgumentException**: `Context.Create` is called
with `["--results"]` (value missing); an `ArgumentException` is thrown. This scenario is tested
by `Context_Create_ResultsFlag_WithoutValue_ThrowsArgumentException`.

**Context_Create_ResultAliasFlag_SetsResultsFile**: `Context.Create` is called with
`["--result", "output.trx"]` (legacy alias); `ResultsFile` equals `"output.trx"`, identical to
the `--results` flag behavior. This scenario is tested by
`Context_Create_ResultAliasFlag_SetsResultsFile`.

**Context_Create_ResultAliasFlag_WithoutValue_ThrowsArgumentException**: `Context.Create` is
called with `["--result"]` (value missing); an `ArgumentException` is thrown. This scenario is
tested by `Context_Create_ResultAliasFlag_WithoutValue_ThrowsArgumentException`.

**Context_Create_DepthFlag_SetsHeadingDepth**: `Context.Create` is called with
`["--depth", "3"]`; `HeadingDepth` equals 3. This scenario is tested by
`Context_Create_DepthFlag_SetsHeadingDepth`.

**Context_Create_NoDepthFlag_ReturnsDefaultHeadingDepth**: `Context.Create` is called with an
empty argument array; `HeadingDepth` equals 1 (the default; valid range is 1–6). This scenario
is tested by `Context_Create_NoDepthFlag_ReturnsDefaultHeadingDepth`.

**Context_Create_DepthFlag_WithoutValue_ThrowsArgumentException**: `Context.Create` is called
with `["--depth"]` (value missing); an `ArgumentException` is thrown. This scenario is tested
by `Context_Create_DepthFlag_WithoutValue_ThrowsArgumentException`.

**Context_Create_DepthFlag_NonIntegerValue_ThrowsArgumentException**: `Context.Create` is
called with `["--depth", "abc"]`; an `ArgumentException` is thrown. This scenario is tested by
`Context_Create_DepthFlag_NonIntegerValue_ThrowsArgumentException`.

**Context_Create_DepthFlag_ZeroValue_ThrowsArgumentException**: `Context.Create` is called with
`["--depth", "0"]` (below the minimum of 1); an `ArgumentException` is thrown. This scenario
is tested by `Context_Create_DepthFlag_ZeroValue_ThrowsArgumentException`.

**Context_Create_DepthFlag_ExceedsMaxValue_ThrowsArgumentException**: *(replaced)*
`Context.Create` with `["--depth", "7"]` no longer throws; this scenario is superseded by
`Context_Create_DepthFlag_ExceedsMaxValue_SetsMaxRenderDepth`.

**Context_Create_DepthFlag_ExceedsMaxValue_SetsMaxRenderDepth**: `Context.Create` is called
with `["--depth", "7"]`; `HeadingDepth` is 6 (clamped) and `MaxRenderDepth` is 7 (raw).
This scenario is tested by `Context_Create_DepthFlag_ExceedsMaxValue_SetsMaxRenderDepth`.

**Context_Create_DepthFlag_SetsMaxRenderDepth**: `Context.Create` is called with
`["--depth", "3"]`; `HeadingDepth` is 3 and `MaxRenderDepth` is 3. This scenario is tested
by `Context_Create_DepthFlag_SetsMaxRenderDepth`.

**Context_Create_ViewFlag_SetsViewName**: `Context.Create` is called with
`["--view", "MyView"]`; `ViewName` equals `"MyView"`. This scenario is tested by
`Context_Create_ViewFlag_SetsViewName`.

**Context_WriteLine_NotSilent_WritesToConsole**: A non-silent `Context` calls `WriteLine` with
a test message; the message appears on standard output. This scenario is tested by
`Context_WriteLine_NotSilent_WritesToConsole`.

**Context_WriteLine_Silent_DoesNotWriteToConsole**: A silent `Context` (created with
`["--silent"]`) calls `WriteLine`; standard output receives nothing, confirming `--silent`
suppresses normal output. This scenario is tested by
`Context_WriteLine_Silent_DoesNotWriteToConsole`.

**Context_WriteError_Silent_DoesNotWriteToConsole**: A silent `Context` calls `WriteError`;
standard error receives nothing, confirming `--silent` also suppresses error output. This
scenario is tested by `Context_WriteError_Silent_DoesNotWriteToConsole`.

**Context_WriteError_SetsErrorExitCode**: A `Context` calls `WriteError`; `ExitCode` is 1
after the call. This scenario is tested by `Context_WriteError_SetsErrorExitCode`.

**Context_WriteError_NotSilent_WritesToConsole**: A non-silent `Context` calls `WriteError`
with a test message; the message appears on standard error. This scenario is tested by
`Context_WriteError_NotSilent_WritesToConsole`.

**Context_WriteError_WritesToLogFile**: A `Context` created with
`["--silent", "--log", "<tmp>.log"]` calls `WriteError` with a test message; the message appears
in the log file, confirming errors are always written to the log regardless of `--silent`. This
scenario is tested by `Context_WriteError_WritesToLogFile`.

**Context_Create_LogFlag_InvalidPath_ThrowsInvalidOperationException**:
`Context.Create` is called with `["--log", "/invalid/\x00path.log"]`; an
`InvalidOperationException` is thrown because the log file cannot be opened. This
scenario is tested by `Context_Create_LogFlag_InvalidPath_ThrowsInvalidOperationException`.

**Context_Create_QueryCommand_WithVerbToken_SetsQueryVerb**: `Context.Create` is called with
`["query", <token>, "--element", "Pkg::Foo"]` for each of the 11 recognized verb tokens;
`Command` equals `SysmlCommand.Query` and `Query.Verb` matches the token. This scenario is
tested by the `[Theory]` `Context_Create_QueryCommand_WithVerbToken_SetsQueryVerb`.

**Context_Create_QueryCommand_UnknownVerb_ThrowsArgumentException**: `Context.Create` is
called with `["query", "bogus"]`; an `ArgumentException` containing `"bogus"` is thrown.
This scenario is tested by `Context_Create_QueryCommand_UnknownVerb_ThrowsArgumentException`.

**Context_Create_QueryCommand_NoVerbWithHelp_LeavesQueryNull**: `Context.Create` is called
with `["query", "--help"]`; `Command` equals `SysmlCommand.Query`, `Help` is true, and
`Query` is null. This scenario is tested by
`Context_Create_QueryCommand_NoVerbWithHelp_LeavesQueryNull`.

**Context_Create_QueryCommand_WithElementFlag_SetsElement**: `Context.Create` is called with
`["query", "uses", "--element", "Pkg::Foo"]`; `Query.Element` equals `"Pkg::Foo"`. This
scenario is tested by `Context_Create_QueryCommand_WithElementFlag_SetsElement`.

**Context_Create_QueryCommand_WithShortElementFlag_SetsElement**: `Context.Create` is called
with `["query", "uses", "-e", "Pkg::Foo"]`; `Query.Element` equals `"Pkg::Foo"`, identical to
`--element`. This scenario is tested by
`Context_Create_QueryCommand_WithShortElementFlag_SetsElement`.

**Context_Create_QueryCommand_WithDirectionFlag_SetsDirection**: `Context.Create` is called
with `["query", "hierarchy", "--element", "Pkg::Foo", "--direction", "up"]`; `Query.Direction`
equals `"up"`. This scenario is tested by
`Context_Create_QueryCommand_WithDirectionFlag_SetsDirection`.

**Context_Create_QueryCommand_WithKindFlag_SetsKind**: `Context.Create` is called with
`["query", "list", "--kind", "part"]`; `Query.Kind` equals `"part"`. This scenario is tested
by `Context_Create_QueryCommand_WithKindFlag_SetsKind`.

**Context_Create_QueryCommand_WithNameFlag_SetsNameFilter**: `Context.Create` is called with
`["query", "find", "--name", "Engine"]`; `Query.NameFilter` equals `"Engine"`. This scenario
is tested by `Context_Create_QueryCommand_WithNameFlag_SetsNameFilter`.

**Context_Create_QueryCommand_WithIncludeStdlibFlag_SetsIncludeStdlibTrue**: `Context.Create`
is called with `["query", "list", "--include-stdlib"]`; `Query.IncludeStdlib` is true. This
scenario is tested by
`Context_Create_QueryCommand_WithIncludeStdlibFlag_SetsIncludeStdlibTrue`.

**Context_Create_QueryCommand_WithFormatMarkdown_SetsQueryFormat**: `Context.Create` is
called with `["query", "list", "--format", "markdown"]`; `Query.Format` equals `"markdown"`
and `RendererFormat` equals `"markdown"` (same parsed value, interpreted independently per
command). This scenario is tested by
`Context_Create_QueryCommand_WithFormatMarkdown_SetsQueryFormat`.

**Context_Create_QueryCommand_WithFormatJson_SetsQueryFormat**: `Context.Create` is called
with `["query", "list", "--format", "json"]`; `Query.Format` equals `"json"`. This scenario
is tested by `Context_Create_QueryCommand_WithFormatJson_SetsQueryFormat`.

**Context_Create_QueryCommand_WithDepthFlag_SetsQueryDepth**: `Context.Create` is called with
`["query", "impact", "--element", "Pkg::Foo", "--depth", "3"]`; `Query.Depth` equals 3 and
`MaxRenderDepth` equals 3 (same underlying parsed value). This scenario is tested by
`Context_Create_QueryCommand_WithDepthFlag_SetsQueryDepth`.

**Context_Create_QueryCommand_WithFiles_SetsQueryFilesNotTopLevelFiles**: `Context.Create` is
called with `["query", "list", "*.sysml"]`; `Query.Files` contains `"*.sysml"` while
`Context.Files` remains empty, confirming query's positional files are kept separate from
`lint`/`render`'s. This scenario is tested by
`Context_Create_QueryCommand_WithFiles_SetsQueryFilesNotTopLevelFiles`.
