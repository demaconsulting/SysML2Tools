# Semantic Internal Subsystem

## Overview

The Semantic Internal subsystem provides the implementation details of the semantic loading pipeline.
It contains four units: `AstBuilder`, `SymbolTable`, `ReferenceResolver`, and `SupertypeWalker`.

## Units

| Unit | Responsibility |
| --- | --- |
| `AstBuilder` | Visits ANTLR4 CST; builds typed AST nodes with qualified names and supertype lists |
| `SymbolTable` | Registry mapping fully-qualified names to their AST nodes |
| `ReferenceResolver` | Checks supertype references; detects circular import chains |
| `SupertypeWalker` | Walks specialization chains; detects cyclic specialization |

## Interaction Model

1. `WorkspaceLoader` creates one `AstBuilder` per file and calls `Build(rootNamespaceContext)`.
2. The returned `SysmlPackageNode` root is passed to `SymbolTable.RegisterAll`.
3. After all files are registered, `ReferenceResolver.ResolveAll` traverses all AST roots.
4. Finally, `SupertypeWalker.WalkAll` iterates over all symbols in the table.
