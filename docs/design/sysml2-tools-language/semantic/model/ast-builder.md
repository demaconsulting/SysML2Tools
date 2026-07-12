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
| `VisitDependency` | `DependencyContext` | `SysmlDependencyNode` |
| `VisitBindingConnectorAsUsage` | `BindingConnectorAsUsageContext` | `SysmlConnectionNode` (kind `binding`) |
| `VisitStateBodyItem` | `StateBodyItemContext` | `SysmlNode` or `MultiNodeCapture` (usage + transitions) |
| `VisitEntryActionMember` | `EntryActionMemberContext` | `SysmlFeatureNode` (`FeatureKeyword = "entry"`) |
| `VisitDoActionMember` | `DoActionMemberContext` | `SysmlFeatureNode` (`FeatureKeyword = "do"`) |
| `VisitExitActionMember` | `ExitActionMemberContext` | `SysmlFeatureNode` (`FeatureKeyword = "exit"`) |
| `VisitActionBodyItem` | `ActionBodyItemContext` | `SysmlNode` or `MultiNodeCapture` (node + successions) |
| `VisitMergeNode` | `MergeNodeContext` | `SysmlFeatureNode` (`FeatureKeyword = "merge"`) |
| `VisitDecisionNode` | `DecisionNodeContext` | `SysmlFeatureNode` (`FeatureKeyword = "decide"`) |
| `VisitJoinNode` | `JoinNodeContext` | `SysmlFeatureNode` (`FeatureKeyword = "join"`) |
| `VisitForkNode` | `ForkNodeContext` | `SysmlFeatureNode` (`FeatureKeyword = "fork"`) |
| `VisitAcceptNode` | `AcceptNodeContext` | `SysmlFeatureNode` (`FeatureKeyword = "accept"`) |
| `VisitSendNode` | `SendNodeContext` | `SysmlFeatureNode` (`FeatureKeyword = "send"`) |

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

When a usage has no explicit declared name (`GetDeclaredName` returns `null`) but does have a
redefinition (`redefined is not null`), `BuildUsageNode` derives an implicit name via
`SimpleNameFromReference(redefined)` — the trailing `::`- or `.`-separated segment of the
redefined feature's reference text, whichever separator occurs furthest to the right (an
`ownedRedefinition` is grammatically `qualifiedName ( DOT qualifiedName )*`, so the reference can
be a dotted feature-chain path like `tank.fuelTankPort`, not just a `::`-qualified name) — and
uses this `effectiveName` everywhere the declared name would otherwise be used: the
namespace-stack push/pop, the constructed node's `QualifiedName`, and its `Name` property. This
mirrors SysML v2's own naming rule that an implicitly-named redefining usage inherits the
redefined feature's name (e.g. `port redefines fuelTankPort { ... }` is named `fuelTankPort`),
and allows such usages — and any references to them (including `bind` connector ends) — to
resolve correctly instead of remaining anonymous and unresolvable.

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
remains unhandled, but `metadataFeature` is now captured as a first-class `SysmlMetadataNode`
child via `BuildMetadataNode`: it records the annotation type reference and any directly-assigned
scalar literal attributes (Boolean/Number/String), preserving unsupported value expressions as raw
text with `MetadataAttributeValueKind.Unsupported`.

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

`VisitBindingConnectorAsUsage` builds a `SysmlConnectionNode` with `ConnectionKeyword = "binding"`
for the common `bind A = B;` (`bindingConnectorAsUsage`) grammar shape, reusing the shared
`ConnectorEndReference` helper against each of the rule's `connectorEndMember()` entries — the
same helper `connectionUsage`'s endpoint extraction already uses. The longer
`bindingConnector`/`typeBody` grammar form has zero corpus evidence and is a documented,
intentional non-goal (not attempted). `ReferenceResolver` resolves `"binding"`-keyword
`SysmlConnectionNode` endpoints via the same dotted-feature-chain walk it already applies to
`"connection"`/`"message"`.

