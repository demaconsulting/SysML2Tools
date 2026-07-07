### GlobFileCollector Verification

#### Verification Approach

`GlobFileCollector` is verified through the unit tests in
`test/DemaConsulting.SysML2Tools.Tests/Io/GlobFileCollectorTests.cs`, which call `Collect`
directly against a real temporary filesystem tree — no mocking of `Directory`, `File`, or
`Matcher` is used. Each test creates its own isolated temporary directory and deletes it during
cleanup.

#### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. All test inputs are files and
directories created inline under `Path.GetTempPath()`; no external network access or services
are required.

#### Acceptance Criteria

- All `GlobFileCollectorTests` pass with zero failures across all three target frameworks.
- `Collect` resolves literal paths, basic globs, and recursive `**` globs correctly.
- `Collect` resolves relative patterns (both literal and glob) against the supplied working
  directory.
- `Collect` applies extension filtering only for a bare-`*` final pattern segment.
- `Collect` processes `!` exclusion patterns in supplied order, and a later inclusion pattern
  re-adds a file previously removed by an earlier exclusion.
- `Collect` normalizes on-disk casing before deduplication.
- `Collect` silently skips missing directories/files and never throws for them.
- `Collect` returns a stable, sorted, deduplicated result; an empty pattern list yields an
  empty result.

#### Test Scenarios

| Test | Assertion |
| --- | --- |
| `GlobFileCollector_Collect_LiteralPath_ReturnsSingleFile` | A literal file path resolves directly |
| `GlobFileCollector_Collect_BasicGlob_ReturnsMatchingFiles` | A single-segment glob resolves matching files |
| `GlobFileCollector_Collect_RecursiveGlob_ReturnsNestedFiles` | A `**` glob resolves files at every depth |
| `GlobFileCollector_Collect_RelativeGlob_ResolvesAgainstWorkingDirectory` | Relative glob resolves |
| `GlobFileCollector_Collect_RelativeLiteralPath_ResolvesAgainstWorkingDirectory` | Relative literal resolves |
| `GlobFileCollector_Collect_BareStarPattern_FiltersToSuppliedExtensions` | Bare `*` is filtered |
| `GlobFileCollector_Collect_ExplicitExtensionPattern_ReturnsAllMatches` | Explicit extension is not filtered |
| `GlobFileCollector_Collect_ExclusionPattern_RemovesPreviouslyIncludedFile` | `!` removes a file |
| `GlobFileCollector_Collect_LaterInclusionAfterExclusion_ReAddsFile` | Later inclusion re-adds a removed file |
| `GlobFileCollector_Collect_CaseInsensitiveFilesystem_DeduplicatesSameFile` | Casing dedup collapses duplicates |
| `GlobFileCollector_Collect_NonExistentDirectory_SilentlySkipped` | Missing directory yields empty, no throw |
| `GlobFileCollector_Collect_NonExistentLiteralFile_SilentlySkipped` | Missing file yields empty, no throw |
| `GlobFileCollector_Collect_MultiplePatterns_ReturnsStableSortedOrder` | Result is ordinally sorted and deduplicated |
| `GlobFileCollector_Collect_EmptyPatternList_ReturnsEmptyResult` | Empty pattern list yields empty result |
