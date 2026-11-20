#### NameResolver

![Language Semantic Model Structure](LanguageSemanticModelView.svg)

##### Overview

`NameResolver` is a public class that resolves qualified-name text against a loaded workspace's
declarations following KerML 1.1 Beta 2 clause 8.2.3.5 (Name Resolution). `ReferenceResolver`
uses it — in an internal *resolving* mode — to resolve every reference of the semantic model, so
the resolved `SysmlEdge`s are themselves KerML-compliant (member visibility, filtered imports,
import scoping). It also serves callers that hold raw reference text the semantic model did not
resolve into a `SysmlEdge`: the Core layout strategies use it as the last resolution step after a feature's or
definition's resolved `Typing`/`Supertype` edge, and trust that edge when it exists. It is
constructed over `SysmlWorkspace.Declarations` (or the symbol table's declarations during
resolution) with an optional `importFilter` predicate, and caches lookups per instance, so one
instance serves one workspace.

##### Data Model

- Owning namespaces are derived from qualified names: the owner of `A::B::C` is `A::B`; an
  element with no `::` is a top-level element of its root namespace.
- Owned memberships of a namespace are its `Children` (excluding imports) plus any declaration
  whose qualified name is `<namespace>::<name>`. Each has a `Name`, an optional `ShortName`, and
  a `Visibility` (`SysmlVisibility`, default `Public`). `SysmlAliasNode` children contribute
  their names for their `TargetName`, resolved relative to the owning namespace.
- Imports are the namespace's `SysmlImportNode` children, with `Visibility` (default `Private`,
  per KerML 7.2.5.4/SysML 7.5.3), `IsImportAll`, `IsMembershipImport`, `IsRecursive`, and
  `BracketFilterExpressionText`.
- A type's general types (`GeneralTypes`) — a definition, feature, view, or viewpoint — are, in
  the public mode, its resolved `Supertype`/`Typing`/`Redefinition`
  edge targets; when it has fewer of those than raw references (e.g. standard-library elements,
  which are never resolved, or a supertype the semantic pass did not resolve), its raw
  `SupertypeNames`, `FeatureTyping`, and view definition are also resolved relative to its owning
  namespace (KerML 8.2.3.5.2: a Specialization's local namespace is the owning namespace of its
  owning type) and merged with the resolved targets, without duplicates. In the internal
  *resolving* mode `GeneralTypes` ignores `ResolvedEdges` entirely and derives the general types
  only from the raw references (`SupertypeNames`, `FeatureTyping`, the view definition name, the
  `ReferenceSubsettingName`, and every redefinition target — `RedefinedFeatureName` and
  `AdditionalRedefinedFeatureNames`),
  cached per type and guarded against re-entry (a specialization
  cycle contributes nothing). The reference subsetting (`::>`, or the feature a `perform`,
  `include` or `assert` usage references) is a general type for *lookup only* (`LookupGeneralTypes`,
  never the public `GeneralTypes`): it adds no edge and no diagnostic. A general-type reference that
  resolves to the type itself (a usage named only by the
  feature it references, `assert massLimitation { ... }`, finds itself under that name) is resolved
  again from the owner's own owner (`ResolveGeneralReference`). A feature that redefines several
  features inherits the members of each, and hides the inherited members each redefines. That makes
  every answer independent of the order in which the
  semantic pass visits nodes and keeps the lookup cache valid while edges are still being
  attached.
