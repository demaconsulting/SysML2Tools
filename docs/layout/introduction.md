# Introduction

This document is the technical reference for the SysML2Tools layout pipeline. It
describes the algorithms used to place nodes, route connectors, and produce readable
diagrams from a SysML v2 model. It is intended for contributors extending or maintaining
the layout subsystem and for reviewers evaluating algorithmic correctness.

The layout pipeline converts a parsed SysML v2 workspace into a `LayoutTree` — an
intermediate representation of positioned boxes, routed lines, and labels — which the SVG
and PNG renderers then paint. The pipeline is responsible for every spatial decision: where
nodes sit, how connectors route between them, and how much space separates them.

## Purpose

This reference documents the layout algorithms so that contributors can extend or maintain
them confidently and reviewers can verify their correctness. It records the algorithms the
code actually implements, the order in which they run, and the responsibilities of each
stage. It exists as design and review evidence for the layout subsystem.

## Scope

This document covers the node-link graph-drawing pipeline (the layered, Sugiyama-style
algorithm and its stages), the wrappers that handle disconnected and nested graphs, and the
mapping from each SysML view to those algorithms. It also covers the bespoke structured
layouts (Sequence, Grid, Browser) that are not node-link graph-drawing problems.

The following are explicitly out of scope: CLI usage and output-format selection (see the
*User Guide*), per-component class and interface design (see the design documentation under
`docs/design/`), and test scenarios and acceptance criteria (see the verification
documentation under `docs/verification/`). Rendering concerns — theme colors, fonts, and the
intrinsic notation geometry of end markers and ports — are owned by the rendering subsystem
and are referenced here only where they constrain layout.

## References

- [REF-1] Sugiyama, K., Tagawa, S., Toda, M. (1981). "Methods for Visual Understanding of
  Hierarchical System Structures." *IEEE Transactions on Systems, Man, and Cybernetics*,
  11(2), 109–125.
- [REF-2] Eades, P., Lin, X., Smyth, W. F. (1993). "A Fast and Effective Heuristic for the
  Feedback Arc Set Problem." *Information Processing Letters*, 47(6), 319–323.
- [REF-3] Gansner, E. R., Koutsofios, E., North, S. C., Vo, K.-P. (1993). "A Technique for
  Drawing Directed Graphs." *IEEE Transactions on Software Engineering*, 19(3), 214–230.
- [REF-4] Brandes, U., Köpf, B. (2002). "Fast and Simple Horizontal Coordinate Assignment."
  In *Graph Drawing (GD 2001)*, LNCS 2265, 31–44.
- [REF-5] OMG Systems Modeling Language (SysML) version 2.0, Object Management Group.

---
