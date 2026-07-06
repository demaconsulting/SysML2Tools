#### SysmlEdge Verification

##### Verification Approach

`SysmlEdge` and `SysmlEdgeKind` are pure data types verified indirectly through
`WorkspaceLoaderTests`. Tests construct SysML models containing supertype, feature-typing,
and import references, call `WorkspaceLoader.LoadAsync`, and assert that the resulting
`SysmlWorkspace.Index` contains `SysmlEdge` instances with the expected `SourceQualifiedName`,
`TargetQualifiedName`, and `Kind` values.

##### Test Environment

Tests run via `dotnet test` against all three target frameworks: net8.0, net9.0, and net10.0.
Temporary `.sysml` files are created in `Path.GetTempPath()` and deleted after each test. No
external services or additional configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- A resolved supertype reference produces a `SysmlEdge` with `Kind == SysmlEdgeKind.Supertype`
  and the fully-qualified `TargetQualifiedName`.
- A resolved feature typing reference produces a `SysmlEdge` with `Kind == SysmlEdgeKind.Typing`.
- A resolved import reference produces a `SysmlEdge` with `Kind == SysmlEdgeKind.Import`.
- A resolved view render-target reference produces a `SysmlEdge` with
  `Kind == SysmlEdgeKind.Render`.
- A resolved view-usage exposed-name reference produces a `SysmlEdge` with
  `Kind == SysmlEdgeKind.Expose`.

##### Test Scenarios

| Scenario | Verified By |
| --- | --- |
| Supertype edge recorded | `WorkspaceLoader_LoadAsync_ResolvedSupertype_RecordsSupertypeEdge` |
| Typing edge recorded | `WorkspaceLoader_LoadAsync_ResolvedFeatureTyping_RecordsTypingEdge` |
| Import edge recorded (wildcard) | `WorkspaceLoader_LoadAsync_WildcardImport_RecordsImportEdge` |
| Import edge recorded (named) | `WorkspaceLoader_LoadAsync_NamedImport_RecordsImportEdge` |
| Render edge recorded | `WorkspaceLoader_LoadAsync_ViewRenderTargetResolves_RecordsRenderEdge` |
| Expose edge recorded | `WorkspaceLoader_LoadAsync_ViewUsageWithExpose_RecordsExposeEdge` |