- *Lookup general types* (`LookupGeneralTypes`, internal) are `GeneralTypes` followed by the
  **implicit specializations** of the element, and are what inherited lookup and redefinition
  resolution use. Every definition and usage kind implicitly specializes (or subsets) a standard
  library type (KerML 7.3.4/SysML default general types; the table below follows the OMG Pilot
  Implementation's `ImplicitGeneralizationMap`), a `variant` usage its owning variation (the
  owner's qualified name), and a usage typed by a conjugated port definition (`port p : ~PortT`,
  `ConjugatedFeatureTyping`) that port definition, resolved relative to the owning namespace.
  Implicit entries come after the declared ones, so an explicit supertype wins a name conflict;
  an entry that is the element itself, already declared, or absent from the loaded declarations
  (a workspace without the standard library seed) is skipped, so without the seed behavior is
  exactly the declared-only lookup. The tables are private static data keyed by the
  `DefinitionKeyword`/`FeatureKeyword` strings `AstBuilder` emits; a usage keyword with no row
  falls back to `Base::things`:

  - Definitions: `part def` → `Parts::Part`; `item def` → `Items::Item`; `port def` → `Ports::Port`;
    `attribute def`, `enum def`, KerML `datatype` → `Base::DataValue`; `action def` →
    `Actions::Action`; `state def` → `States::StateAction`; `calc def` →
    `Calculations::Calculation`; `constraint def` → `Constraints::ConstraintCheck`;
    `requirement def` → `Requirements::RequirementCheck`; `concern def` →
    `Requirements::ConcernCheck`; `case def`, `analysis def`, `verification def`, `use case def` →
    `Cases::Case`, `AnalysisCases::AnalysisCase`, `VerificationCases::VerificationCase`,
    `UseCases::UseCase`; `connection def`, `interface def`, `allocation def`, `flow def` →
    `Connections::Connection`, `Interfaces::Interface`, `Allocations::Allocation`,
    `Flows::MessageAction`; `occurrence def`, `individual def` → `Occurrences::Occurrence`;
    `metadata def` → `Metadata::MetadataItem`; `rendering def` → `Views::Rendering`.
  - Views: a view (definition or usage — the AST does not tell them apart) → `Views::View`; a
    viewpoint definition → `Views::ViewpointCheck`.
  - Transitions: a transition usage (`SysmlTransitionNode`) → `Actions::transitionActions`, the
    library feature that owns `accepter` and `effect`, so `delivering.accepter.acceptedMessage`
    resolves.
  - KerML classifiers: `class` → `Occurrences::Occurrence`; `struct` → `Objects::Object`;
    `assoc struct` → `Objects::LinkObject`; `assoc` → `Links::Link`; `classifier`, `type` →
    `Base::Anything`; `metaclass` → `Metaobjects::Metaobject`; `behavior` →
    `Performances::Performance`; `function` → `Performances::Evaluation`; `predicate` →
    `Performances::BooleanEvaluation`; `interaction` → `Links::Link` and
    `Performances::Performance`.
  - Usages: `part`, `actor`, `stakeholder` → `Parts::parts`; `port` → `Ports::ports`; `item` →
    `Items::items`; `attribute`, `enum`, `enum value` → `Base::dataValues`; `action`, `entry`, `do`,
    `exit`, `merge`, `decide`, `join`, `fork` → `Actions::actions`; `accept` →
    `Actions::acceptActions`; `send` → `Actions::sendActions`; `terminate` →
    `Actions::terminateActions`; `state` → `States::stateActions`; `calc` →
    `Calculations::calculations`; `constraint`, `assume constraint`, `require constraint`,
    `assert constraint` → `Constraints::constraintChecks`; `perform` → `Actions::actions`; `exhibit`
    → `States::stateActions`; `include` → `UseCases::useCases`; `timeslice` →
    `Occurrences::Occurrence::timeSlices`; `snapshot` → `Occurrences::Occurrence::snapshots`;
    `requirement` → `Requirements::requirementChecks`; `concern`
    → `Requirements::concernChecks`; `case`, `analysis`, `verification`, `use case` →
    `Cases::cases`, `AnalysisCases::analysisCases`, `VerificationCases::verificationCases`,
    `UseCases::useCases`; `rendering` → `Views::renderings`; `occurrence`, `event occurrence` →
    `Occurrences::occurrences`.
  - KerML usages: `step` → `Performances::performances`; `expr` → `Performances::evaluations`;
    `bool` → `Performances::booleanEvaluations`; `inv` → `Performances::trueEvaluations`;
    `connector` → `Links::links`; `binding` → `Links::selfLinks`; `succession` →
    `Occurrences::happensBeforeLinks`; `flow` → `Transfers::flowTransfers` (the library's default
    subsetting for non-succession flows) then `Transfers::transfers`; `succession flow` →
    `Transfers::flowTransfersBefore` (the default subsetting for succession flows) then
    `Transfers::flowTransfers`, `Transfers::transfers` and `Occurrences::happensBeforeLinks`.
    The library declares `flowTransfers`/`flowTransfersBefore` (and every other
    connection-like library feature) with connection syntax, which `AstBuilder` records as a
    `SysmlConnectionNode` carrying no supertypes or typing, so the default alone would be a
    lookup dead end; the extra entries are the nearest ancestors that carry members.
  - Connection-like usages (`SysmlConnectionNode`, by `ConnectionKeyword`): `connection` →
    `Connections::connections`, `Parts::parts`; `interface` → `Interfaces::interfaces`,
    `Connections::connections`, `Parts::parts`; `allocation` → `Allocations::allocations`,
    `Connections::connections`, `Parts::parts`; `message` → `Flows::messages`,
    `Transfers::transfers`, `Actions::actions`; `flow` → `Flows::flows`, `Flows::messages`,
    `Transfers::flowTransfers`, `Transfers::transfers`, `Actions::actions`; `binding` →
    `Links::selfLinks`. Each row starts with the specification default and continues with the
    nearest ancestors that carry members, for the same dead-end reason.
  - **Left on the `Base::things` fallback** (usage keywords with no row): `ref` and `subject`
    (reference usages, whose specification default is `Base::things`) and KerML `feature`
    (the root feature kind). No other keyword `AstBuilder` emits is without a row.

  Implicit specializations are **not** `SysmlEdge`s and are not part of the public `GeneralTypes`:
  they exist only as lookup input, so `ResolvedEdges`, `SemanticIndex`, `query`, `export`, every
  layout, and the serialized standard library are unchanged by construction. Nodes that are not
  name-resolution namespaces (transition, satisfy, dependency, metadata usage nodes) are not
  modeled. `AstBuilder` produces no node at all for `perform`, `exhibit`, `include`, `assert`,
  `frame` (other than the concern member), SysML `succession flow` usages, time slice, snapshot
  or `individual` usages, so there is nothing to specialize for them (a time slice or snapshot is
  recorded only as `occurrence`, and `individual` is not refined to `Occurrences::Life`). Each
  listed target is checked to exist in the embedded standard library by a test.
