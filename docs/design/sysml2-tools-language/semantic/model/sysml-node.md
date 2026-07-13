#### SysmlNode — AST Node Hierarchy

##### Overview

`SysmlNode` is the abstract base class for all SysML/KerML AST nodes. Concrete subtypes represent
packages, definitions, features, imports, applied metadata annotations, views, viewpoints,
connections, transitions, dependency declarations, and requirement-satisfaction usages.

##### Class Hierarchy

| Class | Purpose |
| --- | --- |
| `SysmlNode` (abstract) | Base properties common to all nodes (see Properties below) |
| `SysmlPackageNode` | Package or namespace declaration |
| `SysmlDefinitionNode` | Definition element (part def, attribute def, etc.); adds DefinitionKeyword |
| `SysmlFeatureNode` | Feature/usage element |
| `SysmlImportNode` | Import declaration; adds ImportedNamespace, IsWildcard |
| `SysmlMetadataNode` | Applied metadata annotation; adds TypeReference and Attributes |
| `SysmlViewNode` | View definition; adds RenderTargetName, ExposeMembers, and filter-expression fields |
| `SysmlViewpointNode` | Viewpoint definition |
| `SysmlConnectionNode` | Connection/binding/allocation usage; adds ConnectionKeyword, EndpointA, EndpointB |
| `SysmlTransitionNode` | State transition; adds Source, Target, Guard |
| `SysmlSatisfyNode` | `satisfy X by Y;` requirement-satisfaction usage; adds RequirementName, SubjectName |
| `SysmlDependencyNode` | Standalone `dependency A, B to C, D;` declaration; adds FromNames, ToNames |

##### Properties

All nodes carry:

- `Name` — simple (unqualified) name, or null if anonymous.
- `QualifiedName` — fully-qualified name in containing namespace.
- `Children` — nested AST nodes.
- `SupertypeNames` — qualified names of supertypes referenced via `specializes` / `:>`. For a
  `SysmlFeatureNode`, this list is also reused, unresolved, by `GeneralViewLayoutStrategy` as the
  raw source of its private view-layer `Subsetting` classification (`subsets <target>;` /
  `:> <target>` on a feature) — there is no separate `SysmlEdgeKind.Subsetting` or dedicated AST
  field; the layout strategy re-derives which entries are subsetting targets versus ordinary
  specialization targets by walking the same feature-typing/redefinition-owner resolution it
  already performs for `Redefinition` edges. See `general-view-layout-strategy.md` for the exact
  algorithm.
- `ImportedNames` — qualified/dotted-name text of imported namespaces or members; populated by
  `AstBuilder.VisitImportRule` for `SysmlImportNode` (mirroring `ImportedNamespace`), and empty
  for all other node types today.
- `VerifiedRequirementNames` — raw requirement reference names verified by this node's nested
  `verify` members (from `requirementVerificationMember`), one entry per `verify` found directly
  or transitively nested in this node's specialized body (e.g. a `requirement`/`case`/
  `verification`/`analysis` body, or an `objective` nested within one). Populated by
  `AstBuilder`'s `FindVerificationMembers`/`CollectVerificationMembers`/
  `ExtractVerifiedRequirementName` helper trio and resolved uniformly by `ReferenceResolver`
  (mirroring the `SupertypeNames`/`ImportedNames` loops) into `SysmlEdgeKind.Verify` edges
  sourced from this node — there is no standalone verify-usage node type; this list is the sole
  producer of `Verify` edges.
- `ResolvedEdges` — resolved outgoing `SysmlEdge` entries (supertype, typing, redefinition,
  import, satisfy, verify, allocate, connect, transition, expose), populated post-construction by
  `ReferenceResolver`; a settable (not `init`)
  property since resolution runs after the AST is built and the symbol table is fully populated.
  Empty for stdlib-only nodes, which are registered but never passed through
  `ReferenceResolver.ResolveAll`.
- `Annotations` — captured `comment`/`doc` annotating-element text, in source order; populated
  by `AstBuilder` from annotations lexically nested directly in this element's body. An
  explicit `about X` target is not resolved — the annotation is attached to the lexically
  enclosing node, not to `X` (see `SysmlAnnotation` design doc for known limitations).

##### Key Methods

All node types use C# `init`-only properties and are constructed via object initializers.
There are no behavioral methods beyond the inherited `object` members. `SysmlImportNode` adds:

- `ImportedNamespace` — the target namespace string extracted by `ReferenceResolver`.
- `IsWildcard` — `true` if the import ends with `::*`.
- `BracketFilterExpressionText` — the raw source text of an `import`/`expose` bracket filter
  (`::**[<expr>]`), or null when absent. Preserved as capture-only Phase 1 data.

