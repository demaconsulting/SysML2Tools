#### AstBuilder

##### Overview

`AstBuilder` extends `SysMLv2ParserBaseVisitor<SysmlNode?>` and builds a typed AST from the
ANTLR4 CST produced by `SysMLv2Parser`.

##### Namespace Stack

A `List<string> _namespaceStack` tracks the current nesting path. When entering a named package
or definition, the name is pushed; it is popped before returning. `QualifyName(name)` joins the
stack with `::` to form the fully-qualified name.

##### Key Methods

| Method | Input Context | Output |
| --- | --- | --- |
| `VisitRootNamespace` | `RootNamespaceContext` | `SysmlPackageNode` (root) |
| `VisitPackage` | `PackageContext` | `SysmlPackageNode` |
| `VisitLibraryPackage` | `LibraryPackageContext` | `SysmlPackageNode` |
| `VisitPartDefinition` | `PartDefinitionContext` | `SysmlDefinitionNode` |
| `VisitAttributeDefinition` | `AttributeDefinitionContext` | `SysmlDefinitionNode` |
| `VisitItemDefinition` | `ItemDefinitionContext` | `SysmlDefinitionNode` |
| `VisitViewDefinition` | `ViewDefinitionContext` | `SysmlViewNode` |
| `VisitViewUsage` | `ViewUsageContext` | `SysmlViewNode` |
| `VisitViewpointDefinition` | `ViewpointDefinitionContext` | `SysmlViewpointNode` |
| `VisitAllocationUsage` | `AllocationUsageContext` | `SysmlConnectionNode` (`ConnectionKeyword = "allocation"`) |
| `VisitSatisfyRequirementUsage` | `SatisfyRequirementUsageContext` | `SysmlSatisfyNode` |
| `VisitRequirementUsage` | `RequirementUsageContext` | `SysmlFeatureNode` (`FeatureKeyword = "requirement"`) |

`GetDeclaredName(IdentificationContext)` handles the three grammar alternatives:

- `< shortName > declaredName` (alt 1): returns `name(1).GetText()`.
- `< shortName >` (alt 2): no declared name — returns null.
- `declaredName` (alt 3): returns `name(0).GetText()`.

Elements with no declared name are treated as anonymous and are not registered in the symbol table.

`GetSubclassificationSupertypes(SubclassificationPartContext)` iterates
`ownedSubclassification()` entries and calls `qualifiedName().GetText()` on each to produce
the supertype name list.

`VisitImportRule` builds a `SysmlImportNode` for both the wildcard (`namespaceImport`) and
membership (`membershipImport`) grammar alternatives. In both branches it sets the inherited
`ImportedNames` to a single-element list containing the extracted qualified/dotted name text,
alongside the existing `ImportedNamespace` property — letting `ReferenceResolver` treat import
references uniformly with `SupertypeNames` and `FeatureTyping` without any node-type
special-casing.

`BuildUsageNode` additionally calls `ExtractRedefinedFeature(decl?.featureSpecializationPart())`
alongside `ExtractFeatureTyping`, setting the result on the constructed `SysmlFeatureNode`'s
`RedefinedFeatureName` property. `ExtractRedefinedFeature` mirrors `ExtractFeatureTyping`'s exact
structure: it loops `featureSpecializationPart().featureSpecialization()`, and for each entry
whose `redefinitions()` is non-null, first checks `redefinitions().redefines()?.ownedRedefinition()`
(the first redefined feature, held by the `redefines`/`:>>` clause) and returns its `.GetText()`;
if that clause is absent, it falls back to the first non-null entry of
`redefinitions().ownedRedefinition()` (the `redefines (COMMA ownedRedefinition)*` list's
additional entries). It returns `null` when no `redefinitions()` is present anywhere in the
part — i.e. the feature declares no redefinition. Both the `redefines` keyword form and the
`:>>` operator form parse into the same `RedefinesContext` (they differ only in which terminal —
`REDEFINES` or `COLON_GT_GT` — the grammar matched), so `ExtractRedefinedFeature` handles both
forms identically without needing to branch on which token was used. The raw reference text is
captured verbatim — including qualified `Owner::feature` forms — with no resolution attempted;
resolution happens later, in `ReferenceResolver`.