- The global namespace is every top-level declaration of every loaded root namespace, user and
  standard library alike (KerML 8.2.3.5.2).
- *Feature chains* (`a.b.c`, KerML 8.2.3.5.1): a reference containing a `.` outside single quotes
  is split into **parts**. The first part is an ordinary (possibly qualified or `$::`-prefixed)
  name, resolved by full resolution from the local namespace; every later part is resolved as
  a sequence of **visible** lookups, starting in the element the previous part resolved to. That
  lookup already has the semantics a chain needs: the feature's own inline members first, then
  everything it inherits through its lookup general types (explicit typing, subsetting,
  redefinition, and the implicit specializations — so `p1 :> p; p1.x` reaches the members of
  `p`'s type, a port typed by a conjugated port definition reaches that definition's members, and
  a member a nested usage redefines is the nested usage), transitively; only public memberships
  are found, so a private or protected member is not reachable through a chain even by
  subsetting, while the **first** part still sees its namespace's private members; and an alias
  member resolves to its target. A chain resolves only when every part does; a part that does not
  resolve leaves the whole reference unresolved. The result of a chain is the declared element of
  the last part (`Resolve`) and, for callers that key relationships by feature instances, the
  **instance path** (`ResolveFeatureChain`, internal): the first part's element followed by the
  simple name of each later part's element — `P::p1::x::x1` for `p1.x.x1` although `x` is
  declared by the type of `p`. For a reference without a chain the instance path equals the
  element, so non-chain behavior is unchanged.