`SysmlDefinitionNode` adds:

- `DefinitionKeyword` — the grammar keyword string (e.g., `"part def"`, `"attribute def"`).

`SysmlFeatureNode` adds:

- `FeatureKeyword` — the usage keyword string (e.g., `"part"`, `"port"`, `"attribute"`, `"ref"`).
- `FeatureTyping` — the raw reference text of the type after `:` (or `typed by`), or null when
  untyped. Resolved by `ReferenceResolver` into a `SysmlEdgeKind.Typing` edge.
- `RedefinedFeatureName` — the raw reference text of the feature's `redefines <target>;`/
  `:>> <target>` clause, or null when the feature declares no redefinition. Extracted by
  `AstBuilder.ExtractRedefinedFeature`, mirroring `ExtractFeatureTyping`'s structure exactly
  (walking `redefinitions()` instead of `typings()`). Captured verbatim only — including
  qualified `Owner::feature` forms — with no resolution attempted at this stage. Resolved by
  `ReferenceResolver` into a `SysmlEdgeKind.Redefinition` edge, and rendered by
  `GeneralViewLayoutStrategy` as a hollow-triangle-crossbar marker.
- `Multiplicity` — the multiplicity text (e.g., `"[4]"`, `"[0..*]"`), or null when unspecified.
- `ExpressionText` — the raw expression text of a constraint-kind feature's calculation body or
  referenced constraint name (e.g. `"require constraint"`, `"assume constraint"`, `"constraint"`
  keywords), or null for every other feature kind. Mirrors `SysmlTransitionNode.Guard`'s
  raw-text-only capture; no expression-tree modeling is attempted. Rendered by
  `GeneralViewLayoutStrategy.FormatFeatureRow` in place of the generic `name : Type
  [multiplicity]` row shape when non-null.

`SysmlConnectionNode` adds:

- `ConnectionKeyword` — the connection keyword (e.g., `"connection"`, `"binding"`, or the
  `"allocation"` variant produced by `AstBuilder.VisitAllocationUsage` for `allocate A to B;`,
  reusing this node's endpoint shape since `allocationUsageDeclaration`'s `connectorPart` is the
  exact same grammar rule used by `connectionUsage`).
- `EndpointA` — the first endpoint reference (e.g., `"engine.fuelPort"`), or null when unresolved.
  For `"connection"`/`"message"` keyword variants, resolved by `ReferenceResolver`'s feature-chain
  walk into a `SysmlEdgeKind.Connect` edge (see `SysmlEdgeKind.Connect`); dotted chains on
  `"allocation"` endpoints remain unresolved.
- `EndpointB` — the second endpoint reference (e.g., `"transmission.input"`), or null when
  unresolved. Resolved the same way as `EndpointA`.

`SysmlTransitionNode` adds:

- `Source` — the source state reference, or null when implied by the containing state. May be a
  dotted feature chain, resolved by `ReferenceResolver`'s feature-chain walk into a
  `SysmlEdgeKind.Transition` edge together with `Target` — emitted only when both resolve; an
  implied/omitted `Source` produces no edge.
- `Target` — the target state reference. Resolved the same way as `Source`.
- `Guard` — the guard expression text (the condition after `if`), or null when unguarded.

`SysmlSatisfyNode` adds:

- `RequirementName` — the raw reference text of the requirement being satisfied (from
  `ownedReferenceSubsetting` when the `satisfy <ref>` form is used, or from the declared/typed
  name of the `satisfy requirement <usageDeclaration>` form), or null if it could not be
  determined.
- `SubjectName` — the raw reference text of the satisfying subject (from the `by <subject>`
  clause), or null when no `by` clause is present.

`SysmlMetadataNode` adds:

- `TypeReference` — the raw reference text of the annotating metadata type. Resolved by
  `ReferenceResolver` into a `SysmlEdgeKind.MetadataType` edge.
- `Attributes` — the ordered list of captured `MetadataAttributeValue` entries assigned within the
  annotation body. Supported Phase 1 scalar literals are preserved as typed values; unsupported
  value-expression shapes remain raw text only.

`SysmlViewNode` adds:

- `RenderTargetName` — the raw reference text of the view's `render <target>;` member (the
  first one found if more than one appears), or null when the view has no `render` member. Per
  the SysML v2 grammar this names a rendering style/format usage (e.g. `asTreeDiagram`,
  `asElementTable`) — never a content-scoping subject. Extracted by the shared
  `AstBuilder.ExtractRenderTargetName` helper, which follows the same two-form fallback pattern
  (direct reference, then typed placeholder) `VisitSatisfyRequirementUsage` already uses.
  Captured verbatim only: `ReferenceResolver` never inspects or resolves this value (no edge is
  produced, no diagnostic is emitted), and it has no effect on `GeneralViewLayoutStrategy`'s
  rendered scope. Reserved for a possible future capability that selects among rendering-style
  strategies — see the project ROADMAP.
