## Utilities

### Verification Approach

The `Utilities` subsystem is verified by integration tests defined in
`UtilitiesSubsystemTests.cs` (covering `PathHelpers` and `DiagnosticFormatter`) plus dedicated
unit test files: `PathHelpersTests.cs` and `DiagnosticFormatterTests.cs`. Integration tests
exercise `PathHelpers` through realistic
path-combination workflows to confirm that valid paths are resolved correctly, traversal
attacks are rejected, and the resulting paths can be used for actual directory creation, and
exercise `DiagnosticFormatter` over a mix of located and unlocated diagnostics of every
severity. Neither unit has a dependency on any other tool unit, so no mocking is required.

### Test Environment

N/A - standard test environment.

### Acceptance Criteria

- All integration tests pass with zero failures.
- Valid relative paths are combined correctly with no exception.
- Path traversal patterns (e.g., `../`) cause `ArgumentException` to be thrown.
- Absolute paths supplied as the relative argument cause `ArgumentException` to be thrown.
- Paths produced by `SafePathCombine` can be passed directly to `Directory.CreateDirectory`.
- Diagnostics of every severity, located or not, are each formatted as one line in the
  `path(line,col): severity: message` shape (location omitted when there is no file path).

### Test Scenarios

**UtilitiesSubsystem_PathResolutionWorkflow_ValidPaths_ResolvesCorrectly**: Multiple valid
relative path arguments are combined with a base path using `PathHelpers.SafePathCombine`; all
results are correctly combined paths that remain within the base directory and no exception is
thrown. This scenario is tested by
`UtilitiesSubsystem_PathResolutionWorkflow_ValidPaths_ResolvesCorrectly`.

**UtilitiesSubsystem_PathTraversalValidation_DangerousPaths_ThrowsException**: Multiple path
traversal patterns (e.g., `../`, `subfolder/../../../`) are passed to `SafePathCombine`; an
`ArgumentException` is thrown for each traversal pattern and no traversal succeeds. This
scenario is tested by
`UtilitiesSubsystem_PathTraversalValidation_DangerousPaths_ThrowsException`.

**UtilitiesSubsystem_AbsolutePathRejection_ThrowsException**: Absolute paths (e.g.,
`/etc/passwd` and on Windows `C:\Windows\System32\file.txt`) are passed as the relative path
argument to `SafePathCombine`; an `ArgumentException` is thrown for each and no injection
succeeds. This scenario is tested by
`UtilitiesSubsystem_AbsolutePathRejection_ThrowsException`.

**UtilitiesSubsystem_DiagnosticFormatting_MixedDiagnostics_FormatsEachAsOneLine**: Info,
Warning, and Error diagnostics with file paths and positions, plus one diagnostic without a file
path, are each passed to `DiagnosticFormatter.Format`; the results are the expected
`path(line,col): severity: message` lines (and `warning: no file` for the unlocated one), each a
single line. This scenario is tested by
`UtilitiesSubsystem_DiagnosticFormatting_MixedDiagnostics_FormatsEachAsOneLine`.

**UtilitiesSubsystem_DirectoryCreationWorkflow_ValidPaths_CreatesDirectories**:
`PathHelpers.SafePathCombine` is used to compute a nested path, and the resulting path is
passed to `Directory.CreateDirectory`; the directory is created at the expected location within
the base directory. This scenario is tested by
`UtilitiesSubsystem_DirectoryCreationWorkflow_ValidPaths_CreatesDirectories`.
