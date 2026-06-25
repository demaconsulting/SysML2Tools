# SupertypeWalker

## Overview

`SupertypeWalker` traverses the specialization chains of all symbols registered in the
`SymbolTable` to detect cyclic specialization (e.g., A specializes B, B specializes A).

## Algorithm

`WalkAll` iterates over all symbols and for each unvisited symbol calls `WalkNode`. `WalkNode`
maintains two sets:

- `chainVisited` — names in the current DFS path (used to detect cycles in this chain).
- `globalVisited` — all visited names across all chains (used to avoid redundant processing).

For each supertype name in the current node:

- If the name is in `chainVisited`, a cyclic specialization warning is emitted and the loop
  continues (the cycle is not traversed further).
- If the supertype is registered in the symbol table and not yet globally visited, `WalkNode`
  is called recursively with a copy of `chainVisited`.

## Warning Format

Cyclic specialization warnings use `DiagnosticSeverity.Warning` with an empty `FilePath`
(since the cycle spans multiple potential source files).