`VisitDependency` builds a `SysmlDependencyNode` for a standalone `dependency A, B to C, D;`
declaration. The grammar's `dependency` rule exposes a single flat `qualifiedName()` list (no
separate "from" vs. "to" sub-rules), so `VisitDependency` splits it positionally: every
`qualifiedName()` whose start token index is before the `TO()` terminal's token index is a
"from" (client) name, and every one after is a "to" (supplier) name. This also correctly handles
the grammar's optional `FROM` keyword (e.g. `dependency z to x, y;`, with no explicit `from`
before `z`) since the split is driven purely by position relative to `TO`, never by the
presence/absence of the `FROM` keyword token itself. `ReferenceResolver` resolves every
`FromNames` entry against every `ToNames` entry (a cross product), emitting one
`SysmlEdgeKind.Dependency` edge per resolved pair.

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
`ExtractExposedNames` to populate `ExposeMembers`. Unnamed view usages are skipped (no declared
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
expression's raw source text is reconstructed with `GetOriginalText(...)`, preserving inter-token
whitespace so the Filtering subsystem can re-lex it faithfully rather than receiving
`RuleContext.GetText()`'s concatenated token stream.

`ExtractExposedNames(IEnumerable<ViewBodyItemContext> bodyItems)` collects one `ExposeMember`
per `expose <name>;` member in source order, reusing the shared `ExtractImportTarget` helper (see
below) against each `expose` member's wrapped `namespaceImport()`/`membershipImport()` — the
identical grammar shape `import` uses. When an `expose` member uses the dominant corpus form
`qualifiedName::**[<expr>]`, the same helper also returns the bracketed filter expression's
original source text, which `ExtractExposedNames` pairs together with that same entry's qualified
name into a single `ExposeMember(QualifiedName, BracketFilterExpressionText)` record — rather than
appending the qualified name and the bracket-filter text to two separate, unpaired flattened lists
(the earlier Phase 1 shape) — so a view with more than one `expose` member never loses track of
which bracket filter belongs to which exposed path. `ExposeScopeResolver` (Phase 2a) depends on
this pairing to evaluate each entry's bracket filter against that entry's own containment subtree.

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

`VisitStateUsage` additionally calls `ExtractFeatureTyping(decl?.featureSpecializationPart())` and
sets the result on the constructed `SysmlFeatureNode`'s `FeatureTyping` property — previously this
was always left `null` for state usages, unlike every other usage kind built via `BuildUsageNode`.
This is a prerequisite for `StateTransitionViewLayoutStrategy`'s expose-scoping root selection
(which resolves an exposed usage's type via its `Typing` edge) and for
`ReferenceResolver.TryResolveInheritedActionMember` (below) to find the enclosing usage's
supertype when its source has no explicit `state usage : Type { ... }` form.

**Attached-transition state bodies and entry/do/exit action features.** The `stateBodyItem`
grammar rule has six alternatives; two of them attach a transition directly onto the immediately
preceding usage within the same alternative rather than exposing it as a separate
`transitionUsageMember`: `behaviorUsageMember (targetTransitionUsageMember)*` (e.g.
`state off; accept Signal then starting;`) and `entryActionMember (entryTransitionMember)*` (e.g.
`entry action initial; then off;`). Before `VisitStateBodyItem` existed, `AstBuilder` only ever
visited the leading usage of each alternative (via the default `VisitChildren` aggregation) and
silently dropped every attached transition, so this common OMG Annex A.7 idiom produced no
transition edge at all.

`VisitStateBodyItem` dispatches all six alternatives explicitly:

- `nonBehaviorBodyItem`, `transitionUsageMember`, `doActionMember`, `exitActionMember` — passed
  straight through to `Visit(...)` (no attached-transition shape applies).
- `behaviorUsageMember (targetTransitionUsageMember)*` — visits the usage, then calls
  `BuildAttachedTransition` once per `targetTransitionUsageMember` entry, each producing a
  `SysmlTransitionNode` whose `Source` is the visited usage's `Name`.
- `entryActionMember (entryTransitionMember)*` — visits the entry action (via
  `VisitEntryActionMember`, below), then calls `BuildEntryAttachedTransition` once per
  `entryTransitionMember` entry (handling both the `guardedTargetSuccession` and bare `THEN`
  grammar alternatives), each producing a `SysmlTransitionNode` sourced from the entry action's
  name.

When one or more attached transitions are produced, `VisitStateBodyItem` returns a
`MultiNodeCapture` (a private nested `SysmlNode` subtype, mirroring the existing
`AnnotationCapture` sentinel pattern exactly) wrapping the usage plus its transition(s); when none
are produced, it returns the visited usage/pass-through result directly with no wrapping.
`CollectChildren` — the only collection helper that ever visits a `stateBodyItem()` (confirmed by
inspection: `VisitStateDefinition` and `VisitStateUsage` are the sole two call sites) — flattens
any `MultiNodeCapture.Nodes` it encounters into the resulting `Children` list, exactly as it
already does for `AnnotationCapture`. Like `AnnotationCapture`, `MultiNodeCapture` is never
registered as a `[JsonDerivedType]` and must never reach a real `Children` list; the same
single-call-site guarantee that protects `AnnotationCapture` protects it here.

`VisitEntryActionMember`, `VisitDoActionMember`, and `VisitExitActionMember` each delegate to a
shared `BuildStateActionFeatureNode(usage, keyword)` helper, which builds a **minimal**
`SysmlFeatureNode` — `FeatureKeyword` set to `"entry"`/`"do"`/`"exit"` respectively, `Children`
always empty — using `ExtractStateActionName` to determine the node's `Name`. Entry/do/exit
action *bodies* are behavioral (statement sequences: assignments, sends, control flow), which is
out of scope for this unit's declarative AST; this deliberately mirrors the existing
`VisitRequirementUsage` "minimal capture" pattern rather than attempting to model action-body
statements. `ExtractStateActionName` only derives a name for the named
`ACTION usageDeclaration?` grammar alternative (e.g. `do action providePower { ... }` →
`"providePower"`); for the unnamed reference-subsetting alternative (e.g.
`entry performSelfTest{...}`, a reference to an inherited/imported behavior rather than a new
declaration) it deliberately returns `null` rather than attempting to derive or evaluate a name —
the resulting feature node still registers as an (unnamed, unregistered) AST child so no
information is lost, but it is not itself a resolvable symbol. This scope boundary is intentional
and matches the ROADMAP's framing of entry/do/exit action support as "minimal, non-behavioral."

**Attached-succession action bodies and control-node features.** The `actionBodyItem` grammar
rule has an analogous combined-shape problem to `stateBodyItem`: two of its alternatives attach
a succession directly onto the immediately preceding node within the same alternative rather than
exposing it as a separate `successionAsUsage` —
`initialNodeMember (actionTargetSuccessionMember)*` (e.g. `first start; then off;`) and
`(sourceSuccessionMember)? actionBehaviorMember (actionTargetSuccessionMember)*` (e.g. the compact
`action a1; then a2;` idiom). Before `VisitActionBodyItem` existed, `AstBuilder` only ever
visited the leading node of each alternative and silently dropped every attached succession, so
the compact idiom resolved both action nodes but produced no succession edge linking them.

`VisitActionBodyItem` dispatches all four alternatives explicitly:

- `nonBehaviorBodyItem`, `guardedSuccessionMember` — passed straight through to `Visit(...)` (no
  attached-succession shape applies).
- `initialNodeMember (actionTargetSuccessionMember)*` — when one or more
  `actionTargetSuccessionMember`s are attached, synthesizes a `SysmlTransitionNode` per entry
  (via `BuildActionTargetSuccession`) sourced from the `qualifiedName` referenced by the
  `initialNodeMember`; the bare `first start;` form (no attached succession) remains a no-op,
  unchanged from today, since `ActionFlowViewLayoutStrategy` infers start/done markers from
  succession topology rather than a declarative initial-marker concept.
- `(sourceSuccessionMember)? actionBehaviorMember (actionTargetSuccessionMember)*` — visits
  `actionBehaviorMember` (which delegates to the existing `actionNodeMember`/`behaviorUsageMember`
  handling) to obtain the main node, then calls `BuildActionTargetSuccession` once per
  `actionTargetSuccessionMember` entry, each producing a `SysmlTransitionNode` whose `Source` is
  the main node's `Name`. When the optional leading `sourceSuccessionMember` is present (a bare
  `then` immediately before the node, e.g. `action a; then fork f; ...` — the dominant real-world
  idiom for wiring a control node into a flow) an additional *incoming* `SysmlTransitionNode` is
  prepended, whose `Source` is `_actionBodyPreviousNodeName` and whose `Target` is the main node's
  `Name`. The grammar's leading marker (`sourceSuccessionMember: THEN sourceSuccession`, where
  `sourceSuccession`/`sourceEndMember`/`sourceEnd` carry no name token at all) is a pure marker —
  its meaning is "this node's incoming edge comes from whatever immediately preceded it in the
  same enclosing action body" — so its `Source` identity cannot be read off the grammar node
  itself. It is instead resolved from `_actionBodyPreviousNodeName`, an `AstBuilder` instance
  field maintained by `CollectActionBodyChildren` (a body-specific counterpart to the generic
  `CollectChildren`, used only for action bodies) as it iterates an action body's
  `actionBodyItem`s in source order, updating the tracked position after each item via
  `DetermineFlowPositionName` (which resolves to the last synthesized transition's `Target` when
  the item produced trailing successions, or the visited node's own `Name` otherwise). When no
  previous position is known (e.g. the item is the first thing in the body), no incoming edge is
  synthesized rather than fabricating a `Source` from nothing — this matches the safe,
  no-op-by-default behavior applied elsewhere in this visitor. `CollectChildren` itself remains
  untouched and continues to serve the state-body call sites, since only action bodies need this
  order-sensitive bookkeeping.

`BuildActionTargetSuccession` handles all three `actionTargetSuccession` grammar forms:
unguarded `targetSuccession` (`sourceEndMember THEN connectorEndMember`), guarded
`guardedTargetSuccession` (`if guardExpressionMember then connectorEndMember`, capturing the
guard's expression text), and `defaultTargetSuccession` (`else then connectorEndMember`, which the
grammar provides no guard expression for).

When one or more attached successions (incoming, trailing, or both) are produced,
`VisitActionBodyItem` returns the same `MultiNodeCapture` sentinel used by
`VisitStateBodyItem`, wrapping the incoming transition (if any), the node, and its trailing
succession(s) in that order; when none are produced, it returns the visited node/pass-through
result directly.

**Known follow-up gaps (out of scope for this fix).** Two structurally analogous
leading-marker cases were discovered while fixing the above but deliberately left unfixed to keep
this change surgical:

- `nonBehaviorBodyItem`'s `(sourceSuccessionMember)? structureUsageMember` shape has the same
  "leading `then` implies an implicit incoming edge" grammar structure, but there is no
  `VisitNonBehaviorBodyItem`/`VisitStructureUsageMember` override at all today — it relies
  entirely on ANTLR's default `VisitChildren` aggregation, which drops every child but the last.
  This is a broader, pre-existing gap (no `MultiNodeCapture` handling exists there yet at all),
  not merely a missing `sourceSuccessionMember` read.
- `VisitStateBodyItem`'s `(sourceSuccessionMember)? behaviorUsageMember
  (targetTransitionUsageMember)*` shape (State Transition View) has the identical unread-marker
  problem this fix addresses for action bodies, and was not established as a working precedent to
  copy — it has the same gap, latent and undetected because no current test exercises a leading
  `then` before a `behaviorUsageMember` inside a state body.

`VisitMergeNode`, `VisitDecisionNode`, `VisitJoinNode`, `VisitForkNode`, `VisitAcceptNode`, and
`VisitSendNode` each delegate to a shared `BuildActionNodeFeature(usage, keyword)` helper that
builds a **minimal** `SysmlFeatureNode` — `FeatureKeyword` set to
`"merge"`/`"decide"`/`"join"`/`"fork"`/`"accept"`/`"send"` respectively, `Children` always empty.
Unlike ordinary anonymous actions (which are left nameless), an anonymous control node is given a
synthesized internal name of the form `$<keyword><n>` (via a monotonically increasing
`_anonymousNodeCounter` field) rather than `null`. This is a deliberate deviation from the
State Transition View precedent: anonymous fork/decide/send is the *dominant* real-world idiom in
the OMG training corpus (e.g. `then fork;` immediately followed by several `then` successions),
so leaving these nodes nameless would make it impossible to wire their successions or render a
distinct badge for them. The synthetic name is never registered in the symbol table
(`QualifiedName` stays `null`, so `$`-prefixed names never resolve and surface only as cosmetic
"unresolved reference" warnings) and is blanked from rendered labels by
`ActionFlowViewLayoutStrategy`; it exists purely as an internal succession-wiring mechanism. When
the control node instead has an *explicitly declared* name (e.g. `fork buildFork;`), it is treated
like any other named feature: `QualifiedName` is populated via the same `QualifyName` helper used
by `BuildStateActionFeatureNode`, so it is registered in the symbol table and correctly subject to
expose-scope filtering (`ExposeScopeResolver.IsInSubjectScope`) in
`ActionFlowViewLayoutStrategy.CollectActions`.
`assignmentNode`, `terminateNode`, `ifNode`, `whileLoopNode`, and `forLoopNode` remain
intentionally unhandled — a pre-existing gap, not introduced by this change.

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
  `SysmlViewpointNode`, `SysmlSatisfyNode`, `SysmlConnectionNode`, `SysmlDependencyNode`) — AST
  node types constructed by the visitor.
- `SysmlAnnotation` / `SysmlAnnotationKind` — captured comment/documentation data attached to
  `SysmlNode.Annotations`.

##### Callers

`WorkspaceLoader.BuildStdlibSemanticAsync` and `WorkspaceLoader.ParseUserFileAsync` each create
a fresh `AstBuilder` instance and call `Build(RootNamespaceContext)` on the CST root produced
by `WorkspaceParser.ParseSourceToCst`.
