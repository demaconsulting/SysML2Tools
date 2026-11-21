#### SysmlNode — AST Node Hierarchy

![Language Semantic Model Structure](LanguageSemanticModelView.svg)

##### Overview

`SysmlNode` is the abstract base class for all SysML/KerML AST nodes. Concrete subtypes represent
packages, definitions, features, imports, applied metadata annotations, views, viewpoints,
connections, transitions, dependency declarations, and requirement-satisfaction usages.

##### Class Hierarchy

| Class | Purpose |
| --- | --- |
| `SysmlNode` (abstract) | Base properties common to all nodes (see Properties below) |
| `SysmlPackageNode` | Package or namespace declaration |
| `SysmlDefinitionNode` | Definition element (part def, etc.); adds DefinitionKeyword and modifier flags |
| `SysmlFeatureNode` | Feature/usage element; adds typing, multiplicity, and modifier/notation data |
| `SysmlImportNode` | Import declaration; adds ImportedNamespace, IsWildcard |
| `SysmlMetadataNode` | Applied metadata annotation; adds TypeReference and Attributes |
| `SysmlViewNode` | View definition; adds RenderTargetName, ExposeMembers, and filter-expression fields |
| `SysmlViewpointNode` | Viewpoint definition |
| `SysmlConnectionNode` | Connector usage; adds ConnectionKeyword, EndpointA/B, end labels, PayloadText |
| `SysmlTransitionNode` | State transition; adds Source, Target, Guard, Trigger, Effect |
| `SysmlSatisfyNode` | `satisfy X by Y;` requirement-satisfaction usage; adds RequirementName, SubjectName |
| `SysmlDependencyNode` | Standalone `dependency A, B to C, D;`; adds FromNames, ToNames, and a declared name |

##### Properties

All nodes carry:

- `Name` — simple (unqualified) name, or null if anonymous.
- `QualifiedName` — fully-qualified name in containing namespace.
- `Children` — nested AST nodes.
- `SupertypeNames` — qualified names of supertypes referenced via `specializes` / `:>`. For a
  `SysmlFeatureNode`, this list is also reused, unresolved, by `GeneralViewLayoutStrategy` as the
  raw source of its subsetting edges (`subsets <target>;` / `:> <target>` on a usage) — there is
  no separate `SysmlEdgeKind.Subsetting` or dedicated AST field; the layout strategy matches each
  entry to its own resolved `Supertype` edge by the entry's exact text, else resolves it by name.
  See `general-view-layout-strategy.md` for the exact algorithm.
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
- `IsExcludedFromDiagrams` — the member is modeled for name resolution, `query` and `export` only;
  every diagram strategy skips it and everything nested in it (`DiagramChildren` is `Children`
  without the flagged ones). Built by `AstBuilder` for bare parameters, portion usages,
  `perform`/`exhibit`/`include`/`assert` usages, a transition's `effect`, and the bodies of action,
  calculation, constraint and case usages and definitions; never set on a member built before issue
  #137. `[JsonIgnore(WhenWritingDefault)]`, so it is written only when `true`.
- `Annotations` — captured `comment`/`doc` annotating-element text, in source order; populated
  by `AstBuilder` from annotations lexically nested directly in this element's body. An
  explicit `about X` target is not resolved — the annotation is attached to the lexically
  enclosing node, not to `X` (see `SysmlAnnotation` design doc for known limitations).
- `SourceFilePath`, `SourceLine`, `SourceColumn` — the file that declares the element (null when
  the element was not built from a source file, such as a synthesized `--auto` or dynamic view, or
  one loaded from the pre-compiled stdlib) and the one-based line and one-based column (matching
  the `SysmlDiagnostic.Column` convention) at which its declaration starts, 0 when unknown.
- `NamePosition` — the `SourcePosition` (line, column) of the token that spells the declared name
  (quotes included for a quoted name), the default `(0, 0)` when unknown. For a `package` the
  declaration starts at the keyword, so a diagnostic about the name itself — the duplicate
  declaration warning — uses this position instead.
