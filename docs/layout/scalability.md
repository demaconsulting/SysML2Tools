# Scalability and Large Models

For very large views the exact pipeline is bounded:

- **Sparse affinity**: affinity is built as an adjacency list from edge/membership/
  supertype lists in O(m); the dense n² matrix is never materialised (Step 1).
- **Barnes–Hut repulsion**: the O(n²) per-iteration repulsion in Step 3 is replaced by an
  O(n log n) quadtree approximation above a node-count threshold.
- **Cluster-first fallback**: above `N_max` (≈ 300 blocks per view), communities are laid
  out as super-nodes, then each community's interior is laid out independently and
  stitched, bounding total cost while preserving locality.
- **Container recursion**: the pipeline recurses into folder/package containers (via
  `ContainmentPacker` emitting compressor-compatible gaps), so a container's interior is
  compressed at the same density as the top level, with cross-boundary edges meeting at
  container-boundary ports ordered by the external corridor.