- `ExposeMembers` — each `expose <name>[::**[<expr>]];` member in a `view` usage's body, in
  source order, paired as an `ExposeMember(string QualifiedName, string? BracketFilterExpressionText)`
  record — the qualified-name reference text plus that specific entry's own bracket-filter
  expression text, or null when the entry carries none. Empty when no `expose` members are
  present (and always empty for a `view def` definition — `expose` is only valid grammar inside a
  `view` usage's body). Extracted by `AstBuilder.ExtractExposedNames`, sharing the same
  `ExtractImportTarget` helper `VisitImportRule` uses for plain `import`. `GetExposedNames()`
  projects each entry's `QualifiedName` as a computed convenience list (a method rather than a
  property, since a property returning a freshly-projected collection trips SonarAnalyzer S2365).
  Each entry's `QualifiedName` is independently resolved by `ReferenceResolver` into a
  `SysmlEdgeKind.Expose` edge, or an unresolved-reference diagnostic (and no edge) for that
  entry. `GeneralViewLayoutStrategy` (via the shared `ExposeScopeResolver`) uses this to scope a
  rendered diagram, and (Phase 2a) re-pairs each resolved edge back to its originating
  `ExposeMember` to evaluate that entry's own `BracketFilterExpressionText`, if any, against that
  entry's own containment subtree. Phase 1 originally captured each entry's bracket-filter text on
  a separate, unpaired, flattened `ExposeBracketFilterTexts` list alongside an equally flattened
  `ExposedNames` list, making it impossible to tell which exposed path a given bracket filter
  belonged to when a view declared more than one `expose` member; `ExposeMembers` fixes this
  defect by pairing the two together at capture time.
- `FilterExpressionText` — the raw source text of the view's `filter [<expr>];` member's
  bracketed expression, or null when absent. Captured verbatim by `AstBuilder` (using the
  original token spacing, not `RuleContext.GetText()`'s whitespace-stripped form) and never
  inspected by `ReferenceResolver`. The Core Filtering subsystem parses and evaluates this raw
  text later when `GeneralViewLayoutStrategy` renders the view.

##### Error Handling

N/A — node types are pure data containers with no logic or validation. Invalid or anonymous
elements are filtered out by `AstBuilder` before a node is constructed.

##### Dependencies

- No external dependencies. All node types are internal sealed classes or the abstract base class
  within the `Semantic.Model` namespace.

##### Callers

- `AstBuilder` — constructs all concrete node instances during CST visitor traversal; sets
  `ImportedNames` alongside `ImportedNamespace` for `SysmlImportNode`; sets `Annotations` from
  captured `comment`/`doc` annotating elements nested in the node's body; sets
  `VerifiedRequirementNames` via the recursive verification-member finder; builds `SysmlSatisfyNode`
  instances from `satisfyRequirementUsage` via `VisitSatisfyRequirementUsage`, and the
  `"allocation"` `SysmlConnectionNode` variant via `VisitAllocationUsage`; builds `SysmlViewNode`
  instances (setting `RenderTargetName`/`FilterExpressionText`, and additionally `ExposeMembers`
  for usages) from both `VisitViewDefinition` (`view def`) and
  `VisitViewUsage` (`view`, the only form that can carry `expose`); builds `SysmlMetadataNode`
  children from `metadataFeature` annotations.
- `SymbolTable` — traverses the node hierarchy via `Children`; reads `QualifiedName`.
- `ReferenceResolver` — reads `SupertypeNames`, `FeatureTyping`, `RedefinedFeatureName`,
  `ImportedNames`,
  `VerifiedRequirementNames`, `Children`; checks for `SysmlImportNode`, `SysmlSatisfyNode`, the
  `"allocation"` `SysmlConnectionNode` variant, the `"connection"`/`"message"`
  `SysmlConnectionNode` variants, `SysmlTransitionNode`, and `SysmlViewNode` (reading
  `GetExposedNames()`; `RenderTargetName`/`FilterExpressionText`/`ExposeMembers`'
  `BracketFilterExpressionText` are never read here — bracket-filter evaluation is
  `ExposeScopeResolver`'s responsibility), and `SysmlMetadataNode` (reading `TypeReference`);
  writes `ResolvedEdges` after resolving references (in two passes — supertype/typing/
  redefinition/metadata-type/import/satisfy/verify/allocate/expose, then feature-chain
  connect/transition).
- `SupertypeWalker` — reads `SupertypeNames` on each node retrieved from `SymbolTable`.
