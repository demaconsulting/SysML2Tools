#### AstBuilder Verification

##### Verification Approach

`AstBuilder` is an internal class with no public surface and is verified indirectly through
`WorkspaceLoaderTests`. Tests call `WorkspaceLoader.LoadAsync` with controlled `.sysml` source
files and assert that the returned `SysmlLoadResult.Workspace.Declarations` contains the
expected qualified names, confirming that `AstBuilder` correctly extracted names, built
qualified names from the namespace stack, and extracted supertype names from the CST.

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
- `VisitViewDefinition` captures `render <target>;` and `filter [<expr>];` members' raw text on
  the corresponding `SysmlViewNode`, and leaves both null for a view with an empty body.
- `VisitViewUsage` (a named `view` usage, not a `view def` definition) captures the same
  render/filter members plus `expose <name>;` members, producing a `SysmlViewNode` with
  populated `ExposedNames`. This also makes every named `view` usage its own renderable
  declaration, an intentional capability addition beyond `expose` capture alone (see the
  ast-builder design doc).

##### Test Scenarios

| Scenario | Verified By |
| --- | --- |
| Simple name extraction | `WorkspaceLoader_LoadAsync_SinglePackage_RegistersDeclaration` |
| Qualified name from namespace stack | `WorkspaceLoader_LoadAsync_NestedPackages_RegistersQualifiedNames` |
| Definition registration | `WorkspaceLoader_LoadAsync_PartDef_RegistersDefinition` |
| Supertype extraction | `WorkspaceLoader_LoadAsync_SpecializesChain_Registered` |
| `VisitViewDefinition` render | `WorkspaceLoader_LoadAsync_ViewRenderTarget_CapturedRawNeverResolvedNoDiagnostic` |
| `VisitViewDefinition` filter capture | `WorkspaceLoader_LoadAsync_ViewFilterExpression_CapturesTextVerbatimNoEdge` |
| `VisitViewUsage` expose capture | `WorkspaceLoader_LoadAsync_ViewUsageWithExpose_RecordsExposeEdge` |
| `VisitViewUsage` renderable declaration | `RenderSubsystem_OmgSafetyFeatureViewsCorpus_RendersAllNamedViewUsages` |
| Empty view body regression guard | `WorkspaceLoader_LoadAsync_ViewEmptyBody_AllNewFieldsNullOrEmpty` |
