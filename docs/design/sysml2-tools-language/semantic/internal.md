### Semantic Internal Subsystem

#### Overview

The Semantic Internal subsystem provides the implementation details of the semantic loading pipeline.
It contains seven units: `AstBuilder`, `SymbolTable`, `ReferenceResolver`, `SupertypeWalker`,
`SysmlEdge`, `SemanticIndex`, and `SysmlAnnotation`.

#### Interfaces

**`AstBuilder.Build(RootNamespaceContext)`**: Transforms the ANTLR4 CST root into a typed AST root.

- *Type*: In-process .NET internal method.
- *Role*: Provider.
- *Contract*: Accepts a `SysMLv2Parser.RootNamespaceContext`; returns `SysmlPackageNode?` —
  the root package node, or `null` if the root contains no named elements.

**`SymbolTable.RegisterAll(SysmlNode?)`**: Registers all named nodes from an AST root.

- *Type*: In-process .NET internal method.
- *Role*: Provider.
- *Contract*: Traverses the AST depth-first and inserts each non-null `QualifiedName` into
  the symbol dictionary. Duplicate names are silently ignored.

**`ReferenceResolver.ResolveAll(IEnumerable<(string, SysmlNode?)>)`**: Runs import-cycle detection
and supertype/typing/import reference resolution over all loaded file roots.

- *Type*: In-process .NET internal method.
- *Role*: Provider.
- *Contract*: Accepts a list of `(FilePath, Root)` pairs; emits Warning diagnostics for
  unresolved supertype, typing, and import references and for circular import chains; attaches
  resolved `SysmlEdge` entries to each node's `ResolvedEdges`; returns a `SemanticIndex` over
  all resolved edges.

**`SupertypeWalker.WalkAll()`**: Traverses all specialization chains to detect cyclic specialization.

- *Type*: In-process .NET internal method.
- *Role*: Provider.
- *Contract*: Iterates all symbols in the `SymbolTable`; emits Warning diagnostics for any
  cycle detected.

**`SemanticIndex.GetOutgoingEdges(string)` / `GetIncomingEdges(string)`**: Reverse-lookup queries
over resolved edges.

- *Type*: In-process .NET public methods.
- *Role*: Provider.
- *Contract*: Each accepts a qualified name and returns an `IReadOnlyList<SysmlEdge>` — the
  edges originating from (outgoing) or targeting (incoming) that name, or an empty list when
  none are recorded.

#### Design

| Unit | Responsibility |
| --- | --- |
| `AstBuilder` | Visits ANTLR4 CST; builds typed AST nodes with qualified names and supertype lists |
| `SymbolTable` | Registry mapping fully-qualified names to their AST nodes |
| `ReferenceResolver` | Resolves supertype/typing/import refs; detects circular imports; builds a `SemanticIndex` |
| `SupertypeWalker` | Walks specialization chains; detects cyclic specialization |
| `SysmlEdge` | Public record modeling one resolved directed reference (Supertype/Typing/Import) |
| `SemanticIndex` | Public reverse-lookup index over resolved `SysmlEdge` instances |
| `SysmlAnnotation` | Public record modeling one captured `comment`/`doc` annotation (Comment/Documentation) |

Interaction sequence:

1. `WorkspaceLoader` creates one `AstBuilder` per file and calls `Build(rootNamespaceContext)`.
2. The returned `SysmlPackageNode` root is passed to `SymbolTable.RegisterAll`.
3. After all files are registered, `ReferenceResolver.ResolveAll` traverses all user-file AST
   roots, attaches `SysmlEdge` entries to each node's `ResolvedEdges`, and returns a
   `SemanticIndex` over all resolved edges.
4. Finally, `SupertypeWalker.WalkAll` iterates over all symbols in the table.
