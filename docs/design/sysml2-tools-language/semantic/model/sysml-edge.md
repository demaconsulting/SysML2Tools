#### SysmlEdge

##### Overview

`SysmlEdge` and `SysmlEdgeKind` model a single resolved directed reference between two
qualified names in the semantic model. Edges are produced by `ReferenceResolver` while
walking supertype, feature-typing, redefinition, import, satisfy, verify, allocate, connect,
transition, and expose references, and are the raw material indexed by `SemanticIndex`.

##### Types

`SysmlEdgeKind` is an enum with ten members:

- `Supertype` — a specialization reference (`SupertypeNames` / `specializes` / `:>`).
- `Typing` — a feature typing reference (`SysmlFeatureNode.FeatureTyping`, the type after `:`).
- `Import` — an import reference (`ImportedNames` / `import X::Y` or `import X::*`).
- `Satisfy` — a requirement-satisfaction reference (`satisfy X by Y;`), sourced from the
  satisfying subject (`SysmlSatisfyNode.SubjectName`) and targeting the satisfied requirement
  (`SysmlSatisfyNode.RequirementName`).
- `Verify` — a requirement-verification reference (`verify`, in either the direct-reference or
  typed-placeholder grammar form), sourced from the enclosing node's qualified name and
  targeting each resolvable entry in `SysmlNode.VerifiedRequirementNames`.
- `Allocate` — an allocation reference (`allocate A to B;`), sourced from the first connector
  end and targeting the second (`SysmlConnectionNode` with `ConnectionKeyword == "allocation"`,
  reusing the `EndpointA`/`EndpointB` shape); this ordering is a textual convention only and
  carries no semantic directionality beyond "first end to second end" in the source text.
- `Connect` — a resolved connector/message reference (`connect A to B;` or a `message`'s
  from/to events), sourced from the first endpoint and targeting the second
  (`SysmlConnectionNode` with `ConnectionKeyword == "connection"` or `"message"`). Either
  endpoint may be a dotted feature chain (e.g. `engine.fuelPort`), resolved by
  `ReferenceResolver`'s feature-chain walk; recorded only when both endpoints resolve.
- `Transition` — a resolved state-transition reference (`then` / `first ... then ...`), sourced
  from the source state and targeting the target state (`SysmlTransitionNode`). Either side may
  be a dotted feature chain, resolved the same way as `Connect`; recorded only when both the
  source and target resolve — an implied/omitted source produces no edge.
- `Expose` — a view usage's resolved exposed-name reference (`expose <name>;`, valid only inside
  a `view` usage's body), sourced from the view's qualified name and targeting each resolvable
  entry in `SysmlViewNode.ExposedNames`, one `Expose` edge per entry. `Expose` is the sole
  view-scoping edge kind: `GeneralViewLayoutStrategy` scopes its diagram to the union of every
  `Expose` edge's target containment subtree; a view with no `Expose` edges renders the full
  workspace, unchanged from the pre-scoping baseline. `SysmlViewNode.RenderTargetName` (a
  rendering-style/format selector, e.g. `asTreeDiagram`) is captured but never resolved into an
  edge and has no effect on scope.
- `Redefinition` — a feature redefinition reference (`redefines X;` / `:>> X`), sourced from the
  redefining feature's own qualified name (`SysmlFeatureNode.RedefinedFeatureName`) and
  targeting the resolved redefined-feature reference. Rendered by `GeneralViewLayoutStrategy` as
  a solid line with a hollow-triangle-crossbar end marker at the owning definition of the
  redefined feature.

`SysmlEdge` is a sealed positional record with three properties:

- `SourceQualifiedName` (`string?`) — qualified name of the referencing node, or `null` when
  the referencing node is anonymous (e.g., an unnamed import statement).
- `TargetQualifiedName` (`string`) — qualified name of the resolved target symbol.
- `Kind` (`SysmlEdgeKind`) — the kind of reference this edge represents.

##### Error Handling

N/A — `SysmlEdge` is a pure data record with no logic or validation.

##### Dependencies

- No external dependencies. Public types within the `Semantic.Model` namespace.

##### Callers

- `ReferenceResolver` — constructs `SysmlEdge` instances for each resolved reference and
  attaches them to `SysmlNode.ResolvedEdges`.
- `SemanticIndex` — indexes a collection of `SysmlEdge` instances by source and target
  qualified name.
