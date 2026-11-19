#### NameResolver Verification

##### Verification Approach

`NameResolver` is a public class verified directly by `NameResolverTests`. Each test writes a
small `.sysml` model to a temporary file, loads it with `WorkspaceLoader.LoadAsync` seeded with
the real standard library (so global-namespace collisions such as the library's `Base` package
are exercised), constructs a `NameResolver` over the loaded declarations, and asserts the
qualified name each reference resolves to. No mocking is used.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, net10.0, and net11.0. Temporary files are
created in `Path.GetTempPath()` and deleted after each test.

##### Acceptance Criteria

- A simple name resolves in its owning namespace before a same-named element elsewhere.
- Inherited, imported, aliased, and short-named memberships resolve locally; private members and
  privately imported members do not resolve through a qualified name from outside.
- A redefined feature resolves against the redefining feature's owning type's general types.
- Every definition and usage kind implicitly specializes its standard library type for lookup (a
  member declared only there resolves with no explicit supertype), the implicit types are not
  declared general types or edges, explicit supertypes win name conflicts, and every table target
  exists in the embedded standard library.
- Inherited lookup is transitive through explicit and implicit general types, protected members are
  visible to a specialization, private ones never, and a qualified name sees public members only; a
  named feature that redefines an inherited member hides it from the specialization's lookup.
- A variant sees its variation's members, a usage typed by a conjugated port definition sees that
  port definition's members, and an unnamed redefinition of an enclosing or sibling feature's member
  resolves outward from the owning type's owner.
- In the internal resolving mode the general types come only from raw references (a wrong resolved
  edge is ignored), answers do not depend on lookup order, specialization cycles terminate, file-root
  imports apply to the registered declarations (and the referencing file's own supplied imports take
  precedence over a same-named declaration's from another file), and a redefinition never resolves to the
  redefining feature itself.

##### Test Scenarios

| Test | Scenario |
| --- | --- |
| `NameResolver_Resolve_SameNameInSiblingPackages_ResolvesOwningPackageFirst` | Owning package wins |
| `NameResolver_Resolve_InheritedMember_ResolvesThroughSupertype` | Inherited membership |
| `NameResolver_Resolve_AliasAndShortName_ResolveToTheirElement` | Alias and short name |
| `NameResolver_Resolve_PrivateMemberViaQualifiedName_DoesNotResolve` | Private via qualified name |
| `NameResolver_Resolve_ImportedMember_HonorsImportVisibility` | Private vs public import |
| `NameResolver_ResolveRedefinedFeature_ResolvesAgainstGeneralTypes` | Redefinition target |
| `NameResolver_Resolve_FilteredRecursiveImport_SkipsRejectedCandidate` | Filtered recursive import |
| `NameResolver_GeneralTypes_PartiallyResolvedSupertypes_IncludesEveryGeneralType` | Partly resolved supertypes |
| `NameResolver_Resolve_MembershipImportedAlias_ResolvesItsShortName` | Imported alias short name |
| `NameResolver_Resolve_SpecializedBodyMembers_HonorVisibilityAndShortName` | Specialized body members |
| `NameResolver_ResolvingMode_GeneralTypes_IgnoresResolvedEdges` | Resolving mode ignores edges |
| `NameResolver_ResolvingMode_Resolve_SupertypeDeclaredLater_IsOrderIndependent` | Order independence |
| `NameResolver_ResolvingMode_GeneralTypes_SpecializationCycle_Terminates` | Cycle guard |
| `NameResolver_ResolvingMode_Resolve_FileRootImport_AppliesToRegisteredDeclarations` | File-root imports |
| `NameResolver_ResolvingMode_Resolve_SuppliedFileRootImports_OverrideRegisteredByName` | Supplied imports win |
| `NameResolver_ResolveRedefinition_ExcludesRedefiningFeatureAndFallsBack` | Redefinition target rules |
| `NameResolver_ImplicitSpecialization_PerKind_AddsLibraryGeneralType` | Per-kind lookup general type, not declared |
| `NameResolver_ImplicitSpecialization_PerKind_MemberResolves` | Library member resolves, no `:>` |
| `NameResolver_ImplicitSpecialization_TargetsExistInVendoredStdlib` | Table targets exist in the stdlib |
| `NameResolver_ImplicitSpecialization_NoStdlibSeed_NoEffect` | Without the library seed |
| `NameResolver_ImplicitSpecialization_LibraryTypeDoesNotSpecializeItself` | No self-specialization |
| `NameResolver_ImplicitSpecialization_ExplicitSupertypeWinsOverImplicit` | Declared supertypes are consulted first |
| `NameResolver_ImplicitSpecialization_CyclesTerminate` | User type named like a library root; mutual specialization |
| `NameResolver_ImplicitSpecialization_NotMaterialized_AsEdges` | No resolved edge targets an implicit library type |
| `NameResolver_ResolvingMode_ImplicitSpecialization_IsOrderIndependent` | Order independent |
| `NameResolver_InheritedLookup_Transitive_ThroughExplicitAndImplicitGenerals` | Transitive chain |
| `NameResolver_InheritedLookup_ProtectedMemberVisibleToSpecialization_PrivateNot` | Protected/private/public |
| `NameResolver_InheritedLookup_RedefinitionHidesRedefinedMember` | Named redefinition hides the member |
| `NameResolver_InheritedLookup_RedefinitionWithSameName_ResolvesOwned` | Same-name redefinition: owned |
| `NameResolver_InheritedLookup_VariantSeesVariationMembers` | A variant specializes its owning variation |
| `NameResolver_InheritedLookup_ConjugatedPortTypeMembersVisible` | Conjugated port members |
| `NameResolver_InheritedLookup_EnclosingTypedUsage_TypeMembersResolveForNestedRedefinition` | Enclosing usage's type |
| `NameResolver_ResolveRedefinition_UnnamedRedefinitionOfEnclosingMember_Resolves` | Outward retry |
| `NameResolver_ResolveRedefinition_OnlySelf_StaysUnresolved` | Nothing but the redefining feature stays unresolved |