- `ReferencePositions` — the `SourcePosition` (line, column) of the first token of each reference
  text the node declares (supertypes, subsetting, typing, redefinition, imports, `expose`
  members, view definition, connector endpoints, transition source/target, `satisfy`, `verify`,
  `dependency`, and metadata-type references), keyed by the text exactly as `ReferenceResolver`
  resolves it. A reference whose text matches no token (a synthesized name such as an implicit
  transition source) has no entry.
- `ReferenceOccurrences` — each reference the node writes inside text the AST keeps only as raw
  text (a view's `render` target, a transition's trigger/guard/effect, an `accept`/`send` node's
  payload, a state action's declaration, a constraint expression), as a `ReferenceOccurrence`
  (`Name`, `Position`), in source order. Unlike `ReferencePositions` it is a list of occurrences,
  so the same name written twice is two entries. `ReferenceResolver` resolves each and reports
  every unresolved one at its position, recording no edge (see the `AstBuilder` and
  `ReferenceResolver` designs).

  These six are populated by `AstBuilder` and consumed by `ReferenceResolver`, `SupertypeWalker`,
  and `WorkspaceLoader` to locate diagnostics. They are marked `[JsonIgnore]` on `SysmlNode`
  (`SysmlViewNode`'s forwarding location properties excepted — see the view section): positions are only
  needed while a workspace's own source files are being resolved (the stdlib is resolved when it
  is pre-compiled, and its diagnostics, with positions, are what is serialized), so they are
  deliberately not part of the serialized stdlib format, which keeps `stdlib.json.gz` small and
  means a deserialized node has no position.

##### Key Methods

All node types use C# `init`-only properties and are constructed via object initializers.
There are no behavioral methods beyond the inherited `object` members. `SysmlImportNode` adds:

- `ImportedNamespace` — the target namespace string extracted by `ReferenceResolver`.
- `IsWildcard` — `true` if the import ends with `::*`.
- `BracketFilterExpressionText` — the raw source text of an `import`/`expose` bracket filter
  (`::**[<expr>]`), or null when absent. Preserved as capture-only Phase 1 data.
- `RawReferenceText` — the import's reference as written, wildcard/recursive markers included
  (`P`, `P::*`, `P::**`, `P::*::**`) but excluding the bracket filter and inter-token whitespace,
  or null for hand-built nodes and data serialized before the property existed. Captured by
  `AstBuilder.ExtractImportTarget` from the parse tree and used only as the `RawReference` of the
  resolved `Import` edge (falling back to the stripped `ImportedNames` entry when null); it never
  affects resolution. Omitted from JSON when null.

`SysmlDefinitionNode` adds:

- `DefinitionKeyword` — the grammar keyword string (e.g., `"part def"`, `"attribute def"`).
- `IsAbstract`, `IsVariation`, `IsIndividual` — `true` when the definition declares the leading
  `abstract`, `variation`, or `individual` modifier; `IsParallel` — `true` for a `state def`
  declared `parallel`. See "Modifier and notation data" below.

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
  `GeneralViewLayoutStrategy` as a hollow-triangle-crossbar edge between the usage boxes.
- `Multiplicity` — the multiplicity text (e.g., `"[4]"`, `"[0..*]"`), or null when unspecified.
- `ExpressionText` — the raw expression text of a constraint-kind feature's calculation body or
  referenced constraint name (e.g. `"require constraint"`, `"assume constraint"`, `"constraint"`
  keywords), or null for every other feature kind. Mirrors `SysmlTransitionNode.Guard`'s
  raw-text-only capture; no expression-tree modeling is attempted. Rendered by
  `GeneralViewLayoutStrategy.FormatFeatureRow` in place of the generic `name : Type
  [multiplicity]` row shape when non-null.
- Modifier and notation members (`IsAbstract`, `IsVariation`, `IsIndividual`, `IsVariant`,
  `IsReference`, `Direction`, `IsOrdered`, `IsNonunique`, `ValueText`, `ReferenceSubsettingName`,
  `ConjugatedFeatureTyping`, computed `IsConjugated`, `ActionText`, `IsParallel`) — see
  "Modifier and notation data" below.

`SysmlConnectionNode` adds:

- `ConnectionKeyword` — the connection keyword (e.g., `"connection"`, `"binding"`, `"message"`, the
  `"allocation"` variant produced by `AstBuilder.VisitAllocationUsage` for `allocate A to B;`,
  reusing this node's endpoint shape since `allocationUsageDeclaration`'s `connectorPart` is the
  exact same grammar rule used by `connectionUsage`, and the `"interface"` and `"flow"` variants
  produced by `VisitInterfaceUsage` and `VisitFlowUsage`). `ReferenceResolver` resolves only the
  `"connection"`, `"message"`, `"binding"`, and `"allocation"` variants; `"interface"` and `"flow"`
  nodes add no edges and no diagnostics.
- `EndpointA` — the first endpoint reference (e.g., `"engine.fuelPort"`), or null when unresolved.
  For `"connection"`/`"message"` keyword variants, resolved by `ReferenceResolver`'s feature-chain
  walk into a `SysmlEdgeKind.Connect` edge (see `SysmlEdgeKind.Connect`); dotted chains on
  `"allocation"` endpoints remain unresolved.
- `EndpointB` — the second endpoint reference (e.g., `"transmission.input"`), or null when
  unresolved. Resolved the same way as `EndpointA`.
- `EndpointAName`, `EndpointBName` — the role name of an end declared as `name ::> reference`
  (connection, binding, and interface ends), or null. Not resolved; omitted from serialized output
  when null.
- `EndpointAMultiplicity`, `EndpointBMultiplicity` — the cross multiplicity text written before an
  end (`[2] reference`), or null. Omitted from serialized output when null.
- `PayloadText` — the verbatim payload clause after `of` of a `"flow"` or `"message"` (`Torque` in
  `flow of Torque from a.out to b.in`), or null. Omitted from serialized output when null.

`SysmlTransitionNode` adds:

- `Source` — the source state reference, or null when implied by the containing state. May be a
  dotted feature chain, resolved by `ReferenceResolver`'s feature-chain walk into a
  `SysmlEdgeKind.Transition` edge together with `Target` — emitted only when both resolve; an
  implied/omitted `Source` produces no edge.
- `Target` — the target state reference. Resolved the same way as `Source`.
- `Guard` — the guard expression text (the condition after `if`), or null when unguarded. Captured
  from `GetText()`, so inter-token whitespace is dropped (`x > 5` is recorded as `x>5`); unchanged.
- `Trigger` — the verbatim text of the trigger after `accept` (e.g. `callReceived`, `sig via pp`,
  `at 5`), or null. `Effect` — the verbatim text of the effect after `do`, or null. Both keep
  original whitespace and are captured only for the full `transition ...` form and attached
  transitions; transitions synthesized from an entry action's attached successions or an
  action-flow target succession leave them null.
- `IsElse` — `true` only for an action-flow succession declared with `else` (the default branch of
  a `decide` node, which has no guard expression); `false` otherwise. Omitted from serialized output
  when `false`.

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
  Captured verbatim: `ReferenceResolver` produces no edge for this value, and it has no effect on
  `GeneralViewLayoutStrategy`'s rendered scope; the reference it writes is resolved separately
  as one of the view's `ReferenceOccurrences` (an unqualified target without `import Views::*;`
  is reported as an unresolved reference). Reserved for a possible future capability that selects
  among rendering-style strategies — see the project ROADMAP.
