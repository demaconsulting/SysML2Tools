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
  clause; the name is checked by `ReferenceResolver`.
- `SysmlImportNode.ImportedNamespace` is extracted and used by `ReferenceResolver` to build
  the import graph.

##### Test Scenarios

| Scenario | Verified By |
| --- | --- |
| `SysmlPackageNode` construction | `WorkspaceLoader_LoadAsync_SinglePackage_RegistersDeclaration` |
| `SysmlDefinitionNode` construction | `WorkspaceLoader_LoadAsync_PartDef_RegistersDefinition` |
| `SupertypeNames` population | `WorkspaceLoader_LoadAsync_SpecializesChain_Registered` |
