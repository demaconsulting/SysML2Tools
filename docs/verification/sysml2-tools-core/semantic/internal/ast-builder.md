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

##### Test Scenarios

| Scenario | Verified By |
| --- | --- |
| Simple name extraction | `WorkspaceLoader_LoadAsync_SinglePackage_RegistersDeclaration` |
| Qualified name from namespace stack | `WorkspaceLoader_LoadAsync_NestedPackages_RegistersQualifiedNames` |
| Definition registration | `WorkspaceLoader_LoadAsync_PartDef_RegistersDefinition` |
| Supertype extraction | `WorkspaceLoader_LoadAsync_SpecializesChain_Registered` |
