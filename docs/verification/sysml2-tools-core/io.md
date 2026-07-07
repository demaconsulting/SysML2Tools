## DemaConsulting.SysML2Tools — Io Subsystem Verification

### Verification Approach

The Io subsystem is verified by unit tests in `DemaConsulting.SysML2Tools.Tests` that exercise
`GlobFileCollector.Collect` directly against a real, temporary filesystem tree (no mocking of
`Directory`/`File`/`Matcher`). Tests cover literal-path resolution, basic and recursive glob
matching, `!` exclusion processing, bare-`*` extension filtering, on-disk casing normalization,
silent skipping of missing directories/files, and stable sorted/deduplicated output.

### Test Environment

Tests run via `dotnet test` against all three target frameworks: net8.0, net9.0, and net10.0.
Each test creates and cleans up its own temporary directory tree under `Path.GetTempPath()`; no
external network access or services are required.

### Acceptance Criteria

- All `GlobFileCollectorTests` pass with zero failures across all three target frameworks.
- A literal file path (no glob metacharacters) resolves directly without directory traversal.
- A basic single-segment glob pattern (e.g. `*.sysml`) resolves every matching file in the
  target directory.
- A recursive glob pattern (e.g. `**/*.sysml`) resolves matching files at every nesting depth.
- A bare-`*` pattern filters results to the caller-supplied extensions; a pattern with an
  explicit extension does not filter further.
- A `!`-prefixed exclusion pattern removes a previously-included file from the result set.
- Two patterns referring to the same physical file via different casing collapse to a single
  entry in the result set.
- A pattern whose root directory does not exist, or a literal path that does not exist, is
  silently skipped without throwing.
- The returned list is sorted (ordinal) and deduplicated; an empty pattern list returns an
  empty result.

### Test Scenarios

**GlobFileCollector_Collect_LiteralPath_ReturnsSingleFile**: A literal (non-glob) file path is
supplied; the result contains exactly that one file, resolved without directory traversal.

**GlobFileCollector_Collect_BasicGlob_ReturnsMatchingFiles**: A single-segment `*.sysml` pattern
against a directory containing multiple `.sysml` and non-`.sysml` files resolves every matching
`.sysml` file and none of the others.

**GlobFileCollector_Collect_RecursiveGlob_ReturnsNestedFiles**: A `**/*.sysml` pattern against a
directory tree with matching files at multiple nesting depths resolves every one of them.

**GlobFileCollector_Collect_BareStarPattern_FiltersToSuppliedExtensions**: A bare `*` pattern
against a directory containing both `.sysml`/`.kerml` and unrelated files resolves only the
files whose extension is in the supplied extension set.

**GlobFileCollector_Collect_ExplicitExtensionPattern_ReturnsAllMatches**: A pattern with an
explicit extension (e.g. `*.sysml`) is not subject to extension-set filtering; it resolves every
file matching the glob regardless of the supplied extension set.

**GlobFileCollector_Collect_ExclusionPattern_RemovesPreviouslyIncludedFile**: An inclusion
pattern followed by a `!`-prefixed exclusion pattern targeting one of the included files
resolves every included file except the excluded one.

**GlobFileCollector_Collect_CaseInsensitiveFilesystem_DeduplicatesSameFile**: Two patterns
referring to the same physical file with different casing (on a case-insensitive filesystem)
resolve to a single entry in the result set, confirming on-disk casing normalization before
ordinal deduplication.

**GlobFileCollector_Collect_NonExistentDirectory_SilentlySkipped**: A pattern whose root
directory does not exist resolves to an empty result without throwing.

**GlobFileCollector_Collect_NonExistentLiteralFile_SilentlySkipped**: A literal path that does
not exist on disk resolves to an empty result without throwing.

**GlobFileCollector_Collect_MultiplePatterns_ReturnsStableSortedOrder**: Multiple overlapping
inclusion patterns resolve to a stable, ordinally sorted, deduplicated list of file paths.

**GlobFileCollector_Collect_EmptyPatternList_ReturnsEmptyResult**: An empty pattern list
resolves to an empty result.
