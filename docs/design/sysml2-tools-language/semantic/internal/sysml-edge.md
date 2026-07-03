#### SysmlEdge

##### Overview

`SysmlEdge` and `SysmlEdgeKind` model a single resolved directed reference between two
qualified names in the semantic model. Edges are produced by `ReferenceResolver` while
walking supertype, feature-typing, and import references, and are the raw material indexed
by `SemanticIndex`.

##### Types

`SysmlEdgeKind` is an enum with three members:

- `Supertype` — a specialization reference (`SupertypeNames` / `specializes` / `:>`).
- `Typing` — a feature typing reference (`SysmlFeatureNode.FeatureTyping`, the type after `:`).
- `Import` — an import reference (`ImportedNames` / `import X::Y` or `import X::*`).

`SysmlEdge` is a sealed positional record with three properties:

- `SourceQualifiedName` (`string?`) — qualified name of the referencing node, or `null` when
  the referencing node is anonymous (e.g., an unnamed import statement).
- `TargetQualifiedName` (`string`) — qualified name of the resolved target symbol.
- `Kind` (`SysmlEdgeKind`) — the kind of reference this edge represents.

##### Error Handling

N/A — `SysmlEdge` is a pure data record with no logic or validation.

##### Dependencies

- No external dependencies. Public types within the `Semantic.Internal` namespace.

##### Callers

- `ReferenceResolver` — constructs `SysmlEdge` instances for each resolved reference and
  attaches them to `SysmlNode.ResolvedEdges`.
- `SemanticIndex` — indexes a collection of `SysmlEdge` instances by source and target
  qualified name.
