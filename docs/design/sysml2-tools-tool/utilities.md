## Utilities

![Tool Utilities Structure](ToolUtilitiesView.svg)

### Overview

The `Utilities` subsystem provides shared utility functions for the SysML2 Tools. It
supplies reusable, independently testable helpers consumed by other subsystems. Its
responsibilities are safe file-path manipulation (protecting callers from path-traversal
vulnerabilities when constructing paths from caller-supplied inputs) and the single shared
formatting of a diagnostic as a console line, so the `lint`, `render`, and `query` commands
print diagnostics identically. The `Utilities` subsystem contains two units: `PathHelpers`
and `DiagnosticFormatter`.

> Qualified-name compaction for Markdown-oriented rendering (`QualifiedNameShortener`) moved
> to the `DemaConsulting.SysML2Tools.Core` library's `Query` subsystem alongside the rest of
> the public query API — see `docs/design/sysml2-tools-core/query.md`.

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

**DiagnosticFormatter.Format**: Formats one diagnostic as a single console line.

- *Type*: In-process .NET static method.
- *Role*: Provider.
- *Contract*: Accepts a `SysmlDiagnostic` and returns `path(line,col): severity: message`
  (lowercase severity; the `path(line,col):` location is omitted when the diagnostic has no
  file path). Used by the `Lint`, `Render`, and `Query` subsystems for every diagnostic they
  print.
- *Constraints*: Throws `ArgumentNullException` for a null diagnostic; performs no I/O.

### Design

The `Utilities` subsystem contains the `PathHelpers` and `DiagnosticFormatter` units.
`PathHelpers` does not depend on any other
tool unit or subsystem; it uses only .NET BCL types (`Path`/`ArgumentNullException`).

`PathHelpers.SafePathCombine` is a pure utility method: it performs no file-system I/O, holds
no state, and throws immediately on invalid input. All calls to `SafePathCombine` in the
codebase originate from the `SelfTest` subsystem (`Validation`), which uses it to construct
log and result file paths inside temporary directories created during self-validation test
execution.