`BuildUsageNode` also calls `ExtractSubsettingTargetNames(decl?.featureSpecializationPart())`,
setting the result on the constructed `SysmlFeatureNode`'s inherited `SupertypeNames` property —
mirroring `ExtractRedefinedFeature`'s structure (first checking
`subsettings().subsets()?.ownedSubsetting()`, the target held by the `subsets`/`:>` clause, then
falling back to `subsettings().ownedSubsetting()`'s remaining comma-separated entries) but
collecting *every* match into a list rather than returning only the first. Before this, a
usage-level `subsets`/`:>` clause (as opposed to a definition-level `:>` specialization, already
handled by `GetSubclassificationSupertypes`) was never extracted at all, so it produced no
`Supertype` edge and was invisible to `ReferenceResolver` — silently breaking its bare-name
redefinition ancestor-chain walk whenever the redefining feature's owner was itself a
usage-level subsetting rather than a `part def` specialization (the exact shape used by the OMG
corpus fixture `1c-PartsTreeRedefinition.sysml`'s `part vehicle1_c1 :> vehicle1 { ... }`).

`VisitAnnotatingElement(AnnotatingElementContext)` intercepts the `comment` and `documentation`
grammar alternatives of `annotatingElement` (`comment | documentation | textualRepresentation |
metadataFeature`) and returns a private `AnnotationCapture` sentinel node wrapping a
`SysmlAnnotation` built from `ExtractCommentText(REGULAR_COMMENT())`. `textualRepresentation`
and `metadataFeature` are unhandled (falls through to `base.VisitAnnotatingElement`, returning
`null`, unchanged from prior behavior).

`ExtractCommentText(ITerminalNode?)` strips the `/*`/`//*` opening delimiter and trailing `*/`
closing delimiter from a `REGULAR_COMMENT` token's raw text, preserving all interior
whitespace, newlines, and bullet characters verbatim.

The four body-collection helpers — `CollectBodyElements`, `CollectDefinitionBodyItems`,
`CollectChildren`, and `CollectTypeBodyItems` — each return a
`(IReadOnlyList<SysmlNode> Children, IReadOnlyList<SysmlAnnotation> Annotations)` tuple.
While iterating body items, any `Visit(item)` result that is an `AnnotationCapture` is routed
into the `Annotations` list instead of `Children`; all other non-null results are added to
`Children` as before. Every one of the eight call sites that construct a body-bearing node
(`VisitRootNamespace`, `VisitPackage`, `VisitLibraryPackage`, `VisitActionDefinition`,
`VisitStateDefinition`, `BuildUsageNode`, `BuildDefinitionNode`, `BuildClassifierNode`)
unpacks both elements and sets `Children` and `Annotations` on the constructed node.

An `AnnotationCapture` (private nested `SysmlNode` subtype carrying a single `SysmlAnnotation`)
is never registered as a `[JsonDerivedType]` and must never reach a real `Children` list — it
is always intercepted by one of the four collection helpers before being added. If any future
body-bearing construct bypasses all four helpers (e.g. a hand-rolled loop calling `Visit`
directly), an `AnnotationCapture` leaking into `Children` throws `NotSupportedException` during
JSON serialization (the polymorphic type resolver rejects an unregistered runtime type), which
surfaced and was fixed during this unit's implementation for the `CollectTypeBodyItems`
(KerML classifier body) call path.

`VisitAllocationUsage` builds a `SysmlConnectionNode` with `ConnectionKeyword = "allocation"` for
`allocate A to B;` usages, reusing the existing `ExtractConnectorEnds` helper: the generated
`AllocationUsageDeclarationContext.connectorPart()` exposes the exact same `ConnectorPartContext`
shape as `ConnectionUsageContext.connectorPart()`, so no new endpoint-extraction logic is needed.

`VisitSatisfyRequirementUsage` builds a `SysmlSatisfyNode` for `satisfy X by Y;` usages. The
satisfied requirement's raw reference text is taken from `ownedReferenceSubsetting()` when the
`satisfy <ref>` form is used, falling back to the declared/typed name of the
`satisfy requirement <usageDeclaration>` form. The optional satisfying subject's raw reference
text comes from `satisfactionSubjectMember()` (the `by <subject>` clause), or is `null` when
absent.

`VisitViewDefinition` builds a `SysmlViewNode` for `view def` definitions, additionally scanning
`context.viewDefinitionBody()?.viewDefinitionBodyItem()` via the shared
`ExtractViewRenderAndFilter<TItem>` helper (see below) to populate `RenderTargetName` and
`FilterExpressionText`. `VisitViewUsage` builds a `SysmlViewNode` for named `view` usages (the
only body form that may additionally contain `expose` members) the same way, plus
`ExtractExposedNames` to populate `ExposedNames`. Unnamed view usages are skipped (no declared
name), mirroring the existing anonymous-element convention.

**`VisitViewUsage` is an intentional capability addition, not merely an `expose`-capture
prerequisite.** Before this override existed, named `view Name { ... }` usages were silently
dropped by the default `VisitChildren` aggregation — only `view def` declarations were ever
visible as renderable top-level declarations. Adding `VisitViewUsage` means every named `view`
usage in a workspace (whether or not it declares `expose`) now becomes its own `SysmlViewNode`
declaration that the render subsystem discovers and renders. This is a deliberate, documented
increase in output surface area: for example, the OMG corpus fixture
`test/SysMLModels/OMG/validation/11-ViewAndViewpoint/11b-SafetyAndSecurityFeatureViews.sysml`
declares 2 `view def`s plus 3 named `view` usages, so rendering it with no `--view` filter now
produces 5 output files instead of 2 (see
`RenderSubsystemTests.RenderSubsystem_OmgSafetyFeatureViewsCorpus_RendersAllNamedViewUsages`).

`ExtractViewRenderAndFilter<TItem>(IEnumerable<TItem> bodyItems)` is a single generic helper
shared by both `VisitViewDefinition` (`ViewDefinitionBodyItemContext`) and `VisitViewUsage`
(`ViewBodyItemContext`) — the two context types are unrelated in the generated parser's type
hierarchy but expose identically-shaped `viewRenderingMember()`/`elementFilterMember()`
accessors, so a type-switch pattern inside two small private helpers
(`GetViewRenderingMember`/`GetElementFilterMember`) lets one generic method serve both body-item
types without duplicating the scan loop. The first `render` member wins if more than one
appears (a defensive tie-break, not a validated SysML constraint). `ExtractRenderTargetName`
follows the same two-form fallback pattern `VisitSatisfyRequirementUsage` uses: the direct
reference form (`ownedReferenceSubsetting()`), falling back to the typed-placeholder form's
feature typing (`ExtractFeatureTyping`), falling back to the raw usage text. The filter
expression's raw source text is taken verbatim from
`elementFilterMember().ownedExpression()?.GetText()` — never evaluated.

`ExtractExposedNames(IEnumerable<ViewBodyItemContext> bodyItems)` collects the raw reference
text of every `expose <name>;` member in source order, reusing the shared `ExtractImportTarget`
helper (see below) against each `expose` member's wrapped `namespaceImport()`/
`membershipImport()` — the identical grammar shape `import` uses.

`ExtractImportTarget(NamespaceImportContext?, MembershipImportContext?)` is a shared helper
extracted from `VisitImportRule`'s previously inline logic, returning the extracted
qualified/dotted name text and whether the reference is a wildcard, for either the
namespace-import form (`qualifiedName::*`, always a wildcard), the membership-import form
(`qualifiedName`, optionally `::**`), or the bracketed-filter form nested inside a
namespace-import (`qualifiedName::**[<filterExpr>]`) — the dominant `expose` shape in the real
OMG corpus (e.g. `expose vehicle::**[@Safety];`). The grammar nests the qualified name two levels
deeper for that third form: `namespaceImport -> filterPackage -> filterPackageImportDeclaration ->
(membershipImport | namespaceImportDirect)`. `ExtractImportTarget` descends into
`filterPackage().filterPackageImportDeclaration()` and extracts the qualified name from whichever
of `membershipImport()`/`namespaceImportDirect()` is present there, rather than only checking the
direct `qualifiedName()` child (which is null for this alternative). `VisitImportRule` and
`ExtractExposedNames` both call this one helper rather than duplicating the extraction logic, per
the Copy-Paste Programming anti-pattern guidance in coding-principles.md.

`VisitRequirementUsage` performs a minimal capture (name/qualified-name only, so named
requirement usages become resolvable symbols) and additionally invokes `FindVerificationMembers`
against its own `requirementBody()` (when present) to populate `VerifiedRequirementNames` —
covering the case where a `verify` member appears directly inside a `requirement { }` usage
body.

`FindVerificationMembers(IParseTree root)` / `CollectVerificationMembers(IParseTree, List<string>)`
/ `ExtractVerifiedRequirementName(RequirementVerificationUsageContext?)` are a narrow, additive
recursive tree-walk helper trio (not a generic body-traversal rewrite) used to find
`requirementVerificationMember` nodes nested arbitrarily inside `objectiveMember →
objectiveRequirementUsage → requirementBody` chains, since `caseBodyItem` does not expose
`requirementVerificationMember` via a typed accessor. `FindVerificationMembers` seeds an empty
list and delegates to `CollectVerificationMembers`, which recursively walks every child of the
given `IParseTree`, extracting a name via `ExtractVerifiedRequirementName` whenever it encounters
a `RequirementVerificationMemberContext`. `ExtractVerifiedRequirementName` prefers the
`ownedReferenceSubsetting()` reference form (`verify <ref>;`), falling back to the typed-
placeholder form's feature typing (`verify requirement <name> : <Type>;`, via the existing
`ExtractFeatureTyping` helper). This trio is wired into 4 call sites: `VisitCaseDefinition`,
`VisitAnalysisCaseDefinition`, `VisitVerificationCaseDefinition` (via an optional
`specializedBody` parameter added to the shared `BuildDefinitionFromDeclaration`, defaulting to
`null` for backward compatibility with callers that don't have a specialized body to scan), and
`VisitRequirementUsage` (via its own `requirementBody()`). This walk is safe from double-counting
because nothing else in `AstBuilder` currently visits into `requirementBody`/`caseBody`
subtrees.

##### Error Handling

Anonymous elements (null declared names) are silently skipped — visitor methods return `null`
and the caller discards the result. `BuildDefinitionNode` returns `null` when passed a `null`
`DefinitionContext`. No exceptions are thrown; malformed CST nodes produce `null` or empty
results without propagating failures.

##### Dependencies

- `SysMLv2ParserBaseVisitor<SysmlNode?>` (ANTLR4 runtime) — base class providing visitor
  dispatch over the CST.
- `SysMLv2Parser` — provides all CST context types consumed by the visitor methods.
- `SysmlNode` hierarchy (`SysmlPackageNode`, `SysmlDefinitionNode`, `SysmlViewNode`,
  `SysmlViewpointNode`, `SysmlSatisfyNode`, `SysmlConnectionNode`) — AST node types constructed
  by the visitor.
- `SysmlAnnotation` / `SysmlAnnotationKind` — captured comment/documentation data attached to
  `SysmlNode.Annotations`.

##### Callers

`WorkspaceLoader.BuildStdlibSemanticAsync` and `WorkspaceLoader.ParseUserFileAsync` each create
a fresh `AstBuilder` instance and call `Build(RootNamespaceContext)` on the CST root produced
by `WorkspaceParser.ParseSourceToCst`.
