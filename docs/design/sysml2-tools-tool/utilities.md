## Utilities

### Overview

The `Utilities` subsystem provides shared utility functions for the SysML2 Tools. It
supplies reusable, independently testable helpers consumed by other subsystems. Its
responsibilities are safe file-path manipulation (protecting callers from path-traversal
vulnerabilities when constructing paths from caller-supplied inputs) and qualified-name
compaction for Markdown-oriented rendering. The `Utilities` subsystem contains two units:
`PathHelpers` and `QualifiedNameShortener`.

### Interfaces

**PathHelpers.SafePathCombine**: Combines a base path and a relative path, rejecting any result
that escapes the base directory.

- *Type*: In-process .NET static method.
- *Role*: Provider.
- *Contract*: Accepts `string basePath` and `string relativePath`. Returns the combined path
  produced by `Path.Combine(basePath, relativePath)` after verifying that the resolved result
  remains within `basePath`. Preserves the caller's relative/absolute style in the return value.
- *Constraints*: Throws `ArgumentNullException` for null inputs; throws `ArgumentException`
  when the combined path escapes the base directory; may propagate `NotSupportedException` or
  `PathTooLongException` from underlying BCL path operations.

**QualifiedNameShortener.Shorten**: Strips the longest shared leading `::`-segment prefix from a
pool of qualified names, always retaining each name's own leaf segment.

- *Type*: In-process .NET static method.
- *Role*: Provider.
- *Contract*: Accepts `IReadOnlyList<string> qualifiedNames`. Returns an
  `IReadOnlyDictionary<string, string>` mapping each distinct original name to its shortened
  form, stripping the longest run of leading segments common to every name in the pool, capped
  so every name always keeps at least its own final segment.
- *Constraints*: Throws `ArgumentNullException` when the pool or any contained name is `null`.
  Returns an identity map (no stripping) when the pool has fewer than 2 distinct names, or when
  the names share no common leading segment.

### Design

The `Utilities` subsystem contains the `PathHelpers` and `QualifiedNameShortener` units. Neither
unit depends on the other, and neither depends on any other tool unit or subsystem; both use
only .NET BCL types (`Path`/`ArgumentNullException` for `PathHelpers`; `string.Split`/
`string.Join`/`ArgumentNullException` for `QualifiedNameShortener`).

`PathHelpers.SafePathCombine` and `QualifiedNameShortener.Shorten` are both pure utility
methods: they perform no file-system I/O, hold no state, and throw immediately on invalid
input. All calls to `SafePathCombine` in the codebase originate from the `SelfTest` subsystem
(`Validation`), which uses it to construct log and result file paths inside temporary
directories created during self-validation test execution. The sole caller of
`QualifiedNameShortener.Shorten` is the `Query` subsystem's `QueryResultRenderer`, which applies
it only to the `dependencies` verb's Markdown-only prose rendering.
