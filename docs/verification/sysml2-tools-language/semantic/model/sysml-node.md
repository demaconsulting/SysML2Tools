#### SysmlNode Verification

##### Verification Approach

The `SysmlNode` class hierarchy is verified indirectly through `WorkspaceLoaderTests`. These are
pure data container classes constructed by `AstBuilder`; their correctness is confirmed by
asserting that `WorkspaceLoader.LoadAsync` returns the expected qualified names and definition
types in `SysmlLoadResult.Workspace.Declarations`.

##### Test Environment

Tests run via `dotnet test` against all three target frameworks: net8.0, net9.0, and net10.0.
Temporary `.sysml` files are created in `Path.GetTempPath()` and deleted after each test. No
external services or additional configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- `SysmlPackageNode` is constructed with the correct `Name` and `QualifiedName` for a
  single-package source file; the name appears in `Declarations`.
- `SysmlDefinitionNode` is constructed with the correct `QualifiedName` and `DefinitionKeyword`
  for a `part def` declaration; its qualified name appears in `Declarations`.
- `SysmlNode.SupertypeNames` is populated correctly for a definition with a `specializes`
  clause; the name is checked by `ReferenceResolver`. It is likewise populated for a
  usage/feature's own usage-level `subsets`/`:>` clause, distinct from a definition-level
  supertype.
- `SysmlImportNode.ImportedNamespace` is extracted and used by `ReferenceResolver` to build
  the import graph.
- `SysmlNode.ResolvedEdges` is populated by `ReferenceResolver` with the resolved outgoing
  edges for a node that has at least one resolved supertype, typing, or import reference.
- `SysmlNode.Annotations` is populated by `AstBuilder` with captured `comment`/`doc` text for
  a node whose body contains one or more annotating elements, and is empty (never null) for a
  node with none.
- `SysmlViewNode.RenderTargetName`/`FilterExpressionText`/`ExposedNames` are populated verbatim
  from a view's `render`/`filter`/`expose` body members (raw reference/expression text, never
  evaluated), and are `null`/empty for a view with no such members. `RenderTargetName` is
  captured but never resolved into an edge or diagnostic (it names a rendering style/format, not
  content); `ExposedNames` is the only field independently resolved by `ReferenceResolver`.
- `SysmlFeatureNode.RedefinedFeatureName` is populated verbatim from a feature's
  `redefines`/`:>>` clause (bare-name and qualified `Owner::feature` forms, both keyword and
  operator syntax), and is `null` for a feature with no redefinition. It is resolved by
  `ReferenceResolver` into a `Redefinition`-kind edge, mirroring `FeatureTyping`.

##### Test Scenarios

| Scenario | Verified By |
| --- | --- |
| `SysmlPackageNode` construction | `WorkspaceLoader_LoadAsync_SinglePackage_RegistersDeclaration` |
| `SysmlDefinitionNode` construction | `WorkspaceLoader_LoadAsync_PartDef_RegistersDefinition` |
| `SupertypeNames` population | `WorkspaceLoader_LoadAsync_SpecializesChain_Registered` |
| `SupertypeNames` usage-level population | `WorkspaceLoader_LoadAsync_UsageLevelSubsetting_PopulatesSupertypeNames` |
| `ResolvedEdges` populated | `WorkspaceLoader_LoadAsync_ResolvedSupertype_RecordsSupertypeEdge` |
| `Annotations` populated | `WorkspaceLoader_LoadAsync_CommentAndDocumentation_CapturesBothInSourceOrder` |
| `RenderTargetName` unresolved | `WorkspaceLoader_LoadAsync_ViewRenderTarget_CapturedRawNeverResolvedNoDiagnostic` |
| `FilterExpressionText` verbatim | `WorkspaceLoader_LoadAsync_ViewFilterExpression_CapturesTextVerbatimNoEdge` |
| `SysmlViewNode.ExposedNames` from a `view` usage | `WorkspaceLoader_LoadAsync_ViewUsageWithExpose_RecordsExposeEdge` |
| Empty view body leaves all fields null/empty | `WorkspaceLoader_LoadAsync_ViewEmptyBody_AllNewFieldsNullOrEmpty` |
| `RedefinedFeatureName` — `redefines` | `WorkspaceLoader_LoadAsync_RedefinesKeyword_CapturesRedefinedFeatureName` |
| `RedefinedFeatureName` — `:>>` operator | `WorkspaceLoader_LoadAsync_ColonGtGtOperator_CapturesRedefinedFeatureName` |
| `RedefinedFeatureName` — qualified | `WorkspaceLoader_LoadAsync_QualifiedRedefinition_CapturesRawText` |
| `RedefinedFeatureName` — null when absent | `WorkspaceLoader_LoadAsync_NoRedefinition_RedefinedFeatureNameIsNull` |
