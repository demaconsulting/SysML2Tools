#### SysmlNode — AST Node Hierarchy

##### Overview

`SysmlNode` is the abstract base class for all SysML/KerML AST nodes. Concrete subtypes represent
packages, definitions, features, imports, views, viewpoints, connections, transitions, and
requirement-satisfaction usages.

##### Class Hierarchy

| Class | Purpose |
| --- | --- |
| `SysmlNode` (abstract) | Base properties common to all nodes (see Properties below) |
| `SysmlPackageNode` | Package or namespace declaration |
| `SysmlDefinitionNode` | Definition element (part def, attribute def, etc.); adds DefinitionKeyword |
| `SysmlFeatureNode` | Feature/usage element |
| `SysmlImportNode` | Import declaration; adds ImportedNamespace, IsWildcard |
| `SysmlViewNode` | View definition; adds RenderTargetName, ExposedNames, FilterExpressionText |
| `SysmlViewpointNode` | Viewpoint definition |
| `SysmlConnectionNode` | Connection/binding/allocation usage; adds ConnectionKeyword, EndpointA, EndpointB |
| `SysmlTransitionNode` | State transition; adds Source, Target, Guard |
| `SysmlSatisfyNode` | `satisfy X by Y;` requirement-satisfaction usage; adds RequirementName, SubjectName |

##### Properties

All nodes carry:

- `Name` — simple (unqualified) name, or null if anonymous.
- `QualifiedName` — fully-qualified name in containing namespace.
- `Children` — nested AST nodes.
- `SupertypeNames` — qualified names of supertypes referenced via `specializes` / `:>`.
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
- `ResolvedEdges` — resolved outgoing `SysmlEdge` entries (supertype, typing, import, satisfy,
  verify, allocate, connect, transition, expose), populated post-construction by
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

`SysmlDefinitionNode` adds:

- `DefinitionKeyword` — the grammar keyword string (e.g., `"part def"`, `"attribute def"`).

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
- `ExposedNames` — the raw reference text of each `expose <name>;` member in a `view` usage's
  body, in source order, or empty when none are present (and always empty for a `view def`
  definition — `expose` is only valid grammar inside a `view` usage's body). Extracted by
  `AstBuilder.ExtractExposedNames`, sharing the same `ExtractImportTarget` helper `VisitImportRule`
  uses for plain `import`. Each entry is independently resolved by `ReferenceResolver` into a
  `SysmlEdgeKind.Expose` edge, or an unresolved-reference diagnostic (and no edge) for that
  entry. This is the sole field `GeneralViewLayoutStrategy` uses to scope a rendered diagram.
- `FilterExpressionText` — the raw source text of the view's `filter [<expr>];` member's
  bracketed expression, or null when absent. Captured verbatim by `AstBuilder`
  (`elementFilterMember().ownedExpression().GetText()`) and never evaluated or inspected by
  `ReferenceResolver` — full filter expression evaluation is deferred future work (see
  ROADMAP.md). `GeneralViewLayoutStrategy` emits a "parsed but not yet evaluated" warning
  whenever this is non-null.

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
  instances (setting `RenderTargetName`/`FilterExpressionText`, and additionally `ExposedNames`
  for usages) from both `VisitViewDefinition` (`view def`) and `VisitViewUsage` (`view`, the
  only form that can carry `expose`).
- `SymbolTable` — traverses the node hierarchy via `Children`; reads `QualifiedName`.
- `ReferenceResolver` — reads `SupertypeNames`, `FeatureTyping`, `ImportedNames`,
  `VerifiedRequirementNames`, `Children`; checks for `SysmlImportNode`, `SysmlSatisfyNode`, the
  `"allocation"` `SysmlConnectionNode` variant, the `"connection"`/`"message"`
  `SysmlConnectionNode` variants, `SysmlTransitionNode`, and `SysmlViewNode` (reading
  `ExposedNames`; `RenderTargetName`/`FilterExpressionText` are never read); writes
  `ResolvedEdges` after resolving references (in two passes —
  supertype/typing/import/satisfy/verify/allocate/expose, then feature-chain
  connect/transition).
- `SupertypeWalker` — reads `SupertypeNames` on each node retrieved from `SymbolTable`.
