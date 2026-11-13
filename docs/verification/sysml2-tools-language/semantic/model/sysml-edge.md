#### SysmlEdge Verification

##### Verification Approach

`SysmlEdge` and `SysmlEdgeKind` are pure data types verified indirectly through
`WorkspaceLoaderTests`. Tests construct SysML models containing supertype, feature-typing,
and import references, call `WorkspaceLoader.LoadAsync`, and assert that the resulting
`SysmlWorkspace.Index` contains `SysmlEdge` instances with the expected `SourceQualifiedName`,
`TargetQualifiedName`, and `Kind` values.

##### Test Environment

Tests run via `dotnet test` against all four target frameworks: net8.0, net9.0, net10.0, and net11.0.
Temporary `.sysml` files are created in `Path.GetTempPath()` and deleted after each test. No
external services or additional configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- A resolved supertype reference produces a `SysmlEdge` with `Kind == SysmlEdgeKind.Supertype`
  and the fully-qualified `TargetQualifiedName`.
- A resolved feature typing reference produces a `SysmlEdge` with `Kind == SysmlEdgeKind.Typing`.
- A resolved import reference produces a `SysmlEdge` with `Kind == SysmlEdgeKind.Import`.
- A resolved view-usage exposed-name reference produces a `SysmlEdge` with
  `Kind == SysmlEdgeKind.Expose`; `SysmlEdgeKind.Render` no longer exists, since a view's
  `render <target>;` member is never resolved into an edge (it names a rendering style/format,
  not content).
- A resolved feature redefinition reference produces a `SysmlEdge` with
  `Kind == SysmlEdgeKind.Redefinition`.
- Each single-reference edge records the reference text as written in `RawReference`
  (supertype, typing, redefinition, import, verify, expose, metadata type, and a view usage's
  view definition); for import and expose edges the text keeps the wildcard/recursive markers
  (`P`, `P::*`, `P::**`, `P::*::**`) and excludes any bracketed filter, while every form still
  resolves to the same target; an unresolved reference listed before an alias of the same target
  leaves only the alias's edge, tagged with the alias text; the two-ended kinds (satisfy,
  allocate, dependency, connect, binding, transition) leave `RawReference` `null`.

##### Test Scenarios

| Scenario | Verified By |
| --- | --- |
| Supertype edge recorded | `WorkspaceLoader_LoadAsync_ResolvedSupertype_RecordsSupertypeEdge` |
| Typing edge recorded | `WorkspaceLoader_LoadAsync_ResolvedFeatureTyping_RecordsTypingEdge` |
| Import edge recorded (wildcard) | `WorkspaceLoader_LoadAsync_WildcardImport_RecordsImportEdge` |
| Import edge recorded (named) | `WorkspaceLoader_LoadAsync_NamedImport_RecordsImportEdge` |
| RenderTargetName never resolved | `WorkspaceLoader_LoadAsync_ViewRenderTarget_CapturedRawNeverResolvedNoDiagnostic` |
| Expose edge recorded | `WorkspaceLoader_LoadAsync_ViewUsageWithExpose_RecordsExposeEdge` |
| Redefinition edge recorded | `WorkspaceLoader_LoadAsync_ResolvedRedefinition_RecordsRedefinitionEdge` |
| Raw reference text recorded per kind | `WorkspaceLoader_LoadAsync_ResolvedEdges_RecordRawReferenceText` |
| Import raw text keeps markers | `WorkspaceLoader_LoadAsync_ImportForms_RawReferenceKeepsWrittenWildcardMarkers` |
| Expose raw text keeps markers | `WorkspaceLoader_LoadAsync_ExposeForms_RawReferenceKeepsWrittenWildcardMarkers` |
| Raw text falls back to the name | `ReferenceResolver_ImportAndExposeWithoutRawText_FallBackToName` |
| Unresolved + alias | `WorkspaceLoader_LoadAsync_SupertypeUnresolvedBeforeAlias_OnlyAliasEdgeCarriesRawReference` |
| Two-ended kinds carry no raw reference | `WorkspaceLoader_LoadAsync_TwoEndedEdges_HaveNoRawReference` |
| Raw reference preserved by serialization; null omitted | `Serialize_EdgeRawReference_RoundTripsAndNullIsOmitted` |
