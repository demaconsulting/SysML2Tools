#### AstBuilder Verification

##### Verification Approach

`AstBuilder` is an internal class with no public surface and is verified indirectly through
`WorkspaceLoaderTests` plus the focused `AstBuilderMetadataTests`. Tests call
`WorkspaceLoader.LoadAsync` with controlled `.sysml` source files and assert that the returned
`SysmlLoadResult.Workspace.Declarations` and nested node data contain the expected names,
metadata annotations, and raw filter text.

##### Test Environment

Tests run via `dotnet test` against all three target frameworks: net8.0, net9.0, and net10.0.
Temporary `.sysml` files are created in `Path.GetTempPath()` and deleted after each test. No
external services or additional configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- A single package `package Foo {}` registers `"Foo"` in `Declarations`.
- Nested packages `package Foo { package Bar {} }` register both `"Foo"` and `"Foo::Bar"`.
- A part definition `part def MyPart {}` inside `Foo` registers `"Foo::MyPart"`.
- An element with only a short name `< shortName >` (no declared name) is not registered.
- A definition with `specializes KnownType` produces a `SupertypeNames` entry that resolves
  without a Warning when `KnownType` is registered.
- A usage/feature's own usage-level `subsets`/`:>` clause (distinct from a definition's
  `specializes`/`:>` supertype clause) directly populates that feature node's `SupertypeNames`
  with the expected target name.
- A metadata annotation in an element body is captured as a `SysmlMetadataNode` child with its
  raw type reference and any supported literal attribute values.
- `VisitViewDefinition` captures `render <target>;` and `filter [<expr>];` members' raw text on
  the corresponding `SysmlViewNode`, and leaves both null for a view with an empty body.
- `VisitViewUsage` (a named `view` usage, not a `view def` definition) captures the same
  render/filter members plus `expose <name>;` members, producing a `SysmlViewNode` with
  populated `ExposeMembers` (each entry pairing its qualified-name reference text with its own
  bracket-filter expression text, or null when absent). This also makes every named `view`
  usage its own renderable declaration, an intentional capability addition beyond `expose`
  capture alone (see the ast-builder design doc).
- `BuildUsageNode` captures a feature's redefinition reference on `RedefinedFeatureName` for both
  the `redefines` keyword form and the `:>>` operator form, for both a bare simple name and a
  qualified `Owner::feature` form (captured verbatim, unresolved), and leaves it null for a
  feature that declares no redefinition.
- An implicitly-named redefining usage (e.g. `port redefines fuelTankPort { item redefines
  fuelSupply; }`, with no explicit declared name) is assigned an `effectiveName`/`QualifiedName`
  derived from the redefined feature's simple name, making it resolvable by later references
  (including `bind` connector ends) instead of remaining anonymous. When the `redefines`
  reference itself is a dot-chained feature path (e.g. `tank.fuelTankPort`), only the trailing
  segment is used, not the whole dotted reference text.
- `VisitDependency` splits a `dependency A, B to C, D;` declaration's flat qualified-name list
  into `FromNames`/`ToNames` correctly, including the FROM-keyword-omitted shape
  (`dependency z to x, y;`), and resolves to the expected cross-product `Dependency` edges (or a
  Warning with no edge for an unresolvable name).
- `VisitBindingConnectorAsUsage` captures a `bind A = B;` binding connector's two endpoints on a
  `SysmlConnectionNode` with `ConnectionKeyword == "binding"`, resolved via the same
  dotted-feature-chain walk as `connect`, producing a `Binding` edge when both sides resolve (or
  a Warning with no edge otherwise).

##### Test Scenarios

| Scenario | Verified By |
| --- | --- |
| Simple name extraction | `WorkspaceLoader_LoadAsync_SinglePackage_RegistersDeclaration` |
| Qualified name from namespace stack | `WorkspaceLoader_LoadAsync_NestedPackages_RegistersQualifiedNames` |
| Definition registration | `WorkspaceLoader_LoadAsync_PartDef_RegistersDefinition` |
| Supertype extraction | `WorkspaceLoader_LoadAsync_SpecializesChain_Registered` |
| Usage-level `subsets`/`:>` capture | `WorkspaceLoader_LoadAsync_UsageLevelSubsetting_PopulatesSupertypeNames` |
| Metadata annotation capture | `AstBuilder_BareMetadataAnnotation_CapturesMetadataNode` |
| Metadata literal attribute capture | `AstBuilder_MetadataAnnotationWithBooleanAttribute_CapturesLiteralValue` |
| `VisitViewDefinition` render | `WorkspaceLoader_LoadAsync_ViewRenderTarget_CapturedRawNeverResolvedNoDiagnostic` |
| `VisitViewDefinition` filter capture | `WorkspaceLoader_LoadAsync_ViewFilterExpression_CapturesTextVerbatimNoEdge` |
| `VisitViewUsage` expose capture | `WorkspaceLoader_LoadAsync_ViewUsageWithExpose_RecordsExposeEdge` |
| `VisitViewUsage` bracket-filter capture | `AstBuilder_ExposeBracketFilter_CapturesRawText` |
| Bracket filter paired to entry | `AstBuilder_MultipleExposeMembers_OnlyOneBracketed_PairsFilterWithCorrectPath` |
| `VisitViewUsage` renderable declaration | `RenderSubsystem_OmgSafetyFeatureViewsCorpus_RendersAllNamedViewUsages` |
| Empty view body regression guard | `WorkspaceLoader_LoadAsync_ViewEmptyBody_AllNewFieldsNullOrEmpty` |
| Redefinition, `redefines` keyword | `WorkspaceLoader_LoadAsync_RedefinesKeyword_CapturesRedefinedFeatureName` |
| Redefinition capture, `:>>` operator | `WorkspaceLoader_LoadAsync_ColonGtGtOperator_CapturesRedefinedFeatureName` |
| Redefinition capture, qualified form | `WorkspaceLoader_LoadAsync_QualifiedRedefinition_CapturesRawText` |
| No redefinition leaves field null | `WorkspaceLoader_LoadAsync_NoRedefinition_RedefinedFeatureNameIsNull` |
| Implicit redefinition name | `WorkspaceLoader_LoadAsync_BindingViaImplicitlyNamedRedefinedUsage_RecordsBindingEdge` |
| Dotted redefinition name | `WorkspaceLoader_LoadAsync_ImplicitNameFromDottedRedefinitionChain_UsesTrailingSegment` |
| Dependency binary ends | `WorkspaceLoader_LoadAsync_DependencyBinaryEnds_RecordsDependencyEdge` |
| Dependency comma-list cross product | `WorkspaceLoader_LoadAsync_DependencyCommaLists_RecordsCrossProductEdges` |
| Dependency unresolved end | `WorkspaceLoader_LoadAsync_DependencyUnresolvedEnd_ProducesWarningNoEdge` |
| Dependency OMG corpus fixtures | `Dependency_OmgCorpusFixtures_ResolveExpectedEdges` |
| Binding dotted-chain resolution | `WorkspaceLoader_LoadAsync_BindingDottedChain_RecordsBindingEdge` |
| Binding unresolved end | `WorkspaceLoader_LoadAsync_BindingUnresolvedEnd_ProducesWarningNoEdge` |
| Binding OMG corpus fixture | `Binding_OmgCorpusFixture_ResolvesBindingEdgesViaImplicitRedefinitionNames` |
