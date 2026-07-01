# Scalability and Large Models

The layered pipeline is deterministic and runs in low-order polynomial time, so its cost is
predictable as models grow. The dominant costs by stage, for a graph of `n` nodes and `m` edges:

- **Cycle breaking** and **layer assignment** are single graph traversals, `O(n + m)`.
- **Long-edge splitting** adds one dummy node per intermediate layer an edge crosses; the augmented
  graph stays linear in the original size plus the total edge span.
- **Crossing minimization** runs a fixed number of sweeps (four), each sorting every layer by
  barycenter, so it is `O(sweeps · (m + n log n))` — bounded rather than run to convergence, which
  also removes any risk of non-terminating oscillation.
- **Coordinate assignment** computes four linear alignments and combines them, so it stays close to
  linear in the augmented graph size.
- **Orthogonal routing** is the highest-order stage: within each inter-layer channel it compares
  sub-edge pairs to order their routing slots, which is quadratic in the number of sub-edges in that
  channel. Because sub-edges are partitioned per channel, this is far below `m²` overall for typical
  diagrams, where edges spread across many channels.
- **Port distribution**, **long-edge joining**, and the **axis transform** are all linear passes.

Two structural features keep large models tractable:

- **Component packing**: a disconnected model is split into connected components and each is laid out
  independently, so cost scales with the size of each component rather than the whole graph, and the
  layered stages never pay to relate unrelated subgraphs. The laid-out components are then packed onto
  shelves in linear time.
- **Recursive nesting**: a container's interior is laid out once, bottom-up, and thereafter treated as
  a single fixed-size node by its parent. Each nesting level therefore lays out only its own direct
  children, bounding the cost of deeply nested structures.

All stages are free of randomness and iterative relaxation, so runtime and output are reproducible:
the same model always produces the same diagram in the same time.

---
