#### SemanticIndex Verification

##### Verification Approach

`SemanticIndex` is an internal class verified indirectly through `WorkspaceLoaderTests`. Tests
build small fixture models combining several reference kinds (specialization, feature typing,
wildcard and named imports), call `WorkspaceLoader.LoadAsync`, and query the returned
`SysmlWorkspace.Index` via `GetOutgoingEdges` and `GetIncomingEdges` to confirm both directions
return the expected edges.

##### Test Environment

Tests run via `dotnet test` against all three target frameworks: net8.0, net9.0, and net10.0.
Temporary `.sysml` files are created in `Path.GetTempPath()` and deleted after each test. No
external services or additional configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- `GetOutgoingEdges(qualifiedName)` returns all edges whose `SourceQualifiedName` equals
  `qualifiedName`.
- `GetIncomingEdges(qualifiedName)` returns all edges whose `TargetQualifiedName` equals
  `qualifiedName`.
- An unknown qualified name returns an empty list from both lookup methods (never `null`).
- The index correctly answers both incoming and outgoing queries for a fixture model
  combining multiple node kinds (package hierarchy, specialization, typed feature, import).

##### Test Scenarios

| Scenario | Verified By |
| --- | --- |
| Outgoing/incoming supertype edge query | `WorkspaceLoader_LoadAsync_ResolvedSupertype_RecordsSupertypeEdge` |
| Outgoing/incoming typing edge query | `WorkspaceLoader_LoadAsync_ResolvedFeatureTyping_RecordsTypingEdge` |
| Fully-qualified target | `WorkspaceLoader_LoadAsync_SupertypeAcrossEnclosingNamespace_RecordsResolvedTargetName` |
| Multi-kind fixture | `WorkspaceLoader_LoadAsync_MultiKindFixtureModel_IndexAnswersIncomingAndOutgoingQueries` |
