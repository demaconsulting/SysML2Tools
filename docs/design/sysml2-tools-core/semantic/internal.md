### Semantic Internal Subsystem

#### Overview

The Semantic Internal subsystem provides the implementation details of the semantic loading pipeline.
It contains four units: `AstBuilder`, `SymbolTable`, `ReferenceResolver`, and `SupertypeWalker`.

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
and supertype reference resolution over all loaded file roots.

- *Type*: In-process .NET internal method.
- *Role*: Provider.
- *Contract*: Accepts a list of `(FilePath, Root)` pairs; emits Warning diagnostics for
  unresolved supertype names and for circular import chains.

**`SupertypeWalker.WalkAll()`**: Traverses all specialization chains to detect cyclic specialization.

- *Type*: In-process .NET internal method.
- *Role*: Provider.
- *Contract*: Iterates all symbols in the `SymbolTable`; emits Warning diagnostics for any
  cycle detected.

#### Design

| Unit | Responsibility |
| --- | --- |
| `AstBuilder` | Visits ANTLR4 CST; builds typed AST nodes with qualified names and supertype lists |
| `SymbolTable` | Registry mapping fully-qualified names to their AST nodes |
| `ReferenceResolver` | Checks supertype references; detects circular import chains |
| `SupertypeWalker` | Walks specialization chains; detects cyclic specialization |

Interaction sequence:

1. `WorkspaceLoader` creates one `AstBuilder` per file and calls `Build(rootNamespaceContext)`.
2. The returned `SysmlPackageNode` root is passed to `SymbolTable.RegisterAll`.
3. After all files are registered, `ReferenceResolver.ResolveAll` traverses all AST roots.
4. Finally, `SupertypeWalker.WalkAll` iterates over all symbols in the table.
