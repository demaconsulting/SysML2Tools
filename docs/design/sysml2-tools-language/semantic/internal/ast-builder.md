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
  `SysmlViewpointNode`) — AST node types constructed by the visitor.
- `SysmlAnnotation` / `SysmlAnnotationKind` — captured comment/documentation data attached to
  `SysmlNode.Annotations`.

##### Callers

`WorkspaceLoader.BuildStdlibSemanticAsync` and `WorkspaceLoader.ParseUserFileAsync` each create
a fresh `AstBuilder` instance and call `Build(RootNamespaceContext)` on the CST root produced
by `WorkspaceParser.ParseSourceToCst`.