- `ExposeMembers` — each `expose <name>[::**[<expr>]];` member in a `view` usage's body, in
  source order, paired as an
  `ExposeMember(string QualifiedName, string? BracketFilterExpressionText, ExposeRecursionKind RecursionKind, string? RawReferenceText)`
  record — the qualified-name reference text, that specific entry's own bracket-filter expression
  text (or null when the entry carries none), and an `ExposeRecursionKind` classifying which SysML
  v2 `expose` grammar form (MembershipExpose/NamespaceExpose) and recursion setting (a trailing
  `::**`) produced the entry: `MembershipExact` (`expose X;`), `MembershipRecursive`
  (`expose X::**;`), `NamespaceDirectChildren` (`expose X::*;`), or `NamespaceRecursive`
  (`expose X::*::**;`) — a bracket-filtered entry is always classified as one of the two
  *Recursive variants (matching its form), since `ExposeScopeResolver` only consults this
  classification for its unfiltered/fallback whole-subtree-vs-narrow behavior, never for a
  successfully-evaluated bracket filter's own already-exact `ExplicitMembers`. The optional fourth
  component, `RawReferenceText` (default null), holds the entry's reference as written, wildcard and
  recursive markers included but bracket filter excluded (`P`, `P::*`, `P::**`, `P::*::**`); it is
  used only as the `RawReference` of the resolved `Expose` edge (falling back to `QualifiedName`
  when null) and is omitted from JSON when null. Empty when no
  `expose` members are present (and always empty for a `view def` definition — `expose` is only
  valid grammar inside a `view` usage's body). Extracted by `AstBuilder.ExtractExposedNames`,
  sharing the same `ExtractImportTarget` helper `VisitImportRule` uses for plain `import`.
  `GetExposedNames()` projects each entry's `QualifiedName` as a computed convenience list (a
  method rather than a property, since a property returning a freshly-projected collection trips
  SonarAnalyzer S2365). Each entry's `QualifiedName` is independently resolved by
  `ReferenceResolver` into a `SysmlEdgeKind.Expose` edge, or an unresolved-reference diagnostic
  (and no edge) for that entry. `GeneralViewLayoutStrategy` (via the shared `ExposeScopeResolver`)
  uses this to scope a rendered diagram, and (Phase 2a) re-pairs each resolved edge back to its
  originating `ExposeMember` to evaluate that entry's own `BracketFilterExpressionText`, if any,
  against that entry's own containment subtree, and to read its `RecursionKind` to decide between
  exact/direct-children/whole-subtree matching. Phase 1 originally captured each entry's
  bracket-filter text on a separate, unpaired, flattened `ExposeBracketFilterTexts` list alongside
  an equally flattened `ExposedNames` list, making it impossible to tell which exposed path a given
  bracket filter belonged to when a view declared more than one `expose` member; `ExposeMembers`
  fixes this defect by pairing the two (and now, the recursion kind) together at capture time.
