### GlobFileCollector

#### Purpose

`GlobFileCollector` is the single, shared entry point for resolving file glob patterns into
concrete file paths. It is used by the `lint`, `render`, and `query` CLI commands so all three
resolve patterns identically, replacing the pre-existing hand-rolled, single-segment-only
resolver in `LintCommand` and the total absence of any resolution in `RenderCommand`/
`QueryCommand` (which previously passed raw glob tokens straight to `WorkspaceLoader.LoadAsync`,
which treats every entry as a literal file path).

#### Data Model

`GlobFileCollector` is a `public static class` with no instance state; all members are stateless
and thread-safe. It has no dependency on the SysML semantic model, layout, or rendering
pipeline.

#### Key Methods

##### `Collect(patterns, extensions, workingDirectory)`

For each pattern (processed in order): strips an optional leading `!` exclusion marker; splits
the remaining pattern body into a static filesystem root and a glob tail (`ParsePattern`/
`SplitAbsolutePattern`); for a pattern with no glob tail, resolves it as a literal file path
(`ResolveOnDiskPath`); otherwise runs the tail through
`Microsoft.Extensions.FileSystemGlobbing.Matcher` against the root, applying extension
filtering when the final path segment is a bare `*` (`HasBareStarFinalSegment`); and finally
adds (inclusion) or removes (exclusion) the resulting paths from an ordinal-deduplicated
accumulator (`AccumulateResults`). Returns the accumulator's contents ordered ordinally.

##### `ParsePattern(patternBody, workingDirectory)` (private)

Splits a pattern body into a static root and glob tail: fully-qualified absolute patterns
determine their own root from the longest non-glob path prefix (via `SplitAbsolutePattern`);
all other patterns (relative, or rooted-but-not-fully-qualified) resolve against
`workingDirectory`.

##### `HasBareStarFinalSegment(globTail)` (private)

Returns `true` only when the glob tail's final path segment (split on either directory
separator) is exactly `*` — triggering extension filtering; a segment such as `**` or `*.sysml`
does not.

##### `ResolveOnDiskPath(literalPath)` (private)

Resolves a literal file path to its actual on-disk casing via `Directory.GetFiles(directory,
fileName)`, returning `null` when the containing directory or the file itself does not exist.

##### `AccumulateResults(collected, results, isExclusion)` (private)

Adds (`isExclusion == false`) or removes (`isExclusion == true`) each supplied path — converted
to its fully-qualified form — from the shared accumulator set.

#### Error Handling

`Collect` throws `ArgumentNullException` for a null `patterns`, `extensions`, or
`workingDirectory` argument. It never throws for a missing root directory or a missing literal
file — both are silently skipped, since these are common, non-exceptional conditions when
resolving user-supplied patterns.

#### Dependencies

- `Microsoft.Extensions.FileSystemGlobbing.Matcher` (off-the-shelf) — recursive glob pattern
  matching.

#### Callers

- `LintCommand.RunAsync` (Tool `Lint` subsystem).
- `RenderCommand.RunAsync` (Tool `Render` subsystem).
- `QueryCommand.RunAsync` (Tool `Query` subsystem).

#### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| SysML2Tools-Core-Io-GlobFileCollector-PatternResolution | `Collect`, `ParsePattern`, `SplitAbsolutePattern` |
| SysML2Tools-Core-Io-GlobFileCollector-ExclusionOrder | `AccumulateResults`, processed in pattern order by `Collect` |
| SysML2Tools-Core-Io-GlobFileCollector-ExtensionFiltering | `HasBareStarFinalSegment` |
| SysML2Tools-Core-Io-GlobFileCollector-LiteralPath | `Collect`'s literal-path branch |
| SysML2Tools-Core-Io-GlobFileCollector-CasingNormalization | `ResolveOnDiskPath` |
| SysML2Tools-Core-Io-GlobFileCollector-SilentSkip | `Collect`'s directory/file existence guards |
| SysML2Tools-Core-Io-GlobFileCollector-StableOutput | `Collect`'s final `OrderBy` over the deduplicated accumulator |
