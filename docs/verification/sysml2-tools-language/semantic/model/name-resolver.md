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
  exists in the embedded standard library. Every keyword `AstBuilder` emits has its own row-level
  case, including KerML `flow`, `succession` and `succession flow` and the connection, interface,
  allocation, message, flow and binding usages; only `ref`, `subject` and `feature` use the
  `Base::things` fallback.
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
- `ResolveUnambiguous` reports both elements when a final segment is exposed by two public imports
  of one namespace (or two owned names), and none for the same element imported twice, a private
  import's member, or an unknown name; deeper ambiguity follows `Resolve` (documented limitation).
- A dotted feature chain resolves its first segment by full resolution and every later segment by
  visible resolution in the previous feature: through a subsetting or redefining usage, an alias
  (followed to its target), a conjugated port type, a quoted segment (a dot inside quotes does not
  split), and a feature's own inline members before its type's; private and protected members are
  unreachable after the first segment while the first segment sees its namespace's private members;
  a chain with any unresolvable segment does not resolve; self-referential and mutual
  specialization through a chain terminates; a redefinition target written as a chain resolves its
  first segment among the general types. A quoted and an unquoted spelling of one name are the same
  name.
- A reference subsetting (`::>`, or the feature a `perform`/`assert` references) is a general type
  for member lookup only (`LookupGeneralTypes`; the public `GeneralTypes` never includes it, in either
  resolution mode; no edge, no diagnostic); a general-type reference that resolves to the type
  itself is resolved again from the owner's owner; a transition implicitly specializes
  `Actions::transitionActions` and the new usage kinds have their implicit-specialization rows; a
  feature redefining several features inherits the members of each.

##### Test Scenarios

| Test | Scenario |
| --- | --- |
| `NameResolver_Resolve_SameNameInSiblingPackages_ResolvesOwningPackageFirst` | Owning package wins |
| `NameResolver_ResolveUnambiguous_FinalSegmentFromTwoPublicImports_ReportsBoth` | Final-segment ambiguity |
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
| `NameResolver_FeatureChain_ThroughSubsettingUsage_ResolvesInheritedMember` | `p1 :> p; p1.x.x1` |
| `NameResolver_FeatureChain_ThroughAlias_ResolvesAliasTarget` | Alias followed to its target; private alias hidden |
| `NameResolver_FeatureChain_ThroughConjugatedPortType_ResolvesPortMembers` | `~PortT` and `PortT` typed ports |
| `NameResolver_FeatureChain_QuotedSegments_SplitOnDotsOutsideQuotes` | Dots inside quotes do not split |
| `NameResolver_FeatureChain_PrefersOwnedInlineMemberOverTypeMember` | Own members before the type's |
| `NameResolver_FeatureChain_PrivateAndProtectedThroughSubsettingUsage_NotVisible` | Visibility through a chain |
| `NameResolver_FeatureChain_ThreeLevels_MixedDirectTypeAndSpecialization` | Owned, typed and subsetted hops |
| `NameResolver_FeatureChain_UnresolvedSegment_DoesNotResolve` | Missing and malformed segments |
| `NameResolver_FeatureChain_SpecializationCycle_Terminates` | `ref x :> x.y`; mutual subsetting |
| `NameResolver_FeatureChain_QualifiedFirstSegment_Resolves` | `P::p.x`, `$::P::p.x` |
| `NameResolver_ResolveRedefinedFeature_Chain_ResolvesThroughFirstSegment` | `:>> clock.time` |
| `NameResolver_Resolve_QuotedAndUnquotedSimpleName_AreEquivalent` | `'PowerTrain'` and `PowerTrain` |
| `NameResolver_ReferenceSubsetting_IsLookupGeneralType` | `::>` target is a general type for lookup; no edge |
| `NameResolver_Transition_ImplicitlySpecializesTransitionActions` | transition → `Actions::transitionActions` |
| `NameResolver_ImplicitSpecialization_NewUsageKinds_AddLibraryGeneralType` | Rows of the new usage kinds |
| `NameResolver_PortionUsage_ImplicitSpecialization_MemberResolves` | `start` resolves in a `timeslice` |
