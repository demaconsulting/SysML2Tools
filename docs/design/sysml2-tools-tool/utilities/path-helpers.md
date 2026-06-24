### PathHelpers

#### Purpose

`PathHelpers` is a static utility class that provides a safe path-combination method. Its
single responsibility is to combine two path segments while verifying that the result does not
escape the base directory, protecting callers from string-level path-traversal attacks.

#### Data Model

`PathHelpers` holds no instance state. The class is `internal static` with no fields or
properties.

#### Key Methods

**SafePathCombine**: Safely combines a base path and a relative path.

- *Parameters*: `string basePath` — the base directory path; `string relativePath` — the
  relative path to append.
- *Returns*: `string` — the pre-resolved combined path (preserves the caller's
  relative/absolute style).
- *Preconditions*: Both `basePath` and `relativePath` are non-null.
- *Postconditions*: The returned path, when resolved to absolute form, is contained within
  `basePath`.

Validation steps: (1) reject null inputs via `ArgumentNullException.ThrowIfNull`; (2) call
`Path.Combine(basePath, relativePath)` to produce the candidate path; (3) resolve both
`basePath` and the candidate to absolute form with `Path.GetFullPath`; (4) compute
`Path.GetRelativePath(absoluteBase, absoluteCombined)` and reject if the result equals `".."`,
starts with `".."` followed by a directory separator character, or is itself rooted (absolute);
(5) return the pre-resolved `combinedPath` from step 2.

The containment check uses `Path.GetRelativePath` rather than string inspection to handle root
paths, platform case-sensitivity, and directory-separator normalization natively. The `".."`
check treats a double-dot segment as escaping only when it is the entire relative result or is
followed by a directory separator, avoiding false positives for valid names such as `"..data"`.

#### Error Handling

`SafePathCombine` throws `ArgumentNullException` for null inputs. It throws `ArgumentException`
(`"Invalid path component: {relativePath}"`) when the combined path escapes the base directory.
`NotSupportedException` and `PathTooLongException` may propagate from underlying BCL path
operations (`Path.Combine`, `Path.GetFullPath`). No logging or error accumulation is performed;
callers receive exceptions directly.

#### Dependencies

- **.NET BCL** — `Path`, `ArgumentNullException`, and related types are the only dependencies.
  No other tool units or subsystems are used.

#### Callers

- **Validation** — calls `PathHelpers.SafePathCombine` to construct log file paths and
  temporary directory paths during self-validation test execution.