- *Quoted names* compare equal with and without their enclosing single quotes (`'PowerTrain'` and
  `PowerTrain` name the same element, KerML 8.2.2.2.1) in owned and imported memberships, alias
  short names, and top-level (global) names; dots and `::` inside quotes never split a name.
- *File-root imports* (resolving mode only): the imports written at the root of a source file are
  not part of any namespace's `Children` in the declarations, so `RegisterRootImports` records
  them against the qualified name of **every declaration the file contributes** (not merely its
  top-level names: a package may be split across files, each with its own root imports, so a
  top-level name cannot identify the file). Full resolution consults them after the owning
  namespaces and before the global namespace: the referencing file's imports when the caller
  supplies them (the semantic pass always does), otherwise those registered for the local
  namespace's declaration. A type's own general types (and a feature's redefined feature) are
  resolved with the imports registered for that type's declaration — otherwise a supertype
  reachable only through a file-root import would be missing from `GeneralTypes` and the inherited
  members it supplies would not resolve.

##### Key Methods

- `Resolve(reference, localNamespace)` (KerML 8.2.3.5.1): a single segment by full resolution; a
  `$::name` in the global namespace; otherwise the qualification part by this same process (it
  must yield a namespace), then the last segment by visible resolution in that namespace. A
  dotted reference is a **feature chain** (see Data Model): `SplitChain` splits it on `.` outside
  quotes (a reference with no `.` takes a fast path that skips the split), the first part is
  resolved as above, and each later part by `Lookup(segment, current, Visible)` (the cache and
  the in-progress guard apply to every chain step). An internal overload additionally accepts the
  file-root imports for a reference whose local namespace is the file root itself.
- `ResolveFeatureChain(reference, localNamespace, fileRootImports)` (internal): `Resolve` that
  also returns the instance path of a chain (`FeatureChainResolution(Element, InstancePath)`).
  `ReferenceResolver` records relationship ends (connect, bind, transition, allocate, satisfy,
  verify) at the instance path, which is what `GeneralViewLayoutStrategy` maps to boxes, and the
  declared element for type-like references (supertype, typing, redefinition).
- `ResolveRedefinedFeature(reference, owningType)` (KerML 8.2.3.5.1, Redefinition): `Resolve`
  with each lookup general type (declared, then implicit) of `owningType` as the local namespace,
  in turn. A bare name is looked up in the general type itself with the **RedefinitionTarget** scope —
  as Inheritable (the redefining feature inherits from it, so the general type's private members
  cannot be redefined, protected ones can) but without the redefined-member exclusion described
  below, because library associations routinely redefine a name that a general type's own
  feature already redefined; the enclosing namespaces outward from it, and qualified names,
  resolve as in `Resolve`. A redefinition target that is a chain (`:>> localClock.currentTime`)
  resolves its first part this way and the remaining parts by visible resolution.
- `ResolveRedefinition(reference, owningType, redefiningFeature, fileRootImports)` (internal):
  `ResolveRedefinedFeature`, then — a documented lenient deviation — ordinary `Resolve` from the
  owning type; the redefining feature itself is never an acceptable target. When ordinary
  resolution finds only the redefining feature (an unnamed `part :>> part1;` takes the redefined
  name as its own, so the owning type owns it under that name), the reference is resolved again
  from the owning type's own owner and continues outward, which reaches a member of an enclosing
  or sibling feature (`part1` declared on the enclosing usage's type).
- `GeneralTypes(typeName)` (KerML 8.2.3.5.3, inherited memberships): the declared general types
  described under Data Model (public; implicit specializations are not included).
- `LookupGeneralTypes(typeName)` (internal): `GeneralTypes` plus the implicit specializations;
  the general types inherited lookup and redefinition resolution walk.

Lookup scopes, applied per namespace (KerML 8.2.3.5.3):

- **Local** (full resolution of a simple name, each namespace outward): every owned membership
  and alias, every import, and the non-private memberships inherited from the lookup general
  types (explicit and implicit, transitively: each general type's own lookup recurses into its
  general types, so `part def P` reaches `Parts::Part`, `Items::Item`, `Objects::Object`,
  `Occurrences::Occurrence`, `Base::Anything`).
- **Inheritable** (what a specialization inherits): non-private owned memberships and imports,
  recursively inherited non-private memberships (protected members are inherited, private ones
  are not).
- **RedefinitionTarget**: Inheritable, for resolving the target of a redefinition (see above).
- **Visible** (later qualified-name segments, and what an import brings in): public owned
  memberships, public imports, public inherited memberships. `import all` brings in every
  membership regardless of visibility (KerML 8.3.2.4.2 `isImportAll`).

In the inherited step of a Local/Inheritable/Visible lookup, a member `M` found through a general
type is skipped when a **named** feature owned by the specialized type redefines `M` (KerML
`Type::removeRedefinedFeatures`): the lookup continues with the next general type and then the
enclosing namespace. The redefined targets of a namespace's owned features are resolved once per
namespace (strictly, against its general types, with the cycle guard); an unnamed redefining
feature is not registered under any name in the declarations, so it hides nothing and the
inherited member stays visible.

An import brings in (KerML 8.3.2.4.4/8.3.2.4.6): for a membership import `import A::B;` the
membership `B` (by its last segment, its element's short name, or — when `B` is an alias — the
alias's own short name); if recursive (`A::B::**`),
also the visible members of `B` and its nested namespaces, least-nested first; for a namespace
import `import A::*;` the visible members of `A`; if recursive (`A::*::**`), also those of its
nested namespaces. A filtered import (`[expr]`) admits a candidate only when the `importFilter`
predicate accepts it (SysML 7.5.4); without a predicate, filter conditions are ignored. For a
recursive filtered import the predicate is applied while enumerating nested candidates, so a
rejected match does not stop the search for a later one that satisfies the conditions.

##### Performance

The lookup cache is keyed by (name, namespace, scope), so the implicit general types add one cached
lookup per (name, library type) the first time a name is searched through them. Measured on the OMG
corpus lint (251 files) the cost is about +3% CPU time (alternating runs, noisy machine), and the
embedded standard library compiles in comparable time. Feature chains add one cached visible
lookup per chain part, and quote equivalence a length check and, only for names that differ in
length by exactly two, a comparison; measured on the same corpus lint (four alternating runs
against the previous stage on a noisy machine) the chain stage costs about +2.5% mean CPU time
(wall time unchanged within noise).

##### Error Handling

Unresolvable names return `null` (`Resolve`, `ResolveRedefinedFeature`, `ResolveFeatureChain`).
Imports and specializations may be circular (KerML 8.2.3.5.1); a lookup already
in progress for the same (name, namespace, scope) contributes nothing instead of recursing, so
resolution always terminates. A feature that specializes a chain through itself
(`ref x : T :> x.y`) re-enters its own general types while they are being derived; the re-entry
contributes nothing, and a lookup that missed while that happened is not cached, so the answer
does not depend on which reference is resolved first. Null arguments throw
`ArgumentNullException`.

##### Dependencies

- `SysmlNode` and its subclasses, `SysmlAliasNode`, `SysmlImportNode`, `SysmlVisibility`,
  `SysmlEdge`/`SysmlEdgeKind` (Semantic Model subsystem).

##### Callers

- `ReferenceResolver` (Language Semantic Model) resolves every reference of the semantic model with
  an internal resolving-mode instance (import filter from
  `FilterExpressionEvaluator.CreateImportFilter`, file-root imports registered per file).
- `FilterExpressionEvaluator.CreateNameResolver` (Language Filtering) constructs a public-mode one
  with a filtered-import predicate.
- `GeneralViewLayoutStrategy`, `InterconnectionViewLayoutStrategy`, and `GridViewLayoutStrategy`
  (Core Layout) use it, as the last resolution step, to resolve the raw type, supertype,
  redefinition, and subsetting references the semantic pass left unresolved or could not be paired
  with (a reference the semantic pass resolved is used as resolved). A reference KerML resolution
  rejects stays unresolved: the layouts draw no relationship for it and match no element by bare
  simple name.
