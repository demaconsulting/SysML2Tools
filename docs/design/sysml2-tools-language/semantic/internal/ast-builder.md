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