- `ShortName`, `Visibility` (on every node) — the element's declared short name
  (`<shortName>`) and its membership's visibility (`SysmlVisibility.Public` by default, per KerML
  7.2.5.3; for a `SysmlImportNode`, the import's own visibility, `Private` by default per SysML
  7.5.3). `SysmlImportNode.IsImportAll` records `import all`. `SysmlAliasNode` represents an
  `alias <s> N for Q;` membership (name, short name, raw `TargetName`); it has no qualified name
  and is not registered in the symbol table. These feed `NameResolver` (KerML 8.2.3.5.3).
- `FilterExpressionText` — the raw source text of the view's `filter [<expr>];` member's
  bracketed expression, or null when absent. Captured verbatim by `AstBuilder` (using the
  original token spacing, not `RuleContext.GetText()`'s whitespace-stripped form) and never
  inspected by `ReferenceResolver`. The Language Filtering subsystem parses and evaluates this raw
  text later when `GeneralViewLayoutStrategy` renders the view.
- `SourceFilePath`, `SourceLine`, `SourceColumn` — forward to the `SysmlNode` properties (see above)
  but are redeclared (`new`, with their original `init` signatures) on `SysmlViewNode` so binaries
  compiled when they were declared here keep working, and — unlike other nodes — remain serialized
  for views: the
  path of the file declaring the view (null
  for a view not built from a source file, such as a synthesized `--auto` or dynamic view) and
  the one-based line and one-based column (matching the `SysmlDiagnostic.Column` convention) of
  the view's declaration, 0 when unknown. Recorded by `AstBuilder` so view-level diagnostics
  produced after loading — such as `DiagramRenderer`'s empty-view warning — can be located.

- `FilterExpressionTexts` — the raw text of every `filter` member (the view's owned view
  conditions, SysML 8.3.26.11), in source order.
- `ViewDefinitionName` — a view usage's raw view definition reference (`view V : Def`), resolved by
  `ReferenceResolver` into a `Typing` edge; the usage inherits that definition's view conditions
  and rendering (SysML 7.26.2).

##### Modifier and notation data

The members below capture data the SysML v2 notation displays. They are purely additive: every
member is optional, defaults to `false`/null, is never resolved, and never changes any edge,
diagnostic, or layout. Each carries `JsonIgnore(WhenWritingDefault)` (flags) or
`JsonIgnore(WhenWritingNull)` (text, direction), so a node that does not use a member serializes
exactly as before — including in the pre-compiled stdlib and in the `export` JSON/JSONL output —
and data written before the members existed deserializes with all of them at their defaults.

| Member | Node | Meaning |
| --- | --- | --- |
| `IsAbstract`, `IsVariation`, `IsIndividual` | definition, feature | leading modifiers; set by `AstBuilder` |
| `IsVariant` | feature | `variant part x;` usage |
| `IsReference` | feature | declared with `ref` (`ref part x` or `ref x : T`); no composite flag is captured |
| `Direction` | feature | `SysmlFeatureDirection` `In`/`Out`/`InOut`, serialized as a string; null when none |
| `IsOrdered`, `IsNonunique` | feature | multiplicity modifiers, with or without a `[..]` range |
| `ValueText` | feature | whole value clause verbatim, operator included (`= 5 [kg]`); never parsed |
| `ReferenceSubsettingName` | feature | raw target of `::>`/`references`; unresolved, produces no edge |
| `ConjugatedFeatureTyping` | feature | raw name after `~` in a conjugated port typing; unresolved |
| `IsConjugated` | feature | computed (`ConjugatedFeatureTyping is not null`); not serialized |
| `ActionText` | feature | `entry`/`do`/`exit` declaration text or an `accept`/`send` payload; body excluded |
| `IsParallel` | definition, feature | `state def`/`state` declared `parallel` |
| `Trigger`, `Effect` | transition | verbatim trigger and effect text |
| `IsElse` | transition | succession declared with `else` |
| `AdditionalRedefinedFeatureNames` | feature | raw targets of a multiple redefinition after the first; null if none |
| `IsExcludedFromDiagrams` | any node | modeled for name resolution only; diagrams skip it (see Properties) |
| `EndpointAName`, `EndpointBName` | connection | role name of a connector end (`name ::> reference`) |
| `EndpointAMultiplicity`, `EndpointBMultiplicity` | connection | cross multiplicity text of a connector end |
| `PayloadText` | connection | payload clause of a flow or message |

`ReferenceSubsettingName` and `ConjugatedFeatureTyping` are deliberately **not** merged into
`SupertypeNames` or `FeatureTyping`: doing so would add `Supertype`/`Typing` edges and change
diagnostics and layout (`NameResolver` does read `ReferenceSubsettingName` as a general type for
member lookup, which adds no edge). A bare direction-only parameter (`in x : T;`) is a feature with
the keyword `ref` and carries its direction; the other feature keywords beyond the grammar's usage
keywords are `timeslice`, `snapshot`, `perform`, `exhibit`, `include` and `assert constraint`. A
`perform`, `include` or `assert` usage written only as a reference is named by the referenced
feature's simple name and has a synthetic qualified-name segment (`$perform<n>`); an anonymous usage
with a body has `Name` null and a `$anon<n>` qualified-name segment. `SupertypeNames` and
`RedefinedFeatureName` are settable so that `AstBuilder` can record them centrally for every usage kind.
`derived`/`constant`/`end`/`readonly` and modifiers on view, viewpoint, and
connection nodes are not captured.

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
  `"allocation"`, `"interface"`, and `"flow"` `SysmlConnectionNode` variants via `VisitAllocationUsage`,
  `VisitInterfaceUsage`, and `VisitFlowUsage`; builds `SysmlViewNode`
  instances (setting `RenderTargetName`/`FilterExpressionText`/`SourceFilePath`/`SourceLine`/
  `SourceColumn`, and additionally `ExposeMembers`
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
