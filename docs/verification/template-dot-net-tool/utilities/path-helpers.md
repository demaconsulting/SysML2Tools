### PathHelpers

#### Verification Approach

`PathHelpers` is verified with unit tests defined in `PathHelpersTests.cs`. Because `PathHelpers`
performs pure path manipulation using only .NET BCL types, no mocking or test doubles are
required. Tests call `PathHelpers.SafePathCombine` directly with controlled base and relative
path arguments and assert on the returned string or the thrown exception type and message.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Valid relative paths are combined correctly and the returned path equals the expected result.
- Path traversal patterns cause `ArgumentException` with message containing "Invalid path
  component".
- Absolute paths supplied as the relative argument cause `ArgumentException`.
- Null inputs cause `ArgumentNullException`.
- A filename beginning with `".."` that is not a traversal sequence is accepted correctly.

#### Test Scenarios

**PathHelpers_SafePathCombine_ValidPaths_CombinesCorrectly**: A relative path
(`"subfolder/file.txt"`) is combined with a base path; the returned path equals the expected
combined result and no exception is thrown, confirming basic path combination works. This
scenario is tested by `PathHelpers_SafePathCombine_ValidPaths_CombinesCorrectly`.

**PathHelpers_SafePathCombine_PathTraversalWithDoubleDots_ThrowsArgumentException**: A relative
path starting with `"../"` is passed; an `ArgumentException` containing "Invalid path
component" is thrown, rejecting the leading traversal attempt. This scenario is tested by
`PathHelpers_SafePathCombine_PathTraversalWithDoubleDots_ThrowsArgumentException`.

**PathHelpers_SafePathCombine_DoubleDotsInMiddle_ThrowsArgumentException**: A relative path
containing `"subfolder/../../../etc/passwd"` is passed; an `ArgumentException` is thrown,
rejecting the embedded traversal sequence. This scenario is tested by
`PathHelpers_SafePathCombine_DoubleDotsInMiddle_ThrowsArgumentException`.

**PathHelpers_SafePathCombine_AbsolutePath_ThrowsArgumentException**: An absolute path is
passed as the relative argument — Unix-style `/etc/passwd` on all platforms and Windows-style
`C:\Windows\System32\file.txt` when running on Windows; an `ArgumentException` is thrown for
each sub-case. This scenario is tested by
`PathHelpers_SafePathCombine_AbsolutePath_ThrowsArgumentException`.

**PathHelpers_SafePathCombine_CurrentDirectoryReference_CombinesCorrectly**: A relative path
starting with `"./"` (e.g., `"./subfolder/file.txt"`) is combined with a base path; the
returned path equals the expected combined result and no exception is thrown. This scenario is
tested by `PathHelpers_SafePathCombine_CurrentDirectoryReference_CombinesCorrectly`.

**PathHelpers_SafePathCombine_NestedPaths_CombinesCorrectly**: A deeply nested relative path
(`"a/b/c/d/file.txt"`) is combined with a base path; the returned path equals the expected
combined result and no exception is thrown. This scenario is tested by
`PathHelpers_SafePathCombine_NestedPaths_CombinesCorrectly`.

**PathHelpers_SafePathCombine_EmptyRelativePath_ReturnsBasePath**: An empty string is passed as
the relative path; the returned path equals the base path and no exception is thrown,
confirming the empty-string edge case is handled correctly. This scenario is tested by
`PathHelpers_SafePathCombine_EmptyRelativePath_ReturnsBasePath`.

**PathHelpers_SafePathCombine_DotDotPrefixedName_CombinesCorrectly**: A relative path whose
filename starts with `".."` but is not a traversal sequence (e.g., `"..data/file.txt"`) is
combined with a base path; the returned path equals the expected combined result and no
exception is thrown, confirming such filenames are not misidentified as traversal. This scenario
is tested by `PathHelpers_SafePathCombine_DotDotPrefixedName_CombinesCorrectly`.

**PathHelpers_SafePathCombine_NullBasePath_ThrowsArgumentNullException**: `null` is passed as
the `basePath` argument; an `ArgumentNullException` is thrown, confirming the null guard on
`basePath`. This scenario is tested by
`PathHelpers_SafePathCombine_NullBasePath_ThrowsArgumentNullException`.

**PathHelpers_SafePathCombine_NullRelativePath_ThrowsArgumentNullException**: `null` is passed
as the `relativePath` argument; an `ArgumentNullException` is thrown, confirming the null guard
on `relativePath`. This scenario is tested by
`PathHelpers_SafePathCombine_NullRelativePath_ThrowsArgumentNullException`.
