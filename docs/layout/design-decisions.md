# Resolved Design Decisions

The following records the significant design choices behind the current layered layout
architecture, and why each was resolved as it was. These are contributor working notes; the
resolutions are reflected in the algorithm chapters above.

1. **Layered pipeline over force-directed placement** — *Resolved: layered.* The layout engine is
   a layered (Sugiyama-style) pipeline rather than a force-directed simulation. Force-directed
   placement commits node coordinates before routing, which leaves connector routing to reconcile
   overlaps after the fact and cannot guarantee that two connectors avoid the same segment. A
   layered pipeline assigns discrete layers and orderings first, so routing operates on a structured
   graph where slot assignment makes segment conflicts impossible by construction. This replaced the
   earlier force-directed architecture entirely.

2. **Independent implementation, cited by source** — *Resolved: implement the algorithms directly.*
   The pipeline implements established graph-drawing algorithms — layered assignment, barycenter
   crossing reduction, Brandes-Köpf coordinate assignment, and slot-based orthogonal routing — as
   this project's own code, cited to their original publications. The overall structure is similar to
   the Eclipse Layout Kernel's layered algorithm, but the implementation is independent and carries no
   external layout dependency.

3. **Single canonical orientation plus a final transform** — *Resolved: compute in RIGHT, map at the
   end.* Every stage computes in one canonical left-to-right orientation, and a single Axis Transform
   stage maps the result onto the requested direction (`RIGHT`, `DOWN`, `LEFT`, `UP`). This keeps all
   eight upstream stages direction-agnostic and shares one implementation across all four directions,
   rather than special-casing direction throughout the pipeline.

4. **Cycle breaking by greedy reversal** — *Resolved: greedy depth-first heuristic.* Rather than
   solving the minimum feedback arc set exactly, a depth-first traversal reverses edges that point to
   a node still on the recursion stack. This is fast, deterministic, and reverses a small edge set;
   the original orientation is restored when connectors are emitted so arrowheads still point the
   right way.

5. **Longest-path layering** — *Resolved: longest-path.* Layers are assigned by longest path from the
   sources in a single topological pass. It is linear, deterministic, and yields the minimum number of
   layers consistent with edge direction, which is sufficient for these diagrams.

6. **Fixed barycenter sweep budget** — *Resolved: a fixed number of sweeps.* Crossing reduction runs a
   fixed count of alternating barycenter sweeps rather than iterating to convergence, because
   barycenter ordering can oscillate between two equal-crossing orderings. A fixed budget guarantees
   termination and deterministic output while capturing most of the achievable crossing reduction.

7. **Brandes-Köpf coordinate assignment** — *Resolved: balanced four-alignment placement.* Within-layer
   positions use the Brandes-Köpf balanced algorithm, which aligns nodes into blocks through their
   median neighbors and averages four candidate alignments. It favors straight edges and compact,
   aligned columns without letting any single alignment dominate.

8. **Slot-based routing decoupled from placement** — *Resolved: assign slots before committing bends.*
   Each inter-layer channel assigns connectors to distinct routing slots by topological numbering over
   segment-crossing dependencies, so connectors that would otherwise share a vertical line are
   separated by construction. Decoupling slot assignment from placement is what makes shared-segment
   conflicts structurally impossible — the defect the previous architecture could not eliminate.

9. **Component packing as a wrapper stage** — *Resolved: pack disconnected components.* Disconnected
   graphs are split into connected components, laid out independently, and packed onto shelves, instead
   of being forced through shared layers (which stacks unrelated subgraphs into one tall column). A
   connected graph is a transparent pass-through, so the wrapper never changes single-component output.

10. **Recursion driven at the strategy level** — *Resolved: strategy-level, pipeline mode reserved.*
    Nested containers are laid out bottom-up by the Interconnection View strategy, which detects
    containers from the semantic model, lays out each interior with the flat pipeline, and treats the
    container as an atomic node in its parent. The pipeline exposes a `Recursive` hierarchy-handling
    value as reserved scaffolding,     but it is intentionally left inactive because container detection is a
    model concern the model-independent engine cannot perform.

11. **Single-sourced notation geometry** — *Resolved: centralize in shared metric types.* Intrinsic
    notation geometry (end-marker shapes and sizes, port squares, and related constants) is single-
    sourced in the rendering subsystem's notation-metrics type, and layout spacing constants are
    single-sourced in the layered-layout metrics type, so a geometry literal never appears in more than
    one place.

---
